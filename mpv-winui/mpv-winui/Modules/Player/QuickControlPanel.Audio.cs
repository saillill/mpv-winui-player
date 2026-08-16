using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace mpv_winui.Modules.Player;

/// <summary>Audio page of the quick-control panel (preset, device, equalizer).</summary>
public sealed partial class QuickControlPanel
{
    private ComboBox? _panelAudioDeviceBox;

    private void BuildPanelAudio(StackPanel root)
    {
        var lang = AppContext.AppLang;

        _panelAudioDeviceBox = new ComboBox
        {
            MinWidth = 160,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PlaceholderText = lang.SettingsAudioDevice,
        };
        AutomationProperties.SetName(_panelAudioDeviceBox, lang.SettingsAudioDevice);
        var devices = AppContext.GetAudioDevices?.Invoke();
        if (devices is not null)
        {
            foreach (var device in devices)
            {
                var label = string.IsNullOrWhiteSpace(device.Description) ? device.Name : device.Description;
                _panelAudioDeviceBox.Items.Add(new ComboBoxItem { Content = label, Tag = device.Name });
            }
        }
        _panelAudioDeviceBox.SelectionChanged += (_, _) =>
        {
            if (_panelAudioDeviceBox.SelectedItem is not ComboBoxItem { Tag: string name })
            {
                return;
            }
            MediaPlayer?.Command("set", "audio-device", name);
        };

        var presetBox = new ComboBox
        {
            MinWidth = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PlaceholderText = lang.PanelPresetFont,
        };
        AutomationProperties.SetName(presetBox, lang.PanelPresetFont);
        var presets = new (string Id, double[] Gains)[]
        {
            ("flat", [0, 0, 0, 0, 0, 0, 0, 0, 0, 0]),
            ("rock", [4, 3, 2, 0, 0, -1, -1, 1, 3, 5]),
            ("pop", [2, 1, 0, -1, -1, 0, 1, 2, 3, 3]),
            ("jazz", [3, 2, 1, 1, 0, 0, 1, 2, 2, 3]),
            ("classical", [4, 3, 2, 1, 0, -1, -1, 0, 2, 4]),
            ("electronic", [5, 4, 3, 1, 0, -1, 0, 2, 3, 4]),
            ("hiphop", [5, 4, 3, 1, 0, 0, 0, 1, 2, 3]),
            ("acoustic", [4, 3, 2, 0, -1, -1, 0, 1, 2, 3]),
            ("vocal", [0, -2, 0, 3, 4, 4, 3, 2, 0, 0]),
            ("bass", [6, 5, 4, 2, 0, 0, 0, 0, 0, 0]),
            ("treble", [0, 0, 0, 0, 0, 1, 2, 3, 4, 5]),
            ("metal", [4, 2, 0, -1, -1, -1, 0, 2, 3, 4]),
        };
        foreach (var preset in presets)
        {
            presetBox.Items.Add(new ComboBoxItem { Content = EqPresetLabel(preset.Id), Tag = preset.Gains });
        }
        presetBox.SelectionChanged += (_, _) =>
        {
            if (presetBox.SelectedItem is ComboBoxItem { Tag: double[] gains })
            {
                ApplyPanelPreset(gains);
            }
        };

        // Two placeholder-led selectors side by side: EQ preset + audio
        // device, mirroring each other instead of a toggle switch.
        var topRow = new Grid { ColumnSpacing = 8 };
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.Children.Add(presetBox);
        Grid.SetColumn(_panelAudioDeviceBox, 1);
        topRow.Children.Add(_panelAudioDeviceBox);
        root.Children.Add(PanelOptionCard(topRow));

        var bandLabels = new[] { "60", "170", "310", "600", "1K", "3K", "6K", "12K", "14K", "16K" };
        var bands = EqualizerBands;
        bands.Clear();
        for (var i = 0; i < bandLabels.Length; i++)
        {
            var index = i;
            var band = new EqualizerBand(bandLabels[i], EqGains[i]);
            band.ValueChanged += value =>
            {
                EqGains[index] = value;
                ApplyEqualizer?.Invoke();
            };
            bands.Add(band);
        }
        // Ten equal star columns fill the card edge-to-edge; every band
        // stays centered in its own column (no left/right drift).
        var bandGrid = new Grid();
        for (var i = 0; i < bands.Count; i++)
        {
            bandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var cell = new ContentControl
            {
                Content = bands[i],
                ContentTemplate = (DataTemplate)Resources["EqualizerBandTemplate"],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(cell, i);
            bandGrid.Children.Add(cell);
        }
        root.Children.Add(PanelOptionCard(bandGrid));

        // Quick actions fill the bottom of the audio page: mute, track
        // cycling and volume nudges (all localized via existing AppLang keys).
        var mute = PanelToggleButton(lang.PiPMute, "\uE74F",
            on => MediaPlayer?.Command("set", "mute", on ? "yes" : "no"));
        var cycleTrack = PanelIconButton(lang.AudioCycleTrack, "\uE8D6",
            () => MediaPlayer?.Command("cycle", "audio"));
        var volumeUp = PanelIconButton(lang.AudioVolumeUp, "\uE994",
            () => MediaPlayer?.Command("add", "volume", "5"));
        var volumeDown = PanelIconButton(lang.AudioVolumeDown, "\uE992",
            () => MediaPlayer?.Command("add", "volume", "-5"));
        var quickRow = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 8),
            VerticalAlignment = VerticalAlignment.Top,
        };
        quickRow.Children.Add(PanelButtonRow(mute, cycleTrack, volumeUp, volumeDown));
        root.Children.Add(PanelOptionCard(quickRow));
    }

    private void ApplyPanelPreset(double[] gains)
    {
        for (var i = 0; i < EqGains.Count && i < gains.Length; i++)
        {
            EqGains[i] = gains[i];
        }
        PanelUpdating = true;
        try
        {
            var bands = EqualizerBands;
            for (var i = 0; i < bands.Count && i < gains.Length; i++)
            {
                bands[i].Value = gains[i];
            }
        }
        finally
        {
            PanelUpdating = false;
        }
        ApplyEqualizer?.Invoke();
    }
}
