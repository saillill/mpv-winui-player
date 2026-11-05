using System;
using System.Collections.Generic;

namespace mpv_winui.Domains.Playback.Cards;

/// <summary>Which kind of genre the user has picked in the toolbar's dropdown.</summary>
internal enum MusicGenreKind
{
    /// <summary>Every file, whatever it is tagged with.</summary>
    All,

    /// <summary>Only files with no genre at all.</summary>
    Unknown,

    /// <summary>Files tagged with one particular genre.</summary>
    Named,
}

/// <summary>
/// One choice in the genre dropdown.
///
/// <para>A record struct rather than a bare string because "all", "unknown" and "a
/// genre literally called Unknown" are three different answers that a nullable string
/// would collapse into two. <see cref="Kind"/> carries the distinction; the page reads
/// it to decide the button's caption as well as the filter.</para>
/// </summary>
internal readonly record struct MusicGenreSelection(MusicGenreKind Kind, string Name = "")
{
    /// <summary>No filtering.</summary>
    public static MusicGenreSelection All { get; } = new(MusicGenreKind.All);

    /// <summary>Only the untagged files.</summary>
    public static MusicGenreSelection Unknown { get; } = new(MusicGenreKind.Unknown);

    /// <summary>One named genre.</summary>
    public static MusicGenreSelection Named(string name) => new(MusicGenreKind.Named, name);
}

/// <summary>
/// Reads genre tags and answers the two questions the music library asks about them.
///
/// <para><b>No UI type and no <c>AppContext</c>.</b> This file is linked by the test
/// project, which is a plain <c>net10.0</c> library and cannot reference
/// <c>Microsoft.UI.Xaml</c> at all - so the rules live here as pure functions, where
/// they are testable without a window. For the same reason nothing here resolves a
/// localized label: an untagged file's album comes back as the empty string, and the
/// page decides what to call it.</para>
///
/// <para><b>Genre tags are free text.</b> Nothing normalizes them at rip time, so the
/// same idea arrives as "Rock", "rock", "Rock; Pop", "Rock, Pop". Every rule below
/// exists because of a spelling that really occurs.</para>
/// </summary>
internal static class MusicGenreFilter
{
    /// <summary>The separators that appear between genres in real tags, both of them.</summary>
    private static readonly char[] Separators = [',', ';'];

    /// <summary>
    /// The genres a tag actually names, in the order they were written.
    ///
    /// <para>De-duplicated case-insensitively with the FIRST spelling kept, so the
    /// dropdown shows what the file says rather than a canonical form nobody typed.
    /// Order is preserved rather than sorted: callers that want an order apply their
    /// own, and <see cref="Vocabulary"/> is where sorting belongs.</para>
    /// </summary>
    internal static IReadOnlyList<string> Split(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var parts = raw!.Split(
            Separators,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kept = new List<string>(parts.Length);

        foreach (var part in parts)
        {
            if (seen.Add(part))
            {
                kept.Add(part);
            }
        }

        return kept;
    }

    /// <summary>Whether this file's tag satisfies the picked genre.</summary>
    internal static bool Matches(string? raw, MusicGenreSelection selection)
    {
        switch (selection.Kind)
        {
            case MusicGenreKind.All:
                return true;

            case MusicGenreKind.Unknown:
                return Split(raw).Count == 0;

            default:
                foreach (var genre in Split(raw))
                {
                    if (string.Equals(genre, selection.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
        }
    }

    /// <summary>
    /// Every genre name the library actually uses, sorted for the dropdown.
    ///
    /// <para>Untagged files contribute nothing: an empty entry would render as a blank
    /// row. They are reached through <see cref="MusicGenreSelection.Unknown"/>
    /// instead.</para>
    /// </summary>
    internal static IReadOnlyList<string> Vocabulary(IEnumerable<(string Album, string Genre)> tracks)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>();

        foreach (var (_, genre) in tracks)
        {
            foreach (var name in Split(genre))
            {
                if (seen.Add(name))
                {
                    names.Add(name);
                }
            }
        }

        names.Sort(StringComparer.CurrentCultureIgnoreCase);

        return names;
    }

    /// <summary>
    /// The album keys that survive the filter, in the order they first appear.
    ///
    /// <para>An album is kept WHOLE: one matching track keeps its siblings, so the
    /// album page does not open showing half an album. The caller uses the returned
    /// set as "keep every track whose album appears here", which is why this returns
    /// album keys rather than tracks.</para>
    /// </summary>
    internal static IReadOnlyList<string> KeptAlbumKeys(
        IEnumerable<(string Album, string Genre)> tracks,
        MusicGenreSelection selection)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keys = new List<string>();

        foreach (var (album, genre) in tracks)
        {
            if (!Matches(genre, selection))
            {
                continue;
            }

            // Whitespace-only and absent are the same album, spelled the same way here and at every lookup: the page normalises with IsNullOrWhiteSpace, so a raw "   " key would match the genre and then fail its own lookup, dropping that album from the Albums tab.
            // Whitespace-only and absent are the same album, spelled the same way here and at every lookup: the page normalises with IsNullOrWhiteSpace, so a raw "   " key would match the genre and then fail its own lookup, dropping that album from the Albums tab.
            var key = string.IsNullOrWhiteSpace(album) ? string.Empty : album;

            if (seen.Add(key))
            {
                keys.Add(key);
            }
        }

        return keys;
    }
}
