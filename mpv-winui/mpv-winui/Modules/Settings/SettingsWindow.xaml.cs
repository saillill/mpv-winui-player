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
            presenter.PreferredMinimumWidth = 720;
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
        SearchBox.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
        SearchCompactButton.Visibility = narrow ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>The compact shape puts the pill back so the field can be typed in.
    /// It does not widen the window: this handler runs before the user has been
    /// given anything to type into, and a click on a magnifier is not a request
    /// to resize the window they just sized. The pane stays icon-only until the
    /// window is widened, which is the width the breakpoint is about.</summary>
    private void SearchCompactButton_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Visibility = Visibility.Visible;
        SearchCompactButton.Visibility = Visibility.Collapsed;
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
