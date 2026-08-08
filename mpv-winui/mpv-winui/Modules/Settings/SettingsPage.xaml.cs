using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.Language;
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
        var screenshot = AppContext.AppLang.SettingsCategoryScreenshot;
        var paths = AppContext.AppLang.SettingsCategoryPaths;
        var lang = AppContext.AppLang;

        return
        [
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
                Setter = v => AppContext.AppSetting.BackdropType = (string)v
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.EnableDebugLog),
                Label = lang.DebugLog,
                Category = general,
                Description = lang.SettingsHelpDebugLog,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.EnableDebugLog,
                Setter = v => AppContext.AppSetting.EnableDebugLog = (bool)v!
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CurrentLanguage),
                Label = lang.SettingLanguages,
                Category = general,
                Description = lang.SettingsHelpLanguage,
                RequiresRestart = true,
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
                    AppContext.AppSetting.CurrentLanguage = newLang;
                    PromptRestartIfNeeded(lang.SettingLanguages);
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
                Description = lang.SettingsHelpKeepOpen,
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
                Description = lang.SettingsHelpLoopFile,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.LoopFile,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.LoopFile), AppContext.AppSetting.LoopFile = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.LoopPlaylist),
                Label = lang.SettingsLoopPlaylist,
                Category = playback,
                Description = lang.SettingsHelpLoopPlaylist,
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
                Description = lang.SettingsHelpVolume,
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
                Description = lang.SettingsHelpSavePositionOnQuit,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SavePositionOnQuit,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SavePositionOnQuit), AppContext.AppSetting.SavePositionOnQuit = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.Speed),
                Label = lang.SettingsSpeed,
                Category = playback,
                Description = lang.SettingsHelpSpeed,
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
                Key = nameof(AppContext.AppSetting.EnableVideoPreview),
                Label = lang.SettingsVideoPreview,
                Category = playback,
                Description = lang.SettingsHelpVideoPreview,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.EnableVideoPreview,
                Setter = v => AppContext.AppSetting.EnableVideoPreview = (bool)v!
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

            // ===== Video / 视频 =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.Deinterlace),
                Label = lang.SettingsDeinterlace,
                Category = video,
                Description = lang.SettingsHelpDeinterlace,
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
                Description = lang.SettingsHelpAspect,
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
                Key = nameof(AppContext.AppSetting.CorrectDownscaling),
                Label = lang.SettingsCorrectDownscaling,
                Category = video,
                Description = lang.SettingsHelpCorrectDownscaling,
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
                Key = nameof(AppContext.AppSetting.VideoRotate),
                Label = lang.SettingsVideoRotate,
                Category = video,
                Description = lang.SettingsHelpVideoRotate,
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
                Description = lang.SettingsHelpDitherDepth,
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
                Description = lang.SettingsHelpAudioChannels,
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
                Description = lang.SettingsHelpAudioPitchCorrection,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.AudioPitchCorrection,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioPitchCorrection), AppContext.AppSetting.AudioPitchCorrection = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioNormalizeDownmix),
                Label = lang.SettingsAudioNormalizeDownmix,
                Category = audio,
                Description = lang.SettingsHelpAudioNormalizeDownmix,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.AudioNormalizeDownmix,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioNormalizeDownmix), AppContext.AppSetting.AudioNormalizeDownmix = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioFileAuto),
                Label = lang.SettingsAudioFileAuto,
                Category = audio,
                Description = lang.SettingsHelpAudioFileAuto,
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

            // ===== Subtitle / 字幕 =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubFontSize),
                Label = lang.SettingsSubFontSize,
                Category = subtitle,
                Description = lang.SettingsHelpSubFontSize,
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
                Description = lang.SettingsHelpSubDelay,
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
                Description = lang.SettingsHelpSubPos,
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
                Key = nameof(AppContext.AppSetting.SubAssOverride),
                Label = lang.SettingsSubAssOverride,
                Category = subtitle,
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
                Key = nameof(AppContext.AppSetting.SubBlur),
                Label = lang.SettingsSubBlur,
                Category = subtitle,
                Description = lang.SettingsHelpSubBlur,
                Type = OptionType.Double,
                Min = 0,
                Max = 20,
                Step = 0.5,
                Getter = () => AppContext.AppSetting.SubBlur,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubBlur), AppContext.AppSetting.SubBlur = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubAuto),
                Label = lang.SettingsSubAuto,
                Category = subtitle,
                Description = lang.SettingsHelpSubAuto,
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
                Description = lang.SettingsHelpSubFont,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("Source Han Sans SC", "Source Han Sans SC"),
                    new OptionChoice("LXGW WenKai Mono Lite", "LXGW WenKai Mono Lite"),
                    new OptionChoice("Segoe UI", "Segoe UI"),
                    new OptionChoice("Arial", "Arial"),
                    new OptionChoice("Microsoft YaHei", "Microsoft YaHei"),
                    new OptionChoice("sans-serif", "sans-serif"),
                ],
                Getter = () => AppContext.AppSetting.SubFont,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubFont), AppContext.AppSetting.SubFont = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubCodePage),
                Label = lang.SettingsSubCodePage,
                Category = subtitle,
                Description = lang.SettingsHelpSubCodePage,
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
                Description = lang.SettingsHelpSubOutlineSize,
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
                Description = lang.SettingsHelpSubShadowOffset,
                Type = OptionType.Double,
                Min = 0,
                Max = 10,
                Step = 0.5,
                Getter = () => AppContext.AppSetting.SubShadowOffset,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubShadowOffset), AppContext.AppSetting.SubShadowOffset = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubEmbeddedFonts),
                Label = lang.SettingsSubEmbeddedFonts,
                Category = subtitle,
                Description = lang.SettingsHelpSubEmbeddedFonts,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SubEmbeddedFonts,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubEmbeddedFonts), AppContext.AppSetting.SubEmbeddedFonts = (bool)v!)
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

            // ===== Screenshot / 截屏 =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.ScreenshotDirectory),
                Label = lang.SettingsScreenshotDirectory,
                Category = screenshot,
                Description = lang.SettingsHelpScreenshotDirectory,
                Type = OptionType.String,
                AllowEmpty = true,
                PickFolder = true,
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
                Description = lang.SettingsHelpScreenshotFormat,
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
                Description = lang.SettingsHelpScreenshotTagColorspace,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.ScreenshotTagColorspace,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ScreenshotTagColorspace), AppContext.AppSetting.ScreenshotTagColorspace = (bool)v!)
            },

            // ===== Paths / 路径 =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.CacheDirectory),
                Label = lang.SettingsCacheDir,
                Category = paths,
                Description = lang.SettingsHelpCacheDirectory,
                Type = OptionType.String,
                AllowEmpty = true,
                PickFolder = true,
                Getter = () => AppContext.AppSetting.CacheDirectory,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.CacheDirectory), AppContext.AppSetting.CacheDirectory = (string)v!)
            },
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
