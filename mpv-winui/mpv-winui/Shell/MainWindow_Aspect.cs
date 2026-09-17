using Microsoft.UI.Windowing;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;

namespace mpv_winui
{
    /// <summary>
    /// Main-window aspect-ratio handling.
    ///
    /// Two halves:
    ///  1. <see cref="AdjustMainSizingRect"/> shapes the native WM_SIZING drag
    ///     rectangle while the user drag-resizes, so the window keeps the video
    ///     aspect (the "固定长宽比" mode of
    ///     <c>AppSettings.WindowAspectRatioLock</c>).
    ///  2. <see cref="ApplyVideoAspectToWindow"/> re-fits the window when a
    ///     video (or a video with a different aspect) loads, so the lock also
    ///     holds on open - previously it only shaped manual drags, which is why
    ///     the window could open at a ratio that did not match the video at all.
    ///
    /// The ratio is applied to the CLIENT area, not the whole window rectangle:
    /// with the window rectangle the title bar and borders eat into the height,
    /// so even a "locked" window showed thin letterbox bars.
    ///
    /// Snapping, maximize and fullscreen deliberately bypass the lock: they are
    /// not free-form drags and the user expects them to fill the screen.
    /// </summary>
    public sealed partial class MainWindow
    {
        /// <summary>Aspect of the loaded video (published by MpvPlayerPage).</summary>
        public static double CurrentVideoAspect { get; set; } = 16.0 / 9.0;

        /// <summary>False while no video is loaded: a blank window resizes freely.</summary>
        public static bool HasActiveVideo { get; set; }

        /// <summary>
        /// Physical size of the video swap-chain panel, published by
        /// MpvPlayerPage. The aspect fit must target THIS, not the window
        /// client: the page's Grid also carries the menu bar and the control
        /// bar rows, so the panel is shorter than the client — fitting the
        /// client to 16:9 leaves the panel wider than 16:9 and mpv pillarboxes
        /// the picture inside it (the "black bars on both sides").
        /// </summary>
        public static double VideoPanelPhysicalWidth { get; set; }
        public static double VideoPanelPhysicalHeight { get; set; }

        /// <summary>
        /// Static entry point so callers do not have to reach the main window
        /// through <see cref="App.Window"/> (which is typed <c>Window?</c> and
        /// can be null/unset while media info is already arriving).
        /// </summary>
        public static void ApplyVideoAspectToActiveWindow(bool force = false)
        {
            if (_aspectSelfReference?.TryGetTarget(out var self) == true)
            {
                self.ApplyVideoAspectToWindow(force);
            }
        }

        private static WeakReference<MainWindow>? _aspectSelfReference;
        private RECT _sizingAnchorRect;
        private int _chromeWidth;
        private int _chromeHeight;
        private const int AspectSubclassId = 52122;

        /// <summary>True between WM_ENTERSIZEMOVE and WM_EXITSIZEMOVE.</summary>
        public static bool IsDragResizing { get; private set; }

        /// <summary>Ignores aspect deviations below this fraction (rounding and DPI noise).</summary>
        private const double FitTolerance = 0.01;

        private void InstallAspectRatioSubclass()
        {
            _aspectSelfReference = new(this);
            unsafe
            {
                var hwnd = new HWND(WindowNative.GetWindowHandle(this));
                PInvoke.SetWindowSubclass(hwnd, &MainAspectSubclassProc, AspectSubclassId, 0);
            }
            AppContext.SettingChanged += Aspect_SettingChanged;
        }

        private void RemoveAspectRatioSubclass()
        {
            AppContext.SettingChanged -= Aspect_SettingChanged;
            unsafe
            {
                var hwnd = new HWND(WindowNative.GetWindowHandle(this));
                PInvoke.RemoveWindowSubclass(hwnd, &MainAspectSubclassProc, AspectSubclassId);
            }
            _aspectSelfReference = null;
        }

