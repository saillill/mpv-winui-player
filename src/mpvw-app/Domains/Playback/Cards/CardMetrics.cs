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

        /// <summary>
        /// The media tile's corner radius. Replaces CornerRadius="4" x4.
        ///
        /// 6, up from 4: the comparison page's chosen variant pairs this inner
        /// radius with a 6 DIP gutter and a 1.5 DIP frame. The frame's own radius
        /// is derived from this one - see <see cref="HoverFrameCornerRadius"/>,
        /// which is this plus that gutter.
        /// </summary>
        public const double CornerRadius = 6;

        /// <summary>
        /// Gap between the media tile and its first caption line.
        ///
        /// 6, down from 8: the reference's title cap line starts 6 DIP under the
        /// tile edge. At 8 the captions read as a separate block floating below
        /// the picture instead of as its label, which is the same complaint in
        /// the other direction from "the highlight does not overlap the picture".
        /// Must equal the Margin in LibraryCard.xaml - the card draws it, this
        /// adds it into the grid cell, and a mismatch crops the second line.
        /// </summary>
        public const double CaptionTopGap = 6;

        /// <summary>
        /// How far the hover highlight sits inside the cover's edges.
        ///
        /// <para><b>No longer used by the card.</b> The highlight used to be a
        /// wash drawn over the picture, inset from its edges. The report was that
        /// this is the wrong shape entirely: "the black frame is inside the card;
        /// the outermost hover frame should be the outlined one, and what you
        /// framed is the thumbnail". What was wanted is a WHITE rounded rectangle
        /// around the whole card - picture and caption together - which is
        /// <see cref="HoverFrameInset"/> below.</para>
        ///
        /// <para>Kept because CardMetricsTests still pins the value and nothing
        /// else in the app is named after it; delete with the constant if the
        /// inner wash is confirmed gone for good.</para>
        /// </summary>
        public const double HoverInset = 6;

        /// <summary>
        /// The hover frame's ring.
        ///
        /// <para>No longer used - see <see cref="HoverInset"/>. The frame is now
        /// the buttons' white ring colour, not a black one.</para>
        /// </summary>
        public const double HoverRingThickness = 2;

        /// <summary>
        /// The gutter reserved around the card's content, in DIP.
        ///
        /// <para>Reported as "the outermost hover frame should be the outlined
        /// one, with the text aligned to the thumbnail". The frame wraps the whole
        /// card - tile, caption and all - so it has to sit OUTSIDE the content
        /// rather than inset into it.</para>
        ///
        /// <para><b>It is not empty space that happens to fit the stroke: it is
        /// the width of a slot the content and the frame share.</b> 6 DIP is kept
        /// free around the content at all times, hover or not, and the frame's
        /// stroke is drawn inside that band. The content therefore never moves on
        /// hover - the one thing this card must not do - and the stroke has room
        /// to sit clear of the thumbnail's own
        /// <see cref="CornerRadius"/>.</para>
        ///
        /// <para>Why a gutter and not an outward push: the spike measured that the
        /// grid cell clips whatever hangs outside it. A frame pushed out by a
        /// negative margin drew nothing in the band beyond the cell - the same
        /// result with a TranslateTransform instead (the stroke really moved,
        /// x 364 to 597, and the band beyond the cell still changed by zero
        /// pixels). So the gutter is part of the cell and the frame is drawn
        /// inside it: the content keeps the reference's width, the cell carries
        /// this much extra on each side, and the picture never moves on hover.</para>
        /// </summary>
        public const double HoverFrameInset = 6;

        /// <summary>
        /// The hover frame's stroke width.
        ///
        /// <para>1.5, matching <see cref="RingThickness"/>: the frame and the two
        /// corner buttons are the same object family, and a frame heavier than the
        /// discs it contains reads as a selection border rather than a hover
        /// affordance.</para>
        /// </summary>
        public const double HoverFrameThickness = 1.5;

        /// <summary>
        /// The hover frame's corner radius.
        ///
        /// <para><b>Derived, not sampled:</b> <see cref="CornerRadius"/> plus
        /// <see cref="HoverFrameInset"/>, 6 + 6 = 12. The frame's edge runs
        /// <see cref="HoverFrameInset"/> outside the content's, so its arc has to
        /// be that much larger to stay parallel to it; anything else and the two
        /// edges converge at the corners.</para>
        ///
        /// <para>The comparison page offered 11 for this and that is the error
        /// this comment exists to prevent: 11 does not satisfy the relation
        /// against its own 6 DIP push, it was picked by eye. The structure of that
        /// variant was chosen; its arithmetic was not.</para>
        /// </summary>
        public const double HoverFrameCornerRadius = 12;

        /// <summary>
        /// The hover buttons' diameter.
        ///
        /// 30, down from 36, and both buttons are the same size. Chosen on the
        /// comparison page together with the 8 DIP corner inset and the thinner
        /// semi-transparent ring. Whether 30 reads as too small on a real video
        /// frame is one of the decisions no constant can settle - it is in the
        /// design's eye-check list, not its arithmetic.
        /// </summary>
        public const double PlayButtonSize = 30;

        /// <summary>
        /// The hover overflow button's diameter. The same as the play button's,
        /// because the reference draws the two as one matched pair - the previous
        /// 40/32 split made the secondary button look accidental.
        /// </summary>
        public const double MoreButtonSize = 30;

        /// <summary>
        /// How far the corner buttons sit from the tile's edges.
        ///
        /// 8, down from 10, shared by both corners so the two buttons read as one
        /// pair rather than as two elements that happened to land near a corner.
        /// It moves with <see cref="PlayButtonSize"/>: the discs shrank by a fifth
        /// and the inset was brought along rather than left at the old scale.
        /// </summary>
        public const double CornerInset = 8;

        /// <summary>
        /// The hover buttons' white ring, in DIP.
        ///
        /// 1.5, sampled from the reference. Thin enough to read as a hairline
        /// against a bright frame and thick enough to hold against a dark one,
        /// which is what a 1px ring fails to do on a letterboxed still.
        /// </summary>
        public const double RingThickness = 1.5;

        /// <summary>
        /// Height of the now-playing stripe along the cover's bottom edge.
        ///
        /// 2 DIP: the same weight the settings rail uses for a selected row's
        /// indicator, so a "this one" mark looks the same in both surfaces.
        /// </summary>
        public const double NowPlayingBarHeight = 2;

        // ------------------------------------------------------------- cell height

        /// <summary>
        /// One title line's height at MpvCardTitleFontSize (15).
        ///
        /// A constant rather than a TextBlock measurement because the grid asks for
        /// a cell size BEFORE any card exists: UniformGridLayout needs MinItemHeight
        /// on the first layout pass, so there is nothing to measure yet.
        ///
        /// 22 = the 20 a 14px line took, plus the 2 DIP the step to 15 costs at
        /// the default 1.2 line box. Left at 20 the second line of a wrapped title
        /// is clipped by the cell, which reads as a card that lost its text.
        /// </summary>
        public const double TitleLineHeight = 22;

        /// <summary>
        /// One subtitle line's height at MpvCardSubtitleFontSize (14).
        ///
        /// <para><b>No longer part of the cell.</b> The card draws one caption
        /// line now - the file name - so the cell must not reserve room for a
        /// second. Left at 19 and still subtracted below, the difference was 21
        /// DIP of empty band under every card, which reads as inconsistent row
        /// spacing in the grid rather than as a missing line.</para>
        ///
        /// <para>Kept as a constant because the collapsed SubtitleText element
        /// still declares a LineHeight and CardMetricsTests pins the two against
        /// each other; if a second line ever comes back, this is the number that
        /// has to rejoin CaptionBandHeight.</para>
        /// </summary>
        public const double SubtitleLineHeight = 19;

        /// <summary>
        /// Empty space below the caption, in the card.
        ///
        /// <para>Reported as "the title should leave a line and a half of empty
        /// space below it". 1.5 x <see cref="TitleLineHeight"/> = 33.</para>
        ///
        /// <para>It is not padding for its own sake. The hover frame wraps the
        /// card, so whatever the caption does not occupy is what the frame encloses
        /// below the text: at zero the last line sat hard against the card's lower
        /// edge and the frame read as a border around the picture plus an accident
        /// of text.</para>
        /// </summary>
        public const double TrailingCaptionSpace = TitleLineHeight * 1.5;

        /// <summary>
        /// The caption band under the media tile: one gap, one line, and the
        /// reserved space below it.
        ///
        /// Both grids size their cell on this, so it is written once: two grids
        /// that each carried their own copy is how the album cell stayed at a
        /// landscape height after the album tile became square.
        ///
        /// <see cref="TrailingCaptionSpace"/> is included because the cell has to
        /// contain it - leaving it out clips it, and a clipped reserve is
        /// indistinguishable from having none.
        /// </summary>
        public const double CaptionBandHeight =
            CaptionTopGap + TitleLineHeight + TrailingCaptionSpace;

        /// <summary>
        /// The card cell's width: the content plus the gutter on both sides.
        ///
        /// <para>The grid clips at the cell edge, so the frame cannot hang
        /// outside it. The gutter is therefore part of the cell, and the CONTENT
        /// keeps the reference's 253 - the picture must not shrink just because
        /// the frame needed a home.</para>
        ///
        /// <para>This is the number the grid's <c>MinItemWidth</c> takes, so it
        /// has to grow by <see cref="HoverFrameInset"/> twice while the card's own
        /// content margin grows by it once: the margin is inside the cell, and the
        /// two together are what put the stroke one gutter clear of the
        /// picture.</para>
        /// </summary>
        public static double CellWidthFor(double contentWidth) =>
            contentWidth + (HoverFrameInset * 2);

        /// <summary>
        /// The grid cell height for a card of this width.
        ///
        /// Replaces two literals - 170x246 for the album grid and 253x200 for the
        /// video grid - that had to be recomputed by hand whenever an aspect
        /// changed. Two cards, two widths, one arithmetic.
        ///
        /// The gutter is added here for the same reason as in
        /// <see cref="CellWidthFor"/>: it is inside the cell on all four sides, so
        /// the cell carries it above the picture and below the caption as well as
        /// to each side. <paramref name="contentWidth"/> is the CONTENT's width,
        /// not the cell's.
        /// </summary>
        public static double CellHeightFor(double contentWidth, bool isAlbum) =>
            CoverHeightFor(contentWidth, isAlbum) + (HoverFrameInset * 2) + CaptionBandHeight;

        // ----------------------------------------------------------- grid spacing

        /// <summary>
        /// The card grids' column spacing, in DIP.
        ///
        /// <para><b>Not <c>MpvSpaceM</c> (12), and the difference is the point.</b>
        /// The frame lives in a 6 DIP gutter inside the cell, so two adjacent
        /// selected frames are <c>spacing - 2 x HoverFrameInset</c> apart: at 12
        /// they were 2 DIP apart and the two strokes read as one line, which is
        /// exactly what a selection border must not do. At 20 they are 8 apart.</para>
        ///
        /// <para>The token stays at 12 for its other consumers in markup (21 of
        /// them when this was written); the grid gets its own number rather than a
        /// global. The markup's first-paint default cannot reference this class
        /// (it is internal static), so it carries the literal 20 and
        /// CardMetricsTests.EveryCardGridLeavesRoomForTheFrame pins the two
        /// together.</para>
        /// </summary>
        public const double GridColumnSpacing = 20;

        /// <summary>
        /// The card grids' row spacing, in DIP. The same 20 as the columns: a grid
        /// whose rows breathe less than its columns reads as a table with tight
        /// rows rather than as a wall of cards, and the frame's gutter is the same
        /// band vertically.
        /// </summary>
        public const double GridRowSpacing = 20;
    }
}
