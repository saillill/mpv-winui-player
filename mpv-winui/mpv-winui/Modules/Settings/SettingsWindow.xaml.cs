using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using mpv_winui.Modules.Common.View;
using mpv_winui;
using System;
using Windows.Graphics;

namespace mpv_winui.Modules.Settings;

public sealed partial class SettingsWindow : Window
{
    /// <summary>The currently open settings window (used to own folder/file pickers).</summary>
    public static SettingsWindow? Instance { get; private set; }

    private WindowStyleManager? _styleManager;

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

        _styleManager = new WindowStyleManager(this);
        _styleManager?.Setup();
    }

    private void SettingsWindow_Closed(object sender, WindowEventArgs args)
    {
        if (ReferenceEquals(Instance, this))
        {
            Instance = null;
        }
        Closed -= SettingsWindow_Closed;
        AppContext.LanguageChanged -= SettingsWindow_LanguageChanged;
        _styleManager?.Dispose();
        _styleManager = null;
    }

    private void PageFrame_Loaded(object sender, RoutedEventArgs e)
    {
        PageFrame.Navigate(typeof(SettingsPage));
    }

    public void MoveAndResize(RectInt32 rect)
    {
        AppWindow?.MoveAndResize(rect);
    }

    public void UpdateCurrentTheme()
    {
        _styleManager?.UpdateTheme(_styleManager.GetThemeType());
    }

    public void UpdateBackdrop()
    {
        _styleManager?.UpdateBackdrop();
    }

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
