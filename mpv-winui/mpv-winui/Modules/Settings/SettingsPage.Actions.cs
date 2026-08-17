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

                // The section header already shows the group ("音量"), so the
                // row label only needs the final option ("增加音量").
                var separator = label.IndexOf('>');
                if (separator >= 0 && separator < label.Length - 1)
                {
                    label = label[(separator + 1)..].Trim();
                }
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
                Getter = () => ShortcutKeyLocalizer.Localize(shortcutBinding.Key),
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
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "mpv-winui",
                "mpv",
                "input.conf");
            if (!File.Exists(path))
            {
                return;
            }

            var lines = ReadConfigLines(path).ToList();
            var targetIndex = -1;
            var conflictIndex = -1;
            for (var i = 0; i < lines.Count; i++)
            {
                if (!TryParseBindingLine(lines[i], out var lineKey, out var lineCommand))
                {
                    continue;
                }

                // Match by identity (key + command), not by command alone:
                // the same command is often bound to several keys, and a
                // command-string match would rewrite the wrong row.
                if (lineKey == binding.Key && lineCommand == binding.Command && targetIndex < 0)
                {
                    targetIndex = i;
                }
                else if (lineKey == newKey)
                {
                    conflictIndex = i;
                }
            }

            if (targetIndex < 0)
            {
                // The clicked row no longer exists verbatim (the file changed
                // outside the app); leave both the file and the in-memory key
                // untouched.
                return;
            }

            // The rebind wins over whatever was already bound to the new key:
            // drop the conflicting row so input.conf never has two bindings
            // for the same key (mpv would let the last one win by line order).
            if (conflictIndex >= 0)
            {
                lines.RemoveAt(conflictIndex);
                if (conflictIndex < targetIndex)
                {
                    targetIndex--;
                }
            }

            var trimmed = lines[targetIndex].Trim();
            var hash = trimmed.IndexOf('#');
            var bindingText = (hash >= 0 ? trimmed[..hash] : trimmed).Trim();
            var parts = bindingText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return;
            }

            var firstToken = parts[0];
            var tokenIndex = lines[targetIndex].IndexOf(firstToken, StringComparison.Ordinal);
            if (tokenIndex < 0)
            {
                return;
            }

            lines[targetIndex] = lines[targetIndex][..tokenIndex] + newKey + lines[targetIndex][(tokenIndex + firstToken.Length)..];
            binding.Key = newKey;
            WriteConfigLines(path, lines);
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Error(ex, "RebindShortcut failed");
        }
    }

    /// <summary>Parses a key binding line into its key token and command text.</summary>
    private static bool TryParseBindingLine(string line, out string key, out string command)
    {
        key = string.Empty;
        command = string.Empty;

        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
        {
            return false;
        }

        var hash = trimmed.IndexOf('#');
        var bindingText = (hash >= 0 ? trimmed[..hash] : trimmed).Trim();
        var parts = bindingText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        key = parts[0];
        command = string.Join(' ', parts.Skip(1));
        return true;
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
        // Normalize CRLF/LF/CR but keep blank lines: a rebind rewrite must
        // preserve the file layout. Callers skip empty entries themselves.
        return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
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
        lock (DeviceChoicesLock)
        {
            if (_audioDeviceChoicesCache is not null)
            {
                return _audioDeviceChoicesCache;
            }
        }

        var choices = new List<OptionChoice>
        {
            new("auto", AppContext.AppLang.OptionValueAuto),
        };

        var enumerated = false;
        try
        {
            var devices = AppContext.GetAudioDevices?.Invoke();
            if (devices is not null)
            {
                enumerated = true;
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

        // Only cache when the player's audio-device source was available; a
        // premature cache would pin the auto-only list forever.
        if (enumerated)
        {
            lock (DeviceChoicesLock)
            {
                _audioDeviceChoicesCache = choices;
            }
        }
        return choices;
    }

    private static readonly object DeviceChoicesLock = new();
    private static List<OptionChoice>? _audioDeviceChoicesCache;
    private static List<OptionChoice>? _gpuChoicesCache;

    /// <summary>
    /// Warms the cached device-choice lists on a background thread so opening
    /// the settings page never blocks the UI thread on WMI/native enumeration
    /// (audit A2). The synchronous providers still fall back to a first-call
    /// enumeration when the warm-up has not finished.
    /// </summary>
    internal static void WarmDeviceChoices()
    {
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                _ = BuildGpuAdapterChoices();
                _ = BuildAudioDeviceChoices();
            }
            catch (Exception ex)
            {
                AppContext.AppLogger.Warn(ex, "Device choice warm-up failed");
            }
        });
    }

    /// <summary>Lists installed display adapters (DXGI descriptions) for d3d11-adapter.</summary>
    private static List<OptionChoice> BuildGpuAdapterChoices()
    {
        lock (DeviceChoicesLock)
        {
            if (_gpuChoicesCache is not null)
            {
                return _gpuChoicesCache;
            }
        }

        var choices = new List<OptionChoice>
        {
            new("", AppContext.AppLang.OptionValueAuto),
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var enumerated = false;

        // DXGI is the official, fast enumeration path (audit A2); the player
        // page provides it through the native component. WMI remains the
        // fallback for a settings session without an initialized player.
        var dxgiAdapters = AppContext.GetGpuAdapters?.Invoke();
        if (dxgiAdapters is not null)
        {
            enumerated = true;
            foreach (var adapter in dxgiAdapters)
            {
                var name = string.IsNullOrWhiteSpace(adapter.Description)
                    ? adapter.Name
                    : adapter.Description;
                if (!string.IsNullOrWhiteSpace(name)
                    && !IsVirtualDisplayAdapter(name)
                    && seen.Add(name))
                {
                    choices.Add(new OptionChoice(name, name));
                }
            }
        }
        else
        {
            try
            {
                // The display-class registry lists every registered adapter,
                // including disabled/headless cards. Only adapters currently
                // driving a display (non-zero current resolution) are usable
                // for d3d11 presentation.
                using var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT Name, CurrentHorizontalResolution FROM Win32_VideoController");
                using var results = searcher.Get();
                foreach (System.Management.ManagementObject obj in results)
                {
                    using (obj)
                    {
                        if (obj["Name"] is string name
                            && !string.IsNullOrWhiteSpace(name)
                            && obj["CurrentHorizontalResolution"] is uint resolution
                            && resolution > 0
                            && !IsVirtualDisplayAdapter(name)
                            && seen.Add(name))
                        {
                            choices.Add(new OptionChoice(name, name));
                        }
                    }
                }
                enumerated = true;
            }
            catch (Exception ex)
            {
                AppContext.AppLogger.Error(ex, "Failed to enumerate display adapters");
            }
        }

        // Only cache when a source was available; a premature cache would pin
        // the auto-only list before the player starts.
        if (!enumerated)
        {
            return choices;
        }

        lock (DeviceChoicesLock)
        {
            _gpuChoicesCache = choices;
        }
        return choices;
    }

    /// <summary>Skips software/remote display adapters that are not real GPUs.</summary>
    private static bool IsVirtualDisplayAdapter(string description) =>
        description.Contains("Basic Display", StringComparison.OrdinalIgnoreCase)
        || description.Contains("Remote Display", StringComparison.OrdinalIgnoreCase)
        || description.Contains("基本显示", StringComparison.Ordinal)
        || description.Contains("远程显示", StringComparison.Ordinal);

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

    /// <summary>Profiles applied through the settings checklist in this session.</summary>
    private static readonly HashSet<string> AppliedProfiles = new(StringComparer.Ordinal);

    private sealed record MpvProfileInfo(string Name, string? Description);

    private static List<OptionCheckItem> BuildProfileItems()
    {
        // mpv's runtime profile-list is the authoritative source (it includes
        // built-in profiles such as gpu-hq); profiles.conf only enriches the
        // rows with their profile-desc. Fall back to the file alone when no
        // player is initialized.
        var fileDescriptions = ReadMpvProfiles()
            .ToDictionary(p => p.Name, p => p.Description, StringComparer.Ordinal);
        var runtimeProfiles = AppContext.GetMpvProfiles?.Invoke();
        if (runtimeProfiles is { Count: > 0 })
        {
            return runtimeProfiles
                .Select(p => new OptionCheckItem(p.Name, p.Name, AppliedProfiles.Contains(p.Name))
                {
                    Description = fileDescriptions.TryGetValue(p.Name, out var description) ? description : null,
                })
                .ToList();
        }

        return fileDescriptions
            .Select(pair => new OptionCheckItem(pair.Key, pair.Key, AppliedProfiles.Contains(pair.Key))
            {
                Description = pair.Value,
            })
            .ToList();
    }

    /// <summary>
    /// Reads profile names and profile-desc from the user's profiles.conf.
    /// Read-only: mpv owns the file, and applying happens at runtime with the
    /// apply-profile command (config-time profile edits need a restart).
    /// </summary>
    private static List<MpvProfileInfo> ReadMpvProfiles()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "mpv-winui",
            "mpv",
            "profiles.conf");
        var profiles = new List<MpvProfileInfo>();
        try
        {
            if (!File.Exists(path))
            {
                return profiles;
            }

            string? currentName = null;
            string? currentDescription = null;
            foreach (var rawLine in File.ReadLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.Length > 2 && line[0] == '[' && line[^1] == ']')
                {
                    if (currentName is not null)
                    {
                        profiles.Add(new MpvProfileInfo(currentName, currentDescription));
                    }
                    currentName = line[1..^1].Trim();
                    currentDescription = null;
                    continue;
                }

                if (currentName is null || currentDescription is not null)
                {
                    continue;
                }

                var eq = line.IndexOf('=');
                if (eq > 0 && line[..eq].Trim().Equals("profile-desc", StringComparison.OrdinalIgnoreCase))
                {
                    currentDescription = UnquoteMpvValue(line[(eq + 1)..].Trim());
                }
            }

            if (currentName is not null)
            {
                profiles.Add(new MpvProfileInfo(currentName, currentDescription));
            }
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Warn(ex, "failed to read profiles.conf");
        }

        return profiles;
    }

    private static string UnquoteMpvValue(string value)
    {
        if (value.Length >= 2)
        {
            var first = value[0];
            var last = value[^1];
            if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
            {
                return value[1..^1];
            }
        }
        return value;
    }

    private static void ApplyProfile(string name, bool isChecked)
    {
        if (isChecked)
        {
            AppliedProfiles.Add(name);
            AppContext.SendMpvCommand($"apply-profile {QuoteMpvArg(name)}");
            return;
        }

        // mpv has no "unapply": removing the session marker only changes the
        // checklist state; the profile stays active until another overrides it.
        AppliedProfiles.Remove(name);
    }

    private static string QuoteMpvArg(string value)
    {
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
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
                // InvariantCulture: export must round-trip through the
                // InvariantCulture-based import regardless of the user's
                // number format (comma-decimal regions would otherwise emit
                // "0,5" which fails to parse back on import).
                var text = entry.Value switch
                {
                    double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _ => entry.Value?.ToString() ?? string.Empty,
                };
                builder.Append(entry.Key)
                    .Append('=')
                    .AppendLine(text);
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
