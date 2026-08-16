using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using System;

namespace mpv_winui.Modules.Player;

/// <summary>
/// Video page of the quick-control panel: picture sliders as vertical
/// columns (name on top, track in the middle, value + reset at the bottom)
/// followed by the filter/zoom button rows.
/// </summary>
public sealed partial class QuickControlPanel
{
    private void BuildPanelVideo(StackPanel root)
    {
        var lang = AppContext.AppLang;
        var columns = new (string Name, string Property)[]
        {
            (lang.PanelBrightness, "brightness"),
            (lang.PanelContrast, "contrast"),
            (lang.PanelSaturation, "saturation"),
            (lang.PanelHue, "hue"),
        };

        var sliders = new Grid
        {
            ColumnSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        for (var i = 0; i < columns.Length; i++)
        {
            sliders.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var (name, property) = columns[i];

            var slider = PanelPropertySlider(property, -100, 100, 1, name);
            slider.Orientation = Orientation.Vertical;
            slider.Width = 36;
            slider.Height = 100;
            slider.HorizontalAlignment = HorizontalAlignment.Center;
            slider.VerticalAlignment = VerticalAlignment.Center;

            var value = new TextBlock
            {
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            value.Text = slider.Value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            slider.ValueChanged += (_, _) =>
                value.Text = slider.Value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

            var reset = new Button
            {
                Content = new FontIcon
                {
                    Glyph = "\uE777",
                    FontSize = 12,
                    FontFamily = PanelIconFont,
                },
                Width = 26,
                Height = 26,
                MinWidth = 0,
                MinHeight = 0,
                Padding = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            AutomationProperties.SetName(reset, AppContext.AppLang.Reset);
            ToolTipService.SetToolTip(reset, AppContext.AppLang.Reset);
            reset.Click += (_, _) =>
            {
                PanelUpdating = true;
                try
                {
                    slider.Value = 0;
                }
                finally
                {
                    PanelUpdating = false;
                }
                MediaPlayer?.Command("set", property, "0");
            };

            var column = new StackPanel
            {
                Spacing = 4,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
            };
            column.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            column.Children.Add(slider);
            column.Children.Add(value);
            column.Children.Add(reset);
            Grid.SetColumn(column, i);
            sliders.Children.Add(column);
        }
        root.Children.Add(PanelOptionCard(sliders));

        var sharp = PanelToggleButton(lang.PanelSharpen, "\uF47D",
            on => MediaPlayer?.Command("set", "vf", on ? "lavfi=[unsharp=5:5:1.0]" : ""));
        var blur = PanelToggleButton(lang.PanelBlur, "\uF8FB",
            on => MediaPlayer?.Command("set", "vf", on ? "lavfi=[gblur=sigma=1.0]" : ""));
        var post = PanelToggleButton(lang.PanelPost, "\uF489",
            on => MediaPlayer?.Command("set", "deband", on ? "yes" : "no"));
        var deinterlace = PanelToggleButton(lang.SettingsDeinterlace, "\uF2BE",
            on => MediaPlayer?.Command("set", "deinterlace", on ? "yes" : "no"));

        var effects = new StackPanel { Spacing = 8 };
        effects.Children.Add(PanelButtonRow(sharp, blur, post, deinterlace));
        effects.Children.Add(PanelButtonRow(
            PanelIconButton(lang.PanelRotate, "\uF13E",
                () => MediaPlayer?.Command(["cycle-values", "video-rotate", "90", "180", "270", "0"])),
            PanelIconButton(lang.PanelZoomIn, "\uF8C5",
                () => MediaPlayer?.Command("add", "video-zoom", "0.1")),
            PanelIconButton(lang.PanelZoomOut, "\uF8C7",
                () => MediaPlayer?.Command("add", "video-zoom", "-0.1")),
            PanelIconButton(lang.PanelZoomReset, "\uEE8D",
                () => MediaPlayer?.Command("set", "video-zoom", "0"))));
        root.Children.Add(PanelOptionCard(effects));
    }
}
