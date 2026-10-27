using System;

namespace mpv_winui.Modules.Player;

/// <summary>
/// The letter rail's hover wave, as arithmetic.
///
/// Pure and separate from the view for the same reason <see cref="LibraryIndexLetter"/> and
/// <c>ControlBarAdaptiveLayout.TierFor</c> are: the rule is the part that can be wrong, and it can
/// be tested without a window. The view's job is only to apply these numbers.
///
/// Why this exists at all, stated plainly because it is the interesting part. The wave was
/// implemented and then could not be observed: every MCP probe runs under a guard that keeps the
/// harness out of the foreground, a window that is not foreground receives no hover input, so
/// `PointerEntered` never fired and no pixel or rectangle ever changed. Three measurement
/// approaches failed before a log line explained why. Extracting the arithmetic means the claim
/// "the wave has the shape it should" is checkable even though "the frame shows it" is not.
///
/// A cosine hump, not a linear ramp: the peak stays flat, so the letter under the pointer and its
/// neighbour do not flicker between two nearly equal values as the pointer crosses the boundary
/// between them.
/// </summary>
public static class IndexRailHover
{
    /// <summary>How much the letter under the pointer grows. 1.0 is untouched.</summary>
    public const double PeakScale = 1.9;

    /// <summary>
    /// How far the effect reaches, counted in letters. 2.2 means the third letter out is
    /// untouched, which reads as a wave; reaching every letter would read as a wobble.
    /// </summary>
    public const double WaveReach = 2.2;

    /// <summary>
    /// How much a neighbouring letter is dimmed relative to the peak. This is the opacity at
    /// strength 1, not the opacity at distance.
    ///
    /// The distinction matters and this file got it wrong first: written as "the floor", the
    /// value was used as the resting opacity too, so a letter far from the pointer sat at 0.45
    /// forever instead of returning to full. The test
    /// <c>Nothing_moves_beyond_the_reach</c> caught it -- 2.2 letters out reported 0.45 where it
    /// should report 1.0.
    ///
    /// So: strength 0 (far away) means opacity 1.0, full brightness. Strength 1 (under the
    /// pointer) means opacity 1.0 as well, because the letter being touched is the one you are
    /// looking at. The dip is in the MIDDLE -- a neighbour at 0.45 is what makes the touched
    /// letter stand out. That is a trough, not a floor, and calling it a floor is what produced
    /// the bug.
    /// </summary>
    public const double NeighbourOpacity = 0.45;

    /// <summary>
    /// The wave's strength at a given distance from the pointer, in letters. 1 under the pointer,
    /// falling to 0 at <see cref="WaveReach"/> and staying there.
    ///
    /// Exposed rather than inlined into the scale because the strength is the thing with a shape,
    /// and the shape is what a test can assert.
    /// </summary>
    public static double StrengthAt(double slotsFromPointer)
    {
        // Negative distance is meaningless but not worth throwing over: a caller that passes it
        // gets the same answer as for its absolute value, which is what "distance" means.
        var slots = Math.Abs(slotsFromPointer);

        if (slots >= WaveReach)
        {
            return 0.0;
        }

        return 0.5 * (1.0 + Math.Cos(Math.PI * slots / WaveReach));
    }

    /// <summary>The scale a letter at this distance is drawn at.</summary>
    public static double ScaleAt(double slotsFromPointer) =>
        1.0 + (PeakScale - 1.0) * StrengthAt(slotsFromPointer);

    /// <summary>
    /// The opacity a letter at this distance is drawn at.
    ///
    /// **Takes a distance, not a strength** -- and the first version of this file got that wrong,
    /// which the tests caught. Written as a bare "opacity at N letters out" the formula has to
    /// hold BOTH ends at 1.0 and dip only in between:
    ///
    ///     distance   0     1      2     2.2+
    ///     strength   1.0   0.256  0.0   0.0
    ///     scale      1.90  1.23   1.0   1.0
    ///     opacity    1.00  0.59   1.0   1.0
    ///
    /// so it is not `floor + (1 - floor) * strength` (that dims the touched letter and leaves far
    /// letters at the floor forever) and not `1 - (1 - floor) * strength` either (that is the same
    /// mistake mirrored). The dip peaks in the MIDDLE of the reach, which is
    /// <c>4 * strength * (1 - strength)</c>: zero when strength is 0 or 1, and 1 at strength 0.5.
    /// </summary>
    public static double OpacityAt(double slotsFromPointer)
    {
        var strength = StrengthAt(slotsFromPointer);

        // 0 at both ends, 1 where the strength is halfway. A letter is dimmed by how far it is
        // from BOTH the pointer and the rest of the rail, which is what makes the wave a wave
        // rather than a highlight with a tail.
        var dip = 4.0 * strength * (1.0 - strength);

        return 1.0 - (1.0 - NeighbourOpacity) * dip;
    }
}
