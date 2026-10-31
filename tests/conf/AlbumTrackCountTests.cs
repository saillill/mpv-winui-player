using mpv_winui.Domains.Playback.Cards;

namespace mpv_winui_test;

/// <summary>
/// How many tracks an album has, as a value rather than a number.
///
/// The card has to tell "unknown" apart from "none": a library whose tags have
/// not been read yet is not a library of empty albums, and 0 would draw a caption
/// that says so. This is a separate type because LibraryEntry cannot be linked
/// into this test project - it uses Microsoft.UI.Xaml and this assembly targets
/// plain net10.0, which was measured by trying it.
/// </summary>
[TestFixture]
public class AlbumTrackCountTests
{
    [Test]
    public void UnknownIsNullNotZero()
    {
        Assert.That(AlbumTrackCount.For(0), Is.Null,
            "0 would claim the album has no tracks, which is a different fact from unknown");
    }

    [Test]
    public void OneTrackIsOne()
    {
        // The case that would silently disappear behind a truthiness check.
        Assert.That(AlbumTrackCount.For(1), Is.EqualTo(1));
    }

    [Test]
    public void ManyTracksKeepTheirCount()
    {
        Assert.That(AlbumTrackCount.For(13), Is.EqualTo(13));
    }

    [Test]
    public void NegativeReadsAsUnknown()
    {
        // Cannot happen from a group count, and must not reach the UI if it does.
        Assert.That(AlbumTrackCount.For(-1), Is.Null);
    }

    [Test]
    public void NoLabelWhenTheCountIsUnknown()
    {
        // The caption must not render "· 0 首" or a bare separator with nothing
        // after it.
        Assert.That(AlbumTrackCount.LabelFor(null, n => n + " tracks"), Is.Empty);
    }

    [Test]
    public void NoLabelWhenThereIsNoFormatter()
    {
        Assert.That(AlbumTrackCount.LabelFor(7, null), Is.Empty);
    }

    [Test]
    public void LabelUsesTheSuppliedFormatter()
    {
        // The localised wording comes from AppLang at the call site: this class
        // may not reach for AppContext, which is a WinUI-reaching singleton.
        Assert.That(AlbumTrackCount.LabelFor(7, n => n + " tracks"), Is.EqualTo("7 tracks"));
    }
}
