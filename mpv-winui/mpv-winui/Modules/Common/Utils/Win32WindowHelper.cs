using Microsoft.UI.Xaml;
using System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace mpv_winui.Modules.Common.Utils
{
    internal class Win32WindowHelper
    {
        public static nint GetHwnd(Window window) => WinRT.Interop.WindowNative.GetWindowHandle(window);

        public static void SetForegroundWindow(Window window)
        {
            PInvoke.SetForegroundWindow(new HWND(GetHwnd(window)));
        }

        /// <summary>
        /// Show a window WITHOUT taking the foreground: it becomes visible and is laid out
        /// normally, and `GetForegroundWindow()` keeps pointing at whatever had it.
        ///
        /// Needed because `Window.Activate()` is the only thing that makes a WinUI window appear
        /// at launch -- skipping it leaves no window at all (measured: the automation harness
        /// then times out with "no window after 20s"). So an automated launch has to show the
        /// window, and must do it without stealing the foreground from whatever the user is
        /// running: a game in exclusive fullscreen drops its display mode and its controller the
        /// moment a window takes the foreground.
        /// </summary>
        public static void ShowNoActivate(Window window)
        {
            PInvoke.ShowWindow(new HWND(GetHwnd(window)), SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
        }

        /// <summary>
        /// A minimized window still reports AppWindow.IsVisible, so callers
        /// skip the Show() branch and SetForegroundWindow only lights up the
        /// taskbar entry — restore it explicitly first.
        /// </summary>
        public static void RestoreIfMinimized(Window window)
        {
            var hwnd = new HWND(GetHwnd(window));
            if (PInvoke.IsIconic(hwnd))
            {
                PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_RESTORE);
            }
        }

        private static void ShowWindow(Window window, int value)
        {
            PInvoke.ShowWindow(new HWND(GetHwnd(window)), (SHOW_WINDOW_CMD)value);
        }

        public static HMONITOR GetMonitor(Window window)
        {
            var hwnd = new HWND(GetHwnd(window));
            return PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        }

        public static uint GetDisplayFrequency(HMONITOR hMonitor, uint value = 60)
        {
            if (hMonitor == IntPtr.Zero)
            {
                return value;
            }

            unsafe
            {
                MONITORINFOEXW info = new()
                {
                };
                info.monitorInfo.cbSize = (uint)sizeof(MONITORINFOEXW);

                if (!PInvoke.GetMonitorInfo(hMonitor, (MONITORINFO*)&info))
                {
                    return value;
                }

                DEVMODEW dm = new()
                {
                    dmSize = (ushort)sizeof(DEVMODEW)
                };

                if (!PInvoke.EnumDisplaySettings(info.szDevice.ToString(), ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS, ref dm))
                {
                    return value;
                }

                return dm.dmDisplayFrequency;
            }
        }
    }
}
