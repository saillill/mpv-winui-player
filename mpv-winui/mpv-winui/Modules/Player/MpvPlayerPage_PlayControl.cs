using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using mpv_winui.Modules.Settings;

namespace mpv_winui.Modules.Player
{
    public sealed partial class MpvPlayerPage
    {
        private void SetupPlayControl()
        {
            UpdatePlayControlStyle();

            PlayerControl.MediaPlayer = _mediaPlayer;
            PlayerView.PointerPressed += PlayerView_PointerPressed2;
        }

        private void CleanupPlayControl()
        {
            PlayerView.PointerPressed -= PlayerView_PointerPressed2;
            PlayerControl.MediaPlayer = null;
        }

        private void PlayerView_PointerPressed2(object sender, PointerRoutedEventArgs e)
        {
            var kind = e.GetCurrentPoint(PlayerView).Properties.PointerUpdateKind;
            HandlePlayControl(kind);
        }

        private void HandlePlayControl(PointerUpdateKind kind)
        {
            if (_discMenuActive && kind != PointerUpdateKind.MiddleButtonPressed)
            {
                return;
            }

            if (kind != PointerUpdateKind.LeftButtonPressed && kind != PointerUpdateKind.MiddleButtonPressed)
            {
                return;
            }

            TogglePlayerControl();
        }

        public void TogglePlayerControl()
        {
            PlayerControl.ToggleControlPanel();
        }

        /// <summary>
        /// Applies the saved skin at startup.
        ///
        /// The assignment lives on PlayerControl.ApplyPlayerStyle, which the
        /// setting-change event also calls; this exists for the one caller that
        /// runs before any setting can change. Keeping two copies of the switch is
        /// how the two paths would drift apart.
        /// </summary>
        public void UpdatePlayControlStyle() => PlayerControl?.ApplyPlayerStyle();
    }
}