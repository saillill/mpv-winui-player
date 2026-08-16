using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace mpv_winui.Modules.Player;

/// <summary>One equalizer band shown by the quick-control audio page.</summary>
public sealed class EqualizerBand : INotifyPropertyChanged
{
    private double _value;

    public EqualizerBand(string band, double value)
    {
        Band = band;
        _value = value;
    }

    public string Band { get; }

    /// <summary>Current gain formatted for the label above the slider.</summary>
    public string ValueText => _value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

    public double Value
    {
        get => _value;
        set
        {
            if (Math.Abs(_value - value) < 0.0001)
            {
                return;
            }
            _value = value;
            ValueChanged?.Invoke(value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValueText)));
        }
    }

    /// <summary>PlayerControl subscribes to keep _eqGains in sync.</summary>
    public Action<double>? ValueChanged { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Content shell for the quick-control flyout. PlayerControl builds the
/// audio/video/subtitle/playback sections into <see cref="ContentRoot"/>.
/// </summary>
public sealed partial class QuickControlPanel : UserControl
{
    public ObservableCollection<EqualizerBand> EqualizerBands { get; } = [];

    public QuickControlPanel()
    {
        InitializeComponent();
    }

    /// <summary>Panel that PlayerControl fills with the quick-control sections.</summary>
    public StackPanel ContentPanel => ContentRoot;
}
