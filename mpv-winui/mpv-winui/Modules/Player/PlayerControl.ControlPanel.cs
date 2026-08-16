using Microsoft.UI.Xaml;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.Settings.Controls;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.UI;

namespace mpv_winui.Modules.Player
{
    /// <summary>
    /// Quick-control panel builders (audio/video/subtitle/playback sections).
    /// Split from PlayerControl.xaml.cs (audit C5); content is byte-identical.
    /// </summary>
    public sealed partial class PlayerControl
    {
        // ===== Unified control panel =====
        private bool _panelBuilt;
        private bool _panelUpdating;
        private Slider? _panelBrightnessSlider;
        private Slider? _panelContrastSlider;
        private Slider? _panelSaturationSlider;
        private Slider? _panelHueSlider;
        private ToggleButton? _panelEqOffToggle;
        private ComboBox? _panelAudioDeviceBox;
        private ComboBox? _panelFontBox;

        private void OnLanguageChanged()
        {
            // The quick-control panel captures AppLang strings when built;
            // drop it so the next open rebuilds with the new language (B5).
            _panelBuilt = false;
            if (ControlPanelFlyout.IsOpen)
            {
                EnsureControlPanel();
            }
        }

        private void ControlPanelFlyout_Opened(object sender, object e)
        {
            EnsureControlPanel();
        }

