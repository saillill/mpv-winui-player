using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace mpv_winui.Modules.Settings;

/// <summary>
/// Maintains the two marked blocks mpv-winui owns inside the deployed mpv.conf.
///
/// <para><b>managed options</b> - config-only options the app writes (ytdl_hook
/// script options, override-display-fps). These cannot be changed at runtime,
/// so they are written before the next mpv start.</para>
///
/// <para><b>user overrides</b> - an empty, annotated block parked at the very
/// end of the file. It is created once if absent and never rewritten, so a user
/// has an obvious place to hand-write an option. mpv reads config top-down, so
/// a line here beats both the managed block and everything the settings window
/// does at runtime: the precedence model in docs/mpv-conf-precedence.md is a
/// position in the file, not a rule in code.</para>
///
/// <para>Ownership rule: the managed block is only rewritten when its contents
/// still match what this class last wrote. If somebody edited it by hand, their
/// copy is left alone, the app's values are commented out under a note saying
/// which line won, and a warning is logged - a hand edit is legitimate and the
/// user carries the consequence, so it must never be silently reverted.</para>
/// </summary>
public static class ManagedMpvConfig
{
    private const string BeginMarker = "# === mpv-winui managed options (do not edit) ===";
    private const string EndMarker = "# === end mpv-winui managed options ===";

    private const string UserBeginMarker = "# === mpv-winui user overrides (edit freely) ===";
    private const string UserEndMarker = "# === end mpv-winui user overrides ===";

    /// <summary>
    /// Opening line of the note written after a hand edit. It doubles as the
    /// anchor <see cref="RemoveYieldNote"/> uses to replace the note rather than
    /// stacking a new one on every launch.
    /// </summary>
    private const string YieldNoteHeader = "# 上面这一块被手工改过，界面里的值不再覆盖它。";

    /// <summary>Matches the manifest convention in ConfigDeployer: "&lt;sha256&gt;\t&lt;block name&gt;".</summary>
    private const string StateFileName = "mpv-winui-managed.json";

