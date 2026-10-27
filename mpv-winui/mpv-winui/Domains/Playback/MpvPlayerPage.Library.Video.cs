using mpv_winui.Infrastructure.Common.Media;

using mpv_winui.Domains.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.Common.View;
using mpv_winui.Platform.FileSystem;
using mpv_winui.Infrastructure.Motion;
using mpv_winui.Domains.Playlist;
using System;
using System.ComponentModel;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;

// Part of MpvPlayerPage. The partial keyword means the compiler sees one class;
// this file exists so a reader looking for one responsibility opens one file
// instead of scrolling through three thousand lines.
namespace mpv_winui.Domains.Playback
{
    /// <summary>
    /// the video library: the All videos / Video folders tabs, the sort, and folder grouping.
    /// </summary>
    public sealed partial class MpvPlayerPage
    {
        /// <summary>
        /// Which video-library tab is up: false is "All videos", true is
        /// "Video folders". Session state, deliberately not a stored setting.
        /// </summary>
        private bool _videoFolderView;

        /// <summary>
        /// The video library's sort. Session-only on purpose: ordering the grid
        /// is a way of looking at the list during this run, not a preference, so
        /// it is neither persisted nor added to the settings schema.
        /// </summary>
        private VideoSortMode _videoSort = VideoSortMode.NameAsc;

        /// <summary>
        /// The video library's tab labels, toolbar labels and sort menu. Like
        /// the queue's strings, set from AppLang in code so a language change
        /// reaches them through the same path the header uses. The two icons are
        /// parsed fresh here, which is the one-attachment rule FluentIcons
        /// documents.
        /// </summary>
        private void ApplyVideoStrings()
        {
            var lang = AppContext.AppLang;

            VideoTabAll.Content = lang.LibraryTabAllVideos;
            VideoTabFolders.Content = lang.LibraryTabVideoFolders;
            // NOT the heading action's label: that one is section-dependent and is owned by
            // ApplySectionHeader, which runs BEFORE this method in ApplyLibraryStrings.
            // Setting it unconditionally here silently undid the Home branch -- measured
            // after the first attempt: all three sections showed "添加文件夹" on the heading
            // row, including Home, whose XAML and code both said "Open file".
            VideoPlayAllText.Text = lang.LibraryPlayAll;
            // Same reason as the nav pane's settings button: the label is built in code, so
            // the accessible name is set beside it. The heading action's own name is NOT set
            // here for the same reason as its text above -- ApplySectionHeader owns it.
            AutomationProperties.SetName(VideoPlayAllButton, lang.LibraryPlayAll);

            // The music section's three tabs and its Add folder command. The
            // words come from AppLang like every other label.
            MusicTabSongs.Content = lang.MusicTabSongs;

            MusicTabAlbums.Content = lang.MusicTabAlbums;

            MusicTabArtists.Content = lang.MusicTabArtists;
            SortByNameAsc.Text = lang.LibrarySortNameAsc;
            SortByNameDesc.Text = lang.LibrarySortNameDesc;
            SortByNewest.Text = lang.LibrarySortNewest;
            SortByOldest.Text = lang.LibrarySortOldest;

            VideoAddFolderIcon.Data = FluentIcons.Geometry(FluentIcons.FolderAdd);
            VideoPlayAllIcon.Data = FluentIcons.Geometry(FluentIcons.PlayCircle);

            UpdateVideoSortChrome();
        }

        /// <summary>
        /// Points the sort button's caption and the menu's radio mark at the
        /// active order. Called from ApplyVideoStrings and from every sort
        /// change, so the two can never disagree with <see cref="_videoSort"/>.
        /// </summary>
        private void UpdateVideoSortChrome()
        {
            var lang = AppContext.AppLang;
            var current = _videoSort switch
            {
                VideoSortMode.NameDesc => lang.LibrarySortNameDesc,
                VideoSortMode.Newest => lang.LibrarySortNewest,
                VideoSortMode.Oldest => lang.LibrarySortOldest,
                _ => lang.LibrarySortNameAsc,
            };

            VideoSortButton.Content = $"{lang.LibrarySortBy}: {current}";

            SortByNameAsc.IsChecked = _videoSort == VideoSortMode.NameAsc;
            SortByNameDesc.IsChecked = _videoSort == VideoSortMode.NameDesc;
            SortByNewest.IsChecked = _videoSort == VideoSortMode.Newest;
            SortByOldest.IsChecked = _videoSort == VideoSortMode.Oldest;
        }

