using mpv_winui.Modules.Player;

namespace mpv_conf_test;

/// <summary>
/// The control bar's width and orientation tiers.
///
/// <para>Two defects live here, and neither is reachable from a checker that
/// reads source text, because both are properties of a table rather than of a
/// statement.</para>
///
/// <para>The first is orientation. <c>TierFor</c> took only a width and claimed
/// a portrait window "is narrow by definition", which is false at the size
/// people actually use: a 384x744 window is above <c>NarrowWidth</c>, so it
/// landed in <c>Narrow</c> and the portrait rules never ran.</para>
///
/// <para>The second is the overflow button. It was shown when
/// <c>tier != Wide || userHidden.Count &gt; 0</c>, which is not the question the
/// menu answers: a tier below Wide can still keep every control it retains on
/// the bar, and a control the user hid on purpose is skipped by the menu. Either
/// way the button appeared and opened an empty menu.</para>
/// </summary>
[TestFixture]
public class ControlBarAdaptiveLayoutTests
{
    private static IReadOnlySet<string> NothingHidden =>
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlySet<string> Hidden(params string[] ids) =>
        new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);

    // ===== orientation =====

    [Test]
    public void PortraitIsMinimalEvenWhenWideEnoughForNarrow()
    {
        Assert.That(
            ControlBarAdaptiveLayout.TierFor(384, 744),
            Is.EqualTo(ControlBarTier.Minimal));
    }

    [Test]
    public void WidthAloneCannotDetectPortrait()
    {
        // Documents why the height argument exists: this is what the app used
        // to compute for a 384-wide portrait window.
        Assert.That(
            ControlBarAdaptiveLayout.TierFor(384),
            Is.EqualTo(ControlBarTier.Narrow));
    }

    [Test]
    public void LandscapeWindowIsNotPortrait()
    {
        Assert.That(ControlBarAdaptiveLayout.IsPortrait(744, 384), Is.False);
        Assert.That(
            ControlBarAdaptiveLayout.TierFor(744, 384),
            Is.EqualTo(ControlBarTier.Standard));
    }

    [Test]
    public void UnmeasuredWindowIsNeitherPortraitNorMinimal()
    {
        Assert.That(ControlBarAdaptiveLayout.IsPortrait(0, 0), Is.False);
        Assert.That(ControlBarAdaptiveLayout.IsPortrait(double.NaN, 800), Is.False);
        Assert.That(
            ControlBarAdaptiveLayout.TierFor(double.NaN, 800),
            Is.EqualTo(ControlBarTier.Wide));
    }

    // ===== the portrait bar keeps 3 / 3 / 3 =====

    [TestCase("play")]
    [TestCase("previous")]
    [TestCase("next")]
    [TestCase("volume")]
    [TestCase("tracks")]
    [TestCase("panel")]
    [TestCase("aspect")]
    [TestCase("pip")]
    [TestCase("fullscreen")]
    public void PortraitKeepsTheNineControls(string id)
    {
        Assert.That(
            ControlBarAdaptiveLayout.IsVisible(id, ControlBarTier.Minimal, NothingHidden),
            Is.True,
            $"{id} must still be on a portrait bar");
    }

    [TestCase("skip-back")]
    [TestCase("skip-forward")]
    [TestCase("random")]
    [TestCase("rate")]
    [TestCase("fullwindow")]
    [TestCase("volume-slider")]
    public void PortraitDropsOnlyTheSecondaryControls(string id)
    {
        Assert.That(
            ControlBarAdaptiveLayout.IsVisible(id, ControlBarTier.Minimal, NothingHidden),
            Is.False,
            $"{id} is a secondary control and should give way in portrait");
    }

    [Test]
    public void PortraitMiddleIsPreviousPlayNext()
    {
        // The real middle cluster, in its real order.
        string[] middle = ["previous", "skip-back", "play", "skip-forward", "next"];

        var visible = middle
            .Where(id => ControlBarAdaptiveLayout.IsVisible(id, ControlBarTier.Minimal, NothingHidden))
            .ToArray();

        Assert.That(visible, Is.EqualTo(new[] { "previous", "play", "next" }));
    }

    [Test]
    public void PortraitKeepsExactlyThreeControlsOnEachSide()
    {
        string[] leftZone = ["volume", "tracks", "random", "panel"];
        string[] rightZone = ["aspect", "pip", "fullwindow", "fullscreen"];

        Assert.That(
            leftZone.Count(id => ControlBarAdaptiveLayout.IsVisible(id, ControlBarTier.Minimal, NothingHidden)),
            Is.EqualTo(3),
            "left zone");
        Assert.That(
            rightZone.Count(id => ControlBarAdaptiveLayout.IsVisible(id, ControlBarTier.Minimal, NothingHidden)),
            Is.EqualTo(3),
            "right zone");
    }

    [Test]
    public void TightPitchFitsTheNarrowestPortraitBar()
    {
        // Nine controls plus the overflow button, at the pitch the narrow tiers
        // switch to. If this stops holding, the portrait bar clips again.
        const double tightPitch = 33;
        const int slots = 10;

        Assert.That(
            slots * tightPitch,
            Is.LessThanOrEqualTo(ControlBarAdaptiveLayout.NarrowWidth));
    }

    [Test]
    public void TightPitchAppliesToTheNarrowTiersOnly()
    {
        Assert.That(ControlBarAdaptiveLayout.IsTight(ControlBarTier.Wide), Is.False);
        Assert.That(ControlBarAdaptiveLayout.IsTight(ControlBarTier.Compact), Is.False);
        Assert.That(ControlBarAdaptiveLayout.IsTight(ControlBarTier.Narrow), Is.True);
        Assert.That(ControlBarAdaptiveLayout.IsTight(ControlBarTier.Minimal), Is.True);
    }

    // ===== the overflow button =====

    [Test]
    public void WideBarWithEverythingVisibleHasNothingToOverflow()
    {
        Assert.That(
            ControlBarAdaptiveLayout.HasOverflowContent(ControlBarTier.Wide, NothingHidden),
            Is.False);
    }

    [TestCase("pip")]
    [TestCase("tracks")]
    [TestCase("panel")]
    [TestCase("fullscreen")]
    [TestCase("aspect")]
    [TestCase("random")]
    [TestCase("fullwindow")]
    [TestCase("volume")]
    public void AUserHiddenIconAloneDoesNotCreateOverflowContent(string id)
    {
        // The empty-menu bug: the menu skips exactly the ids the user hid, so a
        // user-hidden icon must never be the reason the button appears.
        Assert.That(
            ControlBarAdaptiveLayout.HasOverflowContent(ControlBarTier.Wide, Hidden(id)),
            Is.False,
            $"{id} hidden by the user gives the menu nothing to offer");
    }

    [Test]
    public void TierHiddenControlDoesCreateOverflowContent()
    {
        // The reverse of the test above: the same predicate must still go true
        // when the tier, rather than the user, took something off the bar.
        Assert.That(
            ControlBarAdaptiveLayout.HasOverflowContent(ControlBarTier.Standard, NothingHidden),
            Is.True);
        Assert.That(
            ControlBarAdaptiveLayout.HasOverflowContent(ControlBarTier.Minimal, NothingHidden),
            Is.True);
    }

    [Test]
    public void PortraitAlwaysOffersTheSecondaryTransport()
    {
        // Even with every movable icon hidden the portrait bar keeps a working
        // menu, because the transport entries ignore the hide preference.
        var allHidden = Hidden("volume", "tracks", "random", "panel",
                               "aspect", "pip", "fullwindow", "fullscreen");

        Assert.That(
            ControlBarAdaptiveLayout.HasOverflowContent(ControlBarTier.Minimal, allHidden),
            Is.True);
    }

    // ===== the table's own invariants =====

    [Test]
    public void EverySuppressibleEntryCanActuallyBeHiddenByTheTier()
    {
        // An entry that honours the user-hide preference is only reachable
        // through the tier. If the tier never hides it, the menu can never
        // offer it and the entry is dead weight -- which is how aspect, PiP,
        // tracks, panel and full screen came to be removed from the table.
        foreach (var (id, respectsUserHide) in ControlBarAdaptiveLayout.OverflowEntries)
        {
            if (!respectsUserHide)
            {
                continue;
            }

            Assert.That(
                ControlBarAdaptiveLayout.HiddenFromTier.ContainsKey(id),
                Is.True,
                $"{id} respects the user hide and is never hidden by the tier, "
                + "so nothing can ever put it in the menu");
        }
    }

    [Test]
    public void EveryEntryIsRenderedByTheUiTable()
    {
        // The menu builder maps ids to elements; an id with no mapping is
        // silently skipped, which would make the button visible with nothing
        // behind it. The ids here are the contract that mapping has to meet.
        var expected = new[]
        {
            "skip-back", "skip-forward", "random", "rate",
            "previous", "next", "fullwindow",
        };

        Assert.That(
            ControlBarAdaptiveLayout.OverflowEntries.Select(e => e.Id).ToArray(),
            Is.EqualTo(expected));
    }

    [Test]
    public void HiddenFromTierOnlyHidesThingsThatComeBack()
    {
        // Every tier value in the table has to be a tier the bar can actually
        // be in, and the widest tier must hide nothing, or "Wide" stops being
        // the "everything fits" state the overflow test depends on.
        foreach (var (id, from) in ControlBarAdaptiveLayout.HiddenFromTier)
        {
            Assert.That(from, Is.Not.EqualTo(ControlBarTier.Wide), id);
            Assert.That(
                ControlBarAdaptiveLayout.IsVisible(id, ControlBarTier.Wide, NothingHidden),
                Is.True,
                $"{id} must be visible at Wide");
        }
    }
}
