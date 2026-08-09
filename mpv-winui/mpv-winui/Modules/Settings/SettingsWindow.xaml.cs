using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        AppWindow.Closing += AppWindow_Closing;
        AppContext.LanguageChanged += SettingsWindow_LanguageChanged;

        AppWindow.Title = "Settings";
        AppWindow.SetIcon("App.ico");
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;

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
        AppWindow.Closing -= AppWindow_Closing;
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

    private async void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (PageFrame.Content is not SettingsPage page || !page.IsDirty)
        {
            return;
        }

        args.Cancel = true;
        var dialog = new ContentDialog
        {
            Title = AppContext.AppLang.SettingsUnsaved,
            Content = AppContext.AppLang.SettingsUnsavedConfirm,
            XamlRoot = RootGrid.XamlRoot,
            PrimaryButtonText = AppContext.AppLang.Save,
            SecondaryButtonText = AppContext.AppLang.Discard,
            CloseButtonText = AppContext.AppLang.Cancel,
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            page.Save();
            Close();
        }
        else if (result == ContentDialogResult.Secondary)
        {
            Close();
        }
    }

    private void SettingsWindow_LanguageChanged()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            SettingsTitleText.Text = AppContext.AppLang.SettingsTitle;
            PageFrame.Navigate(typeof(SettingsPage));
        });
    }
}
