using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using mpv_winui.Modules.AppModel;
using mpv_winui.Modules.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using AppInstance = Microsoft.Windows.AppLifecycle.AppInstance;

namespace mpv_winui.Modules.Player
{
public sealed partial class MpvPlayerPage
{
    private Microsoft.UI.Xaml.DispatcherTimer? _sleepTimer;

    private async void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuFlyoutItem { Tag: string tag })
                {
                    switch (tag)
                    {
                        case "open":
                            await OpenFileAsync();
                            break;
                        case "open-folder":
                            await OpenFolderAsync();
                            break;
                        case "open-url":
                            await OpenUrlAsync();
                            break;
                        case "open-clipboard":
                            await OpenClipboardAsync();
                            break;
                        case "open-dvd":
                            await OpenDvdAsync();
                            break;
                        case "open-bd":
                            await OpenBdAsync();
                            break;
                        case "load-subtitle":
                            await LoadSubtitleAsync();
                            break;
                        case "screenshot":
                            await _mediaPlayer.RunCommandAsync(["screenshot"]);
                            break;
                        case "screenshot-no-sub":
                            await _mediaPlayer.RunCommandAsync(["screenshot", "video"]);
                            break;
                        case "conf-folder":
                        {
                            var storageFolder = await AppData.Current.OpenLocalDataFolderAsync();
                            await Launcher.LaunchFolderAsync(storageFolder);
                            break;
                        }
                        case "mpv-folder":
                        {
                            var storageFolder = await AppData.Current.OpenOrCreateLocalDataFolderAsync(MpvConfigFolderName);
                            await Launcher.LaunchFolderAsync(storageFolder);
                            break;
                        }
                        case "playlist":
                        {
                            TogglePlaylist(true);
                            break;
                        }
                        case "open-watch-history":
                        {
                            await ShowWatchHistoryDialogAsync();
                            break;
                        }
                        case "open-watch-later":
                        {
                            await ShowWatchLaterDialogAsync();
                            break;
                        }
                        case "restart":
                        {
                            if (App.Window is MainWindow mainWindow)
                            {
                                mainWindow.SaveWindowPositionAndSize();
                            }
                            AppInstance.Restart("Reset");
                            break;
                        }
                        case "about":
                            await ShowAboutDialogAsync();
                            break;
                        case "mpv-docs":
                            await Launcher.LaunchUriAsync(new Uri("https://mpv.io/manual/master/"));
                            break;
                        case "quit":
                            AppQuit();
                            break;
                        case "fullwindow":
                            PlayerControl.ToggleFullWindow();
                            break;
                        case "fullscreen":
                            PlayerControl.ToggleFullScreen();
                            break;
                        case "options":
                            ShowSettingsWindow();
                            break;
                        case "mpv-command":
                            await ShowMpvCommandDialogAsync();
                            break;
                        case "shortcut-search":
                            await ShowShortcutSearchDialogAsync();
                            break;
                        case "sleep-off":
                            SetSleepTimer(0);
                            break;
                        case "sleep-15":
                            SetSleepTimer(15);
                            break;
                        case "sleep-30":
                            SetSleepTimer(30);
                            break;
                        case "sleep-45":
                            SetSleepTimer(45);
                            break;
                        case "sleep-60":
                            SetSleepTimer(60);
                            break;
                        case "sleep-90":
                            SetSleepTimer(90);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                OnException(ex);
            }
        }

