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
        Assert.That(CardMetrics.CaptionTopGap, Is.EqualTo(8));
        Assert.That(CardMetrics.CaptionLineGap, Is.EqualTo(2));
        Assert.That(CardMetrics.PlayButtonSize, Is.EqualTo(40));
        Assert.That(CardMetrics.MoreButtonSize, Is.EqualTo(32));
        Assert.That(CardMetrics.CornerInset, Is.EqualTo(10));
        Assert.That(CardMetrics.NowPlayingBarHeight, Is.EqualTo(2));
    }
}
