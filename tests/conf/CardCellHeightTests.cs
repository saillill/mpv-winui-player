using mpv_winui.Domains.Playback.Cards;

namespace mpv_winui_test;

/// <summary>
/// The grid cell a card needs: its media height plus its two caption lines.
///
/// Both grids used to carry this as a literal MinItemHeight - 170x246 for the
/// square album grid and 253x200 for the landscape video grid - that had to be
/// recomputed by hand whenever an aspect changed. The comment on AlbumCardLayout
/// said so. Deriving the cell means the cell and the card cannot disagree, which
/// is what "the two cards have no common scale" was about: one arithmetic, two
/// widths.
///
/// <para><b>The cell is the content plus the gutter now.</b> The hover frame
/// cannot be drawn outside the cell (measured, see the plan's Task 0), so the
/// frame lives in a permanent gutter INSIDE it and the cell carries that gutter
/// on all four sides. The width passed here is the CONTENT's - 170 or 253, the
/// reference's picture sizes - and <c>CellHeightFor</c> adds the gutter itself.
/// These assertions therefore state the relation against the content, and the
/// extra 12 is asserted separately so a change that dropped the gutter while
/// keeping the relation could not pass.</para>
///
/// These assertions deliberately do NOT pin the old literals. 246 was 30 DIP
/// taller than the album card needs (170 media + 46 of captions), and copying it
/// into a test would preserve a number whose only justification was that nobody
/// had recomputed it.
/// </summary>
[TestFixture]
public class CardCellHeightTests
{
    [Test]
    public void SquareCellIsItsMediaPlusTheCaptionBandAndTheGutter()
    {
        var h = CardMetrics.CellHeightFor(170, isAlbum: true);

        Assert.That(
            h,
            Is.EqualTo(170 + CardMetrics.CaptionBandHeight + Gutter).Within(0.001));
        Assert.That(h, Is.GreaterThan(170), "the cell must be taller than the media");

        // The gutter is what the frame is drawn in, so it is not optional padding.
        Assert.That(Gutter, Is.EqualTo(CardMetrics.HoverFrameInset * 2));
    }

    [Test]
    public void LandscapeCellIsShorterThanItsWidth()
    {
        // 253 through 16:9 is 142.3 of media; with the caption band and the gutter
        // that is under 253, which is what "a 16:9 card is wider than it is tall"
        // means for the CELL - and at 217.3 it is still comfortably under.
        var h = CardMetrics.CellHeightFor(253, isAlbum: false);

        Assert.That(
            h,
            Is.EqualTo(142.3125 + CardMetrics.CaptionBandHeight + Gutter).Within(0.01));
        Assert.That(h, Is.LessThan(253), "a 16:9 card is wider than it is tall");
    }

    [Test]
    public void CellGrowsWithWidth()
    {
        Assert.That(CardMetrics.CellHeightFor(340, isAlbum: true),
                    Is.GreaterThan(CardMetrics.CellHeightFor(170, isAlbum: true)));
    }

    [Test]
    public void ZeroWidthHasNoMediaSoTheCellIsJustTheCaptionsAndTheGutter()
    {
        // Not 0, and not the caption band plus a bogus media height: a zero-width
        // card has no tile, so the cell is exactly the band plus the two gutter
        // bands. The grid never asks this question - it sizes from MinItemWidth -
        // but a recycled element can report 0 width, and the answer has to be
        // finite.
        Assert.That(
            CardMetrics.CellHeightFor(0, isAlbum: true),
            Is.EqualTo(CardMetrics.CaptionBandHeight + Gutter));
    }

    [Test]
    public void NegativeWidthIsTreatedAsNoWidth()
    {
        Assert.That(
            CardMetrics.CellHeightFor(-20, isAlbum: false),
            Is.EqualTo(CardMetrics.CaptionBandHeight + Gutter));
    }

    /// <summary>
    /// The gutter band, top and bottom: <c>HoverFrameInset</c> twice.
    ///
    /// Written from the constant rather than as 12 so the four tests above stay
    /// relational: if the gutter itself changes, they follow, and
    /// <c>TokensAreTheDocumentedValues</c> is the one place that pins its value.
    /// </summary>
    private const double Gutter = CardMetrics.HoverFrameInset * 2;

    [Test]
    public void CellWidthIsTheContentPlusTheGutter()
    {
        // The width half of the same relation, and the reason a 202 picture still
        // gets a 202 content inside a 214 cell. (The numbers were 253/265 and
        // 170/182 before the card was scaled to 0.8.)
        Assert.That(CardMetrics.CellWidthFor(202), Is.EqualTo(214).Within(1e-9));
        Assert.That(CardMetrics.CellWidthFor(136), Is.EqualTo(148).Within(1e-9));

        // The content keeps its own width: the cell carries the gutter, so the
        // picture is not squeezed by the frame's home.
        Assert.That(
            CardMetrics.CellWidthFor(202) - (CardMetrics.HoverFrameInset * 2),
            Is.EqualTo(202).Within(1e-9));
    }

    /// <summary>
    /// The caption band is the gap, TWO lines, and the space reserved below them.
    ///
    /// <para><b>It is two lines again.</b> The band was one line while the card
    /// showed only the file name, and the second line's reserved 19 DIP was what
    /// made every row look loose. The report asks for the length on that line
    /// (「卡片出标题外下一行应该再交代视频、音乐的时间」), so the band contains it.</para>
    ///
    /// <para>The trailing reserve is part of the band, not padding outside it: the
    /// cell has to contain the space or the grid clips it, and a clipped reserve is
    /// indistinguishable from having none. It is half a line now rather than one
    /// and a half - 「卡片标题下的留白过多，可以稍微缩短一点」.</para>
    /// </summary>
    [Test]
    public void CaptionBandIsTwoLinesAGapAndTheTrailingReserve()
    {
        Assert.That(CardMetrics.CaptionBandHeight,
                    Is.EqualTo(CardMetrics.CaptionTopGap
                               + CardMetrics.TitleLineHeight
                               + CardMetrics.SubtitleLineHeight
                               + CardMetrics.TrailingCaptionSpace));

        // Half a line, as asked for.
        Assert.That(CardMetrics.TrailingCaptionSpace,
                    Is.EqualTo(CardMetrics.TitleLineHeight * 0.5).Within(1e-9));

        // The reserve is real: without it the band would be 47, and the assertion
        // above would still pass if TrailingCaptionSpace silently became 0 only
        // because it was folded into the gap. Naming the number is what stops that.
        Assert.That(CardMetrics.CaptionBandHeight, Is.EqualTo(58).Within(1e-9));
    }
}
