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
        MinHeight = 44,
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
                    new FontIcon { Glyph = glyph, FontSize = 14, FontFamily = PanelIconFont },
                    new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                },
            },
            MinHeight = 32,
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
                    new FontIcon { Glyph = glyph, FontSize = 14, FontFamily = PanelIconFont },
                    new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                },
            },
            MinHeight = 32,
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
        var grid = new Grid { ColumnSpacing = 8 };
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
        var grid = new Grid { ColumnSpacing = 8, VerticalAlignment = VerticalAlignment.Center };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(new TextBlock
        {
            Text = labelText,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(content, 1);
        grid.Children.Add(content);
        return grid;
    }

    private static Grid PanelSliderWithReset(Slider slider, Button reset)
    {
        var grid = new Grid { ColumnSpacing = 8, VerticalAlignment = VerticalAlignment.Center };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        slider.HorizontalAlignment = HorizontalAlignment.Stretch;
        slider.VerticalAlignment = VerticalAlignment.Center;
        reset.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(slider);
        Grid.SetColumn(reset, 1);
        grid.Children.Add(reset);
        return grid;
    }

    private Button PanelResetButton(string property, Slider slider)
    {
        var button = new Button { Content = AppContext.AppLang.Reset };
        AutomationProperties.SetName(button, AppContext.AppLang.Reset);
        button.Click += (_, _) =>
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
        return button;
    }

    private Slider PanelPropertySlider(string property, double min, double max, double step, string? automationName = null)
    {
        var slider = new Slider { Minimum = min, Maximum = max, StepFrequency = step, Height = 20 };
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
}
