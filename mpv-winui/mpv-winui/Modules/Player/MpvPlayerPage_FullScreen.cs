using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace mpv_winui.Modules.Player
{
    public sealed partial class MpvPlayerPage
    {
        private bool _isFullScreen;
        private bool _isFullWindow;

        /// <summary>
        /// Raised by the shell's bar, not wired in this page's XAML any more -
        /// the bar moved to MainWindow so it can stay mounted across pages.
        /// </summary>
        internal bool HandleFullScreenRequest()
        {
            // mpv's "fullscreen" property is the single source of truth: the
            // property change event (MpvPlayerPage_WindowChanged) applies the
            // presenter, the full-window page state and the overlay. Keeping
            // the app's button on this path also makes the ESC binding in
            // input.conf ("set fullscreen no") able to leave fullscreen.
            AppContext.SendMpvCommand(_isFullScreen ? "set fullscreen no" : "set fullscreen yes");
            return _isFullScreen;
        }

        /// <summary>
        /// Raised by the shell's bar. See <see cref="HandleFullScreenRequest"/>.
        /// </summary>
        internal bool HandleFullWindowRequest()
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
