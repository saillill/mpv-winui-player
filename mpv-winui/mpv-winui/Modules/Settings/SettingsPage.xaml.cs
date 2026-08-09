using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.FileSystem;
using mpv_winui.Modules.Language;
using mpv_winui.Modules.Settings.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace mpv_winui.Modules.Settings;

public sealed partial class SettingsPage : Page
{
    public List<Option> Settings { get; } = [];
    public List<string> Categories { get; } = [];
    public List<string> CategoryOrder { get; } = [];

    public SettingsPage()
    {
        InitializeComponent();
        var options = BuildSettings();
        Settings.AddRange(options);
        Categories.AddRange(CategoryOrder.Where(c => Settings.Any(o => o.Category == c)));
        CategoryList.ItemsSource = Categories;
        CategoryList.SelectedIndex = 0;
        SaveButton.Content = AppContext.AppLang.Save;
        ResetButton.Content = AppContext.AppLang.Reset;
        UpdateOptions();
        RefreshWarningsAndEnabled();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        MpvSettings.ApplyAll(cmd => AppContext.SendMpvCommand(cmd));
        if (App.Window is MainWindow mainWindow)
        {
            mainWindow.UpdateCurrentTheme();
        }
        SaveStatusText.Text = AppContext.AppLang.SettingsSaved;
        _ = ClearSaveStatusAsync();
    }

    private async System.Threading.Tasks.Task ClearSaveStatusAsync()
    {
        await System.Threading.Tasks.Task.Delay(2000);
        DispatcherQueue.TryEnqueue(() => SaveStatusText.Text = string.Empty);
    }

