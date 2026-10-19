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
    /// <see cref="ApplyVideoAspectToWindow"/> re-fits the window when a video
    /// (or a video with a different aspect) loads, so
    /// <c>AppSettings.WindowAspectRatioLock</c> holds on open. That is the whole
    /// of it now: the lock used to also reshape the WM_SIZING drag rectangle, so
    /// the window kept the video's ratio while being dragged, and a drag of any
    /// single edge moved the opposite edge as well - which is the "dragging
    /// scales the whole window" behaviour that had to go. Dragging is now the
    /// plain Windows resize: one edge, the edge you grabbed.
    ///
    /// The subclass below therefore exists only to publish "a drag is in
    /// progress" (and to signal mpv to drop its VSR/HDR filters for the
    /// duration), so the aspect fit cannot fight the user's hand while they are
    /// dragging.
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
            const int WM_ENTERSIZEMOVE = 0x0231;
            const int WM_EXITSIZEMOVE = 0x0232;

            // Drags are deliberately NOT constrained: the window is resized one
            // edge at a time, the way Windows does it, and the aspect fit only
            // ever runs when a video opens (ApplyVideoAspectToWindow). A
            // WM_SIZING handler that rewrote the drag rectangle to the video
            // ratio used to live here, and from the outside it read as the whole
            // window scaling when any single edge was dragged.
            if (uMsg == WM_ENTERSIZEMOVE && _aspectSelfReference is not null)
            {
                IsDragResizing = true;
                // 拖动期间摘掉 VSR/HDR 滤镜（脚本观察该 user-data）：
                // d3d11vpp 让每次链路重配置贵得多，且拖动中反复重配置正是卡顿来源。
                AppContext.SendMpvCommand("no-osd set user-data/mpvw/window-resizing yes");
            }
            else if (uMsg == WM_EXITSIZEMOVE && IsDragResizing)
            {
                IsDragResizing = false;
                AppContext.SendMpvCommand("no-osd set user-data/mpvw/window-resizing no");
            }

            return PInvoke.DefSubclassProc(hWnd, uMsg, wParam, lParam);
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
                // 拖动进行中：此时由用户的手决定窗口尺寸（单边拖拽，不设比例约束），
                // fit 若在此期间 MoveAndResize 会和用户的手互相拉扯。
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

            // Fit the VIDEO PANEL, not the window client. The client carries
            // the window's chrome - title bar, navigation pane, tab strip and
            // the control bar - so a client that is 16:9 leaves the panel wider
            // than 16:9 and mpv pillarboxes the picture inside it.
            // panel = client - extra, where "extra" is everything else.
            //
            // "Everything else" is measured rather than listed, so the shell
            // gaining a pane and a tab strip needed no change here: the
            // residual simply grew. The one thing that does matter is that the
            // chrome stays put during a drag - see the PaneDisplayMode note in
            // MainWindow.xaml, which is why the pane is pinned open instead of
            // collapsing itself as the window narrows.
            var panelW = VideoPanelPhysicalWidth;
            var panelH = VideoPanelPhysicalHeight;
            if (panelW <= 0 || panelH <= 0)
            {
                // Also the library case: while the skeleton is covering the
                // video there is no aspect to fit. The panel itself stays
                // measured - it is never collapsed - so this only fires before
                // the first layout pass.
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
