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

    /// <summary>Builds a click-to-rebind shortcut list from the deployed input.conf.</summary>
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
        foreach (var raw in ReadConfigLines(path))
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
            var command = string.Join(' ', parts.Skip(1));

            // Menu-only rows use the "_" placeholder key or an "ignore" command
            // and are not real key bindings, so they do not belong in the list.
            if (key == "_" || string.Equals(command, "ignore", StringComparison.Ordinal))
            {
                continue;
            }

            var label = comment;
            string? section = null;
            if (label.StartsWith("menu:", StringComparison.OrdinalIgnoreCase))
            {
                label = label["menu:".Length..].Trim();
                var stateHash = label.IndexOf('#');
                if (stateHash >= 0)
                {
                    label = label[..stateHash].Trim();
                }
                section = ShortcutSectionLabel(ShortcutSectionKey(label));
            }

            if (!seen.Add(key))
            {
                continue;
            }

            var shortcutBinding = new ShortcutBinding { Key = key, Command = command };
            options.Add(new Option
            {
                Key = $"Shortcut:{index++}",
                Label = string.IsNullOrEmpty(label) ? command : label,
                Category = shortcutsCategory,
                Section = section,
                Type = OptionType.String,
                ReadOnly = true,
                KeyCaptureEditable = true,
                KeyCaptureDefault = key,
                Getter = () => shortcutBinding.Key,
                Setter = _ => { },
                KeyCaptureReplaced = (_, newKey) => RebindShortcut(shortcutBinding, newKey),
                KeyCaptureReset = option => RebindShortcut(shortcutBinding, option.KeyCaptureDefault ?? shortcutBinding.Key),
            });

            if (options.Count >= 240)
            {
                break;
            }
        }
        return options;
    }

    /// <summary>Maps the first path segment of a "#menu:" comment to a shortcut group.</summary>
    private static string ShortcutSectionKey(string menuLabel)
    {
        var top = menuLabel;
        var gt = menuLabel.IndexOf('>');
        if (gt >= 0)
        {
            top = menuLabel[..gt].Trim();
        }

        return top switch
        {
            "播放" or "暂停" or "停止" or "播放列表" or "章节" or "版本" or "轨道" => "playback",
            "导航" => "navigation",
            "视频" => "video",
            "音频" => "audio",
            "字幕" => "subtitle",
            "速度" => "speed",
            "音量" => "volume",
            "滤镜与增强" => "filters",
            "工具" => "tools",
            "查看" => "view",
            "截屏" => "screenshot",
            _ => "other",
        };
    }

    private static string ShortcutSectionLabel(string key)
    {
        var lang = AppContext.AppLang;
        return key switch
        {
            "playback" => lang.ShortcutSectionPlayback,
            "navigation" => lang.ShortcutSectionNavigation,
            "video" => lang.ShortcutSectionVideo,
            "audio" => lang.ShortcutSectionAudio,
            "subtitle" => lang.ShortcutSectionSubtitle,
            "speed" => lang.ShortcutSectionSpeed,
            "volume" => lang.ShortcutSectionVolume,
            "filters" => lang.ShortcutSectionFilters,
            "tools" => lang.ShortcutSectionTools,
            "view" => lang.ShortcutSectionView,
            "screenshot" => lang.ShortcutSectionScreenshot,
            _ => lang.ShortcutSectionOther,
        };
    }

    private sealed class ShortcutBinding
    {
        public string Key = string.Empty;
        public string Command = string.Empty;
    }

    private static void RebindShortcut(ShortcutBinding binding, string newKey)
    {
        if (string.IsNullOrWhiteSpace(newKey) || newKey == binding.Key)
        {
            return;
        }

        try
        {
            binding.Key = newKey;
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "mpv-winui",
                "mpv",
                "input.conf");
            if (!File.Exists(path))
            {
                return;
            }

            var lines = ReadConfigLines(path).ToArray();
            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                {
                    continue;
                }

                var hash = trimmed.IndexOf('#');
                var bindingText = (hash >= 0 ? trimmed[..hash] : trimmed).Trim();
                var parts = bindingText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    continue;
                }

                var command = string.Join(' ', parts.Skip(1));
                if (string.Equals(command, binding.Command, StringComparison.Ordinal))
                {
                    var firstToken = parts[0];
                    var tokenIndex = lines[i].IndexOf(firstToken, StringComparison.Ordinal);
                    if (tokenIndex >= 0)
                    {
                        lines[i] = lines[i][..tokenIndex] + newKey + lines[i][(tokenIndex + firstToken.Length)..];
                    }
                    break;
                }
            }

            WriteConfigLines(path, lines);
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Error(ex, "RebindShortcut failed");
        }
    }

    /// <summary>
    /// Reads a config file as UTF-8 when possible and falls back to GB18030
    /// (the legacy mpv-lazy encoding) so Chinese comments never get corrupted.
    /// </summary>
    private static IEnumerable<string> ReadConfigLines(string path)
    {
        var bytes = File.ReadAllBytes(path);
        string text;
        try
        {
            text = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (System.Text.DecoderFallbackException)
        {
            text = System.Text.Encoding.GetEncoding("GB18030").GetString(bytes);
        }
        return text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>Writes a config file as UTF-8 without a BOM so mpv and the app agree.</summary>
    private static void WriteConfigLines(string path, IEnumerable<string> lines)
    {
        File.WriteAllLines(path, lines, new System.Text.UTF8Encoding(false));
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
            // Backdrop tint/transparency/brightness only apply in Custom theme mode.
            nameof(AppSettings.ThemeAccentColor) when s.ThemeType != AppSettings.ThemeType_Custom => false,
            nameof(AppSettings.ThemeOpacity) when s.ThemeType != AppSettings.ThemeType_Custom => false,
            nameof(AppSettings.ThemeLuminosity) when s.ThemeType != AppSettings.ThemeType_Custom => false,
            // MicaController ignores luminosity, so brightness only shows for Acrylic.
            nameof(AppSettings.ThemeLuminosity) when s.BackdropType != AppSettings.BackdropType_Acrylic => false,
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
            nameof(AppSettings.ScreenshotHighBitDepth) when s.ScreenshotFormat is not ("png" or "webp") => false,
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
        var selected = ParseTokenList(AppContext.AppSetting.FileAssociationExts).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in AssociationExtensions)
        {
            if (selected.Contains(extension))
            {
                RegisterExtension(extension);
            }
            else
            {
                UnregisterExtension(extension);
            }
        }
        _actionStatus = AppContext.AppLang.SettingsAssociateDone;
    }

    private static void RegisterExtension(string extension)
    {
        var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exe))
        {
            return;
        }

        using (var command = Registry.CurrentUser.CreateSubKey(@"Software\Classes\mpv-winui.media\shell\open\command"))
        {
            command.SetValue(string.Empty, $"\"{exe}\" \"%1\"");
        }

        using (var icon = Registry.CurrentUser.CreateSubKey(@"Software\Classes\mpv-winui.media\DefaultIcon"))
        {
            icon.SetValue(string.Empty, $"\"{exe}\",0");
        }

        using var extKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + extension);
        // Never overwrite an association owned by another application (a
        // foreign legacy default value or a Windows-managed UserChoice).
        // Advertise through OpenWithProgids instead, which is the
        // non-destructive way to appear in "Open with" without stealing the
        // user's current default app.
        var currentDefault = extKey.GetValue(string.Empty) as string;
        if (string.IsNullOrEmpty(currentDefault)
            || string.Equals(currentDefault, "mpv-winui.media", StringComparison.OrdinalIgnoreCase))
        {
            extKey.SetValue(string.Empty, "mpv-winui.media");
        }

        using var openWith = extKey.CreateSubKey("OpenWithProgids");
        openWith.SetValue("mpv-winui.media", Array.Empty<byte>());
    }

    private void UnassociateFiles()
    {
        foreach (var extension in ParseTokenList(AppContext.AppSetting.FileAssociationExts))
        {
            UnregisterExtension(extension);
        }

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\mpv-winui.media", throwOnMissingSubKey: false);
        }
        catch (System.Exception)
        {
        }

        AppContext.AppSetting.FileAssociationExts = string.Empty;
        _actionStatus = AppContext.AppLang.SettingsUnassociateDone;
    }

    private static void UnregisterExtension(string extension)
    {
        const string progId = "mpv-winui.media";
        try
        {
            using var extKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + extension, writable: true);
            if (extKey is null)
            {
                return;
            }

            // Remove our ProgID from the shared "Open with" list; the list
            // belongs to every registered application, so only our value.
            using (var openWith = extKey.OpenSubKey("OpenWithProgids", writable: true))
            {
                openWith?.DeleteValue(progId, throwOnMissingValue: false);
            }

            // If the legacy default is not ours, leave everything else alone
            // (another application owns the association).
            var currentDefault = extKey.GetValue(string.Empty) as string;
            if (!string.Equals(currentDefault, progId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            extKey.DeleteValue(string.Empty, throwOnMissingValue: false);

            var subKeys = extKey.GetSubKeyNames();
            var hasUserChoice = subKeys.Any(k => string.Equals(k, "UserChoice", StringComparison.OrdinalIgnoreCase));
            if (hasUserChoice)
            {
                // UserChoice is owned by Windows (per-user default app); never
                // delete it, even when it currently points at this app.
                return;
            }

            var emptyOpenWith = subKeys.Contains("OpenWithProgids", StringComparer.OrdinalIgnoreCase)
                && extKey.OpenSubKey("OpenWithProgids")?.GetValueNames().Length == 0;
            if (emptyOpenWith == true)
            {
                extKey.DeleteSubKeyTree("OpenWithProgids", throwOnMissingSubKey: false);
                subKeys = extKey.GetSubKeyNames();
            }

            if (extKey.GetValueNames().Length == 0 && subKeys.Length == 0)
            {
                extKey.Close();
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + extension, throwOnMissingSubKey: false);
            }
        }
        catch (System.Exception)
        {
            // Some extensions may be owned by another application; keep going.
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
            ("random", lang.ControlBarIconRandom, ControlBarIcons.Shuffle),
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

    private void FireAndForgetExport()
    {
        _ = ExportConfigAsync();
    }

    private void FireAndForgetImport()
    {
        _ = ImportConfigAsync();
    }

    private async System.Threading.Tasks.Task ExportConfigAsync()
    {
        try
        {
            var owner = SettingsWindow.Instance?.AppWindow.Id ?? App.Window!.AppWindow.Id;
            var filePicker = new FileSavePicker(owner)
            {
                SuggestedFileName = "mpv-winui-settings.conf",
            };
            filePicker.FileTypeChoices["Settings"] = new List<string> { ".conf" };
            var file = await filePicker.PickSaveFileAsync();
            if (file is null)
            {
                return;
            }

            var builder = new System.Text.StringBuilder();
            foreach (var entry in AppContext.AppSetting.ExportAll())
            {
                builder.Append(entry.Key)
                    .Append('=')
                    .AppendLine(entry.Value?.ToString() ?? string.Empty);
            }
            await File.WriteAllTextAsync(file.Path, builder.ToString());
            _actionStatus = AppContext.AppLang.SettingsConfigExported;
        }
        catch (System.Exception ex)
        {
            AppContext.AppLogger.Error(ex, "Export config failed");
        }
    }

    private async System.Threading.Tasks.Task ImportConfigAsync()
    {
        try
        {
            var owner = SettingsWindow.Instance?.AppWindow.Id ?? App.Window!.AppWindow.Id;
            var filePicker = new FileOpenPicker(owner);
            filePicker.FileTypeFilter.Add(".conf");
            filePicker.FileTypeFilter.Add("*");
            var file = await filePicker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var line in await File.ReadAllLinesAsync(file.Path))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed[0] == '#')
                {
                    continue;
                }

                var equals = trimmed.IndexOf('=');
                if (equals <= 0)
                {
                    continue;
                }

                values[trimmed[..equals].Trim()] = trimmed[(equals + 1)..];
            }

            AppContext.AppSetting.ImportAll(values);
            _actionStatus = AppContext.AppLang.SettingsConfigImported;
            Frame?.BackStack.Clear();
            Frame?.Navigate(typeof(SettingsPage));
        }
        catch (System.Exception ex)
        {
            AppContext.AppLogger.Error(ex, "Import config failed");
        }
    }

    private void ResetShortcuts()
    {
        var bundled = Path.Combine(System.AppContext.BaseDirectory, "Config", "input.conf");
        var target = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "mpv-winui",
            "mpv",
            "input.conf");
        if (!File.Exists(bundled))
        {
            _actionStatus = AppContext.AppLang.ResetShortcutsMissing;
            return;
        }

        File.Copy(bundled, target, overwrite: true);
        _actionStatus = AppContext.AppLang.ResetShortcutsDone;    }
}
