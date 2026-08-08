using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.Language;
using mpv_winui.Modules.Settings.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

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
        RefreshWarningsAndEnabled();
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
        var assSection = AppContext.AppLang.OptionSectionAssAdvanced;
        var screenshot = AppContext.AppLang.SettingsCategoryScreenshot;
        var advanced = AppContext.AppLang.SettingsCategoryAdvanced;
        var lang = AppContext.AppLang;

        var options = new List<Option>
        {
            // ===== General / 常规 =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.ThemeType),
                Label = lang.AppSettingTheme,
                Category = general,
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
                Category = general,
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
                Key = nameof(AppContext.AppSetting.EnableDebugLog),
                Label = lang.DebugLog,
                Category = general,
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
                Category = general,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.AlwaysOnTop,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AlwaysOnTop), AppContext.AppSetting.AlwaysOnTop = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.InputIme),
                Label = lang.SettingsInputIme,
                Category = general,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.InputIme,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.InputIme), AppContext.AppSetting.InputIme = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CurrentLanguage),
                Label = lang.SettingLanguages,
                Category = general,
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

            // ===== Playback / 播放 =====
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
                Description = lang.SettingsHelpDirectoryMode,
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
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.BackgroundTileColor0,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.BackgroundTileColor0), AppContext.AppSetting.BackgroundTileColor0 = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.BackgroundTileColor1),
                Label = lang.SettingsBackgroundTileColor1,
                Category = video,
                Type = OptionType.String,
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
                Description = lang.SettingsHelpAudioLanguage,
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

            // ===== Subtitle / 字幕 =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubFontSize),
                Label = lang.SettingsSubFontSize,
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
                Label = lang.SettingsSubDelay,
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
                Label = lang.SettingsSubPos,
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
                Label = lang.SettingsSubtitleLanguage,
                Category = subtitle,
                Description = lang.SettingsHelpSubtitleLanguage,
                Type = OptionType.StringList,
                Choices = LanguageChoices(true),
                Getter = () => AppContext.AppSetting.SubtitleLanguage,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubtitleLanguage), AppContext.AppSetting.SubtitleLanguage = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubFilePaths),
                Label = lang.SettingsSubFilePaths,
                Category = subtitle,
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
                Category = subtitle,
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
                Category = subtitle,
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
                Category = subtitle,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.ImageSubsVideoResolution,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ImageSubsVideoResolution), AppContext.AppSetting.ImageSubsVideoResolution = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubColor),
                Label = lang.SettingsSubColor,
                Category = subtitle,
                Description = lang.SettingsHelpSubColor,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.SubColor,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubColor), AppContext.AppSetting.SubColor = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubBackColor),
                Label = lang.SettingsSubBackColor,
                Category = subtitle,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.SubBackColor,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubBackColor), AppContext.AppSetting.SubBackColor = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubBorderColor),
                Label = lang.SettingsSubBorderColor,
                Category = subtitle,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.SubBorderColor,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubBorderColor), AppContext.AppSetting.SubBorderColor = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubScaleSigns),
                Label = lang.SettingsSubScaleSigns,
                Category = subtitle,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SubScaleSigns,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubScaleSigns), AppContext.AppSetting.SubScaleSigns = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubAssOverride),
                Label = lang.SettingsSubAssOverride,
                Category = subtitle,
                Section = assSection,
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
                Category = subtitle,
                Section = assSection,
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
                Category = subtitle,
                Section = assSection,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.SubAssVideoAspectOverride,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubAssVideoAspectOverride), AppContext.AppSetting.SubAssVideoAspectOverride = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubAssVsfilterColorCompat),
                Label = lang.SettingsSubAssVsfilterColorCompat,
                Category = subtitle,
                Section = assSection,
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
                Category = subtitle,
                Section = assSection,
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
                Category = subtitle,
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
                Category = subtitle,
                Type = OptionType.StringList,
                Choices = SubtitleFontChoices(lang),
                Getter = () => AppContext.AppSetting.SubFont,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubFont), AppContext.AppSetting.SubFont = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubFontProvider),
                Label = lang.SettingsSubFontProvider,
                Category = subtitle,
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
                Category = subtitle,
                Type = OptionType.StringList,
                Choices =
                [
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
                Category = subtitle,
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
                Category = subtitle,
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
                Category = subtitle,
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
                Category = subtitle,
                Description = lang.SettingsHelpSubUseMargins,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SubUseMargins,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubUseMargins), AppContext.AppSetting.SubUseMargins = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubAssForceMargins),
                Label = lang.SettingsSubAssForceMargins,
                Category = subtitle,
                Section = assSection,
                Description = lang.SettingsHelpSubAssForceMargins,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SubAssForceMargins,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubAssForceMargins), AppContext.AppSetting.SubAssForceMargins = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubAssScaleWithWindow),
                Label = lang.SettingsSubAssScaleWithWindow,
                Category = subtitle,
                Section = assSection,
                Description = lang.SettingsHelpSubAssScaleWithWindow,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SubAssScaleWithWindow,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubAssScaleWithWindow), AppContext.AppSetting.SubAssScaleWithWindow = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubEmbeddedFonts),
                Label = lang.SettingsSubEmbeddedFonts,
                Category = subtitle,
                Section = assSection,
                Description = lang.SettingsHelpSubEmbeddedFonts,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SubEmbeddedFonts,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubEmbeddedFonts), AppContext.AppSetting.SubEmbeddedFonts = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.BlendSubtitles),
                Label = lang.SettingsBlendSubtitles,
                Category = subtitle,
                Section = assSection,
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
                Category = subtitle,
                Section = assSection,
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
                Category = subtitle,
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

            // ===== Advanced / 高级 =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.CacheOnDisk),
                Label = lang.SettingsCacheOnDisk,
                Category = advanced,
                Description = lang.SettingsHelpCacheOnDisk,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.CacheOnDisk,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.CacheOnDisk), AppContext.AppSetting.CacheOnDisk = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.VideoOutputLevels),
                Label = lang.SettingsVideoOutputLevels,
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.D3d11ExclusiveFs,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.D3d11ExclusiveFs), AppContext.AppSetting.D3d11ExclusiveFs = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.D3d11Flip),
                Label = lang.SettingsD3d11Flip,
                Category = advanced,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.D3d11Flip,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.D3d11Flip), AppContext.AppSetting.D3d11Flip = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.D3d11Adapter),
                Label = lang.SettingsD3d11Adapter,
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
                Description = lang.SettingsHelpIccProfileAuto,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.IccProfileAuto,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.IccProfileAuto), AppContext.AppSetting.IccProfileAuto = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.IccProfile),
                Label = lang.SettingsIccProfile,
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
                Description = lang.SettingsHelpIccCacheDir,
                Type = OptionType.String,
                AllowEmpty = true,
                PickFolder = true,
                OpenFolder = true,
                Getter = () => AppContext.AppSetting.IccCacheDir,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.IccCacheDir), AppContext.AppSetting.IccCacheDir = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.TargetColorspaceHint),
                Label = lang.SettingsTargetColorspaceHint,
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.TargetColorspaceHintStrict,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.TargetColorspaceHintStrict), AppContext.AppSetting.TargetColorspaceHintStrict = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.TargetPrim),
                Label = lang.SettingsTargetPrim,
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
                Description = lang.SettingsHelpIccCache,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.IccCache,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.IccCache), AppContext.AppSetting.IccCache = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.GpuShaderCache),
                Label = lang.SettingsGpuShaderCache,
                Category = advanced,
                Description = lang.SettingsHelpGpuShaderCache,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.GpuShaderCache,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.GpuShaderCache), AppContext.AppSetting.GpuShaderCache = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.GlslShadersAppend),
                Label = lang.SettingsGlslShadersAppend,
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
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
                Key = nameof(AppContext.AppSetting.OsdPlayingMsg),
                Label = lang.SettingsOsdPlayingMsg,
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.OsdFractions,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdFractions), AppContext.AppSetting.OsdFractions = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.OsdColor),
                Label = lang.SettingsOsdColor,
                Category = advanced,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.OsdColor,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdColor), AppContext.AppSetting.OsdColor = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.OsdOutlineColor),
                Label = lang.SettingsOsdOutlineColor,
                Category = advanced,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.OsdOutlineColor,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdOutlineColor), AppContext.AppSetting.OsdOutlineColor = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.VsrAutoEnabled),
                Label = lang.SettingsVsrAuto,
                Category = advanced,
                Description = lang.SettingsHelpVsrAuto,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.VsrAutoEnabled,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.VsrAutoEnabled), AppContext.AppSetting.VsrAutoEnabled = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.HdrAutoMode),
                Label = lang.SettingsHdrAutoMode,
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
                Description = lang.SettingsHelpSeekHold,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SeekHoldEnabled,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SeekHoldEnabled), AppContext.AppSetting.SeekHoldEnabled = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.MetadataOsdEnabled),
                Label = lang.SettingsMetadataOsdEnabled,
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
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
                Key = nameof(AppContext.AppSetting.ThumbfastQuality),
                Label = lang.SettingsThumbfastQuality,
                Category = advanced,
                Description = lang.SettingsHelpThumbfastQuality,
                Type = OptionType.Integer,
                Min = 1,
                Max = 3,
                Step = 1,
                Getter = () => (double)AppContext.AppSetting.ThumbfastQuality,
                Setter = v =>
                {
                    AppContext.AppSetting.ThumbfastQuality = Convert.ToInt32(v);
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ThumbfastNetwork),
                Label = lang.SettingsThumbfastNetwork,
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
                Description = lang.SettingsHelpThumbfastPrecise,
                Type = OptionType.Integer,
                Min = 0,
                Max = 2,
                Step = 1,
                Getter = () => (double)AppContext.AppSetting.ThumbfastPrecise,
                Setter = v =>
                {
                    AppContext.AppSetting.ThumbfastPrecise = Convert.ToInt32(v);
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ThumbfastMaxWidth),
                Label = lang.SettingsThumbfastMaxWidth,
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
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
                Category = advanced,
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
                Key = nameof(AppContext.AppSetting.InputIpcServer),
                Label = lang.SettingsInputIpcServer,
                Category = advanced,
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
                Category = advanced,
                Description = lang.SettingsHelpWatchLaterDir,
                Type = OptionType.String,
                AllowEmpty = true,
                PickFolder = true,
                OpenFolder = true,
                Getter = () => AppContext.AppSetting.WatchLaterDir,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.WatchLaterDir), AppContext.AppSetting.WatchLaterDir = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.GpuShaderCacheDir),
                Label = lang.SettingsGpuShaderCacheDir,
                Category = advanced,
                Description = lang.SettingsHelpGpuShaderCacheDir,
                Type = OptionType.String,
                AllowEmpty = true,
                PickFolder = true,
                OpenFolder = true,
                Getter = () => AppContext.AppSetting.GpuShaderCacheDir,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.GpuShaderCacheDir), AppContext.AppSetting.GpuShaderCacheDir = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CacheDirectory),
                Label = lang.SettingsCacheDir,
                Category = advanced,
                Description = lang.SettingsHelpCacheDirectory,
                Type = OptionType.String,
                AllowEmpty = true,
                PickFolder = true,
                OpenFolder = true,
                Getter = () => AppContext.AppSetting.CacheDirectory,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.CacheDirectory), AppContext.AppSetting.CacheDirectory = (string)v!)
            },
        };

        foreach (var option in options)
        {
            if (RedundantDescriptions.Contains(option.Key))
            {
                option.Description = null;
            }
        }

        return options;
    }

    /// <summary>Options whose help text only restates the title (Windows Settings style: no redundant description).</summary>
    private static readonly System.Collections.Generic.HashSet<string> RedundantDescriptions = new(StringComparer.Ordinal)
    {
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
        return
        [
            new OptionChoice("sans-serif", lang.OptionValueFontDefault),
            new OptionChoice("Segoe UI", "Segoe UI"),
            new OptionChoice("Microsoft YaHei", "Microsoft YaHei"),
            new OptionChoice("Arial", "Arial"),
            new OptionChoice("Times New Roman", "Times New Roman"),
            new OptionChoice("Consolas", "Consolas"),
            new OptionChoice("Source Han Sans SC", "Source Han Sans SC"),
            new OptionChoice("LXGW WenKai Mono Lite", "LXGW WenKai Mono Lite"),
        ];
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
        }
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
            nameof(AppSettings.ScreenshotJpegQuality) when s.ScreenshotFormat != "jpg" => lang.WarningFormatJpeg,
            nameof(AppSettings.ScreenshotPngCompression) when s.ScreenshotFormat != "png" => lang.WarningFormatPng,
            nameof(AppSettings.ScreenshotWebpQuality) when s.ScreenshotFormat != "webp" => lang.WarningFormatWebp,
            nameof(AppSettings.ScreenshotHighBitDepth) when s.ScreenshotFormat is not ("png" or "webp") => lang.WarningHighBitDepthFormat,
            nameof(AppSettings.SeekHoldEnabled) when !s.VsrAutoEnabled && s.HdrAutoMode == "off" => lang.WarningSeekHoldInactive,
            _ => null,
        };
    }

    private static bool ComputeEnabled(Option option, AppSettings s)
    {
        return option.Key switch
        {
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
