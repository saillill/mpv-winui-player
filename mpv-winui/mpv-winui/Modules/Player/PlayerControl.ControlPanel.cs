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
        private Slider? _panelVolumeSlider;
        private Slider? _panelBrightnessSlider;
        private Slider? _panelContrastSlider;
        private Slider? _panelSaturationSlider;
        private Slider? _panelHueSlider;
        private ToggleButton? _panelEqOffToggle;
        private ComboBox? _panelAudioDeviceBox;
        private ComboBox? _panelFontBox;
        private TextBox? _panelAbStartBox;
        private TextBox? _panelAbEndBox;

        private void OnLanguageChanged()
        {
            // The quick-control panel captures AppLang strings when built;
            // drop it so the next open rebuilds with the new language (B5).
            _panelBuilt = false;
            if (ControlPanelFlyout.IsOpen)
            {
                EnsureControlPanel();
                SyncPanelValues();
            }
        }

        private void ControlPanelFlyout_Opened(object sender, object e)
        {
            EnsureControlPanel();
            SyncPanelValues();
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
                MinHeight = 360,
                IsHeaderItemsCarouselEnabled = false,
                IsTabStop = false,
            };
            var pages = new (string Text, string Glyph, Action<StackPanel> Build)[]
            {
                (lang.SettingsCategoryAudio, "\uE8B1", BuildPanelAudio),
                (lang.SettingsCategoryVideo, "\uE790", BuildPanelVideo),
                (lang.SettingsCategorySubtitles, "\uED1F", BuildPanelSubtitles),
                (lang.SettingsCategoryPlayback, "\uE768", BuildPanelPlayback),
            };
            foreach (var (text, glyph, build) in pages)
            {
                var header = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Width = 126,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                header.Children.Add(new FontIcon { Glyph = glyph, FontSize = 16, VerticalAlignment = VerticalAlignment.Center });
                header.Children.Add(new TextBlock
                {
                    Text = text,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                });
                var content = new StackPanel { Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
                build(content);
                pivot.Items.Add(new PivotItem { Header = header, Content = content });
            }
            ControlPanelHost.ContentPanel.Children.Add(pivot);
        }

        private Border PanelOptionCard(UIElement content) => new()
        {
            MinHeight = 48,
            Padding = new Thickness(12, 8, 12, 8),
            CornerRadius = new CornerRadius(4),
            Background = mpv_winui.Modules.Common.View.ThemeResource.Brush(this, "CardBackgroundFillColorDefaultBrush"),
            Child = content,
        };

        private static Grid PanelSection(string labelText, FrameworkElement content)
        {
            var grid = new Grid { ColumnSpacing = 8 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(new TextBlock { Text = labelText, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(content, 1);
            grid.Children.Add(content);
            return grid;
        }

        private static Grid PanelSliderWithReset(Slider slider, Button reset)
        {
            var grid = new Grid { ColumnSpacing = 8 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            slider.HorizontalAlignment = HorizontalAlignment.Stretch;
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
            var slider = new Slider { Minimum = min, Maximum = max, StepFrequency = step };
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

            var presetButton = new Button { Content = lang.PanelPreset, MinWidth = 0 };
            AutomationProperties.SetName(presetButton, lang.PanelPreset);
            presetButton.Flyout = BuildPanelPresetFlyout();

            var topRow = new Grid { ColumnSpacing = 8 };
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.Children.Add(_panelEqOffToggle);
            Grid.SetColumn(_panelAudioDeviceBox, 1);
            topRow.Children.Add(_panelAudioDeviceBox);
            Grid.SetColumn(presetButton, 2);
            topRow.Children.Add(presetButton);
            root.Children.Add(PanelOptionCard(topRow));

            var bandLabels = new[] { "60", "170", "310", "600", "1K", "3K", "6K", "12K", "14K", "16K" };
            var bandRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
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
            var bandList = new ItemsControl
            {
                ItemTemplate = (DataTemplate)ControlPanelHost.Resources["EqualizerBandTemplate"],
                ItemsPanel = (ItemsPanelTemplate)ControlPanelHost.Resources["EqualizerBandsPanelTemplate"],
                ItemsSource = bands,
            };
            bandRow.Children.Add(bandList);

            _panelVolumeSlider = PanelPropertySlider("volume", 0, 150, 1, lang.PanelMasterVolume);
            _panelVolumeSlider.Orientation = Orientation.Vertical;
            _panelVolumeSlider.Height = 230;
            _panelVolumeSlider.Width = 44;
            _panelVolumeSlider.HorizontalAlignment = HorizontalAlignment.Center;
            var volumeColumn = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
            var volumeLabel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            volumeLabel.Children.Add(new FontIcon { Glyph = "\uE995", FontSize = 14 });
            volumeLabel.Children.Add(new TextBlock { Text = lang.PanelMasterVolume, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
            volumeColumn.Children.Add(volumeLabel);
            volumeColumn.Children.Add(_panelVolumeSlider);

            var audioGrid = new Grid { ColumnSpacing = 10 };
            audioGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            audioGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
            audioGrid.Children.Add(PanelOptionCard(bandRow));
            var volumeCard = PanelOptionCard(volumeColumn);
            Grid.SetColumn(volumeCard, 1);
            audioGrid.Children.Add(volumeCard);
            root.Children.Add(audioGrid);
        }

        private MenuFlyout BuildPanelPresetFlyout()
        {
            var flyout = new MenuFlyout();
            var presets = new (string Name, double[] Gains)[]
            {
                ("Flat", [0, 0, 0, 0, 0, 0, 0, 0, 0, 0]),
                ("Bass", [6, 5, 4, 2, 0, 0, 0, 0, 0, 0]),
                ("Vocal", [0, -2, 0, 3, 4, 4, 3, 2, 0, 0]),
                ("Treble", [0, 0, 0, 0, 0, 1, 2, 3, 4, 5]),
            };
            foreach (var preset in presets)
            {
                var item = new MenuFlyoutItem { Text = preset.Name, Tag = preset.Gains };
                item.Click += (_, _) => ApplyPanelPreset((double[])item.Tag);
                flyout.Items.Add(item);
            }
            return flyout;
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

            var sharp = new CheckBox { Content = lang.PanelSharpen, MinWidth = 0 };
            sharp.Checked += (_, _) => MediaPlayer?.Command("set", "vf", "lavfi=[unsharp=5:5:1.0]");
            sharp.Unchecked += (_, _) => MediaPlayer?.Command("set", "vf", "");
            var blur = new CheckBox { Content = lang.PanelBlur, MinWidth = 0 };
            blur.Checked += (_, _) => MediaPlayer?.Command("set", "vf", "lavfi=[gblur=sigma=1.0]");
            blur.Unchecked += (_, _) => MediaPlayer?.Command("set", "vf", "");
            var post = new CheckBox { Content = lang.PanelPost, MinWidth = 0 };
            post.Checked += (_, _) => MediaPlayer?.Command("set", "deband", "yes");
            post.Unchecked += (_, _) => MediaPlayer?.Command("set", "deband", "no");

            var capture = new Button
            {
                MinWidth = 0,
                Content = new FontIcon { Glyph = "\uE722", FontSize = 16 },
            };
            ToolTipService.SetToolTip(capture, lang.PanelCapture);
            AutomationProperties.SetName(capture, lang.PanelCapture);
            capture.Click += (_, _) => MediaPlayer?.Command("screenshot");

            var bottom = new Grid { ColumnSpacing = 8 };
            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var toggles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
            toggles.Children.Add(sharp);
            toggles.Children.Add(blur);
            toggles.Children.Add(post);
            bottom.Children.Add(toggles);
            Grid.SetColumn(capture, 1);
            bottom.Children.Add(capture);
            root.Children.Add(PanelOptionCard(bottom));
        }

        private void BuildPanelSubtitles(StackPanel root)
        {
            var lang = AppContext.AppLang;

            _panelFontBox = new ComboBox
            {
                IsEditable = true,
                MinWidth = 150,
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

            var fontRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            fontRow.Children.Add(_panelFontBox);
            fontRow.Children.Add(sizeBox);
            root.Children.Add(PanelOptionCard(PanelSection(lang.SettingsSubFont, fontRow)));

            var moves = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var up = new Button { Content = lang.PanelMoveUp, MinWidth = 72 };
            up.Click += (_, _) => MediaPlayer?.Command("add", "sub-pos", "-1");
            var down = new Button { Content = lang.PanelMoveDown, MinWidth = 72 };
            down.Click += (_, _) => MediaPlayer?.Command("add", "sub-pos", "1");
            var left = new Button { Content = lang.PanelMoveLeft, MinWidth = 72 };
            left.Click += (_, _) => MediaPlayer?.Command("add", "sub-margin-x", "-5");
            var right = new Button { Content = lang.PanelMoveRight, MinWidth = 72 };
            right.Click += (_, _) => MediaPlayer?.Command("add", "sub-margin-x", "5");
            moves.Children.Add(up);
            moves.Children.Add(down);
            moves.Children.Add(left);
            moves.Children.Add(right);
            root.Children.Add(PanelOptionCard(PanelSection(lang.PanelMove, moves)));

            var syncRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var slower = new Button { Content = lang.PanelSlower, MinWidth = 72 };
            slower.Click += (_, _) => MediaPlayer?.Command("add", "sub-delay", "0.25");
            var normal = new Button { Content = lang.PanelNormal, MinWidth = 72 };
            normal.Click += (_, _) => MediaPlayer?.Command("set", "sub-delay", "0");
            var faster = new Button { Content = lang.PanelFaster, MinWidth = 72 };
            faster.Click += (_, _) => MediaPlayer?.Command("add", "sub-delay", "-0.25");
            syncRow.Children.Add(slower);
            syncRow.Children.Add(normal);
            syncRow.Children.Add(faster);
            root.Children.Add(PanelOptionCard(PanelSection(lang.PanelSync, syncRow)));
        }

        private static Button PanelSeekButton(string text, bool left)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            if (left)
            {
                content.Children.Add(new FontIcon { Glyph = "\uE72B", FontSize = 12 });
                content.Children.Add(new FontIcon { Glyph = "\uE72B", FontSize = 12 });
                content.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
            }
            else
            {
                content.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
                content.Children.Add(new FontIcon { Glyph = "\uE72A", FontSize = 12 });
                content.Children.Add(new FontIcon { Glyph = "\uE72A", FontSize = 12 });
            }
            var button = new Button { Content = content };
            AutomationProperties.SetName(button, text);
            return button;
        }

        private void BuildPanelPlayback(StackPanel root)
        {
            var lang = AppContext.AppLang;

            var seeks = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var seekDefs = new (string Text, bool Left, int Delta)[]
            {
                ("1min", true, -60),
                ("5sec", true, -5),
                ("5sec", false, 5),
                ("1min", false, 60),
            };
            foreach (var (text, left, delta) in seekDefs)
            {
                var button = PanelSeekButton(text, left);
                var offset = delta;
                button.Click += (_, _) =>
                {
                    if (MediaPlayer is not { } player)
                    {
                        return;
                    }
                    var target = player.Position + offset;
                    player.Command("seek", target.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture), "absolute");
                };
                seeks.Children.Add(button);
            }
            root.Children.Add(PanelOptionCard(PanelSection(lang.PanelSeek, seeks)));

            var speeds = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var slower = new Button { Content = lang.PanelSlower, MinWidth = 88 };
            slower.Click += (_, _) => MediaPlayer?.Command("add", "speed", "-0.1");
            var normal = new Button { Content = lang.PanelNormal, MinWidth = 88 };
            normal.Click += (_, _) => MediaPlayer?.Command("set", "speed", "1");
            var faster = new Button { Content = lang.PanelFaster, MinWidth = 88 };
            faster.Click += (_, _) => MediaPlayer?.Command("add", "speed", "0.1");
            speeds.Children.Add(slower);
            speeds.Children.Add(normal);
            speeds.Children.Add(faster);
            root.Children.Add(PanelOptionCard(PanelSection(lang.PanelSpeed, speeds)));

            var aButton = new Button { Content = "A", MinWidth = 44 };
            aButton.Click += (_, _) =>
            {
                MediaPlayer?.Command("ab-loop-a");
                SyncPanelAbLoop();
            };
            var bButton = new Button { Content = "B", MinWidth = 44 };
            bButton.Click += (_, _) =>
            {
                MediaPlayer?.Command("ab-loop-b");
                SyncPanelAbLoop();
            };
            var resetButton = new Button { Content = lang.Reset, MinWidth = 56 };
            resetButton.Click += (_, _) =>
            {
                MediaPlayer?.Command("ab-loop-a", "no");
                MediaPlayer?.Command("ab-loop-b", "no");
                SyncPanelAbLoop();
            };

            _panelAbStartBox = new TextBox { Text = "00:00:00", Width = 88, HorizontalContentAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center };
            _panelAbStartBox.LostFocus += (_, _) => CommitAbLoopTime(_panelAbStartBox, "ab-loop-a");
            _panelAbStartBox.KeyDown += (_, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    CommitAbLoopTime(_panelAbStartBox, "ab-loop-a");
                }
            };
            _panelAbEndBox = new TextBox { Text = "00:00:00", Width = 88, HorizontalContentAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center };
            _panelAbEndBox.LostFocus += (_, _) => CommitAbLoopTime(_panelAbEndBox, "ab-loop-b");
            _panelAbEndBox.KeyDown += (_, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    CommitAbLoopTime(_panelAbEndBox, "ab-loop-b");
                }
            };

            var repeatRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
            };
            repeatRow.Children.Add(aButton);
            repeatRow.Children.Add(_panelAbStartBox);
            repeatRow.Children.Add(new TextBlock { Text = "~", VerticalAlignment = VerticalAlignment.Center });
            repeatRow.Children.Add(_panelAbEndBox);
            repeatRow.Children.Add(bButton);
            repeatRow.Children.Add(resetButton);
            root.Children.Add(PanelOptionCard(PanelSection(lang.MoreRepeat, repeatRow)));
        }

        private void CommitAbLoopTime(TextBox box, string property)
        {
            if (TryParsePanelTime(box.Text, out var seconds))
            {
                MediaPlayer?.Command("set", property, seconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            }
            else
            {
                var current = property == "ab-loop-a" ? MediaPlayer?.AbLoopA ?? 0 : MediaPlayer?.AbLoopB ?? 0;
                box.Text = FormatPanelTime(current);
            }
        }

        private static bool TryParsePanelTime(string? text, out double seconds)
        {
            seconds = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }
            if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out seconds))
            {
                return true;
            }
            var parts = text.Split(':');
            if (parts.Length is 2 or 3)
            {
                var values = new int[parts.Length];
                for (var i = 0; i < parts.Length; i++)
                {
                    if (!int.TryParse(parts[i], out values[i]))
                    {
                        return false;
                    }
                }
                seconds = values.Length == 3
                    ? values[0] * 3600 + values[1] * 60 + values[2]
                    : values[0] * 60 + values[1];
                return true;
            }
            return false;
        }

        private void SyncPanelValues()
        {
            if (MediaPlayer is not { } player)
            {
                return;
            }

            _panelUpdating = true;
            try
            {
                _panelVolumeSlider!.Value = player.Volume;
                SyncPanelAbLoop();
            }
            finally
            {
                _panelUpdating = false;
            }
        }

        private void SyncPanelAbLoop()
        {
            if (_panelAbStartBox is null || _panelAbEndBox is null)
            {
                return;
            }
            _panelAbStartBox.Text = FormatPanelTime(MediaPlayer?.AbLoopA ?? 0);
            _panelAbEndBox.Text = FormatPanelTime(MediaPlayer?.AbLoopB ?? 0);
        }

        private static string FormatPanelTime(double seconds)
        {
            if (seconds <= 0)
            {
                return "00:00:00";
            }
            var t = TimeSpan.FromSeconds(seconds);
            return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
        }
    }
}
