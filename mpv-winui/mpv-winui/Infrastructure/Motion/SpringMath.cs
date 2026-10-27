using System;

namespace mpv_winui.Infrastructure.Motion;

/// <summary>
/// The damped-spring curve, as arithmetic.
///
/// Separated from <see cref="MotionEasing"/> because that file's curves are
/// per-Compositor objects and this is pure maths: the formula is what decides
/// whether a bounce reads as a settle or as a wobble, and it is the only part of
/// the motion system that can be asserted by number. Keeping it here means the
/// test project can link it without dragging in WinUI.
///
/// The four Fluent curves in MotionEasing are monotonic by construction. A spring is
/// not - it passes its target and comes back - and Composition has no spring easing
/// function, so callers sample this into keyframes.
/// </summary>
internal static class SpringMath
{
    /// <summary>
    /// How long a spring takes to settle, in milliseconds.
    ///
    /// 280 rather than the 167 of a move: a bounce that settles as fast as a
    /// straight move reads as a glitch. Deliberately outside the official 83/167/250
    /// triad, which describes transitions between two states - a spring is a
    /// different kind of motion and does not belong to that scale.
    /// </summary>
    internal const double SettleMs = 280;

    /// <summary>
    /// Damping ratio. 1.0 is critically damped and does not overshoot at all; below
    /// 1 oscillates.
    ///
    /// 0.55 is one clear overshoot and a barely-there second one. Lower - 0.35 - is
    /// a cartoon wobble; higher - 0.85 - is hard to tell from a plain ease-out.
    /// </summary>
    internal const double Damping = 0.55;

    /// <summary>
    /// Angular frequency, in radians over the settle period. Chosen so the curve has
    /// visibly finished by t = 1 instead of being cut off by the last keyframe.
    /// </summary>
    internal const double Frequency = 11.0;

    /// <summary>
    /// How far past its target the curve travels at <see cref="Damping"/>.
    ///
    /// Recorded because a caller sizing the animation's range needs it: a scale
    /// heading for 1.0 peaks at 1 + that.
    /// </summary>
    internal const float Overshoot = 0.14f;

    /// <summary>
    /// A damped spring's value at <paramref name="t"/>, where 1 is the resting
    /// target and values above 1 are the overshoot.
    /// </summary>
    /// <param name="t">Normalised time, 0..1 over the settle period.</param>
    /// <param name="damping">
    /// Damping ratio. 1.0 is critically damped (no overshoot); below 1 oscillates.
    /// </param>
    /// <param name="frequency">
    /// Angular frequency, in radians over the settle period. Chosen so the motion has
    /// visibly finished by t = 1 rather than being cut off by the keyframe table.
    /// </param>
    /// <remarks>
    /// The standard underdamped step response of a unit spring:
    ///
    ///     x(t) = 1 - e^(-ζωt) * (cos(ωd t) + (ζω / ωd) sin(ωd t))
    ///
    /// with ωd = ω * sqrt(1 - ζ²). At ζ = 1 it degenerates to 1 - e^(-ωt)(1 + ωt),
    /// which has no overshoot - that branch is written out rather than approximated,
    /// because the general formula divides by ωd.
    /// </remarks>
    internal static float Value(double t, double damping, double frequency)
    {
        if (t <= 0)
        {
            return 0f;
        }

        if (t >= 1)
        {
            return 1f;
        }

        var omega = frequency;

        if (damping >= 1.0)
        {
            return (float)(1.0 - (Math.Exp(-omega * t) * (1.0 + (omega * t))));
        }

        var dampedOmega = omega * Math.Sqrt(1.0 - (damping * damping));

        var envelope = Math.Exp(-damping * omega * t);

        var oscillation = Math.Cos(dampedOmega * t)
            + ((damping * omega / dampedOmega) * Math.Sin(dampedOmega * t));

        return (float)(1.0 - (envelope * oscillation));
    }

    /// <summary>
    /// <see cref="Value"/> mapped onto a scale that starts at 1 and peaks at
    /// <paramref name="peak"/>.
    ///
    /// The callers all animate a scale, and every one of them would otherwise repeat
    /// this one multiplication.
    /// </summary>
    internal static float Scale(double t, float peak, double damping, double frequency) =>
        1f + ((peak - 1f) * Value(t, damping, frequency));
}
