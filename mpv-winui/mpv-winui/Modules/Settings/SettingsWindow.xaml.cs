using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using mpv_winui.Modules.Common.View;
using mpv_winui;
using System;

namespace mpv_winui.Modules.Settings;

public sealed partial class SettingsWindow : BaseWindow
{
    /// <summary>The currently open settings window (used to own folder/file pickers).</summary>
    public static SettingsWindow? Instance { get; private set; }

    public SettingsWindow()
    {
        Instance = this;
        InitializeComponent();
        SettingsTitleText.Text = AppContext.AppLang.SettingsTitle;

        Closed += SettingsWindow_Closed;
        AppContext.LanguageChanged += SettingsWindow_LanguageChanged;
        // Switching the search control's shape is driven by this window's own
        // width, which is the only width that decides it: the top bar lives in
        // this window, and the page's pane threshold is fed the same number
        // through MpvNarrowLayoutThreshold. Wired here rather than in XAML
        // because the swap is a choice between two elements, not a layout rule
        // an AdaptiveTrigger can express as a property setter on one of them.
        SizeChanged += SettingsWindow_SizeChanged;
        // The focus underline is owned here, not by the TextBox's own border:
        // see the note on SearchFocusBar in the XAML.
        SearchBox.GotFocus += (_, _) => SearchFocusBar.Visibility = Visibility.Visible;
        SearchBox.LostFocus += (_, _) => SearchFocusBar.Visibility = Visibility.Collapsed;
        // The window is the only UI that renders a settings row, so it is the
        // only thing that can act on a hand edit to mpv.conf. It subscribes on
        // construction and the watcher stops with it, because the reload writes
        // into settings the page owns and running it against a torn-down page
        // would refresh a tree nothing displays.
        MpvConfWatcher.Reloaded += SettingsWindow_ConfigReloaded;
        MpvConfWatcher.Start();

        AppWindow.Title = AppContext.AppLang.SettingsTitle;
        AppWindow.SetIcon("App.ico");
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;

        // The compact search button is a bare glyph, so its accessible name and
        // its tooltip are the only things that say what it does. Filled here and
        // from SettingsWindow_LanguageChanged, the same two places the pill's
        // own name is filled from.
        ApplySearchCompactLabels();

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            // The floor is set here because this is the only window whose
            // content is a two-column list. Each row is a label and a
            // one-line description on the left, a control on the right, with a
            // 24 px gap between the columns; below roughly 640 logical px the
            // text column is narrower than the longest option label and every
            // description ends in an ellipsis a few characters in. The list
            // then reads as noise rather than as truncated text.
            //
            // 720 was already the width; the height is what moves, because
            // 480 leaves room for about six rows -- fewer than one category
            // holds, so the wheel becomes the only way to see a section.
            // 720 x 560 shows a full screen of rows at the default UI font.
            //
            // The width floor is deliberately BELOW MpvNarrowLayoutThreshold
            // (720), which is the width at which the top bar runs out of room
            // for the search pill and the layout is supposed to collapse to the
            // icon rail. With the floor at 720 the two were the same number, so
            // the window could never be dragged past the breakpoint and the
            // narrow layout was unreachable -- the setting existed, the code
            // existed, and no width could ever trigger it. The floor has to sit
            // far enough below to give the breakpoint somewhere to happen.
            //
            // 560: the collapsed rail is about 48 DIP, which leaves ~500 for the
            // content column -- still a readable card width, and it matches the
            // height floor so the window cannot be made a letterbox slit.
            presenter.PreferredMinimumWidth = 560;
            presenter.PreferredMinimumHeight = 560;
        }

