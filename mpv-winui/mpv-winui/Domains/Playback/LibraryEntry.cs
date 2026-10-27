using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.Common.View;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace mpv_winui.Domains.Playback
{
    /// <summary>
    /// One card in the library: a real file, from the player's own watch
    /// history.
    ///
    /// The previous version of this type was an empty placeholder - the grid
    /// drew a fixed number of grey blocks per section so the layout could be
    /// seen. That was honest while there was no source behind it and is exactly
    /// what the cards are named after: they looked like content while being
    /// none, and the first thing anyone asked on seeing them was why the music
    /// and video libraries were blank.
    /// </summary>
    public sealed partial class LibraryEntry : INotifyPropertyChanged
    {
        /// <summary>Absolute path, and the identity of the card.</summary>
        public string Path { get; init; } = string.Empty;

        /// <summary>
        /// What to show as the card's caption: the media title mpv recorded, or
        /// the file name when the file carried no title.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// The folder the file lives in.
        ///
        /// Not the duration. The watch history records no duration, and the
        /// only way to get one is to open the file - which for a grid of a
        /// hundred cards means opening a hundred files to draw a caption. The
        /// folder is free, and it is the thing that actually distinguishes two
        /// files with similar names.
        /// </summary>
        public string Subtitle { get; init; } = string.Empty;

        public bool IsVideo { get; init; }

        /// <summary>
        /// The album tag, or empty.
        ///
        /// Read from the shell property store (<c>System.Music.AlbumTitle</c>),
        /// which is the indexed service the thumbnails already come from. The
        /// music library's "Albums" tab groups on this; an empty value is a real
        /// answer and becomes the single "unknown album" group rather than
        /// dropping the file.
        /// </summary>
        public string Album { get; init; } = string.Empty;

        /// <summary>
        /// The artist tag, album artist as the fallback.
        ///
        /// "Albums" groups on <see cref="Album"/> and "Artists" on this. The
        /// fallback matters for compilations: a track whose <c>Artist</c> is
        /// "Various Artists" but whose album artist is one performer belongs
        /// under that performer in an artist list, which is what every reference
        /// player does.
        /// </summary>
        public string Artist { get; init; } = string.Empty;

        /// <summary>
        /// The genre tag, or empty. The album page's track table has a genre
        /// column, as the reference player's does.
        /// </summary>
        public string Genre { get; init; } = string.Empty;

        /// <summary>
        /// Playing time, when the shell reports one.
        ///
        /// Null rather than zero for "unknown": the track table shows the length
        /// column only when there is one, and a 00:00 row is a lie the grid would
        /// otherwise tell about every file the index skipped.
        /// </summary>
        public TimeSpan? Duration { get; init; }

        /// <summary>The duration as the table prints it, or empty.</summary>
        public string DurationText => Duration is { } d
            ? d.TotalHours >= 1
                ? $"{(int)d.TotalHours}:{d.Minutes:00}:{d.Seconds:00}"
                : $"{d.Minutes}:{d.Seconds:00}"
            : string.Empty;

        /// <summary>True when the row has a length to show.</summary>
        public Visibility DurationVisibility =>
            Duration is null ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>
        /// When the watch history last recorded this file being played.
        ///
        /// The video library's "newest / oldest" sort reads this. It comes from
        /// the history file's own <c>time</c> field (unix seconds), which
        /// <see cref="History.WatchHistoryParser"/> already parses; null only
        /// for a line that carried no usable timestamp, which the parser
        /// tolerates. Nothing here reads the file system's timestamps.
        /// </summary>
        public DateTimeOffset? Time { get; init; }

        /// <summary>
        /// The path data for this entry's media-kind icon: a film frame for
        /// video, a quaver pair for audio, a plain list for a file whose
        /// extension this app does not claim to know. A STRING rather than a
        /// Geometry on purpose: a Geometry in WinUI belongs to a single
        /// PathIcon for its lifetime, and a recycled card bound to a second
        /// PathIcon would throw on the second assignment. The template binds
        /// <see cref="MediaIcon"/>, which parses a fresh instance per binding
        /// - on the UI thread, which is the only thread a Geometry may be
        /// created on. See FluentIcons for the full rule.
        /// </summary>
        public string MediaIconPath { get; init; } = FluentIcons.List;

        /// <summary>
        /// The album card's first caption: the album, or the localized "unknown".
        ///
        /// Plain data, filled by <see cref="FromFileWithTags"/>, NOT a computed
        /// property reaching for AppLang. x:Bind compiles against the declared
        /// property, and a property whose body calls into a static global crashes this
        /// XAML compiler with an internal error whose own message fails to load -
        /// which is why this looks like an arbitrary failure. Every other caption the
        /// card binds (Title, Subtitle) is data for the same reason.
        ///
        /// Empty at construction and filled by the builder, so the fallback is decided
        /// where AppContext is reachable rather than inside a data template.
        /// </summary>
        public string AlbumTitle { get; init; } = string.Empty;

        /// <summary>The album card's second caption: the artist, or the localized "unknown".</summary>
        public string AlbumArtistText { get; init; } = string.Empty;

        /// <summary>
        /// A fresh Geometry for the media-kind icon, parsed per access.
        /// x:Bind evaluates this once per template application, which is what
        /// keeps every PathIcon holding a geometry of its own.
        /// </summary>
        public Geometry MediaIcon => FluentIcons.Geometry(MediaIconPath);

        /// <summary>
        /// True when the file could not be found on disk any more. The card
        /// still shows - a history entry is a record of something the user did,
        /// and silently dropping it would make the list disagree with the
        /// player's own history dialog - but it is dimmed, because clicking it
        /// will fail.
        /// </summary>
        public bool IsMissing { get; init; }

        private ImageSource? _thumbnail;

        /// <summary>
        /// The real thumbnail, filled in after the card is already on screen.
        /// Null until then, and null for good when the shell has no thumbnail
        /// provider for the file - in which case the card shows the media-kind
        /// icon instead, which is a designed state and not a failure.
        /// </summary>
        public ImageSource? Thumbnail
        {
            get => _thumbnail;
            private set
            {
                if (ReferenceEquals(_thumbnail, value))
                {
                    return;
                }

                _thumbnail = value;
                Raise();
                Raise(nameof(ThumbnailVisibility));
                Raise(nameof(IconVisibility));
            }
        }

        public Visibility ThumbnailVisibility => _thumbnail is null ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>
        /// The fallback glyph is hidden while a thumbnail is up, and hidden
        /// outright for a missing file so the card cannot look playable.
        /// </summary>
        public Visibility IconVisibility =>
            _thumbnail is null && !IsMissing ? Visibility.Visible : Visibility.Collapsed;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Called from the thumbnail loader once a frame has decoded.</summary>
        internal void SetThumbnail(ImageSource image) => Thumbnail = image;

        private void Raise([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// Announces a property from the selection half.
        ///
        /// The selection file is a separate partial of this class, and `Raise` is
        /// private to this one - a partial class shares its members, so this exists only
        /// to keep the selection file readable rather than to bridge anything.
        /// </summary>
        internal void RaiseSelectionChanged(string name) => Raise(name);

        /// <summary>Called by the page when selection mode turns on or off.</summary>
        internal void SetSelectionMode(bool on) => SelectionMode = on;

        /// <summary>Called by the page when a card is ticked or unticked.</summary>
        internal void SetSelected(bool selected) => IsSelected = selected;

        /// <summary>
        /// Builds a card from a history entry, or null when the entry has no
        /// usable path.
        /// </summary>
        /// <summary>
        /// A file found by scanning a bound folder.
        ///
        /// Same shape as a history entry with no timestamp: the file has not been
        /// played yet, so it has no history time to sort by. The caption is the
        /// file name, because a scan deliberately does not open the files - doing
        /// so for a thousand-track folder would take minutes and is what the watch
        /// history avoids too.
        /// </summary>
        internal static LibraryEntry? FromFile(string path) => FromHistory(path, mediaTitle: null, time: null);

        /// <summary>
        /// One entry with its album, artist and length filled in.
        ///
        /// Separate from <see cref="FromHistory"/> because the tags come from the
        /// shell property store, which is slower per file than reading the path -
        /// and the watch history's entries never need them. The music library
        /// calls this in bulk off the UI thread (see LibraryTagsReader).
        /// </summary>
        internal static LibraryEntry? FromFileWithTags(
            string path,
            string? mediaTitle,
            DateTimeOffset? time,
            MusicTags tags)
        {
            var entry = FromHistory(path, mediaTitle, time);

            if (entry is null)
            {
                return null;
            }

            return new LibraryEntry
            {
                Path = entry.Path,
                Title = entry.Title,
                Subtitle = entry.Subtitle,
                IsVideo = entry.IsVideo,
                Time = entry.Time,
                IsMissing = entry.IsMissing,
                MediaIconPath = entry.MediaIconPath,
                Album = tags.Album,
                Artist = string.IsNullOrWhiteSpace(tags.Artist) ? tags.AlbumArtist : tags.Artist,
                AlbumTitle = string.IsNullOrWhiteSpace(tags.Album)
                    ? global::mpv_winui.AppContext.AppLang.LibraryUnknownAlbum
                    : tags.Album,
                AlbumArtistText = string.IsNullOrWhiteSpace(tags.Artist) && string.IsNullOrWhiteSpace(tags.AlbumArtist)
                    ? global::mpv_winui.AppContext.AppLang.LibraryUnknownArtist
                    : (string.IsNullOrWhiteSpace(tags.Artist) ? tags.AlbumArtist : tags.Artist),
                Genre = tags.Genre,
                Duration = tags.Duration,
            };
        }

        internal static LibraryEntry? FromHistory(string path, string? mediaTitle, DateTimeOffset? time = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string fileName;
            string folder;
            try
            {
                fileName = System.IO.Path.GetFileName(path);
                folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path) ?? string.Empty);
            }
            catch (ArgumentException)
            {
                return null;
            }

            // The history stores an mpv media-title, which for a tagged file is
            // the tag and for an untagged one is often the full path. Showing a
            // path as a caption is worse than showing the file name, so a title
            // that looks like a path is discarded.
            var title = string.IsNullOrWhiteSpace(mediaTitle) || mediaTitle.Contains('\\') || mediaTitle.Contains('/')
                ? System.IO.Path.GetFileNameWithoutExtension(fileName)
                : mediaTitle;

            var isVideo = MediaKinds.IsVideo(path);
            var isAudio = MediaKinds.IsAudio(path);

            return new LibraryEntry
            {
                Path = path,
                Title = string.IsNullOrWhiteSpace(title) ? fileName : title,
                Subtitle = folder,
                IsVideo = isVideo,
                Time = time,
                IsMissing = !File.Exists(path),
                // An extension this app does not recognise still gets a card -
                // it came out of the player's own history, so it is something
                // that really played - but it cannot claim to be video or audio,
                // so it takes the neutral icon rather than a wrong one. Video
                // takes the film frame the official player draws for a
                // poster-less video, not the camera glyph.
                MediaIconPath = isVideo ? FluentIcons.Filmstrip
                    : isAudio ? FluentIcons.Music
                    : FluentIcons.List,

                // Nothing has been read from the file yet, and saying so is better than
                // saying nothing: an album card with two empty caption lines reads as
                // broken rather than as untagged, and these are the same buckets the
                // grouping already files such tracks under. FromFileWithTags replaces
                // both with the real tags when the pass runs.
                AlbumTitle = global::mpv_winui.AppContext.AppLang.LibraryUnknownAlbum,
                AlbumArtistText = global::mpv_winui.AppContext.AppLang.LibraryUnknownArtist,
            };
        }
    }
}
