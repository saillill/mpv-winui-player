using System;
using System.Collections.Generic;

namespace mpv_winui.Modules.Player;

/// <summary>
/// Width-adaptive control-bar tiers: which control-bar buttons survive at which
/// window width. Pure data/logic (no WinUI types) so the exact source shipped
/// with the app can be linked into unit tests, the same way
/// <see cref="ControlBarLayoutGrammar"/> is.
///
/// Before this module existed the tiers lived in four XAML
/// VisualStates (Wide/Medium/Compact/Narrow ≥700/≥500/≥280). That had three
/// problems:
///   1. it was cumulative - every state only *added* Collapsed setters, and the
///      "Wide" state was empty, so showing a control again depended entirely on
///      the framework restoring a value the state never set back;
///   2. it fought the settings channel: `ApplyControlBarStyle` sets Visibility
///      directly, and a later width change only re-applied its own setters, so
///      the two writers could disagree;
///   3. several buttons were never covered at all (tracks / panel / PiP stayed
///      visible at every width), so the narrowest window still overflowed.
///
/// Here every control has an explicit "hidden from tier N" entry, and the
/// resolved visibility is a single function of (tier, user-hidden set), so both
/// writers produce the same answer.
/// </summary>
internal enum ControlBarTier
{
    /// <summary>Everything visible; the overflow button is hidden.</summary>
    Wide = 0,

    /// <summary>Secondary transport (seek-by-step, playback mode, rate) drops.</summary>
    Standard = 1,

    /// <summary>The full-window toggle drops.</summary>
    Compact = 2,

    /// <summary>The volume strip drops; the bar starts tightening its button pitch.</summary>
    Narrow = 3,

    /// <summary>
    /// Portrait, or narrower than <see cref="ControlBarAdaptiveLayout.NarrowWidth"/>.
    /// The bar keeps a 3/3/3 grid: previous + play + next in the middle,
    /// three controls on each side, and the overflow button at the left of the
    /// right group. Nothing else is dropped -- at this tier the pitch is what
    /// gives way, not the controls (see <c>IsTight</c>).
    /// </summary>
    Minimal = 4,
}

internal static class ControlBarAdaptiveLayout
{
    /// <summary>
    /// Width thresholds in logical pixels. Each value is the width at which the
    /// tier starts (tiers are inclusive lower bounds, so
    /// <c>width &gt;= StandardWidth</c> is the Standard tier).
    /// </summary>
    public const double WideWidth = 820;
    public const double StandardWidth = 620;
    public const double CompactWidth = 460;
    public const double NarrowWidth = 340;

    /// <summary>
    /// The control id is hidden at this tier **and every narrower tier**.
    ///
    /// Only four kinds of thing drop, in this order, and nothing else does:
    /// the secondary transport, the full-window toggle, then the volume strip.
    /// Everything a portrait window still needs -- previous/next, the track
    /// selector, the panel, aspect, PiP and full screen -- survives to the
    /// narrowest tier, because the tier answers "does it fit", not "is this
    /// window small". The previous table hid previous/next, tracks, aspect,
    /// fullscreen, panel and PiP all the way down, which left portrait with a
    /// bare play button and pushed seven reachable actions into the menu.
    ///
    /// The three controls on each side of a portrait bar fall out of this
    /// table rather than being counted: the middle is built from the transport
    /// ids, and each side loses exactly one of its zone's four.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, ControlBarTier> HiddenFromTier =
        new Dictionary<string, ControlBarTier>(StringComparer.OrdinalIgnoreCase)
        {
            // --- Standard and below: secondary transport / rarely used ---
            ["skip-back"] = ControlBarTier.Standard,
            ["skip-forward"] = ControlBarTier.Standard,
            ["random"] = ControlBarTier.Standard,
            ["rate"] = ControlBarTier.Standard,

            // --- Compact and below: the secondary window mode ---
            ["fullwindow"] = ControlBarTier.Compact,

            // --- Narrow and below: the widest non-essential element ---
            ["volume-slider"] = ControlBarTier.Narrow,
        };

    /// <summary>
    /// The overflow menu's entries, in menu order: the control id, and whether
    /// an explicit user-hide preference suppresses the entry.
    ///
    /// One table for two readers. <c>MoreFlyout_Opening</c> builds the menu from
    /// it and <see cref="HasOverflowContent"/> decides the More button from it,
    /// which is what stops the button opening an empty menu. The flags are not
    /// cosmetic: the transport entries ignore the hide preference (a hidden
    /// previous/next is still offered, because unlike the movable icons there is
    /// no settings toggle that means "never show me this"), while the movable
    /// icons honour it, because there the preference is explicit.
    ///
    /// An entry with the flag set is only ever reachable through the tier, so it
    /// has to be in <see cref="HiddenFromTier"/> or it can never be offered at
    /// all. That is why this list is short: it used to carry aspect, PiP, tracks,
    /// panel and full screen as well, and while the tier hid those at every
    /// narrow width the menu was the only way to reach them. They now survive to
    /// the narrowest tier, so those entries became unreachable and are gone;
    /// ControlBarAdaptiveLayoutTests asserts the invariant.
    /// </summary>
    public static readonly (string Id, bool RespectsUserHide)[] OverflowEntries =
    [
        ("skip-back", false),
        ("skip-forward", false),
        ("random", true),
        ("rate", false),
        ("previous", false),
        ("next", false),
        ("fullwindow", true),
    ];

