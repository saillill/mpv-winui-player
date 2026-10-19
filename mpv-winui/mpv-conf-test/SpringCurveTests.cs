using System;
using mpv_winui.Modules.Motion;
using System;

namespace mpv_winui_test;

/// <summary>
/// The spring curve, by number.
///
/// A bounce is easy to get subtly wrong in ways no screenshot shows: an overshoot
/// that never happens (damping too high), one that rings four times (too low), or a
/// curve that does not reach its target at t=1 so the element snaps at the end. All
/// three are arithmetic, so all three are asserted here instead of being eyeballed
/// later.
/// </summary>
[TestFixture]
public class SpringCurveTests
{
    private const double Damping = SpringMath.Damping;
    private const double Frequency = SpringMath.Frequency;

    private static double V(double t) => SpringMath.Value(t, Damping, Frequency);

    [Test]
    public void TheCurveStartsAtZeroAndEndsAtOne()
    {
        Assert.That(V(0.0), Is.EqualTo(0.0).Within(0.001));
        Assert.That(V(1.0), Is.EqualTo(1.0).Within(0.001));
    }

    [Test]
    public void ItOvershootsByRoughlyTheDocumentedAmount()
    {
        // The token exists so callers can size the animation's range; this is what
        // says the token is telling the truth.
        var peak = 0.0;

        for (var i = 1; i < 200; i++)
        {
            peak = Math.Max(peak, V(i / 200.0));
        }

        Assert.That(peak, Is.GreaterThan(1.05),
            "a curve that never passes its target is an ease-out, not a spring");

        Assert.That(peak, Is.LessThan(1.30),
            "past 1.3 the motion stops reading as a settle and starts reading as a wobble");

        Assert.That(peak, Is.EqualTo(1.0 + SpringMath.Overshoot).Within(0.03),
            $"the documented overshoot is {SpringMath.Overshoot}, measured {peak - 1.0:F3}");
    }

    [Test]
    public void ItSettlesRatherThanRinging()
    {
        // After the first overshoot the curve must stay within a hair of 1.0 by the
        // end of the settle period, or the element visibly trembles.
        var lateDeviation = 0.0;

        for (var i = 80; i <= 100; i++)
        {
            lateDeviation = Math.Max(lateDeviation, Math.Abs(V(i / 100.0) - 1.0));
        }

        Assert.That(lateDeviation, Is.LessThan(0.02),
            $"still {lateDeviation:F3} away from the target in the last fifth of the curve");
    }

    [Test]
    public void CriticallyDampedNeverOvershoots()
    {
        // Damping 1.0 is the boundary: no overshoot at all. Worth pinning because
        // the formula has a separate branch there, and a sign error in it would
        // only show as a tiny bounce nobody would question.
        for (var i = 0; i <= 100; i++)
        {
            Assert.That(SpringMath.Value(i / 100.0, 1.0, Frequency),
                Is.LessThanOrEqualTo(1.001),
                $"critically damped curve overshot at t={i / 100.0:F2}");
        }
    }

    [Test]
    public void TheCurveIsMonotonicUntilItsFirstPeak()
    {
        // A spring may overshoot, but it must not hesitate: the first stretch is the
        // element moving towards its target, and a dip there reads as a stutter.
        double previous = V(0.001);

        for (var i = 1; i < 40; i++)
        {
            double current = V(i / 200.0);

            // Only up to the first peak; after that it comes back down by design.
            if (current < previous)
            {
                break;
            }

            previous = current;
        }

        Assert.That(previous, Is.GreaterThan(0.5),
            "the curve turned round before it was halfway, which no damped spring does");
    }

    [Test]
    public void ExtremeInputIsClamped()
    {
        // Callers sample at t in [0, 1]; a rounding error outside that range must not
        // produce a wild value.
        Assert.That(V(-5.0), Is.EqualTo(0.0));
        Assert.That(V(5.0), Is.EqualTo(1.0));
    }

    [Test]
    public void LowerDampingOvershootsMore()
    {
        // The relationship, not a number: it is what makes SpringDamping tunable
        // with a predictable effect.
        double Peak(double damping)
        {
            var peak = 0.0;

            for (var i = 1; i < 200; i++)
            {
                peak = Math.Max(peak, SpringMath.Value(i / 200.0, damping, Frequency));
            }

            return peak;
        }

        Assert.That(Peak(0.35), Is.GreaterThan(Peak(0.55)));
        Assert.That(Peak(0.55), Is.GreaterThan(Peak(0.85)));
    }
}
