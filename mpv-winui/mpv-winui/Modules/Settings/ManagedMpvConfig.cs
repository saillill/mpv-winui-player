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
/// <para><b>managed options</b> - every option the settings window manages,
/// written as <c>name=value</c>. mpv.conf is the single source of truth: the
/// window is its editor, so a change here is persisted rather than only sent to
/// the running player. The file is therefore inspectable, portable and
/// hand-editable, which is the whole point of the model.</para>
///
/// <para><b>user overrides</b> - an annotated block parked at the very end of
/// the file, created once if absent and never rewritten, so there is an obvious
/// place to hand-write an option. mpv reads config top-down, so a line here
/// beats our block: the precedence model in docs/mpv-conf-precedence.md is a
/// position in the file, not a rule in code.</para>
///
/// <para><b>Ownership rule (per line, not per block).</b> An option line is
/// rewritten only while it still matches what this class last wrote. Anything
/// else - a line edited by hand inside our block, or an active assignment
/// anywhere else in the file - is left exactly as it is, and the option is
/// recorded as user-owned. Ownership is per line because the block now holds
/// two hundred lines: one hand edit must not freeze the other hundred and
/// ninety-nine. The user's value wins because the app stops writing that
/// option, not because a comment says so.</para>
/// </summary>
public static class ManagedMpvConfig
{
    internal const string BeginMarker = "# === mpv-winui managed options (do not edit) ===";
    internal const string EndMarker = "# === end mpv-winui managed options ===";

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
    /// Lines an older build wrote into the managed block that this version no
    /// longer owns, so an upgrade does not strand them.
    ///
    /// Ownership is decided against the recorded state, and a version that
    /// changed the block leaves the record describing something else - which
    /// makes our own old lines read as hand edits, and hand edits are preserved
    /// by design. These prefixes break that tie: the app was the only writer of
    /// them, so they can be dropped outright.
    ///
    /// Safe to delete once no install can be older than the build that removed
    /// the yt-dlp feature.
    /// </summary>
    private static readonly string[] RetiredBlockPrefixes =
    {
        "script-opts=ytdl_hook-",
    };

    private static bool IsRetiredBlockLine(string line) =>
        RetiredBlockPrefixes.Any(p => line.StartsWith(p, StringComparison.OrdinalIgnoreCase));

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
        var path = MpvConfPath;
        if (!File.Exists(path))
        {
            // Config is normally deployed by deploy-config.ps1; without it there
            // is nothing to own, so surface the condition and stop.
            AppContext.AppLogger.Warn("mpv.conf not found at {}, managed options skipped", path);
            return;
        }

        // Ownership is decided from what is on disk right now, so a hand edit
        // made while the app was closed is respected before the first write.
        MpvConfOverrides.Refresh();

        // Options a hand edit owns, from anywhere in the file: a line outside the
        // block, or a line inside it that no longer matches what we wrote. These
        // are excluded from the block below so only the user's copy exists and
        // therefore wins.
        var userOwned = new HashSet<string>(MpvConfOverrides.Names, StringComparer.OrdinalIgnoreCase);
        var ourLines = MpvConfOverrides.OurLines;
        var existingBlock = MpvConfOverrides.CurrentBlockLines;

        var text = await File.ReadAllTextAsync(path);
        var nl = DetectNewLine(text);

        // Lines already in the block that the app did NOT write. Somebody else's
        // edit: carried over verbatim, and their option drops out of `wanted` so
        // the app cannot out-vote them with a second line lower in the file.
        var carried = new List<string>();
        foreach (var line in existingBlock)
        {
            if (ourLines.Contains(Normalise(line)))
            {
                // Ours. Dropped unconditionally: `wanted` re-emits the current
                // value below, and a line we wrote for a setting that has since
                // gone away must not linger.
                continue;
            }

            if (IsRetiredBlockLine(line))
            {
                // Ours from a build whose record no longer describes this block.
                continue;
            }

            carried.Add(line);
            if (AssignmentName(line) is { } foreignName)
            {
                userOwned.Add(foreignName);
            }
        }

        var wanted = new List<string>();
        foreach (var line in MpvSettings.BuildConfigLines())
        {
            var name = line[..line.IndexOf('=')];
            if (!userOwned.Contains(name))
            {
                wanted.Add(line);
            }
        }

        // Hand-owned lines first (they were already there), then our values.
        // Deterministic: same settings in, same block out.
        var blockContent = new List<string>();
        blockContent.AddRange(carried);
        blockContent.AddRange(wanted);

