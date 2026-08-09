using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Globalization;
using Windows.UI;

namespace mpv_winui.Modules.Settings.Controls;

public sealed partial class CustomColorPickerControl : UserControl
{
    private bool _updating;
    private double _hue;
    private double _saturation = 1;
    private double _value = 1;
    private bool _dragging;

    public CustomColorPickerControl()
    {
        InitializeComponent();
        HueLabel = mpv_winui.AppContext.AppLang.ThemeColorHue;
        SaturationLabel = mpv_winui.AppContext.AppLang.ThemeColorSaturation;
        LightnessLabel = mpv_winui.AppContext.AppLang.ThemeColorLightness;
        CancelLabel = mpv_winui.AppContext.AppLang.Cancel;
        DoneLabel = mpv_winui.AppContext.AppLang.ThemeColorDone;
        ApplyCurrent();
    }

    public string HueLabel { get; }
    public string SaturationLabel { get; }
    public string LightnessLabel { get; }
    public string CancelLabel { get; }
    public string DoneLabel { get; }

    /// <summary>The selected color when accepted; null when canceled.</summary>
    public string? Result { get; private set; }

    public event Action? Applied;

    public string CurrentColor
    {
        get => _currentColor;
        set
        {
            _currentColor = value ?? string.Empty;
            if (!_updating)
            {
                ApplyCurrent();
            }
        }
    }
    private string _currentColor = string.Empty;

    private void ApplyCurrent()
    {
        _updating = true;
        try
        {
            var color = OptionColorControl.TryParse(CurrentColor) ?? Colors.White;
            var (hue, sat, val) = RgbToHsv(color);
            _hue = hue;
            _saturation = sat;
            _value = val;
            HueSlider.Value = hue;
            SaturationSlider.Value = sat * 100.0;
            LightnessSlider.Value = val * 100.0;
            HueValue.Text = ((int)hue).ToString(CultureInfo.InvariantCulture);
            SaturationValue.Text = ((int)(sat * 100.0)).ToString(CultureInfo.InvariantCulture);
            LightnessValue.Text = ((int)(val * 100.0)).ToString(CultureInfo.InvariantCulture);
            HexInput.Text = $"{color.R:X2}{color.G:X2}{color.B:X2}";
            UpdateField();
        }
        finally
        {
            _updating = false;
        }
    }

