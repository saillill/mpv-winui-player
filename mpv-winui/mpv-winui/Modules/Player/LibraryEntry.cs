using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.Common.View;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace mpv_winui.Modules.Player
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
    public sealed class LibraryEntry : INotifyPropertyChanged
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
            };
        }
    }
}