    private async void OnResetClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = AppContext.AppLang.Reset,
            Content = AppContext.AppLang.SettingsResetConfirm,
            XamlRoot = XamlRoot,
            PrimaryButtonText = AppContext.AppLang.Reset,
            CloseButtonText = AppContext.AppLang.Cancel,
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        AppContext.AppSetting.ResetAll();
        if (App.Window is MainWindow mainWindow)
        {
            mainWindow.UpdateCurrentTheme();
            AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.BackdropType), AppContext.AppSetting.BackdropType);
        }
        Frame?.Navigate(typeof(SettingsPage));
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
        var program = AppContext.AppLang.SettingsCategoryProgram;
        var playback = AppContext.AppLang.SettingsCategoryPlayback;
        var trackSelection = AppContext.AppLang.SettingsCategoryTrackSelection;
        var watchLater = AppContext.AppLang.SettingsCategoryWatchLater;
        var video = AppContext.AppLang.SettingsCategoryVideo;
        var audio = AppContext.AppLang.SettingsCategoryAudio;
        var subtitles = AppContext.AppLang.SettingsCategorySubtitles;
        var window = AppContext.AppLang.SettingsCategoryWindow;
        var demuxer = AppContext.AppLang.SettingsCategoryDemuxer;
        var cache = AppContext.AppLang.SettingsCategoryCache;
        var network = AppContext.AppLang.SettingsCategoryNetwork;
        var input = AppContext.AppLang.SettingsCategoryInput;
        var shortcuts = AppContext.AppLang.SettingsCategoryShortcuts;
        var osd = AppContext.AppLang.SettingsCategoryOsd;
        var screenshot = AppContext.AppLang.SettingsCategoryScreenshot;
        var testing = AppContext.AppLang.SettingsCategoryTesting;
        var gpuRenderer = AppContext.AppLang.SettingsCategoryGpuRenderer;
        var videoSync = AppContext.AppLang.SettingsCategoryVideoSync;
        var sProgramInterface = AppContext.AppLang.SectionProgramInterface;
        var sProgramLanguageLog = AppContext.AppLang.SectionProgramLanguageLog;
        var sProgramNetwork = AppContext.AppLang.SectionProgramNetwork;
        var sProgramTesting = AppContext.AppLang.SectionProgramTesting;
        var sPlayback = AppContext.AppLang.SectionPlayback;
        var sPlaybackSeeking = AppContext.AppLang.SectionPlaybackSeeking;
        var sPlaybackSeekPreview = AppContext.AppLang.SectionPlaybackSeekPreview;
        var sTrackLanguage = AppContext.AppLang.SectionTrackLanguage;
        var sTrackFallback = AppContext.AppLang.SectionTrackFallback;
        var sWatchLaterResume = AppContext.AppLang.SectionWatchLaterResume;
        var sWatchLaterStorage = AppContext.AppLang.SectionWatchLaterStorage;
        var sVideoDecode = AppContext.AppLang.SectionVideoDecode;
        var sVideoImage = AppContext.AppLang.SectionVideoImage;
        var sVideoHdr = AppContext.AppLang.SectionVideoHdr;
        var sVideoFilters = AppContext.AppLang.SectionVideoFilters;
        var sVideoUpscaling = AppContext.AppLang.SectionVideoUpscaling;
        var sAudioOutput = AppContext.AppLang.SectionAudioOutput;
        var sAudioVolume = AppContext.AppLang.SectionAudioVolume;
        var sAudioExternal = AppContext.AppLang.SectionAudioExternal;
        var sAudioCoverArt = AppContext.AppLang.SectionAudioCoverArt;
        var sSubtitleText = AppContext.AppLang.SectionSubtitleText;
        var sSubtitleAss = AppContext.AppLang.SectionSubtitleAss;
        var sSubtitleImage = AppContext.AppLang.SectionSubtitleImage;
        var sWindow = AppContext.AppLang.SectionWindow;
        var sDemuxerPlaylist = AppContext.AppLang.SectionDemuxerPlaylist;
        var sDemuxerBuffering = AppContext.AppLang.SectionDemuxerBuffering;
        var sCache = AppContext.AppLang.SectionCache;
        var sInput = AppContext.AppLang.SectionInput;
        var sOsd = AppContext.AppLang.SectionOsd;
        var sOsdMetadata = AppContext.AppLang.SectionOsdMetadata;
        var sScreenshotLocation = AppContext.AppLang.SectionScreenshotLocation;
        var sScreenshotQuality = AppContext.AppLang.SectionScreenshotQuality;
        var sGpuScaling = AppContext.AppLang.SectionGpuScaling;
        var sGpuColor = AppContext.AppLang.SectionGpuColor;
        var sGpuInterpolation = AppContext.AppLang.SectionGpuInterpolation;
        var sGpuBackground = AppContext.AppLang.SectionGpuBackground;
        var sGpuD3d11 = AppContext.AppLang.SectionGpuD3d11;
        var sGpuShaders = AppContext.AppLang.SectionGpuShaders;
        var sVideoSync = AppContext.AppLang.SectionVideoSync;
        var lang = AppContext.AppLang;

        var options = new List<Option>
        {
            // ===== Program Behavior =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.ThemeType),
                Label = lang.AppSettingTheme,
                Category = program,
                Description = lang.SettingsHelpTheme,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice(AppSettings.ThemeType_Auto, lang.ThemeAuto),
                    new OptionChoice(AppSettings.ThemeType_Light, lang.ThemeLightName),
                    new OptionChoice(AppSettings.ThemeType_Dark, lang.ThemeDarkName),
                ],
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
                Label = lang.Backdrop,
                Category = program,
                Description = lang.SettingsHelpBackdrop,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice(AppSettings.BackdropType_Acrylic, lang.OptionValueBackdropAcrylic),
                    new OptionChoice(AppSettings.BackdropType_Mica, lang.OptionValueBackdropMica),
                ],
                Getter = () => AppContext.AppSetting.BackdropType,
                Setter = v =>
                {
                    AppContext.AppSetting.BackdropType = (string)v;
                    AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.BackdropType), v);
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ThemeAccentColor),
                Label = lang.SettingsThemeAccentColor,
                Category = program,
                Description = lang.SettingsHelpThemeAccentColor,
                Type = OptionType.Color,
                Getter = () => AppContext.AppSetting.ThemeAccentColor,
                Setter = v =>
                {
                    AppContext.AppSetting.ThemeAccentColor = (string)v!;
                    AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.ThemeAccentColor), v);
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ThemeOpacity),
                Label = lang.SettingsThemeOpacity,
                Category = program,
                Description = lang.SettingsHelpThemeOpacity,
                Type = OptionType.Integer,
                Min = 0,
                Max = 100,
                Step = 5,
                Getter = () => (double)AppContext.AppSetting.ThemeOpacity,
                Setter = v =>
                {
                    AppContext.AppSetting.ThemeOpacity = Convert.ToInt32(v);
                    AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.ThemeOpacity), v);
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ThemeLuminosity),
                Label = lang.SettingsThemeLuminosity,
                Category = program,
                Description = lang.SettingsHelpThemeLuminosity,
                Type = OptionType.Integer,
                Min = 0,
                Max = 100,
                Step = 5,
                Getter = () => (double)AppContext.AppSetting.ThemeLuminosity,
                Setter = v =>
                {
                    AppContext.AppSetting.ThemeLuminosity = Convert.ToInt32(v);
                    AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.ThemeLuminosity), v);
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.UiFont),
                Label = lang.SettingsUiFont,
                Category = program,
                Type = OptionType.StringList,
                Choices = SubtitleFontChoices(lang),
                Getter = () => AppContext.AppSetting.UiFont,
                Setter = v =>
                {
                    AppContext.AppSetting.UiFont = (string)v!;
                    AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.UiFont), v);
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.TestMpvCommandLog),
                Label = lang.SettingsTestMpvCommandLog,
                Category = program,
                Section = sProgramTesting,
                Description = lang.SettingsHelpTestMpvCommandLog,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.TestMpvCommandLog,
                Setter = v => AppContext.AppSetting.TestMpvCommandLog = (bool)v!
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.TestOsdMessage),
                Label = lang.SettingsTestOsdMessage,
                Category = program,
                Section = sProgramTesting,
                Description = lang.SettingsHelpTestOsdMessage,
                Type = OptionType.Boolean,
                Getter = () => false,
                Setter = _ =>
                {
                    AppContext.SendMpvCommand("show-text \"mpv-winui OSD test\"");
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.TestSignal),
                Label = lang.SettingsTestSignal,
                Category = program,
                Section = sProgramTesting,
                Type = OptionType.StringList,
                AllowCustom = false,
                Choices =
                [
                    new OptionChoice("off", lang.OptionValueTestSignalOff),
                    new OptionChoice("testsrc2", lang.OptionValueTestSignalVideo),
                    new OptionChoice("sine", lang.OptionValueTestSignalAudio),
                ],
                Getter = () => AppContext.AppSetting.TestSignal,
                Setter = v =>
                {
                    AppContext.AppSetting.TestSignal = (string)v!;
                    var cmd = (string)v! switch
                    {
                        "testsrc2" => "loadfile \"lavfi://testsrc2=size=1280x720:rate=60\"",
                        "sine" => "loadfile \"lavfi://sine=frequency=1000:duration=60\"",
                        _ => null,
                    };
                    if (cmd is not null)
                    {
                        AppContext.SendMpvCommand(cmd);
                    }
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.EnableDebugLog),
                Label = lang.DebugLog,
                Category = program,
                Description = lang.SettingsHelpDebugLog,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.EnableDebugLog,
                Setter = v =>
                {
                    AppContext.AppSetting.EnableDebugLog = (bool)v!;
                    AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.EnableDebugLog), v);
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AlwaysOnTop),
                Label = lang.SettingsAlwaysOnTop,
                Category = program,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.AlwaysOnTop,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AlwaysOnTop), AppContext.AppSetting.AlwaysOnTop = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.InputIme),
                Label = lang.SettingsInputIme,
                Category = program,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.InputIme,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.InputIme), AppContext.AppSetting.InputIme = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CurrentLanguage),
                Label = lang.SettingLanguages,
                Category = program,
                Description = lang.SettingsHelpLanguage,
                Type = OptionType.StringList,
                Choices = AppContext.AvailableLanguages()
                    .Select(code => new OptionChoice(code, AppLang.NativeLanguageName(code)))
                    .ToList(),
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
                    AppContext.SwitchLanguage(newLang);
                }
            },

            // ===== Playback Control =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.Hwdec),
                Label = lang.SettingsHwdec,
                Category = playback,
                Description = lang.SettingsHelpHwdec,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("auto", lang.OptionValueAuto),
                    new OptionChoice("no", lang.OptionValueNo),
                    new OptionChoice("d3d11va", "D3D11 VA"),
                    new OptionChoice("nvdec", "NVDEC"),
                    new OptionChoice("dxva2", "DXVA2"),
                ],
                Getter = () => AppContext.AppSetting.Hwdec,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.Hwdec), AppContext.AppSetting.Hwdec = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.HwdecCodecs),
                Label = lang.SettingsHwdecCodecs,
                Category = playback,
                Description = lang.SettingsHelpHwdecCodecs,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.HwdecCodecs,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.HwdecCodecs), AppContext.AppSetting.HwdecCodecs = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.VolumeMax),
                Label = lang.SettingsVolumeMax,
                Category = playback,
                Description = lang.SettingsHelpVolumeMax,
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
                Label = lang.SettingsKeepOpen,
                Category = playback,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("yes", lang.OptionValueKeepOpenYes),
                    new OptionChoice("no", lang.OptionValueKeepOpenNo),
                    new OptionChoice("always", lang.OptionValueKeepOpenAlways),
                ],
                Getter = () => AppContext.AppSetting.KeepOpen,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.KeepOpen), AppContext.AppSetting.KeepOpen = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.LoopFile),
                Label = lang.SettingsLoopFile,
                Category = playback,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.LoopFile,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.LoopFile), AppContext.AppSetting.LoopFile = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.LoopPlaylist),
                Label = lang.SettingsLoopPlaylist,
                Category = playback,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("no", lang.OptionValueLoopPlaylistNo),
                    new OptionChoice("yes", lang.OptionValueLoopPlaylistYes),
                    new OptionChoice("inf", lang.OptionValueLoopPlaylistInf),
                    new OptionChoice("force", lang.OptionValueLoopPlaylistForce),
                ],
                Getter = () => AppContext.AppSetting.LoopPlaylist,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.LoopPlaylist), AppContext.AppSetting.LoopPlaylist = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.Volume),
                Label = lang.SettingsVolume,
                Category = playback,
                Type = OptionType.Integer,
                Min = 0,
                Max = 130,
                Step = 5,
                Getter = () => (double)AppContext.AppSetting.Volume,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.Volume), AppContext.AppSetting.Volume = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SavePositionOnQuit),
                Label = lang.SettingsSavePositionOnQuit,
                Category = playback,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SavePositionOnQuit,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SavePositionOnQuit), AppContext.AppSetting.SavePositionOnQuit = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.Speed),
                Label = lang.SettingsSpeed,
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
                Key = nameof(AppContext.AppSetting.Interpolation),
                Label = lang.SettingsInterpolation,
                Category = playback,
                Description = lang.SettingsHelpInterpolation,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.Interpolation,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.Interpolation), AppContext.AppSetting.Interpolation = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ResumePlayback),
                Label = lang.SettingsResumePlayback,
                Category = playback,
                Description = lang.SettingsHelpResumePlayback,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.ResumePlayback,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ResumePlayback), AppContext.AppSetting.ResumePlayback = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CacheSecs),
                Label = lang.SettingsCacheSecs,
                Category = playback,
                Description = lang.SettingsHelpCacheSecs,
                Type = OptionType.Integer,
                Min = 0,
                Max = 600,
                Step = 10,
                Getter = () => (double)AppContext.AppSetting.CacheSecs,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.CacheSecs), AppContext.AppSetting.CacheSecs = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CacheEnabled),
                Label = lang.SettingsCacheEnabled,
                Category = playback,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("no", lang.OptionValueNo),
                    new OptionChoice("auto", lang.OptionValueAuto),
                    new OptionChoice("yes", lang.OptionValueYes),
                ],
                Getter = () => AppContext.AppSetting.CacheEnabled,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.CacheEnabled), AppContext.AppSetting.CacheEnabled = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.DemuxerReadahead),
                Label = lang.SettingsDemuxerReadahead,
                Category = playback,
                Description = lang.SettingsHelpDemuxerReadahead,
                Type = OptionType.Double,
                Min = 0,
                Max = 30,
                Step = 0.5,
                Getter = () => AppContext.AppSetting.DemuxerReadahead,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.DemuxerReadahead), AppContext.AppSetting.DemuxerReadahead = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.Ytdl),
                Label = lang.SettingsYtdl,
                Category = playback,
                Description = lang.SettingsHelpYtdl,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.Ytdl,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.Ytdl), AppContext.AppSetting.Ytdl = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.YtdlRawOptionsAppend),
                Label = lang.SettingsYtdlRawOptionsAppend,
                Category = playback,
                Description = lang.SettingsHelpYtdlRawOptionsAppend,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.YtdlRawOptionsAppend,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.YtdlRawOptionsAppend), AppContext.AppSetting.YtdlRawOptionsAppend = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AutoCreatePlaylist),
                Label = lang.SettingsAutoCreatePlaylist,
                Category = playback,
                Description = lang.SettingsHelpAutoCreatePlaylist,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("no", lang.OptionValueNo),
                    new OptionChoice("filter", lang.OptionValueAutoPlaylistFilter),
                    new OptionChoice("same", lang.OptionValueAutoPlaylistSame),
                ],
                Getter = () => AppContext.AppSetting.AutoCreatePlaylist,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AutoCreatePlaylist), AppContext.AppSetting.AutoCreatePlaylist = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.DirectoryMode),
                Label = lang.SettingsDirectoryMode,
                Category = playback,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("ignore", lang.OptionValueDirModeIgnore),
                    new OptionChoice("lazy", lang.OptionValueDirModeLazy),
                    new OptionChoice("recursive", lang.OptionValueDirModeRecursive),
                    new OptionChoice("auto", lang.OptionValueAuto),
                ],
                Getter = () => AppContext.AppSetting.DirectoryMode,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.DirectoryMode), AppContext.AppSetting.DirectoryMode = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.DirectoryFilterTypes),
                Label = lang.SettingsDirectoryFilterTypes,
                Category = playback,
                Description = lang.SettingsHelpDirectoryFilterTypes,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.DirectoryFilterTypes,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.DirectoryFilterTypes), AppContext.AppSetting.DirectoryFilterTypes = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.VideoExts),
                Label = lang.SettingsVideoExts,
                Category = playback,
                Description = lang.SettingsHelpVideoExts,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.VideoExts,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.VideoExts), AppContext.AppSetting.VideoExts = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ImageExts),
                Label = lang.SettingsImageExts,
                Category = playback,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.ImageExts,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ImageExts), AppContext.AppSetting.ImageExts = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioExts),
                Label = lang.SettingsAudioExts,
                Category = playback,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.AudioExts,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioExts), AppContext.AppSetting.AudioExts = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.WatchLaterOptions),
                Label = lang.SettingsWatchLaterOptions,
                Category = playback,
                Description = lang.SettingsHelpWatchLaterOptions,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.WatchLaterOptions,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.WatchLaterOptions), AppContext.AppSetting.WatchLaterOptions = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.EnableVideoPreview),
                Label = lang.SettingsVideoPreview,
                Category = playback,
                Description = lang.SettingsHelpVideoPreview,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.EnableVideoPreview,
                Setter = v =>
                {
                    AppContext.AppSetting.EnableVideoPreview = (bool)v!;
                    AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.EnableVideoPreview), v);
                }
            },

            // ===== Video / 视频 =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.Deinterlace),
                Label = lang.SettingsDeinterlace,
                Category = video,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("auto", lang.OptionValueDeinterlaceAuto),
                    new OptionChoice("yes", lang.OptionValueDeinterlaceYes),
                    new OptionChoice("no", lang.OptionValueDeinterlaceNo),
                ],
                Getter = () => AppContext.AppSetting.Deinterlace,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.Deinterlace), AppContext.AppSetting.Deinterlace = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AspectRatio),
                Label = lang.SettingsAspect,
                Category = video,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("auto", lang.OptionValueAspectAuto),
                    new OptionChoice("16:9", "16:9"),
                    new OptionChoice("4:3", "4:3"),
                    new OptionChoice("2.35:1", "2.35:1"),
                    new OptionChoice("1.85:1", "1.85:1"),
                ],
                Getter = () => AppContext.AppSetting.AspectRatio,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AspectRatio), AppContext.AppSetting.AspectRatio = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.VideoSync),
                Label = lang.SettingsVideoSync,
                Category = video,
                Description = lang.SettingsHelpVideoSync,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("audio", lang.OptionValueVideoSyncAudio),
                    new OptionChoice("display-resample", lang.OptionValueVideoSyncDisplayResample),
                    new OptionChoice("display-resample-vdrop", lang.OptionValueVideoSyncDisplayResampleVdrop),
                    new OptionChoice("display-adrop", lang.OptionValueVideoSyncDisplayAdrop),
                    new OptionChoice("cfr", lang.OptionValueVideoSyncCfr),
                ],
                Getter = () => AppContext.AppSetting.VideoSync,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.VideoSync), AppContext.AppSetting.VideoSync = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.VideoSyncMaxVideoChange),
                Label = lang.SettingsVideoSyncMaxVideoChange,
                Category = video,
                Description = lang.SettingsHelpVideoSyncMaxVideoChange,
                Type = OptionType.Integer,
                Min = 0,
                Max = 10,
                Step = 1,
                Getter = () => (double)AppContext.AppSetting.VideoSyncMaxVideoChange,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.VideoSyncMaxVideoChange), AppContext.AppSetting.VideoSyncMaxVideoChange = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CorrectDownscaling),
                Label = lang.SettingsCorrectDownscaling,
                Category = video,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.CorrectDownscaling,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.CorrectDownscaling), AppContext.AppSetting.CorrectDownscaling = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.Scale),
                Label = lang.SettingsScale,
                Category = video,
                Description = lang.SettingsHelpScale,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("lanczos", "Lanczos"),
                    new OptionChoice("spline36", "Spline36"),
                    new OptionChoice("spline64", "Spline64"),
                    new OptionChoice("ewa_lanczossharp", "EWA Lanczos (Jinc)"),
                    new OptionChoice("bicubic", "Bicubic"),
                    new OptionChoice("bilinear", "Bilinear"),
                    new OptionChoice("mitchell", "Mitchell"),
                ],
                Getter = () => AppContext.AppSetting.Scale,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.Scale), AppContext.AppSetting.Scale = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.DScale),
                Label = lang.SettingsDScale,
                Category = video,
                Description = lang.SettingsHelpDScale,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("bicubic", "Bicubic"),
                    new OptionChoice("hermite", "Hermite"),
                    new OptionChoice("lanczos", "Lanczos"),
                    new OptionChoice("spline36", "Spline36"),
                    new OptionChoice("mitchell", "Mitchell"),
                    new OptionChoice("bilinear", "Bilinear"),
                ],
                Getter = () => AppContext.AppSetting.DScale,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.DScale), AppContext.AppSetting.DScale = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.Cscale),
                Label = lang.SettingsCscale,
                Category = video,
                Description = lang.SettingsHelpCscale,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("lanczos", "Lanczos"),
                    new OptionChoice("spline36", "Spline36"),
                    new OptionChoice("bicubic", "Bicubic"),
                    new OptionChoice("bilinear", "Bilinear"),
                ],
                Getter = () => AppContext.AppSetting.Cscale,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.Cscale), AppContext.AppSetting.Cscale = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.Tscale),
                Label = lang.SettingsTscale,
                Category = video,
                Description = lang.SettingsHelpTscale,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("oversample", lang.OptionValueTscaleOversample),
                    new OptionChoice("linear", lang.OptionValueTscaleLinear),
                    new OptionChoice("cubic", lang.OptionValueTscaleCubic),
                    new OptionChoice("mitchell", lang.OptionValueTscaleMitchell),
                    new OptionChoice("lanczos", lang.OptionValueTscaleLanczos),
                ],
                Getter = () => AppContext.AppSetting.Tscale,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.Tscale), AppContext.AppSetting.Tscale = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.LinearUpscaling),
                Label = lang.SettingsLinearUpscaling,
                Category = video,
                Description = lang.SettingsHelpLinearUpscaling,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.LinearUpscaling,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.LinearUpscaling), AppContext.AppSetting.LinearUpscaling = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.Dither),
                Label = lang.SettingsDither,
                Category = video,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("fruit", lang.OptionValueDitherFruit),
                    new OptionChoice("ordered", lang.OptionValueDitherOrdered),
                    new OptionChoice("error-diffusion", lang.OptionValueDitherErrorDiffusion),
                    new OptionChoice("no", lang.OptionValueNo),
                ],
                Getter = () => AppContext.AppSetting.Dither,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.Dither), AppContext.AppSetting.Dither = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.Panscan),
                Label = lang.SettingsPanscan,
                Category = video,
                Description = lang.SettingsHelpPanscan,
                Type = OptionType.Double,
                Min = 0,
                Max = 1,
                Step = 0.05,
                Getter = () => AppContext.AppSetting.Panscan,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.Panscan), AppContext.AppSetting.Panscan = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.VideoUnscaled),
                Label = lang.SettingsVideoUnscaled,
                Category = video,
                Description = lang.SettingsHelpVideoUnscaled,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("", lang.OptionValueAuto),
                    new OptionChoice("no", lang.OptionValueVideoUnscaledNo),
                    new OptionChoice("yes", lang.OptionValueVideoUnscaledYes),
                    new OptionChoice("downscale-big", lang.OptionValueVideoUnscaledDownscaleBig),
                ],
                Getter = () => AppContext.AppSetting.VideoUnscaled,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.VideoUnscaled), AppContext.AppSetting.VideoUnscaled = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.BackgroundTileColor0),
                Label = lang.SettingsBackgroundTileColor0,
                Category = video,
                Type = OptionType.Color,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.BackgroundTileColor0,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.BackgroundTileColor0), AppContext.AppSetting.BackgroundTileColor0 = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.BackgroundTileColor1),
                Label = lang.SettingsBackgroundTileColor1,
                Category = video,
                Type = OptionType.Color,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.BackgroundTileColor1,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.BackgroundTileColor1), AppContext.AppSetting.BackgroundTileColor1 = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.BackgroundTileSize),
                Label = lang.SettingsBackgroundTileSize,
                Category = video,
                Type = OptionType.Integer,
                Min = 16,
                Max = 512,
                Step = 16,
                Getter = () => (double)AppContext.AppSetting.BackgroundTileSize,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.BackgroundTileSize), AppContext.AppSetting.BackgroundTileSize = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.VideoRotate),
                Label = lang.SettingsVideoRotate,
                Category = video,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("no", lang.OptionValueRotateNo),
                    new OptionChoice("90", lang.OptionValueRotate90),
                    new OptionChoice("180", lang.OptionValueRotate180),
                    new OptionChoice("270", lang.OptionValueRotate270),
                ],
                Getter = () => AppContext.AppSetting.VideoRotate,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.VideoRotate), AppContext.AppSetting.VideoRotate = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.Deband),
                Label = lang.SettingsDeband,
                Category = video,
                Description = lang.SettingsHelpDeband,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.Deband,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.Deband), AppContext.AppSetting.Deband = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.LinearDownscaling),
                Label = lang.SettingsLinearDownscaling,
                Category = video,
                Description = lang.SettingsHelpLinearDownscaling,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.LinearDownscaling,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.LinearDownscaling), AppContext.AppSetting.LinearDownscaling = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SigmoidUpscaling),
                Label = lang.SettingsSigmoidUpscaling,
                Category = video,
                Description = lang.SettingsHelpSigmoidUpscaling,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SigmoidUpscaling,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SigmoidUpscaling), AppContext.AppSetting.SigmoidUpscaling = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ToneMapping),
                Label = lang.SettingsToneMapping,
                Category = video,
                Description = lang.SettingsHelpToneMapping,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("bt.2390", lang.OptionValueToneMapBt2390),
                    new OptionChoice("bt.2446a", lang.OptionValueToneMapBt2446a),
                    new OptionChoice("mobius", lang.OptionValueToneMapMobius),
                    new OptionChoice("clip", lang.OptionValueToneMapClip),
                    new OptionChoice("reinhard", lang.OptionValueToneMapReinhard),
                ],
                Getter = () => AppContext.AppSetting.ToneMapping,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ToneMapping), AppContext.AppSetting.ToneMapping = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.DitherDepth),
                Label = lang.SettingsDitherDepth,
                Category = video,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("no", lang.OptionValueDitherNo),
                    new OptionChoice("auto", lang.OptionValueDitherAuto),
                    new OptionChoice("8", "8-bit"),
                    new OptionChoice("10", "10-bit"),
                ],
                Getter = () => AppContext.AppSetting.DitherDepth,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.DitherDepth), AppContext.AppSetting.DitherDepth = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.HrSeek),
                Label = lang.SettingsHrSeek,
                Category = video,
                Description = lang.SettingsHelpHrSeek,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.HrSeek,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.HrSeek), AppContext.AppSetting.HrSeek = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.HrSeekFramedrop),
                Label = lang.SettingsHrSeekFramedrop,
                Category = video,
                Description = lang.SettingsHelpHrSeekFramedrop,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.HrSeekFramedrop,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.HrSeekFramedrop), AppContext.AppSetting.HrSeekFramedrop = (bool)v!)
            },

            // ===== Audio / 音频 =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioLanguage),
                Label = lang.SettingsAudioLanguage,
                Category = audio,
                Type = OptionType.StringList,
                Choices = LanguageChoices(true),
                Getter = () => AppContext.AppSetting.AudioLanguage,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioLanguage), AppContext.AppSetting.AudioLanguage = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioDevice),
                Label = lang.SettingsAudioDevice,
                Category = audio,
                Description = lang.SettingsHelpAudioDevice,
                Type = OptionType.StringList,
                ChoicesProvider = BuildAudioDeviceChoices,
                Getter = () => AppContext.AppSetting.AudioDevice,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioDevice), AppContext.AppSetting.AudioDevice = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioChannels),
                Label = lang.SettingsAudioChannels,
                Category = audio,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("auto", lang.OptionValueChannelsAuto),
                    new OptionChoice("stereo", lang.OptionValueStereo),
                    new OptionChoice("5.1", lang.OptionValueSurround51),
                    new OptionChoice("7.1", lang.OptionValueSurround71),
                ],
                Getter = () => AppContext.AppSetting.AudioChannels,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioChannels), AppContext.AppSetting.AudioChannels = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioDelay),
                Label = lang.SettingsAudioDelay,
                Category = audio,
                Description = lang.SettingsHelpAudioDelay,
                Type = OptionType.Double,
                Min = -10,
                Max = 10,
                Step = 0.1,
                Getter = () => AppContext.AppSetting.AudioDelay,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioDelay), AppContext.AppSetting.AudioDelay = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioExclusive),
                Label = lang.SettingsAudioExclusive,
                Category = audio,
                Description = lang.SettingsHelpAudioExclusive,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.AudioExclusive,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioExclusive), AppContext.AppSetting.AudioExclusive = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioPitchCorrection),
                Label = lang.SettingsAudioPitchCorrection,
                Category = audio,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.AudioPitchCorrection,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioPitchCorrection), AppContext.AppSetting.AudioPitchCorrection = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioNormalizeDownmix),
                Label = lang.SettingsAudioNormalizeDownmix,
                Category = audio,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.AudioNormalizeDownmix,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioNormalizeDownmix), AppContext.AppSetting.AudioNormalizeDownmix = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioFileAuto),
                Label = lang.SettingsAudioFileAuto,
                Category = audio,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("no", lang.OptionValueAudioFileAutoNo),
                    new OptionChoice("exact", lang.OptionValueAudioFileAutoExact),
                    new OptionChoice("fuzzy", lang.OptionValueAudioFileAutoFuzzy),
                    new OptionChoice("all", lang.OptionValueAudioFileAutoAll),
                ],
                Getter = () => AppContext.AppSetting.AudioFileAuto,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioFileAuto), AppContext.AppSetting.AudioFileAuto = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioFilePaths),
                Label = lang.SettingsAudioFilePaths,
                Category = audio,
                Description = lang.SettingsHelpAudioFilePaths,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.AudioFilePaths,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioFilePaths), AppContext.AppSetting.AudioFilePaths = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioGapless),
                Label = lang.SettingsAudioGapless,
                Category = audio,
                Description = lang.SettingsHelpAudioGapless,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("no", lang.OptionValueAudioGaplessNo),
                    new OptionChoice("yes", lang.OptionValueAudioGaplessYes),
                    new OptionChoice("weak", lang.OptionValueAudioGaplessWeak),
                ],
                Getter = () => AppContext.AppSetting.AudioGapless,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioGapless), AppContext.AppSetting.AudioGapless = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioWaitOpen),
                Label = lang.SettingsAudioWaitOpen,
                Category = audio,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.AudioWaitOpen,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioWaitOpen), AppContext.AppSetting.AudioWaitOpen = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioBuffer),
                Label = lang.SettingsAudioBuffer,
                Category = audio,
                Description = lang.SettingsHelpAudioBuffer,
                Type = OptionType.Integer,
                Min = 0,
                Max = 2000,
                Step = 50,
                Getter = () => (double)AppContext.AppSetting.AudioBuffer,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioBuffer), AppContext.AppSetting.AudioBuffer = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CoverArtPreferEmbedded),
                Label = lang.SettingsCoverArtPreferEmbedded,
                Category = audio,
                Description = lang.SettingsHelpCoverArtPreferEmbedded,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.CoverArtPreferEmbedded,
                Setter = v =>
                {
                    AppContext.AppSetting.CoverArtPreferEmbedded = (bool)v!;
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CoverArtAlwaysScan),
                Label = lang.SettingsCoverArtAlwaysScan,
                Category = audio,
                Description = lang.SettingsHelpCoverArtAlwaysScan,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.CoverArtAlwaysScan,
                Setter = v =>
                {
                    AppContext.AppSetting.CoverArtAlwaysScan = (bool)v!;
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CoverArtLoadFromFilesystem),
                Label = lang.SettingsCoverArtLoadFromFilesystem,
                Category = audio,
                Description = lang.SettingsHelpCoverArtLoadFromFilesystem,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.CoverArtLoadFromFilesystem,
                Setter = v =>
                {
                    AppContext.AppSetting.CoverArtLoadFromFilesystem = (bool)v!;
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CoverArtPreload),
                Label = lang.SettingsCoverArtPreload,
                Category = audio,
                Description = lang.SettingsHelpCoverArtPreload,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.CoverArtPreload,
                Setter = v =>
                {
                    AppContext.AppSetting.CoverArtPreload = (bool)v!;
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CoverArtNames),
                Label = lang.SettingsCoverArtNames,
                Category = audio,
                Description = lang.SettingsHelpCoverArtNames,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.CoverArtNames,
                Setter = v =>
                {
                    AppContext.AppSetting.CoverArtNames = (string)v!;
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CoverArtImageExts),
                Label = lang.SettingsCoverArtImageExts,
                Category = audio,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.CoverArtImageExts,
                Setter = v =>
                {
                    AppContext.AppSetting.CoverArtImageExts = (string)v!;
                    AppContext.WritePluginConfigs();
                }
            },

            // ===== Subtitles =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubFontSize),
                Label = lang.SettingsSubFontSize,
                Category = subtitles,
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
                Label = lang.SettingsSubDelay,
                Category = subtitles,
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
                Label = lang.SettingsSubPos,
                Category = subtitles,
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
                Label = lang.SettingsSubtitleLanguage,
                Category = subtitles,
                Type = OptionType.StringList,
                Choices = LanguageChoices(true),
                Getter = () => AppContext.AppSetting.SubtitleLanguage,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubtitleLanguage), AppContext.AppSetting.SubtitleLanguage = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubFilePaths),
                Label = lang.SettingsSubFilePaths,
                Category = subtitles,
                Description = lang.SettingsHelpSubFilePaths,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.SubFilePaths,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubFilePaths), AppContext.AppSetting.SubFilePaths = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubHdrPeak),
                Label = lang.SettingsSubHdrPeak,
                Category = subtitles,
                Description = lang.SettingsHelpSubHdrPeak,
                Type = OptionType.Integer,
                Min = 10,
                Max = 10000,
                Step = 50,
                Getter = () => (double)AppContext.AppSetting.SubHdrPeak,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubHdrPeak), AppContext.AppSetting.SubHdrPeak = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ImageSubsHdrPeak),
                Label = lang.SettingsImageSubsHdrPeak,
                Category = subtitles,
                Description = lang.SettingsHelpImageSubsHdrPeak,
                Type = OptionType.Integer,
                Min = 10,
                Max = 10000,
                Step = 50,
                Getter = () => (double)AppContext.AppSetting.ImageSubsHdrPeak,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ImageSubsHdrPeak), AppContext.AppSetting.ImageSubsHdrPeak = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ImageSubsVideoResolution),
                Label = lang.SettingsImageSubsVideoResolution,
                Category = subtitles,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.ImageSubsVideoResolution,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ImageSubsVideoResolution), AppContext.AppSetting.ImageSubsVideoResolution = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubColor),
                Label = lang.SettingsSubColor,
                Category = subtitles,
                Description = lang.SettingsHelpSubColor,
                Type = OptionType.Color,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.SubColor,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubColor), AppContext.AppSetting.SubColor = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubBackColor),
                Label = lang.SettingsSubBackColor,
                Category = subtitles,
                Type = OptionType.Color,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.SubBackColor,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubBackColor), AppContext.AppSetting.SubBackColor = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubBorderColor),
                Label = lang.SettingsSubBorderColor,
                Category = subtitles,
                Type = OptionType.Color,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.SubBorderColor,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubBorderColor), AppContext.AppSetting.SubBorderColor = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubScaleSigns),
                Label = lang.SettingsSubScaleSigns,
                Category = subtitles,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SubScaleSigns,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubScaleSigns), AppContext.AppSetting.SubScaleSigns = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubAssOverride),
                Label = lang.SettingsSubAssOverride,
                Category = subtitles,
                Section = sSubtitleAss,
                Description = lang.SettingsHelpSubAssOverride,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("no", lang.OptionValueAssOverrideNo),
                    new OptionChoice("yes", lang.OptionValueAssOverrideYes),
                    new OptionChoice("force", lang.OptionValueAssOverrideForce),
                    new OptionChoice("scale", lang.OptionValueAssOverrideScale),
                    new OptionChoice("strip", lang.OptionValueAssOverrideStrip),
                ],
                Getter = () => AppContext.AppSetting.SubAssOverride,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubAssOverride), AppContext.AppSetting.SubAssOverride = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubAssUseVideoData),
                Label = lang.SettingsSubAssUseVideoData,
                Category = subtitles,
                Section = sSubtitleAss,
                Description = lang.SettingsHelpSubAssUseVideoData,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("", lang.OptionValueAuto),
                    new OptionChoice("none", lang.OptionValueAssUseVideoDataNone),
                    new OptionChoice("aspect-ratio", lang.OptionValueAssUseVideoDataAspectRatio),
                    new OptionChoice("all", lang.OptionValueAssUseVideoDataAll),
                ],
                Getter = () => AppContext.AppSetting.SubAssUseVideoData,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubAssUseVideoData), AppContext.AppSetting.SubAssUseVideoData = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubAssVideoAspectOverride),
                Label = lang.SettingsSubAssVideoAspectOverride,
                Category = subtitles,
                Section = sSubtitleAss,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.SubAssVideoAspectOverride,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubAssVideoAspectOverride), AppContext.AppSetting.SubAssVideoAspectOverride = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubAssVsfilterColorCompat),
                Label = lang.SettingsSubAssVsfilterColorCompat,
                Category = subtitles,
                Section = sSubtitleAss,
                Description = lang.SettingsHelpSubAssVsfilterColorCompat,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("", lang.OptionValueAuto),
                    new OptionChoice("basic", lang.OptionValueVsfilterBasic),
                    new OptionChoice("full", lang.OptionValueVsfilterFull),
                    new OptionChoice("force-601", lang.OptionValueVsfilterForce601),
                    new OptionChoice("no", lang.OptionValueNo),
                ],
                Getter = () => AppContext.AppSetting.SubAssVsfilterColorCompat,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubAssVsfilterColorCompat), AppContext.AppSetting.SubAssVsfilterColorCompat = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubAssStyleOverrides),
                Label = lang.SettingsSubAssStyleOverrides,
                Category = subtitles,
                Section = sSubtitleAss,
                Description = lang.SettingsHelpSubAssStyleOverrides,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.SubAssStyleOverrides,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubAssStyleOverrides), AppContext.AppSetting.SubAssStyleOverrides = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubAuto),
                Label = lang.SettingsSubAuto,
                Category = subtitles,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("no", lang.OptionValueSubAutoNo),
                    new OptionChoice("exact", lang.OptionValueSubAutoExact),
                    new OptionChoice("fuzzy", lang.OptionValueSubAutoFuzzy),
                    new OptionChoice("all", lang.OptionValueSubAutoAll),
                ],
                Getter = () => AppContext.AppSetting.SubAuto,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubAuto), AppContext.AppSetting.SubAuto = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubFont),
                Label = lang.SettingsSubFont,
                Category = subtitles,
                Type = OptionType.StringList,
                Choices = SubtitleFontChoices(lang),
                Getter = () => AppContext.AppSetting.SubFont,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubFont), AppContext.AppSetting.SubFont = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubFontFile),
                Label = lang.SettingsSubFontFile,
                Category = subtitles,
                Description = lang.SettingsHelpSubFontFile,
                Type = OptionType.String,
                AllowEmpty = true,
                Placeholder = AppData.Current.ResolveLocalData(Path.Combine("mpv", "fonts")),
                PickFile = true,
                OpenFolder = true,
                Getter = () => AppContext.AppSetting.SubFontFile,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubFontFile), AppContext.AppSetting.SubFontFile = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubFontProvider),
                Label = lang.SettingsSubFontProvider,
                Category = subtitles,
                Description = lang.SettingsHelpSubFontProvider,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("auto", lang.OptionValueFontProviderAuto),
                    new OptionChoice("none", lang.OptionValueFontProviderNone),
                    new OptionChoice("fontconfig", lang.OptionValueFontProviderFontconfig),
                ],
                Getter = () => AppContext.AppSetting.SubFontProvider,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubFontProvider), AppContext.AppSetting.SubFontProvider = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubCodePage),
                Label = lang.SettingsSubCodePage,
                Category = subtitles,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("auto", lang.OptionValueCodePageAuto),
                    new OptionChoice("GB18030", lang.OptionValueCodePageGb18030),
                    new OptionChoice("UTF-8", lang.OptionValueCodePageUtf8),
                    new OptionChoice("UTF-16", lang.OptionValueCodePageUtf16),
                    new OptionChoice("cp1252", lang.OptionValueCodePageCp1252),
                    new OptionChoice("shift-jis", lang.OptionValueCodePageShiftJis),
                    new OptionChoice("euc-kr", lang.OptionValueCodePageEucKr),
                    new OptionChoice("cp1251", lang.OptionValueCodePageCp1251),
                ],
                Getter = () => AppContext.AppSetting.SubCodePage,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubCodePage), AppContext.AppSetting.SubCodePage = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubOutlineSize),
                Label = lang.SettingsSubOutlineSize,
                Category = subtitles,
                Type = OptionType.Double,
                Min = 0,
                Max = 10,
                Step = 0.5,
                Getter = () => AppContext.AppSetting.SubOutlineSize,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubOutlineSize), AppContext.AppSetting.SubOutlineSize = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubShadowOffset),
                Label = lang.SettingsSubShadowOffset,
                Category = subtitles,
                Type = OptionType.Double,
                Min = 0,
                Max = 10,
                Step = 0.5,
                Getter = () => AppContext.AppSetting.SubShadowOffset,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubShadowOffset), AppContext.AppSetting.SubShadowOffset = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubBlur),
                Label = lang.SettingsSubBlur,
                Category = subtitles,
                Type = OptionType.Double,
                Min = 0,
                Max = 20,
                Step = 0.5,
                Getter = () => AppContext.AppSetting.SubBlur,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubBlur), AppContext.AppSetting.SubBlur = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubUseMargins),
                Label = lang.SettingsSubUseMargins,
                Category = subtitles,
                Description = lang.SettingsHelpSubUseMargins,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SubUseMargins,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubUseMargins), AppContext.AppSetting.SubUseMargins = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubAssForceMargins),
                Label = lang.SettingsSubAssForceMargins,
                Category = subtitles,
                Section = sSubtitleAss,
                Description = lang.SettingsHelpSubAssForceMargins,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SubAssForceMargins,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubAssForceMargins), AppContext.AppSetting.SubAssForceMargins = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubAssScaleWithWindow),
                Label = lang.SettingsSubAssScaleWithWindow,
                Category = subtitles,
                Section = sSubtitleAss,
                Description = lang.SettingsHelpSubAssScaleWithWindow,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SubAssScaleWithWindow,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubAssScaleWithWindow), AppContext.AppSetting.SubAssScaleWithWindow = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubEmbeddedFonts),
                Label = lang.SettingsSubEmbeddedFonts,
                Category = subtitles,
                Section = sSubtitleAss,
                Description = lang.SettingsHelpSubEmbeddedFonts,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SubEmbeddedFonts,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubEmbeddedFonts), AppContext.AppSetting.SubEmbeddedFonts = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.BlendSubtitles),
                Label = lang.SettingsBlendSubtitles,
                Category = subtitles,
                Section = sSubtitleAss,
                Description = lang.SettingsHelpBlendSubtitles,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("no", lang.OptionValueBlendSubtitlesNo),
                    new OptionChoice("yes", lang.OptionValueBlendSubtitlesYes),
                    new OptionChoice("video", lang.OptionValueBlendSubtitlesVideo),
                ],
                Getter = () => AppContext.AppSetting.BlendSubtitles,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.BlendSubtitles), AppContext.AppSetting.BlendSubtitles = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubFallback),
                Label = lang.SettingsSubFallback,
                Category = subtitles,
                Section = sSubtitleAss,
                Description = lang.SettingsHelpSubFallback,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("default", lang.OptionValueSubsFallbackDefault),
                    new OptionChoice("yes", lang.OptionValueSubsFallbackYes),
                    new OptionChoice("no", lang.OptionValueSubsFallbackNo),
                ],
                Getter = () => AppContext.AppSetting.SubFallback,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubFallback), AppContext.AppSetting.SubFallback = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.StretchImageSubsToScreen),
                Label = lang.SettingsStretchImageSubsToScreen,
                Category = subtitles,
                Description = lang.SettingsHelpStretchImageSubsToScreen,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.StretchImageSubsToScreen,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.StretchImageSubsToScreen), AppContext.AppSetting.StretchImageSubsToScreen = (bool)v!)
            },

            // ===== Screenshot / 截屏 =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.ScreenshotDirectory),
                Label = lang.SettingsScreenshotDirectory,
                Category = screenshot,
                Type = OptionType.String,
                AllowEmpty = true,
                Placeholder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots"),
                PickFolder = true,
                OpenFolder = true,
                Getter = () => AppContext.AppSetting.ScreenshotDirectory,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ScreenshotDirectory), AppContext.AppSetting.ScreenshotDirectory = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ScreenshotTemplate),
                Label = lang.SettingsScreenshotTemplate,
                Category = screenshot,
                Description = lang.SettingsHelpScreenshotTemplate,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("", lang.SettingsScreenshotTemplateDefault),
                    new OptionChoice("MPV-%P-N%n", lang.SettingsScreenshotTemplateMpv),
                    new OptionChoice("%F-%P", lang.SettingsScreenshotTemplateFileTime),
                    new OptionChoice("%F-%P-%n", lang.SettingsScreenshotTemplateFileTimeCounter),
                    new OptionChoice("%P-%n", lang.SettingsScreenshotTemplateTimeCounter),
                ],
                Getter = () => AppContext.AppSetting.ScreenshotTemplate,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ScreenshotTemplate), AppContext.AppSetting.ScreenshotTemplate = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ScreenshotFormat),
                Label = lang.SettingsScreenshotFormat,
                Category = screenshot,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("png", "PNG"),
                    new OptionChoice("jpg", "JPEG"),
                    new OptionChoice("webp", "WebP"),
                    new OptionChoice("jxl", "JXL"),
                    new OptionChoice("avif", "AVIF"),
                ],
                Getter = () => AppContext.AppSetting.ScreenshotFormat,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ScreenshotFormat), AppContext.AppSetting.ScreenshotFormat = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ScreenshotJpegQuality),
                Label = lang.SettingsScreenshotJpegQuality,
                Category = screenshot,
                Description = lang.SettingsHelpScreenshotJpegQuality,
                Type = OptionType.Integer,
                Min = 0,
                Max = 100,
                Step = 5,
                Getter = () => (double)AppContext.AppSetting.ScreenshotJpegQuality,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ScreenshotJpegQuality), AppContext.AppSetting.ScreenshotJpegQuality = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ScreenshotJpegSourceChroma),
                Label = lang.SettingsScreenshotJpegSourceChroma,
                Category = screenshot,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.ScreenshotJpegSourceChroma,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ScreenshotJpegSourceChroma), AppContext.AppSetting.ScreenshotJpegSourceChroma = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ScreenshotPngCompression),
                Label = lang.SettingsScreenshotPngCompression,
                Category = screenshot,
                Description = lang.SettingsHelpScreenshotPngCompression,
                Type = OptionType.Integer,
                Min = 0,
                Max = 9,
                Step = 1,
                Getter = () => (double)AppContext.AppSetting.ScreenshotPngCompression,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ScreenshotPngCompression), AppContext.AppSetting.ScreenshotPngCompression = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ScreenshotPngFilter),
                Label = lang.SettingsScreenshotPngFilter,
                Category = screenshot,
                Type = OptionType.Integer,
                Min = 0,
                Max = 5,
                Step = 1,
                Getter = () => (double)AppContext.AppSetting.ScreenshotPngFilter,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ScreenshotPngFilter), AppContext.AppSetting.ScreenshotPngFilter = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ScreenshotWebpQuality),
                Label = lang.SettingsScreenshotWebpQuality,
                Category = screenshot,
                Description = lang.SettingsHelpScreenshotWebpQuality,
                Type = OptionType.Integer,
                Min = 0,
                Max = 100,
                Step = 5,
                Getter = () => (double)AppContext.AppSetting.ScreenshotWebpQuality,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ScreenshotWebpQuality), AppContext.AppSetting.ScreenshotWebpQuality = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ScreenshotWebpLossless),
                Label = lang.SettingsScreenshotWebpLossless,
                Category = screenshot,
                Description = lang.SettingsHelpScreenshotWebpLossless,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.ScreenshotWebpLossless,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ScreenshotWebpLossless), AppContext.AppSetting.ScreenshotWebpLossless = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ScreenshotWebpCompression),
                Label = lang.SettingsScreenshotWebpCompression,
                Category = screenshot,
                Type = OptionType.Integer,
                Min = 0,
                Max = 6,
                Step = 1,
                Getter = () => (double)AppContext.AppSetting.ScreenshotWebpCompression,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ScreenshotWebpCompression), AppContext.AppSetting.ScreenshotWebpCompression = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ScreenshotJxlDistance),
                Label = lang.SettingsScreenshotJxlDistance,
                Category = screenshot,
                Description = lang.SettingsHelpScreenshotJxlDistance,
                Type = OptionType.Integer,
                Min = 0,
                Max = 15,
                Step = 1,
                Getter = () => (double)AppContext.AppSetting.ScreenshotJxlDistance,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ScreenshotJxlDistance), AppContext.AppSetting.ScreenshotJxlDistance = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ScreenshotJxlEffort),
                Label = lang.SettingsScreenshotJxlEffort,
                Category = screenshot,
                Type = OptionType.Integer,
                Min = 1,
                Max = 9,
                Step = 1,
                Getter = () => (double)AppContext.AppSetting.ScreenshotJxlEffort,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ScreenshotJxlEffort), AppContext.AppSetting.ScreenshotJxlEffort = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ScreenshotAvifEncoder),
                Label = lang.SettingsScreenshotAvifEncoder,
                Category = screenshot,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.ScreenshotAvifEncoder,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ScreenshotAvifEncoder), AppContext.AppSetting.ScreenshotAvifEncoder = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ScreenshotHighBitDepth),
                Label = lang.SettingsScreenshotHighBitDepth,
                Category = screenshot,
                Description = lang.SettingsHelpScreenshotHighBitDepth,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.ScreenshotHighBitDepth,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ScreenshotHighBitDepth), AppContext.AppSetting.ScreenshotHighBitDepth = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ScreenshotTagColorspace),
                Label = lang.SettingsScreenshotTagColorspace,
                Category = screenshot,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.ScreenshotTagColorspace,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ScreenshotTagColorspace), AppContext.AppSetting.ScreenshotTagColorspace = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ScreenshotSw),
                Label = lang.SettingsScreenshotSw,
                Category = screenshot,
                Description = lang.SettingsHelpScreenshotSw,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.ScreenshotSw,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ScreenshotSw), AppContext.AppSetting.ScreenshotSw = (bool)v!)
            },

            // ===== Advanced (initial placeholder; categoryMap below reassigns) =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.CacheOnDisk),
                Label = lang.SettingsCacheOnDisk,
                Category = gpuRenderer,
                Description = lang.SettingsHelpCacheOnDisk,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.CacheOnDisk,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.CacheOnDisk), AppContext.AppSetting.CacheOnDisk = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.VideoOutputLevels),
                Label = lang.SettingsVideoOutputLevels,
                Category = gpuRenderer,
                Description = lang.SettingsHelpVideoOutputLevels,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("auto", lang.OptionValueAuto),
                    new OptionChoice("limited", lang.OptionValueVideoLevelsLimited),
                    new OptionChoice("full", lang.OptionValueVideoLevelsFull),
                ],
                Getter = () => AppContext.AppSetting.VideoOutputLevels,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.VideoOutputLevels), AppContext.AppSetting.VideoOutputLevels = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.D3d11OutputCsp),
                Label = lang.SettingsD3d11OutputCsp,
                Category = gpuRenderer,
                Description = lang.SettingsHelpD3d11OutputCsp,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("", lang.OptionValueAuto),
                    new OptionChoice("srgb", lang.OptionValueCspSrgb),
                    new OptionChoice("bt.709", lang.OptionValueCspBt709),
                    new OptionChoice("bt.2020", lang.OptionValueCspBt2020),
                    new OptionChoice("pq", lang.OptionValueCspPq),
                ],
                Getter = () => AppContext.AppSetting.D3d11OutputCsp,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.D3d11OutputCsp), AppContext.AppSetting.D3d11OutputCsp = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.D3d11ExclusiveFs),
                Label = lang.SettingsD3d11ExclusiveFs,
                Category = gpuRenderer,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.D3d11ExclusiveFs,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.D3d11ExclusiveFs), AppContext.AppSetting.D3d11ExclusiveFs = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.D3d11Flip),
                Label = lang.SettingsD3d11Flip,
                Category = gpuRenderer,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.D3d11Flip,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.D3d11Flip), AppContext.AppSetting.D3d11Flip = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.D3d11Adapter),
                Label = lang.SettingsD3d11Adapter,
                Category = gpuRenderer,
                Description = lang.SettingsHelpD3d11Adapter,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.D3d11Adapter,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.D3d11Adapter), AppContext.AppSetting.D3d11Adapter = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.VideoDecodeDirect),
                Label = lang.SettingsVideoDecodeDirect,
                Category = gpuRenderer,
                Description = lang.SettingsHelpVideoDecodeDirect,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("auto", lang.OptionValueAuto),
                    new OptionChoice("yes", lang.OptionValueYes),
                    new OptionChoice("no", lang.OptionValueNo),
                ],
                Getter = () => AppContext.AppSetting.VideoDecodeDirect,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.VideoDecodeDirect), AppContext.AppSetting.VideoDecodeDirect = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.DemuxerMaxBytes),
                Label = lang.SettingsDemuxerMaxBytes,
                Category = gpuRenderer,
                Description = lang.SettingsHelpDemuxerMaxBytes,
                Type = OptionType.Integer,
                Min = 32,
                Max = 4096,
                Step = 32,
                Getter = () => (double)AppContext.AppSetting.DemuxerMaxBytes,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.DemuxerMaxBytes), AppContext.AppSetting.DemuxerMaxBytes = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.IccProfileAuto),
                Label = lang.SettingsIccProfileAuto,
                Category = gpuRenderer,
                Description = lang.SettingsHelpIccProfileAuto,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.IccProfileAuto,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.IccProfileAuto), AppContext.AppSetting.IccProfileAuto = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.IccProfile),
                Label = lang.SettingsIccProfile,
                Category = gpuRenderer,
                Description = lang.SettingsHelpIccProfile,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.IccProfile,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.IccProfile), AppContext.AppSetting.IccProfile = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.IccForceContrast),
                Label = lang.SettingsIccForceContrast,
                Category = gpuRenderer,
                Description = lang.SettingsHelpIccForceContrast,
                Type = OptionType.Integer,
                Min = 0,
                Max = 1000000,
                Step = 10000,
                Getter = () => (double)AppContext.AppSetting.IccForceContrast,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.IccForceContrast), AppContext.AppSetting.IccForceContrast = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.Icc3dlutSize),
                Label = lang.SettingsIcc3dlutSize,
                Category = gpuRenderer,
                Description = lang.SettingsHelpIcc3dlutSize,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("auto", lang.OptionValueAuto),
                    new OptionChoice("64x64x64", "64×64×64"),
                    new OptionChoice("128x128x128", "128×128×128"),
                    new OptionChoice("256x256x256", "256×256×256"),
                ],
                Getter = () => AppContext.AppSetting.Icc3dlutSize,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.Icc3dlutSize), AppContext.AppSetting.Icc3dlutSize = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.IccCacheDir),
                Label = lang.SettingsIccCacheDir,
                Category = gpuRenderer,
                Description = lang.SettingsHelpIccCacheDir,
                Type = OptionType.String,
                AllowEmpty = true,
                Placeholder = AppData.Current.ResolveLocalData(Path.Combine("mpv", "icc_cache")),
                PickFolder = true,
                OpenFolder = true,
                Getter = () => AppContext.AppSetting.IccCacheDir,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.IccCacheDir), AppContext.AppSetting.IccCacheDir = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.TargetColorspaceHint),
                Label = lang.SettingsTargetColorspaceHint,
                Category = gpuRenderer,
                Description = lang.SettingsHelpTargetColorspaceHint,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("auto", lang.OptionValueAuto),
                    new OptionChoice("yes", lang.OptionValueYes),
                    new OptionChoice("no", lang.OptionValueNo),
                ],
                Getter = () => AppContext.AppSetting.TargetColorspaceHint,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.TargetColorspaceHint), AppContext.AppSetting.TargetColorspaceHint = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.TargetColorspaceHintMode),
                Label = lang.SettingsTargetColorspaceHintMode,
                Category = gpuRenderer,
                Description = lang.SettingsHelpTargetColorspaceHintMode,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("", lang.OptionValueAuto),
                    new OptionChoice("target", lang.OptionValueHintModeTarget),
                    new OptionChoice("source", lang.OptionValueHintModeSource),
                    new OptionChoice("source-dynamic", lang.OptionValueHintModeSourceDynamic),
                ],
                Getter = () => AppContext.AppSetting.TargetColorspaceHintMode,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.TargetColorspaceHintMode), AppContext.AppSetting.TargetColorspaceHintMode = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.TargetColorspaceHintStrict),
                Label = lang.SettingsTargetColorspaceHintStrict,
                Category = gpuRenderer,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.TargetColorspaceHintStrict,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.TargetColorspaceHintStrict), AppContext.AppSetting.TargetColorspaceHintStrict = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.TargetPrim),
                Label = lang.SettingsTargetPrim,
                Category = gpuRenderer,
                Description = lang.SettingsHelpTargetPrim,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("", lang.OptionValueAuto),
                    new OptionChoice("bt.709", "BT.709"),
                    new OptionChoice("bt.2020", "BT.2020"),
                    new OptionChoice("display-p3", "Display P3"),
                    new OptionChoice("adobe", "Adobe RGB"),
                ],
                Getter = () => AppContext.AppSetting.TargetPrim,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.TargetPrim), AppContext.AppSetting.TargetPrim = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.TargetTrc),
                Label = lang.SettingsTargetTrc,
                Category = gpuRenderer,
                Description = lang.SettingsHelpTargetTrc,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("", lang.OptionValueAuto),
                    new OptionChoice("pq", "PQ (HDR)"),
                    new OptionChoice("srgb", "sRGB"),
                    new OptionChoice("gamma2.2", "Gamma 2.2"),
                    new OptionChoice("linear", "Linear"),
                ],
                Getter = () => AppContext.AppSetting.TargetTrc,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.TargetTrc), AppContext.AppSetting.TargetTrc = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.TargetPeak),
                Label = lang.SettingsTargetPeak,
                Category = gpuRenderer,
                Description = lang.SettingsHelpTargetPeak,
                Type = OptionType.Integer,
                Min = 0,
                Max = 10000,
                Step = 100,
                Getter = () => (double)AppContext.AppSetting.TargetPeak,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.TargetPeak), AppContext.AppSetting.TargetPeak = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.GamutMappingMode),
                Label = lang.SettingsGamutMappingMode,
                Category = gpuRenderer,
                Description = lang.SettingsHelpGamutMappingMode,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("", lang.OptionValueAuto),
                    new OptionChoice("clip", lang.OptionValueGamutClip),
                    new OptionChoice("warn", lang.OptionValueGamutWarn),
                    new OptionChoice("desaturate", lang.OptionValueGamutDesaturate),
                    new OptionChoice("darken", lang.OptionValueGamutDarken),
                ],
                Getter = () => AppContext.AppSetting.GamutMappingMode,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.GamutMappingMode), AppContext.AppSetting.GamutMappingMode = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.IccCache),
                Label = lang.SettingsIccCache,
                Category = gpuRenderer,
                Description = lang.SettingsHelpIccCache,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.IccCache,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.IccCache), AppContext.AppSetting.IccCache = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.GpuShaderCache),
                Label = lang.SettingsGpuShaderCache,
                Category = gpuRenderer,
                Description = lang.SettingsHelpGpuShaderCache,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.GpuShaderCache,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.GpuShaderCache), AppContext.AppSetting.GpuShaderCache = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.GlslShadersAppend),
                Label = lang.SettingsGlslShadersAppend,
                Category = gpuRenderer,
                Description = lang.SettingsHelpGlslShadersAppend,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.GlslShadersAppend,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.GlslShadersAppend), AppContext.AppSetting.GlslShadersAppend = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.DemuxerMaxBackBytes),
                Label = lang.SettingsDemuxerMaxBackBytes,
                Category = gpuRenderer,
                Description = lang.SettingsHelpDemuxerMaxBackBytes,
                Type = OptionType.Integer,
                Min = 0,
                Max = 2048,
                Step = 64,
                Getter = () => (double)AppContext.AppSetting.DemuxerMaxBackBytes,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.DemuxerMaxBackBytes), AppContext.AppSetting.DemuxerMaxBackBytes = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioDisplay),
                Label = lang.SettingsAudioDisplay,
                Category = gpuRenderer,
                Description = lang.SettingsHelpAudioDisplay,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("embedded-first", lang.OptionValueAudioDisplayEmbeddedFirst),
                    new OptionChoice("external-first", lang.OptionValueAudioDisplayExternalFirst),
                    new OptionChoice("no", lang.OptionValueAudioDisplayNo),
                ],
                Getter = () => AppContext.AppSetting.AudioDisplay,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioDisplay), AppContext.AppSetting.AudioDisplay = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.OsdFontSize),
                Label = lang.SettingsOsdFontSize,
                Category = gpuRenderer,
                Type = OptionType.Integer,
                Min = 8,
                Max = 96,
                Step = 2,
                Getter = () => (double)AppContext.AppSetting.OsdFontSize,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdFontSize), AppContext.AppSetting.OsdFontSize = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.OsdFont),
                Label = lang.SettingsOsdFont,
                Category = gpuRenderer,
                Description = lang.SettingsHelpOsdFont,
                Type = OptionType.StringList,
                Choices = SubtitleFontChoices(lang),
                Getter = () => AppContext.AppSetting.OsdFont,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdFont), AppContext.AppSetting.OsdFont = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.OsdOnSeek),
                Label = lang.SettingsOsdOnSeek,
                Category = gpuRenderer,
                Description = lang.SettingsHelpOsdOnSeek,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("bar", lang.OptionValueOsdOnSeekBar),
                    new OptionChoice("msg", lang.OptionValueOsdOnSeekMsg),
                    new OptionChoice("msg-bar", lang.OptionValueOsdOnSeekMsgBar),
                    new OptionChoice("no", lang.OptionValueOsdOnSeekNo),
                ],
                Getter = () => AppContext.AppSetting.OsdOnSeek,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdOnSeek), AppContext.AppSetting.OsdOnSeek = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.OsdDuration),
                Label = lang.SettingsOsdDuration,
                Category = gpuRenderer,
                Description = lang.SettingsHelpOsdDuration,
                Type = OptionType.Integer,
                Min = 250,
                Max = 10000,
                Step = 250,
                Getter = () => (double)AppContext.AppSetting.OsdDuration,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdDuration), AppContext.AppSetting.OsdDuration = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ShowOsdPlayingMsg),
                Label = lang.SettingsShowOsdPlayingMsg,
                Category = gpuRenderer,
                Description = lang.SettingsHelpShowOsdPlayingMsg,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.ShowOsdPlayingMsg,
                Setter = v =>
                {
                    AppContext.AppSetting.ShowOsdPlayingMsg = (bool)v!;
                    ApplyMpv(nameof(AppContext.AppSetting.OsdPlayingMsg), AppContext.AppSetting.OsdPlayingMsg);
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.OsdPlayingMsg),
                Label = lang.SettingsOsdPlayingMsg,
                Category = gpuRenderer,
                Description = lang.SettingsHelpOsdPlayingMsg,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.OsdPlayingMsg,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdPlayingMsg), AppContext.AppSetting.OsdPlayingMsg = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.OsdPlayingMsgDuration),
                Label = lang.SettingsOsdPlayingMsgDuration,
                Category = gpuRenderer,
                Description = lang.SettingsHelpOsdPlayingMsgDuration,
                Type = OptionType.Integer,
                Min = 0,
                Max = 10000,
                Step = 250,
                Getter = () => (double)AppContext.AppSetting.OsdPlayingMsgDuration,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdPlayingMsgDuration), AppContext.AppSetting.OsdPlayingMsgDuration = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.OsdBarWidth),
                Label = lang.SettingsOsdBarWidth,
                Category = gpuRenderer,
                Type = OptionType.Integer,
                Min = 1,
                Max = 100,
                Step = 5,
                Getter = () => (double)AppContext.AppSetting.OsdBarWidth,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdBarWidth), AppContext.AppSetting.OsdBarWidth = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.OsdBarHeight),
                Label = lang.SettingsOsdBarHeight,
                Category = gpuRenderer,
                Type = OptionType.Double,
                Min = 0.1,
                Max = 50,
                Step = 0.1,
                Getter = () => AppContext.AppSetting.OsdBarHeight,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdBarHeight), AppContext.AppSetting.OsdBarHeight = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.OsdBlur),
                Label = lang.SettingsOsdBlur,
                Category = gpuRenderer,
                Type = OptionType.Double,
                Min = 0,
                Max = 20,
                Step = 0.5,
                Getter = () => AppContext.AppSetting.OsdBlur,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdBlur), AppContext.AppSetting.OsdBlur = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.OsdOutlineSize),
                Label = lang.SettingsOsdOutlineSize,
                Category = gpuRenderer,
                Type = OptionType.Double,
                Min = 0,
                Max = 5,
                Step = 0.1,
                Getter = () => AppContext.AppSetting.OsdOutlineSize,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdOutlineSize), AppContext.AppSetting.OsdOutlineSize = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.OsdFractions),
                Label = lang.SettingsOsdFractions,
                Category = gpuRenderer,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.OsdFractions,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdFractions), AppContext.AppSetting.OsdFractions = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.OsdColor),
                Label = lang.SettingsOsdColor,
                Category = gpuRenderer,
                Type = OptionType.Color,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.OsdColor,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdColor), AppContext.AppSetting.OsdColor = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.OsdOutlineColor),
                Label = lang.SettingsOsdOutlineColor,
                Category = gpuRenderer,
                Type = OptionType.Color,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.OsdOutlineColor,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdOutlineColor), AppContext.AppSetting.OsdOutlineColor = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.VsrAutoEnabled),
                Label = lang.SettingsVsrAuto,
                Category = gpuRenderer,
                Description = lang.SettingsHelpVsrAuto,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.VsrAutoEnabled,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.VsrAutoEnabled), AppContext.AppSetting.VsrAutoEnabled = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.HdrAutoMode),
                Label = lang.SettingsHdrAutoMode,
                Category = gpuRenderer,
                Description = lang.SettingsHelpHdrAutoMode,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("auto", lang.OptionValueHdrModeAuto),
                    new OptionChoice("on", lang.OptionValueHdrModeOn),
                    new OptionChoice("off", lang.OptionValueHdrModeOff),
                ],
                Getter = () => AppContext.AppSetting.HdrAutoMode,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.HdrAutoMode), AppContext.AppSetting.HdrAutoMode = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.HdrAutoLog),
                Label = lang.SettingsHdrAutoLog,
                Category = gpuRenderer,
                Description = lang.SettingsHelpHdrAutoLog,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.HdrAutoLog,
                Setter = v =>
                {
                    AppContext.AppSetting.HdrAutoLog = (bool)v!;
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SeekHoldEnabled),
                Label = lang.SettingsSeekHold,
                Category = gpuRenderer,
                Description = lang.SettingsHelpSeekHold,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SeekHoldEnabled,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SeekHoldEnabled), AppContext.AppSetting.SeekHoldEnabled = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.MetadataOsdEnabled),
                Label = lang.SettingsMetadataOsdEnabled,
                Category = gpuRenderer,
                Description = lang.SettingsHelpMetadataOsdEnabled,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.MetadataOsdEnabled,
                Setter = v =>
                {
                    AppContext.AppSetting.MetadataOsdEnabled = (bool)v!;
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.MetadataOsdAutohideTimeout),
                Label = lang.SettingsMetadataOsdAutohideTimeout,
                Category = gpuRenderer,
                Description = lang.SettingsHelpMetadataOsdAutohideTimeout,
                Type = OptionType.Integer,
                Min = 1,
                Max = 60,
                Step = 1,
                Getter = () => (double)AppContext.AppSetting.MetadataOsdAutohideTimeout,
                Setter = v =>
                {
                    AppContext.AppSetting.MetadataOsdAutohideTimeout = Convert.ToInt32(v);
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.MetadataOsdShowChapter),
                Label = lang.SettingsMetadataOsdShowChapter,
                Category = gpuRenderer,
                Description = lang.SettingsHelpMetadataOsdShowChapter,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.MetadataOsdShowChapter,
                Setter = v =>
                {
                    AppContext.AppSetting.MetadataOsdShowChapter = (bool)v!;
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.MetadataOsdEnableForVideo),
                Label = lang.SettingsMetadataOsdEnableForVideo,
                Category = gpuRenderer,
                Description = lang.SettingsHelpMetadataOsdEnableForVideo,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.MetadataOsdEnableForVideo,
                Setter = v =>
                {
                    AppContext.AppSetting.MetadataOsdEnableForVideo = (bool)v!;
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.MetadataOsdEnableForImage),
                Label = lang.SettingsMetadataOsdEnableForImage,
                Category = gpuRenderer,
                Description = lang.SettingsHelpMetadataOsdEnableForImage,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.MetadataOsdEnableForImage,
                Setter = v =>
                {
                    AppContext.AppSetting.MetadataOsdEnableForImage = (bool)v!;
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.MetadataOsdAutohideStatusTimeout),
                Label = lang.SettingsMetadataOsdAutohideStatusTimeout,
                Category = gpuRenderer,
                Description = lang.SettingsHelpMetadataOsdAutohideStatusTimeout,
                Type = OptionType.Integer,
                Min = 1,
                Max = 60,
                Step = 1,
                Getter = () => (double)AppContext.AppSetting.MetadataOsdAutohideStatusTimeout,
                Setter = v =>
                {
                    AppContext.AppSetting.MetadataOsdAutohideStatusTimeout = Convert.ToInt32(v);
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.MetadataOsdShowAlbumTrack),
                Label = lang.SettingsMetadataOsdShowAlbumTrack,
                Category = gpuRenderer,
                Description = lang.SettingsHelpMetadataOsdShowAlbumTrack,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.MetadataOsdShowAlbumTrack,
                Setter = v =>
                {
                    AppContext.AppSetting.MetadataOsdShowAlbumTrack = (bool)v!;
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.MetadataOsdMessageMaxLength),
                Label = lang.SettingsMetadataOsdMessageMaxLength,
                Category = gpuRenderer,
                Description = lang.SettingsHelpMetadataOsdMessageMaxLength,
                Type = OptionType.Integer,
                Min = 16,
                Max = 512,
                Step = 8,
                Getter = () => (double)AppContext.AppSetting.MetadataOsdMessageMaxLength,
                Setter = v =>
                {
                    AppContext.AppSetting.MetadataOsdMessageMaxLength = Convert.ToInt32(v);
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ThumbfastQuality),
                Label = lang.SettingsThumbfastQuality,
                Category = gpuRenderer,
                Description = lang.SettingsHelpThumbfastQuality,
                Type = OptionType.StringList,
                AllowCustom = false,
                Choices =
                [
                    new OptionChoice("1", lang.OptionValueThumbfastQualityFast),
                    new OptionChoice("2", lang.OptionValueThumbfastQualityBalanced),
                    new OptionChoice("3", lang.OptionValueThumbfastQualityHighest),
                ],
                Getter = () => AppContext.AppSetting.ThumbfastQuality.ToString(),
                Setter = v =>
                {
                    AppContext.AppSetting.ThumbfastQuality = int.TryParse((string)v!, out var q) ? q : 2;
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ThumbfastNetwork),
                Label = lang.SettingsThumbfastNetwork,
                Category = gpuRenderer,
                Description = lang.SettingsHelpThumbfastNetwork,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.ThumbfastNetwork,
                Setter = v =>
                {
                    AppContext.AppSetting.ThumbfastNetwork = (bool)v!;
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ThumbfastMinDuration),
                Label = lang.SettingsThumbfastMinDuration,
                Category = gpuRenderer,
                Type = OptionType.Integer,
                Min = 0,
                Max = 60,
                Step = 1,
                Getter = () => (double)AppContext.AppSetting.ThumbfastMinDuration,
                Setter = v =>
                {
                    AppContext.AppSetting.ThumbfastMinDuration = Convert.ToInt32(v);
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ThumbfastPrecise),
                Label = lang.SettingsThumbfastPrecise,
                Category = gpuRenderer,
                Description = lang.SettingsHelpThumbfastPrecise,
                Type = OptionType.StringList,
                AllowCustom = false,
                Choices =
                [
                    new OptionChoice("0", lang.OptionValueThumbfastPreciseAuto),
                    new OptionChoice("1", lang.OptionValueThumbfastPreciseKeyframes),
                    new OptionChoice("2", lang.OptionValueThumbfastPreciseAlways),
                ],
                Getter = () => AppContext.AppSetting.ThumbfastPrecise.ToString(),
                Setter = v =>
                {
                    AppContext.AppSetting.ThumbfastPrecise = int.TryParse((string)v!, out var p) ? p : 0;
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ThumbfastMaxWidth),
                Label = lang.SettingsThumbfastMaxWidth,
                Category = gpuRenderer,
                Type = OptionType.Integer,
                Min = 64,
                Max = 2000,
                Step = 16,
                Getter = () => (double)AppContext.AppSetting.ThumbfastMaxWidth,
                Setter = v =>
                {
                    AppContext.AppSetting.ThumbfastMaxWidth = Convert.ToInt32(v);
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ThumbfastMaxHeight),
                Label = lang.SettingsThumbfastMaxHeight,
                Category = gpuRenderer,
                Type = OptionType.Integer,
                Min = 64,
                Max = 4000,
                Step = 16,
                Getter = () => (double)AppContext.AppSetting.ThumbfastMaxHeight,
                Setter = v =>
                {
                    AppContext.AppSetting.ThumbfastMaxHeight = Convert.ToInt32(v);
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ThumbfastSpawnFirst),
                Label = lang.SettingsThumbfastSpawnFirst,
                Category = gpuRenderer,
                Description = lang.SettingsHelpThumbfastSpawnFirst,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.ThumbfastSpawnFirst,
                Setter = v =>
                {
                    AppContext.AppSetting.ThumbfastSpawnFirst = (bool)v!;
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ThumbfastThreads),
                Label = lang.SettingsThumbfastThreads,
                Category = gpuRenderer,
                Description = lang.SettingsHelpThumbfastThreads,
                Type = OptionType.Integer,
                Min = 1,
                Max = 16,
                Step = 1,
                Getter = () => (double)AppContext.AppSetting.ThumbfastThreads,
                Setter = v =>
                {
                    AppContext.AppSetting.ThumbfastThreads = Convert.ToInt32(v);
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ThumbfastFrequency),
                Label = lang.SettingsThumbfastFrequency,
                Category = gpuRenderer,
                Type = OptionType.Double,
                Min = 0.05,
                Max = 1,
                Step = 0.05,
                Getter = () => AppContext.AppSetting.ThumbfastFrequency,
                Setter = v =>
                {
                    AppContext.AppSetting.ThumbfastFrequency = (double)v!;
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ThumbfastDirectIo),
                Label = lang.SettingsThumbfastDirectIo,
                Category = gpuRenderer,
                Description = lang.SettingsHelpThumbfastDirectIo,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.ThumbfastDirectIo,
                Setter = v =>
                {
                    AppContext.AppSetting.ThumbfastDirectIo = (bool)v!;
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ThumbfastQuitAfterInactivity),
                Label = lang.SettingsThumbfastQuitAfterInactivity,
                Category = gpuRenderer,
                Description = lang.SettingsHelpThumbfastQuitAfterInactivity,
                Type = OptionType.Integer,
                Min = 0,
                Max = 600,
                Step = 5,
                Getter = () => (double)AppContext.AppSetting.ThumbfastQuitAfterInactivity,
                Setter = v =>
                {
                    AppContext.AppSetting.ThumbfastQuitAfterInactivity = Convert.ToInt32(v);
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.InputIpcServer),
                Label = lang.SettingsInputIpcServer,
                Category = gpuRenderer,
                Description = lang.SettingsHelpInputIpcServer,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.InputIpcServer,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.InputIpcServer), AppContext.AppSetting.InputIpcServer = (string)v!)
            },

            // ===== Path folders =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.WatchLaterDir),
                Label = lang.SettingsWatchLaterDir,
                Category = gpuRenderer,
                Description = lang.SettingsHelpWatchLaterDir,
                Type = OptionType.String,
                AllowEmpty = true,
                Placeholder = AppData.Current.ResolveLocalData(Path.Combine("mpv", "watch_later")),
                PickFolder = true,
                OpenFolder = true,
                Getter = () => AppContext.AppSetting.WatchLaterDir,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.WatchLaterDir), AppContext.AppSetting.WatchLaterDir = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.GpuShaderCacheDir),
                Label = lang.SettingsGpuShaderCacheDir,
                Category = gpuRenderer,
                Description = lang.SettingsHelpGpuShaderCacheDir,
                Type = OptionType.String,
                AllowEmpty = true,
                Placeholder = AppData.Current.ResolveLocalData(Path.Combine("mpv", "gpu_cache")),
                PickFolder = true,
                OpenFolder = true,
                Getter = () => AppContext.AppSetting.GpuShaderCacheDir,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.GpuShaderCacheDir), AppContext.AppSetting.GpuShaderCacheDir = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CacheDirectory),
                Label = lang.SettingsCacheDir,
                Category = gpuRenderer,
                Description = lang.SettingsHelpCacheDirectory,
                Type = OptionType.String,
                AllowEmpty = true,
                Placeholder = AppData.Current.ResolveLocalData(Path.Combine("mpv", "cache")),
                PickFolder = true,
                OpenFolder = true,
                Getter = () => AppContext.AppSetting.CacheDirectory,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.CacheDirectory), AppContext.AppSetting.CacheDirectory = (string)v!)
            },
        };

        options.AddRange(BuildShortcutOptions(shortcuts));

        foreach (var option in options)
        {
            if (RedundantDescriptions.Contains(option.Key))
            {
                option.Description = null;
            }
            if (NoCustomOptions.Contains(option.Key))
            {
                option.AllowCustom = false;
            }
        }

                                var categoryOrder = new[]
        {
            program,
            playback,
            watchLater,
            video,
            audio,
            subtitles,
            window,
            demuxer,
            cache,
            network,
            input,
            shortcuts,
            osd,
            screenshot,
            testing,
        };

        var categoryMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // program
            [nameof(AppSettings.ThemeType)] = program,
            [nameof(AppSettings.BackdropType)] = program,
            [nameof(AppSettings.ThemeAccentColor)] = program,
            [nameof(AppSettings.ThemeOpacity)] = program,
            [nameof(AppSettings.ThemeLuminosity)] = program,
            [nameof(AppSettings.UiFont)] = program,
            [nameof(AppSettings.CurrentLanguage)] = program,
            [nameof(AppSettings.EnableDebugLog)] = program,
            // playback
            [nameof(AppSettings.LoopFile)] = playback,
            [nameof(AppSettings.LoopPlaylist)] = playback,
            [nameof(AppSettings.Speed)] = playback,
            [nameof(AppSettings.HrSeek)] = playback,
            [nameof(AppSettings.HrSeekFramedrop)] = playback,
            [nameof(AppSettings.SeekHoldEnabled)] = playback,
            [nameof(AppSettings.EnableVideoPreview)] = playback,
            [nameof(AppSettings.ThumbfastQuality)] = playback,
            [nameof(AppSettings.ThumbfastNetwork)] = playback,
            [nameof(AppSettings.ThumbfastMinDuration)] = playback,
            [nameof(AppSettings.ThumbfastPrecise)] = playback,
            [nameof(AppSettings.ThumbfastMaxWidth)] = playback,
            [nameof(AppSettings.ThumbfastMaxHeight)] = playback,
            [nameof(AppSettings.ThumbfastSpawnFirst)] = playback,
            [nameof(AppSettings.ThumbfastThreads)] = playback,
            [nameof(AppSettings.ThumbfastFrequency)] = playback,
            [nameof(AppSettings.ThumbfastDirectIo)] = playback,
            [nameof(AppSettings.ThumbfastQuitAfterInactivity)] = playback,
            // watchLater
            [nameof(AppSettings.SavePositionOnQuit)] = watchLater,
            [nameof(AppSettings.ResumePlayback)] = watchLater,
            [nameof(AppSettings.WatchLaterOptions)] = watchLater,
            [nameof(AppSettings.WatchLaterDir)] = watchLater,
            // video
            [nameof(AppSettings.Hwdec)] = video,
            [nameof(AppSettings.HwdecCodecs)] = video,
            [nameof(AppSettings.VideoDecodeDirect)] = video,
            [nameof(AppSettings.Deinterlace)] = video,
            [nameof(AppSettings.VideoRotate)] = video,
            [nameof(AppSettings.AspectRatio)] = video,
            [nameof(AppSettings.Panscan)] = video,
            [nameof(AppSettings.VideoUnscaled)] = video,
            [nameof(AppSettings.VideoOutputLevels)] = video,
            [nameof(AppSettings.HdrAutoMode)] = video,
            [nameof(AppSettings.HdrAutoLog)] = video,
            [nameof(AppSettings.VsrAutoEnabled)] = video,
            [nameof(AppSettings.Scale)] = video,
            [nameof(AppSettings.DScale)] = video,
            [nameof(AppSettings.Cscale)] = video,
            [nameof(AppSettings.Tscale)] = video,
            [nameof(AppSettings.LinearUpscaling)] = video,
            [nameof(AppSettings.SigmoidUpscaling)] = video,
            [nameof(AppSettings.LinearDownscaling)] = video,
            [nameof(AppSettings.CorrectDownscaling)] = video,
            [nameof(AppSettings.Deband)] = video,
            [nameof(AppSettings.Dither)] = video,
            [nameof(AppSettings.DitherDepth)] = video,
            [nameof(AppSettings.ToneMapping)] = video,
            [nameof(AppSettings.TargetColorspaceHint)] = video,
            [nameof(AppSettings.TargetColorspaceHintMode)] = video,
            [nameof(AppSettings.TargetColorspaceHintStrict)] = video,
            [nameof(AppSettings.TargetPrim)] = video,
            [nameof(AppSettings.TargetTrc)] = video,
            [nameof(AppSettings.TargetPeak)] = video,
            [nameof(AppSettings.GamutMappingMode)] = video,
            [nameof(AppSettings.IccProfileAuto)] = video,
            [nameof(AppSettings.IccProfile)] = video,
            [nameof(AppSettings.IccForceContrast)] = video,
            [nameof(AppSettings.Icc3dlutSize)] = video,
            [nameof(AppSettings.IccCache)] = video,
            [nameof(AppSettings.IccCacheDir)] = video,
            [nameof(AppSettings.D3d11OutputCsp)] = video,
            [nameof(AppSettings.Interpolation)] = video,
            [nameof(AppSettings.BackgroundTileColor0)] = video,
            [nameof(AppSettings.BackgroundTileColor1)] = video,
            [nameof(AppSettings.BackgroundTileSize)] = video,
            [nameof(AppSettings.D3d11ExclusiveFs)] = video,
            [nameof(AppSettings.D3d11Flip)] = video,
            [nameof(AppSettings.D3d11Adapter)] = video,
            [nameof(AppSettings.GpuShaderCache)] = video,
            [nameof(AppSettings.GpuShaderCacheDir)] = video,
            [nameof(AppSettings.GlslShadersAppend)] = video,
            [nameof(AppSettings.VideoSync)] = video,
            [nameof(AppSettings.VideoSyncMaxVideoChange)] = video,
            // audio
            [nameof(AppSettings.AudioDevice)] = audio,
            [nameof(AppSettings.AudioExclusive)] = audio,
            [nameof(AppSettings.AudioChannels)] = audio,
            [nameof(AppSettings.AudioDelay)] = audio,
            [nameof(AppSettings.AudioBuffer)] = audio,
            [nameof(AppSettings.AudioWaitOpen)] = audio,
            [nameof(AppSettings.AudioPitchCorrection)] = audio,
            [nameof(AppSettings.AudioNormalizeDownmix)] = audio,
            [nameof(AppSettings.AudioGapless)] = audio,
            [nameof(AppSettings.Volume)] = audio,
            [nameof(AppSettings.VolumeMax)] = audio,
            [nameof(AppSettings.AudioFileAuto)] = audio,
            [nameof(AppSettings.AudioExts)] = audio,
            [nameof(AppSettings.AudioFilePaths)] = audio,
            [nameof(AppSettings.AudioDisplay)] = audio,
            [nameof(AppSettings.CoverArtPreferEmbedded)] = audio,
            [nameof(AppSettings.CoverArtAlwaysScan)] = audio,
            [nameof(AppSettings.CoverArtLoadFromFilesystem)] = audio,
            [nameof(AppSettings.CoverArtPreload)] = audio,
            [nameof(AppSettings.CoverArtNames)] = audio,
            [nameof(AppSettings.CoverArtImageExts)] = audio,
            // subtitles
            [nameof(AppSettings.AudioLanguage)] = subtitles,
            [nameof(AppSettings.SubtitleLanguage)] = subtitles,
            [nameof(AppSettings.SubFallback)] = subtitles,
            [nameof(AppSettings.SubFontSize)] = subtitles,
            [nameof(AppSettings.SubFont)] = subtitles,
            [nameof(AppSettings.SubFontFile)] = subtitles,
            [nameof(AppSettings.SubFontProvider)] = subtitles,
            [nameof(AppSettings.SubCodePage)] = subtitles,
            [nameof(AppSettings.SubColor)] = subtitles,
            [nameof(AppSettings.SubBackColor)] = subtitles,
            [nameof(AppSettings.SubBorderColor)] = subtitles,
            [nameof(AppSettings.SubOutlineSize)] = subtitles,
            [nameof(AppSettings.SubShadowOffset)] = subtitles,
            [nameof(AppSettings.SubBlur)] = subtitles,
            [nameof(AppSettings.SubPos)] = subtitles,
            [nameof(AppSettings.SubDelay)] = subtitles,
            [nameof(AppSettings.SubScaleSigns)] = subtitles,
            [nameof(AppSettings.SubUseMargins)] = subtitles,
            [nameof(AppSettings.SubAuto)] = subtitles,
            [nameof(AppSettings.SubFilePaths)] = subtitles,
            [nameof(AppSettings.SubHdrPeak)] = subtitles,
            [nameof(AppSettings.SubAssOverride)] = subtitles,
            [nameof(AppSettings.SubAssStyleOverrides)] = subtitles,
            [nameof(AppSettings.SubAssForceMargins)] = subtitles,
            [nameof(AppSettings.SubAssScaleWithWindow)] = subtitles,
            [nameof(AppSettings.SubAssUseVideoData)] = subtitles,
            [nameof(AppSettings.SubAssVideoAspectOverride)] = subtitles,
            [nameof(AppSettings.SubAssVsfilterColorCompat)] = subtitles,
            [nameof(AppSettings.SubEmbeddedFonts)] = subtitles,
            [nameof(AppSettings.BlendSubtitles)] = subtitles,
            [nameof(AppSettings.StretchImageSubsToScreen)] = subtitles,
            [nameof(AppSettings.ImageSubsVideoResolution)] = subtitles,
            [nameof(AppSettings.ImageSubsHdrPeak)] = subtitles,
            // window
            [nameof(AppSettings.AlwaysOnTop)] = window,
            [nameof(AppSettings.KeepOpen)] = window,
            // demuxer
            [nameof(AppSettings.AutoCreatePlaylist)] = demuxer,
            [nameof(AppSettings.DirectoryMode)] = demuxer,
            [nameof(AppSettings.DirectoryFilterTypes)] = demuxer,
            [nameof(AppSettings.VideoExts)] = demuxer,
            [nameof(AppSettings.ImageExts)] = demuxer,
            [nameof(AppSettings.DemuxerMaxBytes)] = demuxer,
            [nameof(AppSettings.DemuxerMaxBackBytes)] = demuxer,
            [nameof(AppSettings.DemuxerReadahead)] = demuxer,
            // cache
            [nameof(AppSettings.CacheEnabled)] = cache,
            [nameof(AppSettings.CacheSecs)] = cache,
            [nameof(AppSettings.CacheOnDisk)] = cache,
            [nameof(AppSettings.CacheDirectory)] = cache,
            // network
            [nameof(AppSettings.Ytdl)] = network,
            [nameof(AppSettings.YtdlRawOptionsAppend)] = network,
            // input
            [nameof(AppSettings.InputIme)] = input,
            [nameof(AppSettings.InputIpcServer)] = input,
            // shortcuts
            // osd
            [nameof(AppSettings.OsdFontSize)] = osd,
            [nameof(AppSettings.OsdFont)] = osd,
            [nameof(AppSettings.OsdColor)] = osd,
            [nameof(AppSettings.OsdOutlineColor)] = osd,
            [nameof(AppSettings.OsdOnSeek)] = osd,
            [nameof(AppSettings.OsdDuration)] = osd,
            [nameof(AppSettings.OsdPlayingMsg)] = osd,
            [nameof(AppSettings.OsdPlayingMsgDuration)] = osd,
            [nameof(AppSettings.OsdBarWidth)] = osd,
            [nameof(AppSettings.OsdBarHeight)] = osd,
            [nameof(AppSettings.OsdBlur)] = osd,
            [nameof(AppSettings.OsdOutlineSize)] = osd,
            [nameof(AppSettings.OsdFractions)] = osd,
            [nameof(AppSettings.ShowOsdPlayingMsg)] = osd,
            [nameof(AppSettings.MetadataOsdEnabled)] = osd,
            [nameof(AppSettings.MetadataOsdAutohideTimeout)] = osd,
            [nameof(AppSettings.MetadataOsdShowChapter)] = osd,
            [nameof(AppSettings.MetadataOsdEnableForVideo)] = osd,
            [nameof(AppSettings.MetadataOsdEnableForImage)] = osd,
            [nameof(AppSettings.MetadataOsdAutohideStatusTimeout)] = osd,
            [nameof(AppSettings.MetadataOsdShowAlbumTrack)] = osd,
            [nameof(AppSettings.MetadataOsdMessageMaxLength)] = osd,
            // screenshot
            [nameof(AppSettings.ScreenshotDirectory)] = screenshot,
            [nameof(AppSettings.ScreenshotTemplate)] = screenshot,
            [nameof(AppSettings.ScreenshotFormat)] = screenshot,
            [nameof(AppSettings.ScreenshotJpegQuality)] = screenshot,
            [nameof(AppSettings.ScreenshotJpegSourceChroma)] = screenshot,
            [nameof(AppSettings.ScreenshotPngCompression)] = screenshot,
            [nameof(AppSettings.ScreenshotPngFilter)] = screenshot,
            [nameof(AppSettings.ScreenshotWebpQuality)] = screenshot,
            [nameof(AppSettings.ScreenshotWebpLossless)] = screenshot,
            [nameof(AppSettings.ScreenshotWebpCompression)] = screenshot,
            [nameof(AppSettings.ScreenshotJxlDistance)] = screenshot,
            [nameof(AppSettings.ScreenshotJxlEffort)] = screenshot,
            [nameof(AppSettings.ScreenshotAvifEncoder)] = screenshot,
            [nameof(AppSettings.ScreenshotHighBitDepth)] = screenshot,
            [nameof(AppSettings.ScreenshotTagColorspace)] = screenshot,
            [nameof(AppSettings.ScreenshotSw)] = screenshot,
            // testing
            [nameof(AppSettings.TestMpvCommandLog)] = testing,
            [nameof(AppSettings.TestOsdMessage)] = testing,
            [nameof(AppSettings.TestSignal)] = testing,
        };

        var optionOrder = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [nameof(AppSettings.ThemeType)] = 0,
            [nameof(AppSettings.BackdropType)] = 1,
            [nameof(AppSettings.ThemeAccentColor)] = 2,
            [nameof(AppSettings.ThemeOpacity)] = 3,
            [nameof(AppSettings.ThemeLuminosity)] = 4,
            [nameof(AppSettings.UiFont)] = 5,
            [nameof(AppSettings.TestMpvCommandLog)] = 6,
            [nameof(AppSettings.TestOsdMessage)] = 7,
            [nameof(AppSettings.TestSignal)] = 8,
            [nameof(AppSettings.CurrentLanguage)] = 9,
            [nameof(AppSettings.EnableDebugLog)] = 10,
            [nameof(AppSettings.Ytdl)] = 11,
            [nameof(AppSettings.YtdlRawOptionsAppend)] = 12,
            [nameof(AppSettings.LoopFile)] = 13,
            [nameof(AppSettings.LoopPlaylist)] = 14,
            [nameof(AppSettings.Speed)] = 15,
            [nameof(AppSettings.HrSeek)] = 16,
            [nameof(AppSettings.HrSeekFramedrop)] = 17,
            [nameof(AppSettings.SeekHoldEnabled)] = 18,
            [nameof(AppSettings.EnableVideoPreview)] = 19,
            [nameof(AppSettings.ThumbfastQuality)] = 20,
            [nameof(AppSettings.ThumbfastNetwork)] = 21,
            [nameof(AppSettings.ThumbfastMinDuration)] = 22,
            [nameof(AppSettings.ThumbfastPrecise)] = 23,
            [nameof(AppSettings.ThumbfastMaxWidth)] = 24,
            [nameof(AppSettings.ThumbfastMaxHeight)] = 25,
            [nameof(AppSettings.ThumbfastSpawnFirst)] = 26,
            [nameof(AppSettings.ThumbfastThreads)] = 27,
            [nameof(AppSettings.ThumbfastFrequency)] = 28,
            [nameof(AppSettings.ThumbfastDirectIo)] = 29,
            [nameof(AppSettings.ThumbfastQuitAfterInactivity)] = 30,
            [nameof(AppSettings.AudioLanguage)] = 31,
            [nameof(AppSettings.SubtitleLanguage)] = 32,
            [nameof(AppSettings.SubFallback)] = 33,
            [nameof(AppSettings.SavePositionOnQuit)] = 34,
            [nameof(AppSettings.ResumePlayback)] = 35,
            [nameof(AppSettings.WatchLaterOptions)] = 36,
            [nameof(AppSettings.WatchLaterDir)] = 37,
            [nameof(AppSettings.Hwdec)] = 38,
            [nameof(AppSettings.HwdecCodecs)] = 39,
            [nameof(AppSettings.VideoDecodeDirect)] = 40,
            [nameof(AppSettings.Deinterlace)] = 41,
            [nameof(AppSettings.VideoRotate)] = 42,
            [nameof(AppSettings.AspectRatio)] = 43,
            [nameof(AppSettings.Panscan)] = 44,
            [nameof(AppSettings.VideoUnscaled)] = 45,
            [nameof(AppSettings.VideoOutputLevels)] = 46,
            [nameof(AppSettings.HdrAutoMode)] = 47,
            [nameof(AppSettings.HdrAutoLog)] = 48,
            [nameof(AppSettings.VsrAutoEnabled)] = 49,
            [nameof(AppSettings.AudioDevice)] = 50,
            [nameof(AppSettings.AudioExclusive)] = 51,
            [nameof(AppSettings.AudioChannels)] = 52,
            [nameof(AppSettings.AudioDelay)] = 53,
            [nameof(AppSettings.AudioBuffer)] = 54,
            [nameof(AppSettings.AudioWaitOpen)] = 55,
            [nameof(AppSettings.AudioPitchCorrection)] = 56,
            [nameof(AppSettings.AudioNormalizeDownmix)] = 57,
            [nameof(AppSettings.AudioGapless)] = 58,
            [nameof(AppSettings.Volume)] = 59,
            [nameof(AppSettings.VolumeMax)] = 60,
            [nameof(AppSettings.AudioFileAuto)] = 61,
            [nameof(AppSettings.AudioExts)] = 62,
            [nameof(AppSettings.AudioFilePaths)] = 63,
            [nameof(AppSettings.AudioDisplay)] = 64,
            [nameof(AppSettings.CoverArtPreferEmbedded)] = 65,
            [nameof(AppSettings.CoverArtAlwaysScan)] = 66,
            [nameof(AppSettings.CoverArtLoadFromFilesystem)] = 67,
            [nameof(AppSettings.CoverArtPreload)] = 68,
            [nameof(AppSettings.CoverArtNames)] = 69,
            [nameof(AppSettings.CoverArtImageExts)] = 70,
            [nameof(AppSettings.SubFontSize)] = 71,
            [nameof(AppSettings.SubFont)] = 72,
            [nameof(AppSettings.SubFontFile)] = 73,
            [nameof(AppSettings.SubFontProvider)] = 74,
            [nameof(AppSettings.SubCodePage)] = 75,
            [nameof(AppSettings.SubColor)] = 76,
            [nameof(AppSettings.SubBackColor)] = 77,
            [nameof(AppSettings.SubBorderColor)] = 78,
            [nameof(AppSettings.SubOutlineSize)] = 79,
            [nameof(AppSettings.SubShadowOffset)] = 80,
            [nameof(AppSettings.SubBlur)] = 81,
            [nameof(AppSettings.SubPos)] = 82,
            [nameof(AppSettings.SubDelay)] = 83,
            [nameof(AppSettings.SubScaleSigns)] = 84,
            [nameof(AppSettings.SubUseMargins)] = 85,
            [nameof(AppSettings.SubAuto)] = 86,
            [nameof(AppSettings.SubFilePaths)] = 87,
            [nameof(AppSettings.SubHdrPeak)] = 88,
            [nameof(AppSettings.SubAssOverride)] = 89,
            [nameof(AppSettings.SubAssStyleOverrides)] = 90,
            [nameof(AppSettings.SubAssForceMargins)] = 91,
            [nameof(AppSettings.SubAssScaleWithWindow)] = 92,
            [nameof(AppSettings.SubAssUseVideoData)] = 93,
            [nameof(AppSettings.SubAssVideoAspectOverride)] = 94,
            [nameof(AppSettings.SubAssVsfilterColorCompat)] = 95,
            [nameof(AppSettings.SubEmbeddedFonts)] = 96,
            [nameof(AppSettings.BlendSubtitles)] = 97,
            [nameof(AppSettings.StretchImageSubsToScreen)] = 98,
            [nameof(AppSettings.ImageSubsVideoResolution)] = 99,
            [nameof(AppSettings.ImageSubsHdrPeak)] = 100,
            [nameof(AppSettings.AlwaysOnTop)] = 101,
            [nameof(AppSettings.KeepOpen)] = 102,
            [nameof(AppSettings.AutoCreatePlaylist)] = 103,
            [nameof(AppSettings.DirectoryMode)] = 104,
            [nameof(AppSettings.DirectoryFilterTypes)] = 105,
            [nameof(AppSettings.VideoExts)] = 106,
            [nameof(AppSettings.ImageExts)] = 107,
            [nameof(AppSettings.DemuxerMaxBytes)] = 108,
            [nameof(AppSettings.DemuxerMaxBackBytes)] = 109,
            [nameof(AppSettings.DemuxerReadahead)] = 110,
            [nameof(AppSettings.CacheEnabled)] = 111,
            [nameof(AppSettings.CacheSecs)] = 112,
            [nameof(AppSettings.CacheOnDisk)] = 113,
            [nameof(AppSettings.CacheDirectory)] = 114,
            [nameof(AppSettings.InputIme)] = 115,
            [nameof(AppSettings.InputIpcServer)] = 116,
            [nameof(AppSettings.OsdFontSize)] = 117,
            [nameof(AppSettings.OsdFont)] = 118,
            [nameof(AppSettings.OsdColor)] = 119,
            [nameof(AppSettings.OsdOutlineColor)] = 120,
            [nameof(AppSettings.OsdOnSeek)] = 121,
            [nameof(AppSettings.OsdDuration)] = 122,
            [nameof(AppSettings.OsdPlayingMsg)] = 123,
            [nameof(AppSettings.OsdPlayingMsgDuration)] = 124,
            [nameof(AppSettings.OsdBarWidth)] = 125,
            [nameof(AppSettings.OsdBarHeight)] = 126,
            [nameof(AppSettings.OsdBlur)] = 127,
            [nameof(AppSettings.OsdOutlineSize)] = 128,
            [nameof(AppSettings.OsdFractions)] = 129,
            [nameof(AppSettings.ShowOsdPlayingMsg)] = 130,
            [nameof(AppSettings.MetadataOsdEnabled)] = 131,
            [nameof(AppSettings.MetadataOsdAutohideTimeout)] = 132,
            [nameof(AppSettings.MetadataOsdShowChapter)] = 133,
            [nameof(AppSettings.MetadataOsdEnableForVideo)] = 134,
            [nameof(AppSettings.MetadataOsdEnableForImage)] = 135,
            [nameof(AppSettings.MetadataOsdAutohideStatusTimeout)] = 136,
            [nameof(AppSettings.MetadataOsdShowAlbumTrack)] = 137,
            [nameof(AppSettings.MetadataOsdMessageMaxLength)] = 138,
            [nameof(AppSettings.ScreenshotDirectory)] = 139,
            [nameof(AppSettings.ScreenshotTemplate)] = 140,
            [nameof(AppSettings.ScreenshotFormat)] = 141,
            [nameof(AppSettings.ScreenshotJpegQuality)] = 142,
            [nameof(AppSettings.ScreenshotJpegSourceChroma)] = 143,
            [nameof(AppSettings.ScreenshotPngCompression)] = 144,
            [nameof(AppSettings.ScreenshotPngFilter)] = 145,
            [nameof(AppSettings.ScreenshotWebpQuality)] = 146,
            [nameof(AppSettings.ScreenshotWebpLossless)] = 147,
            [nameof(AppSettings.ScreenshotWebpCompression)] = 148,
            [nameof(AppSettings.ScreenshotJxlDistance)] = 149,
            [nameof(AppSettings.ScreenshotJxlEffort)] = 150,
            [nameof(AppSettings.ScreenshotAvifEncoder)] = 151,
            [nameof(AppSettings.ScreenshotHighBitDepth)] = 152,
            [nameof(AppSettings.ScreenshotTagColorspace)] = 153,
            [nameof(AppSettings.ScreenshotSw)] = 154,
            [nameof(AppSettings.Scale)] = 155,
            [nameof(AppSettings.DScale)] = 156,
            [nameof(AppSettings.Cscale)] = 157,
            [nameof(AppSettings.Tscale)] = 158,
            [nameof(AppSettings.LinearUpscaling)] = 159,
            [nameof(AppSettings.SigmoidUpscaling)] = 160,
            [nameof(AppSettings.LinearDownscaling)] = 161,
            [nameof(AppSettings.CorrectDownscaling)] = 162,
            [nameof(AppSettings.Deband)] = 163,
            [nameof(AppSettings.Dither)] = 164,
            [nameof(AppSettings.DitherDepth)] = 165,
            [nameof(AppSettings.ToneMapping)] = 166,
            [nameof(AppSettings.TargetColorspaceHint)] = 167,
            [nameof(AppSettings.TargetColorspaceHintMode)] = 168,
            [nameof(AppSettings.TargetColorspaceHintStrict)] = 169,
            [nameof(AppSettings.TargetPrim)] = 170,
            [nameof(AppSettings.TargetTrc)] = 171,
            [nameof(AppSettings.TargetPeak)] = 172,
            [nameof(AppSettings.GamutMappingMode)] = 173,
            [nameof(AppSettings.IccProfileAuto)] = 174,
            [nameof(AppSettings.IccProfile)] = 175,
            [nameof(AppSettings.IccForceContrast)] = 176,
            [nameof(AppSettings.Icc3dlutSize)] = 177,
            [nameof(AppSettings.IccCache)] = 178,
            [nameof(AppSettings.IccCacheDir)] = 179,
            [nameof(AppSettings.D3d11OutputCsp)] = 180,
            [nameof(AppSettings.Interpolation)] = 181,
            [nameof(AppSettings.BackgroundTileColor0)] = 182,
            [nameof(AppSettings.BackgroundTileColor1)] = 183,
            [nameof(AppSettings.BackgroundTileSize)] = 184,
            [nameof(AppSettings.D3d11ExclusiveFs)] = 185,
            [nameof(AppSettings.D3d11Flip)] = 186,
            [nameof(AppSettings.D3d11Adapter)] = 187,
            [nameof(AppSettings.GpuShaderCache)] = 188,
            [nameof(AppSettings.GpuShaderCacheDir)] = 189,
            [nameof(AppSettings.GlslShadersAppend)] = 190,
            [nameof(AppSettings.VideoSync)] = 191,
            [nameof(AppSettings.VideoSyncMaxVideoChange)] = 192,
        };

        var sectionOrder = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [sProgramInterface] = 0,
            [sProgramLanguageLog] = 1,
            [sPlayback] = 2,
            [sPlaybackSeeking] = 3,
            [sPlaybackSeekPreview] = 4,
            [sWatchLaterResume] = 5,
            [sWatchLaterStorage] = 6,
            [sVideoDecode] = 7,
            [sVideoImage] = 8,
            [sVideoFilters] = 9,
            [sGpuScaling] = 10,
            [sGpuColor] = 11,
            [sGpuInterpolation] = 12,
            [sGpuBackground] = 13,
            [sGpuD3d11] = 14,
            [sGpuShaders] = 15,
            [sVideoSync] = 16,
            [sAudioOutput] = 17,
            [sAudioVolume] = 18,
            [sAudioExternal] = 19,
            [sAudioCoverArt] = 20,
            [sTrackLanguage] = 21,
            [sTrackFallback] = 22,
            [sSubtitleText] = 23,
            [sSubtitleAss] = 24,
            [sSubtitleImage] = 25,
            [sWindow] = 26,
            [sDemuxerPlaylist] = 27,
            [sDemuxerBuffering] = 28,
            [sCache] = 29,
            [sProgramNetwork] = 30,
            [sInput] = 31,
            [sOsd] = 32,
            [sOsdMetadata] = 33,
            [sScreenshotLocation] = 34,
            [sScreenshotQuality] = 35,
            [sProgramTesting] = 36,
        };

        var sectionMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // program
            [nameof(AppSettings.ThemeType)] = sProgramInterface,
            [nameof(AppSettings.BackdropType)] = sProgramInterface,
            [nameof(AppSettings.ThemeAccentColor)] = sProgramInterface,
            [nameof(AppSettings.ThemeOpacity)] = sProgramInterface,
            [nameof(AppSettings.ThemeLuminosity)] = sProgramInterface,
            [nameof(AppSettings.UiFont)] = sProgramInterface,
            [nameof(AppSettings.CurrentLanguage)] = sProgramLanguageLog,
            [nameof(AppSettings.EnableDebugLog)] = sProgramLanguageLog,
            // playback
            [nameof(AppSettings.LoopFile)] = sPlayback,
            [nameof(AppSettings.LoopPlaylist)] = sPlayback,
            [nameof(AppSettings.Speed)] = sPlayback,
            [nameof(AppSettings.HrSeek)] = sPlaybackSeeking,
            [nameof(AppSettings.HrSeekFramedrop)] = sPlaybackSeeking,
            [nameof(AppSettings.SeekHoldEnabled)] = sPlaybackSeeking,
            [nameof(AppSettings.EnableVideoPreview)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbfastQuality)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbfastNetwork)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbfastMinDuration)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbfastPrecise)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbfastMaxWidth)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbfastMaxHeight)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbfastSpawnFirst)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbfastThreads)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbfastFrequency)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbfastDirectIo)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbfastQuitAfterInactivity)] = sPlaybackSeekPreview,
            // watchLater
            [nameof(AppSettings.SavePositionOnQuit)] = sWatchLaterResume,
            [nameof(AppSettings.ResumePlayback)] = sWatchLaterResume,
            [nameof(AppSettings.WatchLaterOptions)] = sWatchLaterStorage,
            [nameof(AppSettings.WatchLaterDir)] = sWatchLaterStorage,
            // video
            [nameof(AppSettings.Hwdec)] = sVideoDecode,
            [nameof(AppSettings.HwdecCodecs)] = sVideoDecode,
            [nameof(AppSettings.VideoDecodeDirect)] = sVideoDecode,
            [nameof(AppSettings.Deinterlace)] = sVideoImage,
            [nameof(AppSettings.VideoRotate)] = sVideoImage,
            [nameof(AppSettings.AspectRatio)] = sVideoImage,
            [nameof(AppSettings.Panscan)] = sVideoImage,
            [nameof(AppSettings.VideoUnscaled)] = sVideoImage,
            [nameof(AppSettings.VideoOutputLevels)] = sVideoImage,
            [nameof(AppSettings.HdrAutoMode)] = sVideoFilters,
            [nameof(AppSettings.HdrAutoLog)] = sVideoFilters,
            [nameof(AppSettings.VsrAutoEnabled)] = sVideoFilters,
            [nameof(AppSettings.Scale)] = sGpuScaling,
            [nameof(AppSettings.DScale)] = sGpuScaling,
            [nameof(AppSettings.Cscale)] = sGpuScaling,
            [nameof(AppSettings.Tscale)] = sGpuScaling,
            [nameof(AppSettings.LinearUpscaling)] = sGpuScaling,
            [nameof(AppSettings.SigmoidUpscaling)] = sGpuScaling,
            [nameof(AppSettings.LinearDownscaling)] = sGpuScaling,
            [nameof(AppSettings.CorrectDownscaling)] = sGpuScaling,
            [nameof(AppSettings.Deband)] = sGpuScaling,
            [nameof(AppSettings.Dither)] = sGpuScaling,
            [nameof(AppSettings.DitherDepth)] = sGpuScaling,
            [nameof(AppSettings.ToneMapping)] = sGpuColor,
            [nameof(AppSettings.TargetColorspaceHint)] = sGpuColor,
            [nameof(AppSettings.TargetColorspaceHintMode)] = sGpuColor,
            [nameof(AppSettings.TargetColorspaceHintStrict)] = sGpuColor,
            [nameof(AppSettings.TargetPrim)] = sGpuColor,
            [nameof(AppSettings.TargetTrc)] = sGpuColor,
            [nameof(AppSettings.TargetPeak)] = sGpuColor,
            [nameof(AppSettings.GamutMappingMode)] = sGpuColor,
            [nameof(AppSettings.IccProfileAuto)] = sGpuColor,
            [nameof(AppSettings.IccProfile)] = sGpuColor,
            [nameof(AppSettings.IccForceContrast)] = sGpuColor,
            [nameof(AppSettings.Icc3dlutSize)] = sGpuColor,
            [nameof(AppSettings.IccCache)] = sGpuColor,
            [nameof(AppSettings.IccCacheDir)] = sGpuColor,
            [nameof(AppSettings.D3d11OutputCsp)] = sGpuColor,
            [nameof(AppSettings.Interpolation)] = sGpuInterpolation,
            [nameof(AppSettings.BackgroundTileColor0)] = sGpuBackground,
            [nameof(AppSettings.BackgroundTileColor1)] = sGpuBackground,
            [nameof(AppSettings.BackgroundTileSize)] = sGpuBackground,
            [nameof(AppSettings.D3d11ExclusiveFs)] = sGpuD3d11,
            [nameof(AppSettings.D3d11Flip)] = sGpuD3d11,
            [nameof(AppSettings.D3d11Adapter)] = sGpuD3d11,
            [nameof(AppSettings.GpuShaderCache)] = sGpuShaders,
            [nameof(AppSettings.GpuShaderCacheDir)] = sGpuShaders,
            [nameof(AppSettings.GlslShadersAppend)] = sGpuShaders,
            [nameof(AppSettings.VideoSync)] = sVideoSync,
            [nameof(AppSettings.VideoSyncMaxVideoChange)] = sVideoSync,
            // audio
            [nameof(AppSettings.AudioDevice)] = sAudioOutput,
            [nameof(AppSettings.AudioExclusive)] = sAudioOutput,
            [nameof(AppSettings.AudioChannels)] = sAudioOutput,
            [nameof(AppSettings.AudioDelay)] = sAudioOutput,
            [nameof(AppSettings.AudioBuffer)] = sAudioOutput,
            [nameof(AppSettings.AudioWaitOpen)] = sAudioOutput,
            [nameof(AppSettings.AudioPitchCorrection)] = sAudioOutput,
            [nameof(AppSettings.AudioNormalizeDownmix)] = sAudioOutput,
            [nameof(AppSettings.AudioGapless)] = sAudioOutput,
            [nameof(AppSettings.Volume)] = sAudioVolume,
            [nameof(AppSettings.VolumeMax)] = sAudioVolume,
            [nameof(AppSettings.AudioFileAuto)] = sAudioExternal,
            [nameof(AppSettings.AudioExts)] = sAudioExternal,
            [nameof(AppSettings.AudioFilePaths)] = sAudioExternal,
            [nameof(AppSettings.AudioDisplay)] = sAudioCoverArt,
            [nameof(AppSettings.CoverArtPreferEmbedded)] = sAudioCoverArt,
            [nameof(AppSettings.CoverArtAlwaysScan)] = sAudioCoverArt,
            [nameof(AppSettings.CoverArtLoadFromFilesystem)] = sAudioCoverArt,
            [nameof(AppSettings.CoverArtPreload)] = sAudioCoverArt,
            [nameof(AppSettings.CoverArtNames)] = sAudioCoverArt,
            [nameof(AppSettings.CoverArtImageExts)] = sAudioCoverArt,
            // subtitles
            [nameof(AppSettings.AudioLanguage)] = sTrackLanguage,
            [nameof(AppSettings.SubtitleLanguage)] = sTrackLanguage,
            [nameof(AppSettings.SubFallback)] = sTrackFallback,
            [nameof(AppSettings.SubFontSize)] = sSubtitleText,
            [nameof(AppSettings.SubFont)] = sSubtitleText,
            [nameof(AppSettings.SubFontFile)] = sSubtitleText,
            [nameof(AppSettings.SubFontProvider)] = sSubtitleText,
            [nameof(AppSettings.SubCodePage)] = sSubtitleText,
            [nameof(AppSettings.SubColor)] = sSubtitleText,
            [nameof(AppSettings.SubBackColor)] = sSubtitleText,
            [nameof(AppSettings.SubBorderColor)] = sSubtitleText,
            [nameof(AppSettings.SubOutlineSize)] = sSubtitleText,
            [nameof(AppSettings.SubShadowOffset)] = sSubtitleText,
            [nameof(AppSettings.SubBlur)] = sSubtitleText,
            [nameof(AppSettings.SubPos)] = sSubtitleText,
            [nameof(AppSettings.SubDelay)] = sSubtitleText,
            [nameof(AppSettings.SubScaleSigns)] = sSubtitleText,
            [nameof(AppSettings.SubUseMargins)] = sSubtitleText,
            [nameof(AppSettings.SubAuto)] = sSubtitleText,
            [nameof(AppSettings.SubFilePaths)] = sSubtitleText,
            [nameof(AppSettings.SubHdrPeak)] = sSubtitleText,
            [nameof(AppSettings.SubAssOverride)] = sSubtitleAss,
            [nameof(AppSettings.SubAssStyleOverrides)] = sSubtitleAss,
            [nameof(AppSettings.SubAssForceMargins)] = sSubtitleAss,
            [nameof(AppSettings.SubAssScaleWithWindow)] = sSubtitleAss,
            [nameof(AppSettings.SubAssUseVideoData)] = sSubtitleAss,
            [nameof(AppSettings.SubAssVideoAspectOverride)] = sSubtitleAss,
            [nameof(AppSettings.SubAssVsfilterColorCompat)] = sSubtitleAss,
            [nameof(AppSettings.SubEmbeddedFonts)] = sSubtitleAss,
            [nameof(AppSettings.BlendSubtitles)] = sSubtitleAss,
            [nameof(AppSettings.StretchImageSubsToScreen)] = sSubtitleImage,
            [nameof(AppSettings.ImageSubsVideoResolution)] = sSubtitleImage,
            [nameof(AppSettings.ImageSubsHdrPeak)] = sSubtitleImage,
            // window
            [nameof(AppSettings.AlwaysOnTop)] = sWindow,
            [nameof(AppSettings.KeepOpen)] = sWindow,
            // demuxer
            [nameof(AppSettings.AutoCreatePlaylist)] = sDemuxerPlaylist,
            [nameof(AppSettings.DirectoryMode)] = sDemuxerPlaylist,
            [nameof(AppSettings.DirectoryFilterTypes)] = sDemuxerPlaylist,
            [nameof(AppSettings.VideoExts)] = sDemuxerPlaylist,
            [nameof(AppSettings.ImageExts)] = sDemuxerPlaylist,
            [nameof(AppSettings.DemuxerMaxBytes)] = sDemuxerBuffering,
            [nameof(AppSettings.DemuxerMaxBackBytes)] = sDemuxerBuffering,
            [nameof(AppSettings.DemuxerReadahead)] = sDemuxerBuffering,
            // cache
            [nameof(AppSettings.CacheEnabled)] = sCache,
            [nameof(AppSettings.CacheSecs)] = sCache,
            [nameof(AppSettings.CacheOnDisk)] = sCache,
            [nameof(AppSettings.CacheDirectory)] = sCache,
            // network
            [nameof(AppSettings.Ytdl)] = sProgramNetwork,
            [nameof(AppSettings.YtdlRawOptionsAppend)] = sProgramNetwork,
            // input
            [nameof(AppSettings.InputIme)] = sInput,
            [nameof(AppSettings.InputIpcServer)] = sInput,
            // shortcuts
            // osd
            [nameof(AppSettings.OsdFontSize)] = sOsd,
            [nameof(AppSettings.OsdFont)] = sOsd,
            [nameof(AppSettings.OsdColor)] = sOsd,
            [nameof(AppSettings.OsdOutlineColor)] = sOsd,
            [nameof(AppSettings.OsdOnSeek)] = sOsd,
            [nameof(AppSettings.OsdDuration)] = sOsd,
            [nameof(AppSettings.OsdPlayingMsg)] = sOsd,
            [nameof(AppSettings.OsdPlayingMsgDuration)] = sOsd,
            [nameof(AppSettings.OsdBarWidth)] = sOsd,
            [nameof(AppSettings.OsdBarHeight)] = sOsd,
            [nameof(AppSettings.OsdBlur)] = sOsd,
            [nameof(AppSettings.OsdOutlineSize)] = sOsd,
            [nameof(AppSettings.OsdFractions)] = sOsd,
            [nameof(AppSettings.ShowOsdPlayingMsg)] = sOsd,
            [nameof(AppSettings.MetadataOsdEnabled)] = sOsdMetadata,
            [nameof(AppSettings.MetadataOsdAutohideTimeout)] = sOsdMetadata,
            [nameof(AppSettings.MetadataOsdShowChapter)] = sOsdMetadata,
            [nameof(AppSettings.MetadataOsdEnableForVideo)] = sOsdMetadata,
            [nameof(AppSettings.MetadataOsdEnableForImage)] = sOsdMetadata,
            [nameof(AppSettings.MetadataOsdAutohideStatusTimeout)] = sOsdMetadata,
            [nameof(AppSettings.MetadataOsdShowAlbumTrack)] = sOsdMetadata,
            [nameof(AppSettings.MetadataOsdMessageMaxLength)] = sOsdMetadata,
            // screenshot
            [nameof(AppSettings.ScreenshotDirectory)] = sScreenshotLocation,
            [nameof(AppSettings.ScreenshotTemplate)] = sScreenshotLocation,
            [nameof(AppSettings.ScreenshotFormat)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotJpegQuality)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotJpegSourceChroma)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotPngCompression)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotPngFilter)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotWebpQuality)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotWebpLossless)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotWebpCompression)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotJxlDistance)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotJxlEffort)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotAvifEncoder)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotHighBitDepth)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotTagColorspace)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotSw)] = sScreenshotQuality,
            // testing
            [nameof(AppSettings.TestMpvCommandLog)] = sProgramTesting,
            [nameof(AppSettings.TestOsdMessage)] = sProgramTesting,
            [nameof(AppSettings.TestSignal)] = sProgramTesting,
        };

        foreach (var option in options)
        {
            if (categoryMap.TryGetValue(option.Key, out var category))
            {
                option.Category = category;
            }
            if (sectionMap.TryGetValue(option.Key, out var section))
            {
                option.Section = section;
            }
        }

        var categoryOrderIndex = categoryOrder
            .Select((name, index) => (name, index))
            .ToDictionary(x => x.name, x => x.index, StringComparer.Ordinal);

        // Cluster options by (category, section) in the official manual order,
        // so each section is contiguous and appears in a predictable layout.
        var ordered = options
            .Select((o, i) => (o, i))
            .GroupBy(x => (x.o.Category, x.o.Section ?? string.Empty))
            .OrderBy(g => categoryOrderIndex.TryGetValue(g.Key.Category, out var categoryIndex) ? categoryIndex : int.MaxValue)
            .ThenBy(g => sectionOrder.TryGetValue(g.Key.Item2, out var sectionIndex) ? sectionIndex : int.MaxValue)
            .ThenBy(g => g.Min(x => optionOrder.TryGetValue(x.o.Key, out var optionIndex) ? optionIndex : int.MaxValue))
            .SelectMany(g => g.OrderBy(x => optionOrder.TryGetValue(x.o.Key, out var optionIndex) ? optionIndex : int.MaxValue).Select(x => x.o))
            .ToList();
        options = ordered;

        var seenSections = new HashSet<(string Category, string Section)>();
        foreach (var option in options)
        {
            if (!string.IsNullOrEmpty(option.Section)
                && seenSections.Add((option.Category, option.Section)))
            {
                option.ShowSectionHeader = true;
            }
        }

        CategoryOrder.AddRange(categoryOrder);

        return options;
    }

    /// <summary>Options whose presets cover every legal value; the list control must not add a "Custom" entry.</summary>
    private static readonly System.Collections.Generic.HashSet<string> NoCustomOptions = new(StringComparer.Ordinal)
    {
        nameof(AppContext.AppSetting.ThemeType),
        nameof(AppContext.AppSetting.BackdropType),
        nameof(AppContext.AppSetting.KeepOpen),
        nameof(AppContext.AppSetting.LoopPlaylist),
        nameof(AppContext.AppSetting.CacheEnabled),
        nameof(AppContext.AppSetting.DirectoryMode),
        nameof(AppContext.AppSetting.Deinterlace),
        nameof(AppContext.AppSetting.VideoDecodeDirect),
        nameof(AppContext.AppSetting.VideoUnscaled),
        nameof(AppContext.AppSetting.VideoRotate),
        nameof(AppContext.AppSetting.VideoOutputLevels),
        nameof(AppContext.AppSetting.DitherDepth),
        nameof(AppContext.AppSetting.AudioFileAuto),
        nameof(AppContext.AppSetting.AudioGapless),
        nameof(AppContext.AppSetting.AudioDisplay),
        nameof(AppContext.AppSetting.SubAuto),
        nameof(AppContext.AppSetting.SubAssOverride),
        nameof(AppContext.AppSetting.SubAssUseVideoData),
        nameof(AppContext.AppSetting.SubAssVsfilterColorCompat),
        nameof(AppContext.AppSetting.BlendSubtitles),
        nameof(AppContext.AppSetting.SubFallback),
        nameof(AppContext.AppSetting.ScreenshotFormat),
        nameof(AppContext.AppSetting.D3d11OutputCsp),
        nameof(AppContext.AppSetting.TargetColorspaceHint),
        nameof(AppContext.AppSetting.TargetColorspaceHintMode),
        nameof(AppContext.AppSetting.OsdOnSeek),
        nameof(AppContext.AppSetting.HdrAutoMode),
        nameof(AppContext.AppSetting.ThumbfastQuality),
        nameof(AppContext.AppSetting.ThumbfastPrecise),
    };

    /// <summary>Options whose help text only restates the title (Windows Settings style: no redundant description).</summary>
    private static readonly System.Collections.Generic.HashSet<string> RedundantDescriptions = new(StringComparer.Ordinal)
    {
        nameof(AppContext.AppSetting.AudioLanguage),
        nameof(AppContext.AppSetting.SubtitleLanguage),
        nameof(AppContext.AppSetting.KeepOpen),
        nameof(AppContext.AppSetting.LoopFile),
        nameof(AppContext.AppSetting.LoopPlaylist),
        nameof(AppContext.AppSetting.Volume),
        nameof(AppContext.AppSetting.Speed),
        nameof(AppContext.AppSetting.Deinterlace),
        nameof(AppContext.AppSetting.AspectRatio),
        nameof(AppContext.AppSetting.CorrectDownscaling),
        nameof(AppContext.AppSetting.VideoRotate),
        nameof(AppContext.AppSetting.DitherDepth),
        nameof(AppContext.AppSetting.AudioChannels),
        nameof(AppContext.AppSetting.AudioPitchCorrection),
        nameof(AppContext.AppSetting.AudioNormalizeDownmix),
        nameof(AppContext.AppSetting.AudioFileAuto),
        nameof(AppContext.AppSetting.CacheEnabled),
        nameof(AppContext.AppSetting.DirectoryMode),
        nameof(AppContext.AppSetting.AutoCreatePlaylist),
        nameof(AppContext.AppSetting.SubAuto),
        nameof(AppContext.AppSetting.HdrAutoMode),
        nameof(AppContext.AppSetting.AudioDisplay),
        nameof(AppContext.AppSetting.SubAssUseVideoData),
        nameof(AppContext.AppSetting.SubAssVsfilterColorCompat),
        nameof(AppContext.AppSetting.TargetColorspaceHintMode),
        nameof(AppContext.AppSetting.OsdOnSeek),
        nameof(AppContext.AppSetting.SubFontSize),
        nameof(AppContext.AppSetting.SubDelay),
        nameof(AppContext.AppSetting.SubPos),
        nameof(AppContext.AppSetting.SubBlur),
        nameof(AppContext.AppSetting.SubAuto),
        nameof(AppContext.AppSetting.SubFont),
        nameof(AppContext.AppSetting.SubCodePage),
        nameof(AppContext.AppSetting.SubOutlineSize),
        nameof(AppContext.AppSetting.SubShadowOffset),
        nameof(AppContext.AppSetting.SavePositionOnQuit),
        nameof(AppContext.AppSetting.ScreenshotDirectory),
        nameof(AppContext.AppSetting.ScreenshotFormat),
        nameof(AppContext.AppSetting.ScreenshotTagColorspace),
    };

    private static List<OptionChoice> SubtitleFontChoices(AppLang lang)
    {
        var list = new List<OptionChoice>
        {
            new("sans-serif", lang.OptionValueFontDefault),
        };

        void Add(string value, string label)
        {
            if (!list.Any(c => c.Value == value))
            {
                list.Add(new OptionChoice(value, label));
            }
        }

        // Lead with the common fonts of the current UI language, so each
        // language gets its own familiar font set (and default).
        switch (AppContext.AppSetting.CurrentLanguage)
        {
            case "zh-CN":
                Add("Microsoft YaHei", "微软雅黑");
                Add("SimSun", "宋体");
                Add("DengXian", "等线");
                Add("SimHei", "黑体");
                Add("KaiTi", "楷体");
                break;
            case "ja-JP":
                Add("Yu Gothic UI", "Yu Gothic UI");
                Add("Yu Gothic", "Yu Gothic");
                Add("Meiryo", "Meiryo");
                Add("MS Gothic", "MS Gothic");
                Add("MS PGothic", "MS PGothic");
                break;
            case "ko-KR":
                Add("Malgun Gothic", "맑은 고딕");
                Add("Gulim", "굴림");
                Add("Batang", "바탕");
                Add("Dotum", "돋움");
                break;
            case "ru-RU":
                Add("Segoe UI", "Segoe UI");
                Add("Arial", "Arial");
                Add("Times New Roman", "Times New Roman");
                Add("Georgia", "Georgia");
                break;
            default:
                Add("Segoe UI", "Segoe UI");
                Add("Arial", "Arial");
                Add("Calibri", "Calibri");
                Add("Times New Roman", "Times New Roman");
                Add("Verdana", "Verdana");
                Add("Georgia", "Georgia");
                break;
        }

        if (AppContext.AppSetting.SubFontFile is { Length: > 0 } fontFile && File.Exists(fontFile))
        {
            var fontDir = Path.GetDirectoryName(fontFile);
            if (!string.IsNullOrWhiteSpace(fontDir) && Directory.Exists(fontDir))
            {
                foreach (var file in Directory.GetFiles(fontDir).Where(f =>
                             f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                             || f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)
                             || f.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase)))
                {
                    Add(Path.GetFileNameWithoutExtension(file), Path.GetFileNameWithoutExtension(file));
                }
            }
        }

        Add("Consolas", "Consolas");
        Add("Source Han Sans SC", "Source Han Sans SC");
        Add("LXGW WenKai Mono Lite", "LXGW WenKai Mono Lite");
        return list;
    }

    /// <summary>Builds a read-only shortcut list from the deployed input.conf.</summary>
    private static List<Option> BuildShortcutOptions(string shortcutsCategory)
    {
        var options = new List<Option>();
        var path = AppData.Current.ResolveLocalData(Path.Combine("mpv", "input.conf"));
        if (!File.Exists(path))
        {
            return options;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var hash = line.IndexOf('#');
            var binding = (hash >= 0 ? line[..hash] : line).Trim();
            var comment = hash >= 0 ? line[hash..].TrimStart('#').Trim() : string.Empty;
            var parts = binding.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || parts[0].Contains('='))
            {
                continue;
            }

            var key = parts[0];
            if (!seen.Add(key))
            {
                continue;
            }

            var command = string.Join(' ', parts.Skip(1));
            options.Add(new Option
            {
                Key = $"Shortcut:{index++}",
                Label = string.IsNullOrEmpty(comment) ? command : comment,
                Category = shortcutsCategory,
                Type = OptionType.String,
                ReadOnly = true,
                Getter = () => key,
                Setter = _ => { },
            });

            if (options.Count >= 240)
            {
                break;
            }
        }
        return options;
    }

    private static List<OptionChoice> LanguageChoices(bool includeAuto)
    {
        var codes = new[]
        {
            "eng", "chi", "jpn", "kor", "deu", "fra", "spa", "rus", "ita", "por",
            "ara", "hin", "tha", "vie", "ind", "tur", "nld", "pol", "swe", "dan",
            "nor", "fin", "ces", "hun", "ukr", "ell", "ron", "bul",
        };

        var list = new List<OptionChoice>();
        if (includeAuto)
        {
            list.Add(new OptionChoice("", AppContext.AppLang.OptionValueAuto));
        }
        list.AddRange(codes.Select(code => new OptionChoice(code, AppLang.LanguageCodeName(code))));
        return list;
    }

    private static List<OptionChoice> BuildAudioDeviceChoices()
    {
        var choices = new List<OptionChoice>
        {
            new("auto", AppContext.AppLang.OptionValueAuto),
        };

        try
        {
            var devices = AppContext.GetAudioDevices?.Invoke();
            if (devices is not null)
            {
                foreach (var device in devices)
                {
                    var label = string.IsNullOrWhiteSpace(device.Description) ? device.Name : device.Description;
                    choices.Add(new OptionChoice(device.Name, label));
                }
            }
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Warn(ex, "Failed to enumerate audio devices");
        }

        return choices;
    }

    private void ApplyMpv(string key, object value)
    {
        if (MpvSettings.ToCommand(key, value) is { } cmd)
        {
            AppContext.SendMpvCommand(cmd);
        }
        RefreshWarningsAndEnabled();
    }

    /// <summary>Re-evaluates yellow warnings and disabled states after any option changes.</summary>
    private void RefreshWarningsAndEnabled()
    {
        var s = AppContext.AppSetting;
        foreach (var option in Settings)
        {
            option.Warning = ComputeWarning(option, s);
            option.IsEnabled = ComputeEnabled(option, s);
            option.IsVisible = ComputeVisible(option, s);
        }
        OptionsControl.Refresh();
    }

    private static string? ComputeWarning(Option option, AppSettings s)
    {
        var lang = AppContext.AppLang;
        return option.Key switch
        {
            nameof(AppSettings.Interpolation) when s.VideoSync != "display-resample" => lang.WarningInterpolationVideoSync,
            nameof(AppSettings.Tscale) when !s.Interpolation => lang.WarningTscaleInterpolation,
            nameof(AppSettings.HrSeekFramedrop) when s.Interpolation => lang.WarningHrSeekFramedrop,
            nameof(AppSettings.Deband) when s.Hwdec != "no" => lang.WarningDebandHwdec,
            nameof(AppSettings.SigmoidUpscaling) when s.LinearUpscaling => lang.WarningLinearUpscalingSigmoid,
            nameof(AppSettings.SavePositionOnQuit) when !s.ResumePlayback => lang.WarningSaveWithoutResume,
            nameof(AppSettings.SubUseMargins) when s.BlendSubtitles != "no" => lang.WarningBlendSubtitlesMargins,
            nameof(AppSettings.SubAssForceMargins) when s.BlendSubtitles != "no" => lang.WarningBlendSubtitlesMargins,
            nameof(AppSettings.SubFallback) when string.IsNullOrWhiteSpace(s.SubtitleLanguage) => lang.WarningSubFallbackNoLanguage,
            nameof(AppSettings.SeekHoldEnabled) when !s.VsrAutoEnabled && s.HdrAutoMode == "off" => lang.WarningSeekHoldInactive,
            _ => null,
        };
    }

    private static bool ComputeVisible(Option option, AppSettings s)
    {
        return option.Key switch
        {
            // Backdrop tint/transparency/brightness only apply to Acrylic.
            nameof(AppSettings.ThemeAccentColor) when s.BackdropType != AppSettings.BackdropType_Acrylic => false,
            nameof(AppSettings.ThemeOpacity) when s.BackdropType != AppSettings.BackdropType_Acrylic => false,
            nameof(AppSettings.ThemeLuminosity) when s.BackdropType != AppSettings.BackdropType_Acrylic => false,
            // Format-specific screenshot options only appear for the active format.
            nameof(AppSettings.ScreenshotJpegQuality) when s.ScreenshotFormat != "jpg" => false,
            nameof(AppSettings.ScreenshotJpegSourceChroma) when s.ScreenshotFormat != "jpg" => false,
            nameof(AppSettings.ScreenshotPngCompression) when s.ScreenshotFormat != "png" => false,
            nameof(AppSettings.ScreenshotPngFilter) when s.ScreenshotFormat != "png" => false,
            nameof(AppSettings.ScreenshotWebpQuality) when s.ScreenshotFormat != "webp" => false,
            nameof(AppSettings.ScreenshotWebpLossless) when s.ScreenshotFormat != "webp" => false,
            nameof(AppSettings.ScreenshotWebpCompression) when s.ScreenshotFormat != "webp" => false,
            nameof(AppSettings.ScreenshotJxlDistance) when s.ScreenshotFormat != "jxl" => false,
            nameof(AppSettings.ScreenshotJxlEffort) when s.ScreenshotFormat != "jxl" => false,
            nameof(AppSettings.ScreenshotAvifEncoder) when s.ScreenshotFormat != "avif" => false,
            nameof(AppSettings.ScreenshotHighBitDepth) when s.ScreenshotFormat is not ("png" or "webp") => false,
            nameof(AppSettings.ScreenshotTagColorspace) when s.ScreenshotFormat == "jpg" => false,
            _ => true,
        };
    }

    private static bool ComputeEnabled(Option option, AppSettings s)
    {
        return option.Key switch
        {
            nameof(AppSettings.OsdPlayingMsg) when !s.ShowOsdPlayingMsg => false,
            // mpv: sub-ass-force-margins is ignored when blend-subtitles=yes/video.
            nameof(AppSettings.SubAssForceMargins) when s.BlendSubtitles != "no" => false,
            // mpv: linear-upscaling and sigmoid-upscaling are mutually exclusive.
            nameof(AppSettings.LinearUpscaling) when s.SigmoidUpscaling => false,
            _ => true,
        };
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
