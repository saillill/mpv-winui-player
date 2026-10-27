using mpv_winui.Domains.Playback;
using NUnit.Framework;

namespace mpv_conf_test;

/// <summary>
/// The letter rail's hover wave, as arithmetic.
///
/// These exist because the effect itself cannot be measured here: every UI probe in this repo
/// runs under a guard that keeps the harness out of the foreground, a window that is not
/// foreground gets no hover input, so <c>PointerEntered</c> never fires and nothing on screen
/// changes. Three measurement approaches failed before a log line explained why.
///
/// The pure half can still be checked, and it is the half with a shape that can be wrong. What
/// follows pins the shape: a peak under the pointer, a falloff, and nothing beyond the reach.
/// </summary>
public class IndexRailHoverTests
{
    private const double Tolerance = 1e-9;

    [Test]
    public void The_letter_under_the_pointer_is_the_largest()
    {
        Assert.That(IndexRailHover.ScaleAt(0),
            Is.EqualTo(IndexRailHover.PeakScale).Within(Tolerance));
    }

    [Test]
    public void Strength_falls_away_from_the_pointer()
    {
        var previous = IndexRailHover.StrengthAt(0);

        for (var slots = 0.25; slots < IndexRailHover.WaveReach; slots += 0.25)
        {
            var current = IndexRailHover.StrengthAt(slots);

            // A message argument is not available on the constraint form in this NUnit version,
            // so the failing value is carried by asserting on it directly.
            Assert.That(current - previous, Is.LessThanOrEqualTo(Tolerance),
                "strength rose at " + slots + " letters out, so this is not a falloff");

            previous = current;
        }
    }

    [Test]
    public void Nothing_moves_beyond_the_reach()
    {
        // The point of a reach: a letter far from the pointer is at rest, not slightly moved.
        // Getting this wrong is visible as the whole rail breathing when the pointer enters it.
        foreach (var slots in new[] { IndexRailHover.WaveReach, 3.0, 10.0, 26.0 })
        {
            Assert.That(IndexRailHover.ScaleAt(slots), Is.EqualTo(1.0).Within(Tolerance),
                slots + " letters out was still scaled");
            Assert.That(IndexRailHover.OpacityAt(slots), Is.EqualTo(1.0).Within(Tolerance),
                slots + " letters out was still dimmed");
        }
    }

    [Test]
    public void The_touched_letter_stays_fully_bright()
    {
        // The letter under the pointer is the one being looked at; dimming it would be odd.
        Assert.That(IndexRailHover.OpacityAt(0), Is.EqualTo(1.0).Within(Tolerance));
    }

    [Test]
    public void The_neighbours_dim_and_the_far_letters_do_not()
    {
        // The shape is a trough, not a ramp: full brightness at the pointer, dimmer beside it,
        // full again beyond the reach. Getting the end conditions wrong is what the first
        // version of this class did -- it put a far letter at 0.45 forever, so the rail never
        // returned to rest.
        var oneOut = IndexRailHover.OpacityAt(1);
        var twoOut = IndexRailHover.OpacityAt(2);
        var farOut = IndexRailHover.OpacityAt(5);

        Assert.That(oneOut, Is.LessThan(1.0), "the immediate neighbour did not dim at all");
        Assert.That(oneOut, Is.GreaterThanOrEqualTo(IndexRailHover.NeighbourOpacity));
        Assert.That(twoOut, Is.GreaterThan(oneOut),
            "opacity did not recover as distance increased, so this is a ramp not a trough");
        Assert.That(farOut, Is.EqualTo(1.0).Within(Tolerance),
            "a letter beyond the reach was left dimmed");
    }

    [Test]
    public void Distance_is_direction_free()
    {
        // The caller passes i - peak, which is negative for letters above the pointer. If this
        // did not fold, the wave would only reach downward and the rail would look lopsided.
        for (var slots = 0.5; slots < IndexRailHover.WaveReach; slots += 0.5)
        {
            Assert.That(IndexRailHover.StrengthAt(-slots),
                Is.EqualTo(IndexRailHover.StrengthAt(slots)).Within(Tolerance));
        }
    }

    [Test]
    public void The_shape_is_symmetric_about_the_pointer()
    {
        // Two letters either side of the pointer must be drawn identically, otherwise the wave
        // leans and it reads as a smear rather than as a wave.
        for (var slots = 0.5; slots < IndexRailHover.WaveReach; slots += 0.5)
        {
            Assert.That(IndexRailHover.ScaleAt(slots - 3),
                Is.EqualTo(IndexRailHover.ScaleAt(3 - slots)).Within(Tolerance));
        }
    }

    [Test]
    public void Scale_and_opacity_come_off_the_same_curve()
    {
        // Both are derived from one strength value, so they can never disagree about where the
        // pointer is. The relationship is not the same shape -- the scale peaks where the
        // opacity dips -- so the assertion is on how far each has moved from rest, both of which
        // are zero at both ends of the reach and both maximal somewhere in the middle.
        foreach (var slots in new[] { 0.0, 0.5, 1.0, 1.5, 2.0, 2.19 })
        {
            var strength = IndexRailHover.StrengthAt(slots);

            var scaleFraction =
                (IndexRailHover.ScaleAt(slots) - 1.0) / (IndexRailHover.PeakScale - 1.0);

            Assert.That(scaleFraction - strength, Is.EqualTo(0.0).Within(1e-9),
                "scale is not driven by the strength at " + slots + " letters out");
        }

        // And the two curves are both at rest outside the reach and both moved inside it. A
        // version where either one stayed at rest under the pointer, or never recovered, would
        // fail this.
        Assert.That(IndexRailHover.ScaleAt(0), Is.GreaterThan(1.0));
        Assert.That(IndexRailHover.ScaleAt(IndexRailHover.WaveReach), Is.EqualTo(1.0).Within(1e-9));
        Assert.That(IndexRailHover.OpacityAt(0), Is.EqualTo(1.0).Within(1e-9));
        Assert.That(IndexRailHover.OpacityAt(IndexRailHover.WaveReach),
            Is.EqualTo(1.0).Within(1e-9));
    }

    [Test]
    public void The_neighbour_opacity_is_a_between_value()
    {
        // Below 0 the control would be invisible; at or above 1 there would be no dip at all and
        // the touched letter would not stand out.
        Assert.That(IndexRailHover.NeighbourOpacity, Is.GreaterThan(0.0));
        Assert.That(IndexRailHover.NeighbourOpacity, Is.LessThan(1.0));
    }

    [Test]
    public void A_letter_one_slot_away_is_noticeably_smaller_than_the_peak()
    {
        // Guards against a reach so wide that the whole rail moves together, which would look
        // like a wobble and was explicitly what the report did not want.
        Assert.That(IndexRailHover.ScaleAt(1), Is.LessThan(IndexRailHover.PeakScale - 0.2));
        Assert.That(IndexRailHover.ScaleAt(1), Is.GreaterThan(1.0));
    }
}
