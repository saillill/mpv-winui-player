namespace mpv_winui.Modules.Language
{
    /// <summary>
    /// Strings for the shell chrome - the navigation pane and the tab strip -
    /// which did not exist before the window gained a library skeleton.
    ///
    /// Kept in its own partial, the way <c>AppLang.MpvConfEditor.cs</c> keeps the
    /// strings for one feature together, so it is obvious which keys belong to
    /// the shell rather than being scattered through the alphabetical body of
    /// AppLang.cs.
    ///
    /// These are the only strings in the app whose translations are authored for
    /// Chinese and English only. The other seven language files deliberately
    /// carry no entry for them, so those languages fall back to the English
    /// default declared here - which is what a missing key already does at
    /// runtime - rather than shipping machine-translated chrome.
    /// </summary>
    public partial class AppLang
    {
        public string LibraryMusic { get; set; } = "Music";

        public string LibraryVideos { get; set; } = "Videos";

        public string LibraryPlaylists { get; set; } = "Playlists";

        public string LibraryRecent { get; set; } = "Recently played";

        /// <summary>
        /// The Home section's page title. A separate key from
        /// <see cref="ShellNavHome"/> on purpose: the pane's label and the
        /// page's own heading are allowed to read differently, the way
        /// 音乐库 (pane) and 音乐 (page) already do.
        /// </summary>
        public string LibraryHome { get; set; } = "Home";

        /// <summary>The short heading above Home's grid of recent media.</summary>
        public string LibraryHomeRecent { get; set; } = "Recently used media";

        public string LibraryAddFolder { get; set; } = "Add folder";

        /// <summary>The video library's two tabs.</summary>
        public string LibraryTabAllVideos { get; set; } = "All videos";

        public string LibraryTabVideoFolders { get; set; } = "Video folders";

        /// <summary>The video library's toolbar.</summary>
        public string LibraryPlayAll { get; set; } = "Play all";

        public string LibrarySortBy { get; set; } = "Sort by";

        public string LibrarySortNameAsc { get; set; } = "A-Z";

        /// <summary>
        /// The music library's fallback group for a file with no album tag.
        ///
        /// A group, not an error: an untagged file still belongs in the library,
        /// and every reference player shows one "unknown" bucket rather than
        /// hiding those tracks.
        /// </summary>
        public string LibraryUnknownAlbum { get; set; } = "Unknown album";

        /// <summary>See <see cref="LibraryUnknownAlbum"/>.</summary>
        public string LibraryUnknownArtist { get; set; } = "Unknown artist";

        // ---- album page --------------------------------------------------

        /// <summary>The album page's action row, alongside <see cref="LibraryPlayAll"/>.</summary>
        public string LibraryShufflePlay { get; set; } = "Shuffle";

        /// <summary>"Add to" on the album page, same meaning as the queue's.</summary>
        public string LibraryAlbumAddTo { get; set; } = "Add to";

        /// <summary>Back from an album to the library list.</summary>
        public string LibraryAlbumBack { get; set; } = "Back to music";

        /// <summary>
        /// "{0} songs, {1}" - the count and total length under the album title.
        /// The separator is a middle dot in Latin languages, which is what the
        /// reference player uses; a translator can drop it for their language.
        /// </summary>
        public string LibraryAlbumSummary { get; set; } = "{0} songs \u00b7 {1}";

        /// <summary>The track table's column headings.</summary>
        public string LibraryColumnTitle { get; set; } = "Title";

        /// <summary>See <see cref="LibraryColumnTitle"/>.</summary>
        public string LibraryColumnArtist { get; set; } = "Artist";

        /// <summary>See <see cref="LibraryColumnTitle"/>.</summary>
        public string LibraryColumnGenre { get; set; } = "Genre";

        /// <summary>See <see cref="LibraryColumnTitle"/>.</summary>
        public string LibraryColumnLength { get; set; } = "Length";

        /// <summary>Shown when an album's files cannot be listed any more.</summary>
        public string LibraryAlbumEmpty { get; set; } = "These files are no longer in the library.";

        public string LibrarySortNameDesc { get; set; } = "Z-A";

        public string LibrarySortNewest { get; set; } = "Newest first";

        public string LibrarySortOldest { get; set; } = "Oldest first";

        /// <summary>
        /// The card hover overlay and its overflow menu. "More" is the overflow
        /// button's accessible name; the rest are menu items.
        /// </summary>
        public string LibraryCardPlay { get; set; } = "Play";

        public string LibraryCardMore { get; set; } = "More";

        public string LibraryCardAddToQueue { get; set; } = "Add to play queue";

        public string LibraryCardOpenLocation { get; set; } = "Open file location";

        public string LibraryCardCopyPath { get; set; } = "Copy file path";

        public string LibraryCardCopyTitle { get; set; } = "Copy title";

        public string LibraryEmptyHomeTitle { get; set; } = "No recent media";

        public string LibraryEmptyHomeDescription { get; set; } = "Media you play shows up here.";

        public string LibraryEmptyMusicTitle { get; set; } = "Can't find any music";

        public string LibraryEmptyMusicDescription { get; set; } = "Your music library doesn't contain any music.";

        public string LibraryEmptyVideosTitle { get; set; } = "Can't find any videos";

        public string LibraryEmptyVideosDescription { get; set; } = "Your video library doesn't contain any videos.";

        public string LibraryEmptyPlaylistsTitle { get; set; } = "You don't have any playlists";

        public string LibraryEmptyPlaylistsDescription { get; set; } = "Playlists show up here.";

        public string ShellNavHome { get; set; } = "Home";

        public string ShellNavMusic { get; set; } = "Music library";

        public string ShellNavVideos { get; set; } = "Video library";

        /// <summary>
        /// The play queue section, between the libraries and the playlists.
        /// Doubles as the queue page's own heading, since the pane's label and
        /// the page's title read the same for this one.
        /// </summary>
        public string ShellNavQueue { get; set; } = "Play queue";

        public string ShellNavPlaylists { get; set; } = "Playlists";

        public string LibraryQueueClear { get; set; } = "Clear";

        public string LibraryQueueAddTo { get; set; } = "Add to";

        public string LibraryEmptyTitle { get; set; } = "Nothing is playing";

        public string LibraryEmptyDescription { get; set; } = "Open a file to start playback. What you play shows up here.";

        /// <summary>
        /// Shown when a search matched nothing. Distinct from
        /// <see cref="LibraryEmptyTitle"/>, because being told to go and play
        /// something when the list is empty only because of the filter is the
        /// wrong instruction.
        /// </summary>
        public string LibraryNoResults { get; set; } = "No matching media";

        public string LibraryNoResultsHint { get; set; } = "Try a different word, or clear the search to see everything.";

        public string LibraryOpenFile { get; set; } = "Open file";

        public string ShellSearchPlaceholder { get; set; } = "Search";

        /// <summary>
        /// The shell's back-to-home button while the video is on screen. A
        /// house, because the action takes you to the library.
        /// </summary>
        public string ShellBackToLibrary { get; set; } = "Back to home";

        /// <summary>
        /// The same button once the library is covering a playing file. The
        /// button points both ways, so it needs a word for each direction.
        /// </summary>
        public string ShellBackToPlaying { get; set; } = "Back to playing";

        public string TabCloseTooltip { get; set; } = "Close tab";
    }
}
