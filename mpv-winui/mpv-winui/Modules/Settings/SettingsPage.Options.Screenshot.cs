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
    private List<Option> BuildScreenshotOptions()
    {
        var screenshot = AppContext.AppLang.SettingsCategoryScreenshot;
        var lang = AppContext.AppLang;

        return
        [
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

        ];
    }
}
