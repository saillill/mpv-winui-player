using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace mpv_winui.Modules.Settings;

/// <summary>
/// Reads the deployed mpv.conf and works out, option by option, who owns it.
///
/// <para><b>Three owners, not two.</b> A line in this file is the app's, the
/// user's, or the shipped body's - and the third is what the first version of
/// this class got wrong. The file has three regions: the shipped body, our
/// managed block, and everything after it. Treating "anywhere but inside our
/// markers" as the user's folds the body in with the user's edits, and the body
/// carries about a hundred active assignments, so nearly every row in the
/// settings window was announced as user-owned and disabled. The symptom looked
/// like "the settings page shows nothing"; the page was fine, the options were
/// all read-only.</para>
///
/// <para><b>How the body is classified.</b> By comparison against the config the
/// app ships, read from the bundled layer and falling back to a recorded
/// baseline when an install has one
/// (<see cref="ManagedMpvConfig.ReadRecordedBody"/>). A body line that still
/// matches was never touched, so the window may rewrite it. A line that differs,
/// or that has no counterpart, is an edit and wins. Measured against the live
/// deployed config that is 104 of 105 active body lines claimed as ours, with the
/// one exception being a genuine edit. This is the same line-for-line contract
/// the managed block already uses, so the two regions now answer the same
/// question the same way.</para>
///
/// <para><b>The rule for the block.</b> An option is user-owned when an active
/// assignment exists that the app did not write - either a line outside the
/// body and the block, or a line inside the block that no longer matches what
/// the app last wrote. A user-owned option is left alone by the writer and
/// skipped by the runtime apply, so the config file's value genuinely stands.
/// That is what makes a hand edit before launch win.</para>
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
    /// Body lines still identical to the shipped baseline, normalised.
    ///
    /// Populated in <see cref="Refresh"/> by walking the body a second time once
    /// the baseline is known. Kept separate from <see cref="OurLineSet"/> so the
    /// two regions stay distinguishable in diagnostics.
    /// </summary>
    private static readonly HashSet<string> BodyBaselineLines = new(StringComparer.Ordinal);

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

    /// <summary>
    /// The mpv options an unmodified body line already carries.
    ///
    /// <para>These are neither ours-to-remove nor the user's-to-protect, and
    /// they are the reason the writer must consult this list at all. The body
    /// ships around a hundred active assignments in its documented
    /// <c>name = value  # comment</c> form; a managed-block copy of the same
    /// option would be redundant, and because mpv resolves top-down the block
    /// copy is the one that wins - silently overriding a body line the user
    /// later edits in place. Skipping them keeps a shipped option in exactly one
    /// place, the place it was documented.</para>
    ///
    /// <para>Writing a body option into the block is what made the two regions
    /// overlap on 79 options before this list existed; the file still behaved
    /// correctly only because the later line won.</para>
    /// </summary>
    public static IReadOnlyCollection<string> BodyOwnedNames
    {
        get
        {
            EnsureLoaded();
            lock (Gate)
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in BodyBaselineLines)
                {
                    if (Assignment.Match(line) is { Success: true } m)
                    {
                        names.Add(m.Groups[1].Value);
                    }
                }
                return names.ToArray();
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

    /// <summary>
    /// How many body lines matched the shipped config, and whether a baseline
    /// was available at all.
    ///
    /// <para>Exposed for the same reason the ownership warning is logged: when a
    /// row goes read-only the question is always "who claimed it", and the
    /// answer hinges on whether a baseline was found. Zero lines with a baseline
    /// present means the shipped body no longer resembles what this build
    /// ships - the recognition failed, and every option there just went
    /// read-only. No baseline means neither the bundle nor a record was
    /// readable, so the app is deliberately keeping its hands off.</para>
    /// </summary>
    internal static (bool HasBaseline, int MatchedLines) BodyDiagnostics
    {
        get
        {
            EnsureLoaded();
            lock (Gate)
            {
                return (ManagedMpvConfig.ReadRecordedBody() is not null, BodyBaselineLines.Count);
            }
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

        // The config body the app ships, used to tell a body line we own from
        // one the user edited. Null only when neither a recorded baseline nor
        // the bundled template is available - a source run without the layer -
        // in which case the body is treated as the user's: the safe direction,
        // since the alternative is rewriting a config we cannot verify.
        var bodyBaseline = ManagedMpvConfig.ReadRecordedBody();

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

            // The body is the app's wherever it still matches what the app
            // ships; only a line the user changed (or added) claims the option.
            // Without a baseline there is nothing to compare against, so the
            // body is left as the user's -- see bodyBaseline's comment.
            if (bodyBaseline is not null && bodyBaseline.Contains(Normalise(raw.TrimEnd())))
            {
                BodyBaselineLines.Add(Normalise(raw.TrimEnd()));
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

        // Logged at every refresh, not just at startup: the ownership verdict is
        // the one thing that silently disables a settings row, and these numbers
        // are what tells "the body was recognised" apart from "recognition failed
        // and everything in the body is now read-only".
        var (hasBaseline, matched) = BodyDiagnostics;
        AppContext.AppLogger.Info(
            "mpv.conf ownership: {} option(s) hand-owned, {} body line(s) matched"
            + (hasBaseline ? " (baseline found)" : " (NO baseline: body left alone)"),
            userOwned.Count, matched);
    }

    private static void Reset(IReadOnlyList<string> blockLines)
    {
        lock (Gate)
        {
            UserOwnedBy.Clear();
            OurLineSet.Clear();
            BodyBaselineLines.Clear();
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
