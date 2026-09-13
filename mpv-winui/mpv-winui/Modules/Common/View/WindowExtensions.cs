using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using mpv_winui.Modules.Common.Utils;
using WinRT;

namespace mpv_winui.Modules.Common.View
{
    public static class WindowExtensions
    {
        extension(Window window)
        {
            /// <summary>
            /// Shows the window (if hidden, e.g. after PiP hid the main window)
            /// and brings it to the foreground. SetForegroundWindow alone does
            /// not show a hidden window, which made re-activation a no-op.
            /// </summary>
            public void ShowWindow()
            {
                if (!window.AppWindow.IsVisible)
                {
                    window.AppWindow.Show();
                }
                Win32WindowHelper.RestoreIfMinimized(window);
                Win32WindowHelper.SetForegroundWindow(window);
            }

            public void SetWindowMinSize(double widthPx, double heightPx)
            {
                if (window.Content.XamlRoot is XamlRoot root && window.AppWindow.Presenter.Kind == AppWindowPresenterKind.Overlapped)
                {
                    var overlappedPresenter = window.AppWindow.Presenter.As<OverlappedPresenter>();
                    if (overlappedPresenter != null)
                    {
                        var scale = root.RasterizationScale > 0 ? root.RasterizationScale : 1;
                        overlappedPresenter.PreferredMinimumWidth = (int)(widthPx * scale);
                        overlappedPresenter.PreferredMinimumHeight = (int)(heightPx * scale);
                    }
                }
            }
        }
    }
}
