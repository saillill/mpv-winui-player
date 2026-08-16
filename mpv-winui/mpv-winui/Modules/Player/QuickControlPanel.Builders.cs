using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using mpv_winui.Modules.Common.View;
using System;

namespace mpv_winui.Modules.Player;

/// <summary>
/// Shared control factories for the quick-control sections. Cards and labels
/// use the app-wide MpvCardStyle / MpvCaptionTextStyle so the panel stays
/// consistent with the rest of the UI.
/// </summary>
public sealed partial class QuickControlPanel
{
    /// <summary>Bundled Fluent icon font (verified codepoints, see QuickControlPanel.xaml).</summary>
    internal static readonly FontFamily PanelIconFont = new(IconFonts.FluentSystemIconsUri);

    private Border PanelOptionCard(UIElement content) => new()
    {
        // Narrow horizontal cards: tighter vertical padding keeps single-row
        // controls (slider + value + reset) snug without clipping the slider.
        MinHeight = 40,
        Padding = new Thickness(12, 4, 12, 4),
        Style = (Style)Application.Current.Resources["MpvCardStyle"],
        Child = content,
    };

    private static Button PanelIconButton(string label, string glyph, Action click)
    {
        var button = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new FontIcon { Glyph = glyph, FontSize = 16, FontFamily = PanelIconFont },
                    new TextBlock { Text = label, FontSize = 14, VerticalAlignment = VerticalAlignment.Center },
                },
            },
            MinHeight = 40,
            MinWidth = 88,
            Padding = new Thickness(10, 4, 10, 4),
            CornerRadius = new CornerRadius(6),
        };
        AutomationProperties.SetName(button, label);
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>Large toggle button that fills with the accent color when checked.</summary>
    private static ToggleButton PanelToggleButton(string label, string glyph, Action<bool> onChange)
    {
        var button = new ToggleButton
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new FontIcon { Glyph = glyph, FontSize = 16, FontFamily = PanelIconFont },
                    new TextBlock { Text = label, FontSize = 14, VerticalAlignment = VerticalAlignment.Center },
                },
            },
            MinHeight = 40,
            MinWidth = 92,
            Padding = new Thickness(10, 4, 10, 4),
            CornerRadius = new CornerRadius(6),
        };
        AutomationProperties.SetName(button, label);
        var accent = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        var white = new SolidColorBrush(Microsoft.UI.Colors.White);
        button.Checked += (_, _) =>
        {
            button.Background = accent;
            button.Foreground = white;
            onChange(true);
        };
        button.Unchecked += (_, _) =>
        {
            button.ClearValue(Control.BackgroundProperty);
            button.ClearValue(Control.ForegroundProperty);
            onChange(false);
        };
        return button;
    }

    /// <summary>Lays buttons out in equal-width columns that fill the card.</summary>
    private static Grid PanelButtonRow(params FrameworkElement[] buttons)
    {
        // Equal star columns fill the card so every button row is balanced.
        var grid = new Grid { ColumnSpacing = 8, HorizontalAlignment = HorizontalAlignment.Stretch };
        for (var i = 0; i < buttons.Length; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttons[i].HorizontalAlignment = HorizontalAlignment.Stretch;
            buttons[i].MinWidth = 0;
            Grid.SetColumn(buttons[i], i);
            grid.Children.Add(buttons[i]);
        }
        return grid;
    }

    private static string EqPresetLabel(string id) => id switch
    {
        "flat" => AppContext.AppLang.OptionValueEqPresetFlat,
        "rock" => AppContext.AppLang.OptionValueEqPresetRock,
        "pop" => AppContext.AppLang.OptionValueEqPresetPop,
        "jazz" => AppContext.AppLang.OptionValueEqPresetJazz,
        "classical" => AppContext.AppLang.OptionValueEqPresetClassical,
        "electronic" => AppContext.AppLang.OptionValueEqPresetElectronic,
        "hiphop" => AppContext.AppLang.OptionValueEqPresetHipHop,
        "acoustic" => AppContext.AppLang.OptionValueEqPresetAcoustic,
        "vocal" => AppContext.AppLang.OptionValueEqPresetVocal,
        "bass" => AppContext.AppLang.OptionValueEqPresetBass,
        "treble" => AppContext.AppLang.OptionValueEqPresetTreble,
        "metal" => AppContext.AppLang.OptionValueEqPresetMetal,
        _ => id,
    };

    private static Grid PanelSection(string labelText, FrameworkElement content)
    {
        var grid = new Grid
        {
            ColumnSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
        };
        // Fixed label column keeps every slider at the same start x and the
        // same width across the video/subtitle rows.
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(new TextBlock
        {
            Text = labelText,
            // Centered in the fixed column: left-aligned short labels left a
            // large gap before the slider, right side felt cramped.
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(content, 1);
        grid.Children.Add(content);
        return grid;
    }

    private Slider PanelPropertySlider(string property, double min, double max, double step, string? automationName = null)
    {
        // Natural height: forcing 20px clipped the slider template's lower
        // half inside the card (user-reported cut).
        var slider = new Slider { Minimum = min, Maximum = max, StepFrequency = step };
        // WinUI reads the track thickness from SliderTrackThemeHeight.
        slider.Resources["SliderTrackThemeHeight"] = 8.0;
        if (!string.IsNullOrEmpty(automationName))
        {
            AutomationProperties.SetName(slider, automationName);
        }
        slider.ValueChanged += (_, _) =>
        {
            if (PanelUpdating)
            {
                return;
            }
            MediaPlayer?.Command("set", property, slider.Value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
        };
        return slider;
    }

    /// <summary>
    /// Horizontal slider row: label | slider | live value | reset. The fixed
    /// label column keeps every slider at the same start x and width.
    /// </summary>
    private Grid PanelSliderRow(string label, string property, double min, double max, double step)
    {
        var slider = PanelPropertySlider(property, min, max, step, label);
        var value = new TextBlock
        {
            FontSize = 11,
            MinWidth = 32,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
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
            VerticalAlignment = VerticalAlignment.Center,
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

        var sliderArea = new Grid
        {
            ColumnSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
        };
        sliderArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sliderArea.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        sliderArea.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        slider.HorizontalAlignment = HorizontalAlignment.Stretch;
        slider.VerticalAlignment = VerticalAlignment.Center;
        sliderArea.Children.Add(slider);
        Grid.SetColumn(value, 1);
        sliderArea.Children.Add(value);
        Grid.SetColumn(reset, 2);
        sliderArea.Children.Add(reset);
        return PanelSection(label, sliderArea);
    }

}
