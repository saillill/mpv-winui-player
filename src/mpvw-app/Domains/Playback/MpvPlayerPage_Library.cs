using mpv_winui.Infrastructure.Common.Media;

using mpv_winui.Domains.Library;
using mpv_winui.Domains.Playback.Cards;
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

            // The shared card menu takes the pointer with it, so the card never
            // sees its own exit. Forward Closed to whichever card opened it.
            if (Resources["LibraryCardContextMenu"] is MenuFlyout cardMenu)
            {
                cardMenu.Closed += CardMenu_Closed;
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
                cardMenu.Closed -= CardMenu_Closed;
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

            _cardMenuEntry = null;
            _openAlbum = default;
            _albumOpened = false;
            _openArtist = null;
        }

        private void ApplyLibraryStrings()
        {
            ApplySectionHeader();
            ApplyEmptyStateContent();
            ApplyQueueStrings();
            ApplyVideoStrings();
            ApplyGenreStrings();
            ApplyCardStrings();
            ApplySelectionStrings();
            ApplyPlaylistPanelStrings();
        }

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
            SelectionShowQueueText.Text = lang.LibrarySelectionShowQueue;

            // The two drop-downs' own labels. 添加到 is the queue page's existing word
            // for "add to" - the bar says the same thing in the same place - and 更多
            // is the card menu's, because it is the same overflow idea one level up.
            SelectionAddToText.Text = lang.LibraryQueueAddTo;
            SelectionMoreText.Text = lang.LibraryCardMore;

            // What used to be a button of its own in this bar, now the one item in
            // 更多. Same action, same key: the reference files it under 更多 rather
            // than spending a sixth slot on it.
            SelectionPropertiesItem.Text = lang.LibrarySelectionProperties;

            // Every action in the bar draws an icon plus a TextBlock, and the two
            // drop-downs draw a TextBlock and a chevron, so UIA reports each of them
            // with an empty name: it does not fold a child TextBlock's text into the
            // parent. Named and tooltipped here, in the same pass that sets the
            // visible words, so a language change cannot leave the accessible name in
            // the previous one.
            //
            // The 更多 item needs neither: a menu item shows its own text, and Windows
            // puts no tooltip on menu items.
            AutomationProperties.SetName(SelectionPlayButton, lang.LibrarySelectionPlay);
            AutomationProperties.SetName(SelectionQueueButton, lang.LibrarySelectionQueue);
            AutomationProperties.SetName(SelectionShowQueueButton, lang.LibrarySelectionShowQueue);
            AutomationProperties.SetName(SelectionAddToButton, lang.LibraryQueueAddTo);
            AutomationProperties.SetName(SelectionMoreButton, lang.LibraryCardMore);
            ToolTipService.SetToolTip(SelectionPlayButton, lang.LibrarySelectionPlay);
            ToolTipService.SetToolTip(SelectionQueueButton, lang.LibrarySelectionQueue);
            ToolTipService.SetToolTip(SelectionShowQueueButton, lang.LibrarySelectionShowQueue);
            ToolTipService.SetToolTip(SelectionAddToButton, lang.LibraryQueueAddTo);
            ToolTipService.SetToolTip(SelectionMoreButton, lang.LibraryCardMore);

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

            // No subtitle. Home used to carry one line - "Recently used media" -
            // over two bands that already say what they are (最近观看 / 最近收听).
            // Reported as 主页最近使用的媒体这几个字没用. The element stays declared
            // because the heading row reserves its height; nothing writes it.
            LibrarySectionSubtitle.Text = string.Empty;
            LibrarySectionSubtitle.Visibility = Visibility.Collapsed;

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

            // Held, then applied: while the selection bar is up this row is hidden
            // whatever the section wants, and ApplyVideoChromeVisibility is the one
            // place that combines the two.
            // Videos OR music: the same row serves both, and which buttons it shows
            // is decided by the section. 「音乐库也要有和视频库一样的上方按钮」 - the
            // point is that it is the SAME row in the same place, not a second
            // toolbar that would have to be kept in step with this one.
            //
            // And the artists tab keeps no row at all: it lists people, while every
            // control in the strip - Shuffle, the sort, and the genre button as it
            // arrives - acts on files. The tab condition belongs to THIS field and
            // nowhere else, so the whole row comes and goes together; a second test
            // inside a button is the two-answers-one-question shape this repository
            // has already paid for.
            _videoChromeWanted = ((isVideos && hasVideoFolders) || (isMusic && hasMusicFolders))
                && !(isMusic && _musicGrouping == MusicGrouping.Artists);
            ApplyVideoChromeVisibility();

            // The sort menu belongs to this row, and which field it narrates depends
            // on the section and the music tab - both of which this method is the
            // one place to change. Refreshed here so the caption can never describe
            // the previous section's order.
            UpdateVideoSortChrome();

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
        /// picker, and Playlists offers 「新建播放列表…」, which creates the empty
        /// list its own section will then show. That last one used to offer
        /// nothing, on the grounds that a button that cannot do what its label
        /// says is worse than no button - the rule is unchanged, and the section
        /// simply acquired an action that honours it.
        ///
        /// A search that matched nothing and a section with nothing in it are
        /// different answers: the searching state swaps both texts and hides
        /// the button outright, so the empty state never tells someone to go
        /// and play a file they have already played.
        /// </summary>
        private void ApplyEmptyStateContent()
        {
            var lang = AppContext.AppLang;
            // A genre is a filter too, so an empty grid it produced is not "this library
            // is empty" - it is "nothing matched". Same two texts as a search, because
            // from the user's side both are "I narrowed this and got nothing".
            var searching = !string.IsNullOrWhiteSpace(_libraryFilter)
                || _musicGenre.Kind != MusicGenreKind.All;

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

            // The queue offers no empty-state button: its own panel keeps its
            // "Add to" toolbar visible over the empty state, which is the
            // affordance that belongs there.
            //
            // Playlists USED to belong in that list, on the grounds that there
            // was no "make one" action to offer. There is one now - the section
            // creates a list from its own empty state - so it gets a button, and
            // it is the one screen where a person with no lists can make the
            // first one without first selecting something.
            var showButton = !searching
                && _librarySection is not LibrarySectionQueue;
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
                var isPlaylists = _librarySection == LibrarySectionPlaylists;

                // The same button serves three empty states, so its words and
                // glyph follow the section: adding a folder where a folder is
                // what a library needs, opening a file where a file is, and
                // creating a list where a list is.
                LibraryOpenFileButtonText.Text = isPlaylists
                    ? lang.PlaylistCreate
                    : addFolder ? lang.LibraryAddFolder : lang.LibraryOpenFile;

                LibraryOpenFileIcon.Data = FluentIcons.Geometry(
                    isPlaylists
                        ? FluentIcons.Add
                        : addFolder ? FluentIcons.FolderAdd : FluentIcons.Folder);

                AutomationProperties.SetName(
                    LibraryOpenFileButton, LibraryOpenFileButtonText.Text);
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
        /// <summary>
        /// The music library's three presentations.
        ///
        /// Songs is the flat list; Albums and Artists group the same files by the
        /// one tag each of them surfaces. The grouping uses the same per-file
        /// metadata the rest of the library reads, so there is no second index to
        /// keep in step - and a file with no tag lands in a single "unknown" group
        /// rather than being dropped, which is what the reference players do.
        /// </summary>
        private enum MusicGrouping
        {
            Songs,
            Albums,
            Artists,
        }

        /// <summary>Where each letter's first group header lives. Rebuilt with the groups.</summary>
        private readonly Dictionary<string, FrameworkElement> _groupHeaders = new(StringComparer.Ordinal);

        /// <summary>
        /// Whether the section wants its own toolbar (Play all and the sort).
        ///
        /// Held rather than assigned straight to <c>VideoChrome.Visibility</c>, because
        /// the selection bar has a say in the same answer: while it is up the row is
        /// hidden, and this field is what the header's half of that decision leaves
        /// behind for <see cref="ApplyVideoChromeVisibility"/>.
        /// </summary>
        private bool _videoChromeWanted;

        /// <summary>
        /// True while at least one visible card is ticked.
        ///
        /// Derived, never set from outside: <see cref="UpdateSelectionBar"/> writes it
        /// from the tick count and reads it back only to notice the moment the mode
        /// flips, so every card's tick box can be told once instead of on every toggle.
        /// A mode that could be switched on by itself is what let it outlive the ticks.
        /// </summary>
        private bool _selectionMode;

        /// <summary>
        /// Reads the ticks, then brings the bar and every card's tick box in line with
        /// them.
        ///
        /// The mode IS the count. This is the one place it is decided, and that is what
        /// keeps the two reported behaviours from being rules that can disagree: one or
        /// more ticked means the bar is up and every card shows its box; none ticked
        /// means the bar is gone and the cards are back to their normal hover. Unticking
        /// the last card therefore leaves the mode by itself - there is no exit to look
        /// for.
        ///
        /// Called after every toggle and after the labels change, rather than from a
        /// collection-changed handler: the ticks live on the entries, so there is no
        /// collection to observe.
        /// </summary>
        private void UpdateSelectionBar()
        {
            var selected = _visibleEntries.Count(entry => entry.IsSelected);

            var mode = selected > 0;

            if (mode != _selectionMode)
            {
                _selectionMode = mode;

                foreach (var entry in _visibleEntries)
                {
                    entry.SetSelectionMode(mode);
                }
            }

            LibrarySelectionBar.Visibility = mode ? Visibility.Visible : Visibility.Collapsed;

            // The bar stands where the library's own toolbar was, so the two are never
            // on screen together: see ApplyVideoChromeVisibility.
            ApplyVideoChromeVisibility();

            LibrarySelectionCount.Text = string.Format(
                AppContext.AppLang.LibrarySelectionCount, selected);

            // No ticks means nothing to act on: the buttons dim rather than vanish, so
            // the bar does not resize under the pointer as the first card is ticked.
            var hasSelection = selected > 0;

            SelectionPlayButton.IsEnabled = hasSelection;
            SelectionQueueButton.IsEnabled = hasSelection;
            SelectionShowQueueButton.IsEnabled = hasSelection;
            SelectionAddToButton.IsEnabled = hasSelection;

            // 更多 has to stay reachable: its one item is "show in folder", and the
            // dimming is on the item rather than on the drop-down, so a two-file
            // selection opens a menu that explains itself instead of a dead button.
            SelectionMoreButton.IsEnabled = true;
            SelectionPropertiesItem.IsEnabled = selected == 1;
        }

        /// <summary>
        /// Play all and the sort, hidden while the selection bar is up.
        ///
        /// The reference's bar stands where that row was, and the two are about
        /// different sets: Play all enqueues the whole list the ticks are a subset of.
        /// One write point, called from the two decisions that make it - the section
        /// header (does this section have a toolbar at all) and the tick count (is the
        /// bar up) - so neither of them can leave the row visible on its own.
        /// </summary>
        private void ApplyVideoChromeVisibility()
        {
            var show = _videoChromeWanted && !_selectionMode;
            VideoChrome.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

            // One row, two commands in the same slot. The section decides which, so
            // they can never both be up: a music library showing "Play all" over a
            // flat list of tracks would be playing "the first one", which means
            // nothing there.
            var isMusic = _librarySection == LibrarySectionMusic && !_selectionMode;

            VideoPlayAllButton.Visibility = isMusic ? Visibility.Collapsed : Visibility.Visible;
            MusicShuffleAllButton.Visibility = isMusic ? Visibility.Visible : Visibility.Collapsed;

            // The genre button joins the same swap. It is a MUSIC control standing in a
            // row both libraries share, so without this the video library shows
            // 「流派：所有流派」 over a grid the genre layer refuses to filter - a control
            // that does nothing, which is worse than one that is absent. Set here, beside
            // the other two, rather than in the markup: the markup cannot know the
            // section, and a second predicate elsewhere would be a second answer to one
            // question.
            MusicGenreButton.Visibility = isMusic ? Visibility.Visible : Visibility.Collapsed;
        }
        /// <summary>
        /// The bar's 清除: clears every tick, which is how the mode is left.
        ///
        /// There is no separate "leave" to call any more - the mode is the count, so
        /// unticking everything is the exit. It sweeps the visible entries rather than
        /// remembering which ones were ticked, for the same reason the bar counts them:
        /// the entries are the only record, and a filter can leave the list with entries
        /// that are no longer on screen.
        ///
        /// Escape is not wired here on purpose: this page forwards keystrokes to mpv
        /// (MpvPlayerPage_InputForwarding), so an Escape handler would compete with the
        /// player's own binding instead of sitting above it.
        /// </summary>
        private void SelectionClear_Click(object sender, RoutedEventArgs e) => ClearSelectionTicks();

        /// <summary>
        /// Unticks every visible entry, which is how the mode is left.
        ///
        /// Its own method because a second caller leaves the same way: creating a
        /// list from the selection spends it. Two callers, one sweep, so
        /// "leaving the selection" cannot come to mean two different things
        /// depending on which control was pressed.
        /// </summary>
        private void ClearSelectionTicks()
        {
            foreach (var entry in _visibleEntries)
            {
                entry.SetSelected(false);
            }

            UpdateSelectionBar();
        }

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
        /// The bar's 队列: brings the play queue on screen.
        ///
        /// It calls <see cref="ShowPlayQueue"/> rather than reaching for the section
        /// itself. That method is already the page's one way to the queue - the menu
        /// bar's Playlist item and the mpv menu's "playlist" command both go through it
        /// - and it is what makes the library surface visible before the section
        /// changes. A second path here would be a second place for that pair of steps
        /// to get out of order, which on the queue is the difference between landing on
        /// it and landing on the empty state of whatever section was up.
        /// </summary>
        private void SelectionShowQueue_Click(object sender, RoutedEventArgs e)
        {
            ShowPlayQueue();
        }

        /// <summary>
        /// 「添加到」: the menu is rebuilt every time it opens.
        ///
        /// Not a static list, because its middle is the named playlists and those change
        /// while the page is up (a list is created, renamed or deleted elsewhere). Built
        /// at parse time, the menu would be a copy that drifts; built here, it cannot.
        ///
        /// The first item is the play queue and the last is "new playlist", so the two
        /// fixed destinations sit at the ends and the named lists grow in the middle.
        /// The middle is filled from the folder on every open, never from a cache: a
        /// menu built from a stale field would show lists that no longer exist and
        /// omit the ones made since the page loaded.
        /// </summary>
        private void SelectionAddTo_Opening(object? sender, object e)
        {
            if (SelectionAddToButton.Flyout is not MenuFlyout menu)
            {
                return;
            }

            menu.Items.Clear();

            // The queue's own name from the navigation pane, not LibraryQueueAddTo:
            // that key is the "Add to" label this very button wears, and an item that
            // reads "Add to" inside a menu opened by "Add to" says nothing.
            var queue = new MenuFlyoutItem { Text = AppContext.AppLang.ShellNavQueue };
            queue.Click += SelectionAddToQueue_Click;
            menu.Items.Add(queue);

            menu.Items.Add(new MenuFlyoutSeparator());

            // Read the folder on every open. A menu built from a field filled when
            // the playlists section was last visited lists nothing on a cold page -
            // open the app, tick a card, press 添加到 - and would also offer lists
            // that have since been deleted.
            //
            // A tuple rather than the store's NamedPlaylist: the menu reads only
            // Name, and the full type would keep every path of every list alive for
            // a flyout that uses none of them. It is a local for the same reason -
            // nothing outside this method reads it.
            var namedPlaylists = _playlistStore.List()
                .Select(list => (list.Name, list.Count))
                .ToList();

            foreach (var playlist in namedPlaylists)
            {
                var item = new MenuFlyoutItem { Text = playlist.Name };
                item.Click += (_, _) => AddSelectionToPlaylist(playlist.Name);
                menu.Items.Add(item);
            }

            menu.Items.Add(new MenuFlyoutSeparator());

            var create = new MenuFlyoutItem { Text = AppContext.AppLang.PlaylistCreate };
            create.Click += SelectionAddToNewPlaylist_Click;
            menu.Items.Add(create);
        }

        /// <summary>
        /// 「添加到 → 播放队列」. Forwards to the bar's own 「加入播放队列」: one action with
        /// two ways in, and this forwards rather than repeating the query so the two
        /// cannot disagree about which files count as selected.
        /// </summary>
        private void SelectionAddToQueue_Click(object sender, RoutedEventArgs e)
        {
            SelectionQueue_Click(sender, e);
        }

        /// <summary>
        /// Puts the ticked files into an existing named playlist.
        ///
        /// The store appends rather than replaces, and de-duplicates: pressing
        /// this twice with the same files in the same list is one entry, not two.
        /// A missing or unreadable file is skipped exactly as the play and queue
        /// verbs skip it, so a list never grows an entry that cannot be opened.
        /// </summary>
        private void AddSelectionToPlaylist(string name)
        {
            var paths = _visibleEntries
                .Where(entry => entry.IsSelected && !entry.IsMissing)
                .Select(entry => entry.Path)
                .ToList();

            if (paths.Count == 0)
            {
                return;
            }

            _playlistStore.Append(name, paths);

            // No row rebuild here on purpose: this menu is only reachable from a
            // card grid with cards ticked, and the playlists section has no cards,
            // so the row count cannot be on screen when this runs. The section
            // rebuilds when it is next shown (ApplyLibraryFilter), and the add-to
            // menu re-reads the folder on every open, so both readers stay right
            // without a refresh from here.
        }

        /// <summary>
        /// 「添加到 → 新建播放列表…」: name a list, then put the ticked files in it.
        ///
        /// The dialog is the same one the section's empty state uses, with its own
        /// words - the first draft of that dialog said 「新建播放列表…」 on the
        /// rename path too, which is why the caption is now a parameter rather
        /// than hard-coded.
        /// </summary>
        private void SelectionAddToNewPlaylist_Click(object sender, RoutedEventArgs e)
        {
            CreatePlaylistFromSelectionAsync().FireAndForget(OnException);
        }

        /// <summary>
        /// Names a new list and fills it with the ticked files.
        ///
        /// Refuses a name that is already taken, rather than merging: 「新建」 that
        /// silently appends to an existing list is a surprise, while 「添加到 →
        /// [that list]」 right above it is the way to append on purpose.
        /// </summary>
        private async Task CreatePlaylistFromSelectionAsync()
        {
            var paths = _visibleEntries
                .Where(entry => entry.IsSelected && !entry.IsMissing)
                .Select(entry => entry.Path)
                .ToList();

            if (paths.Count == 0)
            {
                return;
            }

            var typed = await PromptForPlaylistNameAsync(
                AppContext.AppLang.PlaylistCreate,
                string.Empty,
                candidate => _playlistStore.List().Any(
                    list => string.Equals(list.Name, candidate, StringComparison.CurrentCultureIgnoreCase))
                        ? AppContext.AppLang.PlaylistNameTaken
                        : null);

            if (typed is null)
            {
                return;
            }

            _playlistStore.Create(typed, paths);

            BuildPlaylistRows();

            // The selection has been spent - it became a list. Leaving every card
            // ticked would let the next click put the same files in a second list
            // by accident.
            ClearSelectionTicks();
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
            var thumbnails = LoadThumbnailsAsync(generation);

            // Album and artist tags, same rule: the cards are already up and the
            // grouped tabs fill in when the index answers.
            _ = LoadMusicTagsAsync(generation);

            // Video lengths: a third pass for a third reason. The music pass reads
            // MusicProperties, which has no handler for a video container, so video
            // entries never received a Duration and their cards' second line never
            // appeared - 「视频没有显示秒数」. Separate from the music pass rather than
            // folded into it: that one rebuilds entries through FromFileWithTags,
            // which would overwrite a video entry's fields with empty music tags.
            //
            // Sequenced AFTER the thumbnails rather than fired alongside them, and
            // the order is load-bearing: this pass ends by rebuilding the grid so the
            // new second line appears, and a rebuild that runs while decodes are in
            // flight produces cards whose covers resolve to nothing - measured as
            // placeholder glyphs where the sleeves had been. Waiting costs the length
            // caption a few hundred milliseconds and keeps the pictures.
            _ = FinishVideoDurationsAsync(generation, thumbnails);
        }

        /// <summary>
        /// Waits for the thumbnails, then fills in video lengths.
        ///
        /// The two are sequenced rather than parallel because the length pass ends in
        /// a grid rebuild; see the call site for why that rebuild must not race the
        /// decodes.
        /// </summary>
        private async Task FinishVideoDurationsAsync(int generation, Task thumbnails)
        {
            try
            {
                await thumbnails;
            }
            catch (Exception ex)
            {
                // A failed decode pass is not a reason to skip the lengths: they are
                // independent facts, and the rebuild is safe once the decodes have
                // stopped whether or not they succeeded.
                _logger.Warn(ex, "library: thumbnails failed before the length pass");
            }

            await LoadVideoDurationsAsync(generation);
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
            IEnumerable<LibraryEntry> entries = _librarySection switch
            {
                LibrarySectionMusic => _libraryAll.Where(e => MediaKinds.IsAudio(e.Path)),
                LibrarySectionVideos => _libraryAll.Where(e => e.IsVideo),
                LibrarySectionHome => _libraryAll,
                // There is no playlist source yet. Saying so is better than
                // inventing cards, which is what this section used to do.
                _ => [],
            };

            // The music library BEFORE the search runs. The genre dropdown's
            // vocabulary comes from here, not from the search results (spec 8.3.11):
            // built from the results, every genre outside the current matches would
            // vanish from the menu, and the entry the user is filtering by would
            // disappear while the button still named it.
            var beforeSearch = _librarySection == LibrarySectionMusic
                ? entries.ToList()
                : null;

            if (!string.IsNullOrWhiteSpace(_libraryFilter))
            {
                var needle = _libraryFilter.Trim();
                entries = entries.Where(e =>
                    e.Title.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || e.Path.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || e.Subtitle.Contains(needle, StringComparison.OrdinalIgnoreCase));
            }

            // The music library as it stands AFTER the search and BEFORE the genre
            // filter. Two things need it: the genre dropdown's vocabulary (below), and
            // the Albums tab's whole-album retention. Declared here rather than inside a
            // branch because those two live on different branch conditions.
            List<LibraryEntry>? beforeGenre = null;

            var cards = entries.ToList();

            // Genre applies to the Songs and Albums tabs only (spec 8.3.6). The artists
            // tab is not a list of files but one tile per person, and its toolbar row -
            // genre button included - is hidden. Without this exclusion, carrying a genre
            // over to that tab would silently drop half the tiles with nothing on screen
            // to explain why.
            var genreApplies = _librarySection == LibrarySectionMusic
                && _musicGrouping != MusicGrouping.Artists;

            if (genreApplies)
            {
                beforeGenre = cards;

                // From the PRE-SEARCH set (spec 8.3.11). Substituting the search
                // results here is the exact failure the clause names: type a query and
                // the menu shrinks to that query's genres, so the chosen genre can
                // vanish while it is still filtering the grid.
                _musicGenreVocabulary = MusicGenreFilter.Vocabulary(
                    (beforeSearch ?? beforeGenre).Select(e => (e.Album, e.Genre)));

                cards = beforeGenre.Where(e => MusicGenreFilter.Matches(e.Genre, _musicGenre)).ToList();
            }
            // Two libraries have a grouped presentation of one list: the video
            // library by the folder each file was played from, the music library
            // by album or artist. Both share the sectioned container below. Every
            // other section stays a flat grid.
            var musicGrouped = _librarySection == LibrarySectionMusic
                && _musicGrouping != MusicGrouping.Songs;

            // Home always uses the sectioned container: its two bands need two
            // different card shapes, which one grid cannot give them.
            var homeBanded = _librarySection == LibrarySectionHome;

            var folderView = (_librarySection == LibrarySectionVideos && _videoFolderView)
                || musicGrouped || homeBanded;

            // The Albums tab keeps an album WHOLE. The genre filter above dropped every
            // track that is not the chosen genre, which would leave a matching album
            // holding only the tracks that happen to carry that tag - its heading would
            // count one number and its album page another. So a genre match pulls in
            // every track of its album, taken from the snapshot before the genre layer.
            if (_librarySection == LibrarySectionMusic
                && _musicGrouping == MusicGrouping.Albums
                && _musicGenre.Kind != MusicGenreKind.All
                && beforeGenre is { } allTracks)
            {
                var kept = MusicGenreFilter.KeptAlbumKeys(
                    allTracks.Select(e => (e.Album, e.Genre)), _musicGenre);

                var keptSet = new HashSet<string>(kept, StringComparer.CurrentCultureIgnoreCase);

                cards = allTracks
                    .Where(e => keptSet.Contains(string.IsNullOrWhiteSpace(e.Album) ? string.Empty : e.Album))
                    .ToList();
            }

            if (_librarySection == LibrarySectionVideos)
            {
                cards = SortVideoEntries(cards, _videoSort);
            }
            else if (_librarySection == LibrarySectionMusic)
            {
                // Songs is the flat list, so it is the tab the sort menu moves: the
                // mode comes from EffectiveMusicSort, not from a literal. A literal
                // here is exactly the reported defect - the button's caption changed
                // and the list never moved. The grouped tabs order by the tag, so
                // each group's cards read the way its header says, and by title
                // within a group.
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

                    _ => SortVideoEntries(cards, EffectiveMusicSort),
                };
            }

            _visibleEntries = cards;
            LibraryGrid.ItemsSource = cards;

            // The mode is the count OF THE VISIBLE CARDS, so replacing that list is a
            // change to it. Switching section or typing a search while a card is ticked
            // drops the tick with the list it belonged to, and without this the bar would
            // stay up over "已选择 1 项" with nothing on screen ticked - the very state
            // this model exists to make unreachable. Cheap: one pass over cards already
            // in memory, and the entries' own flags are only rewritten when the mode
            // actually flips.
            UpdateSelectionBar();

            // The Songs tab is a flat grid of SQUARE sleeves, and the markup's
            // cell is built for a 16:9 frame. Dropping a square tile into it made
            // the cover fill the cell's height and pushed both caption lines
            // outside - the tab rendered as a wall of album art with no titles,
            // which is what "the music library still uses the old card style" was
            // reporting. The square layout is the same one the grouped tabs use,
            // so the two now share one set of numbers.
            ApplyLibraryGridLayout(_librarySection == LibrarySectionMusic);

            // The playlists section has no cards at all, so it takes the third
            // host and both card hosts stay down. Rebuilt from the folder here
            // rather than cached: the rows are files, and a list can appear or
            // vanish while this page is open.
            var isPlaylists = _librarySection == LibrarySectionPlaylists;

            if (isPlaylists)
            {
                BuildPlaylistRows();
            }

            LibraryGrid.Visibility = isPlaylists || folderView
                ? Visibility.Collapsed
                : Visibility.Visible;

            VideoFolderGroups.Visibility = folderView && !isPlaylists
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (!isPlaylists)
            {
                PlaylistsHost.Visibility = Visibility.Collapsed;
            }

            if (folderView)
            {
                if (musicGrouped)
                {
                    // Two grouped tabs, two shapes: Albums groups tracks under their
                    // album, Artists gives each artist ONE tile and keeps their songs a
                    // level down. Same container, different builder.
                    if (_musicGrouping == MusicGrouping.Albums)
                    {
                        BuildMusicTagGroups(cards);
                    }
                    else
                    {
                        BuildArtistTiles(cards);
                    }
                }
                else if (homeBanded)
                {
                    BuildHomeSections(cards);
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

            // The playlists section draws rows from the folder, not cards from
            // the history, so "no cards" says nothing about it. Its own emptiness
            // is the row count - which is why the card-side empty state must step
            // aside here, or a section holding one list would show 「你没有任何
            // 播放列表」 over it.
            ApplyContentSlotVisibility(isQueue, hasCards);

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
        /// Which of the three content surfaces takes the slot: the scrolling
        /// area, the empty state, or (for the queue) its own host.
        ///
        /// <para><b>Its own method because more than one caller changes the
        /// answer.</b> Creating, renaming or deleting a list changes the row
        /// count without going anywhere near the history, so those paths never
        /// reach ApplyLibraryFilter. Before this was extracted they wrote only
        /// <c>PlaylistsHost</c>, leaving <c>LibraryEmptyState</c> up and
        /// <c>LibraryScroll</c> collapsed - which is the shape of "I made a list
        /// and nothing appeared until I switched sections and came back".</para>
        /// </summary>
        private void ApplyContentSlotVisibility(bool isQueue, bool hasCards)
        {
            var hasPlaylists = _librarySection == LibrarySectionPlaylists
                && PlaylistsHost.Children.Count > 0;

            QueueHost.Visibility = isQueue ? Visibility.Visible : Visibility.Collapsed;

            LibraryScroll.Visibility = !isQueue && (hasCards || hasPlaylists)
                ? Visibility.Visible
                : Visibility.Collapsed;

            LibraryEmptyState.Visibility = !isQueue && !hasPlaylists && !hasCards
                ? Visibility.Visible
                : Visibility.Collapsed;
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
            // One button, one action per section - the same label-to-action rule
            // ApplyEmptyStateContent follows when it decides what the label says.
            // A button that says 「新建播放列表」 and then opens a file picker would
            // be the exact defect this repo keeps a checker for.
            if (_librarySection == LibrarySectionPlaylists)
            {
                CreateEmptyPlaylistAsync().FireAndForget(OnException);
            }
            else if (_librarySection is LibrarySectionMusic or LibrarySectionVideos)
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
            //
            // The artist goes with it: an artist page is reached from the same wall,
            // and leaving it open would make the next visit land one level in with no
            // way to see why.
            _openAlbum = default;

            _albumOpened = false;

            _openArtist = null;

            _albumTracks = [];

            ArtistHost.Visibility = Visibility.Collapsed;

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
