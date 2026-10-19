using System;
using System.Numerics;

namespace mpv_winui.Modules.Motion;

/// <summary>
/// The single source of numbers for motion in this app.
///
/// Durations are the three official Fluent values, and they are deliberately
/// the same ones WinUI itself ships as theme resources - so the XAML side of a
/// new control can point at <c>ControlFasterAnimationDuration</c> /
/// <c>ControlFastAnimationDuration</c> /
/// <c>ControlNormalAnimationDuration</c> and land on the same 83/167/250 ms
/// that composition animations built from here use. No third value exists, and
/// nothing in this app should invent one.
///
/// Easing is expressed as the four official cubic bezier curves. They are kept
/// as raw control points rather than as ready-made easing functions because an
/// <c>EasingFunction</c> belongs to the <c>Compositor</c> that created it -
/// see <see cref="MotionEasing"/> for the per-compositor factory.
///
///   enter  cubic-bezier(0,    0,    0, 1)   position, scale, rotation
///   move   cubic-bezier(0.55, 0.55, 0, 1)   an element travelling between two
///                                           points it already occupies
///   exit   cubic-bezier(1,    0,    1, 1)   leaving - MUST be paired with a
///                                           fade, never used on its own
///   fade   linear                   83 ms   opacity only
///
/// Source: learn.microsoft.com/windows/apps/design/signature-experiences/motion
/// and .../design/motion/timing-and-easing.
/// </summary>
public static class MotionTokens
{
    /// <summary>83 ms. Fades, and the exit half of a paired exit.</summary>
    public const double ShortMs = 83;

    /// <summary>167 ms. Entrances and moves - the workhorse.</summary>
    public const double MediumMs = 167;

    /// <summary>250 ms. Reserved for page-level surfaces.</summary>
    public const double LongMs = 250;

    public static TimeSpan Short => TimeSpan.FromMilliseconds(ShortMs);

    public static TimeSpan Medium => TimeSpan.FromMilliseconds(MediumMs);

    public static TimeSpan Long => TimeSpan.FromMilliseconds(LongMs);

    // Cubic bezier control points, in the CSS order the Fluent spec states
    // them: (x1, y1) then (x2, y2).
    public static readonly Vector2 EnterControl1 = new(0f, 0f);
    public static readonly Vector2 EnterControl2 = new(0f, 1f);

    public static readonly Vector2 MoveControl1 = new(0.55f, 0.55f);
    public static readonly Vector2 MoveControl2 = new(0f, 1f);

    public static readonly Vector2 ExitControl1 = new(1f, 0f);
    public static readonly Vector2 ExitControl2 = new(1f, 1f);

    /// <summary>
    /// The names WinUI itself gives these three values in its theme
    /// resources. Recorded here because XAML cannot read a composition token,
    /// so any XAML-side animation added later should reference the framework
    /// key of the matching name rather than hard-coding a millisecond figure.
    /// Nothing in this module's own behaviours needs them - every one of those
    /// is composition-side.
    /// </summary>
    public const string FrameworkDurationShortKey = "ControlFasterAnimationDuration";

    public const string FrameworkDurationMediumKey = "ControlFastAnimationDuration";

    public const string FrameworkDurationLongKey = "ControlNormalAnimationDuration";

    /// <summary>
    /// A displacement small enough to read as "settling into place" rather
    /// than "arriving from off-screen". Used where the element is short and a
    /// long slide would look like it is falling rather than appearing.
    /// </summary>
    public const float DefaultEnterOffset = 24f;

    /// <summary>
    /// The entrance distance for content that arrives as a body of cards or
    /// rows: the library grid and the settings list.
    ///
    /// This was 8, and 8 was the reason the entrance was reported as missing
    /// rather than as too fast - across 167 ms it is roughly one tenth of what
    /// the eye reads as a slide, and paired with a fade it simply looked like a
    /// fade. Microsoft's own transition guidance puts a content entrance at
    /// "slide up 150px"; 150 is for a full-width page, so the figure here is
    /// scaled down to an element the size of a poster card while staying large
    /// enough to be unmistakable.
    /// </summary>
    public const float ContentEnterOffset = 48f;

    /// <summary>
    /// The excursion of an expanding detail panel. Smaller than a content
    /// entrance because the panel itself is only a few lines tall: at 48 the
    /// text would appear to overshoot the row it belongs to.
    /// </summary>
    public const float RowRevealOffset = 16f;

    /// <summary>Offset used by the player control bar, which slides further.</summary>
    public const float PlayerPanelOffset = 48f;

    /// <summary>Scale a dropdown starts from before growing to full size.</summary>
    public const float DropdownFromScale = 0.94f;
}
