using System;
using System.Collections.Generic;
using System.Linq;

namespace mpv_winui.Domains.SettingsModel
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
        /// Removes one folder from one library.
        ///
        /// The counterpart to <see cref="AddLibraryFolder"/>, comparing the same way -
        /// OrdinalIgnoreCase - so a folder added from Explorer's path and removed from
        /// the browse dialog's cannot both be present as near-duplicates.
        ///
        /// Returns true when something was actually removed, so a caller can tell
        /// "was not bound" from "removed" without reading the list back, and so the
        /// settings row only announces a change that happened.
        ///
        /// Data only, like every other setter here: the caller announces it through
        /// AppContext, which is how the rest of the settings tree works.
        /// </summary>
        public bool RemoveLibraryFolder(bool music, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var current = music ? GetMusicLibraryFolders() : GetVideoLibraryFolders();

            var remaining = current
                .Where(f => !string.Equals(f, path, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (remaining.Count == current.Count)
            {
                return false;
            }

            Write(music, remaining);

            return true;
        }

        /// <summary>Removes every folder from one library. Returns how many went.</summary>
        public int ClearLibraryFolders(bool music)
        {
            var current = music ? GetMusicLibraryFolders() : GetVideoLibraryFolders();

            if (current.Count == 0)
            {
                return 0;
            }

            Write(music, []);

            return current.Count;
        }

        private void Write(bool music, IEnumerable<string> folders)
        {
            var value = string.Join('\n', folders);

            if (music)
            {
                MusicLibraryFolders = value;
            }
            else
            {
                VideoLibraryFolders = value;
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
