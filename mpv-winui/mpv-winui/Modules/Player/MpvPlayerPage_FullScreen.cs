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

            // Activity-based: any movement over the video area expands the
            // overlay bar (the bar itself handles moves over the mask/bar via
            // RootGrid_PointerMoved), and the idle timer retracts it.
            if (sender is FrameworkElement area)
            {
                PlayerControl.NotifyOverlayPointerActivity(e.GetCurrentPoint(area).Position);
            }
            else
            {
                PlayerControl.NotifyOverlayPointerActivity(default);
            }
        }

        private bool PlayerControl_OnFullScreenRequest()
        {
            // mpv's "fullscreen" property is the single source of truth: the
            // property change event (MpvPlayerPage_WindowChanged) applies the
            // presenter, the full-window page state and the overlay. Keeping
            // the app's button on this path also makes the ESC binding in
            // input.conf ("set fullscreen no") able to leave fullscreen.
            AppContext.SendMpvCommand(_isFullScreen ? "set fullscreen no" : "set fullscreen yes");
            return _isFullScreen;
        }

        private bool PlayerControl_OnFullWindowRequest()
        {
            // Track the state ourselves: GoToState's return value only reports
            // whether the state changed, so re-entering an already-active state
            // used to flip _isFullWindow the wrong way and left the overlay
            // (or the windowed bar) in a broken half state.
            if (_isFullWindow)
            {
                VisualStateManager.GoToState(this, "NormalWindow", true);
                _isFullWindow = false;
            }
            else
            {
                VisualStateManager.GoToState(this, "FullWindow", true);
                _isFullWindow = true;
            }

            if (App.Window is MainWindow window)
            {
                window.ChangeFullWindow(_isFullWindow);
            }

            PlayerControl.SetOverlayMode(_isFullWindow);
            PlayerControl.RefreshAdaptiveState();

            return _isFullWindow;
        }
    }
}
