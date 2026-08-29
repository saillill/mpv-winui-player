using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using mpv_winui.Modules.AppModel;
using mpv_winui.Modules.FileSystem;
using mpv_winui.Modules.Player.Menu;
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
    /// <summary>
    /// Menu action registry: action id -> handler. Extensible — any module can
    /// call <see cref="RegisterMenuAction"/> to add a menu action without
    /// touching the built-in switch, and <see cref="KnownMenuActions"/> (used
    /// by the menu builder and the schema check) derives from it.
    /// </summary>
    private static readonly Dictionary<string, Func<MpvPlayerPage, Task>> MenuActions = new(StringComparer.Ordinal);

    public static IReadOnlySet<string> KnownMenuActions => MenuActions.Keys.ToHashSet(StringComparer.Ordinal);

    private static void RegisterMenuAction(string id, Func<MpvPlayerPage, Task> handler)
    {
        MenuActions[id] = handler;
    }

    static MpvPlayerPage()
    {
        RegisterMenuAction("open", p => p.OpenFileAsync());
        RegisterMenuAction("open-folder", p => p.OpenFolderAsync());
        RegisterMenuAction("open-url", p => p.OpenUrlAsync());
        RegisterMenuAction("open-clipboard", p => p.OpenClipboardAsync());
        RegisterMenuAction("open-dvd", p => p.OpenDvdAsync());
        RegisterMenuAction("open-bd", p => p.OpenBdAsync());
        RegisterMenuAction("load-subtitle", p => p.LoadSubtitleAsync());
        RegisterMenuAction("screenshot", p => p._mediaPlayer.RunCommandAsync(["screenshot"]).AsTask());
        RegisterMenuAction("screenshot-no-sub", p => p._mediaPlayer.RunCommandAsync(["screenshot", "video"]).AsTask());
        RegisterMenuAction("conf-folder", async p =>
        {
            var storageFolder = await AppData.Current.OpenLocalDataFolderAsync();
            await Launcher.LaunchFolderAsync(storageFolder);
        });
        RegisterMenuAction("mpv-folder", async p =>
        {
            var storageFolder = await AppData.Current.OpenOrCreateLocalDataFolderAsync(MpvConfigFolderName);
            await Launcher.LaunchFolderAsync(storageFolder);
        });
        RegisterMenuAction("playlist", p =>
        {
            p.TogglePlaylist(true);
            return Task.CompletedTask;
        });
        RegisterMenuAction("playlist-import", p => p.ImportPlaylistAsync());
        RegisterMenuAction("playlist-export", p => p.ExportPlaylistAsync());
        RegisterMenuAction("open-watch-history", p => p.ShowWatchHistoryDialogAsync());
        RegisterMenuAction("open-watch-later", p => p.ShowWatchLaterDialogAsync());
        RegisterMenuAction("restart", p =>
        {
            if (App.Window is MainWindow mainWindow)
            {
                mainWindow.SaveWindowPositionAndSize();
            }
            AppInstance.Restart("Reset");
            return Task.CompletedTask;
        });
        RegisterMenuAction("about", p => p.ShowAboutDialogAsync());
        RegisterMenuAction("display-info", p => p.ShowDisplayInfoDialogAsync());
        RegisterMenuAction("mpv-docs", p =>
        {
            return Launcher.LaunchUriAsync(new Uri("https://mpv.io/manual/master/")).AsTask();
        });
        RegisterMenuAction("quit", p =>
        {
            p.AppQuit();
            return Task.CompletedTask;
        });
        RegisterMenuAction("fullwindow", p =>
        {
            p.PlayerControl.ToggleFullWindow();
            return Task.CompletedTask;
        });
        RegisterMenuAction("fullscreen", p =>
        {
            p.PlayerControl.ToggleFullScreen();
            return Task.CompletedTask;
        });
        RegisterMenuAction("options", p =>
        {
            p.ShowSettingsWindow();
            return Task.CompletedTask;
        });
        RegisterMenuAction("mpv-command", p => p.ShowMpvCommandDialogAsync());
        RegisterMenuAction("shortcut-search", p => p.ShowShortcutSearchDialogAsync());
    }

    private void BuildMainMenuBar()
    {
        var menus = MenuDefinitionSource.TryLoad();
        if (menus is { Count: > 0 })
        {
            MenuBarBuilder.Build(MainMenuBar, menus, KnownMenuActions, MenuFlyoutItem_Click,
                MenuShortcutHints.FindForCommand);
        }
    }

    /// <summary>Rebuilds the menu bar (used after menu definition or language changes).</summary>
    public void RebuildMenuBar() => BuildMainMenuBar();

    private async void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuFlyoutItem { Tag: MenuDefinition def })
                {
                    if (!string.IsNullOrEmpty(def.Action))
                    {
                        await ExecuteMenuAction(def.Action);
                    }
                    else if (!string.IsNullOrEmpty(def.MpvCommand))
                    {
                        await _mediaPlayer.RunCommandAsync(def.MpvCommand);
                    }
                }
            }
            catch (Exception ex)
            {
                OnException(ex);
            }
        }

    private async Task ExecuteMenuAction(string action)
    {
        if (MenuActions.TryGetValue(action, out var handler))
        {
            await handler(this);
            return;
        }
        OnException(new InvalidOperationException($"Unknown menu action: {action}"));
    }

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
                MinWidth = 480,
            };
            var list = new ListView
            {
                MaxHeight = 420,
                SelectionMode = ListViewSelectionMode.None,
            };
            var empty = new TextBlock
            {
                Text = AppContext.AppLang.ShortcutSearchEmpty,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gray),
                Visibility = Visibility.Collapsed
            };
            var panel = new StackPanel { Spacing = 8, MinWidth = 480 };
            panel.Children.Add(search);
            panel.Children.Add(list);
            panel.Children.Add(empty);

            var bindings = LoadBindings();
            void Refresh()
            {
                var query = (search.Text ?? string.Empty).Trim();
                var rows = bindings
                    .Where(b => string.IsNullOrWhiteSpace(query)
                                || b.Key.Contains(query, StringComparison.OrdinalIgnoreCase)
                                || b.Command.Contains(query, StringComparison.OrdinalIgnoreCase)
                                || b.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Select(BuildShortcutRow)
                    .ToList();
                list.ItemsSource = rows;
                empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            search.TextChanged += (_, _) => Refresh();
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

        /// <summary>
        /// One shortcut list row: friendly name on the left, the key rendered
        /// as rounded pills on the right (Win11 settings look). The raw mpv
        /// command stays available as the row tooltip for power users.
        /// </summary>
        private static FrameworkElement BuildShortcutRow((string Key, string Description, string Command) binding)
        {
            var name = new TextBlock
            {
                Text = string.IsNullOrEmpty(binding.Description) ? binding.Command : binding.Description,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };

            var pills = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var parts = binding.Key.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    pills.Children.Add(new TextBlock
                    {
                        Text = "+",
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    });
                }
                pills.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 3, 8, 3),
                    Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                    BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(1),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = mpv_winui.Modules.Settings.Controls.ShortcutKeyLocalizer.Localize(parts[i]),
                        FontSize = 12,
                    },
                });
            }

            var row = new Grid { ColumnSpacing = 16 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(name);
            Grid.SetColumn(pills, 1);
            row.Children.Add(pills);
            row.MinHeight = 36;
            ToolTipService.SetToolTip(row, binding.Command);
            return row;
        }

        /// <summary>
        /// Parses input.conf bindings: key, command and the trailing Chinese
        /// description comment (the bundled conf documents every binding as
        /// "#鼠标左键 暂停/播放"). Menu-directive lines ("#menu: … #@state=…")
        /// contribute their label, dynamic-menu placeholders ("_ ignore") are
        /// dropped, and duplicate key+command pairs collapse.
        /// </summary>
        private static List<(string Key, string Description, string Command)> LoadBindings()
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

            var result = new List<(string Key, string Description, string Command)>();
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                var description = string.Empty;
                var main = line;
                var hash = line.IndexOf('#');
                if (hash >= 0)
                {
                    main = line[..hash].TrimEnd();
                    description = line[(hash + 1)..].Trim();
                    if (description.StartsWith("menu:", StringComparison.OrdinalIgnoreCase))
                    {
                        description = description[5..].Trim();
                    }
                    var meta = description.IndexOf("#@", StringComparison.Ordinal);
                    if (meta >= 0)
                    {
                        description = description[..meta].Trim();
                    }
                    if (description.StartsWith('@'))
                    {
                        description = string.Empty;
                    }
                }

                var separator = main.IndexOfAny([' ', '\t']);
                if (separator <= 0)
                {
                    continue;
                }

                var key = main[..separator].Trim();
                var command = main[separator..].Trim();
                if (key.Length == 0 || command.Length == 0 || key == "_" || command == "ignore")
                {
                    continue;
                }
                result.Add((key, description, command));
            }

            return result
                .GroupBy(b => (b.Key, b.Command))
                .Select(g => g.First())
                .ToList();
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
                Content = AppContext.AppLang.HelpMpvDocs,
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
