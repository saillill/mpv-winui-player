using Microsoft.UI.Windowing;
using Windows.Graphics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.AppLifecycle;
using mpv_winui.Modules.Activation;
using mpv_winui.Modules.AppModel;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.Common.View;
using mpv_winui.Modules.Language;
using mpv_winui.Modules.MediaInfo;
using mpv_winui.Modules.Menu.MenuEditor;
using mpv_winui.Modules.MpvConf;
using mpv_winui.Modules.Player;
using mpv_winui.Modules.Settings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace mpv_winui
{
    public sealed partial class MainWindow : BaseWindow
    {
        private readonly WindowsManager _windowsManager;

        public MainWindow()
        {
            InitializeComponent();

            SetupStyle();

            _windowsManager = new WindowsManager();

            // The bar lives in the shell now, so MpvPlayerPage can no longer
            // wire these two in its own XAML. Forwarding through the frame
            // keeps one owner per request, and it is the same path the PiP
            // window takes because it raises the same events on its own bar.
            // The bar can hide itself (overlay idle timer) or reveal itself on
            // pointer activity. When it does, the ROW it sits in has to follow,
            // or the window keeps a 96 DIP strip of bar colour under a picture
            // that has already stopped showing controls - reported as "after the
            // status bar disappears, a solid colour strip is left behind".
            ShellPlayerBar.OnPanelVisibleChanged += visible =>
            {
                if (_fullWindow)
                {
                    ShellBarRow.Height = visible ? MpvShellBarRowLength : new GridLength(0);
                }
            };

            ShellPlayerBar.OnFullScreenRequest += () =>
                (ShellFrame?.Content as MpvPlayerPage)?.HandleFullScreenRequest() ?? false;
            ShellPlayerBar.OnFullWindowRequest += () =>
                (ShellFrame?.Content as MpvPlayerPage)?.HandleFullWindowRequest() ?? false;

            // The Window-level flag plus SetTitleBar, which is the pairing the
            // TitleBar control expects. The window used to set the AppWindow
            // TitleBar's own ExtendsContentIntoTitleBar instead, and that form
            // is invisible to the control: it extended the client area but
            // established no drag regions, so the bar could not drag the
            // window at all - the report that started this change.
            //
            // No explicit region is set here. The control refreshes its drag
            // regions itself (AutoRefreshDragRegions), and it computes them as
            // "the bar minus its interactive children", which is what keeps the
            // back button clickable. A hand-written caption rect over the whole
            // bar would be exactly the blanket that swallows those clicks -
            // the documented way to carve out interactive content is
            // NonClientRegionKind.Passthrough, and the framework already does
            // that division for a XAML title bar element.
            ExtendsContentIntoTitleBar = true;
            // The visible title is drawn in the bar's LeftHeader cluster (see
            // MainWindow.xaml), not by the TitleBar control: the icon, the gap
            // to the title and the title's font size all have to follow the
            // navigation pane's own geometry, and the control exposes none of
            // those (its TitleTextBlockFontSize / TitleBarTitleMargin theme
            // resources were measured to have no effect at either scope in
            // tools/mvpw-lab). The control's own Title therefore stays empty;
            // AppWindow.Title still carries the window-level name for the
            // taskbar and Alt-Tab.
            ShellTitleText.Text = PackageHelper.AppName;
            SetTitleBar(ShellTitleBar);

            // The title is ours now, so nothing bounds it the way the
            // framework's own title was bounded by its column - and the title
            // carries media file names. Clamp it to the bar's width less the
            // fixed chrome (the back button + icon + the system caption
            // buttons) so a long name ellipsizes instead of running under the
            // window's buttons.
            ShellTitleBar.SizeChanged += (_, e) =>
                ShellTitleText.MaxWidth = Math.Max(0, e.NewSize.Width - TitleChromeReserve);

            AppWindow.Title = PackageHelper.AppName;
            AppWindow.SetIcon("App.ico");
            var customTitle = AppContext.AppSetting.WindowTitle;
            if (!string.IsNullOrWhiteSpace(customTitle))
            {
                ShellTitleText.Text = customTitle;
                AppWindow.Title = customTitle;
            }

            SetupWindowSize();

            BuildShellNavigation();

            // The tab strip mirrors the player page's playlist, and that
            // playlist only exists once the page has been navigated to.
            ShellFrame.Navigated += ShellFrame_Navigated;
            AppContext.LanguageChanged += MainWindow_LanguageChanged;

            Closed += Window_Closed;
            AppContext.SettingChanged += MainWindow_SettingChanged;

            // Once, for the first frame. SetupStyle (line 34) applies the
            // backdrop, but nothing asks which of the two surface sets to show
            // until a theme or backdrop CHANGE arrives - so the window used to
            // start on whatever the markup had visible, which is the solid set.
            // That is why the main window showed no Mica on launch while the
            // settings window did: the backdrop was there the whole time, under
            // an opaque fill.
            ApplyShellBackdropVeil();
        }

        // ------------------------------------------------------- shell chrome

        /// <summary>
        /// How much of the title bar the fixed chrome owns: the back button and
        /// the app icon cluster, plus room for the system caption buttons. Used
        /// only to bound the title's width, so it errs wide.
        /// </summary>
        private const double TitleChromeReserve = 200;

        /// <summary>
        /// The two chrome row heights, read from the resources the markup uses.
        /// Read rather than repeated: a literal here would stop matching the
        /// moment the title row or the bar is retuned, and the failure mode is
        /// the window changing height when full window is left.
        ///
        /// GridLength, because that is what RowDefinition.Height takes. The
        /// x:Double versions stay for the title row's two keys, which size
        /// themselves rather than a row.
        /// </summary>
        private static GridLength MpvShellTitleRowLength =>
            (GridLength)Application.Current.Resources["MpvShellTitleRowLength"];

        private static GridLength MpvShellBarRowLength =>
            (GridLength)Application.Current.Resources["MpvShellBarRowLength"];

        /// <summary>One entry in the navigation pane's library list.</summary>
        private readonly record struct ShellSection(string Key, string Glyph, Func<AppLang, string> Title);

        /// <summary>
        /// The library sections, in the official Media Player's own order:
        /// Home, the two libraries, the play queue, then Playlists.
        ///
        /// Every glyph was rendered from Segoe Fluent Icons and compared
        /// against the same item in the official player's navigation pane
        /// before it was written down - E80F house, E8D6 double note, E8B2
        /// film frame, EA37 list (queue), E93C record sleeve. That comparison
        /// is the point: a wrong codepoint renders as a blank box, and nothing
        /// in this project's checks would catch it.
        ///
        /// The play queue is the one deliberate mismatch. The official player
        /// draws it as a play triangle beside three lines, at codepoint E93F,
        /// but that glyph is one of the few the player ships in its own
        /// MediaPlayerIcons font on top of the system font: Segoe Fluent Icons
        /// leaves E93F blank, so a nav built from the system font has to use
        /// the nearest glyph it actually has. EA37 (List) is that glyph - the
        /// render comparison is in this round's delivery notes.
        /// </summary>
        private static readonly ShellSection[] ShellSections =
        [
            new("home", "\uE80F", static l => l.ShellNavHome),
            new("music", "\uE8D6", static l => l.ShellNavMusic),
            new("videos", "\uE8B2", static l => l.ShellNavVideos),
            new("queue", "\uEA37", static l => l.ShellNavQueue),
            // The playlists section used to live here. It rendered the mpv
            // playlist again - the same rows the queue section and the side panel
            // already show - so it was a third view of one collection with no
            // distinct content of its own. Removed at the owner's request: "keep
            // only the play queue; the playlist is too much of a duplicate to be
            // worth anything".
        ];

        /// <summary>
        /// Built per call rather than cached in a static field: WinUI
        /// FontFamily is thread affine, and a static initialiser can be reached
        /// from a background thread (the same trap SettingsPage documents).
        /// </summary>
        private static FontFamily CreateShellIconFont() => new("Segoe Fluent Icons");

        private MpvPlayerPage? ActivePlayer => ShellFrame?.Content as MpvPlayerPage;

        /// <summary>
        /// The selected section is re-applied by key, so a language change can
        /// rebuild the labels without losing the user's place.
        /// </summary>
        private void BuildShellNavigation()
        {
            var lang = AppContext.AppLang;
            var previousKey = (ShellNav.SelectedItem as NavigationViewItem)?.Tag as string;

            ShellNav.MenuItems.Clear();
            NavigationViewItem? restore = null;

            foreach (var section in ShellSections)
            {
                var item = new NavigationViewItem
                {
                    Content = section.Title(lang),
                    Tag = section.Key,
                    Icon = new FontIcon
                    {
                        Glyph = section.Glyph,
                        FontFamily = CreateShellIconFont(),
                    },
                };

                if (string.Equals(section.Key, previousKey, StringComparison.Ordinal))
                {
                    restore = item;
                }

                ShellNav.MenuItems.Add(item);

                // The official player splits the pane's list into two groups:
                // the places you browse (Home and the two libraries) and the two
                // things you play from (the queue and the playlists), with a
                // hairline between them. It goes after the video library, which
                // is the last browsing entry.
                if (string.Equals(section.Key, "videos", StringComparison.Ordinal))
                {
                    ShellNav.MenuItems.Add(new NavigationViewItemSeparator());
                }
            }

            ShellSettingsText.Text = lang.SettingsTitle;

            // The pane's search and the title bar's open button. The icon is set
            // here rather than bound in markup because a PathIcon takes a
            // Geometry, not the path string the icon table holds, and this file
            // already gathers the shell's static text in one place.
            //
            // The box sits in the NavigationView's own AutoSuggestBox slot, and
            // the framework draws the rail's search button out of the same slot
            // - so this one string labels both shapes.
            ShellLibrarySearch.PlaceholderText = lang.ShellSearchPlaceholder;
            AutomationProperties.SetName(ShellLibrarySearch, lang.ShellSearchPlaceholder);
            ShellOpenFileText.Text = lang.LibraryOpenFile;

            BuildShellOpenMenu();
            ShellOpenFileIcon.Data = FluentIcons.Geometry(FluentIcons.Folder);

            ShellNav.SelectedItem = restore
                ?? (ShellNav.MenuItems.Count > 0 ? ShellNav.MenuItems[0] : null);

            // The pane's chrome follows its state, and this is the one place
            // that runs both at startup and on every language change.
            UpdateShellPaneChrome();
        }

        private void ShellNav_PaneOpened(NavigationView sender, object args) => UpdateShellPaneChrome();

        private void ShellNav_PaneClosed(NavigationView sender, object args) => UpdateShellPaneChrome();

        // The two "opening" edges exist for one reason: anything that changes
        // MEASUREMENT has to be settled before the pane starts moving, not after
        // it has arrived. Changing it on PaneOpened is what made the footer
        // label appear a frame after the rest of the pane.
        private void ShellNav_PaneOpening(NavigationView sender, object args) => UpdateShellPaneChrome();

        private void ShellNav_PaneClosing(NavigationView sender, object args) => UpdateShellPaneChrome();

        private void ShellNav_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args) => UpdateShellPaneChrome();

        /// <summary>
        /// The pane has two shapes - the full pane and the rail - and two things
        /// still have to be told which one is on screen: the footer's label, and
        /// the strip of pane surface that carries the pane's fill up through the
        /// title bar row. The search is not a third: it is the NavigationView's
        /// own AutoSuggestBox slot, and the framework swaps the box for its rail
        /// button on its own.
        ///
        /// The toggle moved out of the framework's hands on purpose. Its
        /// PaneToggleButton is drawn inside the pane root, which lives in the
        /// row under the title bar, so it could never sit beside the back arrow
        /// the way the official player's does - and because it lands on the
        /// rail's axis it read as the first navigation item rather than as a
        /// key. The shell now draws ShellPaneToggleButton in the title row and
        /// calls IsPaneOpen itself; this method only decides whether it has
        /// anything to toggle, which is "not while the pane is expanded" - the
        /// requested "no collapse button while the pane is open".
        /// </summary>
        private void UpdateShellPaneChrome()
        {
            var expanded = ShellNav.IsPaneOpen || ShellNav.DisplayMode == NavigationViewDisplayMode.Expanded;

            // The framework's own toggle is off for good; the shell draws its
            // own in the title row (ShellPaneToggleButton), and its visibility
            // is decided below, together with the pane surface it belongs to.
            //
            // Opacity, not Visibility: collapsing the label changes the footer's
            // desired size, and the NavigationView measures its pane from that.
            // In the rail that made the label disappear a frame late and come
            // back a frame late - the reported "Settings word lags the pane".
            // Opacity touches rendering only, so both shapes measure the same.
            ShellSettingsText.Opacity = expanded ? 1 : 0;

            // The pane reads as one surface from the window's top edge, so this
            // strip has to be exactly as wide as the pane is showing - the rail
            // in the collapsed state, the full pane when it is open (including
            // the overlay shape a narrow window uses), and nothing at all when
            // the pane is a closed fly-out.
            // Nothing at all while the pane is hidden - the playing page puts it
            // away (ApplyShellPageChrome) - or the strip would keep painting a
            // pane-coloured block in the title row with no pane under it.
            ShellNavPaneSurface.Width = !ShellNav.IsPaneVisible
                ? 0
                : ShellNav.DisplayMode switch
                {
                    NavigationViewDisplayMode.Expanded => ShellNav.OpenPaneLength,
                    NavigationViewDisplayMode.Compact => ShellNav.IsPaneOpen
                        ? ShellNav.OpenPaneLength
                        : ShellNav.CompactPaneLength,
                    _ => ShellNav.IsPaneOpen ? ShellNav.OpenPaneLength : 0,
                };

            // The title row's two keys belong to the pane's block: with the pane
            // gone there is nothing to toggle, so the hamburger goes with it and
            // the back arrow stays (it is the way out of the playing page).
            ShellPaneToggleButton.Visibility = !ShellNav.IsPaneVisible
                || ShellNav.DisplayMode == NavigationViewDisplayMode.Expanded
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        /// <summary>
        /// The pane's search box, forwarded to the library.
        ///
        /// Only user typing is forwarded. A TextChanged also fires when the box
        /// is set programmatically - including by this app when the language
        /// changes - and treating one of those as a search would filter the
        /// grid on text the user never typed.
        /// </summary>
        private void ShellLibrarySearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                ActivePlayer?.ApplyLibrarySearch(sender.Text);
            }
        }

        /// <summary>
        /// The same path as the page's own open-file command, so the two entry
        /// points cannot drift apart in what they do with the file.
        /// </summary>
        private void ShellOpenFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ActivePlayer?.OpenFileFromShell();
        }

        private void ShellOpenFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ActivePlayer?.OpenFolderFromShell();
        }

        private void ShellOpenUrlMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ActivePlayer?.OpenUrlFromShell();
        }

        /// <summary>
        /// Fill the open menu with the official player's three items: each a
        /// glyph, a title and a one-line explanation.
        ///
        /// Built in code rather than in markup because a MenuFlyoutItem's Text
        /// is a single string; the official menu is two lines per entry and the
        /// explanation is the part that tells you which of the three you want.
        /// </summary>
        private void BuildShellOpenMenu()
        {
            var lang = AppContext.AppLang;

            BuildShellOpenMenuItem(
                OpenFileMenuItem,
                FluentIcons.File,
                lang.ShellOpenFileTitle,
                lang.ShellOpenFileDescription);

            BuildShellOpenMenuItem(
                OpenFolderMenuItem,
                FluentIcons.Folder,
                lang.ShellOpenFolderTitle,
                lang.ShellOpenFolderDescription);

            BuildShellOpenMenuItem(
                OpenUrlMenuItem,
                FluentIcons.Globe,
                lang.ShellOpenUrlTitle,
                lang.ShellOpenUrlDescription);
        }

        /// <summary>
        /// One two-line menu entry.
        ///
        /// MenuFlyoutItem has no Content - it renders its Text and nothing else -
        /// so the two lines the official menu shows are one string with a
        /// newline in it, and the glyph is the item's Icon. That is a real
        /// constraint of the control, not a shortcut: assigning Content fails to
        /// compile because the property does not exist.
        /// </summary>
        private static void BuildShellOpenMenuItem(MenuFlyoutItem item, string pathData, string title, string description)
        {
            item.Text = title + "\n" + description;

            item.Icon = new PathIcon
            {
                Data = FluentIcons.Geometry(pathData),
                Width = 20,
                Height = 20,
            };
        }

        private void ShellNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            // The back button's enabled state depends on which section is up.
            UpdateShellBackButton();

            if (args.SelectedItem is not NavigationViewItem { Tag: string key })
            {
                return;
            }

            // The playing page absorbs navigation. The sections sitting behind
            // it must not move while the user cannot see them - otherwise
            // withdrawing would land on a section the user never chose - and a
            // click must not read as "left the video". The highlight is put
            // back where it was so the click reads as "nothing happened" rather
            // than as a selection that had no effect.
            if (ActivePlayer is { ShellPageState: ShellPage.Playing })
            {
                RestoreNavSelection();
                return;
            }

            foreach (var section in ShellSections)
            {
                if (string.Equals(section.Key, key, StringComparison.Ordinal))
                {
                    // The content area is inside the player page, so a section
                    // change is a message to that page rather than a navigation
                    // of ShellFrame. Navigating the frame would unmount the
                    // video panel, which is bound to libmpv once. The page maps
                    // the key to its own heading - the pane's label and the
                    // page's title are separate strings.
                    _lastNavKey = key;
                    ActivePlayer?.ShowLibrarySection(key);
                    return;
                }
            }
        }

        /// <summary>
        /// Puts the navigation highlight back on the section that is actually
        /// showing.
        ///
        /// Re-selecting raises SelectionChanged again, so the re-entry is
        /// guarded; without the guard the handler would see an absorbed click,
        /// snap the selection back, and be called again by its own snap.
        /// </summary>
        private void RestoreNavSelection()
        {
            if (_restoringNavSelection)
            {
                return;
            }

            foreach (var item in ShellNav.MenuItems)
            {
                if (item is NavigationViewItem { Tag: string tag } &&
                    string.Equals(tag, _lastNavKey, StringComparison.Ordinal))
                {
                    _restoringNavSelection = true;
                    try
                    {
                        ShellNav.SelectedItem = item;
                    }
                    finally
                    {
                        _restoringNavSelection = false;
                    }

                    return;
                }
            }
        }

        /// <summary>
        /// The title row's own hamburger. The framework's PaneToggleButton lives
        /// inside the pane root, which is the row BELOW the title bar, so it
        /// could never sit beside the back arrow; this one is in the title row
        /// and simply flips the pane. Setting IsPaneOpen is the same state the
        /// framework's button writes, so PaneOpened/PaneClosed still run and
        /// UpdateShellPaneChrome still re-measures the rail.
        /// </summary>
        private void ShellPaneToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ShellNav.IsPaneOpen = !ShellNav.IsPaneOpen;
        }

        private void ShellSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            OpenSettingWindow();
        }

        private void MainWindow_LanguageChanged()
        {
            DispatcherQueue.RunAsync(() =>
            {
                BuildShellNavigation();
            });
        }

        // ------------------------------------------------- page subscription

        /// <summary>
        /// The page the shell is currently subscribed to. Held so the
        /// subscription can be taken back off: the frame outlives the page it
        /// hosts, and a handler left on a page that is gone keeps it alive.
        /// </summary>
        private MpvPlayerPage? _watchedPlayer;

        /// <summary>
        /// The section the navigation pane's highlight belongs on. Kept here
        /// rather than read back off the selected item, because the playing
        /// page absorbs clicks: while it is up the pane may briefly show a
        /// selection that was never applied, and this is the value the snap-
        /// back has to aim at.
        /// </summary>
        private string _lastNavKey = "home";

        /// <summary>Guards the snap-back against the SelectionChanged it raises.</summary>
        private bool _restoringNavSelection;

        /// <summary>
        /// True while the page is the playing one. The library's chrome - the
        /// pane, the open-file button - stays down in that state.
        /// </summary>
        private bool _pageIsPlaying;

        /// <summary>True while fullwindow mode has the chrome put away.</summary>
        private bool _fullWindow;

        private void ShellFrame_Navigated(object sender, NavigationEventArgs e)
        {
            AttachActivePage();
        }

        private void AttachActivePage()
        {
            if (_watchedPlayer is not null)
            {
                _watchedPlayer.ShellStateChanged -= ActivePlayer_ShellStateChanged;
                _watchedPlayer.EmptyStateVisibleChanged -= ActivePlayer_EmptyStateVisibleChanged;
                _watchedPlayer = null;
            }

            if (ActivePlayer is { } page)
            {
                _watchedPlayer = page;
                page.ShellStateChanged += ActivePlayer_ShellStateChanged;
                page.EmptyStateVisibleChanged += ActivePlayer_EmptyStateVisibleChanged;

                // The page may already be showing the library by the time the
                // shell gets here, so both the button and the chrome are set
                // from the page's current state rather than waiting for the
                // next change - a page navigated to while already playing would
                // otherwise keep the pane up until something else moved.
                UpdateShellBackButton();
                ApplyShellPageChrome(page.ShellPageState);
            }
        }

        /// <summary>
        /// The shell's back button, styled after the official player's title
        /// bar arrow. What "back" means follows the page's state (ShellPage):
        /// on the playing page it withdraws to the library with the video still
        /// running, once the library is up it returns to the video, and
        /// otherwise it opens Home, dimming when Home is already showing.
        ///
        /// The playing state is enabled on purpose. The player page's own
        /// withdraw button used to be its only way out, which left the title
        /// bar's key dimmed on the one surface where "back to home" is the
        /// obvious thing to want; the key is now the player's back affordance
        /// too, and the overlay button is gone.
        /// </summary>
        private void UpdateShellBackButton()
        {
            var state = ActivePlayer?.ShellPageState ?? ShellPage.Library;

            // The key is ALWAYS enabled, and that is a deliberate change from
            // the state-dependent enable it used to have.
            //
            // It used to dim on Home ("a key that did nothing would read as
            // broken"). What that actually produced was a greyed key sitting in
            // a cluster of live ones, with the disabled chrome's own bottom
            // border showing under it - so the one key in the title row that
            // never moves or changes size still managed to look like a
            // different, broken control. Nothing about a Back arrow at the top
            // of a window wants that: the official player's arrow keeps its
            // full-weight glyph and simply has nowhere to go.
            //
            // So it stays enabled and same-sized, and ShellBackButton_Click
            // decides whether there is a destination. A click with nowhere to
            // go is a no-op rather than something the user can see.
            ShellBackButton.IsEnabled = true;

            // The playing state reads "Back to home" like the library does: the
            // library IS home while something plays. Only the return leg needs
            // its own word, because the button points both ways there.
            var label = state switch
            {
                ShellPage.LibraryWhilePlaying => AppContext.AppLang.ShellBackToPlaying,
                _ => AppContext.AppLang.ShellBackToLibrary,
            };

            ToolTipService.SetToolTip(ShellBackButton, label);
            AutomationProperties.SetName(ShellBackButton, label);
        }

        /// <summary>True when the back key has somewhere to go from here.</summary>
        private bool ShellCanGoBack()
        {
            var state = ActivePlayer?.ShellPageState ?? ShellPage.Library;
            if (state is ShellPage.Playing or ShellPage.LibraryWhilePlaying)
            {
                return true;
            }

            return !string.Equals(
                (ShellNav.SelectedItem as NavigationViewItem)?.Tag as string,
                "home",
                StringComparison.Ordinal);
        }

        /// <summary>
        /// The library's empty state owns the open affordance while it is up, so
        /// the shell's button steps aside. See the note in ApplyEmptyStateContent.
        /// </summary>
        private void ActivePlayer_EmptyStateVisibleChanged(bool empty)
        {
            if (_pageIsPlaying || _fullWindow)
            {
                return;
            }

            ShellOpenFileButton.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ActivePlayer_ShellStateChanged(ShellPage state)
        {
            UpdateShellBackButton();
            ApplyShellPageChrome(state);
        }

        /// <summary>
        /// Puts the shell's browsing chrome away while the video is the page.
        ///
        /// The playing page is the whole window: video, the title row's two
        /// keys, the status bar. Everything else on this window belongs to
        /// browsing - the navigation pane, the tab strip, the open-file button -
        /// and leaving any of it up is what produced the report that a video
        /// plays with the library's furniture still around it, and the one where
        /// the open-file button drew on top of a tab.
        ///
        /// IsPaneVisible rather than Visibility: collapsing the NavigationView
        /// would collapse the frame inside it, and the frame holds the video.
        /// The back key, the title and the status bar stay - they are how you
        /// get out and what you control it with.
        /// </summary>
        private void ApplyShellPageChrome(ShellPage state)
        {
            var playing = state == ShellPage.Playing;

            // _fullWindow is checked here as well as in ChangeFullWindow: the
            // two writers own different halves of the same two elements, and a
            // track ending while full-window was up used to bring the pane back
            // on top of a window that asked for no chrome at all.
            ShellNav.IsPaneVisible = !playing && !_fullWindow;
            ShellOpenFileButton.Visibility = playing || _fullWindow
                ? Visibility.Collapsed
                : Visibility.Visible;

            _pageIsPlaying = playing;
            UpdateShellPaneChrome();

            // The bar keeps its own row and its opaque surface in this state,
            // which is what the official player's windowed playback looks like:
            // measured off its own window, the top and bottom bars are opaque
            // #1A1A1A and never fade. The transparent shape is the full-window /
            // fullscreen one, where the bar floats over the picture with its
            // gradient and auto-hides - PlayerControl.SetOverlayMode, driven by
            // HandleFullWindowRequest. So both modes exist; which one you get is
            // decided by the window's shape, not by a second setting.
            PlayerBar.RefreshAdaptiveState();
        }

        private void ShellBackButton_Click(object sender, RoutedEventArgs e)
        {
            if (ActivePlayer is not { } page)
            {
                return;
            }

            // The key stays enabled everywhere (see UpdateShellBackButton), so
            // "nowhere to go" is decided here. Home is the only such place: the
            // two playing shapes always have the other leg to move to.
            if (page.ShellPageState is ShellPage.Playing or ShellPage.LibraryWhilePlaying)
            {
                page.ToggleLibraryFromShell();
                return;
            }

            if (!ShellCanGoBack())
            {
                return;
            }

            // Otherwise "back" means Home: select the Home item and let the
            // regular selection path do the rest.
            foreach (var item in ShellNav.MenuItems)
            {
                if (item is NavigationViewItem { Tag: "home" })
                {
                    ShellNav.SelectedItem = item;
                    return;
                }
            }
        }

        /// <summary>
        /// The shell's player control bar. <see cref="MpvPlayerPage"/> reaches
        /// it through this rather than owning its own, so the bar survives
        /// whatever the content area is showing.
        /// </summary>
        public PlayerControl PlayerBar => ShellPlayerBar;

        public async void Open()
        {
            AppContext.AppLogger.Debug("MainWindow.Open enter");
            IReadOnlyList<FileItem>? fileItems = null;
            try
            {
                var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
                fileItems = await ActivationService.Instance.ParseFileItemsAsync(activatedArgs);
                AppContext.AppLogger.Debug($"MainWindow.Open parsed {fileItems?.Count ?? 0} item(s)");
                // Inside the try: async void — a Navigate throw here would be
                // an unhandled exception and take the app down at launch.
                ShellFrame.Navigate(typeof(MpvPlayerPage), fileItems);
            }
            catch (System.Exception ex)
            {
                // A page that throws in its own constructor shows up here and
                // NOWHERE ELSE: Frame.Navigate catches it, the window stays up
                // with an empty frame, and the app looks like it launched with a
                // blank content area. That is exactly how a bad StaticResource in
                // MpvPlayerPage.xaml (a Double handed to ColumnDefinition.Width)
                // presented itself - no crash, no dialog, no log line.
                AppContext.AppLogger.Error(ex, "MainWindow.Open failed to navigate to MpvPlayerPage");
            }
        }

        public async void Refresh(AppActivationArguments activatedArgs)
        {
            AppContext.AppLogger.Debug($"MainWindow.Refresh enter, kind={activatedArgs.Kind}");
            try
            {
                var fileItems = await ActivationService.Instance.ParseFileItemsAsync(activatedArgs, includeProcessArgv: false);
                if (fileItems?.Count > 0)
                {
                    DispatcherQueue.RunAsync(() =>
                    {
                        if (ShellFrame?.Content is IParameterRefreshSupportView view)
                        {
                            view.OnRefresh(fileItems);
                        }
                    });
                }
            }
            catch (System.Exception ex)
            {
                AppContext.AppLogger.Error(ex);
            }
        }

        /// <summary>Rebuilds the player page's menu bar (after menu definition or language changes).</summary>
        public void RebuildPlayerMenuBar()
        {
            if (ShellFrame?.Content is MpvPlayerPage page)
            {
                page.RebuildMenuBar();
            }
        }

        /// <summary>
        /// Full window: the shell chrome gets out of the way so the video has
        /// the whole client area, and the player bar floats over the video
        /// instead of sitting in its own row beneath it.
        ///
        /// This is the shell's half of a state the page also reacts to - the
        /// page collapses its own menu row - so that between them the window
        /// shows nothing but video and the bar.
        ///
        /// The pane is hidden with IsPaneVisible, never with Visibility.
        /// Collapsing the NavigationView would collapse the frame inside it,
        /// which is the video.
        /// </summary>
        public void ChangeFullWindow(bool full)
        {
            _fullWindow = full;

            if (full)
            {
                TitleBarRow.Height = new GridLength(0);
                ShellBarRow.Height = new GridLength(0);
                Grid.SetRow(ShellPlayerBarSurface, 1);
                ShellNav.IsPaneVisible = false;
                ShellOpenFileButton.Visibility = Visibility.Collapsed;
                UpdateShellPaneChrome();

                // No surface of its own while it floats over the video: the
                // bar's gradient is what separates it from the picture. Both
                // surfaces are hidden rather than their brushes nulled, because
                // a null brush still leaves the element hit-testable, and these
                // span the whole video area in this row.
                SetShellBarSurfacesVisible(false);
            }
            else
            {
                // Restored from the same tokens the markup uses, not from Auto:
                // going back to Auto here would reintroduce the first-frame
                // height change every time full window is left.
                TitleBarRow.Height = MpvShellTitleRowLength;
                ShellBarRow.Height = MpvShellBarRowLength;
                Grid.SetRow(ShellPlayerBarSurface, 2);

                // Restoring the windowed shape does not mean restoring the
                // pane: leaving full-window while a video is the page lands back
                // on the playing page, which has no browsing chrome at all.
                ShellNav.IsPaneVisible = !_pageIsPlaying;
                UpdateShellPaneChrome();
                ShellOpenFileButton.Visibility = _pageIsPlaying ? Visibility.Collapsed : Visibility.Visible;

                SetShellBarSurfacesVisible(true);
            }
        }

        /// <summary>
        /// Whether the status bar paints a surface at all. Full window hides it,
        /// because there the bar floats over the picture and its own gradient is
        /// the only thing separating it from the video.
        /// </summary>
        private void SetShellBarSurfacesVisible(bool visible)
        {
            ShellBarSolid.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private string _lastMediaTitle = PackageHelper.AppName;

        public void UpdateTitle(string title)
        {
            if (!string.IsNullOrEmpty(title))
            {
                _lastMediaTitle = title;
            }

            var custom = AppContext.AppSetting.WindowTitle;
            var effective = string.IsNullOrWhiteSpace(custom) ? _lastMediaTitle : custom;
            ShellTitleText.Text = effective;
            AppWindow.Title = effective;
        }

        private SettingsWindow? _settingsWindow;
        public void OpenSettingWindow()
        {
            if (null == _settingsWindow)
            {
                _settingsWindow = new SettingsWindow();
                _settingsWindow.Closed += SettingsWindow_Closed;
            }

            var position = AppWindow.Position;
            var size = AppWindow.Size;
            var rect = new RectInt32(
                (int)(position.X + (size.Width * 0.1)),
                (int)(position.Y + (size.Height * 0.1)),
                (int)(size.Width * 0.8),
                (int)(size.Height * 0.8));
            _settingsWindow?.MoveAndResize(rect);
            _settingsWindow?.ShowWindow();
        }

        private void SettingsWindow_Closed(object sender, WindowEventArgs args)
        {
            if (_settingsWindow is not null)
            {
                _settingsWindow.Closed -= SettingsWindow_Closed;
            }
            _settingsWindow = null;
        }

        public void OpenMpvConfigWindow()
        {
            _windowsManager.Open("mpvconf", () => new MpvConfEditorWindow(), this, 0.8, 480, 320);
        }

        public void OpenMediaInfoWindow(string? path)
        {
            _windowsManager.Open("mediainfo_" + HashUtil.ComputeMd5(path ?? string.Empty), () => new MediaInfoWindow(path), this, 0.8, 480, 320);
        }

        public void OpenMenuEditorWindow(string filePath, MenuType type)
        {
            _windowsManager.Open("menueditor_" + HashUtil.ComputeMd5(filePath ?? string.Empty), () => new MenuEditorWindow(filePath ?? string.Empty, type), this, 0.8, 720, 480);
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            AppContext.SettingChanged -= MainWindow_SettingChanged;
            AppContext.LanguageChanged -= MainWindow_LanguageChanged;
            _settingsWindow?.Close();
            CleanupStyle();
            AppContext.AppSetting.Flush();
        }
    }
}
