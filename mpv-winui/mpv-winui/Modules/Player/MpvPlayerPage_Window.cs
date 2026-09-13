using Microsoft.UI.Windowing;
using mpv_winrt;
using mpv_winui.Modules.Common.Utils;

namespace mpv_winui.Modules.Player
{
    public sealed partial class MpvPlayerPage
    {
        private void MpvPlayerPage_WindowChanged(WindowChangedEventArgs args)
        {
            DispatcherQueue.RunAsync(() =>
            {
                try
                {
                    switch (args.PropertyId)
                    {
                        case 201:
                            SetFullScreen(args.Value);
                            break;
                        case 202:
                            SetAlwaysOnTop(args.Value);
                            break;
                        case 203:
                            SetWindowMinimized(args.Value);
                            break;
                        case 204:
                            SetWindowMaximized(args.Value);
                            break;
                        case 205:
                            SetFullWindow(args.Value);
                            break;
                        case 206:
                            SetWindowBorder(args.Value);
                            break;
                    }
                }
                catch (System.Exception ex)
                {
                    OnException(ex);
                }
            });
        }

        private void SetFullScreen(bool fullscreen)
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
            // Re-evaluate width-adaptive state after presenter transition:
            // without this, exiting fullscreen sometimes left only the slider
            // visible because _currentSegment was stale.
            PlayerControl.RefreshAdaptiveState();
        }

        private void SetAlwaysOnTop(bool enable)
        {
            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = enable;
            }
            if (TopBarOntopIcon is not null)
            {
                // Fluent pin glyphs: F602 = pin (unpinned), F604 = pin off (pinned).
                TopBarOntopIcon.Glyph = enable ? "\uF604" : "\uF602";
            }
            if (PlaylistOntopIcon is not null)
            {
                PlaylistOntopIcon.Glyph = enable ? "\uF604" : "\uF602";
            }
        }

        private void ToggleAlwaysOnTop()
        {
            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = !presenter.IsAlwaysOnTop;
            }
        }

        private void SetWindowMinimized(bool minimized)
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

        private void SetWindowMaximized(bool maximized)
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

        private void SetFullWindow(bool showTitleBar)
        {
            // mpv's title-bar property maps to the app's full-window state;
            // apply the requested state instead of blindly toggling.
            if (showTitleBar == _isFullWindow)
            {
                PlayerControl.ToggleFullWindow();
            }
        }

        private void SetWindowBorder(bool hasBorder)
        {
            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(hasBorder, true);
            }
        }
    }
}
