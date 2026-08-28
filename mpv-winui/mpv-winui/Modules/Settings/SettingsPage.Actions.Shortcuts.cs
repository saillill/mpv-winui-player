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
            else if (label.Length > 0)
            {
                // Non-menu rows use "#键描述 功能描述"; keep only the function
                // part so the row name is not bound to a key name.
                var space = label.IndexOf(' ');
                if (space > 0 && space < label.Length - 1)
                {
                    label = label[(space + 1)..].Trim();
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
