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
    private List<Option> BuildAudioOptions()
    {
        var audio = AppContext.AppLang.SettingsCategoryAudio;
        var lang = AppContext.AppLang;

        return
        [
            // ===== Audio / 音频 =====
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
                Description = lang.SettingsHelpAudioChannels,
                Label = lang.SettingsAudioChannels,
                Category = audio,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("auto-safe", lang.OptionValueAutoSafe),
                    new OptionChoice("auto", lang.OptionValueChannelsAuto),
                    new OptionChoice("mono", lang.OptionValueMono),
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
                Description = lang.SettingsHelpAudioPitchCorrection,
                Label = lang.SettingsAudioPitchCorrection,
                Category = audio,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.AudioPitchCorrection,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioPitchCorrection), AppContext.AppSetting.AudioPitchCorrection = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioNormalizeDownmix),
                Description = lang.SettingsHelpAudioNormalizeDownmix,
                Label = lang.SettingsAudioNormalizeDownmix,
                Category = audio,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.AudioNormalizeDownmix,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioNormalizeDownmix), AppContext.AppSetting.AudioNormalizeDownmix = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioFileAuto),
                Description = lang.SettingsHelpAudioFileAuto,
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
                Type = OptionType.MultiList,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.AudioFilePaths,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioFilePaths), AppContext.AppSetting.AudioFilePaths = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioWaitOpen),
                Label = lang.SettingsAudioWaitOpen,
                Category = audio,
                Description = lang.SettingsHelpAudioWaitOpen,
                Type = OptionType.Double,
                Min = 0,
                Max = 60,
                Step = 0.5,
                Getter = () => AppContext.AppSetting.AudioWaitOpen,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioWaitOpen), AppContext.AppSetting.AudioWaitOpen = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioBuffer),
                Label = lang.SettingsAudioBuffer,
                Category = audio,
                Description = lang.SettingsHelpAudioBuffer,
                Type = OptionType.Double,
                Min = 0,
                Max = 10,
                Step = 0.1,
                Getter = () => AppContext.AppSetting.AudioBuffer,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioBuffer), AppContext.AppSetting.AudioBuffer = (double)v!)
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
                Key = nameof(AppContext.AppSetting.AudioSpdif),
                Label = lang.SettingsAudioSpdif,
                Category = audio,
                Description = lang.SettingsHelpAudioSpdif,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("", lang.Off),
                    new OptionChoice("ac3", "AC3"),
                    new OptionChoice("eac3", "EAC3"),
                    new OptionChoice("dts", "DTS"),
                    new OptionChoice("truehd", "TrueHD"),
                    new OptionChoice("dts-hd", "DTS-HD"),
                ],
                Getter = () => AppContext.AppSetting.AudioSpdif,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioSpdif), AppContext.AppSetting.AudioSpdif = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.Replaygain),
                Label = lang.SettingsReplaygain,
                Category = audio,
                Description = lang.SettingsHelpReplaygain,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("no", lang.Off),
                    new OptionChoice("track", lang.OptionValueReplaygainTrack),
                    new OptionChoice("album", lang.OptionValueReplaygainAlbum),
                ],
                Getter = () => AppContext.AppSetting.Replaygain,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.Replaygain), AppContext.AppSetting.Replaygain = (string)v!)
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
                Description = lang.SettingsHelpCoverArtImageExts,
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

        ];
    }
}