        private void VideoTabAll_Checked(object sender, RoutedEventArgs e) => SetVideoFolderView(false);

        private void VideoTabFolders_Checked(object sender, RoutedEventArgs e) => SetVideoFolderView(true);

        /// <summary>
        /// Switches the video library between its flat and grouped presentations
        /// of the same list. Guarded against re-entry because the tab is also
        /// set programmatically from the sort chrome, and the very first
        /// assignment happens while the page is still loading.
        /// </summary>
        private void SetVideoFolderView(bool folders)
        {
            if (_videoFolderView == folders)
            {
                return;
            }

            _videoFolderView = folders;

            if (_libraryLoaded)
            {
                ApplyLibraryFilter();
            }
        }

        /// <summary>
        /// A sort menu choice. The order is session state, so
        /// <see cref="_videoSort"/> is the only place it is written; the button
        /// caption and the list are then refreshed from it.
        /// </summary>
        private void VideoSort_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioMenuFlyoutItem { Tag: string tag })
            {
                return;
            }

            _videoSort = tag switch
            {
                "name-desc" => VideoSortMode.NameDesc,
                "newest" => VideoSortMode.Newest,
                "oldest" => VideoSortMode.Oldest,
                _ => VideoSortMode.NameAsc,
            };

            UpdateVideoSortChrome();