        var newBlock = string.Join(nl,
            new[] { BeginMarker }.Concat(blockContent).Append(EndMarker));

        var start = text.IndexOf(BeginMarker, StringComparison.Ordinal);
        var end = text.IndexOf(EndMarker, StringComparison.Ordinal);

        var changed = false;
        if (start >= 0 && end > start)
        {
            var existingEnd = end + EndMarker.Length;
            var current = text[start..existingEnd];
            if (Normalise(current) != Normalise(newBlock))
            {
                text = text[..start] + newBlock + text[existingEnd..];
                changed = true;
            }
        }
        else
        {
            if (text.Length > 0 && !text.EndsWith(nl, StringComparison.Ordinal))
            {
                text += nl;
            }
            text += newBlock + nl;
            changed = true;
        }

        // The note older builds wrote when they yielded the whole block. Its
        // values now live as ordinary hand-owned lines, so the note is stale.
        var withoutNote = RemoveYieldNote(text);
        if (!string.Equals(withoutNote, text, StringComparison.Ordinal))
        {
            text = withoutNote;
            changed = true;
        }

        // --- user-overrides block ---
        // Created once. The only exception is refreshing its own instructions
        // when the block still holds nothing but those instructions: the text
        // describes how precedence works, and leaving a stale explanation that
        // contradicts the app's behaviour is worse than rewriting a comment
        // nobody wrote. A block with any content of the user's own is never
        // touched, whatever it says.
        var userBlock = FindUserOverridesBlock(text, nl);
        if (userBlock is null)
        {
            if (!text.EndsWith(nl, StringComparison.Ordinal))
            {
                text += nl;
            }
            text += nl + BuildUserOverridesBlock(nl) + nl;
            changed = true;
        }
        else if (userBlock.Value.IsUntouched)
        {
            var current = BuildUserOverridesBlock(nl);
            if (!string.Equals(userBlock.Value.Text, current, StringComparison.Ordinal))
            {
                text = text[..userBlock.Value.Start] + current + text[userBlock.Value.End..];
                changed = true;
            }
        }

        if (changed)
        {
            text = CollapseBlankLines(text, nl);
            await File.WriteAllTextAsync(path, text, new UTF8Encoding(false));
        }

        // Recorded unconditionally: the block on disk is ours by construction
        // now (hand-owned lines were carried over untouched), and recording it
        // is what lets the NEXT run tell an edit from our own output.
        //
        // Only `wanted` is recorded, NOT the carried hand-owned lines. Recording
        // those would adopt somebody's edit into our record, and the next launch
        // would find it matching and treat it as ours - so the value the user
        // set by hand would become writable again on the second start after the
        // edit. The record has to describe what the app wrote, and nothing else.
        WriteState(path, string.Join(nl,
            new[] { BeginMarker }.Concat(wanted).Append(EndMarker)), nl);
        MpvConfOverrides.Refresh();

