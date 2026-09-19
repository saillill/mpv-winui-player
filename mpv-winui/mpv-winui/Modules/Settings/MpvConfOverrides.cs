using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace mpv_winui.Modules.Settings;

/// <summary>
/// Reads the deployed mpv.conf and works out, option by option, who owns it.
///
/// <para><b>Why ownership rather than "is it mentioned".</b> The settings
/// window now persists its values into mpv.conf, so almost every option is
/// named there. Asking only "does the file name this option" would mark the
/// whole window as overridden, which is the opposite of the truth: those lines
/// are ours and the window wins. The useful question is which lines the app did
/// NOT write, because only those can beat it.</para>
///
/// <para><b>The rule.</b> An option is user-owned when an active assignment
/// exists that the app did not write - either a line outside the managed block,
/// or a line inside it that no longer matches what the app last wrote. A
/// user-owned option is left alone by the writer and skipped by the runtime
/// apply, so the config file's value genuinely stands. That is what makes a
/// hand edit before launch win.</para>
///
/// <para>Parsing stays deliberately shallow: top-level name=value only, and
/// profile sections are ignored since they apply only when activated. It never
/// writes. A parse failure means "nothing is known to be user-owned", which is
/// the safe reading - the window keeps working and its own lines stay its
/// own.</para>
/// </summary>
public static class MpvConfOverrides
{
    /// <summary>A top-level assignment: optional indent, name, '=', value.</summary>
    private static readonly Regex Assignment = new(
        @"^\s*([A-Za-z][A-Za-z0-9_-]*)\s*=\s*(?<value>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly object Gate = new();

    /// <summary>Option name -> the active line a user owns.</summary>
    private static readonly Dictionary<string, string> UserOwnedBy =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Option name -> line inside our block that is still the one we wrote.</summary>
    private static readonly Dictionary<string, string> OursBy =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Lines the app wrote last time, normalised, as a set.
    ///
    /// Identity is the whole line rather than its option name, because a name is
    /// not always unique per line: <c>script-opts=a=b</c> parses as the option
    /// "script-opts" for every one of its lines, so a name-keyed map would
    /// classify all but the first as a hand edit and refuse to drop them.
    /// </summary>
    private static readonly HashSet<string> OurLineSet = new(StringComparer.Ordinal);

    /// <summary>Every line currently between the block markers, in file order.</summary>
    private static readonly List<string> BlockLinesInner = new();

    private static bool _loaded;

    /// <summary>
    /// The mpv options a hand edit currently owns: the ones the writer must not
    /// touch and the runtime apply must not send.
    /// </summary>
    public static IReadOnlyCollection<string> Names
    {
        get
        {
            EnsureLoaded();
            lock (Gate)
            {
                return UserOwnedBy.Keys.ToArray();
            }
        }
    }

    /// <summary>True when mpv.conf currently owns this mpv option.</summary>
    public static bool IsUserOwned(string? mpvOptionName)
    {
        if (string.IsNullOrEmpty(mpvOptionName))
        {
            return false;
        }

        EnsureLoaded();
        lock (Gate)
        {
            return UserOwnedBy.ContainsKey(mpvOptionName);
        }
    }

    /// <summary>The normalised text of every line inside the managed block that
    /// the app still owns. The writer drops these and re-emits current values;
    /// anything else in the block is somebody's edit and is preserved verbatim.</summary>
    internal static IReadOnlySet<string> OurLines
    {
        get
        {
            EnsureLoaded();
            lock (Gate)
            {
                return new HashSet<string>(OurLineSet, StringComparer.Ordinal);
            }
        }
    }

    /// <summary>Raw lines currently in the managed block, in file order.</summary>
    internal static IReadOnlyList<string> CurrentBlockLines
    {
        get
        {
            EnsureLoaded();
            lock (Gate)
            {
                return BlockLinesInner.ToArray();
            }
        }
    }

    /// <summary>
    /// Re-reads the file. Called at startup and after the settings window
    /// rewrites the managed block, so a stale verdict cannot survive a change.
    /// </summary>
    public static void Refresh()
    {
        var path = ManagedMpvConfig.MpvConfPath;

        string[] lines;
        try
        {
            if (!File.Exists(path))
            {
                Reset(Array.Empty<string>());
                return;
            }
            lines = File.ReadAllLines(path);
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Warn(ex, "mpv.conf unreadable, nothing marked as hand-edited");
            Reset(Array.Empty<string>());
            return;
        }

        // What the app wrote last time, to tell its own lines from edits.
        var recorded = ManagedMpvConfig.ReadRecordedBlock();

        var userOwned = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ourLines = new HashSet<string>(StringComparer.Ordinal);
        var blockLines = new List<string>();

        var inProfile = false;
        var inBlock = false;
        var blockClosed = false;

        foreach (var raw in lines)
        {
            var trimmed = raw.Trim();

            if (!inBlock && !blockClosed
                && string.Equals(trimmed, ManagedMpvConfig.BeginMarker, StringComparison.Ordinal))
            {
                inBlock = true;
                continue;
            }

            if (inBlock
                && string.Equals(trimmed, ManagedMpvConfig.EndMarker, StringComparison.Ordinal))
            {
                inBlock = false;
                blockClosed = true;
                continue;
            }

            var line = StripComment(raw);
            if (line.Length == 0)
            {
                continue;
            }

            if (inBlock)
            {
                // Keep the raw line so a rewrite can preserve an edit verbatim.
                var here = raw.TrimEnd();
                blockLines.Add(here);

                if (Assignment.Match(line) is not { Success: true } m)
                {
                    continue;
                }

                if (recorded is not null
                    && recorded.Contains(Normalise(here)))
                {
                    // Byte-for-byte what we wrote last time: nobody has touched
                    // it, so it is ours to update or drop.
                    ourLines.Add(Normalise(here));
                }
                else
                {
                    // Changed by hand inside our own block.
                    userOwned[m.Groups[1].Value] = here;
                }
                continue;
            }

            if (blockClosed)
            {
                // Everything after the block - the user-overrides block and
                // anything below it - is the user's by construction.
                ApplyAssignment(line, raw, userOwned);
                continue;
            }

            // Before the block: the shipped config body.
            if (line.TrimStart().StartsWith('['))
            {
                inProfile = true;
                continue;
            }

            if (inProfile)
            {
                continue;
            }

            ApplyAssignment(line, raw, userOwned);
        }

        lock (Gate)
        {
            UserOwnedBy.Clear();
            foreach (var kv in userOwned)
            {
                UserOwnedBy[kv.Key] = kv.Value;
            }

            OurLineSet.Clear();
            OurLineSet.UnionWith(ourLines);

            BlockLinesInner.Clear();
            BlockLinesInner.AddRange(blockLines);
            _loaded = true;
        }
    }

    private static void Reset(IReadOnlyList<string> blockLines)
    {
        lock (Gate)
        {
            UserOwnedBy.Clear();
            OurLineSet.Clear();
            BlockLinesInner.Clear();
            BlockLinesInner.AddRange(blockLines);
            _loaded = true;
        }
    }

    private static void ApplyAssignment(
        string line, string raw, Dictionary<string, string> sink)
    {
        if (Assignment.Match(line) is not { Success: true } m)
        {
            return;
        }

        // First occurrence wins: an earlier active line is one the app did not
        // write, which is the fact that matters.
        sink.TryAdd(m.Groups[1].Value, raw.TrimEnd());
    }

    private static void EnsureLoaded()
    {
        lock (Gate)
        {
            if (_loaded)
            {
                return;
            }
        }
        Refresh();
    }

    /// <summary>
    /// Drops a trailing "# ..." comment and rejects a line that is entirely
    /// commented. The shipped config annotates nearly every line, and option
    /// values here are never quoted strings containing '#'.
    /// </summary>
    private static string StripComment(string raw)
    {
        var trimmed = raw.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] == '#')
        {
            return string.Empty;
        }

        var hash = raw.IndexOf('#');
        var body = hash >= 0 ? raw[..hash] : raw;
        return body.TrimEnd();
    }

    /// <summary>Line-ending insensitive compare: git may check the file out as CRLF or LF.</summary>
    private static string Normalise(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
}
