using Microsoft.UI.Xaml;
using mpv_winui.Infrastructure.Common.View;

namespace mpv_winui
{
    public sealed partial class MainWindow
    {
        public override void UpdateTheme()
        {
            base.UpdateTheme();
            _windowsManager.UpdateTheme();
            ApplyShellBackdropVeil();
        }

        public override void UpdateBackdrop()
        {
            base.UpdateBackdrop();
            _windowsManager.UpdateBackdrop();
            ApplyShellBackdropVeil();
        }

        /// <summary>
        /// Chooses between the shell's two surface sets according to whether a
        /// system backdrop is actually in play.
        ///
        /// The main window and the settings window set their backdrop through
        /// the same <c>WindowStyleManager.ApplyBackdrop</c>, but only the
        /// settings window ever showed it: its surfaces are Transparent, while
        /// this window painted SolidBackgroundFillColorBase over everything. An
        /// opaque fill over Mica hides it completely, so "the main window has no
        /// Mica" was a painting problem, not a backdrop problem.
        ///
        /// Both sets exist in markup, stacked, and this only flips which one is
        /// visible. That is deliberate: a brush cannot be looked up from a theme
        /// dictionary in code (the indexer resolves against the application
        /// theme, not the element's, and this file's window switches
        /// RequestedTheme on its content root), so writing
        /// <c>ShellContentVeil.Background = someBrush</c> would silently paint
        /// the wrong theme's colour - the failure this app has already been
        /// through once, see the ClearValue note in ChangeFullWindow.
        ///
        /// Which set: with no backdrop (Mica unavailable, transparency effects
        /// off, or the user chose None) the window sits on a solid colour and a
        /// translucent surface there reads as washed-out grey, so the solid set
        /// wins. With a backdrop the veils win, and the wallpaper tints them.
        /// The RELATIONSHIP between the three blocks - pane darkest, content
        /// lightest, bar between - is identical in both sets.
        /// </summary>
                private void ApplyShellBackdropVeil()
        {
            // Nothing to switch any more: see the note on ShellContentView.
            //
            // This method's name is kept because two call sites reference it -
            // the theme/backdrop override and the settings change handler - and
            // removing those is a change to code that is otherwise correct. It
            // is deliberately empty, with the reason written down rather than
            // the method quietly deleted.
        }

private void MainWindow_SettingChanged(string key, object? value)
        {
            if (key == nameof(AppContext.AppSetting.BackdropType))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    _styleManager?.UpdateBackdrop();
                    _settingsWindow?.UpdateBackdrop();
                    ApplyShellBackdropVeil();
                });
            }
            else if (key == nameof(AppContext.AppSetting.WindowPiP)
                || key == nameof(AppContext.AppSetting.WindowPiPSize))
            {
                DispatcherQueue.TryEnqueue(() => ApplyPiP());
            }
            else if (key == nameof(AppContext.AppSetting.UiFont))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    _styleManager?.UpdateUiFont();
                    _settingsWindow?.UpdateUiFont();
                });
            }
            else if (key == nameof(AppContext.AppSetting.ControlBarLayout))
            {
                // The centered layout needs a wider minimum; re-apply so the
                // window cannot be squeezed below the bar's comfortable width.
                DispatcherQueue.TryEnqueue(() =>
                {
                    this.SetWindowMinSize(GetMinLogicalWidth(), MIN_LOGICAL_HEIGHT);
                });
            }
        }
    }
}
