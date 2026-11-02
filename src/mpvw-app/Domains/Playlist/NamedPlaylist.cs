using System.Collections.Generic;

namespace mpv_winui.Domains.Playlist
{
    /// <summary>
    /// One named playlist: the name it is filed under, and the entries its file
    /// holds.
    ///
    /// <para>Immutable and built only by <see cref="NamedPlaylistStore"/>, so the
    /// name a row shows and the name its file is called cannot drift: both come
    /// from the same read.</para>
    ///
    /// <para>Internal like the store that constructs it and like
    /// <see cref="NamedPlaylistFile"/> beside them. The app is one assembly, so
    /// nothing here needs a public surface; the tests compile these files into
    /// their own assembly and reach internal members without any extra ceremony.</para>
    /// </summary>
    internal sealed class NamedPlaylist
    {
        public NamedPlaylist(string name, IReadOnlyList<string> paths)
        {
            Name = name;
            Paths = paths;
        }

        /// <summary>The file's name without <c>.m3u</c>. This is the list's name.</summary>
        public string Name
        {
            get;
        }

        /// <summary>The entries, in the order the file holds them.</summary>
        public IReadOnlyList<string> Paths
        {
            get;
        }

        /// <summary>
        /// How many entries the list holds.
        ///
        /// <para>Parsed entries, not the file's lines: an <c>.m3u</c> carries a
        /// header and one <c>#EXTINF</c> record per entry, so the line count is
        /// roughly twice the entry count and would overstate every list. A
        /// header-only file is a list of zero entries, not one.</para>
        /// </summary>
        public int Count => Paths.Count;
    }
}
