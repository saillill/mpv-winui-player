using System;
using System.Collections.Generic;
using System.IO;

namespace mpv_winui.Infrastructure.Common.Utils
{
    /// <summary>
    /// Which kind of media a path is, by extension.
    ///
    /// The lists were private to the settings page, where they exist to group
    /// the file-association checklist. The library needs the same answer for
    /// the same files, so they live here now and the settings page reads them
    /// from here: two lists that have to agree about what "video" means is one
    /// list too many, and the failure mode is silent - a format added to one
    /// and not the other simply appears under the wrong heading.
    ///
    /// Extension only, deliberately. Probing the container to find out whether
    /// there is a video stream would be more accurate and would also mean
    /// opening every file in a library of thousands to answer a question the
    /// user asked by looking at a grid.
    /// </summary>
    public static class MediaKinds
    {
        /// <summary>Extensions offered for file association, video first.</summary>
        public static readonly string[] VideoExtensions =
        [
            ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v",
            ".mpg", ".mpeg", ".ts", ".m2ts", ".3gp", ".ogv", ".rm", ".rmvb",
        ];

        public static readonly string[] AudioExtensions =
        [
            ".mp3", ".flac", ".wav", ".aac", ".m4a", ".ogg", ".opus", ".wma",
        ];

        private static readonly HashSet<string> Video = new(VideoExtensions, StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> Audio = new(AudioExtensions, StringComparer.OrdinalIgnoreCase);

        public static bool IsVideo(string? path) => InSet(Video, path);

        public static bool IsAudio(string? path) => InSet(Audio, path);

        /// <summary>
        /// True for anything the library should show at all. A history entry
        /// whose file has since been renamed to something this app does not
        /// open is still worth showing, so callers use this only to pick an
        /// icon, never to drop a row.
        /// </summary>
        public static bool IsKnownMedia(string? path) => IsVideo(path) || IsAudio(path);

        private static bool InSet(HashSet<string> set, string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            string extension;
            try
            {
                extension = Path.GetExtension(path);
            }
            catch (ArgumentException)
            {
                // A malformed path from a hand-edited history file must not take
                // the whole library down.
                return false;
            }

            return !string.IsNullOrEmpty(extension) && set.Contains(extension);
        }
    }
}
