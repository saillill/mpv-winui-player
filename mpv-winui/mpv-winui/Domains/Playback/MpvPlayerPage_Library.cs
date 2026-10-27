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

namespace mpv_winui.Domains.Playback
{
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
        /// What the active view is showing, after the section filter, the search
        /// box and the video sort. Held so the thumbnail loader can walk
        /// whatever is on screen - the flat grid or the per-folder groups - from
        /// one list instead of reading it back out of a control, and so "Play
        /// all" enqueues exactly the list the user is looking at.
        /// </summary>
        private List<LibraryEntry> _visibleEntries = [];

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

            // The data, not only the events. Leaving these populated was how a rebuilt
            // library could show entries from the folder that had just been unbound, and
            // it is the same missing-owner problem that made cover art vanish when the
            // tag pass replaced the entry objects.
            //
            // Two kinds, two calls: the three collections below are `readonly`, so they
            // must be emptied rather than reassigned, while the rest take the empty value
            // they were declared with.
            _libraryThumbnails.Clear();
            _libraryWithoutThumbnail.Clear();
            _groupHeaders.Clear();

            _libraryAll = [];
            _albumTracks = [];
            _lyrics = [];
            _palette = Audio.AlbumColorExtractor.None;

            // The "loaded once" flag belongs with the data it describes: with the caches
            // empty, a set flag would make the next entry skip the reload and show nothing.
            _libraryLoaded = false;

