using Microsoft.UI.Xaml.Controls;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.Settings.Controls;
using System.Collections.Generic;

namespace mpv_winui.Modules.Settings;

public sealed partial class SettingsPage : Page
{
    public List<Option> Settings { get; } = [];

    public SettingsPage()
    {
        InitializeComponent();
        Settings.AddRange(BuildSettings());
    }

    private List<Option> BuildSettings()
    {
        return
        [
           new Option
            {
                Key = nameof(AppContext.AppSetting.ThemeType),
                Label = AppContext.AppLang.AppSettingTheme,
                Type = OptionType.StringList,
                Options = [AppSettings.ThemeType_Auto, AppSettings.ThemeType_Light, AppSettings.ThemeType_Dark],
                Getter = () => AppContext.AppSetting.ThemeType,
                Setter = v =>{
                    AppContext.AppSetting.ThemeType = (string)v;
                    UpdateTheme((string)v);
                }
            },

            new Option
            {
                Key =  nameof(AppContext.AppSetting.BackdropType),
                Label = AppContext.AppLang.Backdrop,
                Type = OptionType.StringList,
                Options = [AppSettings.BackdropType_Acrylic, AppSettings.BackdropType_Mica],
                Getter = () => AppContext.AppSetting.BackdropType,
                Setter = v => AppContext.AppSetting.BackdropType = (string)v
            },

            new Option
            {
                Key =  nameof(AppContext.AppSetting.EnableDebugLog),
                Label = AppContext.AppLang.DebugLog,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.EnableDebugLog,
                Setter = v => AppContext.AppSetting.EnableDebugLog = (bool)v!
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CurrentLanguage),
                Label = AppContext.AppLang.SettingLanguages,
                Type = OptionType.StringList,
                Options = ["en-US", "zh-CN"],
                Getter = () =>
                {
                    var lang = AppContext.AppSetting.CurrentLanguage;
                    return string.IsNullOrEmpty(lang) ? "en-US" : lang;
                },
                Setter = v => AppContext.AppSetting.CurrentLanguage = (string)v!
            },
        ];
    }

    private void UpdateTheme(string theme)
    {
        DispatcherQueue.RunAsync(() =>
        {
            if (App.Window is MainWindow mainWindow)
            {
                mainWindow.UpdateCurrentTheme();
            }
        });
    }
}
