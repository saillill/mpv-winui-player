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

    /// <summary>Track skipping and the secondary window mode drop.</summary>
    Compact = 2,

    /// <summary>Only the primary transport plus volume remain.</summary>
    Narrow = 3,

    /// <summary>Portrait / very narrow: play, volume and the overflow menu only.</summary>
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
    /// Ids not listed are always visible (play, volume/mute, more).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, ControlBarTier> HiddenFromTier =
        new Dictionary<string, ControlBarTier>(StringComparer.OrdinalIgnoreCase)
        {
            // --- Standard and below: secondary transport / rarely used ---
            ["skip-back"] = ControlBarTier.Standard,
            ["skip-forward"] = ControlBarTier.Standard,
            ["random"] = ControlBarTier.Standard,
            ["rate"] = ControlBarTier.Standard,

            // --- Compact and below: track skipping, window extras ---
            ["previous"] = ControlBarTier.Compact,
            ["next"] = ControlBarTier.Compact,
            ["fullwindow"] = ControlBarTier.Compact,
            ["aspect"] = ControlBarTier.Compact,

            // --- Narrow and below: only the primary transport survives ---
            ["fullscreen"] = ControlBarTier.Narrow,
            ["tracks"] = ControlBarTier.Narrow,
            ["volume-slider"] = ControlBarTier.Narrow,

            // --- Minimal: keep play, volume(mute) and the overflow menu ---
            ["panel"] = ControlBarTier.Minimal,
            ["pip"] = ControlBarTier.Minimal,
        };

    /// <summary>Every control id the adaptive layout can hide (used by the overflow menu).</summary>
    public static IEnumerable<string> AllAdaptiveIds => HiddenFromTier.Keys;

    /// <summary>Tier for a control-bar width in logical pixels.</summary>
    public static ControlBarTier TierFor(double width)
    {
        if (double.IsNaN(width) || width <= 0)
        {
            // Not measured yet: assume the roomiest layout rather than hiding
            // controls that would fit.
            return ControlBarTier.Wide;
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

        // Portrait / very narrow windows. Height is not part of the decision:
        // a portrait window is narrow by definition, so its width already
        // selects a low tier - no separate orientation rule is needed.
        return ControlBarTier.Minimal;
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
