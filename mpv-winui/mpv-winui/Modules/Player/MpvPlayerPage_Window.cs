using Microsoft.UI.Windowing;
using mpv_winrt;
using mpv_winui.Modules.Common.Utils;

namespace mpv_winui.Modules.Player
{
    public sealed partial class MpvPlayerPage
    {
        private void MpvPlayerPage_WindowChanged(MpvMediaPlayer player, WindowChangedEventArgs args)
        {
            DispatcherQueue.RunAsync(() =>
            {
                try
                {
                    switch (args.PropertyId)
                    {
                        case 201:
                            HandleFullscreenProperty(args.Value);
                            break;
                        case 202:
                            HandleOnTopProperty(args.Value);
                            break;
                        case 203:
                            HandleWindowMinimizedProperty(args.Value);
                            break;
                        case 204:
                            HandleWindowMaximizedProperty(args.Value);
                            break;
                        case 205:
                            HandleTitleBarProperty(args.Value);
                            break;
                        case 206:
                            HandleBorderProperty(args.Value);
                            break;
                    }
                }
                catch (System.Exception ex)
                {
                    OnException(ex);
                }
            });
        }

        private void HandleFullscreenProperty(bool fullscreen)
        {
            // State-based (not toggle-based): the mpv "fullscreen" property is
            // the source of truth, so "set fullscreen no" (ESC) and repeated
            // property events converge instead of flipping the presenter back.
            if (fullscreen == _isFullScreen)
            {
                return;
            }

            _isFullScreen = fullscreen;
            if (fullscreen)
            {
                if (_appWindow.Presenter.Kind != AppWindowPresenterKind.FullScreen)
                {
                    _appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
                }
                if (!_isFullWindow)
                {
                    PlayerControl.ToggleFullWindow();
                }
            }
            else
            {
                if (_appWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen)
                {
                    _appWindow.SetPresenter(AppWindowPresenterKind.Default);
                }
                if (_isFullWindow)
                {
                    PlayerControl.ToggleFullWindow();
                }
            }
            PlayerControl.UpdateFullScreen(fullscreen);
        }

        private void HandleOnTopProperty(bool enable)
        {
            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = enable;
            }
        }

        private void HandleWindowMinimizedProperty(bool minimized)
        {
            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                if (minimized)
                {
                    presenter.Minimize();
                }
                else
                {
                    presenter.Restore();
                }
            }
        }

        private void HandleWindowMaximizedProperty(bool maximized)
        {
            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                if (maximized)
                {
                    presenter.Maximize();
                }
                else
                {
                    presenter.Restore();
                }
            }
        }

        private void HandleTitleBarProperty(bool showTitleBar)
        {
            // mpv's title-bar property maps to the app's full-window state;
            // apply the requested state instead of blindly toggling.
            if (showTitleBar == _isFullWindow)
            {
                PlayerControl.ToggleFullWindow();
            }
        }

        private void HandleBorderProperty(bool hasBorder)
        {
            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(hasBorder, true);
            }
        }
    }
}
