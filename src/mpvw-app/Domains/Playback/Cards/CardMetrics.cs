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
        /// <para><b>12, equal to <see cref="HoverFrameCornerRadius"/>.</b> The
        /// report is 「卡片缩略图的圆角还要再圆滑一点，和遮罩框一致」 - rounder, and
        /// the same as the frame. It was 6 (frame 12, derived as 6 + the 6 DIP
        /// gutter), so the two arcs were parallel but differed by the gutter: a
        /// tight corner inside a much larger one reads as two unrelated shapes when
        /// the frame appears.</para>
        ///
        /// <para>Making them equal breaks the "outer radius = inner + offset"
        /// relation on purpose. That relation is for a border drawn directly on the
        /// same box; here the frame is a separate element 6 DIP away, and matching
        /// radii is what makes the card read as one rounded object rather than a
        /// picture inside a box. The frame's radius therefore stops being derived
        /// and becomes its own number, which is why the relation's doc comment no
        /// longer claims otherwise.</para>
        /// </summary>
        public const double CornerRadius = 12;

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

        // HoverInset and HoverRingThickness used to sit here: the inset and the
        // ring of the old inner wash, a translucent fill drawn over the picture.
        // Both were dead - their own doc said "no longer used", and the only other
        // mention of either was a test comment recounting what they had been.
        //
        // Deleted once the thing they named was confirmed gone rather than merely
        // unused: HoverOverlay's markup now says in as many words that the wash
        // is gone and what remains there is the two corner buttons. The live
        // constant for the frame's gutter is HoverFrameInset, below.
        //
        // A constant kept "because a test pins it" is a test pinning a number no
        // code honours - the same shape as the dead brush token removed from
        // Styles.xaml in the same pass.

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
        /// <para><b>Equal to <see cref="CornerRadius"/> by request, not derived
        /// from it.</b> It used to be <c>CornerRadius + HoverFrameInset</c> - the
        /// rule for a border drawn on the same box, which keeps an outer arc
        /// concentric with an inner one - and that gave 12 against a 6 DIP tile.
        /// The report asked for the thumbnail's corners to match this one
        /// (「和遮罩框一致」), so the tile came up to 12 and this stayed put. Two
        /// equal radii on concentric boxes are NOT parallel curves; they are the
        /// shape a person asked for, and the arithmetic that would forbid it
        /// describes a different construction.</para>
        /// </summary>
        public const double HoverFrameCornerRadius = 12;

        /// <summary>
        /// The hover buttons' diameter.
        ///
        /// <para><b>36, not 30.</b> 30 was measured against a 253-wide card and
        /// read as too small on a real video frame - which is what the eye-check
        /// list predicted it might ("whether 30 reads as too small on a real video
        /// frame is one of the decisions no constant can settle"). The report is
        /// the answer: 「目前的悬浮按钮又过小了，原基础放大1.2倍左右」, and 30 x 1.2
        /// is 36 - the size it had before this branch shrank it.</para>
        /// </summary>
        public const double PlayButtonSize = 36;

        /// <summary>
        /// The hover overflow button's diameter. The same as the play button's,
        /// because the reference draws the two as one matched pair - the previous
        /// 40/32 split made the secondary button look accidental.
        /// </summary>
        public const double MoreButtonSize = 36;

        /// <summary>
        /// How far the corner buttons sit from the tile's edges.
        ///
        /// <para>10, up from 8 with the discs: the inset belongs to the disc's
        /// scale, and 8 was chosen to keep a 30px disc off the corner. At 36 the
        /// same 8 crowds the arc.</para>
        /// </summary>
        public const double CornerInset = 10;

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
        /// <para><b>Back in the cell.</b> The card drew one caption line for a
        /// while - the file name alone - and the band was reduced to match. The
        /// report asks for the second line back, carrying the length
        /// (「卡片出标题外下一行应该再交代视频、音乐的时间」), so the band reserves room
        /// for it again.</para>
        /// </summary>
        public const double SubtitleLineHeight = 19;

        /// <summary>
        /// Empty space below the caption, in the card.
        ///
        /// <para><b>0.5 x <see cref="TitleLineHeight"/> = 11, down from 1.5 x = 33.</b>
        /// The report is 「卡片标题下的留白过多，可以稍微缩短一点」. The 1.5 was chosen
        /// when the card was 253 wide and the caption was one line; with a second
        /// line back the band is already taller, and 33 on top of it is what made
        /// the gap read as a hole rather than as breathing room.</para>
        ///
        /// <para>Not zero: the hover frame wraps the card, so the caption must not
        /// end hard against the card's lower edge or the frame reads as a border
        /// around the picture plus an accident of text. 11 keeps that gap visible
        /// without leaving a hole.</para>
        /// </summary>
        public const double TrailingCaptionSpace = TitleLineHeight * 0.5;

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
            CaptionTopGap + TitleLineHeight + SubtitleLineHeight + TrailingCaptionSpace;

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
        /// <para><b>4, down from 20.</b> The report is 「卡片与卡片之间间距太宽，应该
        /// 紧密挨着不要留空」. 20 was chosen to keep two SELECTED frames from reading
        /// as one line - <c>spacing - 2 x HoverFrameInset</c> had to stay positive -
        /// but that reasoning optimised for the rare state and paid for it in the
        /// common one. The gutter is already load-bearing: at 4 the two frames are
        /// 4 - 12 = -8, i.e. the gutters OVERLAP, and since a frame is only visible
        /// under the pointer or on a ticked card, two adjacent frames never appear
        /// together. What the eye sees at rest is two pictures 4 DIP apart, which
        /// is what 「紧密挨着」 asks for.</para>
        ///
        /// <para>Kept non-negative rather than negative: a negative spacing in
        /// UniformGridLayout is not a thing, and the gutter still has to fit
        /// inside each cell.</para>
        /// </summary>
        public const double GridColumnSpacing = 4;

        /// <summary>
        /// The card grids' row spacing, in DIP. The same 4 as the columns: the
        /// rows are separated by the caption band's own trailing space in practice
        /// (see <see cref="TrailingCaptionSpace"/>), so a large row gap stacked on
        /// top of that is what made 「卡片标题下的留白过多」.
        /// </summary>
        public const double GridRowSpacing = 4;
    }
}