    private void OnSliderChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_updating)
        {
            return;
        }

        _hue = Normalize(HueSlider.Value, 0, 360);
        _saturation = Normalize(SaturationSlider.Value, 0, 100) / 100.0;
        _value = Normalize(LightnessSlider.Value, 0, 100) / 100.0;
        _updating = true;
        try
        {
            HueValue.Text = ((int)_hue).ToString(CultureInfo.InvariantCulture);
            SaturationValue.Text = ((int)(_saturation * 100.0)).ToString(CultureInfo.InvariantCulture);
            LightnessValue.Text = ((int)(_value * 100.0)).ToString(CultureInfo.InvariantCulture);
            var color = HsvToRgb(_hue, _saturation, _value);
            HexInput.Text = $"{color.R:X2}{color.G:X2}{color.B:X2}";
            PreviewBox.Background = new SolidColorBrush(color);
            UpdateField();
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

        var color = Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        var (hue, sat, val) = RgbToHsv(color);
        _hue = hue;
        _saturation = sat;
        _value = val;
        _updating = true;
        try
        {
            HueSlider.Value = hue;
            SaturationSlider.Value = sat * 100.0;
            LightnessSlider.Value = val * 100.0;
            HueValue.Text = ((int)hue).ToString(CultureInfo.InvariantCulture);
            SaturationValue.Text = ((int)(sat * 100.0)).ToString(CultureInfo.InvariantCulture);
            LightnessValue.Text = ((int)(val * 100.0)).ToString(CultureInfo.InvariantCulture);
            PreviewBox.Background = new SolidColorBrush(color);
            UpdateField();
        }
        finally
        {
            _updating = false;
        }
    }

    private void ColorField_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _dragging = true;
        AttachRootPointerHandlers();
        TryUpdateFromPointer(e);
    }

    private void ColorField_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging)
        {
            TryUpdateFromPointer(e);
        }
    }

    private void ColorField_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        EndDrag();
    }

    private void ColorField_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        // Keep dragging while the button is held; root-level handlers follow
        // the pointer outside the color field and stop on release.
    }

    /// <summary>
    /// Pointer capture is deliberately not used here: capturing a pointer
    /// inside a dialog can raise a WinRT E_POINTER failure on some input
    /// paths, so the drag state is tracked with a simple flag instead.
    /// </summary>
    private void TryUpdateFromPointer(PointerRoutedEventArgs e)
    {
        try
        {
            UpdateFromPointer(e);
        }
        catch
        {
            EndDrag();
        }
    }

    private UIElement? _pointerRoot;

    private void AttachRootPointerHandlers()
    {
        if (_pointerRoot is not null)
        {
            return;
        }

        _pointerRoot = XamlRoot?.Content as UIElement;
        if (_pointerRoot is null)
        {
            return;
        }

        _pointerRoot.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnRootPointerMoved), true);
        _pointerRoot.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnRootPointerReleased), true);
        _pointerRoot.AddHandler(UIElement.PointerCanceledEvent, new PointerEventHandler(OnRootPointerReleased), true);
        _pointerRoot.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(OnRootPointerReleased), true);
        Unloaded += OnPickerUnloaded;
    }

    private void OnRootPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging)
        {
            TryUpdateFromPointer(e);
        }
    }

    private void OnRootPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        EndDrag();
    }

    private void EndDrag()
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        DetachRootPointerHandlers();
    }

    private void DetachRootPointerHandlers()
    {
        if (_pointerRoot is null)
        {
            return;
        }

        _pointerRoot.RemoveHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnRootPointerMoved));
        _pointerRoot.RemoveHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnRootPointerReleased));
        _pointerRoot.RemoveHandler(UIElement.PointerCanceledEvent, new PointerEventHandler(OnRootPointerReleased));
        _pointerRoot.RemoveHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(OnRootPointerReleased));
        Unloaded -= OnPickerUnloaded;
        _pointerRoot = null;
    }

    private void OnPickerUnloaded(object sender, RoutedEventArgs e)
    {
        _dragging = false;
        DetachRootPointerHandlers();
    }

    private void UpdateFromPointer(PointerRoutedEventArgs e)
    {
        if (!SafeSize(ColorField.ActualWidth, out var width)
            || !SafeSize(ColorField.ActualHeight, out var height))
        {
            return;
        }

        var point = e.GetCurrentPoint(ColorField);
        _saturation = Math.Clamp(point.Position.X / width, 0, 1);
        _value = 1 - Math.Clamp(point.Position.Y / height, 0, 1);
        _updating = true;
        try
        {
            SaturationSlider.Value = _saturation * 100.0;
            LightnessSlider.Value = _value * 100.0;
            SaturationValue.Text = ((int)(_saturation * 100.0)).ToString(CultureInfo.InvariantCulture);
            LightnessValue.Text = ((int)(_value * 100.0)).ToString(CultureInfo.InvariantCulture);
            var color = HsvToRgb(_hue, _saturation, _value);
            HexInput.Text = $"{color.R:X2}{color.G:X2}{color.B:X2}";
            PreviewBox.Background = new SolidColorBrush(color);
            UpdateField();
        }
        finally
        {
            _updating = false;
        }
    }

    private void UpdateField()
    {
        HueBase.Background = new SolidColorBrush(HsvToRgb(_hue, 1, 1));
        SafeSize(ColorField.ActualWidth, out var width);
        SafeSize(ColorField.ActualHeight, out var height);
        FieldThumb.Margin = new Thickness(
            _saturation * Math.Max(0, width - 14),
            (1 - _value) * Math.Max(0, height - 14),
            0,
            0);
        PreviewBox.Background = new SolidColorBrush(HsvToRgb(_hue, _saturation, _value));
    }

    /// <summary>Clamps a value into [min, max], mapping NaN/Infinity to the midpoint.</summary>
    private static double Normalize(double value, double min, double max)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return (min + max) / 2.0;
        }
        return Math.Clamp(value, min, max);
    }

    /// <summary>Treats NaN/Infinity/zero layout sizes as 1 so no division or
    /// margin ever receives a non-finite value.</summary>
    private static bool SafeSize(double value, out double size)
    {
        size = double.IsNaN(value) || double.IsInfinity(value) || value <= 0 ? 1 : value;
        return true;
    }

    private void OnDoneClick(object sender, RoutedEventArgs e)
    {
        var color = HsvToRgb(_hue, _saturation, _value);
        Result = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        Applied?.Invoke();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Result = null;
        Applied?.Invoke();
    }

    private static (double Hue, double Saturation, double Value) RgbToHsv(Color color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var d = max - min;
        double h = 0;
        if (Math.Abs(d) > 0.0001)
        {
            if (Math.Abs(max - r) < 0.0001)
            {
                h = (g - b) / d + (g < b ? 6 : 0);
            }
            else if (Math.Abs(max - g) < 0.0001)
            {
                h = (b - r) / d + 2;
            }
            else
            {
                h = (r - g) / d + 4;
            }
            h *= 60;
        }
        var s = Math.Abs(max) < 0.0001 ? 0 : d / max;
        return (h, s, max);
    }

    private static Color HsvToRgb(double hue, double saturation, double value)
    {
        var h = ((hue % 360.0) + 360.0) % 360.0 / 60.0;
        var s = Math.Clamp(saturation, 0, 1);
        var v = Math.Clamp(value, 0, 1);
        var c = v * s;
        var x = c * (1 - Math.Abs(h % 2 - 1));
        double r, g, b;
        switch ((int)h)
        {
            case 0: r = c; g = x; b = 0; break;
            case 1: r = x; g = c; b = 0; break;
            case 2: r = 0; g = c; b = x; break;
            case 3: r = 0; g = x; b = c; break;
            case 4: r = x; g = 0; b = c; break;
            default: r = c; g = 0; b = x; break;
        }
        var m = v - c;
        return Color.FromArgb(
            255,
            (byte)Math.Round((r + m) * 255.0),
            (byte)Math.Round((g + m) * 255.0),
            (byte)Math.Round((b + m) * 255.0));
    }
}