        /// <summary>Turning the lock on should fix the current window immediately, not after the next drag.</summary>
        private void Aspect_SettingChanged(string key, object? value)
        {
            if (key != nameof(AppContext.AppSetting.WindowAspectRatioLock))
            {
                return;
            }

            if (value is true)
            {
                DispatcherQueue.TryEnqueue(() => ApplyVideoAspectToWindow(force: true));
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static LRESULT MainAspectSubclassProc(
            HWND hWnd,
            uint uMsg,
            WPARAM wParam,
            LPARAM lParam,
            nuint uIdSubclass,
            nuint dwRefData)
        {
            const int WM_SIZING = 0x0214;
            const int WM_ENTERSIZEMOVE = 0x0231;
            const int WM_EXITSIZEMOVE = 0x0232;

            if (uMsg == WM_ENTERSIZEMOVE
                && _aspectSelfReference?.TryGetTarget(out var sizingSelf) == true)
            {
                IsDragResizing = true;
                // 拖动期间摘掉 VSR/HDR 滤镜（脚本观察该 user-data）：
                // d3d11vpp 让每次链路重配置贵得多，且拖动中反复重配置正是卡顿来源。
                AppContext.SendMpvCommand("no-osd set user-data/mpvw/window-resizing yes");

                // The rect the drag started from: corner drags are resolved
                // against it to decide which axis the user is actually moving.
                PInvoke.GetWindowRect(hWnd, out sizingSelf._sizingAnchorRect);

                // Chrome = client area minus the video panel (menu bar + control
                // bar rows). It is layout-fixed, so measure it once here and use
                // it for the whole drag: the aspect constraint must hold for the
                // PANEL, otherwise the bars come back as soon as dragging starts.
                var startFrame = sizingSelf.MeasureFrame();
                sizingSelf._chromeWidth = Math.Max(0,
                    (sizingSelf._sizingAnchorRect.right - sizingSelf._sizingAnchorRect.left)
                    - startFrame.Width - (int)Math.Round(VideoPanelPhysicalWidth));
                sizingSelf._chromeHeight = Math.Max(0,
                    (sizingSelf._sizingAnchorRect.bottom - sizingSelf._sizingAnchorRect.top)
                    - startFrame.Height - (int)Math.Round(VideoPanelPhysicalHeight));
            }
            else if (uMsg == WM_EXITSIZEMOVE && IsDragResizing)
            {
                IsDragResizing = false;
                AppContext.SendMpvCommand("no-osd set user-data/mpvw/window-resizing no");
            }

            if (uMsg == WM_SIZING
                && _aspectSelfReference?.TryGetTarget(out var self) == true)
            {
                var rect = Marshal.PtrToStructure<RECT>((nint)lParam.Value);
                if (self.AdjustMainSizingRect((int)wParam.Value, ref rect))
                {
                    Marshal.StructureToPtr(rect, (nint)lParam.Value, false);
                    return (LRESULT)1;
                }
            }

            return PInvoke.DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        /// <summary>
        /// Constrains the WM_SIZING drag rectangle to the video aspect, keeping
        /// the edge/corner opposite the dragged one fixed.
        /// </summary>
        private bool AdjustMainSizingRect(int edge, ref RECT rect)
        {
            const int WMSZ_LEFT = 1;
            const int WMSZ_RIGHT = 2;
            const int WMSZ_TOP = 3;
            const int WMSZ_TOPLEFT = 4;
            const int WMSZ_TOPRIGHT = 5;
            const int WMSZ_BOTTOM = 6;
            const int WMSZ_BOTTOMLEFT = 7;
            const int WMSZ_BOTTOMRIGHT = 8;

            if (!AppContext.AppSetting.WindowAspectRatioLock || !HasActiveVideo)
            {
                return false;
            }

            var aspect = CurrentVideoAspect > 0 ? CurrentVideoAspect : 16.0 / 9.0;

            var frame = MeasureFrame();
            int proposedW = (rect.right - rect.left) - frame.Width - _chromeWidth;
            int proposedH = (rect.bottom - rect.top) - frame.Height - _chromeHeight;
            if (proposedW <= 0 || proposedH <= 0)
            {
                return false;
            }

            double clientW;
            double clientH;
            var anchorCx = (_sizingAnchorRect.left + _sizingAnchorRect.right) / 2;
            var anchorCy = (_sizingAnchorRect.top + _sizingAnchorRect.bottom) / 2;
            if (anchorCx == 0 && anchorCy == 0)
            {
                // No anchor captured (e.g. keyboard sizing): use the proposed rect.
                anchorCx = (rect.left + rect.right) / 2;
                anchorCy = (rect.top + rect.bottom) / 2;
            }
            if (edge is WMSZ_LEFT or WMSZ_RIGHT)
            {
                clientW = proposedW;
                clientH = clientW / aspect;
            }
            else if (edge is WMSZ_TOP or WMSZ_BOTTOM)
            {
                clientH = proposedH;
                clientW = clientH * aspect;
            }
            else if (edge is WMSZ_TOPLEFT or WMSZ_TOPRIGHT or WMSZ_BOTTOMLEFT or WMSZ_BOTTOMRIGHT)
            {
                // Corners: follow whichever axis the user moved further, measured
                // against the rect the drag started from. Deriving the height from
                // the width - as the old code did for every corner - made dragging
                // a corner vertically a no-op, because the width never changed.
                var anchorW = (_sizingAnchorRect.right - _sizingAnchorRect.left) - frame.Width - _chromeWidth;
                var anchorH = (_sizingAnchorRect.bottom - _sizingAnchorRect.top) - frame.Height - _chromeHeight;
                var dx = Math.Abs(proposedW - anchorW);
                var dy = Math.Abs(proposedH - anchorH);
                if (dy > dx)
                {
                    clientH = proposedH;
                    clientW = clientH * aspect;
                }
                else
                {
                    clientW = proposedW;
                    clientH = clientW / aspect;
                }
            }
            else
            {
                // Unknown edge: keep the OS proposal's width and derive the height.
                clientW = proposedW;
                clientH = clientW / aspect;
            }

            // Clamp to the window minimums, converted to panel space, and
            // re-derive the other axis so the ratio survives the clamp.
            var minW = MinPhysicalWidth() - frame.Width - _chromeWidth;
            var minH = MinPhysicalHeight() - frame.Height - _chromeHeight;
            if (minW <= 0 || minH <= 0)
            {
                return false;
            }
            if (clientW < minW)
            {
                clientW = minW;
                clientH = clientW / aspect;
            }
            if (clientH < minH)
            {
                clientH = minH;
                clientW = clientH * aspect;
            }

            var newW = (int)Math.Round(clientW) + frame.Width + _chromeWidth;
            var newH = (int)Math.Round(clientH) + frame.Height + _chromeHeight;

            // Never exceed the screen, and never slide once a boundary is hit:
            // the available space is measured from the FIXED edge/corner (the
            // anchor), so clamping shrinks the window in place instead of
            // letting it slide. For edge drags the perpendicular axis is
            // centred on the opposite edge's midpoint, so its limit is twice
            // the distance from that midpoint to the nearest work-area edge.
            var work = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
            var anchorRight = edge is WMSZ_LEFT or WMSZ_TOPLEFT or WMSZ_BOTTOMLEFT;
            var anchorBottom = edge is WMSZ_TOP or WMSZ_TOPLEFT or WMSZ_TOPRIGHT;

            int axisWLimit = int.MaxValue;
            int axisHLimit = int.MaxValue;
            double perpMaxW = double.MaxValue;
            double perpMaxH = double.MaxValue;
            if (edge is WMSZ_LEFT or WMSZ_RIGHT)
            {
                axisWLimit = (anchorRight ? rect.right - work.X : work.X + work.Width - rect.left)
                             - frame.Width - _chromeWidth;
                var midY = anchorCy > 0 ? anchorCy : (rect.top + rect.bottom) / 2;
                perpMaxH = 2.0 * Math.Min(midY - work.Y, work.Y + work.Height - midY)
                           - frame.Height - _chromeHeight;
            }
            else
            {
                axisHLimit = (anchorBottom ? rect.bottom - work.Y : work.Y + work.Height - rect.top)
                             - frame.Height - _chromeHeight;
                var midX = anchorCx > 0 ? anchorCx : (rect.left + rect.right) / 2;
                perpMaxW = 2.0 * Math.Min(midX - work.X, work.X + work.Width - midX)
                           - frame.Width - _chromeWidth;
            }

            var maxPanelW = Math.Min(axisWLimit, perpMaxW);
            var maxPanelH = Math.Min(axisHLimit, perpMaxH);
            if (maxPanelW < minW || maxPanelH < minH)
            {
                // No room left on this monitor for the ratio: stop growing.
                return false;
            }
            if (clientW > maxPanelW || clientH > maxPanelH)
            {
                var scale = Math.Min(1.0, Math.Min(maxPanelW / (double)clientW, maxPanelH / (double)clientH));
                clientW = Math.Floor(clientW * scale);
                clientH = Math.Floor(clientH * scale);
                newW = (int)Math.Round(clientW) + frame.Width + _chromeWidth;
                newH = (int)Math.Round(clientH) + frame.Height + _chromeHeight;
            }
            if (newW == (rect.right - rect.left) && newH == (rect.bottom - rect.top))
            {
                return false;
            }

            // Anchor rules (standard Windows + this app's convention):
            //   * dragging a CORNER  -> the opposite CORNER stays fixed;
            //   * dragging an EDGE   -> the opposite EDGE stays fixed, and the
            //     resize is centred on the MIDPOINT of that opposite edge
            //     (dragging the right border anchors on the midpoint of the
            //     left border, and so on for the other three edges).
            var isCorner = edge is WMSZ_TOPLEFT or WMSZ_TOPRIGHT or WMSZ_BOTTOMLEFT or WMSZ_BOTTOMRIGHT;

            if (isCorner)
            {
                var cornerAnchorRight = edge is WMSZ_TOPLEFT or WMSZ_BOTTOMLEFT;
                var cornerAnchorBottom = edge is WMSZ_TOPLEFT or WMSZ_TOPRIGHT;
                if (cornerAnchorRight)
                {
                    rect.left = rect.right - newW;
                }
                else
                {
                    rect.right = rect.left + newW;
                }
                if (cornerAnchorBottom)
                {
                    rect.top = rect.bottom - newH;
                }
                else
                {
                    rect.bottom = rect.top + newH;
                }
                return true;
            }

            if (edge is WMSZ_LEFT or WMSZ_RIGHT)
            {
                // The opposite (fixed) edge keeps its X; height is centred on
                // that edge's midpoint.
                if (edge == WMSZ_LEFT)
                {
                    rect.right = rect.left + newW;
                }
                else
                {
                    rect.left = rect.right - newW;
                }
                rect.top = anchorCy - (newH / 2);
                rect.bottom = rect.top + newH;
            }
            else
            {
                // WMSZ_TOP / WMSZ_BOTTOM: width centred on the opposite edge's
                // midpoint.
                if (edge == WMSZ_TOP)
                {
                    rect.bottom = rect.top + newH;
                }
                else
                {
                    rect.top = rect.bottom - newH;
                }
                rect.left = anchorCx - (newW / 2);
                rect.right = rect.left + newW;
            }
            return true;
        }

        /// <summary>
        /// Resizes the window so its client area matches the video aspect. Keeps
        /// the window's width (and top-left) and derives the height, falling back
        /// to deriving the width when the height would not fit the work area.
        /// Does nothing while maximized/minimized/fullscreen.
        /// </summary>
        public void ApplyVideoAspectToWindow(bool force = false)
        {
            // Diagnostics: this runs on every VIDEO_RECONFIG, so keep it cheap
            // but complete enough to explain a skipped fit in the log.
            void Trace(string why, object? extra = null)
            {
                if (AppContext.AppLogger.IsDebugEnabled)
                {
                    AppContext.AppLogger.Debug("aspect fit: {} (extra={})", why, extra);
                }
            }

            if (!AppContext.AppSetting.WindowAspectRatioLock)
            {
                Trace("skipped: lock disabled");
                return;
            }
            if (IsDragResizing)
            {
                // 拖动进行中：窗口尺寸由 WM_SIZING 的比例约束负责，fit 若在此期间
                // MoveAndResize 会和用户的手互相拉扯（表现为窗口越拖越大）。
                Trace("skipped: drag resizing in progress");
                return;
            }
            if (!HasActiveVideo)
            {
                Trace("skipped: no active video");
                return;
            }

            if (AppWindow.Presenter is not OverlappedPresenter presenter
                || presenter.State != OverlappedPresenterState.Restored)
            {
                // Maximized/minimized/fullscreen: leave the window alone.
                Trace("skipped: presenter not restored", AppWindow.Presenter?.Kind.ToString());
                return;
            }

            var aspect = CurrentVideoAspect > 0 ? CurrentVideoAspect : 16.0 / 9.0;
            var frame = MeasureFrame();
            var size = AppWindow.Size;
            var position = AppWindow.Position;

            // Fit the VIDEO PANEL, not the window client. The client is split
            // into menu row + video row + control row, so a 16:9 client leaves
            // the panel wider than 16:9 and mpv pillarboxes the picture inside
            // it. panel = client - extra, where "extra" is everything else.
            var panelW = VideoPanelPhysicalWidth;
            var panelH = VideoPanelPhysicalHeight;
            if (panelW <= 0 || panelH <= 0)
            {
                Trace("skipped: panel size unknown", $"{panelW}x{panelH}");
                return;
            }

            var clientW = size.Width - frame.Width;
            var clientH = size.Height - frame.Height;
            var extraW = clientW - panelW;
            var extraH = clientH - panelH;
            if (clientW <= 0 || clientH <= 0 || extraH < 0)
            {
                Trace("skipped: implausible geometry",
                    $"client={clientW}x{clientH} panel={panelW:0}x{panelH:0}");
                return;
            }

            // Keep the panel width (and therefore the window width); derive the
            // panel height from the video aspect, then add the chrome back.
            var targetPanelW = panelW;
            var targetPanelH = targetPanelW / aspect;
            var targetW = size.Width;
            var targetH = (int)Math.Round(targetPanelH + extraH) + frame.Height;

            var minH = MinPhysicalHeight();
            var minW = MinPhysicalWidth();
            if (targetH < minH)
            {
                targetH = minH;
                targetPanelH = targetH - frame.Height - extraH;
                targetPanelW = targetPanelH * aspect;
                targetW = (int)Math.Round(targetPanelW + extraW) + frame.Width;
                if (targetW < minW)
                {
                    targetW = minW;
                }
            }

            var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
            var maxH = Math.Max(minH, workArea.Height - 40);
            if (targetH > maxH)
            {
                targetH = maxH;
                targetPanelH = targetH - frame.Height - extraH;
                targetPanelW = targetPanelH * aspect;
                targetW = (int)Math.Round(targetPanelW + extraW) + frame.Width;
            }

            if (!force && Math.Abs(targetH - size.Height) <= Math.Max(2, size.Height * FitTolerance))
            {
                Trace("skipped: already within tolerance",
                    $"h={size.Height} target={targetH} aspect={aspect:0.###} panel={panelW:0}x{panelH:0} extraH={extraH:0}");
                return;
            }

            var x = position.X;
            var y = position.Y;
            var bottom = workArea.Y + workArea.Height - 20;
            if (y + targetH > bottom)
            {
                y = Math.Max(workArea.Y, bottom - targetH);
            }
            var right = workArea.X + workArea.Width - 20;
            if (x + targetW > right)
            {
                x = Math.Max(workArea.X, right - targetW);
            }

            Trace("applied", $"aspect={aspect:0.###} {size.Width}x{size.Height} -> {targetW}x{targetH} panel {panelW:0}x{panelH:0}->{targetPanelW:0}x{targetPanelH:0} extraH={extraH:0}");
            AppWindow.MoveAndResize(new RectInt32(x, y, targetW, targetH));
        }

        /// <summary>
        /// Non-client area (borders + title bar) of the current window, in the
        /// same physical units as the rects above. 0x0 when it cannot be measured.
        /// </summary>
        private RectInt32 MeasureFrame()
        {
            try
            {
            var handle = WindowNative.GetWindowHandle(this);
            if (handle == IntPtr.Zero)
            {
                return default;
            }
            var hwnd = new HWND(handle);

                PInvoke.GetWindowRect(hwnd, out var window);
                if (!PInvoke.GetClientRect(hwnd, out var client))
                {
                    return default;
                }

                var fw = (window.right - window.left) - client.right;
                var fh = (window.bottom - window.top) - client.bottom;
                if (fw < 0 || fh < 0)
                {
                    return default;
                }
                return new RectInt32(0, 0, fw, fh);
            }
            catch
            {
                return default;
            }
        }

        private int MinPhysicalWidth()
        {
            if (AppWindow.Presenter is OverlappedPresenter presenter
                && presenter.PreferredMinimumWidth is > 0)
            {
                return presenter.PreferredMinimumWidth.Value;
            }
            return 300;
        }

        private int MinPhysicalHeight()
        {
            if (AppWindow.Presenter is OverlappedPresenter presenter
                && presenter.PreferredMinimumHeight is > 0)
            {
                return presenter.PreferredMinimumHeight.Value;
            }
            return 200;
        }
    }
}
