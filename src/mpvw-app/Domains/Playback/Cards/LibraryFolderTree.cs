using System;
using System.Collections.Generic;
using System.Linq;

namespace mpv_winui.Domains.Playback.Cards;

/// <summary>
/// One directory in the video library, as the folder grid shows it.
/// </summary>
/// <param name="Path">The full directory path, which is also its identity.</param>
/// <param name="Name">The leaf name, for the caption.</param>
/// <param name="Count">How many videos are DIRECTLY in it.</param>
internal readonly record struct LibraryFolder(string Path, string Name, int Count);

/// <summary>
/// Turns a flat list of video paths into the folders a person can walk through.
///
/// <para><b>Why this is not just a GroupBy.</b> The scan recurses, so a path already
/// tells the whole story: <c>D:\Media\Trips\2024\a.mp4</c> is a file two levels below
/// the library root. The video tab used to group by the file's immediate directory and
/// show every such directory as a heading, which meant a folder holding only
/// sub-folders never appeared at all - there was no file at that level to group - and
/// there was no way to walk INTO one.</para>
///
/// <para><b>The rules.</b> A folder appears if it is under the current directory and
/// either holds a video directly or leads to one. Only the NEXT level appears: the grid
/// shows what is one step down, not the whole tree, so a library of a thousand nested
/// folders still opens on a readable list.</para>
///
/// <para>Pure, and free of WinUI: the test project links this file and cannot reference
/// <c>Microsoft.UI.Xaml</c>, and the tree shape is exactly the kind of thing worth
/// testing without a window.</para>
/// </summary>
internal static class LibraryFolderTree
{
    /// <summary>
    /// The folders directly under <paramref name="directory"/>, ordered by name.
    /// </summary>
    /// <param name="videoPaths">Every video the library knows about.</param>
    /// <param name="directory">
    /// The directory to list. Empty means "every root the paths sit under", which is how
    /// the tab opens: a library bound to one folder lists that folder, and a library
    /// bound to several lists all of them.
    /// </param>
    internal static IReadOnlyList<LibraryFolder> Children(
        IEnumerable<string> videoPaths,
        string directory)
    {
        // Directories one level below, with the number of videos that live IN each. A
        // folder whose videos are all deeper still counts as present - see the second
        // pass, which adds the folders that only lead somewhere.
        var direct = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // An empty directory means "the top of the library", and the top of the library
        // is the common root the videos sit under. Resolved ONCE, here: doing it per file
        // made each file contribute its own ancestry, so the top level came out as
        // D: plus D:\Media plus D:\Media\Trips - three levels pretending to be one.
        var listing = videoPaths.ToList();

        if (string.IsNullOrEmpty(directory))
        {
            // The top level is the set of directories the library is BOUND to, not the
            // common ancestor of what is in it. A library is a list of folders - a person
            // can bind D:\Media and C:\Clips at once - and their common ancestor is the
            // drive, which is neither part of the library nor a useful thing to open.
            return Roots(listing)
                .Select(root => new LibraryFolder(root, LeafOf(root), VideosIn(listing, root).Count))
                .OrderBy(folder => folder.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        foreach (var path in listing)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var parent = ParentOf(path);

            if (parent is null || !IsUnder(parent, directory) || IsSame(parent, directory))
            {
                continue;
            }

            // The immediate child of `directory` that this file sits under.
            var child = ImmediateChildOf(parent, directory);

            if (child is null)
            {
                continue;
            }

            var own = IsSame(parent, child) || IsSame(parent, directory);

            direct.TryGetValue(child, out var count);

            // Only a file whose OWN directory is this child counts toward its number.
            // A file two levels down makes the folder present, not populated.
            if (own)
            {
                count++;
            }

            direct[child] = count;
        }

        return direct
            .Select(pair => new LibraryFolder(pair.Key, LeafOf(pair.Key), pair.Value))
            .OrderBy(folder => folder.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The videos directly inside <paramref name="directory"/>, ordered by name.
    ///
    /// <para>A folder's own videos only: the ones below it belong to that folder, and a
    /// list that mixed the two would make the count on the card disagree with the grid
    /// it opens.</para>
    /// </summary>
    internal static IReadOnlyList<string> VideosIn(IEnumerable<string> videoPaths, string directory)
    {
        return videoPaths
            .Where(path => !string.IsNullOrWhiteSpace(path)
                && IsSame(ParentOf(path) ?? string.Empty, directory))
            .OrderBy(path => System.IO.Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The breadcrumb for a directory: every level from the library root down to it.
    ///
    /// <para>The last entry is the directory itself, so a caller can render the trail
    /// and the current name from one list rather than appending the leaf separately.</para>
    /// </summary>
    internal static IReadOnlyList<LibraryFolder> Trail(
        IEnumerable<string> videoPaths,
        string directory,
        string root = "")
    {
        var trail = new List<LibraryFolder>();

        if (string.IsNullOrWhiteSpace(directory))
        {
            return trail;
        }

        // No videos, no library: the trail describes a place INSIDE the library, and
        // with nothing loaded there is no library to be inside. Without this the walk
        // below produced the drive and the folder as a two-step trail through nothing.
        if (!videoPaths.Any(path => !string.IsNullOrWhiteSpace(path)))
        {
            return trail;
        }

        // Walk up to the topmost directory any video lives under, then back down. Built
        // from the PATH rather than from the video list so a level with no videos of its
        // own still appears - it is on the way, which is reason enough to show it.
        var levels = new List<string>();
        var current = Normalise(directory);

        while (!string.IsNullOrEmpty(current))
        {
            levels.Add(current);

            var parent = ParentOf(current);

            if (parent is null || IsSame(parent, current))
            {
                break;
            }

            current = parent;
        }

        levels.Reverse();

        // Trim to the library root: the levels above it are not the library, and a trail
        // that started at the drive would be mostly places the user never bound. The root
        // is the BOUND folder - one of Roots() - chosen as the one that contains this
        // directory, not the common ancestor of everything (see Roots for why).
        // The root is the caller's when it has one. It cannot be recovered from
        // `videoPaths` reliably: the caller passes the paths of the folder being shown,
        // and those all sit INSIDE that folder - so every candidate root is the folder
        // itself and the trail collapses to one step. The page knows which folder the
        // library is bound to; asking it is the only honest way to know.
        if (string.IsNullOrEmpty(root))
        {
            root = Roots(videoPaths).OrderBy(candidate => candidate.Length).FirstOrDefault()
                ?? RootOf(videoPaths);
        }

        var started = string.IsNullOrEmpty(root);

        foreach (var level in levels)
        {
            if (!started)
            {
                if (!IsSame(level, root))
                {
                    continue;
                }

                started = true;
            }

            trail.Add(new LibraryFolder(level, LeafOf(level), 0));
        }

        return trail;
    }

    /// <summary>
    /// The topmost directory that still contains at least one video, for a caller that
    /// needs to know where the library begins - the shell back button stops here.
    /// </summary>
    internal static string RootFor(IEnumerable<string> videoPaths) => RootOf(videoPaths);

    /// <summary>
    /// The folders the library is bound to, for a caller that needs a real root.
    ///
    /// <para>Public within the assembly because <see cref="Trail"/> cannot discover it:
    /// it is given the paths of the folder on screen, and every one of them sits inside
    /// that folder.</para>
    /// </summary>
    internal static IReadOnlyList<string> RootsFor(IEnumerable<string> videoPaths) => Roots(videoPaths);

    /// <summary>
    /// The directories the library is bound to, as far as the paths reveal them.
    ///
    /// <para>Recovered from the paths themselves, because the tree is given a list of
    /// files and nothing else. A path's root is the shortest ancestor that is still a real
    /// directory holding videos - which is the bound folder, since a scan starts there and
    /// walks down.</para>
    ///
    /// <para>Two folders are separate roots when neither contains the other. That is what
    /// keeps a library bound to two unrelated places from collapsing into the drive.</para>
    /// </summary>
    private static IReadOnlyList<string> Roots(IEnumerable<string> videoPaths)
    {
        var roots = new List<string>();

        foreach (var path in videoPaths)
        {
            var parent = ParentOf(path);

            if (parent is null)
            {
                continue;
            }

            // Fold this path into an existing root when it sits under one, and drop any
            // root that turns out to sit under it.
            var absorbed = false;

            for (var i = roots.Count - 1; i >= 0; i--)
            {
                if (IsUnder(parent, roots[i]))
                {
                    absorbed = true;

                    break;
                }

                if (IsUnder(roots[i], parent))
                {
                    roots.RemoveAt(i);
                }
            }

            if (!absorbed)
            {
                roots.Add(parent);
            }
        }

        return roots;
    }

    /// <summary>The topmost directory that still contains at least one video.</summary>
    private static string RootOf(IEnumerable<string> videoPaths)
    {
        string? commonest = null;

        foreach (var path in videoPaths)
        {
            var parent = ParentOf(path);

            if (parent is null)
            {
                continue;
            }

            if (commonest is null)
            {
                commonest = parent;

                continue;
            }

            // Keep climbing while the two disagree, so the answer is the deepest
            // directory that contains them all.
            while (!IsUnder(parent, commonest) && !IsSame(parent, commonest))
            {
                var up = ParentOf(commonest);

                if (up is null || IsSame(up, commonest))
                {
                    break;
                }

                commonest = up;
            }
        }

        return commonest ?? string.Empty;
    }

    /// <summary>The immediate child of <paramref name="ancestor"/> that <paramref name="path"/> sits under.</summary>
    private static string? ImmediateChildOf(string path, string ancestor)
    {
        if (string.IsNullOrEmpty(ancestor))
        {
            // No current directory means the caller has not entered anything yet, so the
            // top level is the library ROOT - the common directory the videos sit under -
            // and not the drive. Listing the drive put "C:" on screen as a folder to open,
            // which is not part of the library and walks out of it.
            var root = RootOf([path]);

            return string.IsNullOrEmpty(root) ? null : root;
        }

        var trimmedAncestor = ancestor.TrimEnd(
            System.IO.Path.DirectorySeparatorChar,
            System.IO.Path.AltDirectorySeparatorChar);

        if (path.Length <= trimmedAncestor.Length
            || !path.StartsWith(trimmedAncestor, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rest = path[trimmedAncestor.Length..].TrimStart(
            System.IO.Path.DirectorySeparatorChar,
            System.IO.Path.AltDirectorySeparatorChar);

        if (rest.Length == 0)
        {
            return null;
        }

        var cut = rest.IndexOfAny(
            [System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar]);

        var leaf = cut < 0 ? rest : rest[..cut];

        return trimmedAncestor + System.IO.Path.DirectorySeparatorChar + leaf;
    }

    /// <summary>Whether <paramref name="path"/> is at or below <paramref name="ancestor"/>.</summary>
    private static bool IsUnder(string path, string ancestor)
    {
        if (string.IsNullOrEmpty(ancestor))
        {
            return true;
        }

        var trimmed = ancestor.TrimEnd(
            System.IO.Path.DirectorySeparatorChar,
            System.IO.Path.AltDirectorySeparatorChar);

        return IsSame(path, trimmed)
            || path.StartsWith(
                trimmed + System.IO.Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Case-insensitive path equality, trailing separators ignored.</summary>
    private static bool IsSame(string a, string b)
    {
        return string.Equals(Normalise(a), Normalise(b), StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalise(string path)
    {
        return string.IsNullOrEmpty(path)
            ? string.Empty
            : path.TrimEnd(
                System.IO.Path.DirectorySeparatorChar,
                System.IO.Path.AltDirectorySeparatorChar);
    }

    /// <summary>The leaf name of a directory, or the path itself when it has none.</summary>
    private static string LeafOf(string directory)
    {
        var trimmed = Normalise(directory);

        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var leaf = System.IO.Path.GetFileName(trimmed);

        return string.IsNullOrEmpty(leaf) ? trimmed : leaf;
    }

    /// <summary>The containing directory, or null when the path has none.</summary>
    private static string? ParentOf(string path)
    {
        try
        {
            var parent = System.IO.Path.GetDirectoryName(path);

            return string.IsNullOrEmpty(parent) ? null : parent;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
