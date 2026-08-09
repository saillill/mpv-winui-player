using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using Windows.UI;

namespace mpv_winui.Modules.Settings.Controls;

public sealed partial class ThemeColorPickerControl : UserControl
{
    private const int RecentColorCount = 10;
    private bool _updating;
    private Color _color = Colors.Transparent;
    private readonly List<Button> _swatches = [];

    public ThemeColorPickerControl()
    {
        InitializeComponent();
        RecentColorsLabel = mpv_winui.AppContext.AppLang.ThemeColorRecentColors;
        WindowsColorsLabel = mpv_winui.AppContext.AppLang.ThemeColorWindowsColors;
        CustomColorsLabel = mpv_winui.AppContext.AppLang.ThemeColorCustomColors;
        RedLabel = mpv_winui.AppContext.AppLang.ThemeColorRed;
        GreenLabel = mpv_winui.AppContext.AppLang.ThemeColorGreen;
        BlueLabel = mpv_winui.AppContext.AppLang.ThemeColorBlue;
        HueLabel = mpv_winui.AppContext.AppLang.ThemeColorHue;
        SaturationLabel = mpv_winui.AppContext.AppLang.ThemeColorSaturation;
        LightnessLabel = mpv_winui.AppContext.AppLang.ThemeColorLightness;
        CancelLabel = mpv_winui.AppContext.AppLang.Cancel;
        DoneLabel = mpv_winui.AppContext.AppLang.ThemeColorDone;
        BuildRecentGrid();
        BuildWindowsGrid();
        ApplyCurrent();
    }

    public string RecentColorsLabel { get; }
    public string WindowsColorsLabel { get; }
    public string CustomColorsLabel { get; }
    public string RedLabel { get; }
    public string GreenLabel { get; }
    public string BlueLabel { get; }
    public string HueLabel { get; }
    public string SaturationLabel { get; }
    public string LightnessLabel { get; }
    public string CancelLabel { get; }
    public string DoneLabel { get; }

    /// <summary>The hex color selected when the dialog is accepted; null when canceled.</summary>
    public string? Result { get; private set; }

    /// <summary>Raised when Done or Cancel is clicked so the host can close the dialog.</summary>
    public event Action? Applied;

    public string CurrentColor
    {
        get => _currentColor;
        set
        {
            _currentColor = value ?? string.Empty;
            if (_updating)
            {
                return;
            }
            ApplyCurrent();
        }
    }
    private string _currentColor = string.Empty;

    private void ApplyCurrent()
    {
        _updating = true;
        try
        {
            _color = OptionColorControl.TryParse(CurrentColor) ?? Colors.Transparent;
            UpdateHslFromColor();
            UpdatePreview();
            HighlightSelected();
        }
        finally
        {
            _updating = false;
        }
    }

    private void BuildRecentGrid()
    {
        var colors = ParseList(mpv_winui.AppContext.AppSetting.ThemeRecentColors);
        AddSwatches(RecentGrid, colors, 5);
    }

    private void BuildWindowsGrid()
    {
        var palette = new List<string>
        {
            "#FFFFFF", "#E3E3E3", "#C7C7C7", "#ABABAB", "#8F8F8F", "#737373", "#575757", "#3B3B3B",
            "#FFB900", "#FF8C00", "#F7630C", "#CA5010", "#DA3B01", "#EF6950", "#D13438", "#FF4343",
            "#E74856", "#E81123", "#EA005E", "#C30052", "#E3008C", "#B4009E", "#881798", "#5C2D91",
            "#0078D4", "#0063B1", "#004E8C", "#003966", "#00809B", "#00B7C3", "#038387", "#005B70",
            "#00CC6A", "#10893E", "#107C10", "#004B1C", "#7A7574", "#5D5A58", "#767676", "#4C4A48",
        };
        AddSwatches(WindowsGrid, palette, 8);
    }

