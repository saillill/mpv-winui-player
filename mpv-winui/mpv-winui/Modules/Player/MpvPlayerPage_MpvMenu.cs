using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using mpv_winrt;
using mpv_winui.Modules.Common.Utils;
using mpv_winui;
using System;
using System.Collections.Generic;

namespace mpv_winui.Modules.Player
{
    public sealed partial class MpvPlayerPage
    {
        private MenuFlyout BuildMenuFlyoutFromData(IReadOnlyList<MpvMenuItem>? items)
        {
            var flyout = new MenuFlyout();

            AddOpenHeaderItems(flyout.Items);

            AddCustomMenuItems(flyout.Items);

            if (items?.Count > 0)
            {
                AddMenuDataItems(flyout.Items, items);
            }
            else
            {
                flyout.Items.Add(new MenuFlyoutSeparator());
            }

            AddCustomFooterItems(flyout.Items);

            return flyout;
        }

        /// <summary>
        /// Inserts the user's custom commands (custom_menu.json in the mpv
        /// config directory) between the fixed header and the mpv menu-data.
        /// </summary>
        private void AddCustomMenuItems(IList<MenuFlyoutItemBase> target)
        {
            var custom = Menu.CustomMenuSource.TryLoad();
            if (custom is not { Count: > 0 })
            {
                return;
            }
            target.Add(new MenuFlyoutSeparator());
            foreach (var entry in custom)
            {
                if (entry.Separator)
                {
                    target.Add(new MenuFlyoutSeparator());
                    continue;
                }
                if (string.IsNullOrEmpty(entry.Label) || string.IsNullOrEmpty(entry.MpvCommand))
                {
                    continue;
                }
                var item = new MenuFlyoutItem { Text = entry.Label };
                var cmd = entry.MpvCommand;
                item.Click += (_, _) => MpvMenuItemClick(cmd!);
                target.Add(item);
            }
        }

        private void AddOpenHeaderItems(IList<MenuFlyoutItemBase> target)
        {
            var openSub = new MenuFlyoutSubItem { Text = AppContext.AppLang.File };
            target.Add(openSub);

            var item = new MenuFlyoutItem { Text = AppContext.AppLang.OpenFile, Tag = "open" };
            item.Click += Item_Click;
            openSub.Items.Add(item);

            item = new MenuFlyoutItem { Text = AppContext.AppLang.OpenFolder, Tag = "open-folder" };
            item.Click += Item_Click;
            openSub.Items.Add(item);

            item = new MenuFlyoutItem { Text = AppContext.AppLang.OpenUrl, Tag = "open-url" };
            item.Click += Item_Click;
            openSub.Items.Add(item);

            item = new MenuFlyoutItem { Text = AppContext.AppLang.OpenFromClipboard, Tag = "open-clipboard" };
            item.Click += Item_Click;
            openSub.Items.Add(item);

            openSub.Items.Add(new MenuFlyoutSeparator());

            item = new MenuFlyoutItem { Text = AppContext.AppLang.OpenWatchHistory, Tag = "open-watch-history" };
            item.Click += Item_Click;
            openSub.Items.Add(item);

            item = new MenuFlyoutItem { Text = AppContext.AppLang.OpenWatchLater, Tag = "open-watch-later" };
            item.Click += Item_Click;
            openSub.Items.Add(item);

            item = new MenuFlyoutItem { Text = AppContext.AppLang.Playlist, Tag = "playlist" };
            item.Click += Item_Click;
            target.Add(item);
        }

