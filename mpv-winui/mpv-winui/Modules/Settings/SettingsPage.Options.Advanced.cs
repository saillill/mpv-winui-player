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
    private List<Option> BuildAdvancedOptions()
    {
        var audio = AppContext.AppLang.SettingsCategoryAudio;
        var program = AppContext.AppLang.SettingsCategoryProgram;
        var shortcuts = AppContext.AppLang.SettingsCategoryShortcuts;
        var network = AppContext.AppLang.SettingsCategoryNetwork;
        var osd = AppContext.AppLang.SettingsCategoryOsd;
        var playback = AppContext.AppLang.SettingsCategoryPlayback;
        var video = AppContext.AppLang.SettingsCategoryVideo;
        var lang = AppContext.AppLang;

        return
        [
            // ===== Advanced (initial placeholder; categoryMap below reassigns) =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.CacheOnDisk),
                Label = lang.SettingsCacheOnDisk,
                Category = network,
                Description = lang.SettingsHelpCacheOnDisk,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.CacheOnDisk,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.CacheOnDisk), AppContext.AppSetting.CacheOnDisk = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.VideoOutputLevels),
                Label = lang.SettingsVideoOutputLevels,
                Category = video,
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
                Category = video,
                Description = lang.SettingsHelpD3d11OutputCsp,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("", lang.OptionValueAuto),
                    new OptionChoice("srgb", lang.OptionValueCspSrgb),
                    new OptionChoice("bt.2020", lang.OptionValueCspBt2020),
                    new OptionChoice("pq", lang.OptionValueCspPq),
                    new OptionChoice("linear", lang.OptionValueCspLinear),
                ],
                Getter = () => AppContext.AppSetting.D3d11OutputCsp,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.D3d11OutputCsp), AppContext.AppSetting.D3d11OutputCsp = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.D3d11OutputFormat),
                Description = lang.SettingsHelpD3d11OutputFormat,
                Label = lang.SettingsD3d11OutputFormat,
                Category = video,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("", lang.OptionValueAuto),
                    new OptionChoice("rgba8", "rgba8"),
                    new OptionChoice("bgra8", "bgra8"),
                    new OptionChoice("rgb10_a2", "rgb10_a2"),
                    new OptionChoice("rgba16f", "rgba16f"),
                ],
                Getter = () => AppContext.AppSetting.D3d11OutputFormat,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.D3d11OutputFormat), AppContext.AppSetting.D3d11OutputFormat = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.D3d11ExclusiveFs),
                Description = lang.SettingsHelpD3d11ExclusiveFs,
                Label = lang.SettingsD3d11ExclusiveFs,
                Category = video,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.D3d11ExclusiveFs,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.D3d11ExclusiveFs), AppContext.AppSetting.D3d11ExclusiveFs = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.D3d11Flip),
                Description = lang.SettingsHelpD3d11Flip,
                Label = lang.SettingsD3d11Flip,
                Category = video,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.D3d11Flip,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.D3d11Flip), AppContext.AppSetting.D3d11Flip = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.D3d11Adapter),
                Label = lang.SettingsD3d11Adapter,
                Category = video,
                Description = lang.SettingsHelpD3d11Adapter,
                Type = OptionType.StringList,
                ChoicesProvider = BuildGpuAdapterChoices,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.D3d11Adapter,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.D3d11Adapter), AppContext.AppSetting.D3d11Adapter = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.VideoDecodeDirect),
                Label = lang.SettingsVideoDecodeDirect,
                Category = video,
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
                Category = network,
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
                Category = video,
                Description = lang.SettingsHelpIccProfileAuto,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.IccProfileAuto,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.IccProfileAuto), AppContext.AppSetting.IccProfileAuto = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.IccProfile),
                Label = lang.SettingsIccProfile,
                Category = video,
                Description = lang.SettingsHelpIccProfile,
                Type = OptionType.String,
                AllowEmpty = true,
                PickFile = true,
                OpenFolder = true,
                FileTypeFilter = [".icc", ".icm"],
                FallbackOpenFolder = "mpv",
                Getter = () => AppContext.AppSetting.IccProfile,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.IccProfile), AppContext.AppSetting.IccProfile = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.IccForceContrast),
                Label = lang.SettingsIccForceContrast,
                Category = video,
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
                Category = video,
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
                Category = video,
                Description = lang.SettingsHelpIccCacheDir,
                Type = OptionType.String,
                AllowEmpty = true,
                Placeholder = AppData.Current.ResolveLocalData(Path.Combine("mpv", "_cache", "icc")),
                PickFolder = true,
                OpenFolder = true,
                Getter = () => AppContext.AppSetting.IccCacheDir,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.IccCacheDir), AppContext.AppSetting.IccCacheDir = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.TargetColorspaceHint),
                Label = lang.SettingsTargetColorspaceHint,
                Category = video,
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
                Category = video,
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
                Description = lang.SettingsHelpTargetColorspaceHintStrict,
                Label = lang.SettingsTargetColorspaceHintStrict,
                Category = video,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.TargetColorspaceHintStrict,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.TargetColorspaceHintStrict), AppContext.AppSetting.TargetColorspaceHintStrict = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.TargetPrim),
                Label = lang.SettingsTargetPrim,
                Category = video,
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
                Category = video,
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
                Category = video,
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
                Key = nameof(AppContext.AppSetting.DisplayPeak),
                Label = lang.SettingsDisplayPeak,
                Category = video,
                Description = lang.SettingsHelpDisplayPeak,
                Type = OptionType.Integer,
                Min = 0,
                Max = 10000,
                Step = 100,
                Getter = () => (double)AppContext.AppSetting.DisplayPeak,
                Setter = v =>
                {
                    AppContext.AppSetting.DisplayPeak = Convert.ToInt32(v);
                    AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.DisplayPeak), AppContext.AppSetting.DisplayPeak);
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.GamutMappingMode),
                Label = lang.SettingsGamutMappingMode,
                Category = video,
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
                Key = nameof(AppContext.AppSetting.TargetGamut),
                Description = lang.SettingsHelpTargetGamut,
                Label = lang.SettingsTargetGamut,
                Category = video,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.TargetGamut,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.TargetGamut), AppContext.AppSetting.TargetGamut = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ToneMappingMaxBoost),
                Description = lang.SettingsHelpToneMappingMaxBoost,
                Label = lang.SettingsToneMappingMaxBoost,
                Category = video,
                Type = OptionType.Double,
                Min = 1,
                Max = 10,
                Step = 0.1,
                Getter = () => AppContext.AppSetting.ToneMappingMaxBoost,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ToneMappingMaxBoost), AppContext.AppSetting.ToneMappingMaxBoost = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.HdrComputePeak),
                Description = lang.SettingsHelpHdrComputePeak,
                Label = lang.SettingsHdrComputePeak,
                Category = video,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("auto", lang.OptionValueAuto),
                    new OptionChoice("yes", lang.OptionValueOn),
                    new OptionChoice("no", lang.OptionValueOff),
                ],
                Getter = () => AppContext.AppSetting.HdrComputePeak,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.HdrComputePeak), AppContext.AppSetting.HdrComputePeak = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.HdrPeakDecayRate),
                Description = lang.SettingsHelpHdrPeakDecayRate,
                Label = lang.SettingsHdrPeakDecayRate,
                Category = video,
                Type = OptionType.Double,
                Min = 0,
                Max = 1000,
                Step = 1,
                Getter = () => AppContext.AppSetting.HdrPeakDecayRate,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.HdrPeakDecayRate), AppContext.AppSetting.HdrPeakDecayRate = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.HdrSceneThresholdLow),
                Description = lang.SettingsHelpHdrSceneThresholdLow,
                Label = lang.SettingsHdrSceneThresholdLow,
                Category = video,
                Type = OptionType.Double,
                Min = 0,
                Max = 100,
                Step = 0.1,
                Getter = () => AppContext.AppSetting.HdrSceneThresholdLow,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.HdrSceneThresholdLow), AppContext.AppSetting.HdrSceneThresholdLow = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.HdrSceneThresholdHigh),
                Description = lang.SettingsHelpHdrSceneThresholdHigh,
                Label = lang.SettingsHdrSceneThresholdHigh,
                Category = video,
                Type = OptionType.Double,
                Min = 0,
                Max = 100,
                Step = 0.1,
                Getter = () => AppContext.AppSetting.HdrSceneThresholdHigh,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.HdrSceneThresholdHigh), AppContext.AppSetting.HdrSceneThresholdHigh = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.HdrContrastRecovery),
                Description = lang.SettingsHelpHdrContrastRecovery,
                Label = lang.SettingsHdrContrastRecovery,
                Category = video,
                Type = OptionType.Double,
                Min = 0,
                Max = 2,
                Step = 0.05,
                Getter = () => AppContext.AppSetting.HdrContrastRecovery,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.HdrContrastRecovery), AppContext.AppSetting.HdrContrastRecovery = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.HdrContrastSmoothness),
                Description = lang.SettingsHelpHdrContrastSmoothness,
                Label = lang.SettingsHdrContrastSmoothness,
                Category = video,
                Type = OptionType.Double,
                Min = 1,
                Max = 100,
                Step = 0.1,
                Getter = () => AppContext.AppSetting.HdrContrastSmoothness,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.HdrContrastSmoothness), AppContext.AppSetting.HdrContrastSmoothness = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CachePauseInitial),
                Description = lang.SettingsHelpCachePauseInitial,
                Label = lang.SettingsCachePauseInitial,
                Category = network,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.CachePauseInitial,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.CachePauseInitial), AppContext.AppSetting.CachePauseInitial = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CachePauseWait),
                Description = lang.SettingsHelpCachePauseWait,
                Label = lang.SettingsCachePauseWait,
                Category = network,
                Type = OptionType.Double,
                Min = 0,
                Max = 60,
                Step = 0.5,
                Getter = () => AppContext.AppSetting.CachePauseWait,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.CachePauseWait), AppContext.AppSetting.CachePauseWait = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.D3d11SyncInterval),
                Description = lang.SettingsHelpD3d11SyncInterval,
                Label = lang.SettingsD3d11SyncInterval,
                Category = video,
                Type = OptionType.Integer,
                Min = 0,
                Max = 4,
                Step = 1,
                Getter = () => (double)AppContext.AppSetting.D3d11SyncInterval,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.D3d11SyncInterval), AppContext.AppSetting.D3d11SyncInterval = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.InverseToneMapping),
                Description = lang.SettingsHelpInverseToneMapping,
                Label = lang.SettingsInverseToneMapping,
                Category = video,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.InverseToneMapping,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.InverseToneMapping), AppContext.AppSetting.InverseToneMapping = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ToneMappingVisualize),
                Description = lang.SettingsHelpToneMappingVisualize,
                Label = lang.SettingsToneMappingVisualize,
                Category = video,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.ToneMappingVisualize,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ToneMappingVisualize), AppContext.AppSetting.ToneMappingVisualize = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.D3d11Warp),
                Description = lang.SettingsHelpD3d11Warp,
                Label = lang.SettingsD3d11Warp,
                Category = video,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("auto", lang.OptionValueAuto),
                    new OptionChoice("yes", lang.OptionValueOn),
                    new OptionChoice("no", lang.OptionValueOff),
                ],
                Getter = () => AppContext.AppSetting.D3d11Warp,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.D3d11Warp), AppContext.AppSetting.D3d11Warp = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.VideoReversalBuffer),
                Description = lang.SettingsHelpVideoReversalBuffer,
                Label = lang.SettingsVideoReversalBuffer,
                Category = playback,
                Type = OptionType.Integer,
                Min = 0,
                Max = 1000000000,
                Step = 1000000,
                Getter = () => (double)AppContext.AppSetting.VideoReversalBuffer,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.VideoReversalBuffer), AppContext.AppSetting.VideoReversalBuffer = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioReversalBuffer),
                Description = lang.SettingsHelpAudioReversalBuffer,
                Label = lang.SettingsAudioReversalBuffer,
                Category = playback,
                Type = OptionType.Integer,
                Min = 0,
                Max = 1000000000,
                Step = 1000000,
                Getter = () => (double)AppContext.AppSetting.AudioReversalBuffer,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioReversalBuffer), AppContext.AppSetting.AudioReversalBuffer = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.IccCache),
                Label = lang.SettingsIccCache,
                Category = video,
                Description = lang.SettingsHelpIccCache,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.IccCache,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.IccCache), AppContext.AppSetting.IccCache = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.GpuShaderCache),
                Label = lang.SettingsGpuShaderCache,
                Category = video,
                Description = lang.SettingsHelpGpuShaderCache,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.GpuShaderCache,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.GpuShaderCache), AppContext.AppSetting.GpuShaderCache = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.GlslShadersAppend),
                Label = lang.SettingsGlslShadersAppend,
                Category = video,
                Description = lang.SettingsHelpGlslShadersAppend,
                Type = OptionType.MultiList,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.GlslShadersAppend,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.GlslShadersAppend), AppContext.AppSetting.GlslShadersAppend = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.GlslShaders),
                Description = lang.SettingsHelpGlslShaders,
                Label = lang.SettingsGlslShaders,
                Category = video,
                Type = OptionType.ShaderList,
                Getter = () => AppContext.AppSetting.GlslShaders,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.GlslShaders), AppContext.AppSetting.GlslShaders = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.GlslShaderOpts),
                Description = lang.SettingsHelpGlslShaderOpts,
                Label = lang.SettingsGlslShaderOpts,
                Category = video,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.GlslShaderOpts,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.GlslShaderOpts), AppContext.AppSetting.GlslShaderOpts = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.DemuxerMaxBackBytes),
                Label = lang.SettingsDemuxerMaxBackBytes,
                Category = network,
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
                Key = nameof(AppContext.AppSetting.DemuxerHysteresisSecs),
                Label = lang.SettingsDemuxerHysteresisSecs,
                Category = network,
                Description = lang.SettingsHelpDemuxerHysteresisSecs,
                Type = OptionType.Double,
                Min = 0,
                Max = 10,
                Step = 0.1,
                Getter = () => AppContext.AppSetting.DemuxerHysteresisSecs,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.DemuxerHysteresisSecs), AppContext.AppSetting.DemuxerHysteresisSecs = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.DemuxerCacheDir),
                Label = lang.SettingsDemuxerCacheDir,
                Category = network,
                Description = lang.SettingsHelpDemuxerCacheDir,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.DemuxerCacheDir,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.DemuxerCacheDir), AppContext.AppSetting.DemuxerCacheDir = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioDisplay),
                Label = lang.SettingsAudioDisplay,
                Category = audio,
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
                Description = lang.SettingsHelpOsdFontSize,
                Label = lang.SettingsOsdFontSize,
                Category = osd,
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
                Category = osd,
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
                Category = osd,
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
                Category = osd,
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
                Category = osd,
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
                Category = osd,
                Description = lang.SettingsHelpOsdPlayingMsg,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.OsdPlayingMsg,
                Setter = v =>
                {
                    AppContext.AppSetting.OsdPlayingMsg = (string)v!;
                    // Only push the text while the display toggle is on;
                    // otherwise editing the message would silently clear it
                    // (audit B2).
                    if (AppContext.AppSetting.ShowOsdPlayingMsg)
                    {
                        ApplyMpv(nameof(AppContext.AppSetting.OsdPlayingMsg), (string)v!);
                    }
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.OsdPlayingMsgDuration),
                Label = lang.SettingsOsdPlayingMsgDuration,
                Category = osd,
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
                Description = lang.SettingsHelpOsdBarWidth,
                Label = lang.SettingsOsdBarWidth,
                Category = osd,
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
                Description = lang.SettingsHelpOsdBarHeight,
                Label = lang.SettingsOsdBarHeight,
                Category = osd,
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
                Description = lang.SettingsHelpOsdBlur,
                Label = lang.SettingsOsdBlur,
                Category = osd,
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
                Description = lang.SettingsHelpOsdOutlineSize,
                Label = lang.SettingsOsdOutlineSize,
                Category = osd,
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
                Description = lang.SettingsHelpOsdFractions,
                Label = lang.SettingsOsdFractions,
                Category = osd,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.OsdFractions,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdFractions), AppContext.AppSetting.OsdFractions = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.OsdLevel),
                Label = lang.SettingsOsdLevel,
                Category = osd,
                Description = lang.SettingsHelpOsdLevel,
                Type = OptionType.Integer,
                Min = 0,
                Max = 3,
                Step = 1,
                Getter = () => (double)AppContext.AppSetting.OsdLevel,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdLevel), AppContext.AppSetting.OsdLevel = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.OsdAlignX),
                Label = lang.SettingsOsdAlignX,
                Category = osd,
                Description = lang.SettingsHelpOsdAlignX,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("left", lang.OptionValueAlignLeft),
                    new OptionChoice("center", lang.OptionValueAlignCenter),
                    new OptionChoice("right", lang.OptionValueAlignRight),
                ],
                Getter = () => AppContext.AppSetting.OsdAlignX,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdAlignX), AppContext.AppSetting.OsdAlignX = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.OsdAlignY),
                Label = lang.SettingsOsdAlignY,
                Category = osd,
                Description = lang.SettingsHelpOsdAlignY,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("top", lang.OptionValueAlignTop),
                    new OptionChoice("center", lang.OptionValueAlignCenter),
                    new OptionChoice("bottom", lang.OptionValueAlignBottom),
                ],
                Getter = () => AppContext.AppSetting.OsdAlignY,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdAlignY), AppContext.AppSetting.OsdAlignY = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.OsdMarginX),
                Label = lang.SettingsOsdMarginX,
                Category = osd,
                Description = lang.SettingsHelpOsdMarginX,
                Type = OptionType.Integer,
                Min = 0,
                Max = 1000,
                Step = 1,
                Getter = () => (double)AppContext.AppSetting.OsdMarginX,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdMarginX), AppContext.AppSetting.OsdMarginX = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.OsdMarginY),
                Label = lang.SettingsOsdMarginY,
                Category = osd,
                Description = lang.SettingsHelpOsdMarginY,
                Type = OptionType.Integer,
                Min = 0,
                Max = 1000,
                Step = 1,
                Getter = () => (double)AppContext.AppSetting.OsdMarginY,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdMarginY), AppContext.AppSetting.OsdMarginY = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.OsdColor),
                Description = lang.SettingsHelpOsdColor,
                Label = lang.SettingsOsdColor,
                Category = osd,
                Type = OptionType.Color,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.OsdColor,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdColor), AppContext.AppSetting.OsdColor = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.OsdOutlineColor),
                Description = lang.SettingsHelpOsdOutlineColor,
                Label = lang.SettingsOsdOutlineColor,
                Category = osd,
                Type = OptionType.Color,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.OsdOutlineColor,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.OsdOutlineColor), AppContext.AppSetting.OsdOutlineColor = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.VsrAutoEnabled),
                Label = lang.SettingsVsrAuto,
                Category = video,
                Description = lang.SettingsHelpVsrAuto,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.VsrAutoEnabled,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.VsrAutoEnabled), AppContext.AppSetting.VsrAutoEnabled = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.HdrAutoMode),
                Label = lang.SettingsHdrAutoMode,
                Category = video,
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
                Key = nameof(AppContext.AppSetting.HdrOverrideMode),
                Label = lang.SettingsHdrOverrideMode,
                Category = video,
                Description = lang.SettingsHelpHdrOverrideMode,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("", lang.OptionValueAuto),
                    new OptionChoice("HDR", "HDR"),
                    new OptionChoice("SDR", "SDR"),
                ],
                Getter = () => AppContext.AppSetting.HdrOverrideMode,
                Setter = v =>
                {
                    AppContext.AppSetting.HdrOverrideMode = (string)v!;
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.HdrAutoLog),
                Label = lang.SettingsHdrAutoLog,
                Category = video,
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
                Category = playback,
                Description = lang.SettingsHelpSeekHold,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SeekHoldEnabled,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SeekHoldEnabled), AppContext.AppSetting.SeekHoldEnabled = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.MetadataOsdEnabled),
                Label = lang.SettingsMetadataOsdEnabled,
                Category = osd,
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
                Category = osd,
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
                Category = osd,
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
                Category = osd,
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
                Key = nameof(AppContext.AppSetting.MetadataOsdEnableForAudio),
                Label = lang.SettingsMetadataOsdEnableForAudio,
                Category = osd,
                Description = lang.SettingsHelpMetadataOsdEnableForAudio,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.MetadataOsdEnableForAudio,
                Setter = v =>
                {
                    AppContext.AppSetting.MetadataOsdEnableForAudio = (bool)v!;
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.MetadataOsdEnableForAudioWithAlbumArt),
                Label = lang.SettingsMetadataOsdEnableForAudioWithAlbumArt,
                Category = osd,
                Description = lang.SettingsHelpMetadataOsdEnableForAudioWithAlbumArt,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.MetadataOsdEnableForAudioWithAlbumArt,
                Setter = v =>
                {
                    AppContext.AppSetting.MetadataOsdEnableForAudioWithAlbumArt = (bool)v!;
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.MetadataOsdAutohideForAudio),
                Label = lang.SettingsMetadataOsdAutohideForAudio,
                Category = osd,
                Description = lang.SettingsHelpMetadataOsdAutohideForAudio,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.MetadataOsdAutohideForAudio,
                Setter = v =>
                {
                    AppContext.AppSetting.MetadataOsdAutohideForAudio = (bool)v!;
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.MetadataOsdAutohideForAudioWithAlbumArt),
                Label = lang.SettingsMetadataOsdAutohideForAudioWithAlbumArt,
                Category = osd,
                Description = lang.SettingsHelpMetadataOsdAutohideForAudioWithAlbumArt,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.MetadataOsdAutohideForAudioWithAlbumArt,
                Setter = v =>
                {
                    AppContext.AppSetting.MetadataOsdAutohideForAudioWithAlbumArt = (bool)v!;
                    AppContext.WritePluginConfigs();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.MetadataOsdEnableForImage),
                Label = lang.SettingsMetadataOsdEnableForImage,
                Category = osd,
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
                Category = osd,
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
                Category = osd,
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
                Category = osd,
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
                Key = nameof(AppContext.AppSetting.InputIpcServer),
                Label = lang.SettingsInputIpcServer,
                Category = program,
                Description = lang.SettingsHelpInputIpcServer,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.InputIpcServer,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.InputIpcServer), AppContext.AppSetting.InputIpcServer = (string)v!)
            },

        ];
    }
}