            if (_libraryLoaded)
            {
                ApplyLibraryFilter();
            }
        }

        /// <summary>
        /// "Play all": enqueue exactly the list the user is looking at - the
        /// same entries the grid shows, in the same order - and start it. Reuses
        /// the page's existing batch-open path.
        /// </summary>
        private void VideoPlayAll_Click(object sender, RoutedEventArgs e)
        {
            // Missing files are left out of the play list, though their cards
            // stay on screen: mpv failing on the first loadfile would surface as
            // "the button did nothing". The cards stay so the list keeps
            // matching the player's own history.
            var paths = _visibleEntries
                .Where(entry => !entry.IsMissing)
                .Select(entry => entry.Path)
                .ToList();
            if (paths.Count == 0)
            {
                return;
            }

            PlayFiles(paths, OpenMode.Replace).FireAndForget(OnException);
        }

        private void VideoAddFolder_Click(object sender, RoutedEventArgs e)
        {
            // The same command the empty state's "Add folder" button uses.
            //
            // On Home the heading slot carries "Open file" instead of "Add folder" (see
            // ApplySectionHeader for the placement reasoning), so it has to run the file
            // picker rather than the folder binder. Doing the wrong one here would be
            // invisible in a screenshot and obvious to a user: a folder picker under a
            // label that says "open a file".
            if (_librarySection == LibrarySectionHome)
            {
                LibraryOpenFileButton_Click(sender, e);
                return;
            }
            _ = AddLibraryFoldersAsync(_librarySection == LibrarySectionMusic);
        }

        /// <summary>
        /// Binds folders to a library and rescans it.
        ///
        /// This is what "Add folder" was always meant to do. It used to call
        /// OpenFolderAsync, which opened the folder as a playlist and remembered
        /// nothing - so the button labelled "add a folder to the library" only
        /// ever played one, and the library stayed a list of things already
        /// played. Binding is the difference between a media library and a
        /// history.
        ///
        /// Multi-select, because a library is rarely one folder. The folders are
        /// persisted before the rescan so a scan that throws still leaves the
        /// binding recorded.
        /// </summary>
        private async Task AddLibraryFoldersAsync(bool music)
        {
            try
            {
                var picked = await FilePickerHelper.PickFoldersAsync();

                if (picked is null || picked.Count == 0)
                {
                    return;
                }

                foreach (var folder in picked)
                {
                    AppContext.AppSetting.AddLibraryFolder(music, folder);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "add library folders failed, music={}", music);

                return;
            }

            // Rescan, then re-apply the header: the toolbar's visibility depends
            // on whether a folder is now bound, so it has to be recomputed rather
            // than waiting for the next section switch.
            await LoadLibraryAsync();

            ApplySectionHeader();
        }

        /// <summary>
        /// Orders the video list for the active sort.
        ///
        /// Name sorts use the current-culture comparer because the caption is
        /// what the user reads and that is how Explorer orders the same names.
        /// "Newest / oldest" sorts on each entry's watch-history timestamp - the
        /// file itself is never touched, so a file with no history time sorts as
        /// the oldest rather than pulling a disk timestamp. <c>OrderBy</c> is a
        /// stable sort, so ties keep the history's own (newest-first) order
        /// instead of shuffling on every switch.
        /// </summary>
        private static List<LibraryEntry> SortVideoEntries(List<LibraryEntry> entries, VideoSortMode mode)
        {
            IEnumerable<LibraryEntry> ordered = mode switch
            {
                VideoSortMode.NameDesc => entries.OrderByDescending(e => e.Title, StringComparer.CurrentCultureIgnoreCase),
                VideoSortMode.Newest => entries.OrderByDescending(e => e.Time ?? DateTimeOffset.MinValue),
                VideoSortMode.Oldest => entries.OrderBy(e => e.Time ?? DateTimeOffset.MinValue),
                _ => entries.OrderBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase),
            };

            return ordered.ToList();
        }

        /// <summary>
        /// Builds the "Video folders" tab: the visible entries grouped by the
        /// directory they were played from, one header followed by one card grid
        /// per folder.
        ///
        /// A folder is here because something in it was played, never because
        /// the disk was scanned - the same scope its empty state describes.
        /// Rebuilt wholesale rather than diffed, because the groups change shape
        /// (folders appear and vanish, counts move); grouping gives up
        /// virtualisation, which the video counts make an acceptable trade.
        /// </summary>
        private void BuildVideoFolderGroups(List<LibraryEntry> entries)
        {
            VideoFolderGroups.Children.Clear();

            var cardTemplate = (DataTemplate)Resources["LibraryCardTemplate"];
            var headerTemplate = (DataTemplate)Resources["VideoFolderHeaderTemplate"];

            // Registered here as well as in the music grouped view: the index
            // rail jumps by looking the letter up in this map, so a header that
            // was added to the panel but not to the map is a rail button that
            // scrolls nowhere.
            _groupHeaders.Clear();

            var groups = GroupVideoFolders(entries);

            foreach (var group in groups)
            {
                var header = new ContentPresenter
                {
                    Content = group,
                    ContentTemplate = headerTemplate,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };

                VideoFolderGroups.Children.Add(header);

                if (!string.IsNullOrEmpty(group.Letter))
                {
                    // First group wins, matching the music view: several folders
                    // can share a letter and the rail lands on the first.
                    _groupHeaders.TryAdd(group.Letter, header);
                }

                var repeater = new ItemsRepeater
                {
                    ItemTemplate = cardTemplate,
                    ItemsSource = group.Items,
                };
                repeater.Layout = CardLayout();

                // The same arrival animation the flat grid uses, so the two tabs
                // read as one surface.
                repeater.ElementPrepared += LibraryGrid_ElementPrepared;
                VideoFolderGroups.Children.Add(repeater);
            }

            BuildLibraryIndexRail(groups);
        }

        private static UniformGridLayout CardLayout() => new()
        {
            MinItemWidth = 253,
            MinItemHeight = 200,
            MinColumnSpacing = 12,
            MinRowSpacing = 16,
        };

        private static List<VideoFolderGroup> GroupVideoFolders(List<LibraryEntry> entries)
        {
            var groups = new List<VideoFolderGroup>();

            var byFolder = entries
                .GroupBy(entry => FolderOf(entry.Path), StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => FolderLabel(group.Key), StringComparer.CurrentCultureIgnoreCase);

            foreach (var group in byFolder)
            {
                var name = FolderLabel(group.Key);

                // The letter is what the index rail jumps by. Without it the
                // rail had nothing to bind to for videos: the groups were real
                // and ordered, but every row's Letter was empty, so
                // BuildRail saw no letters in use and drew nothing.
                groups.Add(new VideoFolderGroup(
                    name,
                    group.ToList(),
                    group.Key,
                    Letter: LibraryIndexLetter.For(name, ch => PinyinInitial.For(ch.ToString()))));
            }

            return groups;
        }

        /// <summary>The directory a path lives in, or empty for a bare file name.</summary>
        private static string FolderOf(string path)
        {
            try
            {
                return System.IO.Path.GetDirectoryName(path) ?? string.Empty;
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
        }

        /// <summary>The folder's own name, which is what a group is labelled with.</summary>
        private static string FolderLabel(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                return string.Empty;
            }

            var trimmed = directory.TrimEnd(
                System.IO.Path.DirectorySeparatorChar,
                System.IO.Path.AltDirectorySeparatorChar);
            var leaf = System.IO.Path.GetFileName(trimmed);
            return string.IsNullOrEmpty(leaf) ? directory : leaf;
        }
    }
}
