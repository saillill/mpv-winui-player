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
        var sSubBehavior = AppContext.AppLang.SectionSubtitleBehavior;
        var sSubStyle = AppContext.AppLang.SectionSubtitleStyle;
        var sSubPosition = AppContext.AppLang.SectionSubtitlePosition;
        var sSubAss = AppContext.AppLang.SectionSubtitleAss;
        var sSubImage = AppContext.AppLang.SectionSubtitleImage;
        var sSubFilter = AppContext.AppLang.SectionSubtitleFilter;

        var sSubSecondary = AppContext.AppLang.SectionSubtitleSecondary;

        var subtitles = AppContext.AppLang.SettingsCategorySubtitles;
        var sSubtitleAss = AppContext.AppLang.SectionSubtitleAss;
        var lang = AppContext.AppLang;

        return
        [
            // ===== Subtitles =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubFontSize),
                Description = lang.SettingsHelpSubFontSize,
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
                Key = nameof(AppContext.AppSetting.SubScaleByWindow),
                Description = lang.SettingsHelpSubScaleByWindow,
                Label = lang.SettingsSubScaleByWindow,
                Category = subtitles,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SubScaleByWindow,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubScaleByWindow), AppContext.AppSetting.SubScaleByWindow = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubLineSpacing),
                Description = lang.SettingsHelpSubLineSpacing,
                Label = lang.SettingsSubLineSpacing,
                Category = subtitles,
                Type = OptionType.Double,
                Min = -2,
                Max = 2,
                Step = 0.1,
                Getter = () => AppContext.AppSetting.SubLineSpacing,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubLineSpacing), AppContext.AppSetting.SubLineSpacing = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubJustify),
                Description = lang.SettingsHelpSubJustify,
                Label = lang.SettingsSubJustify,
                Category = subtitles,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("auto", lang.OptionValueAuto),
                    new OptionChoice("left", lang.OptionValueAlignLeft),
                    new OptionChoice("center", lang.OptionValueAlignCenter),
                    new OptionChoice("right", lang.OptionValueAlignRight),
                ],
                Getter = () => AppContext.AppSetting.SubJustify,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubJustify), AppContext.AppSetting.SubJustify = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubClearOnSeek),
                Description = lang.SettingsHelpSubClearOnSeek,
                Label = lang.SettingsSubClearOnSeek,
                Category = subtitles,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SubClearOnSeek,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubClearOnSeek), AppContext.AppSetting.SubClearOnSeek = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubHinting),
                Description = lang.SettingsHelpSubHinting,
                Label = lang.SettingsSubHinting,
                Category = subtitles,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("none", lang.OptionValueHintingNone),
                    new OptionChoice("light", lang.OptionValueHintingLight),
                    new OptionChoice("normal", lang.OptionValueHintingNormal),
                    new OptionChoice("native", lang.OptionValueHintingNative),
                ],
                Getter = () => AppContext.AppSetting.SubHinting,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubHinting), AppContext.AppSetting.SubHinting = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubDelay),
                Description = lang.SettingsHelpSubDelay,
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
                Description = lang.SettingsHelpSubPos,
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

            // Track language: the preferred audio language lives in the audio
            // category; only the subtitle language stays with the subtitle
            // settings.
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubtitleLanguage),
                Description = lang.SettingsHelpSubtitleLanguage,
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
                Type = OptionType.MultiList,
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
                Description = lang.SettingsHelpImageSubsVideoResolution,
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
                Description = lang.SettingsHelpSubBackColor,
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
                Description = lang.SettingsHelpSubBorderColor,
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
                Description = lang.SettingsHelpSubScaleSigns,
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
                Description = lang.SettingsHelpSubAssVideoAspectOverride,
                Label = lang.SettingsSubAssVideoAspectOverride,
                Category = subtitles,
                Section = sSubtitleAss,
                Type = OptionType.String,
