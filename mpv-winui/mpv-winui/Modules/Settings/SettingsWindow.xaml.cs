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

        AppWindow.Title = AppContext.AppLang.SettingsTitle;
        AppWindow.SetIcon("App.ico");
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 720;
            presenter.PreferredMinimumHeight = 480;
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
        // The style manager is disposed by BaseWindow's own Closed handler.
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
