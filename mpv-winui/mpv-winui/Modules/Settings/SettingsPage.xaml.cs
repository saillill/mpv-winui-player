using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.Settings.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using AppInstance = Microsoft.Windows.AppLifecycle.AppInstance;

namespace mpv_winui.Modules.Settings;

public sealed partial class SettingsPage : Page
{
    public List<Option> Settings { get; } = [];
    public List<string> Categories { get; } = [];

    public SettingsPage()
    {
        InitializeComponent();
        var options = BuildSettings();
        Settings.AddRange(options);
        Categories.AddRange(options.Select(o => o.Category).Distinct());
        CategoryList.ItemsSource = Categories;
        CategoryList.SelectedIndex = 0;
        UpdateOptions();
    }

    private void CategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateOptions();

    private void UpdateOptions()
    {
        var selected = CategoryList.SelectedItem as string;
        OptionsControl.OptionList = selected is null
            ? Settings
            : Settings.Where(o => o.Category == selected).ToList();
    }

    private List<Option> BuildSettings()
    {
        var general = AppContext.AppLang.SettingsCategoryGeneral;
        var playback = AppContext.AppLang.SettingsCategoryPlayback;
        var video = AppContext.AppLang.SettingsCategoryVideo;
        var audio = AppContext.AppLang.SettingsCategoryAudio;
        var subtitle = AppContext.AppLang.SettingsCategorySubtitle;
        var paths = AppContext.AppLang.SettingsCategoryPaths;

        return
        [
            // ===== General / 常规 =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.ThemeType),
                Label = AppContext.AppLang.AppSettingTheme,
                Category = general,
                Type = OptionType.StringList,
                Options = [AppSettings.ThemeType_Auto, AppSettings.ThemeType_Light, AppSettings.ThemeType_Dark],
                Getter = () => AppContext.AppSetting.ThemeType,
                Setter = v =>
                {
                    AppContext.AppSetting.ThemeType = (string)v;
                    UpdateTheme((string)v);
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.BackdropType),
                Label = AppContext.AppLang.Backdrop,
                Category = general,
                Type = OptionType.StringList,
                Options = [AppSettings.BackdropType_Acrylic, AppSettings.BackdropType_Mica],
                Getter = () => AppContext.AppSetting.BackdropType,
                Setter = v => AppContext.AppSetting.BackdropType = (string)v
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.EnableDebugLog),
                Label = AppContext.AppLang.DebugLog,
                Category = general,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.EnableDebugLog,
                Setter = v => AppContext.AppSetting.EnableDebugLog = (bool)v!
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CurrentLanguage),
                Label = AppContext.AppLang.SettingLanguages,
                Category = general,
                RequiresRestart = true,
                Type = OptionType.StringList,
                Options = AppContext.AvailableLanguages(),
                Getter = () =>
                {
                    var lang = AppContext.AppSetting.CurrentLanguage;
                    return string.IsNullOrEmpty(lang) ? "en-US" : lang;
                },
                Setter = v =>
                {
                    var newLang = (string)v!;
                    var current = AppContext.AppSetting.CurrentLanguage;
                    if (string.IsNullOrEmpty(current)) current = "en-US";
                    if (current == newLang) return; // 控件初始化回填不视为用户改动
                    AppContext.AppSetting.CurrentLanguage = newLang;
                    PromptRestartIfNeeded(AppContext.AppLang.SettingLanguages);
                }
            },

            // ===== Playback / 播放 =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.Hwdec),
                Label = AppContext.AppLang.SettingsHwdec,
                Category = playback,
                Description = "auto / no / d3d11va / nvdec / dxva2",
                Type = OptionType.StringList,
                Options = ["auto", "no", "d3d11va", "nvdec", "dxva2"],
                Getter = () => AppContext.AppSetting.Hwdec,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.Hwdec), AppContext.AppSetting.Hwdec = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.VolumeMax),
                Label = AppContext.AppLang.SettingsVolumeMax,
                Category = playback,
                Type = OptionType.Integer,
                Min = 100,
                Max = 300,
                Step = 10,
                Getter = () => (double)AppContext.AppSetting.VolumeMax,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.VolumeMax), AppContext.AppSetting.VolumeMax = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.KeepOpen),
                Label = AppContext.AppLang.SettingsKeepOpen,
                Category = playback,
                Type = OptionType.StringList,
                Options = ["yes", "no", "always"],
                Getter = () => AppContext.AppSetting.KeepOpen,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.KeepOpen), AppContext.AppSetting.KeepOpen = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.LoopFile),
                Label = AppContext.AppLang.SettingsLoopFile,
                Category = playback,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.LoopFile,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.LoopFile), AppContext.AppSetting.LoopFile = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.Speed),
                Label = AppContext.AppLang.SettingsSpeed,
                Category = playback,
                Type = OptionType.Double,
                Min = 0.25,
                Max = 4,
                Step = 0.1,
                Getter = () => AppContext.AppSetting.Speed,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.Speed), AppContext.AppSetting.Speed = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.EnableVideoPreview),
                Label = AppContext.AppLang.SettingsVideoPreview,
                Category = playback,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.EnableVideoPreview,
                Setter = v => AppContext.AppSetting.EnableVideoPreview = (bool)v!
            },

            // ===== Video / 视频 =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.Deinterlace),
                Label = AppContext.AppLang.SettingsDeinterlace,
                Category = video,
                Type = OptionType.StringList,
                Options = ["auto", "yes", "no"],
                Getter = () => AppContext.AppSetting.Deinterlace,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.Deinterlace), AppContext.AppSetting.Deinterlace = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AspectRatio),
                Label = AppContext.AppLang.SettingsAspect,
                Category = video,
                Type = OptionType.StringList,
                Options = ["auto", "16:9", "4:3", "2.35:1", "1.85:1"],
                Getter = () => AppContext.AppSetting.AspectRatio,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AspectRatio), AppContext.AppSetting.AspectRatio = (string)v!)
            },

            // ===== Audio / 音频 =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioLanguage),
                Label = AppContext.AppLang.SettingsAudioLanguage,
                Category = audio,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.AudioLanguage,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioLanguage), AppContext.AppSetting.AudioLanguage = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioDevice),
                Label = AppContext.AppLang.SettingsAudioDevice,
                Category = audio,
                Type = OptionType.String,
                Getter = () => AppContext.AppSetting.AudioDevice,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioDevice), AppContext.AppSetting.AudioDevice = (string)v!)
            },

            // ===== Subtitle / 字幕 =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubFontSize),
                Label = AppContext.AppLang.SettingsSubFontSize,
                Category = subtitle,
                Type = OptionType.Integer,
                Min = 10,
                Max = 120,
                Step = 2,
                Getter = () => (double)AppContext.AppSetting.SubFontSize,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubFontSize), AppContext.AppSetting.SubFontSize = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubDelay),
                Label = AppContext.AppLang.SettingsSubDelay,
                Category = subtitle,
                Type = OptionType.Double,
                Min = -10,
                Max = 10,
                Step = 0.1,
                Getter = () => AppContext.AppSetting.SubDelay,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubDelay), AppContext.AppSetting.SubDelay = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubPos),
                Label = AppContext.AppLang.SettingsSubPos,
                Category = subtitle,
                Type = OptionType.Integer,
                Min = 0,
                Max = 100,
                Step = 1,
                Getter = () => (double)AppContext.AppSetting.SubPos,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubPos), AppContext.AppSetting.SubPos = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubtitleLanguage),
                Label = AppContext.AppLang.SettingsSubtitleLanguage,
                Category = subtitle,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.SubtitleLanguage,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubtitleLanguage), AppContext.AppSetting.SubtitleLanguage = (string)v!)
            },

            // ===== Paths / 路径 =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.ScreenshotDirectory),
                Label = AppContext.AppLang.SettingsScreenshotDirectory,
                Category = paths,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.ScreenshotDirectory,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ScreenshotDirectory), AppContext.AppSetting.ScreenshotDirectory = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ScreenshotTemplate),
                Label = AppContext.AppLang.SettingsScreenshotTemplate,
                Category = paths,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.ScreenshotTemplate,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ScreenshotTemplate), AppContext.AppSetting.ScreenshotTemplate = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CacheDir),
                Label = AppContext.AppLang.SettingsCacheDir,
                Category = paths,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.CacheDir,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.CacheDir), AppContext.AppSetting.CacheDir = (string)v!)
            },
        ];
    }

    private static void ApplyMpv(string key, object value)
    {
        if (MpvSettings.ToCommand(key, value) is { } cmd)
        {
            AppContext.SendMpvCommand(cmd);
        }
    }

    private async void PromptRestartIfNeeded(string settingLabel)
    {
        var dialog = new ContentDialog
        {
            Title = AppContext.AppLang.RestartRequiredTitle,
            Content = string.Format(AppContext.AppLang.RestartRequiredMessage, settingLabel),
            PrimaryButtonText = AppContext.AppLang.RestartNow,
            CloseButtonText = AppContext.AppLang.RestartLater,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (App.Window is MainWindow mainWindow)
            {
                mainWindow.SaveWindowPositionAndSize();
            }
            AppInstance.Restart("Reset");
        }
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
