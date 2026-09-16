using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using mpv_winrt;
using mpv_winui.Modules.Common.Utils;
using System;

namespace mpv_winui.Modules.Player
{
    //TODO use state
    public sealed partial class VolumeFlyoutControl : UserControl
    {
        private readonly WeakReference<MpvPlayer?> _player = new(null);

        public VolumeFlyoutControl(MpvPlayer player)
        {
            this.InitializeComponent();
            ApplyLocalizedChrome();
            _player.SetTarget(player);
            VolumeSlider.Value = player.Volume();
            UpdateVolumeIcon(player.IsMuted(), player.Volume());
        }

        /// <summary>
        /// The mute button is icon-only and its glyph changes with the volume,
        /// so it cannot carry a caption: the tooltip and the accessible name
        /// are the only places its meaning can live.
        /// </summary>
        private void ApplyLocalizedChrome()
        {
            var lang = AppContext.AppLang;
            ToolTipService.SetToolTip(MuteButton, lang.AudioMute);
            AutomationProperties.SetName(MuteButton, lang.AudioMute);
        }

        private void MuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_player.TryGetTarget(out var player))
            {
                player.IsMuted(!player.IsMuted());
                UpdateVolumeIcon(player.IsMuted(), player.Volume());
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_player.TryGetTarget(out var player))
            {
                player.Volume(e.NewValue);
                UpdateVolumeIcon(player.IsMuted(), e.NewValue);
            }
        }

        //TODO use state
        private void UpdateVolumeIcon(bool isMuted, double volume)
        {
            DispatcherQueue.RunAsync(() =>
            {
                if (isMuted)
                {
                    VolumeIcon.Glyph = "\uE74F";
                }
                else
                {
                    var vol = volume;
                    VolumeIcon.Glyph = vol < 1 ? "\uE992" : vol < 34 ? "\uE993" : vol < 67 ? "\uE994" : "\uE995";
                }
            });
        }
    }
}
