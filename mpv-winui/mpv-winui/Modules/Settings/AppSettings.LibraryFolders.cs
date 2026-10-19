using System;
using System.Collections.Generic;
using System.Linq;

namespace mpv_winui.Modules.Settings
{
    /// <summary>
    /// The folders the two libraries are built from.
    ///
    /// Note that this did not exist before: the libraries were built purely from
    /// mpv's watch history, so "a file is in your library" meant "you have played
    /// it at least once". There was no way to say "these are my media folders" -
    /// the Add folder button only opened a folder as a playlist and forgot it.
    ///
    /// Stored as two lists rather than one with a kind column: a folder is added
    /// from one library's toolbar and belongs to that library, and a folder can
    /// legitimately appear in both.
    /// </summary>
    public partial class AppSettings
    {
        /// <summary>
        /// Folders the video library scans. Stored as a newline-separated string
        /// because the settings store round-trips primitives - a List{T} is not
        /// one of them, and a JSON blob inside a value would be a second format to
        /// keep correct for no gain.
        /// </summary>
        public string VideoLibraryFolders
        {
            get => _dataSetting.GetValue(nameof(VideoLibraryFolders), string.Empty);
            set => _dataSetting.SetValue(nameof(VideoLibraryFolders), value);
        }

        /// <summary>Folders the music library scans. See <see cref="VideoLibraryFolders"/>.</summary>
        public string MusicLibraryFolders
        {
            get => _dataSetting.GetValue(nameof(MusicLibraryFolders), string.Empty);
            set => _dataSetting.SetValue(nameof(MusicLibraryFolders), value);
        }

        /// <summary>The video folders, split and cleaned.</summary>
        public IReadOnlyList<string> GetVideoLibraryFolders() => Split(VideoLibraryFolders);

        /// <summary>The music folders, split and cleaned.</summary>
        public IReadOnlyList<string> GetMusicLibraryFolders() => Split(MusicLibraryFolders);

        /// <summary>
        /// Adds a folder to one library, ignoring a duplicate.
        ///
        /// Compare with OrdinalIgnoreCase: Windows paths are case-insensitive, so
        /// picking the same folder twice with different casing must not add it
        /// twice.
        /// </summary>
        public void AddLibraryFolder(bool music, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var current = music ? GetMusicLibraryFolders() : GetVideoLibraryFolders();

            if (current.Any(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var updated = string.Join('\n', current.Append(path));

            if (music)
            {
                MusicLibraryFolders = updated;
            }
            else
            {
                VideoLibraryFolders = updated;
            }
        }

        /// <summary>
        /// True when this library has at least one folder.
        ///
        /// The toolbars are gated on this: with no folder there is nothing to
        /// play all of, nothing to sort and nothing to group, so the official
        /// player shows only the empty state.
        /// </summary>
        public bool HasLibraryFolders(bool music) =>
            (music ? GetMusicLibraryFolders() : GetVideoLibraryFolders())
                .Any(f => !string.IsNullOrWhiteSpace(f) && System.IO.Directory.Exists(f));

        private static IReadOnlyList<string> Split(string? raw) =>
            string.IsNullOrWhiteSpace(raw)
                ? []
                : raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
