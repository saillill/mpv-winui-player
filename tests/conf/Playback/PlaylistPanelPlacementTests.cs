using NUnit.Framework;
using mpv_winui.Domains.Playback;

namespace mpv_conf_test.Playback;

/// <summary>
/// The playlist panel's placement arithmetic.
///
/// Why this is extracted and tested: the panel is a Popup with
/// ShouldConstrainToRootBounds="False", so nothing but this code pulls it back
/// onto the display. The x axis was clamped and the y axis was not, and the
/// panel is 640 tall anchored on the tool row -- on a 1366x768 display its
/// bottom edge landed 112 DIPs below the work area, with the lower half of the
/// list invisible and unreachable. A model of the formula catches that;
/// nothing in the build did.
/// </summary>
[TestFixture]
public class PlaylistPanelPlacementTests
{
    private const double PanelHeight = 640;

    /// <summary>
    /// The panel's top must be chosen so its bottom stays inside the work area,
    /// and so its top does not leave through the top either. When the panel is
    /// taller than the display there is no correct answer; the rule is to pin
    /// it to the top, which keeps the header (and its close button) reachable.
    /// </summary>
    [TestCase(1040, 200, "a 1080p display has room")]
    [TestCase(728, 200, "a 768p display does not")]
    [TestCase(680, 200, "neither does 720p")]
    public void PanelBottomStaysInsideTheWorkArea(double workHeight, double anchorY, string because)
    {
        var y = PlaylistPanelPlacement.ClampY(anchorY, PanelHeight, 0, workHeight);

        Assert.That(y + PanelHeight, Is.LessThanOrEqualTo(workHeight + 0.001),
            "the panel's bottom edge leaves the work area: " + because);
        Assert.That(y, Is.GreaterThanOrEqualTo(-0.001),
            "the panel's top edge leaves the work area at the top: " + because);
    }

    [Test]
    public void AShortDisplayPinsThePanelToTheTopRatherThanClippingItsBottom()
    {
        // 720 tall, 640 panel: 80 DIPs of slack. Anchoring at 200 would overflow
        // by 120, so the anchor has to be ignored and the panel moved up to 80.
        var y = PlaylistPanelPlacement.ClampY(anchorY: 200, height: 640, top: 0, bottom: 720);

        Assert.That(y, Is.EqualTo(80).Within(0.001),
            "the panel should sit as low as it can while still fitting: 720 - 640");
    }

    [Test]
    public void ATallDisplayKeepsThePanelUnderItsAnchor()
    {
        var y = PlaylistPanelPlacement.ClampY(anchorY: 200, height: 640, top: 0, bottom: 1040);

        Assert.That(y, Is.EqualTo(200).Within(0.001),
            "there is room, so the anchor is honoured and nothing moves");
    }

    [Test]
    public void APanelTallerThanTheDisplayPinsItsTopEdge()
    {
        // No placement fits. Dropping the top edge is wrong: the header carries
        // the close button, so the panel would be unclosable by pointer.
        var y = PlaylistPanelPlacement.ClampY(anchorY: 100, height: 900, top: 0, bottom: 720);

        Assert.That(y, Is.EqualTo(0).Within(0.001),
            "an oversized panel keeps its top edge on screen so the close button stays reachable");
    }

    [Test]
    public void AWindowOnANegativeOriginDisplayStillClampsToTheWorkArea()
    {
        // A second monitor placed to the left of the primary has a negative
        // work-area origin, which is the case the x clamp already handled.
        var y = PlaylistPanelPlacement.ClampY(anchorY: -900, height: 640, top: -1080, bottom: -40);

        Assert.That(y, Is.GreaterThanOrEqualTo(-1080.001));
        Assert.That(y + 640, Is.LessThanOrEqualTo(-40 + 0.001));
        Assert.That(y, Is.EqualTo(-900).Within(0.001),
            "the anchor already fits (-900 + 640 = -260, above the -40 bottom), so nothing moves");
    }

    [Test]
    public void ANegativeOriginDisplayPullsThePanelUpWhenTheAnchorOverflows()
    {
        // The same negative-origin display, with the panel anchored so its
        // bottom would leave the work area: -40 - 640 = -680 is the lowest
        // placement that fits.
        var y = PlaylistPanelPlacement.ClampY(anchorY: -300, height: 640, top: -1080, bottom: -40);

        Assert.That(y, Is.EqualTo(-680).Within(0.001),
            "the panel sits at the bottom of that display's work area");
    }
}
