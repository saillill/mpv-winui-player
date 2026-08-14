using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.FileSystem;
using mpv_winui.Modules.Language;
using mpv_winui.Modules.Player;
using mpv_winui.Modules.Settings.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Storage;
using Microsoft.Windows.Storage.Pickers;

namespace mpv_winui.Modules.Settings;

public sealed partial class SettingsPage
{
    private List<Option> BuildVideoOptions()
    {
        var video = AppContext.AppLang.SettingsCategoryVideo;
        var playback = AppContext.AppLang.SettingsCategoryPlayback;
        var lang = AppContext.AppLang;

        return
        [
            // ===== Video / 视频 =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.Deinterlace),
                Description = lang.SettingsHelpDeinterlace,
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
                Description = lang.SettingsHelpAspectRatio,
                Label = lang.SettingsAspect,
                Category = video,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("no", lang.OptionValueAspectAuto),
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
                Key = nameof(AppContext.AppSetting.OverrideDisplayFps),
                Label = lang.SettingsOverrideDisplayFps,
                Category = video,
                Description = lang.SettingsHelpOverrideDisplayFps,
                Type = OptionType.Double,
                Min = 0,
                Max = 300,
                Step = 1,
                Getter = () => AppContext.AppSetting.OverrideDisplayFps,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OverrideDisplayFps), AppContext.AppSetting.OverrideDisplayFps = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CorrectDownscaling),
                Description = lang.SettingsHelpCorrectDownscaling,
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
                    new OptionChoice("bicubic", lang.OptionValueTscaleCubic),
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
                Description = lang.SettingsHelpDither,
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
                Description = lang.SettingsHelpBackgroundTileColor0,
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
                Description = lang.SettingsHelpBackgroundTileColor1,
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
                Description = lang.SettingsHelpBackgroundTileSize,
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
                Description = lang.SettingsHelpVideoRotate,
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
                Description = lang.SettingsHelpDitherDepth,
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
                Category = playback,
                Description = lang.SettingsHelpHrSeek,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.HrSeek,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.HrSeek), AppContext.AppSetting.HrSeek = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.HrSeekFramedrop),
                Label = lang.SettingsHrSeekFramedrop,
                Category = playback,
                Description = lang.SettingsHelpHrSeekFramedrop,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.HrSeekFramedrop,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.HrSeekFramedrop), AppContext.AppSetting.HrSeekFramedrop = (bool)v!)
            },

        ];
    }
}
