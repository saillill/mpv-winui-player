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
    private List<Option> BuildSubtitlesOptions()
    {
        var subtitles = AppContext.AppLang.SettingsCategorySubtitles;
        var sSubtitleAss = AppContext.AppLang.SectionSubtitleAss;
        var lang = AppContext.AppLang;

        return
        [
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
                Key = nameof(AppContext.AppSetting.SubBold),
                Label = lang.SettingsSubBold,
                Category = subtitles,
                Description = lang.SettingsHelpSubBold,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SubBold,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubBold), AppContext.AppSetting.SubBold = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubItalic),
                Label = lang.SettingsSubItalic,
                Category = subtitles,
                Description = lang.SettingsHelpSubItalic,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SubItalic,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubItalic), AppContext.AppSetting.SubItalic = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubAlignX),
                Label = lang.SettingsSubAlignX,
                Category = subtitles,
                Description = lang.SettingsHelpSubAlignX,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("left", lang.OptionValueAlignLeft),
                    new OptionChoice("center", lang.OptionValueAlignCenter),
                    new OptionChoice("right", lang.OptionValueAlignRight),
                ],
                Getter = () => AppContext.AppSetting.SubAlignX,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubAlignX), AppContext.AppSetting.SubAlignX = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubAlignY),
                Label = lang.SettingsSubAlignY,
                Category = subtitles,
                Description = lang.SettingsHelpSubAlignY,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("top", lang.OptionValueAlignTop),
                    new OptionChoice("center", lang.OptionValueAlignCenter),
                    new OptionChoice("bottom", lang.OptionValueAlignBottom),
                ],
                Getter = () => AppContext.AppSetting.SubAlignY,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubAlignY), AppContext.AppSetting.SubAlignY = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubMarginX),
                Label = lang.SettingsSubMarginX,
                Category = subtitles,
                Description = lang.SettingsHelpSubMarginX,
                Type = OptionType.Integer,
                Min = 0,
                Max = 1000,
                Step = 1,
                Getter = () => (double)AppContext.AppSetting.SubMarginX,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubMarginX), AppContext.AppSetting.SubMarginX = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubMarginY),
                Label = lang.SettingsSubMarginY,
                Category = subtitles,
                Description = lang.SettingsHelpSubMarginY,
                Type = OptionType.Integer,
                Min = 0,
                Max = 1000,
                Step = 1,
                Getter = () => (double)AppContext.AppSetting.SubMarginY,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubMarginY), AppContext.AppSetting.SubMarginY = Convert.ToInt32(v))
            },

            // Track language: the audio preferred language lives next to the
            // subtitle preferred language (both drive track selection), not in
            // the audio output section.
            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioLanguage),
                Label = lang.SettingsAudioLanguage,
                Category = subtitles,
                Type = OptionType.StringList,
                Choices = LanguageChoices(true),
                Getter = () => AppContext.AppSetting.AudioLanguage,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioLanguage), AppContext.AppSetting.AudioLanguage = (string)v!)
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

        ];
    }
}
