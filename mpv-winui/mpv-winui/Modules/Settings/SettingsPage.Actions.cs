using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using mpv_winui.Modules.Activation;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.FileSystem;
using mpv_winui.Modules.Language;
using mpv_winui.Modules.Player;
using mpv_winui.Modules.Settings.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.Windows.Storage.Pickers;

namespace mpv_winui.Modules.Settings;

public sealed partial class SettingsPage
{
private static readonly System.Collections.Generic.HashSet<string> NoCustomOptions = new(StringComparer.Ordinal)
    {
        nameof(AppContext.AppSetting.ThemeType),
        nameof(AppContext.AppSetting.BackdropType),
        nameof(AppContext.AppSetting.CurrentLanguage),
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
        nameof(AppContext.AppSetting.YtdlThumbnails),
        nameof(AppContext.AppSetting.WindowPiPSize),
        nameof(AppContext.AppSetting.ControlBarLayout),
    };

    /// <summary>Options whose help text only restates the title (Windows Settings style: no redundant description).</summary>
    private static readonly System.Collections.Generic.HashSet<string> RedundantDescriptions = new(StringComparer.Ordinal)
    {
        nameof(AppContext.AppSetting.AudioLanguage),
        nameof(AppContext.AppSetting.SubtitleLanguage),
        nameof(AppContext.AppSetting.KeepOpen),
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

    private static readonly Dictionary<string, List<OptionChoice>> FontChoicesCache = new(StringComparer.Ordinal);

    private static List<OptionChoice> SubtitleFontChoices(AppLang lang)
    {
        // The same list feeds the UI font, OSD font and subtitle font options
        // on every settings build; scanning the font directory three times per
        // navigation is wasted I/O. Cache per language + selected font file.
        var cacheKey = AppContext.AppSetting.CurrentLanguage + "|" + (AppContext.AppSetting.SubFontFile ?? string.Empty);
        if (FontChoicesCache.TryGetValue(cacheKey, out var cachedChoices))
        {
            return cachedChoices;
        }

        var list = new List<OptionChoice>
        {
            new("sans-serif", lang.OptionValueFontDefault),
        };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sans-serif" };

        void Add(string value, string label)
        {
            if (seen.Add(value))
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
            case "zh-TW":
                Add("Microsoft JhengHei", "微軟正黑體");
                Add("PMingLiU", "新細明體");
                Add("DFKai-SB", "標楷體");
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
        FontChoicesCache[cacheKey] = list;
        return list;
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

    private void ApplyMpv(string key, object value)
    {
        if (MpvSettings.ToCommand(key, value) is { } cmd)
        {
            AppContext.SendMpvCommand(cmd);
        }
        // Only settings that feed warning/visibility/enabled rules require the
        // full O(N) re-evaluation; everything else skips it (audit A1).
        if (WarningDependencyKeys.Contains(key))
        {
            RefreshWarningsAndEnabled();
        }
    }

    /// <summary>AppSettings keys whose change can alter warnings/visibility/enabled state.</summary>
    private static readonly HashSet<string> WarningDependencyKeys = new(StringComparer.Ordinal)
    {
        nameof(AppSettings.VideoSync),
        nameof(AppSettings.Interpolation),
        nameof(AppSettings.Hwdec),
        nameof(AppSettings.LinearUpscaling),
        nameof(AppSettings.SigmoidUpscaling),
        nameof(AppSettings.ResumePlayback),
        nameof(AppSettings.BlendSubtitles),
        nameof(AppSettings.SubtitleLanguage),
        nameof(AppSettings.VsrAutoEnabled),
        nameof(AppSettings.HdrAutoMode),
        nameof(AppSettings.ThemeType),
        nameof(AppSettings.BackdropType),
        nameof(AppSettings.WindowPiP),
        nameof(AppSettings.ScreenshotFormat),
    };

    /// <summary>Re-evaluates yellow warnings and disabled states after any option changes.</summary>
    private void RefreshWarningsAndEnabled()
    {
        var s = AppContext.AppSetting;
        var changed = false;
        foreach (var option in Settings)
        {
            var warning = ComputeWarning(option, s);
            var enabled = ComputeEnabled(option, s);
            var visible = ComputeVisible(option, s);
            changed |= option.Warning != warning;
            changed |= option.IsEnabled != enabled;
            changed |= option.IsVisible != visible;
            option.Warning = warning;
            option.IsEnabled = enabled;
            option.IsVisible = visible;
        }
        if (changed)
        {
            OptionsControl.Refresh();
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
            nameof(AppSettings.SeekHoldEnabled) when !s.VsrAutoEnabled && s.HdrAutoMode == "off" => lang.WarningSeekHoldInactive,
            _ => null,
        };
    }

    private static bool ComputeVisible(Option option, AppSettings s)
    {
        return option.Key switch
        {
            // The custom tint color only applies in Custom theme mode; the
            // other themes follow the system accent. Transparency/brightness
            // sliders apply to Acrylic and Mica in every theme mode.
            nameof(AppSettings.ThemeAccentColor) when s.ThemeType != AppSettings.ThemeType_Custom => false,
            // PiP size only applies while the mini player is enabled.
            nameof(AppSettings.WindowPiPSize) when !s.WindowPiP => false,
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
            // mpv only writes high-bit-depth screenshots for PNG, JXL and AVIF
            // (image_writer_high_depth); WebP ignores this option.
            nameof(AppSettings.ScreenshotHighBitDepth) when s.ScreenshotFormat is not ("png" or "jxl" or "avif") => false,
            nameof(AppSettings.ScreenshotTagColorspace) when s.ScreenshotFormat == "jpg" => false,
            _ => true,
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
            // MicaController ignores luminosity, so brightness only applies to Acrylic.
            nameof(AppSettings.ThemeLuminosity) when s.BackdropType != AppSettings.BackdropType_Acrylic => false,
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

    private static readonly string[] AssociationExtensions =
    [
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v",
        ".mpg", ".mpeg", ".ts", ".m2ts", ".3gp", ".ogv", ".rm", ".rmvb",
        ".mp3", ".flac", ".wav", ".aac", ".m4a", ".ogg", ".opus", ".wma",
    ];

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v",
        ".mpg", ".mpeg", ".ts", ".m2ts", ".3gp", ".ogv", ".rm", ".rmvb",
    };

    private static List<OptionCheckItem> BuildAssociationItems()
    {
        var selected = ParseTokenList(AppContext.AppSetting.FileAssociationExts);
        var lang = AppContext.AppLang;
        return AssociationExtensions
            .Select(ext => new OptionCheckItem(
                ext,
                ext,
                selected.Contains(ext, StringComparer.OrdinalIgnoreCase),
                VideoExtensions.Contains(ext) ? "\uE714" : "\uE8D6",
                VideoExtensions.Contains(ext) ? lang.FileAssociationGroupVideo : lang.FileAssociationGroupAudio))
            .ToList();
    }

    private static void UpdateAssociationSelection(string extension, bool isChecked)
    {
        var list = ParseTokenList(AppContext.AppSetting.FileAssociationExts).ToList();
        if (isChecked)
        {
            if (!list.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(extension);
            }
        }
        else
        {
            list.RemoveAll(x => string.Equals(x, extension, StringComparison.OrdinalIgnoreCase));
        }

        AppContext.AppSetting.FileAssociationExts = string.Join(';', list);
    }

    private void ApplyAssociations()
    {
        _ = ApplyAssociationsAsync();
    }

    private async Task ApplyAssociationsAsync()
    {
        try
        {
            var service = ActivationRegistrationService.Instance;
            var selected = ParseTokenList(AppContext.AppSetting.FileAssociationExts).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var toRegister = AssociationExtensions.Where(selected.Contains).ToList();
            var toUnregister = AssociationExtensions.Where(ext => !selected.Contains(ext)).ToList();

            if (toRegister.Count > 0)
            {
                await service.RegisterAsync(toRegister);
            }

            if (toUnregister.Count > 0)
            {
                await service.UnregisterAsync(toUnregister);
            }

            // Keep the mpv-winui:// protocol while any file association is
            // active; remove it when the user clears the whole checklist.
            if (selected.Count == 0)
            {
                await service.UnregisterProtocolAsync("mpv-winui");
            }
            else
            {
                await service.RegisterProtocolAsync("mpv-winui");
            }

            _actionStatus = AppContext.AppLang.SettingsAssociateDone;
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Error(ex, "file association apply failed");
        }
    }

    private void UnassociateFiles()
    {
        _ = UnassociateFilesAsync();
    }

    private async Task UnassociateFilesAsync()
    {
        try
        {
            var extensions = ParseTokenList(AppContext.AppSetting.FileAssociationExts).ToList();
            if (extensions.Count > 0)
            {
                await ActivationRegistrationService.Instance.UnregisterAsync(extensions);
            }

            await ActivationRegistrationService.Instance.UnregisterProtocolAsync("mpv-winui");
            AppContext.AppSetting.FileAssociationExts = string.Empty;
            _actionStatus = AppContext.AppLang.SettingsUnassociateDone;
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Error(ex, "file association unregister failed");
        }
    }

    private static IEnumerable<string> ParseTokenList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 0);
    }

    private static List<OptionCheckItem> BuildControlBarIconItems(string settingKey)
    {
        var lang = AppContext.AppLang;
        var hiddenValue = settingKey == nameof(AppSettings.ControlBarHiddenIconsModernX)
            ? AppContext.AppSetting.ControlBarHiddenIconsModernX
            : AppContext.AppSetting.ControlBarHiddenIconsClassic;
        var hidden = ParseTokenList(hiddenValue).ToHashSet(StringComparer.OrdinalIgnoreCase);
        (string Value, string Label, string Glyph)[] items =
        [
            ("volume", lang.ControlBarIconVolume, "\uE767"),
            ("tracks", lang.ControlBarIconTracks, "\uED1F"),
            ("random", lang.ControlBarIconPlaybackMode, "\uF172"),
            ("speed", lang.ControlBarIconSpeed, "\uEC57"),
            ("aspect", lang.ControlBarIconAspect, "\uE799"),
            ("fullwindow", lang.ControlBarIconFullWindow, "\uF16B"),
            ("fullscreen", lang.ControlBarIconFullScreen, "\uE740"),
            ("pip", lang.ControlBarIconPiP, ControlBarIcons.PictureInPicture),
        ];
        // A checked box means "show this button"; an unchecked box hides it.
        return items
            .Select(x => new OptionCheckItem(x.Value, x.Label, !hidden.Contains(x.Value), x.Item3, target: settingKey))
            .ToList();
    }

    private static void ApplyControlBarIcon(string value, bool isChecked, string? targetKey)
    {
        var key = targetKey ?? (NormalizeControlBarLayout(AppContext.AppSetting.ControlBarLayout) == "modernx"
            ? nameof(AppSettings.ControlBarHiddenIconsModernX)
            : nameof(AppSettings.ControlBarHiddenIconsClassic));
        var current = key == nameof(AppSettings.ControlBarHiddenIconsModernX)
            ? AppContext.AppSetting.ControlBarHiddenIconsModernX
            : AppContext.AppSetting.ControlBarHiddenIconsClassic;
        var list = ParseTokenList(current).ToList();
        if (isChecked)
        {
            list.RemoveAll(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            if (!list.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(value);
            }
        }

        var joined = string.Join(',', list);
        if (key == nameof(AppSettings.ControlBarHiddenIconsModernX))
        {
            AppContext.AppSetting.ControlBarHiddenIconsModernX = joined;
            AppContext.NotifySettingChanged(key, joined);
        }
        else
        {
            AppContext.AppSetting.ControlBarHiddenIconsClassic = joined;
            AppContext.NotifySettingChanged(key, joined);
        }
    }

    private static string NormalizeControlBarLayout(string? value)
    {
        return value switch
        {
            "modernx" or "center" or "right" => "modernx",
            _ => "classic",
        };
    }

}
