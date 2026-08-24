using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace mpv_winui.Modules.Player;

/// <summary>
/// Equalizer / audio-subtitle delay / custom playback-rate flyout handlers.
/// Split from PlayerControl.xaml.cs (audit C5) without behavior changes.
/// </summary>
public sealed partial class PlayerControl
{
    // ===== Equalizer =====
    private static readonly string[] EqualizerBands =
    [
        "31", "62", "125", "250", "500", "1k", "2k", "4k", "8k", "16k",
    ];

    private readonly List<double> _eqGains = new(10) { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
    private readonly ObservableCollection<EqualizerBand> _equalizerFlyoutBands = [];
    private bool _eqUpdating;

    private void EqualizerFlyout_Opened(object sender, object e)
    {
        BuildEqualizerBands();
    }

    private void BuildEqualizerBands()
    {
        EqualizerBandsPanel.ItemsSource = _equalizerFlyoutBands;
        _equalizerFlyoutBands.Clear();
        for (int i = 0; i < EqualizerBands.Length; i++)
        {
            int index = i;
            var band = new EqualizerBand(EqualizerBands[i], _eqGains[i]);
            band.ValueChanged += value =>
            {
                _eqGains[index] = value;
                if (!_eqUpdating)
                {
                    ApplyEqualizer();
                }
            };
            _equalizerFlyoutBands.Add(band);
        }
    }

    private void ApplyEqualizer()
    {
        if (MediaPlayer is null)
        {
            return;
        }
        // superequalizer gains order: 10 bands (31Hz..16kHz), where the
        // first value is the "bass" band and last the "treble".
        var gains = string.Join(":", _eqGains.Select(g => g.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)));
        MediaPlayer.Command(["set", "af", $"lavfi=[superequalizer@eq:{gains}]"]);
    }

    private void EqualizerReset_Click(object sender, RoutedEventArgs e)
    {
        _eqUpdating = true;
        try
        {
            for (int i = 0; i < _eqGains.Count; i++)
            {
                _eqGains[i] = 0;
                if (i < _equalizerFlyoutBands.Count)
                {
                    _equalizerFlyoutBands[i].Value = 0;
                }
            }
        }
        finally
        {
            _eqUpdating = false;
        }
        MediaPlayer?.Command(["set", "af", ""]);
    }

    private void EqualizerOff_Click(object sender, RoutedEventArgs e)
    {
        MediaPlayer?.Command(["set", "af", ""]);
    }

    // ===== Audio / subtitle delay =====
    private bool _delaySliderUpdating;

    private void DelayFlyout_Opened(object? sender, object e)
    {
        _delaySliderUpdating = true;
        AudioDelaySlider.Value = AppContext.AppSetting.AudioDelay;
        SubDelaySlider.Value = AppContext.AppSetting.SubDelay;
        _delaySliderUpdating = false;
    }

    private void AudioDelaySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_delaySliderUpdating)
        {
            return;
        }
        var value = Math.Round(e.NewValue, 1);
        AppContext.AppSetting.AudioDelay = value;
        AppContext.SendMpvCommand($"no-osd set audio-delay {value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
    }

    private void SubDelaySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_delaySliderUpdating)
        {
            return;
        }
        var value = Math.Round(e.NewValue, 1);
        AppContext.AppSetting.SubDelay = value;
        AppContext.SendMpvCommand($"no-osd set sub-delay {value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
    }

    private void DelayReset_Click(object sender, RoutedEventArgs e)
    {
        AudioDelaySlider.Value = 0;
        SubDelaySlider.Value = 0;
    }

    // ===== Custom playback rate =====
    private async void CustomRateItem_Click(object sender, RoutedEventArgs e)
    {
        var box = new TextBox { PlaceholderText = "e.g. 1.3 or 16" };
        var dialog = new ContentDialog
        {
            Title = AppContext.AppLang.CustomRate,
            Content = box,
            PrimaryButtonText = AppContext.AppLang.Ok,
            CloseButtonText = AppContext.AppLang.Cancel,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary
            && double.TryParse(box.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rate)
            && rate > 0 && rate <= 100)
        {
            MediaPlayer?.Command(["set", "speed", rate.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)]);
        }
    }
}