    /// <summary>
    /// The deployed mpv.conf this class rewrites, and therefore the one file
    /// whose lines outrank everything the settings window does at runtime.
    ///
    /// Exposed deliberately: the "open mpv.conf" escape hatch has to point at
    /// exactly this file. Deriving the path independently would risk sending
    /// someone to edit a different mpv.conf than the app maintains, which is
    /// the one failure mode that would make the precedence promise a lie.
    /// </summary>
    public static string MpvConfPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "mpv-winui",
        "mpv",
        "mpv.conf");

    /// <summary>True when the deployed config exists, so the UI can disable a
    /// control whose target is missing rather than failing on click.</summary>
    public static bool MpvConfExists => File.Exists(MpvConfPath);

    public static async Task WriteAsync()
    {
        try
        {
            await WriteCoreAsync();
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Error(ex, "managed mpv.conf write failed");
        }
    }

    private static async Task WriteCoreAsync()
    {
        var s = AppContext.AppSetting;
        var lines = new List<string>
        {
            BeginMarker,
            ScriptOpt("ytdl_hook-ytdl_path", s.YtdlPath),
            ScriptOpt("ytdl_hook-try_ytdl_first", s.YtdlTryFirst ? "yes" : "no"),
            ScriptOpt("ytdl_hook-all_formats", s.YtdlAllFormats ? "yes" : "no"),
            ScriptOpt("ytdl_hook-use_manifests", s.YtdlUseManifests ? "yes" : "no"),
            ScriptOpt("ytdl_hook-thumbnails", s.YtdlThumbnails),
            ScriptOpt("ytdl_hook-exclude", s.YtdlExclude),
            MpvOption("override-display-fps", s.OverrideDisplayFps),
            EndMarker,
        };

        var path = MpvConfPath;
        if (!File.Exists(path))
        {
            // Config is normally deployed by deploy-config.ps1; without it the
            // ytdl_hook options cannot take effect, so surface the condition.
            AppContext.AppLogger.Warn("mpv.conf not found at {}, managed options skipped", path);
            return;
        }

        var newBlock = string.Join(Environment.NewLine, lines);
        var text = await File.ReadAllTextAsync(path);
        var changed = false;
        var ourBlockIsInEffect = true;

        // --- managed block: only rewrite while it is still ours ---
        var start = text.IndexOf(BeginMarker, StringComparison.Ordinal);
        var end = text.IndexOf(EndMarker, StringComparison.Ordinal);
        if (start >= 0 && end > start)
        {
            var existingEnd = end + EndMarker.Length;
            var existing = text[start..existingEnd];

            if (IsOurs(existing, ReadState(path)))
            {
                if (!string.Equals(existing, newBlock, StringComparison.Ordinal))
                {
                    text = text[..start] + newBlock + text[existingEnd..];
                    changed = true;
                }
            }
            else
            {
                // A hand edit inside the block. Keep it, and append our values
                // commented out so they stay visible but cannot win.
                AppContext.AppLogger.Warn(
                    "mpv.conf managed block was edited by hand; interface values for {} will not take effect",
                    ConfigOnlyOptionNames());
                ourBlockIsInEffect = false;

                // The previously yielded note is dropped first. Keeping it would
                // make the next run read our own comment as user content, append
                // a second copy, and grow the file on every launch.
                text = RemoveYieldNote(text);
                var blockEnd = text.IndexOf(EndMarker, StringComparison.Ordinal) + EndMarker.Length;
                text = text[..blockEnd]
                    + Environment.NewLine + BuildYieldingBlock(lines[1..^1])
                    + text[blockEnd..];
                changed = true;
            }
        }
        else
        {
            if (text.Length > 0 && !text.EndsWith(Environment.NewLine, StringComparison.Ordinal))
            {
                text += Environment.NewLine;
            }
            text += newBlock + Environment.NewLine;
            changed = true;
        }

        // --- user-overrides block: create once, never touch again ---
        if (text.IndexOf(UserBeginMarker, StringComparison.Ordinal) < 0)
        {
            if (!text.EndsWith(Environment.NewLine, StringComparison.Ordinal))
            {
                text += Environment.NewLine;
            }
            text += Environment.NewLine + BuildUserOverridesBlock() + Environment.NewLine;
            changed = true;
        }

        if (changed)
        {
            // Runs of blank lines accumulate whenever two appends meet; the
            // shipped file is dense, so collapse them back to one.
            text = CollapseBlankLines(text);
            await File.WriteAllTextAsync(path, text, new UTF8Encoding(false));
            if (ourBlockIsInEffect)
            {
                WriteState(path, newBlock);
            }
        }
    }

    /// <summary>
    /// True when the block on disk is byte-identical to what we wrote last time.
    /// No recorded state means a legacy install - treat it as ours, because
    /// every install up to now had this block written by the app and nobody had
    /// a reason to edit a line that says "do not edit".
    /// </summary>
    private static bool IsOurs(string existing, string? expected)
    {
        if (expected is null)
        {
            return true;
        }
        return string.Equals(Normalise(existing), Normalise(expected), StringComparison.Ordinal);
    }

    /// <summary>Line-ending insensitive compare: git may check the file out as CRLF or LF.</summary>
    private static string Normalise(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>Drops the yielded-values note this class writes after a hand edit, if present.</summary>
    private static string RemoveYieldNote(string text)
    {
        var note = text.IndexOf(YieldNoteHeader, StringComparison.Ordinal);
        if (note < 0)
        {
            return text;
        }

        // The note runs to the end of the commented run: drop its header, its
        // two explanatory lines and the blank line that follows.
        var after = text.IndexOf(Environment.NewLine + Environment.NewLine, note, StringComparison.Ordinal);
        return after < 0 ? text[..note] : text[..note] + text[(after + Environment.NewLine.Length * 2)..];
    }

    private static string BuildYieldingBlock(IEnumerable<string> optionLines)
    {
        var sb = new StringBuilder();
        sb.Append(YieldNoteHeader).Append(Environment.NewLine);
        sb.Append("# 界面当前想写入的值（已注释，仅供对照）：").Append(Environment.NewLine);
        foreach (var line in optionLines)
        {
            // A line we already commented out for being empty must not gain a
            // second '#' - "# #foo=" is noise, and "#" alone is what it means.
            var body = line.StartsWith('#') ? line[1..] : line;
            sb.Append("# ").Append(body).Append(Environment.NewLine);
        }
        return sb.ToString();
    }

    /// <summary>Collapses runs of blank lines to a single one.</summary>
    private static string CollapseBlankLines(string text)
    {
        var nl = Environment.NewLine;
        var result = new StringBuilder(text.Length);
        var blankRun = 0;
        foreach (var line in text.Split(nl))
        {
            if (line.Length == 0)
            {
                if (++blankRun > 1)
                {
                    continue;
                }
            }
            else
            {
                blankRun = 0;
            }
            result.Append(line).Append(nl);
        }
        // The split/append round-trip adds one trailing newline; keep exactly one.
        var trimmed = result.ToString().TrimEnd('\r', '\n');
        return trimmed + nl;
    }

    private static string BuildUserOverridesBlock()
    {
        var nl = Environment.NewLine;
        return string.Join(nl, new[]
        {
            UserBeginMarker,
            "# 手写在这里的 mpv 选项优先级最高：它排在文件最后，mpv 由上往下读，",
            "# 所以会覆盖上面的内容，也会覆盖 App 设置界面在运行时下发的值。",
            "#",
            "# 例：",
            "#   sub-font-size = 60",
            "#   hwdec = no",
            "#",
            "# 设置界面里被这里覆盖的项会显示一个「已被 mpv.conf 覆盖」标记。",
            "# 这个块只在文件里不存在时创建一次，之后 App 永不改写它。",
            UserEndMarker,
        });
    }

    private static string ConfigOnlyOptionNames() =>
        string.Join(", ", ManagedOptionNames);

    /// <summary>mpv option names this class writes - the ones a hand edit can shadow.</summary>
    private static readonly string[] ManagedOptionNames =
    {
        "ytdl_hook-ytdl_path", "ytdl_hook-try_ytdl_first", "ytdl_hook-all_formats",
        "ytdl_hook-use_manifests", "ytdl_hook-thumbnails", "ytdl_hook-exclude",
        "override-display-fps",
    };

    // --- state file -------------------------------------------------------

    private static string StatePath(string mpvConfPath) =>
        Path.Combine(Path.GetDirectoryName(mpvConfPath) ?? ".", StateFileName);

    private static string? ReadState(string mpvConfPath)
    {
        try
        {
            var statePath = StatePath(mpvConfPath);
            if (!File.Exists(statePath))
            {
                return null;
            }
            foreach (var line in File.ReadAllLines(statePath))
            {
                var tab = line.IndexOf('\t');
                if (tab > 0 && tab < line.Length - 1
                    && string.Equals(line[..tab], "managed", StringComparison.Ordinal))
                {
                    // Stored hex-encoded so a multi-line block survives one line.
                    return Encoding.UTF8.GetString(Convert.FromHexString(line[(tab + 1)..]));
                }
            }
        }
        catch (Exception ex)
        {
            // A damaged state file must not block the write; treating it as
            // absent is the same path as a legacy install.
            AppContext.AppLogger.Warn(ex, "managed mpv.conf state unreadable, treating as absent");
        }
        return null;
    }

    private static void WriteState(string mpvConfPath, string block)
    {
        try
        {
            var encoded = Convert.ToHexString(Encoding.UTF8.GetBytes(block));
            File.WriteAllText(StatePath(mpvConfPath), "managed\t" + encoded + Environment.NewLine,
                new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Warn(ex, "managed mpv.conf state write failed");
        }
    }

    // --- option formatting ------------------------------------------------

    private static string ScriptOpt(string key, string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? $"#script-opts={key}="
            : $"script-opts={key}={Quote(value)}";
    }

    private static string MpvOption(string key, double value)
    {
        return value > 0
            ? $"{key}={value.ToString(CultureInfo.InvariantCulture)}"
            : $"#{key}=0";
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }
}
