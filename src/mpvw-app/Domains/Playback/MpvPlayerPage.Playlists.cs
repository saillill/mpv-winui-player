using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using mpv_winui.Domains.Playlist;
using mpv_winui.Infrastructure.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace mpv_winui.Domains.Playback
{
    /// <summary>
    /// The named playlists section: one row per list, and the verbs that act on
    /// one.
    ///
    /// <para><b>Rows, not cards.</b> A playlist is a name and a count. Its file
    /// is a <c>.m3u</c>, which <c>MediaKinds</c> classifies as neither audio nor
    /// video, so a card grid would have no aspect to draw and would fall back to
    /// the empty tile glyph on every list - a wall of placeholder squares. The
    /// queue page and the watch-later dialog already show their contents as
    /// rows, which is the shape a list of lists wants.</para>
    ///
    /// <para><b>Rebuilt on every entry.</b> The rows are files: one can appear
    /// from another window, a sync client, or the user's own editor while the
    /// page is up. There is no collection to observe, so the section reads the
    /// folder when it is shown rather than caching what it read once.</para>
    /// </summary>
    public sealed partial class MpvPlayerPage
    {
        private readonly NamedPlaylistStore _playlistStore = new();

        /// <summary>Glyph for a row's own icon, shared by every row.</summary>
        private static readonly string PlaylistRowGlyph = "\uE8FD";

        /// <summary>
        /// Builds one row per named list, or hides the host when there is nothing
        /// to show.
        ///
        /// Called from ApplyLibraryFilter for the playlists section, and again
        /// after any action that changes the folder.
        /// </summary>
        private void BuildPlaylistRows()
        {
            PlaylistsHost.Children.Clear();

            var lists = _playlistStore.List();

            foreach (var list in lists)
            {
                PlaylistsHost.Children.Add(BuildPlaylistRow(list));
            }

            // The bar's add-to menu reads this. Refreshed here rather than on
            // every open: the menu is built when it opens, and this is when the
            // folder was last read.
            _namedPlaylists = lists
                .Select(list => (list.Name, list.Count))
                .ToList();

            UpdatePlaylistsVisibility();
        }

        /// <summary>
        /// One row: icon, name, entry count, play, and an overflow menu.
        ///
        /// The row itself is not clickable. A list has two things you can do to
        /// it and neither is "open", so a click target would have to guess; the
        /// reference puts the verbs on the row.
        /// </summary>
        private UIElement BuildPlaylistRow(NamedPlaylist list)
        {
            var lang = AppContext.AppLang;

            var row = new Grid
            {
                Padding = new Thickness(12, 8, 8, 8),
                ColumnSpacing = 12,
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                CornerRadius = new CornerRadius(4),
            };

            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new FontIcon
            {
                FontSize = 16,
                Glyph = PlaylistRowGlyph,
                VerticalAlignment = VerticalAlignment.Center,
            };

            Grid.SetColumn(icon, 0);
            row.Children.Add(icon);

            var label = new TextBlock
            {
                Text = list.Name,
                Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };

            Grid.SetColumn(label, 1);
            row.Children.Add(label);

            // "12 items" - the count is the one fact a list has before you open
            // it, and it is what tells an empty list from a typo.
            var count = new TextBlock
            {
                Text = string.Format(lang.PlaylistItemCount, list.Count),
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                VerticalAlignment = VerticalAlignment.Center,
            };

            Grid.SetColumn(count, 2);
            row.Children.Add(count);

            var verbs = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var play = new Button
            {
                Padding = (Thickness)Application.Current.Resources["MpvActionButtonPaddingTight"],
                Content = new FontIcon { FontSize = 12, Glyph = "\uE768" },
            };

            AutomationProperties.SetName(play, lang.PlaylistPlay);
            ToolTipService.SetToolTip(play, lang.PlaylistPlay);
            play.Click += (_, _) => PlayNamedPlaylist(list.Name);

            verbs.Children.Add(play);

            var more = new DropDownButton
            {
                Padding = (Thickness)Application.Current.Resources["MpvActionButtonPaddingTight"],
                Content = new FontIcon { FontSize = 12, Glyph = "\uE712" },
            };

            var menu = new MenuFlyout();

            var playItem = new MenuFlyoutItem { Text = lang.PlaylistPlay };
            playItem.Click += (_, _) => PlayNamedPlaylist(list.Name);

            var renameItem = new MenuFlyoutItem { Text = lang.PlaylistRename };
            renameItem.Click += (_, _) => RenameNamedPlaylistAsync(list.Name).FireAndForget(OnException);

            var deleteItem = new MenuFlyoutItem { Text = lang.PlaylistDelete };
            deleteItem.Click += (_, _) => DeleteNamedPlaylistAsync(list.Name).FireAndForget(OnException);

            menu.Items.Add(playItem);
            menu.Items.Add(renameItem);
            menu.Items.Add(deleteItem);

            more.Flyout = menu;

            AutomationProperties.SetName(more, lang.PlaylistMore);
            ToolTipService.SetToolTip(more, lang.PlaylistMore);

            verbs.Children.Add(more);

            Grid.SetColumn(verbs, 3);
            row.Children.Add(verbs);

            return row;
        }

        /// <summary>
        /// Shows the rows host for the playlists section and hides the two card
        /// hosts.
        ///
        /// Its own method because three callers need the same three-way answer:
        /// the section switch, a rename, and a delete. Written once so the three
        /// hosts can never be visible together.
        /// </summary>
        private void UpdatePlaylistsVisibility()
        {
            var isPlaylists = _librarySection == LibrarySectionPlaylists;

            PlaylistsHost.Visibility = isPlaylists && PlaylistsHost.Children.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Hands the list's file to mpv.
        ///
        /// The same <c>loadlist</c> the import path uses
        /// (MpvPlayerPage_Playlist.ImportPlaylistAsync): mpv resolves relative
        /// entries against the m3u's own directory and replaces the current
        /// playlist, which is what "play this list" means. Reading the entries
        /// here and issuing one <c>loadfile</c> each would be a second
        /// implementation of a loader mpv already has.
        ///
        /// The path comes from the store. Rebuilding the folder here is the
        /// mistake AppData's own comments record - six call sites each one
        /// directory short.
        /// </summary>
        private void PlayNamedPlaylist(string name)
        {
            if (_playlistStore.Read(name).Count == 0)
            {
                return;
            }

            if (_playlistStore.FilePath(name) is not { } file)
            {
                return;
            }

            _mediaPlayer.Command(["osd-auto", "loadlist", file]);

            RefreshPlaylistAsync();
        }

        /// <summary>
        /// Renames a list, refusing a name that would land on another list.
        ///
        /// The store already refuses and only logs, so the refusal is checked
        /// here first: a menu item that silently does nothing reads as a bug, and
        /// the user has no way to tell "that name is taken" from "the app is
        /// broken".
        /// </summary>
        private async Task RenameNamedPlaylistAsync(string from)
        {
            var typed = await PromptForPlaylistNameAsync(
                AppContext.AppLang.PlaylistRename,
                from,
                candidate =>
                {
                    if (string.Equals(candidate, from, StringComparison.Ordinal))
                    {
                        return null;
                    }

                    var taken = _playlistStore.List()
                        .Any(list => string.Equals(list.Name, candidate, StringComparison.CurrentCultureIgnoreCase));

                    return taken ? AppContext.AppLang.PlaylistNameTaken : null;
                });

            if (typed is null || string.Equals(typed, from, StringComparison.Ordinal))
            {
                return;
            }

            _playlistStore.Rename(from, typed);

            BuildPlaylistRows();
        }

        /// <summary>
        /// Deletes a list after asking.
        ///
        /// Confirmed because there is no undo: the file is gone, and the entries
        /// with it. Renaming needs no confirmation - it can be renamed back.
        /// </summary>
        private async Task DeleteNamedPlaylistAsync(string name)
        {
            var dialog = new ContentDialog
            {
                Title = AppContext.AppLang.PlaylistDelete,
                Content = string.Format(AppContext.AppLang.PlaylistDeleteConfirm, name),
                PrimaryButtonText = AppContext.AppLang.PlaylistDelete,
                CloseButtonText = AppContext.AppLang.Cancel,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            _playlistStore.Delete(name);

            BuildPlaylistRows();
        }
    }
}
