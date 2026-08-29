using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using mpv_winrt;
using mpv_winui.Modules.Common.Utils;
using System;

namespace mpv_winui.Modules.Player
{
    public sealed partial class VolumeFlyoutControl : UserControl
    {
        private readonly WeakReference<MpvMediaPlayer?> _player = new(null);
        private MpvMediaPlayer? _subscribedPlayer;
        private bool _updating;

        public VolumeFlyoutControl(MpvMediaPlayer player)
        {
            this.InitializeComponent();
            _player.SetTarget(player);
            _subscribedPlayer = player;
            player.Native.VolumeChanged += OnPlayerVolumeChanged;
            Unloaded += VolumeFlyoutControl_Unloaded;
            // Maximum must precede Value, otherwise a volume above 100
            // (allowed by volume-max) is clamped down on open.
            VolumeSlider.Maximum = Math.Max(100, mpv_winui.AppContext.AppSetting.VolumeMax);
            VolumeSlider.Value = player.Volume;
            VolumeSlider.ValueChanged += VolumeSlider_ValueChanged;
            UpdateVolumeIcon(player.IsMuted, player.Volume);
            // Localized names for keyboard/screen-reader access (XAML defaults
            // are English placeholders; unpackaged WinUI has no x:Uid).
            AutomationProperties.SetName(MuteButton, mpv_winui.AppContext.AppLang.PiPMute);
            AutomationProperties.SetName(VolumeSlider, mpv_winui.AppContext.AppLang.ControlBarIconVolume);
        }

        private void MuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_player.TryGetTarget(out var player))
            {
                player.IsMuted = !player.IsMuted;
                UpdateVolumeIcon(player.IsMuted, player.Volume);
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_updating)
            {
                return;
            }
            if (_player.TryGetTarget(out var player))
            {
                player.Volume = e.NewValue;
                UpdateVolumeIcon(player.IsMuted, e.NewValue);
            }
        }

        private void OnPlayerVolumeChanged(VolumeChangedEventArgs args)
        {
            DispatcherQueue.RunAsync(() =>
            {
                if (_updating)
                {
                    return;
                }
                _updating = true;
                try
                {
                    var volume = (int)args.Volume;
                    VolumeSlider.Value = volume;
                    if (_player.TryGetTarget(out var player))
                    {
                        UpdateVolumeIcon(player.IsMuted, volume);
                    }
                }
                finally
                {
                    _updating = false;
                }
            });
        }

        private void VolumeFlyoutControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_subscribedPlayer is { } player)
            {
                player.Native.VolumeChanged -= OnPlayerVolumeChanged;
                _subscribedPlayer = null;
            }
        }

        private void UpdateVolumeIcon(bool isMuted, double volume)
        {
            if (isMuted)
            {
                VolumeIcon.Glyph = "\uEB4B";
            }
            else
            {
                var vol = volume;
                VolumeIcon.Glyph = vol < 1 ? "\uF6F9" : vol < 34 ? "\uF6FB" : "\uEB43";
            }
        }
    }
}
