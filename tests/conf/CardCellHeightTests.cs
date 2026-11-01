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
/// These assertions deliberately do NOT pin the old literals. 246 was 30 DIP
/// taller than the album card needs (170 media + 46 of captions), and copying it
/// into a test would preserve a number whose only justification was that nobody
/// had recomputed it.
/// </summary>
[TestFixture]
public class CardCellHeightTests
{
    [Test]
    public void SquareCellIsItsMediaPlusTheCaptionBand()
    {
        var h = CardMetrics.CellHeightFor(170, isAlbum: true);

        Assert.That(h, Is.EqualTo(170 + CardMetrics.CaptionBandHeight).Within(0.001));
        Assert.That(h, Is.GreaterThan(170), "the cell must be taller than the media");
    }

    [Test]
    public void LandscapeCellIsShorterThanItsWidth()
    {
        // 253 through 16:9 is 142.3 of media; with the caption band that is under
        // 200, which is why the old literal was closer here than on the album grid.
        var h = CardMetrics.CellHeightFor(253, isAlbum: false);

        Assert.That(h, Is.EqualTo(142.3125 + CardMetrics.CaptionBandHeight).Within(0.01));
        Assert.That(h, Is.LessThan(253), "a 16:9 card is wider than it is tall");
    }

    [Test]
    public void CellGrowsWithWidth()
    {
        Assert.That(CardMetrics.CellHeightFor(340, isAlbum: true),
                    Is.GreaterThan(CardMetrics.CellHeightFor(170, isAlbum: true)));
    }

    [Test]
    public void ZeroWidthHasNoMediaSoTheCellIsJustTheCaptions()
    {
        // Not 0, and not the caption band plus a bogus media height: a zero-width
        // card has no tile, so the cell is exactly the band. The grid never asks
        // this question - it sizes from MinItemWidth - but a recycled element can
        // report 0 width, and the answer has to be finite.
        Assert.That(CardMetrics.CellHeightFor(0, isAlbum: true),
                    Is.EqualTo(CardMetrics.CaptionBandHeight));
    }

    [Test]
    public void NegativeWidthIsTreatedAsNoWidth()
    {
        Assert.That(CardMetrics.CellHeightFor(-20, isAlbum: false),
                    Is.EqualTo(CardMetrics.CaptionBandHeight));
    }

    /// <summary>
    /// The caption band is one line, its gap, and the space reserved below it.
    ///
    /// It was CaptionTopGap + TitleLineHeight + CaptionLineGap + SubtitleLineHeight
    /// while the card drew two lines. The card draws one - the file name - and the
    /// report then asked for "a line and a half of empty space below the title",
    /// so the trailing reserve is part of the band, not padding outside it: the
    /// cell has to contain the space or the grid clips it, and a clipped reserve
    /// is indistinguishable from having none.
    /// </summary>
    [Test]
    public void CaptionBandIsOneLineAGapAndTheTrailingReserve()
    {
        Assert.That(CardMetrics.CaptionBandHeight,
                    Is.EqualTo(CardMetrics.CaptionTopGap
                               + CardMetrics.TitleLineHeight
                               + CardMetrics.TrailingCaptionSpace));

        // One and a half lines, as asked for.
        Assert.That(CardMetrics.TrailingCaptionSpace,
                    Is.EqualTo(CardMetrics.TitleLineHeight * 1.5).Within(1e-9));

        // The reserve is real: without it the band would be 28, and the assertion
        // above would still pass if TrailingCaptionSpace silently became 0 only
        // because it was folded into the gap. Naming the number is what stops that.
        Assert.That(CardMetrics.CaptionBandHeight, Is.EqualTo(61).Within(1e-9));
    }
}
