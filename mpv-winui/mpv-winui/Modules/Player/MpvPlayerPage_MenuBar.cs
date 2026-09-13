using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using mpv_winui.Modules.AppModel;
using mpv_winui.Modules.Common.View;
using mpv_winui.Modules.FileSystem;
using mpv_winui.Modules.Menu.MenuBar;
using mpv_winui.Modules.Menu.MenuEditor;
using mpv_winui.Modules.Player.Menu;
using mpv_winui.Modules.Settings.Controls;
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
        private const string MpvMenuConfFileName = "mpv\\menu.conf";

        /// <summary>
        /// Menu action registry: action id -> handler. Extensible - any module can
        /// call <see cref="RegisterMenuAction"/> to add a menu action without
        /// touching a switch, and <see cref="KnownMenuActions"/> (used by
        /// <see cref="MenuBarBuilder"/> and the definitions check) derives from it.
        ///
        /// The config-driven definitions in Menus/menus.json reference actions by
        /// id, so an id that is not registered is skipped at build time rather
        /// than throwing when clicked.
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
            RegisterMenuAction("open-dvda", p => p.OpenDvdaAsync());
            RegisterMenuAction("open-cdda", p => p.OpenCddaAsync());
            RegisterMenuAction("open-cd-img", p => p.OpenCdImgAsync());
            RegisterMenuAction("open-dvd-img", p => p.OpenDvdImgAsync());
            RegisterMenuAction("open-dvda-img", p => p.OpenDvdaImgAsync());
            RegisterMenuAction("open-bd-img", p => p.OpenBdImgAsync());
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
            RegisterMenuAction("ontop", p =>
            {
                p.ToggleAlwaysOnTop();
                return Task.CompletedTask;
            });
            RegisterMenuAction("options", p =>
            {
                p.ShowSettingsWindow();
                return Task.CompletedTask;
            });
            RegisterMenuAction("conf-edit", p =>
            {
                p.ShowMpvConfigWindow();
                return Task.CompletedTask;
            });
            RegisterMenuAction("media-info", p =>
            {
                p.ShowMediaInfoWindow();
                return Task.CompletedTask;
            });
            RegisterMenuAction("edit-menu-menubar", p =>
            {
                p.ShowMenuEditorWindow(MenuBarService.Instance.FilePath, MenuType.Menubar);
                return Task.CompletedTask;
            });
            RegisterMenuAction("edit-menu-mpv", p =>
            {
                p.ShowMenuEditorWindow(AppData.Current.ResolveLocalData(MpvMenuConfFileName), MenuType.ContextMenu);
                return Task.CompletedTask;
            });
            RegisterMenuAction("open-mpv-conf", p => p.OpenConfigFileAsync("mpv.conf", true));
            RegisterMenuAction("open-mpvw-conf", p => p.OpenConfigFileAsync("mpvw.conf", true));
            RegisterMenuAction("open-input-conf", p => p.OpenConfigFileAsync("input.conf", true));
            RegisterMenuAction("open-menu-conf", p => p.OpenConfigFileAsync("menu.conf", true));
            RegisterMenuAction("open-mpv-log", p => p.OpenConfigFileAsync("mpv.log", true));
            RegisterMenuAction("open-mpwv-menu-conf", p => p.OpenConfigFileAsync("mpvw-menu.conf", false));
            RegisterMenuAction("link-mpv-wiki", p =>
                Launcher.LaunchUriAsync(new Uri("https://github.com/mpv-player/mpv/wiki")).AsTask());
            RegisterMenuAction("link-mpv-manual-stable", p =>
                Launcher.LaunchUriAsync(new Uri("https://mpv.io/manual/stable/")).AsTask());
            RegisterMenuAction("link-mpv-manual", p =>
                Launcher.LaunchUriAsync(new Uri("https://mpv.io/manual/master/")).AsTask());
            RegisterMenuAction("link-mpvw-wiki", p =>
                Launcher.LaunchUriAsync(new Uri("https://github.com/ikas-mc/mpv-winui-player/wiki")).AsTask());
            RegisterMenuAction("mpv-command", p => p.ShowMpvCommandDialogAsync());
            RegisterMenuAction("shortcut-search", p => p.ShowShortcutSearchDialogAsync());
        }

        /// <summary>
        /// Builds the menu bar from the config-driven definitions
        /// (Menus/menus.json, with a user override in the mpv config directory
        /// taking precedence). Labels resolve through AppLang at build time, so
        /// a rebuild after a language change re-localizes the whole bar.
        /// </summary>
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

        // ------------------------------------------------------- built-in fallback

        /// <summary>
        /// Wires the built-in (XAML-declared) menu bar items. Used when no
        /// config-driven definition is available, so the menu bar is never empty.
        /// The definitions path is the normal one; this is the safety net.
        /// </summary>
        private void SetupCustomMenuBarItems()
        {
            if (MenuDefinitionSource.TryLoad() is { Count: > 0 })
            {
                // Config-driven definitions own the bar; nothing to patch here.
                return;
            }

            // No definitions: the XAML items stay as authored. Their Click
            // handlers already route through MenuFlyoutItem_Click, which no-ops
            // for tags that are not MenuDefinition instances - so remap the
            // built-in tags onto the action registry to keep them alive.
            AttachBuiltInMenuActions();
        }

        /// <summary>
        /// Rewrites the XAML-declared menu items so their Tag carries a
        /// <see cref="MenuDefinition"/> (action id) instead of the bare string.
        /// Only used on the fallback path, where the bar is authored in XAML.
        /// </summary>
        private void AttachBuiltInMenuActions()
        {
            if (MainMenuBar is null)
            {
                return;
            }

            foreach (var item in EnumerateMenuItems(MainMenuBar.Items))
            {
                var def = BuildDefinitionFor(item);
                if (def is not null)
                {
                    item.Tag = def;
                }
            }
        }

        private static IEnumerable<MenuFlyoutItem> EnumerateMenuItems(IList<MenuBarItem> barItems)
        {
            foreach (var barItem in barItems)
            {
                foreach (var leaf in EnumerateLeaves(barItem.Items))
                {
                    yield return leaf;
                }
            }
        }

        private static IEnumerable<MenuFlyoutItem> EnumerateLeaves(IList<MenuFlyoutItemBase> items)
        {
            foreach (var entry in items)
            {
                switch (entry)
                {
                    case MenuFlyoutItem leaf:
                        yield return leaf;
                        break;
                    case MenuFlyoutSubItem sub:
                        foreach (var nested in EnumerateLeaves(sub.Items))
                        {
                            yield return nested;
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Maps a XAML-authored item onto a definition so the shared click path
        /// can dispatch it. Text is preserved; the action id comes from the Tag.
        /// </summary>
        private static MenuDefinition? BuildDefinitionFor(MenuFlyoutItem item)
        {
            if (item.Tag is MenuDefinition existing)
            {
                return existing;
            }

            var id = item.Tag as string;
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            return new MenuDefinition
            {
                Id = id,
                Action = id,
            };
        }

        // ------------------------------------------------------------ dialogs

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
                        Text = ShortcutKeyLocalizer.Localize(parts[i]),
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
        /// Parses input.conf bindings: key, command and the trailing description
        /// comment (the bundled conf documents every binding as
        /// "#鼠标左键 暂停/播放"). Menu-directive lines ("#menu: ... #@state=...")
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

        private void ShowMpvConfigWindow()
        {
            if (App.Window is MainWindow window)
            {
                window.OpenMpvConfigWindow();
            }
        }

        private void ShowMediaInfoWindow()
        {
            if (App.Window is MainWindow window)
            {
                window.OpenMediaInfoWindow(_mediaPlayer?.GetCurrentPath());
            }
        }

        private void ShowMenuEditorWindow(string filePath, MenuType type)
        {
            if (App.Window is MainWindow window)
            {
                window.OpenMenuEditorWindow(filePath, type);
            }
        }

        private async Task OpenConfigFileAsync(string fileName, bool inMpvFolder)
        {
            var folder = inMpvFolder
                ? await AppData.Current.OpenOrCreateLocalDataFolderAsync(MpvConfigFolderName)
                : await AppData.Current.OpenLocalDataFolderAsync();
            var file = await folder.CreateFileAsync(fileName, Windows.Storage.CreationCollisionOption.OpenIfExists);
            await Launcher.LaunchFileAsync(file);
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
