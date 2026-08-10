using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace mpv_winui.Modules.Player
{
    public sealed partial class MpvPlayerPage
    {
        private bool _isFullScreen;
        private bool _isFullWindow;

        private void VideoArea_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            // In fullscreen/full-window the control bar overlays the video with
            // a gradient mask: the bar pops out while the pointer is inside the
            // mask and retracts when it leaves.
            if (!_isFullWindow && !_isFullScreen)
            {
                return;
            }

            if (sender is FrameworkElement area)
            {
                var position = e.GetCurrentPoint(area).Position;
                if (position.Y >= area.ActualHeight - 120)
                {
                    PlayerControl.ShowControlPanel();
                }
                else
                {
                    PlayerControl.HideControlPanel();
                }
            }
        }

        private bool PlayerControl_OnFullScreenRequest()
        {
            bool isFullScreen;

            if (_appWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen)
            {
                _appWindow.SetPresenter(AppWindowPresenterKind.Default);
            }
            else
            {
                _appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            }

            isFullScreen = _appWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;

            if (isFullScreen)
            {
                if (!_isFullWindow)
                {
                    PlayerControl.ToggleFullWindow();
                }
            }
            else
            {
                if (_isFullWindow)
                {
                    PlayerControl.ToggleFullWindow();
                }
            }

            _isFullScreen = isFullScreen;
            PlayerControl.UpdateFullScreen(isFullScreen);

            return isFullScreen;
        }

        private bool PlayerControl_OnFullWindowRequest()
        {
            bool isFullWindow = _isFullWindow;

            if (isFullWindow)
            {
                isFullWindow = !VisualStateManager.GoToState(this, "NormalWindow", true);
            }
            else
            {
                isFullWindow = VisualStateManager.GoToState(this, "FullWindow", true);
            }

            //TODO 
            if (App.Window is MainWindow window)
            {
                window.ChangeFullWindow(isFullWindow);
            }

            _isFullWindow = isFullWindow;
            PlayerControl.SetOverlayMode(isFullWindow);

            return isFullWindow;
        }
    }
}
