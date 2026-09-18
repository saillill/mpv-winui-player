using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace mpv_winui.Modules.Settings;

/// <summary>
/// Reads the deployed mpv.conf and reports which mpv options it sets
/// explicitly, so the settings window can mark the ones it cannot win.
///
/// <para>Why this exists: mpv reads its config before accepting IPC commands,
/// so any option named in mpv.conf beats the same option set at runtime by the
/// settings window. Without this the window would happily show a value that is
/// not in effect - the exact confusion the precedence model is meant to
/// remove (see docs/mpv-conf-precedence.md).</para>
///
/// <para>This is a deliberately shallow parser: it handles the subset of the
/// config syntax that can shadow a setting - top-level <c>name = value</c> and
/// <c>name=value</c> lines - and ignores profile sections, since a profile only
/// applies when something activates it. It never writes; a parse failure just
/// means "nothing is known to be overridden", which is the safe reading.</para>
/// </summary>
public static class MpvConfOverrides
{
    /// <summary>A top-level assignment: optional indent, name, '=', optional quoted value, optional trailing comment.</summary>
    private static readonly Regex Assignment = new(
        @"^\s*([A-Za-z][A-Za-z0-9_-]*)\s*=\s*(?<value>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> CachedOptions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();
    private static bool _loaded;

    /// <summary>The mpv options the config file currently sets. Empty when it cannot be read.</summary>
    public static IReadOnlyCollection<string> Names
    {
        get
        {
            lock (Gate)
            {
                if (!_loaded)
                {
                    Refresh();
                }
                return CachedOptions;
            }
        }
    }

    /// <summary>
    /// Re-reads the file. Called at startup and after the settings window
    /// writes the managed block, so a stale badge cannot survive a change.
    /// </summary>
    public static void Refresh()
    {
        lock (Gate)
        {
            CachedOptions.Clear();
            _loaded = true;

            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "mpv-winui", "mpv", "mpv.conf");

            string[] lines;
            try
            {
                if (!File.Exists(path))
                {
                    return;
                }
                lines = File.ReadAllLines(path);
            }
            catch (Exception ex)
            {
                AppContext.AppLogger.Warn(ex, "mpv.conf unreadable, no overrides will be marked");
                return;
            }

            // Inside the managed block our own values live, and inside the
            // user-overrides block the user's do. Both shadow a runtime set, so
            // both count - but a commented-out line does not.
            var inProfile = false;
            foreach (var raw in lines)
            {
                var line = StripComment(raw);
                if (line.Length == 0)
                {
                    continue;
                }

                // A profile header ("[name]") opens a section that only applies
                // when activated; stop treating lines as unconditional there.
                if (line.TrimStart().StartsWith('['))
                {
                    inProfile = true;
                    continue;
                }

                if (inProfile)
                {
                    continue;
                }

                var match = Assignment.Match(line);
                if (match.Success)
                {
                    CachedOptions.Add(match.Groups[1].Value);
                }
            }
        }
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
        return hash >= 0 ? raw[..hash] : raw;
    }
}
