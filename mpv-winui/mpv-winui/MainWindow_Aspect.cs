using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;

namespace mpv_winui
{
    /// <summary>
    /// Main-window aspect-ratio lock: while the user drag-resizes the window
    /// (native SC_SIZE loop, WM_SIZING), the rect is adjusted to the current
    /// video aspect with the dragged edge's opposite side anchored. Snapping,
    /// maximize and programmatic resizes do not go through WM_SIZING and stay
    /// untouched, so the lock only shapes manual drags.
    /// </summary>
    public sealed partial class MainWindow
    {
        /// <summary>Aspect of the loaded video (published by MpvPlayerPage).</summary>
        public static double CurrentVideoAspect { get; set; } = 16.0 / 9.0;

        /// <summary>False while no video is loaded: a blank window resizes freely.</summary>
        public static bool HasActiveVideo { get; set; }

        private static WeakReference<MainWindow>? _aspectSelfReference;
        private RECT _sizingAnchorRect;
        private const int AspectSubclassId = 52122;

        private void InstallAspectRatioSubclass()
        {
            _aspectSelfReference = new(this);
            unsafe
            {
                var hwnd = new HWND(WinRT.Interop.WindowNative.GetWindowHandle(this));
                PInvoke.SetWindowSubclass(hwnd, &MainAspectSubclassProc, AspectSubclassId, 0);
            }
        }

        private void RemoveAspectRatioSubclass()
        {
            unsafe
            {
                var hwnd = new HWND(WinRT.Interop.WindowNative.GetWindowHandle(this));
                PInvoke.RemoveWindowSubclass(hwnd, &MainAspectSubclassProc, AspectSubclassId);
            }
            _aspectSelfReference = null;
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

            if (uMsg == WM_ENTERSIZEMOVE
                && _aspectSelfReference?.TryGetTarget(out var sizingSelf) == true)
            {
                PInvoke.GetWindowRect(hWnd, out sizingSelf._sizingAnchorRect);
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
            var width = rect.right - rect.left;
            var height = rect.bottom - rect.top;

            double w;
            double h;
            if (edge is WMSZ_LEFT or WMSZ_RIGHT)
            {
                w = width;
                h = w / aspect;
            }
            else if (edge is WMSZ_TOP or WMSZ_BOTTOM)
            {
                h = height;
                w = h * aspect;
            }
            else
            {
                // Corners: the OS proposal already reflects both deltas; keep
                // the proposed width and derive the height.
                w = width;
                h = w / aspect;
            }

            var minW = MinPhysicalWidth();
            var minH = MinPhysicalHeight();
            if (w < minW)
            {
                w = minW;
                h = w / aspect;
            }
            if (h < minH)
            {
                h = minH;
                w = h * aspect;
            }

            var newW = (int)Math.Round(w);
            var newH = (int)Math.Round(h);
            if (newW == width && newH == height)
            {
                return false;
            }

            // Dragging the left edge / left corners keeps the right side put;
            // dragging the top edge / top corners keeps the bottom side put.
            var anchorRight = edge is WMSZ_LEFT or WMSZ_TOPLEFT or WMSZ_BOTTOMLEFT;
            var anchorBottom = edge is WMSZ_TOP or WMSZ_TOPLEFT or WMSZ_TOPRIGHT;

            if (anchorRight)
            {
                rect.left = rect.right - newW;
            }
            else
            {
                rect.right = rect.left + newW;
            }
            if (anchorBottom)
            {
                rect.top = rect.bottom - newH;
            }
            else
            {
                rect.bottom = rect.top + newH;
            }
            return true;
        }

        private int MinPhysicalWidth()
        {
            if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter
                && presenter.PreferredMinimumWidth is > 0)
            {
                return presenter.PreferredMinimumWidth.Value;
            }
            return 300;
        }

        private int MinPhysicalHeight()
        {
            if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter
                && presenter.PreferredMinimumHeight is > 0)
            {
                return presenter.PreferredMinimumHeight.Value;
            }
            return 200;
        }
    }
}