        private void AddCustomFooterItems(IList<MenuFlyoutItemBase> target)
        {
            var subItem = new MenuFlyoutSubItem
            {
                Text = AppContext.AppLang.Window,
                MinWidth = 200
            };
            target.Add(subItem);

            var item = new MenuFlyoutItem
            {
                Text = AppContext.AppLang.TogglePlaylist,
                Tag = "playlist"
            };
            item.Click += Item_Click;
            subItem.Items.Add(item);

            item = new MenuFlyoutItem
            {
                Text = AppContext.AppLang.ToggleFullScreen,
                Tag = "fullscreen"
            };
            item.Click += Item_Click;
            subItem.Items.Add(item);

            item = new MenuFlyoutItem
            {
                Text = AppContext.AppLang.ToggleFullWindow,
                Tag = "fullwindow"
            };
            item.Click += Item_Click;
            subItem.Items.Add(item);

            item = new MenuFlyoutItem
            {
                Text = AppContext.AppLang.Quit,
                Tag = "quit"
            };
            item.Click += Item_Click;
            target.Add(item);
        }

        // Debug/technical entries hidden from the right-click menu; these are
        // developer-facing tools that regular users never need.
        private static readonly HashSet<string> HiddenMenuTitles = new(StringComparer.Ordinal)
        {
            "按键名检测", "清除已记录的属性值", "打开select总菜单",
            "打开select分菜单-属性列表", "环境体检", "常驻显示统计信息",
            "时间码解析模式", "切换解码模式", "按键绑定列表",
        };

        private void AddMenuDataItems(IList<MenuFlyoutItemBase> target, IReadOnlyList<MpvMenuItem> items, string? inheritGlyph = null)
        {
            bool isSeparatorPre = false;
            foreach (var entry in items)
            {
                if (entry.IsHidden)
                {
                    continue;
                }

                var cleanTitle = DisplayTitle(entry.Title);
                if (HiddenMenuTitles.Contains(cleanTitle))
                {
                    continue;
                }

                if (entry.Type == "separator")
                {
                    if (!isSeparatorPre)
                    {
                        target.Add(new MenuFlyoutSeparator());
                    }
                    isSeparatorPre = true;
                    continue;
                }
                isSeparatorPre = false;

                if (entry.Type == "submenu" && entry.Items.Count > 0)
                {
                    var subItem = new MenuFlyoutSubItem { Text = DisplayTitle(entry.Title), IsEnabled = !entry.IsDisabled };
                    var gs = IconMap.For(entry.Title) ?? inheritGlyph;
                    if (gs is not null)
                        subItem.Icon = new FontIcon { Glyph = gs, FontFamily = new FontFamily(IconMap.Font) };
                    AddMenuDataItems(subItem.Items, entry.Items, gs);
                    if (subItem.Items.Count > 0)
                    {
                        target.Add(subItem);
                    }
                }
                else if (!string.IsNullOrEmpty(entry.Command))
                {
                    var cmd = entry.Command;
                    MenuFlyoutItem item;
                    if (entry.IsChecked)
                    {
                        item = new ToggleMenuFlyoutItem { Text = DisplayTitle(entry.Title), IsEnabled = !entry.IsDisabled, IsChecked = true, };
                    }
                    else
                    {
                        item = new MenuFlyoutItem { Text = DisplayTitle(entry.Title), IsEnabled = !entry.IsDisabled, };
                    }

                    var g = IconMap.For(entry.Title) ?? inheritGlyph;
                    if (g is not null)
                        item.Icon = new FontIcon { Glyph = g, FontFamily = new FontFamily(IconMap.Font) };
                    item.Click += (_, _) => MpvMenuItemClick(cmd);
                    target.Add(item);
                }
            }
        }

        /// <summary>mpv 菜单标题还原：dyn_menu escape_title 会把字面 & 写成 &amp;&amp;，WinUI 不解释 &，还原为单 &。</summary>
        private static string DisplayTitle(string? title) =>
            title is not null && title.Contains("&&", StringComparison.Ordinal)
                ? title.Replace("&&", "&")
                : title ?? string.Empty;

