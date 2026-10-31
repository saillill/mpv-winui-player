using System;

namespace mpv_winui.Domains.Playback.Cards
{
    /// <summary>
    /// How many tracks an album holds, and how to say so.
    ///
    /// Two facts this type exists to keep apart:
    ///
    ///   * "the tags have not been read yet" is UNKNOWN, and unknown has no
    ///     caption at all.
    ///   * an album with no tracks cannot happen here - a grouping is built from
    ///     the files that are in it - but a zero would still be a lie if it
    ///     reached the UI, so it reads as unknown rather than as "0 tracks".
    ///
    /// A separate type rather than a property on LibraryEntry because the test
    /// project targets plain net10.0 and LibraryEntry uses Microsoft.UI.Xaml;
    /// linking it there fails with CS0234, measured. The rule needs a home the
    /// tests can reach.
    ///
    /// The wording is passed in: this class may not reach for AppContext, which
    /// is the same WinUI-reaching singleton.
    /// </summary>
    internal static class AlbumTrackCount
    {
        /// <summary>A group's size as a card can use it: null when nothing is known.</summary>
        public static int? For(int count) => count > 0 ? count : null;

        /// <summary>
        /// The caption fragment for a count, or an empty string.
        ///
        /// Empty rather than a placeholder: the subtitle line already carries the
        /// artist, and appending "· " with nothing after it would be worse than
        /// saying nothing.
        /// </summary>
        public static string LabelFor(int? count, Func<int, string>? format)
        {
            if (count is not { } known || known <= 0 || format is null)
            {
                return string.Empty;
            }

            return format(known);
        }
    }
}