        private void SetSleepTimer(int minutes)
        {
            _sleepTimer?.Stop();
            _sleepTimer = null;

            if (minutes <= 0)
            {
                AppContext.SendMpvCommand($"show-text {QuoteForMpv(AppContext.AppLang.SleepTimerCanceled)}");
                return;
            }

            _sleepTimer = new Microsoft.UI.Xaml.DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(minutes)
            };
            _sleepTimer.Tick += (_, _) =>
            {
                _sleepTimer?.Stop();
                _mediaPlayer.Pause();
                AppContext.SendMpvCommand($"show-text {QuoteForMpv(AppContext.AppLang.SleepTimerFinished)}");
            };
            _sleepTimer.Start();
            AppContext.SendMpvCommand(
                $"show-text {QuoteForMpv(string.Format(AppContext.AppLang.SleepTimerSetMessage, minutes))}");
        }

        private static string QuoteForMpv(string value) =>
            $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

        private async Task ShowMpvCommandDialogAsync()
        {
            var input = new TextBox
            {
                PlaceholderText = AppContext.AppLang.SettingsCommandPlaceholder,
                MinWidth = 360,
                AcceptsReturn = false
            };
            var dialog = new ContentDialog
            {
                Title = AppContext.AppLang.SettingsCommandMenuItem,
                Content = input,
                PrimaryButtonText = AppContext.AppLang.Ok,
                CloseButtonText = AppContext.AppLang.Cancel,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            input.KeyDown += (_, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    RunMpvCommandInput(input.Text);
                    dialog.Hide();
                }
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                RunMpvCommandInput(input.Text);
            }
        }

        private static void RunMpvCommandInput(string? text)
        {
            var command = text?.Trim();
            if (!string.IsNullOrEmpty(command))
            {
                AppContext.SendMpvCommand(command);
            }
        }

        private async Task ShowShortcutSearchDialogAsync()
        {
            var search = new TextBox
            {
                PlaceholderText = AppContext.AppLang.ShortcutSearchPlaceholder,
                MinWidth = 440
            };
            var list = new ListView
            {
                MaxHeight = 380,
                SelectionMode = ListViewSelectionMode.None
            };
            var empty = new TextBlock
            {
                Text = AppContext.AppLang.ShortcutSearchEmpty,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gray),
                Visibility = Visibility.Collapsed
            };
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(search);
            panel.Children.Add(list);
            panel.Children.Add(empty);

            var bindings = LoadBindings();
            var query = string.Empty;
            void Refresh()
            {
                var items = bindings
                    .Where(b => string.IsNullOrWhiteSpace(query)
                                || b.Key.Contains(query, StringComparison.OrdinalIgnoreCase)
                                || b.Command.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Select(b => $"{b.Key}    {b.Command}")
                    .ToList();
                list.ItemsSource = items;
                empty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            search.TextChanged += (_, _) =>
            {
                query = search.Text ?? string.Empty;
                Refresh();
            };
            Refresh();

            var dialog = new ContentDialog
            {
                Title = AppContext.AppLang.ShortcutSearchTitle,
                Content = panel,
                CloseButtonText = AppContext.AppLang.Cancel,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }

        private static List<(string Key, string Command)> LoadBindings()
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "mpv-winui",
                "mpv",
                "input.conf");
            if (!File.Exists(path))
            {
                return [];
            }

            var result = new List<(string, string)>();
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                var separator = line.IndexOfAny([' ', '\t']);
                if (separator <= 0)
                {
                    continue;
                }

                var key = line[..separator].Trim();
                var command = line[separator..].Trim();
                if (key.Length > 0 && command.Length > 0)
                {
                    result.Add((key, command));
                }
            }
            return result;
        }

        private void ShowSettingsWindow()
        {
            if (App.Window is MainWindow window)
            {
                window.OpenSettingWindow();
            }
        }

        private async Task ShowAboutDialogAsync()
        {
            var stack = new StackPanel { Spacing = 12, MinWidth = 400 };

            stack.Children.Add(new TextBlock
            {
                Text = PackageHelper.AppName,
                FontSize = 20,
                FontWeight = new Windows.UI.Text.FontWeight(600)
            });

            stack.Children.Add(new TextBlock
            {
                Text = PackageHelper.AppVersion,
                TextWrapping = TextWrapping.Wrap
            });

            stack.Children.Add(new TextBlock
            {
                Text = "mpv",
                TextWrapping = TextWrapping.Wrap
            });
            var mpvLink = new HyperlinkButton
            {
                Content = "github.com/mpv-player/mpv",
                NavigateUri = new Uri("https://github.com/mpv-player/mpv"),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            stack.Children.Add(mpvLink);
            var mpvDocsLink = new HyperlinkButton
            {
                Content = "mpv.io/manual/master (官方文档)",
                NavigateUri = new Uri("https://mpv.io/manual/master/"),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            stack.Children.Add(mpvDocsLink);

            stack.Children.Add(new TextBlock
            {
                Text = "mpv-winui-player",
                TextWrapping = TextWrapping.Wrap
            });
            var projectLink = new HyperlinkButton
            {
                Content = "github.com/saillill/mpv-winui-player",
                NavigateUri = new Uri("https://github.com/saillill/mpv-winui-player"),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            stack.Children.Add(projectLink);

            var dialog = new ContentDialog
            {
                Title = AppContext.AppLang.HelpAbout,
                Content = stack,
                CloseButtonText = AppContext.AppLang.Ok,
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
