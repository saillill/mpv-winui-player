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
                    properties.Duration > TimeSpan.Zero ? properties.Duration : null,

                    // MusicProperties reports 0 for "no year in the tag" - it is not a
                    // year, and filing every untagged file under year zero would put
                    // half the library in a 0-year group. null keeps them reachable
                    // through the single "unknown" bucket instead.
                    properties.Year > 0 ? (int)properties.Year : null);
            }
            catch (Exception)
            {
                // Missing file, no property handler for the container, a drive
                // that went away mid-scan: all of them mean "no tags".
                return MusicTags.Empty;
            }
        }

        /// <summary>
        /// Lengths for a set of VIDEO files, keyed case-insensitively.
        ///
        /// <para><b>Why this cannot reuse <see cref="ReadAsync"/>.</b> That method
        /// goes through <c>GetMusicPropertiesAsync</c>, which has no handler for a
        /// video container: it throws, the catch turns it into
        /// <c>MusicTags.Empty</c>, and the card ends up with no length. That is the
        /// whole of 「视频没有显示秒数」 - the caption itself was fine, the number was
        /// never there. Music entries were the only ones ever fed to a reader.</para>
        ///
        /// <para><c>GetBasicPropertiesAsync</c> does answer for video: it is backed by
        /// the shell property store, so it costs an index lookup rather than a decode,
        /// which is the same trade the music pass makes and the reason neither of them
        /// is MediaInfo.</para>
        ///
        /// <para>Never throws, for the reason the class states: an unreadable file
        /// contributes nothing and the card shows no length rather than disappearing.
        /// </para>
        /// </summary>
        internal static Task<IReadOnlyDictionary<string, TimeSpan>> ReadDurationsAsync(
            IReadOnlyList<string> paths,
            CancellationToken token = default) =>
            Task.Run(async () =>
            {
                var result = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);

                foreach (var path in paths)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        var file = await StorageFile.GetFileFromPathAsync(path).AsTask().ConfigureAwait(false);

                        // VideoProperties answers for a video container where the
                        // music properties do not, and both are served from the same
                        // shell store - so this costs an index lookup, not a decode.
                        var video = await file.Properties.GetVideoPropertiesAsync().AsTask().ConfigureAwait(false);

                        if (video.Duration > TimeSpan.Zero)
                        {
                            result[path] = video.Duration;
                        }
                    }
                    catch (Exception)
                    {
                        // Missing file, no property handler, a drive that went away:
                        // all of them mean "no length", which the card renders as no
                        // second line rather than as 0:00.
                    }
                }

                return (IReadOnlyDictionary<string, TimeSpan>)result;
            },
            token);
    }
}
