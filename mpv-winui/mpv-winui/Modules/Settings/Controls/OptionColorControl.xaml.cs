using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace mpv_winui.Modules.Settings.Controls;

public sealed partial class OptionColorControl : OptionControlBase
{
    public OptionColorControl()
    {
        InitializeComponent();
    }

    protected override void OnSettingChanged(Option? oldValue, Option? newValue)
    {
        if (newValue is null)
        {
            return;
        }

        LabelText.Text = newValue.Label;
        UpdateDescription(DescriptionText);
        PickButton.Content = mpv_winui.AppContext.AppLang.SettingsChooseThemeColor;
        PickButton.IsEnabled = newValue.IsEnabled;
        UpdateSwatch();
    }

    protected override void OnOptionStateChanged()
    {
        UpdateWarning(WarningText);
        PickButton.IsEnabled = Setting?.IsEnabled ?? true;
    }

    private void UpdateSwatch()
    {
        if (Setting?.Getter?.Invoke() is string hex && TryParse(hex) is { } color)
        {
            ColorSwatch.Background = new SolidColorBrush(color);
        }
        else
        {
            ColorSwatch.Background = new SolidColorBrush(Colors.Transparent);
        }
    }

    private async void OnPickClick(object sender, RoutedEventArgs e)
    {
        if (Setting is null || XamlRoot is null)
        {
            return;
        }

        var picker = new ThemeColorPickerControl
        {
            CurrentColor = Setting.Getter?.Invoke() as string ?? string.Empty,
        };
        var dialog = new ContentDialog
        {
            Title = mpv_winui.AppContext.AppLang.SettingsThemeAccentColor,
            Content = picker,
            XamlRoot = XamlRoot,
        };
        picker.Applied += () => dialog.Hide();
        await dialog.ShowAsync();

        if (picker.Result is string hex)
        {
            Setting.Setter?.Invoke(hex);
            UpdateSwatch();
        }
    }

    internal static Color? TryParse(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        var value = hex.Trim().TrimStart('#');
        if (value.Length == 6
            && uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
        {
            return Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        }

        if (value.Length == 8
            && uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var argb))
        {
            return Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
        }

        return null;
    }
}