Placeholder = "16:9",
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
Placeholder = "Fontname=Microsoft YaHei,Fontsize=24",
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.SubAssStyleOverrides,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubAssStyleOverrides), AppContext.AppSetting.SubAssStyleOverrides = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SubAuto),
                Description = lang.SettingsHelpSubAuto,
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
                Description = lang.SettingsHelpSubFont,
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
                PickFolder = true,
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
                Description = lang.SettingsHelpSubCodePage,
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
                Description = lang.SettingsHelpSubOutlineSize,
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
                Description = lang.SettingsHelpSubShadowOffset,
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
                Description = lang.SettingsHelpSubBlur,
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

            // Added in the advanced batch: secondary-sub-visibility
            new Option
            {
                Key = nameof(AppContext.AppSetting.SecondarySubVisibility),
                Label = lang.SettingsSecondarySubVisibility,
                Category = subtitles,
                Description = lang.SettingsHelpSecondarySubVisibility,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SecondarySubVisibility,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SecondarySubVisibility), AppContext.AppSetting.SecondarySubVisibility = (bool)v!)
            },

            // Added in the advanced batch: secondary-sub-delay
            new Option
            {
                Key = nameof(AppContext.AppSetting.SecondarySubDelay),
                Label = lang.SettingsSecondarySubDelay,
                Category = subtitles,
                Description = lang.SettingsHelpSecondarySubDelay,
                Type = OptionType.Double,
                Min = -10,
                Max = 10,
                Step = 0.1,
                Getter = () => AppContext.AppSetting.SecondarySubDelay,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SecondarySubDelay), AppContext.AppSetting.SecondarySubDelay = (double)v!)
            },

            // Added in the advanced batch: secondary-sub-pos
            new Option
            {
                Key = nameof(AppContext.AppSetting.SecondarySubPos),
                Label = lang.SettingsSecondarySubPos,
                Category = subtitles,
                Description = lang.SettingsHelpSecondarySubPos,
                Type = OptionType.Double,
                Min = 0,
                Max = 150,
                Step = 1,
                Getter = () => AppContext.AppSetting.SecondarySubPos,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SecondarySubPos), AppContext.AppSetting.SecondarySubPos = (double)v!)
            },

            // Added in the advanced batch: secondary-sub-scale
            new Option
            {
                Key = nameof(AppContext.AppSetting.SecondarySubScale),
                Label = lang.SettingsSecondarySubScale,
                Category = subtitles,
                Description = lang.SettingsHelpSecondarySubScale,
                Type = OptionType.Double,
                Min = 0.1,
                Max = 10,
                Step = 0.05,
                Getter = () => AppContext.AppSetting.SecondarySubScale,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SecondarySubScale), AppContext.AppSetting.SecondarySubScale = (double)v!)
            },

            // Added with the subtitle batch: sub-visibility
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubVisibility),
                Label = lang.SettingsSubVisibility,
                Category = subtitles,
                Description = lang.SettingsHelpSubVisibility,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SubVisibility,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubVisibility), AppContext.AppSetting.SubVisibility = (bool)v!)
            },

            // Added with the subtitle batch: sub-fix-timing
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubFixTiming),
                Label = lang.SettingsSubFixTiming,
                Category = subtitles,
                Description = lang.SettingsHelpSubFixTiming,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SubFixTiming,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubFixTiming), AppContext.AppSetting.SubFixTiming = (bool)v!)
            },

            // Added with the subtitle batch: sub-fix-timing-threshold
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubFixTimingThreshold),
                Label = lang.SettingsSubFixTimingThreshold,
                Category = subtitles,
                Description = lang.SettingsHelpSubFixTimingThreshold,
                Type = OptionType.Integer,
                Min = 0,
                Max = 10000,
                Step = 10,
                Getter = () => AppContext.AppSetting.SubFixTimingThreshold,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubFixTimingThreshold), AppContext.AppSetting.SubFixTimingThreshold = (int)v!)
            },

            // Added with the subtitle batch: subs-match-os-language
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubsMatchOsLanguage),
                Label = lang.SettingsSubsMatchOsLanguage,
                Category = subtitles,
                Description = lang.SettingsHelpSubsMatchOsLanguage,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SubsMatchOsLanguage,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubsMatchOsLanguage), AppContext.AppSetting.SubsMatchOsLanguage = (bool)v!)
            },

            // Added with the subtitle batch: subs-with-matching-audio
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubsWithMatchingAudio),
                Label = lang.SettingsSubsWithMatchingAudio,
                Category = subtitles,
                Description = lang.SettingsHelpSubsWithMatchingAudio,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("no", lang.OptionValueNo),
                    new OptionChoice("forced", lang.OptionValueMatchingAudioForced),
                    new OptionChoice("yes", lang.OptionValueYes),
                ],
                Getter = () => AppContext.AppSetting.SubsWithMatchingAudio,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubsWithMatchingAudio), AppContext.AppSetting.SubsWithMatchingAudio = (string)v!)
            },

            // Added with the subtitle batch: subs-fallback-forced
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubsFallbackForced),
                Label = lang.SettingsSubsFallbackForced,
                Category = subtitles,
                Description = lang.SettingsHelpSubsFallbackForced,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("no", lang.OptionValueNo),
                    new OptionChoice("yes", lang.OptionValueYes),
                    new OptionChoice("always", lang.OptionValueForcedAlways),
                ],
                Getter = () => AppContext.AppSetting.SubsFallbackForced,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubsFallbackForced), AppContext.AppSetting.SubsFallbackForced = (string)v!)
            },

            // Added with the subtitle batch: sub-forced-events-only
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubForcedEventsOnly),
                Label = lang.SettingsSubForcedEventsOnly,
                Category = subtitles,
                Description = lang.SettingsHelpSubForcedEventsOnly,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SubForcedEventsOnly,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubForcedEventsOnly), AppContext.AppSetting.SubForcedEventsOnly = (bool)v!)
            },

            // Added with the subtitle batch: sub-shaper
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubShaper),
                Label = lang.SettingsSubShaper,
                Category = subtitles,
                Description = lang.SettingsHelpSubShaper,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("simple", "simple"),
                    new OptionChoice("complex", "complex"),
                ],
                Getter = () => AppContext.AppSetting.SubShaper,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubShaper), AppContext.AppSetting.SubShaper = (string)v!)
            },

            // Added with the subtitle batch: sub-scale
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubScale),
                Label = lang.SettingsSubScale,
                Category = subtitles,
                Description = lang.SettingsHelpSubScale,
                Type = OptionType.Double,
                Min = 0,
                Max = 100,
                Step = 0.05,
                Getter = () => AppContext.AppSetting.SubScale,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubScale), AppContext.AppSetting.SubScale = (double)v!)
            },

            // Added with the subtitle batch: sub-spacing
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubSpacing),
                Label = lang.SettingsSubSpacing,
                Category = subtitles,
                Description = lang.SettingsHelpSubSpacing,
                Type = OptionType.Double,
                Min = -10,
                Max = 10,
                Step = 0.1,
                Getter = () => AppContext.AppSetting.SubSpacing,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubSpacing), AppContext.AppSetting.SubSpacing = (double)v!)
            },

            // Added with the subtitle batch: sub-outline-color
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubOutlineColor),
                Label = lang.SettingsSubOutlineColor,
                Category = subtitles,
                Description = lang.SettingsHelpSubOutlineColor,
                Type = OptionType.Color,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.SubOutlineColor,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubOutlineColor), AppContext.AppSetting.SubOutlineColor = (string)v!)
            },

            // Added with the subtitle batch: sub-gauss
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubGauss),
                Label = lang.SettingsSubGauss,
                Category = subtitles,
                Description = lang.SettingsHelpSubGauss,
                Type = OptionType.Double,
                Min = 0,
                Max = 3,
                Step = 0.05,
                Getter = () => AppContext.AppSetting.SubGauss,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubGauss), AppContext.AppSetting.SubGauss = (double)v!)
            },

            // Added with the subtitle batch: sub-margin-y-offset
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubMarginYOffset),
                Label = lang.SettingsSubMarginYOffset,
                Category = subtitles,
                Description = lang.SettingsHelpSubMarginYOffset,
                Type = OptionType.Integer,
                Min = -1000,
                Max = 1000,
                Step = 1,
                Getter = () => AppContext.AppSetting.SubMarginYOffset,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubMarginYOffset), AppContext.AppSetting.SubMarginYOffset = (int)v!)
            },

            // Added with the subtitle batch: sub-ass-justify
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubAssJustify),
                Label = lang.SettingsSubAssJustify,
                Category = subtitles,
                Description = lang.SettingsHelpSubAssJustify,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SubAssJustify,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubAssJustify), AppContext.AppSetting.SubAssJustify = (bool)v!)
            },

            // Added with the subtitle batch: sub-ass-prune-delay
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubAssPruneDelay),
                Label = lang.SettingsSubAssPruneDelay,
                Category = subtitles,
                Description = lang.SettingsHelpSubAssPruneDelay,
                Type = OptionType.Double,
                Min = -1,
                Max = 10000,
                Step = 0.5,
                Getter = () => AppContext.AppSetting.SubAssPruneDelay,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubAssPruneDelay), AppContext.AppSetting.SubAssPruneDelay = (double)v!)
            },

            // Added with the subtitle batch: sub-bitmap-max-size
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubBitmapMaxSize),
                Label = lang.SettingsSubBitmapMaxSize,
                Category = subtitles,
                Description = lang.SettingsHelpSubBitmapMaxSize,
                Type = OptionType.Integer,
                Min = 0,
                Max = 100000000,
                Step = 1000,
                Getter = () => AppContext.AppSetting.SubBitmapMaxSize,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubBitmapMaxSize), AppContext.AppSetting.SubBitmapMaxSize = (int)v!)
            },

            // Added with the subtitle batch: secondary-sub-ass-override
            new Option
            {
                Key = nameof(AppContext.AppSetting.SecondarySubAssOverride),
                Label = lang.SettingsSecondarySubAssOverride,
                Category = subtitles,
                Description = lang.SettingsHelpSecondarySubAssOverride,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("no", lang.OptionValueAssOverrideNo),
                    new OptionChoice("yes", lang.OptionValueAssOverrideYes),
                    new OptionChoice("scale", lang.OptionValueAssOverrideScale),
                    new OptionChoice("force", lang.OptionValueAssOverrideForce),
                    new OptionChoice("strip", lang.OptionValueAssOverrideStrip),
                ],
                Getter = () => AppContext.AppSetting.SecondarySubAssOverride,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SecondarySubAssOverride), AppContext.AppSetting.SecondarySubAssOverride = (string)v!)
            },

            // Added with the subtitle batch: sub-filter-sdh
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubFilterSdh),
                Label = lang.SettingsSubFilterSdh,
                Category = subtitles,
                Description = lang.SettingsHelpSubFilterSdh,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SubFilterSdh,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubFilterSdh), AppContext.AppSetting.SubFilterSdh = (bool)v!)
            },

            // Added with the subtitle batch: sub-filter-sdh-enclosures
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubFilterSdhEnclosures),
                Label = lang.SettingsSubFilterSdhEnclosures,
                Category = subtitles,
                Description = lang.SettingsHelpSubFilterSdhEnclosures,
                Type = OptionType.String,
                AllowEmpty = true,
                Placeholder = "(),[],（）",
                Getter = () => AppContext.AppSetting.SubFilterSdhEnclosures,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubFilterSdhEnclosures), AppContext.AppSetting.SubFilterSdhEnclosures = (string)v!)
            },

            // Added with the subtitle batch: sub-filter-sdh-harder
            new Option
            {
                Key = nameof(AppContext.AppSetting.SubFilterSdhHarder),
                Label = lang.SettingsSubFilterSdhHarder,
                Category = subtitles,
                Description = lang.SettingsHelpSubFilterSdhHarder,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SubFilterSdhHarder,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SubFilterSdhHarder), AppContext.AppSetting.SubFilterSdhHarder = (bool)v!)
            },

        ];
    }
}
