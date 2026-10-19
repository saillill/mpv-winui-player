using Microsoft.UI.Composition;
using System.Runtime.CompilerServices;

namespace mpv_winui.Modules.Motion;

/// <summary>
/// Easing functions for the four official curves, built once per
/// <see cref="Compositor"/>.
///
/// An <c>EasingFunction</c> cannot be shared between compositors - it belongs
/// to the one that created it - so a plain static field would be wrong and a
/// static factory would rebuild four objects on every animation. The cache is
/// keyed weakly on the compositor because this app has more than one: the main
/// window, and a fresh one every time the picture-in-picture window is opened.
/// A strong reference in a dictionary would keep every PiP compositor alive for
/// the life of the process.
///
/// Everything here is concrete types and a table lookup - no reflection, so it
/// survives Native AOT and trimming unchanged.
/// </summary>
public static class MotionEasing
{
    private sealed class CurveSet
    {
        public CubicBezierEasingFunction Enter = null!;

        public CubicBezierEasingFunction Move = null!;

        public CubicBezierEasingFunction Exit = null!;

        public LinearEasingFunction Linear = null!;
    }

    private static readonly ConditionalWeakTable<Compositor, CurveSet> Cache = new();

    private static CurveSet For(Compositor compositor) =>
        Cache.GetValue(compositor, static c => new CurveSet
        {
            Enter = c.CreateCubicBezierEasingFunction(MotionTokens.EnterControl1, MotionTokens.EnterControl2),
            Move = c.CreateCubicBezierEasingFunction(MotionTokens.MoveControl1, MotionTokens.MoveControl2),
            Exit = c.CreateCubicBezierEasingFunction(MotionTokens.ExitControl1, MotionTokens.ExitControl2),
            Linear = c.CreateLinearEasingFunction(),
        });

    /// <summary>cubic-bezier(0, 0, 0, 1) - an element that did not exist before now arriving.</summary>
    public static CubicBezierEasingFunction Enter(Compositor compositor) => For(compositor).Enter;

    /// <summary>cubic-bezier(0.55, 0.55, 0, 1) - an element already on screen moving.</summary>
    public static CubicBezierEasingFunction Move(Compositor compositor) => For(compositor).Move;

    /// <summary>
    /// cubic-bezier(1, 0, 1, 1) - an element leaving. The spec pairs this with
    /// a fade; using it alone reads as an abrupt disappearance, so every caller
    /// in this module fades at the same time.
    /// </summary>
    public static CubicBezierEasingFunction Exit(Compositor compositor) => For(compositor).Exit;

    /// <summary>Linear - opacity only. 83 ms.</summary>
    public static LinearEasingFunction Linear(Compositor compositor) => For(compositor).Linear;
}