    /// <summary>Every control id the adaptive layout can hide (used by the overflow menu).</summary>
    public static IEnumerable<string> AllAdaptiveIds => HiddenFromTier.Keys;

    /// <summary>Tier for a control-bar width in logical pixels, ignoring orientation.</summary>
    public static ControlBarTier TierFor(double width) => TierFor(width, 0);

    /// <summary>
    /// Tier for a window of this logical size.
    ///
    /// Orientation is part of the answer. The previous version claimed a
    /// portrait window "is narrow by definition, so its width already selects a
    /// low tier", and that is false at the size people actually use: a
    /// 384x744 portrait window is above <see cref="NarrowWidth"/> and landed in
    /// <see cref="ControlBarTier.Narrow"/>, so the portrait rules never ran and
    /// the bar fell back to whatever the width tiers happened to leave. A
    /// window that is taller than it is wide is portrait whatever its width.
    /// </summary>
    public static ControlBarTier TierFor(double width, double height)
    {
        if (double.IsNaN(width) || width <= 0)
        {
            // Not measured yet: assume the roomiest layout rather than hiding
            // controls that would fit.
            return ControlBarTier.Wide;
        }

        if (IsPortrait(width, height))
        {
            return ControlBarTier.Minimal;
        }

        if (width >= WideWidth)
        {
            return ControlBarTier.Wide;
        }
        if (width >= StandardWidth)
        {
            return ControlBarTier.Standard;
        }
        if (width >= CompactWidth)
        {
            return ControlBarTier.Compact;
        }
        if (width >= NarrowWidth)
        {
            return ControlBarTier.Narrow;
        }

        return ControlBarTier.Minimal;
    }

    /// <summary>
    /// True when the window is taller than it is wide. Unmeasured sizes are not
    /// portrait, so a not-yet-laid-out bar is never mistaken for one.
    /// </summary>
    public static bool IsPortrait(double width, double height) =>
        width > 0 && height > 0 && height > width;

    /// <summary>
    /// True when the bar should tighten its button pitch. The narrow tiers keep
    /// three controls on each side plus three in the middle, and at the default
    /// 40px pitch that is 400px of buttons in a 340px window -- the row would
    /// clip. The pitch gives way before the controls do.
    /// </summary>
    public static bool IsTight(ControlBarTier tier) => tier >= ControlBarTier.Narrow;

    /// <summary>
    /// True when the overflow menu would offer at least one control right now.
    ///
    /// This is the More button's visibility rule, and it is deliberately the
    /// same question the menu answers. It used to be
    /// <c>tier != Wide || userHidden.Count &gt; 0</c>, which is a different
    /// question twice over: a tier below Wide can still have every control it
    /// keeps on the bar, and a control the user hid on purpose is never offered
    /// back by the menu. Either way the button appeared with nothing behind it.
    /// </summary>
    public static bool HasOverflowContent(ControlBarTier tier, IReadOnlySet<string> userHidden)
    {
        foreach (var (id, respectsUserHide) in OverflowEntries)
        {
            // Already on the bar: the menu does not repeat it.
            if (IsVisible(id, tier, userHidden))
            {
                continue;
            }
            // Hidden on purpose: the menu does not undo that.
            if (respectsUserHide && userHidden is not null && userHidden.Contains(id))
            {
                continue;
            }
            return true;
        }
        return false;
    }

    /// <summary>True when the tier alone keeps the control visible.</summary>
    public static bool VisibleAtTier(string id, ControlBarTier tier)
    {
        return !HiddenFromTier.TryGetValue(id, out var from) || tier < from;
    }

    /// <summary>
    /// Final visibility: the user's "hidden icons" preference wins over the
    /// tier, and the tier wins over the default. A control the user hid is never
    /// shown back by widening the window.
    /// </summary>
    public static bool IsVisible(string id, ControlBarTier tier, IReadOnlySet<string> userHidden)
    {
        return userHidden is null || !userHidden.Contains(id) ? VisibleAtTier(id, tier) : false;
    }

    /// <summary>
    /// The settings channel only knows the "volume" id (one toggle for the
    /// mute button and its slider). The tier treats the slider separately
    /// because the slider is the widest non-essential element in the bar.
    /// </summary>
    public const string VolumeId = "volume";
    public const string VolumeSliderId = "volume-slider";
}
