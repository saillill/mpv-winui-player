﻿using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace mpv_winui.Domains.Playback
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
            // **Ask the shell first, and only flip state if it agrees.**
            //
            // The order here is the fix for a measured defect. This method used to flip its
            // own visual state and then call `ChangeFullWindow`, which decides whether the
            // window's chrome may be replaced. When the shell refused -- the library page is
            // up, so there is no video to fill the window -- the page had ALREADY gone to
            // `FullWindow`: its menu row collapsed while the shell kept the title row and bar
            // row, and the result was a windowed frame with a full-window page inside it and
            // no control bar anywhere in the tree (53 elements -> 31, no ProgressSlider).
            //
            // Now the page asks `CanEnterFullWindow` before touching the visual state, so
            // there is no half-applied state to unwind.
            var window = App.Window as MainWindow;
            var wantFull = !_isFullWindow;
            if (wantFull && window is { CanEnterFullWindow: false })
            {
                return false;
            }

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

            window?.ChangeFullWindow(_isFullWindow);

            PlayerControl.SetOverlayMode(_isFullWindow);
            PlayerControl.RefreshAdaptiveState();

            return _isFullWindow;
        }
    }
}
