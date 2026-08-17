using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using System;

namespace mpv_winui.Modules.Player;

/// <summary>Subtitle page of the quick-control panel (font, size, sync, move).</summary>
public sealed partial class QuickControlPanel
{
    private ComboBox? _panelFontBox;

    private void BuildPanelSubtitles(StackPanel root)
    {
        var lang = AppContext.AppLang;

        _panelFontBox = new ComboBox
        {
            IsEditable = true,
            MinWidth = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Text = "Segoe UI",
            ItemsSource = new[]
            {
                "sans-serif", "Segoe UI", "Microsoft YaHei", "SimSun", "DengXian",
                "SimHei", "Consolas", "Source Han Sans SC", "LXGW WenKai Mono Lite",
            },
        };
        AutomationProperties.SetName(_panelFontBox, lang.SettingsSubFont);
        _panelFontBox.SelectionChanged += (_, _) =>
        {
            if (PanelUpdating)
            {
                return;
            }
            var font = (_panelFontBox.SelectedItem as string) ?? _panelFontBox.Text;
            if (!string.IsNullOrWhiteSpace(font))
            {
                MediaPlayer?.Command("set", "sub-font", font);
            }
        };

        var sizeBox = new NumberBox
        {
            Minimum = 1,
            Maximum = 200,
            Value = AppContext.AppSetting.SubFontSize,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Width = 110,
        };
        AutomationProperties.SetName(sizeBox, lang.SettingsSubFontSize);
        sizeBox.ValueChanged += (_, _) =>
        {
            if (PanelUpdating || double.IsNaN(sizeBox.Value))
            {
                return;
            }
            var value = (int)Math.Round(sizeBox.Value);
            AppContext.AppSetting.SubFontSize = value;
            MediaPlayer?.Command("set", "sub-font-size", value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        };

        var fontRow = new Grid { ColumnSpacing = 8 };
        fontRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fontRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fontRow.Children.Add(_panelFontBox);
        Grid.SetColumn(sizeBox, 1);
        fontRow.Children.Add(sizeBox);

        // Bold / italic / position reset stay on their own row, placed right
        // under the font card so the subtitle style controls are still
        // grouped with the font itself.
        var bold = PanelToggleButton(lang.SettingsSubBold, "\uE8D4",
            on => MediaPlayer?.Command("set", "sub-bold", on ? "yes" : "no"));
        var italic = PanelToggleButton(lang.SettingsSubItalic, "\uE8DB",
            on => MediaPlayer?.Command("set", "sub-italic", on ? "yes" : "no"));
        var resetPos = PanelIconButton(lang.Reset, "\uE777", () =>
        {
            MediaPlayer?.Command("set", "sub-pos", "0");
            MediaPlayer?.Command("set", "sub-margin-x", "0");
        });
        root.Children.Add(PanelOptionCard(PanelSection(lang.SettingsSubFont, fontRow)));

        var styleRow = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 8),
            VerticalAlignment = VerticalAlignment.Top,
        };
        styleRow.Children.Add(PanelButtonRow(bold, italic, resetPos));
        root.Children.Add(PanelOptionCard(styleRow));

        var slower = new Button { Content = lang.PanelSlower, MinWidth = 72 };
        slower.Click += (_, _) => MediaPlayer?.Command("add", "sub-delay", "0.25");
        var normal = new Button { Content = lang.PanelNormal, MinWidth = 72 };
        normal.Click += (_, _) => MediaPlayer?.Command("set", "sub-delay", "0");
        var faster = new Button { Content = lang.PanelFaster, MinWidth = 72 };
        faster.Click += (_, _) => MediaPlayer?.Command("add", "sub-delay", "-0.25");
        root.Children.Add(PanelOptionCard(PanelSection(lang.PanelSync, PanelButtonRow(slower, normal, faster))));

        Button PadButton(string glyph, string name, Action click)
        {
            var b = new Button
            {
                Content = new FontIcon { Glyph = glyph, FontSize = 16, FontFamily = PanelIconFont },
                Width = 48,
                Height = 48,
                MinWidth = 0,
                MinHeight = 0,
                Padding = new Thickness(4),
            };
            AutomationProperties.SetName(b, name);
            ToolTipService.SetToolTip(b, name);
            b.Click += (_, _) => click();
            return b;
        }

        var pad = new Grid
        {
            Width = 160,
            Height = 160,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        for (var r = 0; r < 3; r++)
        {
            pad.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }
        for (var c = 0; c < 3; c++)
        {
            pad.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        var up = PadButton("\uF19C", lang.PanelMoveUp, () => MediaPlayer?.Command("add", "sub-pos", "-1"));
        var down = PadButton("\uF149", lang.PanelMoveDown, () => MediaPlayer?.Command("add", "sub-pos", "1"));
        var left = PadButton("\uF15C", lang.PanelMoveLeft, () => MediaPlayer?.Command("add", "sub-margin-x", "-5"));
        var right = PadButton("\uF182", lang.PanelMoveRight, () => MediaPlayer?.Command("add", "sub-margin-x", "5"));
        Grid.SetRow(up, 0);
        Grid.SetColumn(up, 1);
        Grid.SetRow(left, 1);
        Grid.SetColumn(left, 0);
        Grid.SetRow(right, 1);
        Grid.SetColumn(right, 2);
        Grid.SetRow(down, 2);
        Grid.SetColumn(down, 1);
        pad.Children.Add(up);
        pad.Children.Add(left);
        pad.Children.Add(right);
        pad.Children.Add(down);
        root.Children.Add(PanelOptionCard(PanelSection(lang.PanelMove, pad)));

    }
}
