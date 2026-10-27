namespace mpv_winui.Domains.Playback
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Windows.Storage;
    using Windows.Storage.FileProperties;

    /// <summary>
    /// Album / artist / length for the music library, in bulk.
    ///
    /// The grouped tabs ("Albums", "Artists") need one tag per card, so reading
    /// them from the player would mean opening files; <see cref="StorageFile"/>'s
    /// music properties go through the Windows index instead, which is the same
    /// service the grid's thumbnails already use (ThumbnailMode.MusicView).
    ///
    /// Deliberately not MediaInfo: that opens and parses each file, which is
    /// right for the one track that is playing (the native GetMediaTags path) and
    /// wrong for four hundred cards.
    ///
    /// Never throws: a file whose tags cannot be read contributes an empty entry,
    /// which the grouping turns into the single "unknown" group rather than
    /// dropping the card.
    /// </summary>
    internal static class LibraryTagsReader
    {
        /// <summary>
        /// Tags for each path, keyed case-insensitively. Runs on the thread pool;
        /// callers await it and then fill entries on the UI thread.
        /// </summary>
        internal static Task<IReadOnlyDictionary<string, MusicTags>> ReadAsync(
            IReadOnlyList<string> paths,
            CancellationToken token = default) =>
            Task.Run(async () =>
            {
                var result = new Dictionary<string, MusicTags>(StringComparer.OrdinalIgnoreCase);

                foreach (var path in paths)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    result[path] = await ReadOneAsync(path).ConfigureAwait(false);
                }

                return (IReadOnlyDictionary<string, MusicTags>)result;
            },
            token);

        private static async Task<MusicTags> ReadOneAsync(string path)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(path).AsTask().ConfigureAwait(false);

                var properties = await file.Properties.GetMusicPropertiesAsync().AsTask().ConfigureAwait(false);

                return new MusicTags(
                    properties.Album ?? string.Empty,
                    properties.Artist ?? string.Empty,
                    properties.AlbumArtist ?? string.Empty,
                    properties.Genre is { Count: > 0 } genres
                        ? string.Join(", ", genres)
                        : string.Empty,

                    // MusicProperties reports zero for "I do not know", which is
                    // not the same as a zero-length track; both become null so the
                    // table hides the column instead of printing 0:00.
                    properties.Duration > TimeSpan.Zero ? properties.Duration : null);
            }
            catch (Exception)
            {
                // Missing file, no property handler for the container, a drive
                // that went away mid-scan: all of them mean "no tags".
                return MusicTags.Empty;
            }
        }
    }
}