        // WindowStyleManager and the theme/backdrop plumbing live on BaseWindow;
        // this window only has to opt in.
        SetupStyle();
    }

    private void SettingsWindow_Closed(object sender, WindowEventArgs args)
    {
        if (ReferenceEquals(Instance, this))
        {
            Instance = null;
        }
        Closed -= SettingsWindow_Closed;
        AppContext.LanguageChanged -= SettingsWindow_LanguageChanged;
        SizeChanged -= SettingsWindow_SizeChanged;
        MpvConfWatcher.Reloaded -= SettingsWindow_ConfigReloaded;
        MpvConfWatcher.Stop();
        // The style manager is disposed by BaseWindow's own Closed handler.
    }

    /// <summary>Narrow shape of the top bar. Both this and the page's pane
    /// threshold read <c>MpvNarrowLayoutThreshold</c>, so the pane and the
    /// search control change at the same width instead of two numbers that look
    /// related.
    ///
    /// <paramref name="e"/> carries the window's *client* size in DIPs, which is
    /// what the top bar is laid out against, so it is measured directly rather
    /// than through AppWindow.Size, whose units are physical pixels.
    ///
    /// The collapsed pill keeps whatever the user typed. The text lives on the
    /// AutoSuggestBox, which is only hidden, and the page holds the query and the
    /// filtered list, so hiding the field is not the same as clearing it.</summary>
    private void SettingsWindow_SizeChanged(object sender, WindowSizeChangedEventArgs e)
    {
        var narrow = e.Size.Width < (double)Application.Current.Resources["MpvNarrowLayoutThreshold"];
        // Widening closes the dropped-down field: it is a narrow-shape affordance,
        // and leaving it open would push the whole top bar down in a window that
        // has room to show the pill in the bar itself.
        if (!narrow)
        {
            _narrowSearchOpen = false;
        }
        _narrow = narrow;
        ApplySearchShape();
    }

    /// <summary>True while the window is below the narrow-layout breakpoint.</summary>
    private bool _narrow;

    /// <summary>True while the narrow shape has its search field dropped open.</summary>
    private bool _narrowSearchOpen;

    /// <summary>Puts the one search control into whichever shape the current width
    /// calls for.
    ///
    /// There is a single AutoSuggestBox, so the narrow shape cannot be a second
    /// field: it is the same field moved to a second row of the top bar, spanning
    /// both columns, which is how Windows 11's settings window does it. The move
    /// is done with Grid.SetRow/SetColumn on the host rather than by reparenting,
    /// so the typed text, the focus and the page's query binding all survive it.
    ///
    /// The overlaid magnifier is tied to the pill's visibility here. It used to
    /// be left alone, so in the narrow shape the compact button and this glyph
    /// drew the same icon in the same place -- two magnifiers on top of each
    /// other, which is what the overlap was.</summary>
    private void ApplySearchShape()
    {
        var droppedDown = _narrow && _narrowSearchOpen;

        SearchBox.Visibility = _narrow && !droppedDown ? Visibility.Collapsed : Visibility.Visible;
        SearchGlyph.Visibility = SearchBox.Visibility;
        // Only while the pill is hidden. The compact button lives inside the same
        // host as the pill, so leaving it visible once the field has dropped open
        // put it directly on top of the pill's own magnifier: two icons in one
        // place, which is what the overlap was.
        SearchCompactButton.Visibility =
            _narrow && !droppedDown ? Visibility.Visible : Visibility.Collapsed;

        Grid.SetRow(SearchPillHost, droppedDown ? 1 : 0);
        Grid.SetColumn(SearchPillHost, droppedDown ? 0 : 2);
        Grid.SetColumnSpan(SearchPillHost, droppedDown ? 3 : 1);
        // The host shrinks to its content whenever the pill is hidden. It used to
        // stay at its full pill width and centred, so the compact button sat on
        // the host's left edge and that edge tracked the window: the magnifier
        // slid leftwards as the window narrowed instead of standing still beside
        // the pane button.
        var pillHidden = _narrow && !droppedDown;
        SearchPillHost.Width = droppedDown || pillHidden
            ? double.NaN
            : (double)Application.Current.Resources["MpvSearchPillWidth"];
        SearchPillHost.HorizontalAlignment = droppedDown
            ? HorizontalAlignment.Stretch
            : pillHidden
                ? HorizontalAlignment.Left
                : HorizontalAlignment.Center;
        // Dropped down it sits under the title row, and the two rows had no gap
        // between them: the field was flush against the title above it.
        SearchPillHost.Margin = droppedDown
            ? new Thickness(0, NumericGapAboveDroppedPill, 0, 0)
            : new Thickness(0);

        // Narrow, the title gives up its word and the bar keeps only the shapes
        // that do something: the pane's button and the magnifier. The word was
        // competing with the search field for a row that no longer has room for
        // both, which is the same trade the Windows 11 top bar makes.
        SettingsTitleText.Visibility = _narrow ? Visibility.Collapsed : Visibility.Visible;
        PaneToggleButton.Visibility = _narrow ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Opens the category pane over the content, the way the Windows 11
    /// settings pane comes out. The pane belongs to the page, so the window asks
    /// it rather than reaching into it.</summary>
    private void PaneToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (PageFrame.Content is SettingsPage page)
        {
            page.TogglePane();
        }
    }

    /// <summary>Gap between the title row and the dropped-open search field, in DIPs.</summary>
    private const double NumericGapAboveDroppedPill = 10;

    /// <summary>The compact shape's magnifier drops the pill open underneath the
    /// title, and takes it away again on a second tap. It does not widen the
    /// window: a click on a magnifier is not a request to resize the window the
    /// user just sized, and the pane stays icon-only until the window is widened,
    /// which is the width the breakpoint is about.</summary>
    private void SearchCompactButton_Click(object sender, RoutedEventArgs e)
    {
        // Open, never toggle. Toggling meant a second tap on the magnifier took
        // the field away, which reads as the search vanishing rather than as a
        // control being closed. Widening the window is what puts it away.
        _narrowSearchOpen = true;
        ApplySearchShape();
        SearchBox.Focus(FocusState.Programmatic);
    }

    /// <summary>Handles a hand edit to mpv.conf that the watcher has already
    /// read into the settings.
    ///
    /// Raised on a worker thread, so it marshals: touching the option tree or
    /// the list control from here would throw. The page is reached through the
    /// frame because the page is created by navigation, not held as a field.</summary>
    private void SettingsWindow_ConfigReloaded()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (PageFrame.Content is SettingsPage page)
            {
                page.OnExternalConfigChanged();
            }
        });
    }

    private void PageFrame_Loaded(object sender, RoutedEventArgs e)
    {
        // The search box lives in the window top bar; Frame can only build
        // pages by type, so the box travels as the navigation parameter and
        // the page picks it up in OnNavigatedTo.
        PageFrame.Navigate(typeof(SettingsPage), SearchBox);
    }

    // The search box is owned by the window (top bar); behaviour stays on the
    // page and the events are simply forwarded.
    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        => (PageFrame.Content as SettingsPage)?.SearchBox_TextChanged(sender, args);

    private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        => (PageFrame.Content as SettingsPage)?.SearchBox_SuggestionChosen(sender, args);

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        => (PageFrame.Content as SettingsPage)?.SearchBox_QuerySubmitted(sender, args);

    // MoveAndResize, UpdateTheme and UpdateBackdrop are BaseWindow's; this window
    // only adds the one refresh BaseWindow does not know about.
    public void UpdateUiFont()
    {
        _styleManager?.UpdateUiFont();
    }

    /// <summary>Accessible name and tooltip for the compact search button, which
    /// shows a glyph and no text of its own. Called on construction and on a
    /// language switch, so the two always agree with the current language.</summary>
    private void ApplySearchCompactLabels()
    {
        var label = AppContext.AppLang.Search;
        ToolTipService.SetToolTip(SearchCompactButton, label);
        AutomationProperties.SetName(SearchCompactButton, label);

        // Same reason: a glyph with no text has nowhere else to say what it does.
        // The settings title is the honest label here -- this button opens the
        // settings window's own navigation.
        var paneLabel = AppContext.AppLang.SettingsTitle;
        ToolTipService.SetToolTip(PaneToggleButton, paneLabel);
        AutomationProperties.SetName(PaneToggleButton, paneLabel);
    }

    private void SettingsWindow_LanguageChanged()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            SettingsTitleText.Text = AppContext.AppLang.SettingsTitle;
            AppWindow.Title = AppContext.AppLang.SettingsTitle;
            ApplySearchCompactLabels();
            // Language switches refresh the existing page in place, keeping
            // the selected category, search text and scroll offset.
            if (PageFrame.Content is SettingsPage page)
            {
                page.OnLanguageChanged();
            }
        });
    }
}
