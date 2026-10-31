namespace mpv_winui.Domains.Playback.Cards
{
    /// <summary>
    /// The library card's geometry, in one place.
    ///
    /// These numbers were literals repeated across two DataTemplates and two
    /// layout methods: CornerRadius="4" four times, Width/Height="48" twice per
    /// template, and a MinItemHeight that had to be recomputed by hand whenever
    /// the aspect changed. A token that lives in two places is not a token, and
    /// the drift it invites is invisible in review - which is exactly what
    /// "the two cards have no common scale" was reporting.
    ///
    /// Deliberately free of WinUI types so the arithmetic is testable: the test
    /// project targets plain net10.0 and cannot reference Microsoft.UI.Xaml.
    /// The values are DIPs, the unit every XAML number in this repository uses.
    /// </summary>
    internal static class CardMetrics
    {
        /// <summary>
        /// Width divided by height for a card's cover.
        ///
        /// A square IS one, so the album card has no converter: the video card
        /// needs 16:9 because its thumbnails are landscape frames. Replaces the
        /// two hand-tuned numbers (170 square, 253 through 16:9) that had to be
        /// kept in agreement by hand.
        /// </summary>
        public static double CoverAspect(bool isAlbum) => isAlbum ? 1.0 : 16.0 / 9.0;

        /// <summary>
        /// A cover's height for a given card width.
        ///
        /// Zero for zero or negative widths rather than NaN or a negative
        /// value: a recycled card reports an ActualWidth of 0 before its first
        /// layout pass, and a non-finite height assigned at that moment throws
        /// inside the layout pass instead of drawing nothing.
        /// </summary>
        public static double CoverHeightFor(double width, bool isAlbum)
        {
            if (width <= 0)
            {
                return 0;
            }

            return width / CoverAspect(isAlbum);
        }

        /// <summary>The media tile's corner radius. Replaces CornerRadius="4" x4.</summary>
        public const double CornerRadius = 4;

        /// <summary>Gap between the media tile and its first caption line.</summary>
        public const double CaptionTopGap = 8;

        /// <summary>Gap between the title line and the subtitle line.</summary>
        public const double CaptionLineGap = 2;

        /// <summary>
        /// The hover play button's diameter.
        ///
        /// 40 rather than the previous 48: the button sits in a corner now, and
        /// at 48 it covered a visible slice of the cover's lower-left corner -
        /// the same complaint that moved it out of the middle.
        /// </summary>
        public const double PlayButtonSize = 40;

        /// <summary>The hover overflow button's diameter. Smaller: it is secondary.</summary>
        public const double MoreButtonSize = 32;

        /// <summary>
        /// How far the corner buttons sit from the tile's edges.
        ///
        /// Shared by both corners so the two buttons read as one pair rather
        /// than as two elements that happened to land near a corner.
        /// </summary>
        public const double CornerInset = 10;

        /// <summary>
        /// Height of the now-playing stripe along the cover's bottom edge.
        ///
        /// 2 DIP: the same weight the settings rail uses for a selected row's
        /// indicator, so a "this one" mark looks the same in both surfaces.
        /// </summary>
        public const double NowPlayingBarHeight = 2;
    }
}
