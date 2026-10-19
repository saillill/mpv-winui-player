namespace mpv_winui.Modules.Player
{
    /// <summary>
    /// What the shell property store says about one music file.
    ///
    /// A value type with plain strings rather than the LibraryEntry itself: the
    /// reader returns a bag of tags keyed by path, and the entry is assembled on
    /// the UI thread from whichever fields a file actually carries.
    ///
    /// Genre is here because the album page's track table has a genre column, the
    /// way the reference player's does. A file with no genre shows an empty cell
    /// rather than a placeholder.
    /// </summary>
    internal readonly record struct MusicTags(
        string Album,
        string Artist,
        string AlbumArtist,
        string Genre,
        System.TimeSpan? Duration)
    {
        public static MusicTags Empty { get; } = new(
            string.Empty, string.Empty, string.Empty, string.Empty, null);
    }
}