    private static List<string> ParseList(string? value)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return list;
        }

        foreach (var item in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (OptionColorControl.TryParse(item) is not null && !list.Contains(item))
            {
                list.Add(item);
            }
        }
        return list;
    }

    private void AddSwatches(Grid grid, IReadOnlyList<string> colors, int columns)
    {
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        for (var c = 0; c < columns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var rowCount = (colors.Count + columns - 1) / columns;
        for (var r = 0; r < rowCount; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var i = 0; i < colors.Count; i++)
        {
            var hex = colors[i];
            var button = new Button
            {
                Width = 36,
                Height = 32,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(OptionColorControl.TryParse(hex) ?? Colors.Transparent),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(64, 0, 0, 0)),
                Tag = hex,
            };
            button.Click += (_, _) => SelectColor(hex);
            Grid.SetRow(button, i / columns);
            Grid.SetColumn(button, i % columns);
            grid.Children.Add(button);
            _swatches.Add(button);
        }
    }

    private void SelectColor(string hex)
    {
        _updating = true;
        try
        {
            _color = OptionColorControl.TryParse(hex) ?? _color;
            _currentColor = hex;
            HexInput.Text = hex.TrimStart('#');
            RedSlider.Value = _color.R;
            GreenSlider.Value = _color.G;
            BlueSlider.Value = _color.B;
            UpdateHslFromColor();
            UpdatePreview();
            HighlightSelected();
        }
        finally
        {
            _updating = false;
        }
    }

    private void OnHexChanged(object sender, TextChangedEventArgs e)
    {
        if (_updating)
        {
            return;
        }

        var text = HexInput.Text?.Trim().TrimStart('#') ?? string.Empty;
        if (text.Length != 6
            || !uint.TryParse(text, NumberStyles.HexNumber, null, out var rgb))
        {
            return;
        }

        _updating = true;
        try
        {
            _color = Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            RedSlider.Value = _color.R;
            GreenSlider.Value = _color.G;
            BlueSlider.Value = _color.B;
            UpdateHslFromColor();
            UpdatePreview();
        }
        finally
        {
            _updating = false;
        }
    }

    private void OnRgbChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_updating)
        {
            return;
        }

        _color = Color.FromArgb(255, (byte)RedSlider.Value, (byte)GreenSlider.Value, (byte)BlueSlider.Value);
        _updating = true;
        try
        {
            HexInput.Text = $"{_color.R:X2}{_color.G:X2}{_color.B:X2}";
            RedValue.Text = ((int)RedSlider.Value).ToString(CultureInfo.InvariantCulture);
            GreenValue.Text = ((int)GreenSlider.Value).ToString(CultureInfo.InvariantCulture);
            BlueValue.Text = ((int)BlueSlider.Value).ToString(CultureInfo.InvariantCulture);
            UpdateHslFromColor();
            UpdatePreview();
        }
        finally
        {
            _updating = false;
        }
    }

    private void UpdatePreview()
    {
        PreviewBox.Background = new SolidColorBrush(_color);
        HexInput.Text = $"{_color.R:X2}{_color.G:X2}{_color.B:X2}";
        RedSlider.Value = _color.R;
        GreenSlider.Value = _color.G;
        BlueSlider.Value = _color.B;
        RedValue.Text = _color.R.ToString(CultureInfo.InvariantCulture);
        GreenValue.Text = _color.G.ToString(CultureInfo.InvariantCulture);
        BlueValue.Text = _color.B.ToString(CultureInfo.InvariantCulture);
    }

    private void OnCustomToggleClick(object sender, RoutedEventArgs e)
    {
        var expanded = CustomPanel.Visibility != Visibility.Visible;
        CustomPanel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        CustomToggleIcon.Glyph = expanded ? "\uE70E" : "\uE70D";
    }

    private void OnHslChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_updating)
        {
            return;
        }

        _color = HslToRgb(HueSlider.Value, SaturationSlider.Value / 100.0, LightnessSlider.Value / 100.0);
        _updating = true;
        try
        {
            HexInput.Text = $"{_color.R:X2}{_color.G:X2}{_color.B:X2}";
            RedSlider.Value = _color.R;
            GreenSlider.Value = _color.G;
            BlueSlider.Value = _color.B;
            RedValue.Text = _color.R.ToString(CultureInfo.InvariantCulture);
            GreenValue.Text = _color.G.ToString(CultureInfo.InvariantCulture);
            BlueValue.Text = _color.B.ToString(CultureInfo.InvariantCulture);
            HueValue.Text = ((int)HueSlider.Value).ToString(CultureInfo.InvariantCulture);
            SaturationValue.Text = ((int)SaturationSlider.Value).ToString(CultureInfo.InvariantCulture);
            LightnessValue.Text = ((int)LightnessSlider.Value).ToString(CultureInfo.InvariantCulture);
            UpdatePreview();
        }
        finally
        {
            _updating = false;
        }
    }

    private void UpdateHslFromColor()
    {
        _updating = true;
        try
        {
            var (hue, sat, light) = RgbToHsl(_color);
            HueSlider.Value = hue;
            SaturationSlider.Value = sat * 100.0;
            LightnessSlider.Value = light * 100.0;
            HueValue.Text = ((int)hue).ToString(CultureInfo.InvariantCulture);
            SaturationValue.Text = ((int)(sat * 100.0)).ToString(CultureInfo.InvariantCulture);
            LightnessValue.Text = ((int)(light * 100.0)).ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            _updating = false;
        }
    }

    private static (double Hue, double Saturation, double Lightness) RgbToHsl(Color color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2.0;
        if (Math.Abs(max - min) < 0.0001)
        {
            return (0, 0, l);
        }

        var d = max - min;
        var s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
        double h;
        if (Math.Abs(max - r) < 0.0001)
        {
            h = (g - b) / d + (g < b ? 6.0 : 0.0);
        }
        else if (Math.Abs(max - g) < 0.0001)
        {
            h = (b - r) / d + 2.0;
        }
        else
        {
            h = (r - g) / d + 4.0;
        }

        return (h * 60.0, s, l);
    }

    private static Color HslToRgb(double hue, double saturation, double lightness)
    {
        var h = ((hue % 360.0) + 360.0) % 360.0 / 360.0;
        var s = Math.Clamp(saturation, 0, 1);
        var l = Math.Clamp(lightness, 0, 1);

        double r, g, b;
        if (Math.Abs(s) < 0.0001)
        {
            r = g = b = l;
        }
        else
        {
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            r = HueToRgb(p, q, h + 1.0 / 3.0);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1.0 / 3.0);
        }

        return Color.FromArgb(
            255,
            (byte)Math.Round(r * 255.0),
            (byte)Math.Round(g * 255.0),
            (byte)Math.Round(b * 255.0));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0)
        {
            t += 1;
        }
        if (t > 1)
        {
            t -= 1;
        }
        if (t < 1.0 / 6.0)
        {
            return p + (q - p) * 6 * t;
        }
        if (t < 1.0 / 2.0)
        {
            return q;
        }
        if (t < 2.0 / 3.0)
        {
            return p + (q - p) * (2.0 / 3.0 - t) * 6;
        }
        return p;
    }

    private void HighlightSelected()
    {
        var accent = Colors.Transparent;
        try
        {
            if (Application.Current.Resources.TryGetValue("SystemAccentColor", out var value) && value is Color color)
            {
                accent = color;
            }
        }
        catch (Exception)
        {
        }

        foreach (var swatch in _swatches)
        {
            var isSelected = swatch.Tag is string hex
                && string.Equals(hex, CurrentColor, StringComparison.OrdinalIgnoreCase);
            swatch.BorderThickness = new Thickness(isSelected ? 2 : 1);
            swatch.BorderBrush = new SolidColorBrush(isSelected ? accent : Color.FromArgb(64, 0, 0, 0));
        }
    }

    private void OnDoneClick(object sender, RoutedEventArgs e)
    {
        var hex = $"#{_color.R:X2}{_color.G:X2}{_color.B:X2}";
        Result = hex;
        SaveRecent(hex);
        Applied?.Invoke();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Result = null;
        Applied?.Invoke();
    }

    private static void SaveRecent(string hex)
    {
        var list = ParseList(mpv_winui.AppContext.AppSetting.ThemeRecentColors);
        list.Remove(hex);
        list.Insert(0, hex);
        if (list.Count > RecentColorCount)
        {
            list.RemoveRange(RecentColorCount, list.Count - RecentColorCount);
        }
        mpv_winui.AppContext.AppSetting.ThemeRecentColors = string.Join(';', list);
    }
}
