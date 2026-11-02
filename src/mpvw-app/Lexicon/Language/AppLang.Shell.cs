namespace mpv_winui.Lexicon.Language
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

        /// <summary>
        /// The track-count fragment on an album card's subtitle: {0} tracks.
        ///
        /// A format string rather than a whole sentence: the card appends it to
        /// the artist, and a sentence would have to know about the separator.
        /// </summary>
        public string LibraryTrackCountFormat { get; set; } = "{0} tracks";

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

        // ---- library folders ----------------------------------------------

        /// <summary>The video library's list of bound folders.</summary>
        public string SettingsVideoLibraryFolders { get; set; } = "Video library folders";

        /// <summary>The music library's list of bound folders.</summary>
        public string SettingsMusicLibraryFolders { get; set; } = "Music library folders";

        /// <summary>What unticking a video folder does, and where they come from.</summary>
        public string SettingsHelpVideoLibraryFolders { get; set; } =
            "Folders the video library scans. Add one with the add-folder button on the "
            + "library page; untick it here to remove it.";

        /// <summary>What unticking a music folder does, and where they come from.</summary>
        public string SettingsHelpMusicLibraryFolders { get; set; } =
            "Folders the music library scans. Add one with the add-folder button on the "
            + "library page; untick it here to remove it.";

        /// <summary>Shown when a library has no folder bound at all.</summary>
        public string SettingsLibraryFoldersNone { get; set; } =
            "No folder is bound. Use the add-folder button on the library page.";

        // ---- selection ----------------------------------------------------

        /// <summary>The card menu's entry into selection mode.</summary>
        public string LibraryCardSelect { get; set; } = "Select";

        /// <summary>"{0} selected" - the count on the selection bar.</summary>
        public string LibrarySelectionCount { get; set; } = "{0} selected";

        /// <summary>Clears the ticks and leaves selection mode.</summary>
        public string LibrarySelectionClear { get; set; } = "Clear";

        /// <summary>Plays the ticked files.</summary>
        public string LibrarySelectionPlay { get; set; } = "Play";

        /// <summary>Appends the ticked files to the play queue.</summary>
        public string LibrarySelectionQueue { get; set; } = "Add to play queue";

        /// <summary>Shows the one ticked file in Explorer.</summary>
        public string LibrarySelectionProperties { get; set; } = "Show in folder";

        /// <summary>
        /// The bar's queue action: brings the play queue on screen.
        ///
        /// Not <see cref="ShellNavQueue"/>: that one is the navigation pane's label and
        /// the queue page's heading ("Play queue"), while the bar says the short word
        /// the reference uses, because five actions have to fit on one line.
        /// </summary>
        public string LibrarySelectionShowQueue { get; set; } = "Queue";

        /// <summary>
        /// The last item of the selection bar's 添加到 menu. The dialog it opens arrives
        /// with Task 13 and the store behind it with Task 12; the key is here now because
        /// the menu item is, and because a menu item with no words is not a menu item.
        /// </summary>
        public string PlaylistCreate { get; set; } = "New playlist…";

        // PlaylistPlay already exists: AppLang.cs has it for the queue's own row
        // menu, and a named list's rows are the same verb. Adding a second one
        // here was a duplicate declaration, not a second string.

        /// <summary>The row menu's verb that asks for a new name.</summary>
        public string PlaylistRename { get; set; } = "Rename";

        /// <summary>The row menu's verb that removes the file, after asking.</summary>
        public string PlaylistDelete { get; set; } = "Delete";

        /// <summary>"{0}" is the list's name.</summary>
        public string PlaylistDeleteConfirm { get; set; } = "Delete \"{0}\"? Its entries go with it, and there is no undo.";

        /// <summary>The row's overflow button.</summary>
        public string PlaylistMore { get; set; } = "More";

        /// <summary>The naming field's hint.</summary>
        public string PlaylistNamePlaceholder { get; set; } = "Name";

        /// <summary>Shown while the typed name is one a list already uses.</summary>
        public string PlaylistNameTaken { get; set; } = "A list with that name already exists.";

        /// <summary>"{0}" is the number of entries.</summary>
        public string PlaylistItemCount { get; set; } = "{0} items";

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