        private async void Item_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem { Tag: string tag })
            {
                try
                {
                    switch (tag)
                    {
                        case "open":
                        {
                            await OpenFileAsync();
                            break;
                        }
                        case "open-folder":
                        {
                            await OpenFolderAsync();
                            break;
                        }
                        case "open-url":
                        {
                            await OpenUrlAsync();
                            break;
                        }
                        case "open-clipboard":
                        {
                            await OpenClipboardAsync();
                            break;
                        }
                        case "open-dvd":
                        {
                            await OpenDvdAsync();
                            break;
                        }
                        case "open-bd":
                        {
                            await OpenBdAsync();
                            break;
                        }
                        case "load-subtitle":
                        {
                            await LoadSubtitleAsync();
                            break;
                        }
                        case "quit":
                        {
                            AppQuit();
                            break;
                        }
                        case "fullscreen":
                        {
                            PlayerControl.ToggleFullScreen();
                            break;
                        }
                        case "fullwindow":
                        {
                            PlayerControl.ToggleFullWindow();
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
                    }
                }
                catch (Exception ex)
                {
                    OnException(ex);
                }
            }
        }

        private void MpvMenuItemClick(string cmd)
        {
            if (_logger.IsDebugEnabled)
            {
                _logger.Debug("mpv menu item click, cmd={}", cmd);
            }

            _mediaPlayer.RunCommandAsync(cmd).FireAndForget(OnException);
        }

        private void PlayerView_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var menuItems = _mediaPlayer.MenuData();
            var flyout = BuildMenuFlyoutFromData(menuItems);
            if (args.TryGetPosition(PlayerView, out var point))
            {
                flyout.ShowAt(PlayerView, point);
            }
            else
            {
                flyout.ShowAt(PlayerView);
            }

            args.Handled = true;
        }

        /// <summary>
        /// 将固定菜单文本从 AppLang 应用到 XAML（unpackaged WinUI 3 不支持 x:Uid 语言切换，
        /// 也不支持 x:Bind 静态属性，故用代码后置赋值；AppLang 在 AppContext.Init 时已加载）。
        /// </summary>
        private void ApplyLocalizedStrings()
        {
            if (Resources["PlaylistContextMenu"] is MenuFlyout playlistMenu)
            {
                foreach (var item in playlistMenu.Items)
                {
                    if (item is MenuFlyoutItem mi)
                    {
                        mi.Text = mi.Tag switch
                        {
                            "play" => AppContext.AppLang.PlaylistPlay,
                            "move-top" => AppContext.AppLang.PlaylistMoveTop,
                            "move-up" => AppContext.AppLang.PlaylistMoveUp,
                            "move-down" => AppContext.AppLang.PlaylistMoveDown,
                            "move-bottom" => AppContext.AppLang.PlaylistMoveBottom,
                            "remove" => AppContext.AppLang.PlaylistRemove,
                            "copy-title" => AppContext.AppLang.PlaylistCopyTitle,
                            "copy-path" => AppContext.AppLang.PlaylistCopyPath,
                            "open-location" => AppContext.AppLang.PlaylistOpenLocation,
                            _ => mi.Text,
                        };
                    }
                }
            }

            ToolTipService.SetToolTip(TopBarOntopButton, AppContext.AppLang.SettingsAlwaysOnTop);
            ToolTipService.SetToolTip(TopBarScreenshotButton, AppContext.AppLang.FileScreenshot);
            ToolTipService.SetToolTip(TopBarPlaylistButton, AppContext.AppLang.TogglePlaylist);
            ToolTipService.SetToolTip(PlaylistOntopButton, AppContext.AppLang.SettingsAlwaysOnTop);
            ToolTipService.SetToolTip(PlaylistScreenshotButton, AppContext.AppLang.FileScreenshot);
            ToolTipService.SetToolTip(PlaylistCloseButton, AppContext.AppLang.TogglePlaylist);
            ToolTipService.SetToolTip(PlaylistRefreshButton, AppContext.AppLang.Refresh);
            PlaylistFilterBox.PlaceholderText = AppContext.AppLang.PlaylistFilterPlaceholder;
        }
    }
}
