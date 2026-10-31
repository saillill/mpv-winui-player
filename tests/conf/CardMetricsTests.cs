using mpv_winui.Domains.Playback.Cards;

namespace mpv_winui_test;

/// <summary>
/// The card geometry, in one place.
///
/// These numbers used to be literals repeated in two DataTemplates and in two
/// layout methods: CornerRadius="4" four times, Width/Height="48" twice per
/// template, and a MinItemHeight that had to be recomputed by hand whenever the
/// aspect changed. The tests below pin the arithmetic that ties a card's width
/// to its cover height, because getting it wrong is not a style question - it
/// either crops the picture or opens a gap under it.
/// </summary>
[TestFixture]
public class CardMetricsTests
{
    [Test]
    public void AlbumCoverIsSquare()
    {
        Assert.That(CardMetrics.CoverAspect(isAlbum: true), Is.EqualTo(1.0));
    }

    [Test]
    public void VideoCoverIsSixteenByNine()
    {
        Assert.That(CardMetrics.CoverAspect(isAlbum: false), Is.EqualTo(16.0 / 9.0).Within(1e-9));
    }

    [Test]
    public void AlbumCoverHeightEqualsItsWidth()
    {
        Assert.That(CardMetrics.CoverHeightFor(170, isAlbum: true), Is.EqualTo(170).Within(1e-9));
    }

    [Test]
    public void VideoCoverHeightIsWidthOverAspect()
    {
        // 253 through 16:9 is 142.31: the height the markup comment claims.
        Assert.That(CardMetrics.CoverHeightFor(253, isAlbum: false), Is.EqualTo(142.3125).Within(1e-3));
    }

    [Test]
    public void ZeroWidthStaysZero()
    {
        // A recycled card reports 0 before its first layout pass. A NaN or a
        // negative height assigned here throws inside the layout pass instead of
        // drawing nothing.
        Assert.That(CardMetrics.CoverHeightFor(0, isAlbum: true), Is.EqualTo(0));
    }

    [Test]
    public void NegativeWidthStaysZero()
    {
        Assert.That(CardMetrics.CoverHeightFor(-5, isAlbum: false), Is.EqualTo(0));
    }

    [Test]
    public void TokensAreTheDocumentedValues()
    {
        Assert.That(CardMetrics.CornerRadius, Is.EqualTo(4));

        // The caption band follows the cell height, so a change here without a
        // change in CardMetrics.CaptionBandHeight clips the second line. These
        // four numbers are the reference's own, measured on its hovered card.
        Assert.That(CardMetrics.CaptionTopGap, Is.EqualTo(6));
        Assert.That(CardMetrics.CaptionLineGap, Is.EqualTo(2));

        // Both hover buttons are the same size now: the reference draws them as
        // one matched pair, and the old 40/32 split made the second look accidental.
        Assert.That(CardMetrics.PlayButtonSize, Is.EqualTo(36));
        Assert.That(CardMetrics.MoreButtonSize, Is.EqualTo(36));
        Assert.That(CardMetrics.CornerInset, Is.EqualTo(10));
        Assert.That(CardMetrics.RingThickness, Is.EqualTo(1.5));
        Assert.That(CardMetrics.NowPlayingBarHeight, Is.EqualTo(2));
    }

    /// <summary>
    /// The ring has to fit inside the disc's corner radius or it renders as a
    /// square with a circle inside it. Half the diameter is the only radius that
    /// makes the two shapes concentric.
    /// </summary>
    [Test]
    public void RingSitsOnTheDiscEdge()
    {
        Assert.That(CardMetrics.PlayButtonSize, Is.EqualTo(CardMetrics.MoreButtonSize));
        Assert.That(CardMetrics.RingThickness, Is.LessThan(CardMetrics.PlayButtonSize / 4));
    }

    /// <summary>
    /// The two caption line heights have to clear the type sizes they stand for.
    ///
    /// 15 and 14 at the default 1.2 line box are 18 and 16.8. These constants are
    /// what the grid cell is built from before any card is measured, so being
    /// merely equal is not enough - a cell exactly as tall as the glyph box clips
    /// the descenders of a wrapped line.
    /// </summary>
    [Test]
    public void CaptionLineHeightsClearTheirTypeSizes()
    {
        const double TitleFontSize = 15;
        const double SubtitleFontSize = 14;
        const double LineBox = 1.2;

        Assert.That(CardMetrics.TitleLineHeight, Is.GreaterThan(TitleFontSize * LineBox));
        Assert.That(CardMetrics.SubtitleLineHeight, Is.GreaterThan(SubtitleFontSize * LineBox));
    }
}
