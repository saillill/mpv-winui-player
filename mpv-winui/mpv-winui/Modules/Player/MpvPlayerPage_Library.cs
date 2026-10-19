using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.Common.View;
using mpv_winui.Modules.FileSystem;
using mpv_winui.Modules.Motion;
using mpv_winui.Modules.Player.History;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace mpv_winui.Modules.Player
{
    /// <summary>
    /// Which of the playing page's states is up.
    ///
    /// One host page, two states: the video panel cannot leave the visual tree
    /// (libmpv binds it once), so "which page is showing" is a state rather
    /// than a navigation. The states differ in what the surrounding chrome
    /// does, which is what this names:
    ///
    ///   * <see cref="Library"/> - the library is up, nothing is playing.
    ///   * <see cref="Playing"/> - the playing page: video up, library down.
    ///     The navigation pane absorbs its clicks and the title bar's back key
    ///     is disabled, because there is nowhere to go back to; the page's own
    ///     withdraw button is the single way out.
    ///   * <see cref="LibraryWhilePlaying"/> - withdrawn to the library with
    ///     the video still running; the title bar's key returns to the video.
    /// </summary>
    internal enum ShellPage
    {
        Library,
        Playing,
        LibraryWhilePlaying,
    }

    /// <summary>
    /// The library: real files out of the player's own watch history, and the
    /// switch between it and the video.
    ///
    /// The switch never touches the video. <c>PlayerView</c> stays Visible and
    /// stays in the tree at all times, because libmpv binds that SwapChainPanel
    /// once and taking it out of the tree would break playback for good. What
    /// changes is whether an opaque surface is covering it.
    /// </summary>
    public sealed partial class MpvPlayerPage
    {
        internal const string LibrarySectionHome = "home";

        internal const string LibrarySectionMusic = "music";

        internal const string LibrarySectionVideos = "videos";

        /// <summary>
        /// The play queue. Not a history view like the rest of the library:
        /// its rows are mpv's own playlist, drawn from the same
        /// <c>PlaylistItems</c> the right-hand pane and the tab strip use.
        /// </summary>
        internal const string LibrarySectionQueue = "queue";

        internal const string LibrarySectionPlaylists = "playlists";

        /// <summary>
        /// Long edge of a decoded thumbnail, in pixels. The cards are about 180
        /// wide at 100% scale, so this covers 150% scaling without decoding
        /// anything the eye cannot see.
        /// </summary>
        private const uint LibraryThumbnailSize = 256;

        /// <summary>
        /// mpv reports an idle gap while it tears down the previous file and
        /// starts the next one, so acting on the first idle report would flash
        /// the library over the video on every track change. It only comes back
        /// once the player has stayed idle long enough that it is not merely
        /// between files.
        /// </summary>
        private readonly DispatcherTimer _libraryIdleTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };

        private readonly Dictionary<string, BitmapImage> _libraryThumbnails = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Paths the shell had no real frame for. Kept so a section the user
        /// moves back and forth between does not re-ask the shell about every
        /// card it already answered for.
        /// </summary>
        private readonly HashSet<string> _libraryWithoutThumbnail = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Everything in the history, newest first, once per path.</summary>
        private List<LibraryEntry> _libraryAll = [];

        /// <summary>Which section the content area is showing.</summary>
        private string _librarySection = LibrarySectionHome;

        private string _libraryFilter = string.Empty;

        private bool _libraryVisible;

        /// <summary>
        /// True while a file is loaded and playing, as opposed to the idle gap
        /// between two files.
        ///
        /// The library switch and the shell's back-to-home button both key off
        /// this, and both need it to mean "something is on screen that the user
        /// would be interrupting", which is not the same question as "mpv
        /// currently has a file open".
        /// </summary>
        private bool _playbackActive;

        /// <summary>
        /// True while the user has deliberately put the library back up over a
        /// running video (the shell's back key), as opposed to the video simply
        /// having nothing to show. Kept separately from
        /// <see cref="_libraryVisible"/> because it answers a different
        /// question - "should the next file hijack the surface?" - which must
        /// be no while browsing, and yes for a freshly opened file.
        /// </summary>
        private bool _withdrewToLibrary;

        private bool _libraryLoaded;

        /// <summary>
        /// Bumped whenever a new load supersedes the last one, so a thumbnail
        /// that arrives late from the old load cannot paint a card in the new
        /// one.
        /// </summary>
        private int _libraryGeneration;

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
        /// What the active view is showing, after the section filter, the search
        /// box and the video sort. Held so the thumbnail loader can walk
        /// whatever is on screen - the flat grid or the per-folder groups - from
        /// one list instead of reading it back out of a control, and so "Play
        /// all" enqueues exactly the list the user is looking at.
        /// </summary>
        private List<LibraryEntry> _visibleEntries = [];

        /// <summary>
        /// The one card overlay currently faded in. A pointer can rest on only
        /// one card, so tracking it lets a scroll or the overflow menu put the
        /// overlay away without walking the repeater's realised children.
        /// </summary>
        private FrameworkElement? _activeCardOverlay;

        /// <summary>
        /// The entry the overflow menu was opened for. Captured because the menu
        /// is a single shared MenuFlyout that outlives the card it points at.
        /// </summary>
        private LibraryEntry? _cardMenuEntry;

        private void SetupLibrary()
        {
            LibraryGrid.ElementPrepared += LibraryGrid_ElementPrepared;
            _libraryIdleTimer.Tick += LibraryIdleTimer_Tick;

            // The shared card menu puts its card's overlay away when it closes.
            // A flyout takes the pointer with it, so the card never sees its own
            // exit and the overlay would otherwise stay up behind the menu.
            if (Resources["LibraryCardContextMenu"] is MenuFlyout cardMenu)
            {
                cardMenu.Closed += LibraryCardMenu_Closed;
            }

            // Home is the section the page opens on, the same landing the
            // official player uses. What the grid shows waits for the player
            // (see the load in MpvPlayerPage_Loaded - the first moment the
            // watch history path exists), but the header and the empty state
            // stand from here, so the surface says something before the shell's
            // navigation pane has finished selecting.
            _librarySection = LibrarySectionHome;

            ApplyLibraryStrings();

            // The page opens on the library: nothing is playing yet, and an
            // empty video surface is a worse first impression than the list of
            // what you were last watching.
            SetLibraryVisible(true);
        }

        private void TeardownLibrary()
        {
            LibraryGrid.ElementPrepared -= LibraryGrid_ElementPrepared;
            _libraryIdleTimer.Stop();
            _libraryIdleTimer.Tick -= LibraryIdleTimer_Tick;

            if (Resources["LibraryCardContextMenu"] is MenuFlyout cardMenu)
            {
                cardMenu.Closed -= LibraryCardMenu_Closed;
            }
        }

        private void ApplyLibraryStrings()
        {
            ApplySectionHeader();
            ApplyEmptyStateContent();
            ApplyQueueStrings();
            ApplyVideoStrings();
            ApplyCardStrings();
            ApplyPlaylistPanelStrings();
        }

        /// <summary>
        /// The card overflow menu's five items. Set here rather than in the
        /// markup because the menu is a single shared resource, so this is the
        /// one place with a stable handle on its items - and because a language
        /// change has to reach them.
        /// </summary>
        private void ApplyCardStrings()
        {
            var lang = AppContext.AppLang;

            if (Resources["LibraryCardContextMenu"] is not MenuFlyout cardMenu)
            {
                return;
            }

            foreach (var item in cardMenu.Items)
            {
                if (item is not MenuFlyoutItem { Tag: string tag } menuItem)
                {
                    continue;
                }

                menuItem.Text = tag switch
                {
                    "play" => lang.LibraryCardPlay,
                    "add-queue" => lang.LibraryCardAddToQueue,
                    "open-location" => lang.LibraryCardOpenLocation,
                    "copy-path" => lang.LibraryCardCopyPath,
                    "copy-title" => lang.LibraryCardCopyTitle,
                    _ => menuItem.Text,
                };
            }
        }

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
            VideoAddFolderText.Text = lang.LibraryAddFolder;
            VideoPlayAllText.Text = lang.LibraryPlayAll;

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

        /// <summary>
        /// The play queue's toolbar labels and empty state. Its two buttons are
        /// laid out in markup with their Symbol icons; only the words are set
        /// here, from the section key the shell drove rather than from a stored
        /// copy, so a language change flows through the same path the header
        /// does.
        /// </summary>
        private void ApplyQueueStrings()
        {
            var lang = AppContext.AppLang;
            // Plain Buttons now, so the label is a TextBlock and the glyph is a
            // PathIcon rather than the CommandBar's Label/Icon pair.
            QueueClearText.Text = lang.LibraryQueueClear;
            QueueAddToText.Text = lang.LibraryQueueAddTo;

            QueueClearIcon.Data = FluentIcons.Geometry(FluentIcons.Delete);
            QueueAddToIcon.Data = FluentIcons.Geometry(FluentIcons.Add);

            // The empty queue reuses Home's empty words ("Nothing is playing" /
            // "Open a file to start playback ..."). An empty queue and an
            // empty Home describe the same situation - nothing has been played
            // yet - so a second, near-identical pair of strings would be one
            // more thing to keep in step for no gain.
            QueueEmptyIcon.Data = FluentIcons.Geometry(FluentIcons.List);
            QueueEmptyTitle.Text = lang.LibraryEmptyTitle;
            QueueEmptyDescription.Text = lang.LibraryEmptyDescription;
        }

        /// <summary>
        /// The content header: the section's title, plus the sub-heading Home
        /// carries ("Recently used media"). Mapped from the section key rather
        /// than passed in from the shell, so a language change can refresh the
        /// header without the shell re-sending anything.
        /// </summary>
        private void ApplySectionHeader()
        {
            var lang = AppContext.AppLang;

            LibrarySectionTitle.Text = _librarySection switch
            {
                LibrarySectionMusic => lang.LibraryMusic,
                LibrarySectionVideos => lang.LibraryVideos,
                LibrarySectionPlaylists => lang.LibraryPlaylists,
                // The queue's pane label and page heading are the same words,
                // so it reads its title straight from the navigation key.
                LibrarySectionQueue => lang.ShellNavQueue,
                _ => lang.LibraryHome,
            };

            var isHome = _librarySection == LibrarySectionHome;
            LibrarySectionSubtitle.Text = lang.LibraryHomeRecent;
            LibrarySectionSubtitle.Visibility = isHome ? Visibility.Visible : Visibility.Collapsed;

            var isVideos = _librarySection == LibrarySectionVideos;

            var isMusic = _librarySection == LibrarySectionMusic;

            // The toolbars are gated on THIS, not on which section is up.
            //
            // A library with no folder bound has nothing to play all of, nothing
            // to sort and nothing to group, so the official player shows only the
            // empty state there - the toolbar belongs to the state where there is
            // media to act on. Gating on the section alone put "Play all" and a
            // sort menu over an empty grid, which is what the report was about.
            var hasVideoFolders = AppContext.AppSetting.HasLibraryFolders(music: false);

            var hasMusicFolders = AppContext.AppSetting.HasLibraryFolders(music: true);

            VideoChrome.Visibility = isVideos && hasVideoFolders
                ? Visibility.Visible
                : Visibility.Collapsed;

            LibraryHeadingTabs.Visibility = isVideos && hasVideoFolders
                ? Visibility.Visible
                : Visibility.Collapsed;

            MusicHeadingTabs.Visibility = isMusic && hasMusicFolders
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Add folder follows the same rule as the rest of the toolbar, and
            // that is the official behaviour: while a library is empty the ONLY
            // way in is the accent button under the empty state's description, and
            // the top-right one appears once the library has folders. The previous
            // arrangement had both at once, which is the duplicate that was
            // reported against on the Home section.
            VideoAddFolderButton.Visibility = (isVideos && hasVideoFolders) || (isMusic && hasMusicFolders)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Fills the empty state for the current section.
        ///
        /// The illustration is the section's own media kind - an empty music
        /// library is drawn as music rather than as a generic folder, which is
        /// the same choice the official player makes - and the button follows
        /// the same rule: the two libraries offer "Add folder" (the app's
        /// existing open-folder command; its media joins the queue and enters
        /// the library through the watch history), Home offers the plain file
        /// picker, and Playlists offers nothing, because there is no playlist
        /// to create yet and a button that cannot do what its label says would
        /// be worse than no button.
        ///
        /// A search that matched nothing and a section with nothing in it are
        /// different answers: the searching state swaps both texts and hides
        /// the button outright, so the empty state never tells someone to go
        /// and play a file they have already played.
        /// </summary>
        private void ApplyEmptyStateContent()
        {
            var lang = AppContext.AppLang;
            var searching = !string.IsNullOrWhiteSpace(_libraryFilter);

            LibraryEmptyIcon.Data = FluentIcons.Geometry(_librarySection switch
            {
                LibrarySectionVideos => FluentIcons.Filmstrip,
                LibrarySectionPlaylists => FluentIcons.Album,
                LibrarySectionQueue => FluentIcons.List,
                LibrarySectionHome => FluentIcons.History,
                _ => FluentIcons.Music,
            });

            LibraryEmptyTitle.Text = searching ? lang.LibraryNoResults : _librarySection switch
            {
                LibrarySectionVideos => lang.LibraryEmptyVideosTitle,
                LibrarySectionPlaylists => lang.LibraryEmptyPlaylistsTitle,
                LibrarySectionQueue => lang.LibraryEmptyTitle,
                LibrarySectionHome => lang.LibraryEmptyHomeTitle,
                _ => lang.LibraryEmptyMusicTitle,
            };

            LibraryEmptyDescription.Text = searching ? lang.LibraryNoResultsHint : _librarySection switch
            {
                LibrarySectionVideos => lang.LibraryEmptyVideosDescription,
                LibrarySectionPlaylists => lang.LibraryEmptyPlaylistsDescription,
                LibrarySectionQueue => lang.LibraryEmptyDescription,
                LibrarySectionHome => lang.LibraryEmptyHomeDescription,
                _ => lang.LibraryEmptyMusicDescription,
            };

            // Playlists and the queue offer no empty-state button: neither has
            // a real "make one" action here, and the queue's own panel keeps
            // its "Add to" toolbar visible over its empty state, which is the
            // affordance that belongs there.
            var showButton = !searching
                && _librarySection is not (LibrarySectionPlaylists or LibrarySectionQueue);
            LibraryOpenFileButton.Visibility = showButton ? Visibility.Visible : Visibility.Collapsed;

            // The shell's own open button hides while this empty state is up.
            //
            // The official player has no permanent open button at all: while a
            // library is empty the only affordance is the one in the empty state,
            // and once the library has cards the top-right one appears. Two open
            // commands on one screen is what was reported - "do not show the
            // top-right open-file button in the empty state".
            EmptyStateVisibleChanged?.Invoke(LibraryEmptyState.Visibility == Visibility.Visible);

            if (showButton)
            {
                var addFolder = _librarySection is LibrarySectionMusic or LibrarySectionVideos;
                LibraryOpenFileButtonText.Text = addFolder ? lang.LibraryAddFolder : lang.LibraryOpenFile;
                LibraryOpenFileIcon.Data = FluentIcons.Geometry(addFolder ? FluentIcons.FolderAdd : FluentIcons.Folder);
            }
        }

        /// <summary>
        /// Selects which section the content area is showing.
        ///
        /// Called by the shell's navigation pane. The pane does not navigate
        /// ShellFrame - the video panel lives in this page and must not be
        /// unmounted - so a section change is a message rather than a
        /// navigation. The heading follows from the key: the shell owns the
        /// pane's label ("Music library"), this page owns its own heading
        /// ("Music").
        /// </summary>
        internal void ShowLibrarySection(string key)
        {
            // Defensive. The shell absorbs a navigation click while the playing
            // page is up, so this is the backstop for any caller that does not
            // know the state: the sections sitting behind the playing page must
            // not move, or withdrawing would land on one the user never chose.
            if (_playbackActive && !_libraryVisible)
            {
                return;
            }

            _librarySection = key;
            ApplySectionHeader();

            // The queue has nothing to read from disk: its rows are mpv's own
            // playlist. Selecting it shows the panel and asks for a refresh
            // through the same coalescing timer the right-hand pane uses, so
            // the queue never becomes a second refresh path.
            if (_librarySection == LibrarySectionQueue)
            {
                ApplyLibraryFilter();
                RefreshPlaylistAsync();
                return;
            }

            if (!_libraryLoaded)
            {
                _ = LoadLibraryAsync();
                return;
            }

            ApplyLibraryFilter();

            if (_libraryVisible)
            {
                MotionBehaviors.SectionSwitch(LibrarySectionTitle);
            }
        }

        /// <summary>
        /// Shows the queue's rows or its empty message, from the live playlist
        /// count. Called when the section is entered and again whenever mpv
        /// reports a playlist change, so an item added or cleared while the
        /// queue is on screen flips the panel without a second refresh path.
        /// </summary>
        private void RefreshQueueChrome()
        {
            if (_librarySection != LibrarySectionQueue)
            {
                return;
            }

            var hasItems = PlaylistItems.Count > 0;
            QueueView.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
            QueueEmptyState.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        }

        // ------------------------------------------------------ video library
        //
        // The video section carries the two things the official player puts
        // there and nowhere else: the All videos / Video folders tabs and the
        // toolbar under them. Both are session state - a way of looking at the
        // list, not a preference - so nothing here is persisted.

        private void VideoTabAll_Checked(object sender, RoutedEventArgs e) => SetVideoFolderView(false);

        private void VideoTabFolders_Checked(object sender, RoutedEventArgs e) => SetVideoFolderView(true);

        /// <summary>
        /// The music library's three presentations.
        ///
        /// Songs is the flat list; Albums and Artists group the same files by the
        /// one tag each of them surfaces. The grouping uses the same per-file
        /// metadata the rest of the library reads, so there is no second index to
        /// keep in step - and a file with no tag lands in a single "unknown" group
        /// rather than being dropped, which is what the reference players do.
        /// </summary>
        private void MusicTabSongs_Checked(object sender, RoutedEventArgs e) =>
            SetMusicGrouping(MusicGrouping.Songs);

        private void MusicTabAlbums_Checked(object sender, RoutedEventArgs e) =>
            SetMusicGrouping(MusicGrouping.Albums);

        private void MusicTabArtists_Checked(object sender, RoutedEventArgs e) =>
            SetMusicGrouping(MusicGrouping.Artists);

        private enum MusicGrouping
        {
            Songs,
            Albums,
            Artists,
        }

        private MusicGrouping _musicGrouping = MusicGrouping.Songs;

        private void SetMusicGrouping(MusicGrouping grouping)
        {
            if (_musicGrouping == grouping)
            {
                return;
            }

            _musicGrouping = grouping;

            if (_libraryLoaded)
            {
                ApplyLibraryFilter();
            }
        }

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

        // ---- album page commands -------------------------------------------

        private void AlbumBack_Click(object sender, RoutedEventArgs e) => CloseAlbum();

        /// <summary>The album's tracks, in table order, replacing the queue.</summary>
        private void AlbumPlayAll_Click(object sender, RoutedEventArgs e) => PlayAlbum(shuffle: false);

        /// <summary>
        /// The album's tracks in a shuffled order.
        ///
        /// Shuffled here rather than by mpv's shuffle property: this is a one-shot
        /// request to play this album in another order, and leaving mpv shuffling
        /// afterwards would silently change what "next" means for the rest of the
        /// session.
        /// </summary>
        private void AlbumShuffle_Click(object sender, RoutedEventArgs e) => PlayAlbum(shuffle: true);

        private void PlayAlbum(bool shuffle)
        {
            var paths = _albumTracks
                .Where(t => !t.IsMissing)
                .Select(t => t.Path)
                .ToList();

            if (paths.Count == 0)
            {
                return;
            }

            if (shuffle)
            {
                // Fisher-Yates with the shared Random: a single pass over the list
                // rather than OrderBy(random), which is both slower and biased.
                for (var i = paths.Count - 1; i > 0; i--)
                {
                    var j = Random.Shared.Next(i + 1);
                    (paths[i], paths[j]) = (paths[j], paths[i]);
                }
            }

            PlayFiles(paths, OpenMode.Replace).FireAndForget(OnException);
        }

        /// <summary>
        /// "Add to": appends the album to whatever is already queued.
        /// </summary>
        private void AlbumAddTo_Click(object sender, RoutedEventArgs e)
        {
            var paths = _albumTracks
                .Where(t => !t.IsMissing)
                .Select(t => t.Path)
                .ToList();

            if (paths.Count == 0)
            {
                return;
            }

            PlayFiles(paths, OpenMode.Append).FireAndForget(OnException);
        }

        /// <summary>Clicking a row in the album table plays it.</summary>
        private void AlbumTrackList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is LibraryEntry entry)
            {
                PlayLibraryEntry(entry);
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

            foreach (var group in GroupVideoFolders(entries))
            {
                VideoFolderGroups.Children.Add(new ContentPresenter
                {
                    Content = group,
                    ContentTemplate = headerTemplate,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                });

                var repeater = new ItemsRepeater
                {
                    ItemTemplate = cardTemplate,
                    ItemsSource = group.Items,
                };
                repeater.Layout = new UniformGridLayout
                {
                    MinItemWidth = 116,
                    MinItemHeight = 176,
                    MinColumnSpacing = 8,
                    MinRowSpacing = 16,
                };

                // The same arrival animation the flat grid uses, so the two tabs
                // read as one surface.
                repeater.ElementPrepared += LibraryGrid_ElementPrepared;
                VideoFolderGroups.Children.Add(repeater);
            }
        }

        // ------------------------------------------------------------ album page

        /// <summary>The album currently shown, or null when the page is closed.</summary>
        private string? _openAlbum;

        /// <summary>
        /// The entries of the open album, in playing order.
        ///
        /// Kept as a list rather than re-derived on every action: "play all" and
        /// "add to" must act on exactly what the table shows.
        /// </summary>
        private List<LibraryEntry> _albumTracks = [];

        /// <summary>
        /// Shows one album: its cover, its identity and its tracks.
        ///
        /// Called from a card click while the Albums tab is up. The tracks are the
        /// library's own entries filtered by the same album key the grid grouped
        /// on, so the table cannot disagree with the cards behind it.
        /// </summary>
        private void OpenAlbum(string album)
        {
            _openAlbum = album;

            _albumTracks = _librarySection == LibrarySectionMusic
                ? _libraryAll
                    .Where(e => MediaKinds.IsAudio(e.Path))
                    .Where(e => string.Equals(
                        string.IsNullOrWhiteSpace(e.Album) ? AppContext.AppLang.LibraryUnknownAlbum : e.Album,
                        album,
                        StringComparison.CurrentCultureIgnoreCase))
                    .OrderBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
                    .ToList()
                : [];

            ApplyAlbumChrome();

            LibraryHost.Visibility = Visibility.Collapsed;

            AlbumHost.Visibility = Visibility.Visible;

            MotionBehaviors.FadeIn(AlbumHost);
        }

        /// <summary>Closes the album page and puts the library back.</summary>
        private void CloseAlbum()
        {
            if (_openAlbum is null)
            {
                return;
            }

            HideSurfacesOverLibrary();

            LibraryHost.Visibility = Visibility.Visible;

            // The grid may have been reloaded while the album was open (a folder
            // re-bind, a rescan); re-applying is cheaper than tracking that.
            ApplyLibraryFilter();
        }

        /// <summary>
        /// Fills the album page from <see cref="_openAlbum"/> and
        /// <see cref="_albumTracks"/>.
        ///
        /// All strings, the artwork and the labels come from here rather than
        /// from markup, because the page is one surface reused for every album.
        /// </summary>
        private async void ApplyAlbumChrome()
        {
            var lang = AppContext.AppLang;

            AlbumBackText.Text = lang.LibraryAlbumBack;

            AlbumColumnTitle.Text = lang.LibraryColumnTitle;
            AlbumColumnArtist.Text = lang.LibraryColumnArtist;
            AlbumColumnGenre.Text = lang.LibraryColumnGenre;
            AlbumColumnLength.Text = lang.LibraryColumnLength;

            AlbumPlayAllText.Text = lang.LibraryPlayAll;
            AlbumShuffleText.Text = lang.LibraryShufflePlay;
            AlbumAddToText.Text = lang.LibraryAlbumAddTo;

            AlbumTitle.Text = _openAlbum ?? string.Empty;

            // The artist line is the album's own artist, which every track of an
            // album shares; the first track that has one speaks for the album.
            var artist = _albumTracks
                .Select(e => e.Artist)
                .FirstOrDefault(a => !string.IsNullOrWhiteSpace(a)) ?? string.Empty;

            AlbumArtist.Text = artist;

            AlbumSummary.Text = string.Format(
                lang.LibraryAlbumSummary,
                _albumTracks.Count,
                FormatTotalLength(_albumTracks));

            AlbumTrackList.ItemsSource = _albumTracks;

            var empty = _albumTracks.Count == 0;

            AlbumEmptyText.Text = lang.LibraryAlbumEmpty;

            AlbumEmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;

            AlbumTrackList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;

            AlbumPlayAllButton.IsEnabled = !empty;
            AlbumShuffleButton.IsEnabled = !empty;
            AlbumAddToButton.IsEnabled = !empty;

            await ApplyAlbumArtworkAsync();
        }

        /// <summary>
        /// Total running time of the open album, or "--:--" when no track reports
        /// one.
        ///
        /// Not printed as 0:00 for an album whose lengths the index skipped: that
        /// reads as an empty album rather than an unknown one.
        /// </summary>
        private static string FormatTotalLength(IReadOnlyList<LibraryEntry> tracks)
        {
            var known = tracks.Where(t => t.Duration is not null).ToList();

            if (known.Count == 0)
            {
                return "--:--";
            }

            var total = TimeSpan.FromTicks(known.Sum(t => t.Duration!.Value.Ticks));

            return total.TotalHours >= 1
                ? $"{(int)total.TotalHours}:{total.Minutes:00}:{total.Seconds:00}"
                : $"{total.Minutes}:{total.Seconds:00}";
        }

        /// <summary>
        /// The album's cover.
        ///
        /// From the first track's embedded picture, through the same native getter
        /// the music page uses - the player is told which path to read, so this
        /// does not depend on the album's own file being the one playing.
        /// </summary>
        private async Task ApplyAlbumArtworkAsync()
        {
            AlbumArtwork.Visibility = Visibility.Collapsed;
            AlbumArtworkFallbackIcon.Visibility = Visibility.Visible;

            if (_albumTracks.Count == 0 || _mediaPlayer is null)
            {
                return;
            }

            try
            {
                // Try each track until one carries art: an album ripped as one
                // file per track usually tags only the first, and a compilation
                // may tag only some.
                foreach (var track in _albumTracks)
                {
                    if (!_mediaPlayer.TryGetCoverArtForPath(track.Path, out var stream) || stream is null)
                    {
                        continue;
                    }

                    var bitmap = new BitmapImage();

                    await bitmap.SetSourceAsync(stream);

                    AlbumArtwork.Source = bitmap;

                    AlbumArtwork.Visibility = Visibility.Visible;

                    AlbumArtworkFallbackIcon.Visibility = Visibility.Collapsed;

                    return;
                }
            }
            catch (Exception ex)
            {
                // Same rule as the music page: no art is a normal state.
                _logger.Warn(ex, "album: cover art unavailable");
            }
        }

        /// <summary>
        /// Builds the music library's "Albums" and "Artists" tabs: one header and
        /// one card grid per tag, in the same container the video library's
        /// folder view uses.
        ///
        /// A file with no tag is not dropped: it lands in one group labelled
        /// "unknown" (localised), which is what the reference players do and what
        /// keeps the card count honest. The group order is alphabetical by tag.
        /// </summary>
        private void BuildMusicTagGroups(List<LibraryEntry> entries)
        {
            VideoFolderGroups.Children.Clear();

            var cardTemplate = (DataTemplate)Resources["LibraryCardTemplate"];
            var headerTemplate = (DataTemplate)Resources["VideoFolderHeaderTemplate"];

            var byAlbum = _musicGrouping == MusicGrouping.Albums;

            // "Unknown" sorts last rather than first: it is the fallback bucket,
            // not the most important album in the library.
            var unknownLabel = byAlbum
                ? AppContext.AppLang.LibraryUnknownAlbum
                : AppContext.AppLang.LibraryUnknownArtist;

            var groups = entries
                .GroupBy(entry =>
                {
                    var key = byAlbum ? entry.Album : entry.Artist;

                    return string.IsNullOrWhiteSpace(key) ? "\uFFFF" : key;
                }, StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
                .Select(group => new VideoFolderGroup(
                    group.Key == "\uFFFF" ? unknownLabel : group.Key,
                    group.ToList()));

            foreach (var group in groups)
            {
                VideoFolderGroups.Children.Add(new ContentPresenter
                {
                    Content = group,
                    ContentTemplate = headerTemplate,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                });

                var repeater = new ItemsRepeater
                {
                    ItemTemplate = cardTemplate,
                    ItemsSource = group.Items,
                };
                repeater.Layout = new UniformGridLayout
                {
                    MinItemWidth = 116,
                    MinItemHeight = 176,
                    MinColumnSpacing = 8,
                    MinRowSpacing = 16,
                };

                repeater.ElementPrepared += LibraryGrid_ElementPrepared;
                VideoFolderGroups.Children.Add(repeater);
            }
        }

        /// <summary>
        /// Fills in album / artist / length for the music entries, in one batch.
        ///
        /// The folder scan cannot carry these: it enumerates paths and never opens
        /// a file, because doing so for a thousand-track folder takes minutes.
        /// Reading them afterwards keeps the grid on screen while the tags arrive,
        /// and only the music library pays for it.
        /// </summary>
        private async Task LoadMusicTagsAsync(int generation)
        {
            var audio = _libraryAll.Where(e => !e.IsVideo && MediaKinds.IsAudio(e.Path)).ToList();

            if (audio.Count == 0)
            {
                return;
            }

            var paths = audio.Select(e => e.Path).ToList();

            try
            {
                var tags = await LibraryTagsReader.ReadAsync(paths);

                if (generation != _libraryGeneration)
                {
                    return;
                }

                for (var i = 0; i < _libraryAll.Count; i++)
                {
                    var entry = _libraryAll[i];

                    if (!tags.TryGetValue(entry.Path, out var found))
                    {
                        continue;
                    }

                    _libraryAll[i] = LibraryEntry.FromFileWithTags(
                        entry.Path, entry.Title, entry.Time, found) ?? entry;
                }

                // The grid holds the same objects the list did, so it has to be
                // handed the new instances rather than refreshed in place.
                if (_librarySection == LibrarySectionMusic && _libraryLoaded)
                {
                    ApplyLibraryFilter();
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "library: music tags unavailable");
            }
        }

        /// <summary>
        /// Groups video entries by the directory each was played from, ordered
        /// by folder name. Two directories that share a leaf name (two
        /// "Downloads" folders) stay separate groups: the key is the full path,
        /// only the label is the leaf.
        /// </summary>
        private static List<VideoFolderGroup> GroupVideoFolders(List<LibraryEntry> entries)
        {
            var groups = new List<VideoFolderGroup>();

            var byFolder = entries
                .GroupBy(entry => FolderOf(entry.Path), StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => FolderLabel(group.Key), StringComparer.CurrentCultureIgnoreCase);

            foreach (var group in byFolder)
            {
                groups.Add(new VideoFolderGroup(FolderLabel(group.Key), group.ToList()));
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

        /// <summary>
        /// The shell's search box. Empty text restores the whole section.
        /// </summary>
        internal void ApplyLibrarySearch(string? text)
        {
            var filter = text ?? string.Empty;
            if (string.Equals(filter, _libraryFilter, StringComparison.Ordinal))
            {
                return;
            }

            _libraryFilter = filter;

            // Searching is a request to see something, so a search typed while
            // a file is playing brings the library back rather than filtering a
            // surface the user cannot see.
            if (!string.IsNullOrEmpty(filter) && !_libraryVisible)
            {
                SetLibraryVisible(true);
            }

            if (_libraryLoaded)
            {
                ApplyLibraryFilter();
            }
        }

        /// <summary>
        /// First read of the watch history, called once the player exists.
        ///
        /// Not from the constructor: the history path comes from the player,
        /// and an early read would cache an empty library as a loaded one.
        /// </summary>
        internal void LoadLibraryFirstTime()
        {
            if (!_libraryLoaded)
            {
                _ = LoadLibraryAsync();
            }
        }

        private async Task LoadLibraryAsync()
        {
            var generation = ++_libraryGeneration;

            List<LibraryEntry> entries = [];
            string? historyPath = null;

            try
            {
                // Read here rather than from the constructor: the player does
                // not exist yet at that point, and its own answer changes with
                // the save-history setting.
                historyPath = _mediaPlayer?.GetWatchHistoryPath();
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "library: watch history path unavailable");
            }

            if (!string.IsNullOrEmpty(historyPath))
            {
                try
                {
                    entries = await Task.Run(() => BuildEntries(historyPath));
                }
                catch (Exception ex)
                {
                    // A missing or unreadable history file is a normal state,
                    // not a failure: the empty state is the right answer.
                    _logger.Warn(ex, "library: watch history unreadable");
                }
            }

            // The folders the user bound, scanned after the history so a file
            // that is both recently played and inside a bound folder keeps its
            // history timestamp and does not appear twice.
            //
            // This is what makes the library a library: before it, the only
            // source was the watch history, so a file existed in the grid only if
            // it had been played at least once. A bound folder contributes files
            // nobody has opened yet.
            foreach (var (music, folders) in new[]
                     {
                         (false, AppContext.AppSetting.GetVideoLibraryFolders()),
                         (true, AppContext.AppSetting.GetMusicLibraryFolders()),
                     })
            {
                foreach (var folder in folders)
                {
                    try
                    {
                        var scanned = await Task.Run(() => ScanFolder(folder, music));

                        entries.AddRange(scanned);
                    }
                    catch (Exception ex)
                    {
                        // A folder that has been moved or unplugged is a normal
                        // state; the rest of the library still works.
                        _logger.Warn(ex, "library: folder unreadable: {}", folder);
                    }

                    if (generation != _libraryGeneration)
                    {
                        return;
                    }
                }
            }

            if (generation != _libraryGeneration)
            {
                return;
            }

            _libraryAll = entries;
            _libraryLoaded = true;
            ApplyLibraryFilter();

            // Thumbnails after the cards, never before them: a grid that waits
            // for fifty disk reads before showing anything is slower to look at
            // than one that fills in.
            _ = LoadThumbnailsAsync(generation);

            // Album and artist tags, same rule: the cards are already up and the
            // grouped tabs fill in when the index answers.
            _ = LoadMusicTagsAsync(generation);
        }

        /// <summary>
        /// Reads the history file off the UI thread. A pure function of the
        /// file, so it can be handed to the thread pool.
        /// </summary>
        /// <summary>
        /// One library folder, as entries. Recursive, because a media folder
        /// normally has subfolders per album or per series - a non-recursive scan
        /// would show only the handful of files at the top.
        ///
        /// Kind-filtered: the video library contributes video files and the music
        /// library audio, so one folder can be bound to both and each library
        /// still shows only what belongs to it.
        /// </summary>
        private static List<LibraryEntry> ScanFolder(string folder, bool music)
        {
            var result = new List<LibraryEntry>();

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return result;
            }

            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,

                // Skip hidden and system files: a media library should not list
                // the thumbnails and cache files that Windows keeps beside media.
                // Fully qualified: Windows.Storage also declares FileAttributes,
                // and both are in scope in this file.
                AttributesToSkip = System.IO.FileAttributes.Hidden | System.IO.FileAttributes.System,

                // A folder the user cannot read is skipped rather than throwing
                // out of the whole scan.
                IgnoreInaccessible = true,
            };

            foreach (var path in Directory.EnumerateFiles(folder, "*", options))
            {
                var isAudio = MediaKinds.IsAudio(path);

                var isVideo = MediaKinds.IsVideo(path);

                if (music ? !isAudio : !isVideo)
                {
                    continue;
                }

                if (LibraryEntry.FromFile(path) is { } entry)
                {
                    result.Add(entry);
                }
            }

            return result;
        }

        private static List<LibraryEntry> BuildEntries(string historyPath)
        {
            var result = new List<LibraryEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var items = WatchHistoryParser.Parse(historyPath)
                .OrderByDescending(item => item.Time ?? DateTimeOffset.MinValue);

            foreach (var item in items)
            {
                // The history file is append-only, so the same file appears
                // once per play. The newest occurrence wins, which after this
                // sort is the first one seen.
                if (!seen.Add(item.Path))
                {
                    continue;
                }

                if (LibraryEntry.FromHistory(item.Path, item.Title, item.Time) is { } entry)
                {
                    result.Add(entry);
                }
            }

            return result;
        }

        /// <summary>
        /// Applies the section, the search filter, the video tab and the video
        /// sort, and repopulates whichever surface the section shows.
        /// </summary>
        private void ApplyLibraryFilter()
        {
            // The cards about to be replaced may include the one whose overlay is
            // up; the tracked reference would then point at a discarded element.
            _activeCardOverlay = null;

            IEnumerable<LibraryEntry> entries = _librarySection switch
            {
                LibrarySectionMusic => _libraryAll.Where(e => MediaKinds.IsAudio(e.Path)),
                LibrarySectionVideos => _libraryAll.Where(e => e.IsVideo),
                LibrarySectionHome => _libraryAll,
                // There is no playlist source yet. Saying so is better than
                // inventing cards, which is what this section used to do.
                _ => [],
            };

            if (!string.IsNullOrWhiteSpace(_libraryFilter))
            {
                var needle = _libraryFilter.Trim();
                entries = entries.Where(e =>
                    e.Title.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || e.Path.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || e.Subtitle.Contains(needle, StringComparison.OrdinalIgnoreCase));
            }

            var cards = entries.ToList();

            // Two libraries have a grouped presentation of one list: the video
            // library by the folder each file was played from, the music library
            // by album or artist. Both share the sectioned container below. Every
            // other section stays a flat grid.
            var musicGrouped = _librarySection == LibrarySectionMusic
                && _musicGrouping != MusicGrouping.Songs;

            var folderView = (_librarySection == LibrarySectionVideos && _videoFolderView) || musicGrouped;

            if (_librarySection == LibrarySectionVideos)
            {
                cards = SortVideoEntries(cards, _videoSort);
            }
            else if (_librarySection == LibrarySectionMusic)
            {
                // Songs keeps the title order the flat grid has always used. The
                // grouped tabs order by the tag first, so each group's cards read
                // the way its header says, and by title within a group.
                cards = _musicGrouping switch
                {
                    MusicGrouping.Albums => cards
                        .OrderBy(e => string.IsNullOrWhiteSpace(e.Album) ? "\uFFFF" : e.Album,
                            StringComparer.CurrentCultureIgnoreCase)
                        .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
                        .ToList(),

                    MusicGrouping.Artists => cards
                        .OrderBy(e => string.IsNullOrWhiteSpace(e.Artist) ? "\uFFFF" : e.Artist,
                            StringComparer.CurrentCultureIgnoreCase)
                        .ThenBy(e => e.Album, StringComparer.CurrentCultureIgnoreCase)
                        .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
                        .ToList(),

                    _ => SortVideoEntries(cards, VideoSortMode.NameAsc),
                };
            }

            _visibleEntries = cards;
            LibraryGrid.ItemsSource = cards;
            LibraryGrid.Visibility = folderView ? Visibility.Collapsed : Visibility.Visible;
            VideoFolderGroups.Visibility = folderView ? Visibility.Visible : Visibility.Collapsed;

            if (folderView)
            {
                if (musicGrouped)
                {
                    BuildMusicTagGroups(cards);
                }
                else
                {
                    BuildVideoFolderGroups(cards);
                }
            }
            else
            {
                VideoFolderGroups.Children.Clear();
            }

            var hasCards = cards.Count > 0;
            var isQueue = _librarySection == LibrarySectionQueue;

            // The queue is a sibling surface in the same slot, not a filtered
            // grid: exactly one of the three shows at a time. Its panel keeps
            // the toolbar above the rows and swaps the rows for the empty
            // message when mpv reports nothing queued.
            QueueHost.Visibility = isQueue ? Visibility.Visible : Visibility.Collapsed;
            LibraryScroll.Visibility = !isQueue && hasCards ? Visibility.Visible : Visibility.Collapsed;
            LibraryEmptyState.Visibility = !isQueue && !hasCards ? Visibility.Visible : Visibility.Collapsed;

            if (isQueue)
            {
                RefreshQueueChrome();
            }
            else if (!hasCards)
            {
                // The empty state's icon, texts and button all follow from the
                // section and from whether a search is what emptied it.
                ApplyEmptyStateContent();
            }
        }

        /// <summary>
        /// Fills in thumbnails one card at a time, after the grid is already up.
        ///
        /// Serial rather than parallel on purpose: every card is the same disk
        /// and the same shell thumbnail service, and a burst of fifty decodes
        /// at once would make the grid appear instantly and then stutter while
        /// it filled. One at a time, the cards simply arrive.
        /// </summary>
        private async Task LoadThumbnailsAsync(int generation)
        {
            // Walked from the field rather than out of the grid: the video
            // library can be showing the grouped view, whose cards live in
            // per-folder repeaters and not in LibraryGrid.ItemsSource. Both
            // views hold the same LibraryEntry objects, so filling this list
            // fills whichever one is on screen.
            foreach (var entry in _visibleEntries)
            {
                if (generation != _libraryGeneration)
                {
                    return;
                }

                if (entry.Thumbnail is not null)
                {
                    continue;
                }

                if (_libraryThumbnails.TryGetValue(entry.Path, out var cached))
                {
                    entry.SetThumbnail(cached);
                    continue;
                }

                if (_libraryWithoutThumbnail.Contains(entry.Path))
                {
                    continue;
                }

                var image = await TryLoadThumbnailAsync(entry);

                if (image is null)
                {
                    _libraryWithoutThumbnail.Add(entry.Path);
                    continue;
                }

                _libraryThumbnails[entry.Path] = image;

                if (generation == _libraryGeneration)
                {
                    entry.SetThumbnail(image);
                }
            }
        }

        /// <summary>
        /// One card's thumbnail, from the shell's thumbnail service.
        ///
        /// Not extracted with mpv. A second mpv instance per card would fight
        /// the playing one for the audio device and the GPU, and the playing
        /// one cannot be borrowed for it without interrupting playback. The
        /// shell already has a thumbnail provider for every format Windows can
        /// preview, backed by a disk cache, so this asks it instead.
        ///
        /// Returns null - never throws - when there is no thumbnail to be had.
        /// Null is a designed outcome: the card falls back to the media-kind
        /// icon, which is what a poster-less row should look like anyway.
        /// </summary>
        private static async Task<BitmapImage?> TryLoadThumbnailAsync(LibraryEntry entry)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(entry.Path);

                var mode = entry.IsVideo ? ThumbnailMode.VideosView : ThumbnailMode.MusicView;
                using var thumbnail = await file.GetThumbnailAsync(
                    mode, LibraryThumbnailSize, ThumbnailOptions.UseCurrentScale);

                // The shell answers with a generic type icon when it has no real
                // frame for the file. That icon is another app's idea of what a
                // video looks like, drawn at a size picked for Explorer, and
                // taking it would replace a correct Fluent icon with a wrong
                // one.
                if (thumbnail is null || thumbnail.Type != ThumbnailType.Image)
                {
                    return null;
                }

                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(thumbnail);
                return bitmap;
            }
            catch (Exception)
            {
                // Missing file, no provider, unsupported container, a drive that
                // went away mid-scan. All of them mean the same thing here.
                return null;
            }
        }

        /// <summary>
        /// Opens the file a card stands for.
        /// </summary>
        private void LibraryCard_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not LibraryEntry entry)
            {
                return;
            }

            // On the Albums tab a card stands for its album, not for one track:
            // clicking it opens the album, which is what the reference player does
            // and what makes the tab more than a differently-sorted list.
            if (_librarySection == LibrarySectionMusic && _musicGrouping == MusicGrouping.Albums)
            {
                OpenAlbum(string.IsNullOrWhiteSpace(entry.Album)
                    ? AppContext.AppLang.LibraryUnknownAlbum
                    : entry.Album);

                return;
            }

            PlayLibraryEntry(entry);
        }

        /// <summary>
        /// Opens a card's file. The card click and the overlay's Play button are
        /// the same action, so they share this one path rather than drifting.
        /// </summary>
        private void PlayLibraryEntry(LibraryEntry entry) =>
            _mediaPlayer.OpenAsync(new FileItem(entry.Path)).FireAndForget(OnException);

        // ------------------------------------------------------- card overlay
        //
        // The hover overlay is driven entirely from code, by the card's pointer
        // events, and never by a VisualState: the card template is shared by the
        // flat grid and every folder group, and a state machine parked on a
        // recycled template instance is exactly what would leave the next item
        // wearing the previous one's hover.

        private void LibraryCard_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not FrameworkElement card || FindCardOverlay(card) is not { } overlay)
            {
                return;
            }

            // Filled here, not in the template: the card template is shared, so
            // it cannot bind a page-level string, and doing it at the moment the
            // overlay becomes visible keeps it right after a language change.
            ApplyCardOverlayStrings(card);

            _activeCardOverlay = overlay;
            ShowCardOverlay(overlay);
        }

        private void LibraryCard_PointerExited(object sender, PointerRoutedEventArgs e) => HideCardOf(sender);

        private void LibraryCard_PointerCanceled(object sender, PointerRoutedEventArgs e) => HideCardOf(sender);

        /// <summary>
        /// A scroll moves the cards out from under a resting pointer without
        /// always producing an exit, so the overlay that was up is put away
        /// explicitly. Only the tracked one can be up, so there is nothing to
        /// enumerate.
        /// </summary>
        private void LibraryScroll_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (_activeCardOverlay is { } overlay)
            {
                HideCardOverlay(overlay);
            }
        }

        private void LibraryCardMenu_Closed(object? sender, object e)
        {
            if (_activeCardOverlay is { } overlay)
            {
                HideCardOverlay(overlay);
            }
        }

        private void HideCardOf(object sender)
        {
            if (sender is FrameworkElement card && FindCardOverlay(card) is { } overlay)
            {
                HideCardOverlay(overlay);
            }
        }

        private void ShowCardOverlay(FrameworkElement overlay)
        {
            overlay.Visibility = Visibility.Visible;
            overlay.IsHitTestVisible = true;
            MotionBehaviors.FadeIn(overlay);
        }

        /// <summary>
        /// Fades the overlay out and collapses it once the fade has run. It is
        /// made un-hit-testable immediately, so the 83 ms it spends fading out
        /// cannot swallow a click meant for whatever is underneath.
        /// </summary>
        private void HideCardOverlay(FrameworkElement overlay)
        {
            overlay.IsHitTestVisible = false;

            if (ReferenceEquals(_activeCardOverlay, overlay))
            {
                _activeCardOverlay = null;
            }

            MotionBehaviors.FadeOut(overlay, () =>
            {
                // Re-checked in the callback: the pointer can come back inside
                // the 83 ms the fade takes.
                if (!overlay.IsHitTestVisible)
                {
                    overlay.Visibility = Visibility.Collapsed;
                    MotionBehaviors.Reset(overlay);
                }
            });
        }

        /// <summary>Returns an overlay to the state a freshly realised card has.</summary>
        private static void ResetCardOverlay(FrameworkElement overlay)
        {
            MotionBehaviors.Reset(overlay);
            overlay.IsHitTestVisible = false;
            overlay.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Fills the overlay's two labels from AppLang. Called as the card is
        /// entered rather than from the template, which is shared and so cannot
        /// carry a page-level string.
        /// </summary>
        private static void ApplyCardOverlayStrings(FrameworkElement card)
        {
            var lang = AppContext.AppLang;

            if (FindDescendant(card, "CardPlayText") is TextBlock playText)
            {
                playText.Text = lang.LibraryCardPlay;
            }

            if (FindDescendant(card, "CardMoreButton") is Button moreButton)
            {
                AutomationProperties.SetName(moreButton, lang.LibraryCardMore);
                ToolTipService.SetToolTip(moreButton, lang.LibraryCardMore);
            }
        }

        /// <summary>The card's overlay, by name, from the card root.</summary>
        private static FrameworkElement? FindCardOverlay(DependencyObject card) =>
            FindDescendant(card, "CardHoverOverlay") as FrameworkElement;

        /// <summary>
        /// Depth-first search of a realised card for one named element.
        ///
        /// A name search rather than a template part because the card is a
        /// DataTemplate instance, not a ControlTemplate: there is no
        /// GetTemplateChild, and the overlay sits behind a content presenter
        /// whose shape is the card style's business.
        /// </summary>
        private static DependencyObject? FindDescendant(DependencyObject root, string name)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is FrameworkElement { Name: var childName } && childName == name)
                {
                    return child;
                }

                if (FindDescendant(child, name) is { } found)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Keeps a press on one of the overlay's buttons from reaching the card
        /// underneath.
        ///
        /// WinUI's own ButtonBase already marks the press handled, and that is
        /// the real guarantee - a click on Play or More cannot also open the
        /// card. This is the belt to that suspender: if the class handler ever
        /// stops marking it, the outer card click still cannot fire. Setting
        /// Handled does not stop the button itself raising Click.
        /// </summary>
        private void CardOverlayButton_PointerPressed(object sender, PointerRoutedEventArgs e) => e.Handled = true;

        private void CardPlay_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is LibraryEntry entry)
            {
                PlayLibraryEntry(entry);
            }
        }

        /// <summary>
        /// The overlay's overflow button: enables the items that need the file
        /// to exist, records which entry the menu is for, and shows the shared
        /// menu against this button.
        /// </summary>
        private void CardMore_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement button || button.DataContext is not LibraryEntry entry)
            {
                return;
            }

            _cardMenuEntry = entry;

            if (Resources["LibraryCardContextMenu"] is MenuFlyout cardMenu)
            {
                foreach (var item in cardMenu.Items)
                {
                    if (item is MenuFlyoutItem { Tag: string tag } menuItem)
                    {
                        // The three that open the file need it to be there; the
                        // two that copy a path or a title do not.
                        menuItem.IsEnabled = tag is "play" or "add-queue" or "open-location"
                            ? !entry.IsMissing
                            : true;
                    }
                }

                cardMenu.ShowAt(button);
            }
        }

        /// <summary>The card menu's five commands. All pre-existing app paths.</summary>
        private void CardMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem { Tag: string action } || _cardMenuEntry is not { } entry)
            {
                return;
            }

            switch (action)
            {
                case "play":
                    PlayLibraryEntry(entry);
                    break;
                case "add-queue":
                    // The queue's own "Add to" opens a picker, so what is reused
                    // here is its mechanism - a single loadfile append - not that
                    // method.
                    _mediaPlayer.OpenAsync((FileItem[])[new FileItem(entry.Path)], OpenMode.Append)
                        .FireAndForget(OnException);
                    break;
                case "open-location":
                    // Explorer with the file selected, which is what "open the
                    // file's location" means and what this helper already does.
                    FileLauncher.ShellLaunchFileAsync(entry.Path).FireAndForget(OnException);
                    break;
                case "copy-path":
                    ClipboardHelper.SetCopyText(entry.Path);
                    break;
                case "copy-title":
                    ClipboardHelper.SetCopyText(entry.Title);
                    break;
            }
        }

        /// <summary>
        /// The poster cards arriving, staggered so the grid reads as filling in
        /// rather than blinking on.
        ///
        /// This is the same behaviour the settings list uses, with the same
        /// distance, curve, duration and stagger - the two surfaces are meant to
        /// look like one product, and that only holds while both call this one
        /// name.
        /// </summary>
        private void LibraryGrid_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            // A recycled card keeps whatever the previous item left on it, and
            // the hover overlay is code-driven state the template cannot reset
            // by itself. Put it back to rest before the card is reused, so no
            // card can appear already dressed for another item's hover.
            if (FindCardOverlay(args.Element) is { } overlay)
            {
                ResetCardOverlay(overlay);
            }

            // The overlay's labels are set here as well as on hover, so a card
            // that is on screen but never hovered still carries its button names
            // for a screen reader walking the grid. Cheap: it reuses the walk
            // above. A language change reaches cards realised after it, and the
            // hover path covers the rest.
            if (args.Element is FrameworkElement card)
            {
                ApplyCardOverlayStrings(card);
            }

            var delay = TimeSpan.FromMilliseconds(Math.Min(args.Index, 8) * 20);
            MotionBehaviors.PosterEnter(args.Element, delay: delay);
        }

        private void LibraryIdleTimer_Tick(object? sender, object e)
        {
            _libraryIdleTimer.Stop();

            // Re-check before acting, and this is the whole of the reported
            // "pausing sends me back to Home".
            //
            // idle-active is not "there is no media": mpv clears and re-sets it
            // around a pause as well as around a file load, so a pause in the
            // middle of a video produced an idle edge. The timer was written on
            // the assumption that reaching it meant the player had stayed idle
            // for the interval - and the interval then expired underneath a
            // perfectly good, paused file, withdrew to the library, and left the
            // user on Home with the video still loaded.
            //
            // So the tick asks the player directly what it is holding. A path
            // means there is a file; a file means this is not the end of
            // playback, however idle mpv says it is.
            var stillLoaded = !string.IsNullOrEmpty(_mediaPlayer?.GetCurrentPath());

            // A pause is the other thing that is not the end: the file is up,
            // nothing is playing, and the only correct behaviour is to stay.
            var paused = _mediaPlayer?.IsPaused() ?? false;

            if (stillLoaded || paused)
            {
                return;
            }

            // No re-check of _playbackActive here: the flag moves before the
            // surface does, for the same reason the reverse transition does it
            // that way.
            _playbackActive = false;
            PlayerControl.SetTransportEnabled(false);
            SetLibraryVisible(true);

            // Playback has just ended, so the history on disk has changed.
            _ = LoadLibraryAsync();
        }

        /// <summary>
        /// A file has started loading: enter <see cref="ShellPage.Playing"/>.
        ///
        /// Driven by mpv's FILE_STARTED rather than by the pause observation.
        /// Pausing is not starting, and the old PlaybackStateChanged-only
        /// wiring - whose IsIdle was hard-coded false on the native side -
        /// could not tell the page that a file had begun, so a video played
        /// with the library still on screen.
        ///
        /// The one case that must not flip the surface is a deliberate
        /// withdrawal: the user pressed back to browse while something played,
        /// and the next track starting must not yank the library away from
        /// under them. That is what <see cref="_withdrewToLibrary"/> records,
        /// and it is only cleared when playback actually ends - so a fresh
        /// "open this file" always lands on the video, and an auto-advance
        /// during browsing never does.
        /// </summary>
        /// <summary>
        /// Set when the user opens or drops a file, so the next FileStarted
        /// shows the video regardless of whether a session was already running.
        /// Consumed once.
        /// </summary>
        private bool _forceShowVideo;

        /// <summary>
        /// Called from the open menu and from a drop: the two ways the user says
        /// "show me this file" while the page may already be playing something.
        /// </summary>
        internal void ShowVideoForNextOpen() => _forceShowVideo = true;

        private void MpvPlayerPage_LibraryFileStarted()
        {
            DispatcherQueue.RunAsync(() =>
            {
                _libraryIdleTimer.Stop();
                var wasPlaying = _playbackActive;
                _playbackActive = true;

                // A new session (nothing was playing) always shows the video;
                // an advance inside a session respects the withdrawal.
                //
                // _forceShowVideo is the third case: the user just handed this
                // page a file. That is an explicit request to watch it, so it
                // overrides the withdrawal even though a session is already
                // running - otherwise dropping a second video while browsing
                // loaded it and left the library on screen, which is exactly the
                // reported "dropping a new video during playback should jump to
                // the video page and play it".
                if (!wasPlaying || _forceShowVideo)
                {
                    _withdrewToLibrary = false;
                }

                _forceShowVideo = false;

                if (!_withdrewToLibrary)
                {
                    SetLibraryVisible(false);
                }

                // Audio gets the music page, video gets the picture.
                ShowMusicPageFor(_mediaPlayer?.GetCurrentPath());

                // The transport came alive the moment the file did.
                PlayerControl.SetTransportEnabled(true);

                RaiseShellState();
            });
        }

        /// <summary>
        /// The idle edge only. Entering playback is <see cref="MpvPlayerPage_LibraryFileStarted"/>'s
        /// job; this handler exists to arm the debounce that brings the library
        /// back when nothing is loaded any more.
        /// </summary>
        private void MpvPlayerPage_LibraryPlaybackStateChanged(mpv_winrt.PlaybackStateChangedEventArgs args)
        {
            DispatcherQueue.RunAsync(() =>
            {
                if (args.IsIdle)
                {
                    // Armed, not acted on: mpv reports idle during the tear-down
                    // between two files, and covering a running video with the
                    // library for 300ms on every track change is the bug this
                    // timer was added for.
                    _libraryIdleTimer.Stop();
                    _libraryIdleTimer.Start();
                }
                else
                {
                    _libraryIdleTimer.Stop();
                }
            });
        }

        /// <summary>
        /// The shell's back-to-home button.
        ///
        /// A toggle rather than a one-way trip. A button that only ever takes
        /// you to the library would strand you there during playback - the
        /// video is still running behind it, and nothing on the library page
        /// would take you back to it.
        /// </summary>
        /// <summary>
        /// Any transport press, from anywhere on the bar.
        ///
        /// Only one case needs work: the library is up over a running video and
        /// the user reached for the transport. The video comes back, and the
        /// button they pressed then acts on a picture they can see. When the video
        /// is already up this does nothing, which is the point - it must not
        /// consume the press.
        /// </summary>
        private void MpvPlayerPage_TransportInteraction(object? sender, EventArgs e)
        {
            if (_libraryVisible && _playbackActive)
            {
                SetLibraryVisible(false);
            }
        }

        internal void ToggleLibraryFromShell()
        {
            SetLibraryVisible(!_libraryVisible);
        }

        /// <summary>
        /// Which state the page is in. Derived rather than stored: the two
        /// booleans are the facts, and a stored copy would be a third thing to
        /// keep in step with them.
        /// </summary>
        internal ShellPage ShellPageState => !_playbackActive
            ? ShellPage.Library
            : _libraryVisible ? ShellPage.LibraryWhilePlaying : ShellPage.Playing;

        /// <summary>
        /// Told to the shell whenever the state changes, so it can re-point,
        /// enable or disable its back key and pass on or absorb a navigation
        /// click. The state rather than the two booleans: every shell question
        /// is "which state is this", and the two callers that asked with the
        /// booleans separately were one edit away from disagreeing about what
        /// "playing with the library up" means.
        /// </summary>
        internal event Action<ShellPage>? ShellStateChanged;

        /// <summary>
        /// True while a section's empty state is on screen. The shell uses it to
        /// put its own open button away, so an empty library offers exactly one
        /// way in - the one inside the empty state.
        /// </summary>
        internal event Action<bool>? EmptyStateVisibleChanged;

        private void RaiseShellState()
        {
            ShellStateChanged?.Invoke(ShellPageState);
        }

        private void LibraryOpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            // Home's empty state opens a file; the two libraries' "Add folder"
            // reuses the app's existing open-folder command. Same label to
            // action rule as ApplyEmptyStateContent, which decides which of
            // the two is on screen.
            if (_librarySection is LibrarySectionMusic or LibrarySectionVideos)
            {
                _ = AddLibraryFoldersAsync(_librarySection == LibrarySectionMusic);
            }
            else
            {
                OpenFileFromShell();
            }
        }

        /// <summary>
        /// The shell's open-file button.
        ///
        /// Routed through the page rather than the shell calling the picker
        /// itself: the picker's result has to reach this page's mpv instance,
        /// and the page is the thing that owns it.
        /// </summary>
        internal void OpenFileFromShell()
        {
            ShowVideoForNextOpen();
            _ = OpenFileAsync();
        }

        /// <summary>The "add a folder" leg of the shell's open menu.</summary>
        internal void OpenFolderFromShell()
        {
            ShowVideoForNextOpen();
            _ = OpenFolderAsync();
        }

        /// <summary>The "add from URL" leg of the shell's open menu.</summary>
        internal void OpenUrlFromShell()
        {
            ShowVideoForNextOpen();
            _ = OpenUrlAsync();
        }

        /// <summary>
        /// Puts away every surface that can cover the library.
        ///
        /// There are three: the music page, the album page, and the album page is
        /// the one that was missed. It sits at a higher ZIndex, has an opaque
        /// background and hit-tests by default, so leaving it up hides the library
        /// behind a featureless rectangle AND swallows the drop target that lives
        /// on the page root.
        ///
        /// One method rather than a line at each call site, and called from both
        /// directions, because the album page was added to this file later by
        /// someone who did not know the collapse list existed. A fourth surface
        /// should be no more likely to be forgotten.
        /// </summary>
        private void HideSurfacesOverLibrary()
        {
            MusicHost.Visibility = Visibility.Collapsed;

            StopRhythm();

            _musicPageVisible = false;

            // Any album is abandoned rather than kept: coming back to the library
            // is a navigation away from it, and re-entering it later should start
            // from the list, which is where the cards are.
            _openAlbum = null;

            _albumTracks = [];

            AlbumHost.Visibility = Visibility.Collapsed;

            MotionBehaviors.Reset(AlbumHost);
        }

        private void SetLibraryVisible(bool visible)
        {
            var wasVisible = _libraryVisible;
            _libraryVisible = visible;

            // Whether this counts as "the user chose to browse" is decided HERE
            // rather than at each call site: there are four ways the library can
            // come back over a running video (the shell's back key, a search
            // being typed, the queue being asked for, and playback ending), and
            // only one of them is not a user choice. Deriving it from the two
            // facts it depends on is what keeps a track advance from yanking the
            // library out from under someone reading it.
            _withdrewToLibrary = visible && _playbackActive;

            // The queue asks for itself on the way back, but on the way IN it
            // has to be asked too: the section is already "queue" when playback
            // ends, so nothing else would refresh it and the rows would be one
            // session old.
            SelectCurrentPlayListItem();

            // The menu bar belongs to the video, not the library: the official
            // player has no menu at all, and the user asked for the bar to stay
            // out of the library's way while every command stays one click away
            // during playback, which is exactly where it lives now.
            // The ROW goes to zero as well as the bar being hidden. Collapsing a
            // child of an Auto row collapses the row in principle, but the row
            // still participates in the page's own layout for one frame, and a
            // MenuBar's measured height is not zero while it is being hidden.
            // That frame is what put a #0F0F0F strip between the tab strip and
            // the library - reported as "the black band should not be there".
            MainMenuBar.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;

            MenuRow.Height = visible ? new GridLength(0) : GridLength.Auto;

            // Announced here rather than at each call site: the shell's button
            // has to follow every way the library can appear or disappear -
            // playback starting, playback ending, the button itself - and one
            // report from the single place the flag changes is the only version
            // of that which cannot drift.
            RaiseShellState();

            // The page's own tool keys belong to the picture, so they go with it.
            UpdatePlaybackTools(!visible);

            // And the transport follows the same fact: it is the transport for
            // what is playing, so with the library up there is nothing for it to
            // drive. Inert rather than hidden - hiding the bar would move the
            // window's bottom edge every time the user browsed.
            // Enabled while a file is loaded, even with the library up over it.
            //
            // The first version of this disabled the bar whenever the library
            // was visible, which greyed the transport on exactly the screen the
            // report was about: "returning to Home while a video plays must not
            // grey out the status bar". Browsing does not unload the video, so
            // the bar still has something to drive - every press just brings the
            // video back first (see MpvPlayerPage_TransportInteraction).
            PlayerControl.SetTransportEnabled(_playbackActive);

            // The music page covers the library the same way the video does, so
            // bringing the library back has to put the music page away with it.
            if (visible)
            {
                HideSurfacesOverLibrary();
            }

            if (visible)
            {
                LibraryHost.Visibility = Visibility.Visible;

                // Restoring the surface has to re-ask for the queue. While the
                // library was covered by playback, playlist events still
                // refreshed the collection, but the queue's scroll position and
                // current-row highlight were never recomputed for a surface
                // nobody could see, so this converges them on the way back.
                if (_librarySection == LibrarySectionQueue)
                {
                    RefreshPlaylistAsync();
                }

                if (wasVisible)
                {
                    // Already up: make sure a previous exit did not leave the
                    // composition opacity parked at zero.
                    MotionBehaviors.Reset(LibraryHost);
                    return;
                }

                MotionBehaviors.FadeIn(LibraryHost);
                return;
            }

            if (!wasVisible)
            {
                LibraryHost.Visibility = Visibility.Collapsed;
                return;
            }

            MotionBehaviors.FadeOut(LibraryHost, () =>
            {
                // Re-checked in the callback: playback can resume inside the
                // 83 ms the fade takes.
                if (!_libraryVisible)
                {
                    LibraryHost.Visibility = Visibility.Collapsed;
                    MotionBehaviors.Reset(LibraryHost);
                }
            });
        }
    }

    /// <summary>
    /// How the video library orders its list. Session-only: it is a way of
    /// looking at the list during this run, not a stored preference, so it has
    /// no AppSettings property and never reaches mpv.conf.
    /// </summary>
    public enum VideoSortMode
    {
        NameAsc,
        NameDesc,
        Newest,
        Oldest,
    }

    /// <summary>
    /// One folder in the video library's "Video folders" tab: the directory and
    /// the played videos that live directly in it. Built from the watch history
    /// alone, so a group exists because something in the folder was played, not
    /// because the folder was found on disk. <see cref="CountText"/> is a string
    /// so the row template can bind it to a TextBlock without a conversion.
    /// </summary>
    public sealed record VideoFolderGroup(string Name, IReadOnlyList<LibraryEntry> Items)
    {
        public string CountText => Items.Count.ToString();
    }
}