        private void EnsureControlPanel()
        {
            if (_panelBuilt)
            {
                return;
            }
            _panelBuilt = true;

            var lang = AppContext.AppLang;
            ControlPanelHost.ContentPanel.Children.Clear();

            var pivot = new Pivot
            {
                MinHeight = 340,
                Padding = new Thickness(0),
                IsHeaderItemsCarouselEnabled = false,
                IsTabStop = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            var pages = new (string Text, string Glyph, Action<StackPanel> Build)[]
            {
                (lang.SettingsCategoryAudio, "\uF472", BuildPanelAudio),
                (lang.SettingsCategoryVideo, "\uF84D", BuildPanelVideo),
                (lang.SettingsCategorySubtitles, "\uEBCD", BuildPanelSubtitles),
            };
            foreach (var (text, glyph, build) in pages)
            {
                var header = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                header.Children.Add(new FontIcon
                {
                    Glyph = glyph,
                    FontSize = 16,
                    FontFamily = PanelIconFont,
                    VerticalAlignment = VerticalAlignment.Center,
                });
                header.Children.Add(new TextBlock
                {
                    Text = text,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                });
                // Cards keep their natural height. The old filler-stretch
                // made the last card huge and hid its content (giant empty
                // EQ/移动/按钮 cards) - removed.
                var cards = new StackPanel
                {
                    Spacing = 8,
                    Margin = new Thickness(0, 8, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                build(cards);
                // Never let a card stretch to fill leftover page height
                // (that was what inflated the EQ/移动/按钮 cards).
                foreach (var child in cards.Children)
                {
                    if (child is FrameworkElement fe)
                    {
                        fe.VerticalAlignment = VerticalAlignment.Top;
                    }
                }
                pivot.Items.Add(new PivotItem { Header = header, Content = cards });
            }
            ControlPanelHost.ContentPanel.Children.Add(pivot);
        }

        private Border PanelOptionCard(UIElement content) => new()
        {
            MinHeight = 44,
            Padding = new Thickness(12, 6, 12, 6),
            CornerRadius = new CornerRadius(4),
            Background = mpv_winui.Modules.Common.View.ThemeResource.Brush(this, "CardBackgroundFillColorDefaultBrush"),
            Child = content,
        };

        /// <summary>Bundled Fluent icon font (verified codepoints, see QuickControlPanel.xaml).</summary>
        private static readonly FontFamily PanelIconFont =
            new("ms-appx:///Assets/FluentSystemIcons-Regular.ttf#FluentSystemIcons-Regular");

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
                _panelUpdating = true;
                try
                {
                    slider.Value = 0;
                }
                finally
                {
                    _panelUpdating = false;
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
                if (_panelUpdating)
                {
                    return;
                }
                MediaPlayer?.Command("set", property, slider.Value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            };
            return slider;
        }

        private void UpdatePanelEqToggleLabel()
        {
            if (_panelEqOffToggle is not { } toggle)
            {
                return;
            }
            var lang = AppContext.AppLang;
            toggle.Content = $"{lang.PanelEqualizer} {(toggle.IsChecked == true ? lang.Off : lang.PanelOn)}";
        }

        private void BuildPanelAudio(StackPanel root)
        {
            var lang = AppContext.AppLang;

            _panelEqOffToggle = new ToggleButton
            {
                IsChecked = true,
                MinWidth = 0,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            AutomationProperties.SetName(_panelEqOffToggle, lang.PanelEqualizer);
            UpdatePanelEqToggleLabel();
            _panelEqOffToggle.Checked += (_, _) =>
            {
                if (!_panelUpdating)
                {
                    MediaPlayer?.Command("set", "af", "");
                }
                UpdatePanelEqToggleLabel();
            };
            _panelEqOffToggle.Unchecked += (_, _) =>
            {
                if (!_panelUpdating)
                {
                    ApplyEqualizer();
                }
                UpdatePanelEqToggleLabel();
            };

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
                if (_panelUpdating || _panelAudioDeviceBox.SelectedItem is not ComboBoxItem { Tag: string name })
                {
                    return;
                }
                MediaPlayer?.Command("set", "audio-device", name);
            };

            var presetBox = new ComboBox
            {
                MinWidth = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = string.Empty,
            };
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
            var presetRow = new Grid { ColumnSpacing = 8 };
            presetRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            presetRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            presetRow.Children.Add(new TextBlock
            {
                Text = lang.PanelPresetFont,
                VerticalAlignment = VerticalAlignment.Center,
            });
            Grid.SetColumn(presetBox, 1);
            presetRow.Children.Add(presetBox);

            var topRow = new Grid { ColumnSpacing = 8 };
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.Children.Add(_panelEqOffToggle);
            Grid.SetColumn(_panelAudioDeviceBox, 1);
            topRow.Children.Add(_panelAudioDeviceBox);
            root.Children.Add(PanelOptionCard(topRow));
            root.Children.Add(PanelOptionCard(presetRow));

            var bandLabels = new[] { "60", "170", "310", "600", "1K", "3K", "6K", "12K", "14K", "16K" };
            var bands = ControlPanelHost.EqualizerBands;
            bands.Clear();
            for (var i = 0; i < bandLabels.Length; i++)
            {
                var index = i;
                var band = new EqualizerBand(bandLabels[i], _eqGains[i]);
                band.ValueChanged += value =>
                {
                    _eqGains[index] = value;
                    if (!_panelUpdating)
                    {
                        ApplyEqualizer();
                    }
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
                    ContentTemplate = (DataTemplate)ControlPanelHost.Resources["EqualizerBandTemplate"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(cell, i);
                bandGrid.Children.Add(cell);
            }
            root.Children.Add(PanelOptionCard(bandGrid));
        }

        private void ApplyPanelPreset(double[] gains)
        {
            for (var i = 0; i < _eqGains.Count && i < gains.Length; i++)
            {
                _eqGains[i] = gains[i];
            }
            _panelUpdating = true;
            try
            {
                var bands = ControlPanelHost.EqualizerBands;
                for (var i = 0; i < bands.Count && i < gains.Length; i++)
                {
                    bands[i].Value = gains[i];
                }
            }
            finally
            {
                _panelUpdating = false;
            }
            if (_panelEqOffToggle is { } toggle)
            {
                if (toggle.IsChecked == true)
                {
                    toggle.IsChecked = false; // the Unchecked handler applies the curve
                }
                else
                {
                    ApplyEqualizer();
                }
            }
        }

        private void BuildPanelVideo(StackPanel root)
        {
            var lang = AppContext.AppLang;
            _panelBrightnessSlider = PanelPropertySlider("brightness", -100, 100, 1, lang.PanelBrightness);
            _panelContrastSlider = PanelPropertySlider("contrast", -100, 100, 1, lang.PanelContrast);
            _panelSaturationSlider = PanelPropertySlider("saturation", -100, 100, 1, lang.PanelSaturation);
            _panelHueSlider = PanelPropertySlider("hue", -100, 100, 1, lang.PanelHue);

            root.Children.Add(PanelOptionCard(PanelSection(
                lang.PanelBrightness,
                PanelSliderWithReset(_panelBrightnessSlider, PanelResetButton("brightness", _panelBrightnessSlider)))));
            root.Children.Add(PanelOptionCard(PanelSection(
                lang.PanelContrast,
                PanelSliderWithReset(_panelContrastSlider, PanelResetButton("contrast", _panelContrastSlider)))));
            root.Children.Add(PanelOptionCard(PanelSection(
                lang.PanelSaturation,
                PanelSliderWithReset(_panelSaturationSlider, PanelResetButton("saturation", _panelSaturationSlider)))));
            root.Children.Add(PanelOptionCard(PanelSection(
                lang.PanelHue,
                PanelSliderWithReset(_panelHueSlider, PanelResetButton("hue", _panelHueSlider)))));

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
                if (_panelUpdating)
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
                if (_panelUpdating || double.IsNaN(sizeBox.Value))
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
            root.Children.Add(PanelOptionCard(PanelSection(lang.SettingsSubFont, fontRow)));


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
}
