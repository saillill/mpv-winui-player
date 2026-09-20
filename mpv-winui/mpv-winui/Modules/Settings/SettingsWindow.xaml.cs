using Microsoft.UI.Xaml;
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
        MpvConfWatcher.Reloaded -= SettingsWindow_ConfigReloaded;
        MpvConfWatcher.Stop();
        // The style manager is disposed by BaseWindow's own Closed handler.
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

    private void SettingsWindow_LanguageChanged()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            SettingsTitleText.Text = AppContext.AppLang.SettingsTitle;
            AppWindow.Title = AppContext.AppLang.SettingsTitle;
            // Language switches refresh the existing page in place, keeping
            // the selected category, search text and scroll offset.
            if (PageFrame.Content is SettingsPage page)
            {
                page.OnLanguageChanged();
            }
        });
    }
}
