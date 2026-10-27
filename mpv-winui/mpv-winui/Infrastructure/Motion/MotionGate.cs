using System;
using Windows.UI.ViewManagement;

namespace mpv_winui.Infrastructure.Motion;

/// <summary>
/// The gate every behaviour in this module consults before animating.
///
/// The required behaviour is the accessibility one: when the user has turned
/// animations off in Windows, nothing animates and every caller lands straight
/// on its final value. <see cref="AppEnabled"/> is a runtime-only override on
/// top of that, for A/B-ing the motion system without changing a Windows
/// setting. It is deliberately NOT persisted in <c>AppSettings</c>: a stored
/// copy would have to travel through the settings schema, its drift checker and
/// the localization files, and the system setting is the one that actually
/// matters.
///
/// <see cref="Resolve"/> re-reads the system value every time rather than
/// caching the answer at startup. That costs one property read per animation
/// and buys two things: a flip of the Windows setting is picked up without
/// needing an event subscription, and there is no stale cache to invalidate.
/// The value is also compared against the last one observed so that a flip can
/// be announced to behaviours that are already in flight - an animation that
/// started before the user turned the setting off cannot be un-started by an
/// entry check, so <see cref="Changed"/> is how the player control bar forces
/// its panel to its end state instead.
/// </summary>
public static class MotionGate
{
    private static readonly UISettings UiSettings = new();

    private static bool _appEnabled = true;

    private static bool _lastResolved = true;

    /// <summary>
    /// Raises when the resolved answer flips. Fired from <see cref="Resolve"/>,
    /// so it is only observed on the next motion attempt or on an explicit
    /// <see cref="Resolve"/> call - never from a system callback.
    /// </summary>
    public static event Action? Changed;

    /// <summary>Runtime override for A/B testing. Not persisted.</summary>
    public static bool AppEnabled
    {
        get => _appEnabled;
        set
        {
            if (_appEnabled == value)
            {
                return;
            }

            _appEnabled = value;
            Resolve();
        }
    }

    /// <summary>
    /// Reads the current answer. True means animations may run.
    ///
    /// The read is wrapped because <c>AnimationsEnabled</c> is a Windows
    /// Runtime property on a type this app also constructs on the 17763 floor,
    /// and a failure there must degrade to "animate" rather than take the app
    /// down at first use.
    /// </summary>
    public static bool Resolve()
    {
        bool systemEnabled;
        try
        {
            systemEnabled = UiSettings.AnimationsEnabled;
        }
        catch
        {
            systemEnabled = true;
        }

        var enabled = systemEnabled && _appEnabled;
        if (enabled != _lastResolved)
        {
            _lastResolved = enabled;
            Changed?.Invoke();
        }

        return enabled;
    }

    /// <summary>
    /// The current answer without side effects on <see cref="Changed"/> -
    /// for callers that only want to know which branch to take.
    /// </summary>
    public static bool IsEnabled => Resolve();
}
