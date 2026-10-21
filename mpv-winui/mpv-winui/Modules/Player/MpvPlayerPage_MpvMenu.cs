using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using mpv_winrt;
using mpv_winui.Modules.Common.Utils;
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
        /// The app's own menu entries, above mpv's script-supplied ones.
        ///
        /// Organised the way the settings window is: by what a person came to do,
        /// not by which subsystem answers. The previous list mixed three
        /// different jobs under one "File" submenu - opening a file, opening a
        /// URL, and pulling something out of history - and then put the playlist
        /// toggle both there and under "Window", so the same command appeared
        /// twice with two different captions.
        ///
        /// Now: Open (get media in), Recent (resume something), Track (choose
        /// what plays), View (change how it looks), and Quit at the root.
        /// </summary>
        private void AddOpenHeaderItems(IList<MenuFlyoutItemBase> target)
        {
            var lang = AppContext.AppLang;

            var openSub = new MenuFlyoutSubItem { Text = lang.MenuSectionOpen };
            target.Add(openSub);

            AddItem(openSub.Items, lang.MenuOpenFile, "open");
            AddItem(openSub.Items, lang.MenuOpenFolder, "open-folder");
            AddItem(openSub.Items, lang.MenuOpenUrl, "open-url");
            AddItem(openSub.Items, lang.MenuOpenClipboard, "open-clipboard");

            openSub.Items.Add(new MenuFlyoutSeparator());

            // Discs are an "open" action, not a separate topic; they were missing
            // from this menu entirely while the tags existed in Item_Click.
            var discSub = new MenuFlyoutSubItem { Text = lang.MenuOpenDisc };
            AddItem(discSub.Items, lang.MenuOpenDvd, "open-dvd");
            AddItem(discSub.Items, lang.MenuOpenBluRay, "open-bd");
            openSub.Items.Add(discSub);

            var recentSub = new MenuFlyoutSubItem { Text = lang.MenuRecent };
            AddItem(recentSub.Items, lang.MenuOpenWatchHistory, "open-watch-history");
            AddItem(recentSub.Items, lang.MenuOpenWatchLater, "open-watch-later");
            target.Add(recentSub);

            var trackSub = new MenuFlyoutSubItem { Text = lang.MenuSectionTrack };
            AddItem(trackSub.Items, lang.MenuLoadSubtitle, "load-subtitle");
            target.Add(trackSub);
        }

        private void AddItem(IList<MenuFlyoutItemBase> target, string text, string tag)
        {
            var item = new MenuFlyoutItem { Text = text, Tag = tag };
            item.Click += Item_Click;
            target.Add(item);
        }

        private void AddCustomFooterItems(IList<MenuFlyoutItemBase> target)
        {
            var lang = AppContext.AppLang;

            var viewSub = new MenuFlyoutSubItem
            {
                Text = lang.MenuSectionView,
                MinWidth = 200,
            };
            target.Add(viewSub);

            AddItem(viewSub.Items, lang.MenuTogglePlaylist, "playlist");
            AddItem(viewSub.Items, lang.MenuToggleFullScreen, "fullscreen");
            AddItem(viewSub.Items, lang.MenuToggleFullWindow, "fullwindow");
            AddItem(viewSub.Items, lang.MenuAlwaysOnTop, "ontop");

            target.Add(new MenuFlyoutSeparator());
            AddItem(target, lang.MenuQuit, "quit");
        }

        private void AddMenuDataItems(IList<MenuFlyoutItemBase> target, IReadOnlyList<MpvMenuItem> items)
        {
            bool isSeparatorPre = false;
            foreach (var entry in items)
            {
                if (!CheckMpvMenu(entry))
                {
                    continue;
                }

                if (entry.IsHidden)
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
                    var subItem = new MenuFlyoutSubItem { Text = entry.Title?.Replace("&", "") ?? string.Empty, IsEnabled = !entry.IsDisabled };
                    AddMenuDataItems(subItem.Items, entry.Items);
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
                        item = new ToggleMenuFlyoutItem { Text = entry.Title?.Replace("&", "") ?? string.Empty, IsEnabled = !entry.IsDisabled, IsChecked = true, };
                    }
                    else
                    {
                        item = new MenuFlyoutItem { Text = entry.Title?.Replace("&", "") ?? string.Empty, IsEnabled = !entry.IsDisabled, };
                    }

                    item.Click += (_, _) => MpvMenuItemClick(cmd);
                    target.Add(item);
                }
            }
        }

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
                        case "ontop":
                        {
                            ToggleAlwaysOnTop();
                            break;
                        }
                        case "playlist":
                        {
                            ShowPlayQueue();
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

        private bool CheckMpvMenu(MpvMenuItem mpvMenuItem)
        {
            //TODO remove&  check cmd ??
            if (mpvMenuItem.Title == "Ope&n" || mpvMenuItem.Title == "&Stop" || mpvMenuItem.Title == "&Quit" || mpvMenuItem.Title == "Quit an&d save position")
            {
                return false;
            }

            return true;
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
            var menuItems = _mediaPlayer.GetMenu();
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
    }
}