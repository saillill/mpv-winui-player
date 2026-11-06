using mpv_winui.Infrastructure.Common.Media;
using mpv_winui.Domains.Playback.Cards;

using mpv_winui.Domains.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using mpv_winui.Infrastructure.Common.Utils;
using mpv_winui.Infrastructure.Common.View;
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
        /// The music library's sort. Deliberately a field of its own rather than a
        /// second reader of <see cref="_videoSort"/>: sharing one would make
        /// reordering the songs reorder the video grid, and a video library left on
        /// "Newest" would put that word on the Albums tab, where the order it names
        /// is greyed out.
        ///
        /// Session-only for the same reason as the video sort: a way of looking at
        /// the list during this run, not a preference.
        /// </summary>
        private VideoSortMode _musicSort = VideoSortMode.NameAsc;

        /// <summary>
        /// The music sort actually in force on the tab that is up.
        ///
        /// The Albums tab groups files by a tag, and a group carries no
        /// watch-history time, so both time orders collapse to name order there
        /// instead of sitting in the menu over a list they cannot move.
        /// </summary>
        private VideoSortMode EffectiveMusicSort =>
            !MusicSortHasTimeModes && _musicSort is VideoSortMode.Newest or VideoSortMode.Oldest
                ? VideoSortMode.NameAsc
                : _musicSort;

        /// <summary>
        /// Whether the tab that is up offers the two time orders. Only the flat
        /// Songs list of the music library does: Albums and Artists order tags, and
        /// a tag has no time. The section is named in the condition rather than left
        /// implied, so outside the music library the answer is false rather than
        /// accidentally true.
        /// </summary>
        private bool MusicSortHasTimeModes =>
            _librarySection == LibrarySectionMusic && _musicGrouping == MusicGrouping.Songs;

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

            // Stable automation ids for the three tabs, set beside their words.
            //
            // The accessible NAME is the tab's own label, which changes with the
            // language - so a query for 「专辑」 finds the tab in Chinese and nothing
            // in any of the other eight. An id does not move, which is what a test or
            // a future automation needs to say "switch to Albums" at all. Found the
            // hard way: the album-card tick box could not be exercised because the
            // tab could not be reached.
            AutomationProperties.SetAutomationId(MusicTabSongs, "MusicTabSongs");
            AutomationProperties.SetAutomationId(MusicTabAlbums, "MusicTabAlbums");
            AutomationProperties.SetAutomationId(MusicTabArtists, "MusicTabArtists");
            SortByNameAsc.Text = lang.LibrarySortNameAsc;
            SortByNameDesc.Text = lang.LibrarySortNameDesc;
            SortByNewest.Text = lang.LibrarySortNewest;
            SortByOldest.Text = lang.LibrarySortOldest;

            VideoAddFolderIcon.Data = FluentIcons.Geometry(FluentIcons.FolderAdd);
            VideoPlayAllIcon.Data = FluentIcons.Geometry(FluentIcons.PlayCircle);

            // The music section's own command, in the same slot. Its words, glyph and
            // accessible name are set here beside Play all so the two can never drift
            // apart: to the user they are one button whose meaning the section decides.
            MusicShuffleAllText.Text = lang.LibraryShufflePlay;
            MusicShuffleAllIcon.Data = FluentIcons.Geometry(FluentIcons.ArrowShuffle);
            AutomationProperties.SetName(MusicShuffleAllButton, lang.LibraryShufflePlay);
            ToolTipService.SetToolTip(MusicShuffleAllButton, lang.LibraryShufflePlay);

            UpdateVideoSortChrome();
        }

        /// <summary>
        /// Points the sort button's caption, the menu's four radio marks and the
        /// enabled state of the two time orders at the order the list on screen is
        /// in. Which order that is belongs to the SECTION: the music library's
        /// <see cref="EffectiveMusicSort"/> while the music section is up, the video
        /// library's <see cref="_videoSort"/> otherwise.
        ///
        /// Called from ApplyVideoStrings, because a language change re-words it;
        /// from ApplySectionHeader, which is the one place the section and the music
        /// tab change, so a tab switch cannot leave the caption describing the
        /// previous tab; and after every sort choice.
        /// </summary>
        private void UpdateVideoSortChrome()
        {
            var lang = AppContext.AppLang;

            // Which field the menu speaks for. The two libraries sort themselves,
            // so the caption and the four marks have to describe the order the list
            // ON SCREEN is in - otherwise a video sort left on "Newest" narrates a
            // music list that was never ordered by time.
            var isMusic = _librarySection == LibrarySectionMusic;

            var current = isMusic ? EffectiveMusicSort : _videoSort;

            var caption = current switch
            {
                VideoSortMode.NameDesc => lang.LibrarySortNameDesc,
                VideoSortMode.Newest => lang.LibrarySortNewest,
                VideoSortMode.Oldest => lang.LibrarySortOldest,
                VideoSortMode.Artist => lang.LibrarySortArtist,
                VideoSortMode.Year => lang.LibrarySortYear,
                VideoSortMode.Added => lang.LibrarySortAdded,
                _ => lang.LibrarySortNameAsc,
            };

            VideoSortButton.Content = $"{lang.LibrarySortBy}: {caption}";

            SortByNameAsc.IsChecked = current == VideoSortMode.NameAsc;
            SortByNameDesc.IsChecked = current == VideoSortMode.NameDesc;
            SortByNewest.IsChecked = current == VideoSortMode.Newest;
            SortByOldest.IsChecked = current == VideoSortMode.Oldest;
            SortByArtist.IsChecked = current == VideoSortMode.Artist;
            SortByYear.IsChecked = current == VideoSortMode.Year;
            SortByAdded.IsChecked = current == VideoSortMode.Added;

            // Words, not just marks: an item with no Text renders as an empty row.
            SortByNameAsc.Text = lang.LibrarySortNameAsc;
            SortByNameDesc.Text = lang.LibrarySortNameDesc;
            SortByNewest.Text = lang.LibrarySortNewest;
            SortByOldest.Text = lang.LibrarySortOldest;
            SortByArtist.Text = lang.LibrarySortArtist;
            SortByYear.Text = lang.LibrarySortYear;
            SortByAdded.Text = lang.LibrarySortAdded;

            // The three tag orders exist only where there are tags. The video library
            // sorts a folder scan, which carries no artist, year or arrival date, so
            // offering them there would be three rows that change nothing.
            var tagOrders = isMusic ? Visibility.Visible : Visibility.Collapsed;

            SortByArtist.Visibility = tagOrders;
            SortByYear.Visibility = tagOrders;
            SortByAdded.Visibility = tagOrders;

            // Greyed out where the tab has no time dimension, rather than hidden:
            // the menu keeps its shape, and the dimmed item says why the order it
            // names is not on offer here.
            SortByNewest.IsEnabled = !(isMusic && !MusicSortHasTimeModes);
            SortByOldest.IsEnabled = !(isMusic && !MusicSortHasTimeModes);
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
        /// A sort menu choice. The order is session state, and WHICH field holds it
        /// is the section's business: the music library has
        /// <see cref="_musicSort"/> so that reordering music cannot reorder the
        /// video grid. The button caption and the list are refreshed from the field
        /// just written.
        /// </summary>
        private void VideoSort_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioMenuFlyoutItem { Tag: string tag })
            {
                return;
            }

            var parsed = tag switch
            {
                "name-desc" => VideoSortMode.NameDesc,
                "newest" => VideoSortMode.Newest,
                "oldest" => VideoSortMode.Oldest,
                "artist" => VideoSortMode.Artist,
                "year" => VideoSortMode.Year,
                "added" => VideoSortMode.Added,
                _ => VideoSortMode.NameAsc,
            };

            if (_librarySection == LibrarySectionMusic)
            {
                _musicSort = parsed;
            }
            else
            {
                _videoSort = parsed;
            }

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
        /// <summary>
        /// Shuffles the whole music library: every visible track, in a random order.
        ///
        /// <para>Shuffle rather than Play all, and the difference is not cosmetic. A
        /// video library has an order the user chose (the sort), so "play all" has a
        /// first file. A music library does not: 「随机播放」 is the reference own verb
        /// for this slot, and it is what <see cref="AlbumShuffleButton"/> already does
        /// inside an album.</para>
        ///
        /// <para>Shares the Play all path otherwise, missing files included: mpv failing
        /// on the first loadfile would read as "the button did nothing".</para>
        /// </summary>
        private void MusicShuffleAll_Click(object sender, RoutedEventArgs e)
        {
            var paths = _visibleEntries
                .Where(entry => !entry.IsMissing)
                .Select(entry => entry.Path)
                .ToList();
            if (paths.Count == 0)
            {
                return;
            }
            // Shuffled HERE rather than by a player flag: the queue is what PlayFiles
            // builds, so the order the user sees in the queue is the order that plays.
            var order = paths
                .Select(path => (path, key: Guid.NewGuid()))
                .OrderBy(entry => entry.key)
                .Select(entry => entry.path)
                .ToList();
            PlayFiles(order, OpenMode.Replace).FireAndForget(OnException);
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
        /// The music library's order, by whichever key the sort menu named.
        ///
        /// <para>Separate from <see cref="SortVideoEntries"/> because three of the modes
        /// do not exist for videos: a folder scan carries no artist, year or arrival date,
        /// so the two lists cannot share one switch without the video half growing arms
        /// that only ever see null.</para>
        ///
        /// <para>Every order falls back to the title, and every "missing tag" file sorts
        /// last rather than first: a list that opens on its untagged files buries the ones
        /// the reader can identify.</para>
        /// </summary>
        private static List<LibraryEntry> SortMusicEntries(List<LibraryEntry> entries, VideoSortMode mode)
        {
            IEnumerable<LibraryEntry> ordered = mode switch
            {
                VideoSortMode.NameDesc =>
                    entries.OrderByDescending(e => e.Title, StringComparer.CurrentCultureIgnoreCase),

                VideoSortMode.Artist => entries
                    .OrderBy(e => string.IsNullOrWhiteSpace(e.Artist) ? "\uFFFF" : e.Artist,
                        StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(e => e.Album, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase),

                VideoSortMode.Year => entries
                    .OrderBy(e => e.Year ?? int.MaxValue)
                    .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase),

                VideoSortMode.Added => entries
                    .OrderByDescending(e => e.AddedAt ?? DateTimeOffset.MinValue)
                    .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase),

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

            var cardTemplate = (DataTemplate)Resources["LibraryCardControlTemplate"];
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

        }

        private static UniformGridLayout CardLayout()
        {
            // 202 is 0.8 x the reference's 253, and it is the whole of 「卡片整体
            // 大小缩小一点，0.8左右」. The tile stays 16:9 because the files are
            // landscape frames; the cell height follows from both.
            //
            // The width here is the CONTENT's: CellWidthFor adds the frame's 6 DIP
            // gutter on each side. Scaled together with the album grid's 170 -> 136
            // so the two grids keep the ratio they had.
            const double width = 222;

            return new UniformGridLayout
            {
                MinItemWidth = CardMetrics.CellWidthFor(width),
                MinItemHeight = CardMetrics.CellHeightFor(width, isAlbum: false),
                MinColumnSpacing = CardMetrics.GridColumnSpacing,
                MinRowSpacing = CardMetrics.GridRowSpacing,
            };
        }

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