            // Two references that outlive their usefulness in a way a plain field does not:
            // one points at a live element, the other at the entry its menu belongs to.
            _activeCardOverlay = null;
            _cardMenuEntry = null;
            _openAlbum = null;
        }

        private void ApplyLibraryStrings()
        {
            ApplySectionHeader();
            ApplyEmptyStateContent();
            ApplyQueueStrings();
            ApplyVideoStrings();
            ApplyCardStrings();
            ApplySelectionStrings();
            ApplyPlaylistPanelStrings();
        }

        /// <summary>
        /// The card overflow menu's five items. Set here rather than in the
        /// markup because the menu is a single shared resource, so this is the
        /// one place with a stable handle on its items - and because a language
        /// change has to reach them.
        /// </summary>
        /// <summary>
        /// The selection bar's labels.
        ///
        /// In code rather than in the markup because the bar is built once and the
        /// language can change while it is open; a literal in XAML would need the page
        /// reloaded to follow.
        /// </summary>
        private void ApplySelectionStrings()
        {
            var lang = AppContext.AppLang;

            LibrarySelectionClearText.Text = lang.LibrarySelectionClear;
            SelectionPlayText.Text = lang.LibrarySelectionPlay;
            SelectionQueueText.Text = lang.LibrarySelectionQueue;
            SelectionPropertiesText.Text = lang.LibrarySelectionProperties;

            // The count string is a format with a {0}, so the bar is refreshed rather
            // than assigned - the number is the point of the label.
            UpdateSelectionBar();
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
            AutomationProperties.SetName(QueueClearButton, lang.LibraryQueueClear);
            AutomationProperties.SetName(QueueAddToButton, lang.LibraryQueueAddTo);

            // The queue's right-hand action. Reported as 播放队列也应有右上角"添加文件"按钮 --
            // measured, the queue had only Clear (x=936) and Add to (x=1087), both against the
            // left gutter, while every library section carries its own action in the top right.
            // It reuses the library's open-file string so the two read as the same action.
            QueueOpenFileText.Text = lang.LibraryOpenFile;
            AutomationProperties.SetName(QueueOpenFileButton, lang.LibraryOpenFile);
            QueueOpenFileIcon.Data = FluentIcons.Geometry(FluentIcons.FolderOpen);

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
            //
            // **Home now uses this same slot, and that is the point.** Reported:
            // "首页 打开文件 按钮位置太靠上，应与音乐库 添加文件夹 按钮处于相同位置".
            // Measured before the change, by UI Automation rectangles:
            //
            //     home    heading top=479   ShellOpenFileButton top=404  <- 75 px ABOVE
            //     music   heading top=407   VideoAddFolderButton top=445  <- on the row
            //
            // Home's only open command was the shell's DropDownButton, which lives on its
            // own band above the whole library (Grid.Row=0 of the content area). So on
            // Home the affordance sat in a different row from every other section's, and
            // the two never lined up. Driving it from this slot instead means one row, one
            // position, one owner -- and the shell button is hidden on Home as a
            // consequence (see UpdateShellOpenFileButton).
            var showHeadingAction = (isVideos && hasVideoFolders)
                || (isMusic && hasMusicFolders)
                || (isHome && string.IsNullOrWhiteSpace(_libraryFilter));
            VideoAddFolderButton.Visibility = showHeadingAction
                ? Visibility.Visible
                : Visibility.Collapsed;

            // One slot, two commands. The heading action is "Add folder" on the two media
            // libraries and "Open file" on Home, so its words and glyph follow the section
            // rather than the XAML. Set here, next to the visibility that decides whether
            // anyone can see it, so the two can never disagree.
            if (isHome)
            {
                VideoAddFolderText.Text = lang.LibraryOpenFile;
                AutomationProperties.SetName(VideoAddFolderButton, lang.LibraryOpenFile);
                VideoAddFolderIcon.Data = FluentIcons.Geometry(FluentIcons.Folder);
            }
            else
            {
                VideoAddFolderText.Text = lang.LibraryAddFolder;
                AutomationProperties.SetName(VideoAddFolderButton, lang.LibraryAddFolder);
                VideoAddFolderIcon.Data = FluentIcons.Geometry(FluentIcons.FolderAdd);
            }
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

            // Announced here rather than at each call site: this is the one place
            // the section changes, so one report cannot drift from the value.
            LibrarySectionChanged?.Invoke(_librarySection);

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

        private enum MusicGrouping
        {
            Songs,
            Albums,
            Artists,
        }

        /// <summary>Where each letter's first group header lives. Rebuilt with the groups.</summary>
        private readonly Dictionary<string, FrameworkElement> _groupHeaders = new(StringComparer.Ordinal);

        /// <summary>True while cards are being ticked.</summary>
        private bool _selectionMode;

        /// <summary>
        /// Enters or leaves selection mode.
        ///
        /// The entries' own flag is what the tick boxes bind to, so both the flag and
        /// the visible bar move together - a mode that was on while the bar was hidden
        /// would leave ticks the user could not act on.
        /// </summary>
        /// <remarks>
        /// Leaving clears every tick. The reference does the same, and keeping them would
        /// mean a mode re-entered later started with a selection nobody could see, from a
        /// list that may have changed shape in between.
        /// </remarks>
        private void SetSelectionMode(bool on)
        {
            if (_selectionMode == on)
            {
                return;
            }

            _selectionMode = on;

            foreach (var entry in _visibleEntries)
            {
                entry.SetSelectionMode(on);

                if (!on)
                {
                    entry.SetSelected(false);
                }
            }

            UpdateSelectionBar();
        }

        /// <summary>
        /// Refreshes the selection bar from the current ticks.
        ///
        /// Called after every toggle and after the mode changes, rather than from a
        /// collection-changed handler: the ticks live on the entries, so there is no
        /// collection to observe.
        /// </summary>
        private void UpdateSelectionBar()
        {
            var selected = _visibleEntries.Count(entry => entry.IsSelected);

            LibrarySelectionBar.Visibility = _selectionMode && selected > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            LibrarySelectionCount.Text = string.Format(
                AppContext.AppLang.LibrarySelectionCount, selected);

            // No ticks means nothing to act on: the buttons dim rather than vanish, so
            // the bar does not resize under the pointer as the first card is ticked.
            var hasSelection = selected > 0;

            SelectionPlayButton.IsEnabled = hasSelection;
            SelectionQueueButton.IsEnabled = hasSelection;
            SelectionPropertiesButton.IsEnabled = selected == 1;
        }

        /// <summary>
        /// Leaves selection mode and clears every tick.
        ///
        /// The reference's 清除 does both: the bar goes away and the cards stop showing
        /// tick boxes. Unticking alone would leave the mode on with nothing selected - a
        /// state the user had already asked to leave, still on, and reachable no other
        /// way back out except through another card's menu.
        ///
        /// Escape is not wired here on purpose: this page forwards keystrokes to mpv
        /// (MpvPlayerPage_InputForwarding), so an Escape handler would compete with the
        /// player's own binding instead of sitting above it.
        /// </summary>
        private void SelectionClear_Click(object sender, RoutedEventArgs e) =>
            SetSelectionMode(false);

        /// <summary>Plays the first ticked file and queues the rest.</summary>
        private void SelectionPlay_Click(object sender, RoutedEventArgs e)
        {
            var paths = _visibleEntries
                .Where(entry => entry.IsSelected && !entry.IsMissing)
                .Select(entry => entry.Path)
                .ToList();

            if (paths.Count == 0)
            {
                return;
            }

            PlayFiles(paths, OpenMode.Replace).FireAndForget(OnException);
        }

        /// <summary>Appends the ticked files to the queue without starting them.</summary>
        private void SelectionQueue_Click(object sender, RoutedEventArgs e)
        {
            var paths = _visibleEntries
                .Where(entry => entry.IsSelected && !entry.IsMissing)
                .Select(entry => entry.Path)
                .ToList();

            if (paths.Count == 0)
            {
                return;
            }

            PlayFiles(paths, OpenMode.Append).FireAndForget(OnException);
        }

        /// <summary>
        /// Opens the one ticked file's location in Explorer, with the file selected.
        ///
        /// The reference calls this "属性" and shows a real property sheet. This app has
        /// no property sheet, and opening the containing folder with the file selected is
        /// what its own "open file location" already does - a capability rather than a
        /// stand-in. The button is enabled only for a single selection, which is the
        /// same rule the reference applies.
        /// </summary>
        private void SelectionProperties_Click(object sender, RoutedEventArgs e)
        {
            var entry = _visibleEntries.FirstOrDefault(item => item.IsSelected);

            if (entry is null)
            {
                return;
            }

            FileLauncher.ShellLaunchFileAsync(entry.Path).FireAndForget(OnException);
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

        /// <summary>
        /// Rebuilds the library after a folder was bound or unbound.
        ///
        /// Clears <see cref="_libraryLoaded"/> first, and that line is the whole point:
        /// the loader is guarded by it so the library loads once rather than on every
        /// section switch. Leaving it set would make this do nothing at all - the symptom
        /// being "I unticked the folder and the files are still listed", which is exactly
        /// the bug this exists to fix.
        ///
        /// The full reload rather than an incremental removal, because a folder change
        /// alters which files exist, and that changes the grouping, the album cards, the
        /// index rail's letters and the empty state. Four things to get right instead of
        /// one loader that already gets all four right.
        /// </summary>
        private void ReloadLibraryForFolderChange()
        {
            // Nothing to refresh when the user is not looking at the library; entering
            // the section reloads anyway because the flag is now clear.
            if (!_libraryVisible)
            {
                _libraryLoaded = false;

                return;
            }

            _libraryLoaded = false;

            _ = LoadLibraryAsync();
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
        /// The first descendant with this name, found by walking the visual tree.
        ///
        /// This exists because FrameworkElement.FindName does not search inside a realised
        /// DataTemplate: the name belongs to the template instance, and the element the
        /// ElementPrepared handler receives is the template's root, not its namescope owner.
        /// </summary>
        private static T? FindDescendant<T>(DependencyObject root, string name)
            where T : FrameworkElement
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T typed && typed.Name == name)
                {
                    return typed;
                }

                if (FindDescendant<T>(child, name) is { } found)
                {
                    return found;
                }
            }

            return null;
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
            // INFO, not DEBUG, and deliberately so.
            //
            // "The video does not play" was undiagnosable for weeks because this
            // whole chain was silent: the handler ran (or did not), the surface
            // was told to change (or was not), and nothing said which. mpv's own
            // `mpv: loadfile` lines are logged next to these, so the five links
            // below split the load into its parts and name the one that failed.
            // They cost one write per file load.
            PlayChain.Page("start-file handler fired", string.Format(
                "wasPlaying={0} withdrew={1} forceShow={2}",
                _playbackActive, _withdrewToLibrary, _forceShowVideo));

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
                    PlayChain.Page("video", "start-file, no withdrawal");
                }
                else
                {
                    PlayChain.Page("library", "user withdrew to browse");
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

        /// <summary>
        /// The section now up, as one of the LibrarySection* keys.
        ///
        /// Separate from <see cref="ShellStateChanged"/> on purpose: that one
        /// answers "is a video playing", which is a different question from "which
        /// section is the user looking at". The shell needs the second to keep its
        /// open-file button on Home only, and widening ShellPageState would change
        /// a value every other shell consumer switches on.
        /// </summary>
        internal event Action<string>? LibrarySectionChanged;

        /// <summary>The section currently up.</summary>
        internal string CurrentLibrarySection => _librarySection;

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

            // Every transition of the one flag that decides whether the picture or
            // the library is on screen. Renamed to the PlayChain logger so a reader
            // can find it with the rest of the chain instead of grepping for a
            // message.
            PlayChain.Page(visible ? "library" : "video",
                string.Format("SetLibraryVisible, was {0}, playbackActive={1}",
                    wasVisible, _playbackActive));

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

                // Coming BACK to the picture has to put the music page back too, and nothing did.
                // ShowMusicPageFor has exactly one caller -- the FileStarted handler -- so the
                // page was only ever shown when a file loaded. Browsing away and returning to a
                // track that is still playing never loads a file again, so the audio page stayed
                // hidden and the raw mpv surface showed through. Reported as 音乐播放后先退回主页，
                // 再退回播放页，自绘封面就会消失，并返回 MPV 的原始界面.
                //
                // Restoring it here rather than at each call site for the same reason the rest of
                // this method exists: there are several ways back to the picture and one place
                // that knows the library just went away.
                ShowMusicPageFor(_mediaPlayer?.GetCurrentPath());
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
}