        if (userOwned.Count > 0)
        {
            // Warn, not Info: the configured log level hides Info, and this is
            // the one line that explains why a settings row stopped taking
            // effect. Without it the behaviour looks like a bug.
            AppContext.AppLogger.Warn(
                "mpv.conf owns {} option(s) by hand; the settings window will not override them: {}",
                userOwned.Count, string.Join(", ", userOwned.OrderBy(n => n, StringComparer.Ordinal)));
        }
    }

    /// <summary>The user-overrides block's extent, and whether anyone has put
    /// content of their own in it. Null when the block is absent.</summary>
    private static (int Start, int End, string Text, bool IsUntouched)?
        FindUserOverridesBlock(string text, string nl)
    {
        var start = text.IndexOf(UserBeginMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        var endMarkerAt = text.IndexOf(UserEndMarker, start, StringComparison.Ordinal);
        if (endMarkerAt < 0)
        {
            return null;
        }

        var end = endMarkerAt + UserEndMarker.Length;
        var block = text[start..end];

        // "Untouched" means every line between the markers is a comment or
        // blank. One real assignment and the whole block is the user's.
        var isUntouched = block
            .Split('\n')
            .Select(l => l.TrimEnd('\r').Trim())
            .All(l => l.Length == 0
                      || l.StartsWith('#')
                      || string.Equals(l, UserBeginMarker, StringComparison.Ordinal)
                      || string.Equals(l, UserEndMarker, StringComparison.Ordinal));

        return (start, end, block, isUntouched);
    }

    /// <summary>The option name of a config line, or null when it is not one.</summary>
    private static string? AssignmentName(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] == '#')
        {
            return null;
        }

        var equals = trimmed.IndexOf('=');
        return equals <= 0 ? null : trimmed[..equals].Trim();
    }

    /// <summary>
    /// The file's own line separator, so a CRLF config does not gain lone LF
    /// lines in the middle. Defaults to the platform's when the file is new.
    /// </summary>
    private static string DetectNewLine(string text)
    {
        var crlf = text.IndexOf("\r\n", StringComparison.Ordinal);
        if (crlf >= 0)
        {
            return "\r\n";
        }
        return text.IndexOf('\n') >= 0 ? "\n" : Environment.NewLine;
    }

    /// <summary>Line-ending insensitive compare: git may check the file out as CRLF or LF.</summary>
    private static string Normalise(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>
    /// Drops the yielded-values note older builds wrote when they gave up the
    /// whole block after a hand edit. Values now live as ordinary hand-owned
    /// lines, so the note is stale - and leaving it would make the next run read
    /// our own comment as user content.
    /// </summary>
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
        if (after < 0)
        {
            after = text.IndexOf("\n\n", note, StringComparison.Ordinal);
        }
        if (after < 0)
        {
            return text[..note];
        }

        // Back up to the start of the line the note begins on, so no dangling
        // indentation is left behind.
        var lineStart = text.LastIndexOf('\n', note);
        return text[..(lineStart < 0 ? note : lineStart + 1)] + text[(after + 2)..];
    }

    /// <summary>Collapses runs of blank lines to a single one.</summary>
    private static string CollapseBlankLines(string text, string nl)
    {
        var result = new StringBuilder(text.Length);
        var blankRun = 0;
        foreach (var line in text.Split('\n'))
        {
            var body = line.TrimEnd('\r');
            if (body.Length == 0)
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
            result.Append(body).Append(nl);
        }

        // The split/append round-trip adds one trailing newline; keep exactly one.
        return result.ToString().TrimEnd('\r', '\n') + nl;
    }

    private static string BuildUserOverridesBlock(string nl)
    {
        return string.Join(nl, new[]
        {
            UserBeginMarker,
            "# 手写在这里的 mpv 选项优先级最高：它排在文件最后，mpv 由上往下读，",
            "# 所以会覆盖上面托管块里的值。",
            "#",
            "# 例：",
            "#   sub-font-size = 60",
            "#   hwdec = no",
            "#",
            "# 被这里覆盖的项，设置界面会显示一个「已被 mpv.conf 覆盖」标记，",
            "# 并且不再把自己的值下发给播放器 —— 也就是以你写的为准。",
            "# 这个块只在文件里不存在时创建一次，之后 App 永不改写它。",
            UserEndMarker,
        });
    }

    // --- state file -------------------------------------------------------

    private static string StatePath(string mpvConfPath) =>
        Path.Combine(Path.GetDirectoryName(mpvConfPath) ?? ".", StateFileName);

    /// <summary>
    /// The normalised lines this class wrote last time.
    ///
    /// This is how a line already in the file is classified: present in the set
    /// means nobody has touched it since, so it is ours to update or drop;
    /// absent means a hand edit, which wins. Null when there is no usable
    /// record, in which case every line reads as hand-written and nothing is
    /// overwritten - the safe direction, since the alternative is clobbering
    /// somebody's edit.
    ///
    /// Keyed by the whole line rather than by option name: a name is not unique
    /// per line (<c>script-opts=a=b</c> parses as "script-opts" every time), so
    /// a name-keyed record would mistake our own repeated names for edits.
    /// </summary>
    internal static IReadOnlySet<string>? ReadRecordedBlock()
    {
        var state = ReadState(MpvConfPath);
        if (state is null)
        {
            return null;
        }

        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in state.Split('\n'))
        {
            var body = line.TrimEnd('\r').Trim();
            if (body.Length == 0
                || body.StartsWith('#')
                || string.Equals(body, BeginMarker, StringComparison.Ordinal)
                || string.Equals(body, EndMarker, StringComparison.Ordinal))
            {
                continue;
            }

            set.Add(Normalise(body));
        }

        return set.Count > 0 ? set : null;
    }

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

    private static void WriteState(string mpvConfPath, string block, string nl)
    {
        try
        {
            var encoded = Convert.ToHexString(Encoding.UTF8.GetBytes(block));
            File.WriteAllText(StatePath(mpvConfPath), "managed\t" + encoded + nl,
                new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Warn(ex, "managed mpv.conf state write failed");
        }
    }

    // --- option formatting ------------------------------------------------

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }
}
