using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using mpv_winui.Modules.Common.View;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace mpv_winui.Modules.Settings.Controls;

public sealed partial class OptionColorControl : OptionControlBase
{
    private bool _expanded;

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
        RecentLabel.Text = mpv_winui.AppContext.AppLang.ThemeColorRecentColors;
        WindowsLabel.Text = mpv_winui.AppContext.AppLang.ThemeColorWindowsColors;
        CustomButton.Content = mpv_winui.AppContext.AppLang.ThemeColorCustomColors;
        BuildRecentGrid();
        BuildWindowsGrid();
        UpdateSwatch();
    }

    protected override void OnOptionStateChanged()
    {
        UpdateWarning(WarningText);
    }

    private void OnHeaderTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        _expanded = !_expanded;
        ExpandedPanel.Visibility = _expanded ? Visibility.Visible : Visibility.Collapsed;
        ExpandIcon.Glyph = _expanded ? "\uE70E" : "\uE70D";
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
            if (TryParse(item) is not null && !list.Contains(item))
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
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        }

        var rowCount = (colors.Count + columns - 1) / columns;
        for (var r = 0; r < rowCount; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        }

        for (var i = 0; i < colors.Count; i++)
        {
            var hex = colors[i];
            var cell = new Grid();
            var button = new Button
            {
                Width = 32,
                Height = 32,
                MinWidth = 0,
                MinHeight = 0,
                Margin = new Thickness(4),
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(TryParse(hex) ?? Colors.Transparent),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(64, 0, 0, 0)),
                Tag = hex,
            };
            var overlay = new Border
            {
                Width = 32,
                Height = 32,
                Margin = new Thickness(4),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromArgb(96, 0, 0, 0)),
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
                Tag = hex,
            };
            button.Click += (_, _) => ApplyColor(hex);
            cell.Children.Add(button);
            cell.Children.Add(overlay);
            Grid.SetRow(cell, i / columns);
            Grid.SetColumn(cell, i % columns);
            grid.Children.Add(cell);
        }

        HighlightSelected();
    }

    private void ApplyColor(string hex)
    {
        if (Setting is not { } option)
        {
            return;
        }

        option.Setter?.Invoke(hex);
        option.NotifyChanged();
        ColorSwatch.Background = new SolidColorBrush(TryParse(hex) ?? Colors.Transparent);
        SaveRecent(hex);
        BuildRecentGrid();
        BuildWindowsGrid();
    }

    private void HighlightSelected()
    {
        var selected = Setting?.Getter?.Invoke() as string ?? string.Empty;
        foreach (var cell in RecentGrid.Children.Concat(WindowsGrid.Children))
        {
            if (cell is not Grid grid)
            {
                continue;
            }

            foreach (var child in grid.Children)
            {
                if (child is Border { Tag: string hex } overlay)
                {
                    overlay.Visibility = string.Equals(hex, selected, StringComparison.OrdinalIgnoreCase)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            }
        }
    }

    private async void OnCustomClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Setting is null || XamlRoot is null)
            {
                return;
            }

            var picker = new ColorPicker
            {
                Color = TryParse(Setting.Getter?.Invoke() as string) ?? Colors.White,
                IsAlphaEnabled = false,
                IsColorSliderVisible = true,
                IsColorChannelTextInputVisible = true,
                IsHexInputVisible = true,
                IsMoreButtonVisible = false,
                ColorSpectrumShape = ColorSpectrumShape.Box,
                MinWidth = 320,
            };
            var dialog = new ContentDialog
            {
                Title = mpv_winui.AppContext.AppLang.ThemeColorCustomColors,
                Content = picker,
                XamlRoot = XamlRoot,
                RequestedTheme = WindowStyleManager.ResolveTheme(),
                PrimaryButtonText = mpv_winui.AppContext.AppLang.ThemeColorDone,
                CloseButtonText = mpv_winui.AppContext.AppLang.Cancel,
                DefaultButton = ContentDialogButton.Primary,
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var color = picker.Color;
                ApplyColor($"#{color.R:X2}{color.G:X2}{color.B:X2}");
            }
        }
        catch (Exception ex)
        {
            mpv_winui.AppContext.AppLogger.Error(ex, "custom color dialog failed");
        }
    }

    private static void SaveRecent(string hex)
    {
        var list = ParseList(mpv_winui.AppContext.AppSetting.ThemeRecentColors);
        list.Remove(hex);
        list.Insert(0, hex);
        if (list.Count > 10)
        {
            list.RemoveRange(10, list.Count - 10);
        }
        mpv_winui.AppContext.AppSetting.ThemeRecentColors = string.Join(';', list);
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
