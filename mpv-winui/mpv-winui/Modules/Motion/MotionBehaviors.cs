using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.Numerics;

namespace mpv_winui.Modules.Motion;

/// <summary>
/// The named, reusable motion behaviours. Nothing in this app should build a
/// one-off composition animation; it should name one of these instead, so that
/// the whole product moves on the same four curves and three durations.
///
/// Two rules run through every method here.
///
/// 1. DISPLACEMENT GOES THROUGH THE "Translation" PROPERTY-SET ENTRY, never
///    <c>Visual.Offset</c>.
///
///    Offset is owned by layout: a completed Offset animation keeps filling the
///    value, so the element stays permanently displaced, and if layout then
///    gives it a new offset the animation's stale value wins. That is the bug
///    recorded in <c>PlayerControl.Animation.cs</c> where the control bar was
///    "stranded" after a zoom.
///
///    The composition-side answer is the "Translation" entry that
///    <c>ElementCompositionPreview.SetIsTranslationEnabled</c> publishes on the
///    element's visual, and the Windows App SDK documents that method as
///    toggling "the ability to set, animate, and use a render-time post-layout
///    translate transform to position XAML elements on screen using Composition
///    APIs". Post-layout is the whole point: the value composes on top of where
///    layout put the element instead of replacing it, so it needs no base
///    offset to be read and cannot be reset by a later layout pass. That is why
///    the PiP path and the main-window path share one implementation here.
///
///    It also has to be a departure from the resting value of zero, not a
///    journey to a remembered position, which is why every distance in this
///    module is a magnitude rather than a coordinate - and why those magnitudes
///    are large enough to see. An entrance that travels eight pixels is
///    indistinguishable from a plain fade.
///
/// 2. COMPOSITION OPACITY MULTIPLIES XAML OPACITY. These behaviours animate the
///    composition value only; the XAML <c>Opacity</c> of the element is a
///    separate factor the caller keeps at 1 (the one exception in this app is
///    the player bar gradient, which parks at 0.01 to stay hit-testable - that
///    is the caller's business, not this module's).
///
/// When <see cref="MotionGate"/> says animations are off, every method sets the
/// final value synchronously, invokes its completion callback immediately, and
/// returns. There is no separate "reduced motion" code path for callers to
/// remember.
/// </summary>
public static class MotionBehaviors
{
    /// <summary>
    /// The name <c>ElementCompositionPreview.SetIsTranslationEnabled</c> and the
    /// XAML <c>UIElement.Translation</c> property both read. Spelled once.
    /// </summary>
    private const string Translation = "Translation";

    private const string Opacity = "Opacity";

    private const string Scale = "Scale";

    // ---------------------------------------------------------------- plumbing

    /// <summary>
    /// The visual for an element, with the translation channel published.
    ///
    /// <c>SetIsTranslationEnabled</c> tells XAML to apply that entry, and the
    /// entry has to exist before it can be animated - a composition animation
    /// cannot target a property set member that was never inserted. Both calls
    /// are idempotent, which matters because they run from click handlers with
    /// no single natural "initialise once" moment.
    /// </summary>
    private static Visual VisualOf(UIElement element)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);

        if (visual.Properties.TryGetVector3(Translation, out _) != CompositionGetValueStatus.Succeeded)
        {
            visual.Properties.InsertVector3(Translation, Vector3.Zero);
        }

        return visual;
    }

    private static Vector3 TranslationOf(Visual visual) =>
        visual.Properties.TryGetVector3(Translation, out var value) == CompositionGetValueStatus.Succeeded
            ? value
            : Vector3.Zero;

    private static ScalarKeyFrameAnimation Scalar(
        Compositor compositor,
        float from,
        float to,
        TimeSpan duration,
        CompositionEasingFunction? easing,
        TimeSpan delay)
    {
        var animation = compositor.CreateScalarKeyFrameAnimation();
        animation.Duration = duration;
        animation.InsertKeyFrame(0f, from);
        animation.InsertKeyFrame(1f, to, easing);
        ApplyDelay(animation, delay);
        return animation;
    }

    /// <summary>
    /// Displacement for both axes in one animation. A single vector animation
    /// keeps a diagonal move on one curve - two scalar sub-channel animations
    /// would each be on-curve but could start on different ticks.
    /// </summary>
    private static Vector3KeyFrameAnimation TranslateTo(
        Visual visual,
        float x,
        float y,
        TimeSpan duration,
        CompositionEasingFunction? easing,
        TimeSpan delay)
    {
        var current = TranslationOf(visual);
        var animation = visual.Compositor.CreateVector3KeyFrameAnimation();
        animation.Duration = duration;
        animation.InsertKeyFrame(0f, new Vector3(current.X, current.Y, 0f));
        animation.InsertKeyFrame(1f, new Vector3(x, y, 0f), easing);
        ApplyDelay(animation, delay);
        return animation;
    }

    /// <summary>
    /// A zero delay is not reliably the same as an unset delay, so only assign
    /// when there is one.
    /// </summary>
    private static void ApplyDelay(KeyFrameAnimation animation, TimeSpan delay)
    {
        if (delay > TimeSpan.Zero)
        {
            animation.DelayTime = delay;
        }
    }

    /// <summary>
    /// Runs <paramref name="start"/> and then calls <paramref name="completed"/>
    /// once every animation it kicked off has finished. A scoped batch is the
    /// composition-native completion signal - the alternative, a DispatcherTimer
    /// counting down the duration, drifts against the compositor and was one of
    /// the two timing paths this module replaced.
    /// </summary>
    private static void Batch(Compositor compositor, Action start, Action? completed)
    {
        if (completed is null)
        {
            start();
            return;
        }

        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        start();
        batch.Completed += (_, _) => completed();
        batch.End();
    }

    /// <summary>Stops the translation animation and parks the entry at the given offset.</summary>
    private static void ParkTranslated(Visual visual, float x, float y)
    {
        visual.StopAnimation(Translation);
        visual.Properties.InsertVector3(Translation, new Vector3(x, y, 0f));
    }

    // ------------------------------------------------------------------ fades

    /// <summary>
    /// Opacity only, linear, 83 ms - the "minimum viable" motion the spec gives
    /// for elements that need to appear without travelling.
    /// </summary>
    public static void FadeIn(UIElement element, TimeSpan? delay = null)
    {
        var visual = VisualOf(element);
        var compositor = visual.Compositor;

        if (!MotionGate.Resolve())
        {
            visual.StopAnimation(Opacity);
            visual.Opacity = 1f;
            return;
        }

        visual.StopAnimation(Opacity);
        visual.StartAnimation(
            Opacity,
            Scalar(compositor, 0f, 1f, MotionTokens.Short, MotionEasing.Linear(compositor), delay ?? TimeSpan.Zero));
    }

    /// <summary>
    /// Opacity only, linear, 83 ms. <paramref name="completed"/> runs after the
    /// animation finishes, which is where callers collapse the element.
    /// </summary>
    public static void FadeOut(UIElement element, Action? completed = null, TimeSpan? delay = null)
    {
        var visual = VisualOf(element);
        var compositor = visual.Compositor;

        if (!MotionGate.Resolve())
        {
            visual.StopAnimation(Opacity);
            visual.Opacity = 0f;
            completed?.Invoke();
            return;
        }

        visual.StopAnimation(Opacity);
        Batch(
            compositor,
            () => visual.StartAnimation(
                Opacity,
                Scalar(compositor, visual.Opacity, 0f, MotionTokens.Short, MotionEasing.Linear(compositor), delay ?? TimeSpan.Zero)),
            completed);
    }

    // -------------------------------------------------------------- entrances

    /// <summary>
    /// An element that did not exist a moment ago arriving: it fades in while
    /// settling up into place. Enter curve, 167 ms.
    /// </summary>
    public static void ContentEnter(
        UIElement element,
        float fromY = MotionTokens.DefaultEnterOffset,
        TimeSpan? delay = null,
        Action? completed = null)
    {
        SlideCore(element, 0f, fromY, MotionTokens.Medium, delay, completed);
    }

    /// <summary>
    /// The entrance the library grid and the settings list both use.
    ///
    /// It is a separate name rather than a pair of call sites passing the same
    /// arguments, because "the two animations must have the same duration,
    /// curve and trigger" is a promise that only survives if there is one thing
    /// to change. Home and settings both call this; neither passes a distance,
    /// a duration or a curve.
    ///
    /// Deliberately the slower of the two permitted entrance durations. An
    /// entrance that a body of content arrives on reads better when it has room
    /// to settle, and the same 250 ms on both surfaces is what makes switching
    /// between them feel like one product.
    /// </summary>
    public static void PosterEnter(UIElement element, TimeSpan? delay = null, Action? completed = null)
    {
        SlideCore(element, 0f, MotionTokens.ContentEnterOffset, MotionTokens.Long, delay, completed);
    }

    /// <summary>
    /// Every entrance and every horizontal settle in this module funnels
    /// through here: fade from nothing, travel in from an offset, one curve,
    /// one duration. Callers reach it through a name that says what is arriving
    /// rather than how it moves.
    ///
    /// Both axes come from one animation so a move that has both components
    /// stays on a single curve rather than reading as two.
    /// </summary>
    private static void SlideCore(
        UIElement element,
        float fromX,
        float fromY,
        TimeSpan duration,
        TimeSpan? delay,
        Action? completed)
    {
        var visual = VisualOf(element);
        var compositor = visual.Compositor;
        var ease = MotionEasing.Enter(compositor);
        var d = delay ?? TimeSpan.Zero;

        if (!MotionGate.Resolve())
        {
            visual.StopAnimation(Opacity);
            visual.Opacity = 1f;
            ParkTranslated(visual, 0f, 0f);
            completed?.Invoke();
            return;
        }

        visual.StopAnimation(Opacity);
        visual.StopAnimation(Translation);
        ParkTranslated(visual, fromX, fromY);

        // Parked invisible, not merely transparent-by-animation.
        //
        // StopAnimation above restores the XAML default of opacity 1, and an
        // animation with a DelayTime does not touch its property until the
        // delay elapses. Without this line a staggered card is fully opaque and
        // displaced for its whole delay, then snaps to nothing and fades in -
        // which reads as a flicker on every card after the first, and is
        // exactly the bug the stagger exists to avoid.
        visual.Opacity = 0f;

        Batch(
            compositor,
            () =>
            {
                visual.StartAnimation(Opacity, Scalar(compositor, 0f, 1f, duration, ease, d));
                visual.StartAnimation(Translation, TranslateTo(visual, 0f, 0f, duration, ease, d));
            },
            completed);
    }

    /// <summary>
    /// Settles an element into place horizontally, starting from where it used
    /// to be.
    ///
    /// This is a connected transition for a layout change the app cannot
    /// animate itself. The settings pane is the case it exists for: switching
    /// the navigation pane between its expanded and icon-rail shapes is a
    /// <c>PaneDisplayMode</c> change, which is instant and not animatable, so
    /// the pane snaps and the content beside it is given the journey it would
    /// have had if the pane's width could be tweened.
    ///
    /// <paramref name="fromX"/> is the distance the element moved, measured
    /// rather than assumed - positive means it moved left. No fade: the element
    /// was already on screen and never left it.
    /// </summary>
    public static void SettleFrom(UIElement element, float fromX, TimeSpan? duration = null, Action? completed = null)
    {
        var visual = VisualOf(element);
        var compositor = visual.Compositor;
        var ease = MotionEasing.Move(compositor);
        var d = duration ?? MotionTokens.Medium;

        if (!MotionGate.Resolve())
        {
            visual.StopAnimation(Translation);
            ParkTranslated(visual, 0f, 0f);
            completed?.Invoke();
            return;
        }

        visual.StopAnimation(Translation);
        ParkTranslated(visual, fromX, 0f);

        Batch(
            compositor,
            () => visual.StartAnimation(Translation, TranslateTo(visual, 0f, 0f, d, ease, TimeSpan.Zero)),
            completed);
    }

    /// <summary>
    /// An element leaving: it fades out while travelling away, on the exit
    /// curve. The spec requires the fade - an exit curve on its own reads as a
    /// jump - so the two run together for 83 ms.
    /// </summary>
    public static void ContentExit(
        UIElement element,
        float toY = MotionTokens.DefaultEnterOffset,
        Action? completed = null)
    {
        var visual = VisualOf(element);
        var compositor = visual.Compositor;

        if (!MotionGate.Resolve())
        {
            visual.StopAnimation(Opacity);
            visual.Opacity = 0f;
            ParkTranslated(visual, 0f, toY);
            completed?.Invoke();
            return;
        }

        visual.StopAnimation(Opacity);
        visual.StopAnimation(Translation);

        Batch(
            compositor,
            () =>
            {
                visual.StartAnimation(
                    Opacity,
                    Scalar(compositor, visual.Opacity, 0f, MotionTokens.Short, MotionEasing.Linear(compositor), TimeSpan.Zero));
                visual.StartAnimation(
                    Translation,
                    TranslateTo(visual, 0f, toY, MotionTokens.Short, MotionEasing.Exit(compositor), TimeSpan.Zero));
            },
            completed);
    }

    // ------------------------------------------------------------------ moves

    /// <summary>
    /// An element already on screen travelling to a new offset. Move curve,
    /// 167 ms.
    /// </summary>
    public static void MoveTo(UIElement element, float dx, float dy, Action? completed = null)
    {
        var visual = VisualOf(element);
        var compositor = visual.Compositor;

        if (!MotionGate.Resolve())
        {
            ParkTranslated(visual, dx, dy);
            completed?.Invoke();
            return;
        }

        visual.StopAnimation(Translation);
        Batch(
            compositor,
            () => visual.StartAnimation(Translation, TranslateTo(visual, dx, dy, MotionTokens.Medium, MotionEasing.Move(compositor), TimeSpan.Zero)),
            completed);
    }

    // ------------------------------------------------------- expand / collapse

    /// <summary>
    /// A detail panel opening under a row.
    ///
    /// Height is a layout property and composition cannot animate it, so the
    /// layout change happens first and reads as correct because the content
    /// inside slides and fades into the space that just opened.
    ///
    /// 250 ms rather than 167: at 167 the panel had arrived before the eye had
    /// finished travelling to it, which is what made an expanding row read as
    /// having simply appeared. The distance stays short because the panel is
    /// only a few lines tall - at a content entrance's 48 the text would appear
    /// to overshoot the row it belongs to.
    /// </summary>
    public static void RowReveal(UIElement element, float fromY = MotionTokens.RowRevealOffset, Action? completed = null)
    {
        SlideCore(element, 0f, fromY, MotionTokens.Long, null, completed);
    }

    /// <summary>The closing half of <see cref="RowReveal"/>. Exit curve, 83 ms, paired with a fade.</summary>
    public static void RowCollapse(UIElement element, float toY = MotionTokens.RowRevealOffset, Action? completed = null)
    {
        ContentExit(element, toY, completed);
    }

    // -------------------------------------------------------------- dropdowns

    /// <summary>
    /// Gives a dropdown control's popup the slow expand defined by
    /// <see cref="DropdownEnter"/> instead of the framework's own.
    ///
    /// No template is replaced. The control's own popup is found in its
    /// realised template tree, its built-in open transition is cleared, and the
    /// expansion is re-driven from the popup's Opened event. Copying WinUI's
    /// control template to change one transition inside it would mean owning a
    /// few hundred lines of template that has to be re-checked against every
    /// framework update; this touches the one property that matters and leaves
    /// the rest of the control alone.
    ///
    /// Idempotent: safe to call from a control's constructor as well as from
    /// Loaded, and calling it twice cannot wire the popup twice.
    /// </summary>
    public static void SoftenDropdown(Control dropdown)
    {
        void Attach()
        {
            if (FindTemplatePart<Popup>(dropdown) is not { } popup
                || popup.Tag as string == AttachedMarker)
            {
                return;
            }

            popup.Tag = AttachedMarker;

            // The framework's own open animation exposes no duration, so it has
            // to go rather than be retimed.
            popup.ChildTransitions = null;

            popup.Opened += (_, _) =>
            {
                if (popup.Child is UIElement content)
                {
                    DropdownEnter(content);
                }
            };
        }

        if (dropdown.IsLoaded)
        {
            Attach();
        }
        else
        {
            dropdown.Loaded += (_, _) => Attach();
        }
    }

    private const string AttachedMarker = "mpv-motion-dropdown";

    /// <summary>
    /// Depth-first search of a control's template tree for a part of the given
    /// type.
    ///
    /// Depth-bounded because a template tree is walked on the UI thread and a
    /// malformed one should give up rather than spin, and because the part being
    /// looked for is always near the top of a template in practice.
    /// </summary>
    private static T? FindTemplatePart<T>(DependencyObject root, int depth = 0)
        where T : DependencyObject
    {
        if (depth > 12)
        {
            return null;
        }

        int count;
        try
        {
            count = VisualTreeHelper.GetChildrenCount(root);
        }
        catch
        {
            // Some parts report no children until they are realised.
            return null;
        }

        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            if (FindTemplatePart<T>(child, depth + 1) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// Grows a dropdown's own content into place: a small scale up from the top
    /// edge plus a fade, on the enter curve, at the slowest of the three
    /// permitted durations.
    ///
    /// This exists because the framework's ComboBox popup animation runs at a
    /// fixed speed that is not configurable - <c>PopupThemeTransition</c>
    /// exposes no duration - and at that speed the expansion reads as a snap.
    /// So the popup's own transition is switched off where it is applied (see
    /// <c>OptionStringListControl</c>) and this takes over: the shape of the
    /// motion is kept - grow downward out of the control the popup belongs to -
    /// while the timing comes back under this module's control.
    ///
    /// The scale is anchored to the top centre. Scaling about the element's
    /// default origin would grow it down and to the right, away from its
    /// anchor.
    /// </summary>
    public static void DropdownEnter(UIElement content, float fromScale = MotionTokens.DropdownFromScale, Action? completed = null)
    {
        var visual = VisualOf(content);
        var compositor = visual.Compositor;
        var ease = MotionEasing.Enter(compositor);
        var size = visual.Size;

        // A popup whose content has not been laid out has no meaningful centre
        // to grow from, and growing about the origin would look like the panel
        // flying in from the corner. Showing it outright is the honest fallback.
        if (!MotionGate.Resolve() || size.X <= 0f || size.Y <= 0f)
        {
            visual.StopAnimation(Scale);
            visual.StopAnimation(Opacity);
            visual.CenterPoint = Vector3.Zero;
            visual.Scale = Vector3.One;
            visual.Opacity = 1f;
            completed?.Invoke();
            return;
        }

        visual.StopAnimation(Scale);
        visual.StopAnimation(Opacity);
        visual.CenterPoint = new Vector3(size.X / 2f, 0f, 0f);

        Batch(
            compositor,
            () =>
            {
                var grow = compositor.CreateVector3KeyFrameAnimation();
                grow.Duration = MotionTokens.Long;
                grow.InsertKeyFrame(0f, new Vector3(fromScale, fromScale, 1f));
                grow.InsertKeyFrame(1f, Vector3.One, ease);
                visual.StartAnimation(Scale, grow);

                visual.StartAnimation(Opacity, Scalar(compositor, 0f, 1f, MotionTokens.Long, ease, TimeSpan.Zero));
            },
            () =>
            {
                // Released rather than left holding the final value: a filled
                // Scale would scale the popup's content again on every later
                // open, compounding with each use.
                visual.StopAnimation(Scale);
                visual.StopAnimation(Opacity);
                visual.CenterPoint = Vector3.Zero;
                visual.Scale = Vector3.One;
                visual.Opacity = 1f;
                completed?.Invoke();
            });
    }

    // -------------------------------------------------------- section changes

    /// <summary>
    /// A content surface being replaced - the settings browser swapping
    /// sections, a library filter changing.
    ///
    /// <paramref name="direction"/> is +1 when the new content should read as
    /// arriving from the right and -1 from the left, so moving deeper and
    /// moving back are distinguishable. Enter curve, 167 ms, and the same
    /// sliding core every other entrance uses.
    /// </summary>
    public static void SectionSwitch(UIElement element, int direction = 1, Action? completed = null)
    {
        var fromX = direction >= 0 ? 16f : -16f;
        SlideCore(element, fromX, 0f, MotionTokens.Medium, null, completed);
    }

    // --------------------------------------------------------- state feedback

    /// <summary>
    /// A short settle on a control whose value just changed, so the row
    /// acknowledges the commit. Move curve, 167 ms.
    ///
    /// It scales about the element's own centre and puts the centre point back
    /// to the origin afterwards: a leftover centre point would offset every
    /// later XAML render transform on that element.
    /// </summary>
    public static void ControlStateChange(UIElement element)
    {
        var visual = VisualOf(element);
        var compositor = visual.Compositor;

        if (!MotionGate.Resolve())
        {
            visual.StopAnimation(Scale);
            visual.Scale = Vector3.One;
            visual.CenterPoint = Vector3.Zero;
            return;
        }

        visual.StopAnimation(Scale);

        var size = visual.Size;
        if (size.X <= 0f || size.Y <= 0f)
        {
            // Not laid out yet - a pulse about an unknown centre is worse than
            // no pulse.
            visual.Scale = Vector3.One;
            return;
        }

        visual.CenterPoint = new Vector3(size.X / 2f, size.Y / 2f, 0f);

        var pulse = compositor.CreateVector3KeyFrameAnimation();
        pulse.Duration = MotionTokens.Medium;
        pulse.InsertKeyFrame(0f, Vector3.One);
        pulse.InsertKeyFrame(0.4f, new Vector3(0.98f, 0.98f, 1f), MotionEasing.Move(compositor));
        pulse.InsertKeyFrame(1f, Vector3.One, MotionEasing.Move(compositor));

        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        visual.StartAnimation(Scale, pulse);
        batch.Completed += (_, _) =>
        {
            visual.StopAnimation(Scale);
            visual.Scale = Vector3.One;
            visual.CenterPoint = Vector3.Zero;
        };
        batch.End();
    }

    // ------------------------------------------------------ player control bar

    /// <summary>
    /// The player control bar sliding up with its gradient. Move curve, 167 ms.
    /// </summary>
    public static void PanelReveal(
        UIElement panel,
        UIElement? gradient = null,
        float fromY = MotionTokens.PlayerPanelOffset,
        Action? completed = null)
    {
        var visual = VisualOf(panel);
        var compositor = visual.Compositor;
        var ease = MotionEasing.Move(compositor);
        var gradientVisual = gradient is null ? null : VisualOf(gradient);

        if (!MotionGate.Resolve())
        {
            visual.StopAnimation(Opacity);
            visual.Opacity = 1f;
            ParkTranslated(visual, 0f, 0f);
            if (gradientVisual is not null)
            {
                gradientVisual.StopAnimation(Opacity);
                gradientVisual.Opacity = 1f;
            }

            completed?.Invoke();
            return;
        }

        visual.StopAnimation(Opacity);
        visual.StopAnimation(Translation);
        gradientVisual?.StopAnimation(Opacity);
        ParkTranslated(visual, 0f, fromY);

        Batch(
            compositor,
            () =>
            {
                visual.StartAnimation(Opacity, Scalar(compositor, 0f, 1f, MotionTokens.Medium, ease, TimeSpan.Zero));
                visual.StartAnimation(Translation, TranslateTo(visual, 0f, 0f, MotionTokens.Medium, ease, TimeSpan.Zero));
                gradientVisual?.StartAnimation(Opacity, Scalar(compositor, 0f, 1f, MotionTokens.Medium, ease, TimeSpan.Zero));
            },
            completed);
    }

    /// <summary>
    /// The closing half of <see cref="PanelReveal"/>: exit curve, 83 ms, with
    /// the fade the spec requires to travel alongside it. Shorter than the
    /// reveal on purpose - a dismissal should feel quicker than an
    /// introduction, and the bar is on its way out of the user's attention.
    /// </summary>
    public static void PanelConceal(
        UIElement panel,
        UIElement? gradient = null,
        float toY = MotionTokens.PlayerPanelOffset,
        Action? completed = null)
    {
        var visual = VisualOf(panel);
        var compositor = visual.Compositor;
        var exitEase = MotionEasing.Exit(compositor);
        var linear = MotionEasing.Linear(compositor);
        var gradientVisual = gradient is null ? null : VisualOf(gradient);

        if (!MotionGate.Resolve())
        {
            visual.StopAnimation(Opacity);
            visual.Opacity = 0f;
            ParkTranslated(visual, 0f, toY);
            if (gradientVisual is not null)
            {
                gradientVisual.StopAnimation(Opacity);
                gradientVisual.Opacity = 0f;
            }

            completed?.Invoke();
            return;
        }

        visual.StopAnimation(Opacity);
        visual.StopAnimation(Translation);
        gradientVisual?.StopAnimation(Opacity);

        Batch(
            compositor,
            () =>
            {
                visual.StartAnimation(Opacity, Scalar(compositor, visual.Opacity, 0f, MotionTokens.Short, linear, TimeSpan.Zero));
                visual.StartAnimation(Translation, TranslateTo(visual, 0f, toY, MotionTokens.Short, exitEase, TimeSpan.Zero));
                gradientVisual?.StartAnimation(Opacity, Scalar(compositor, gradientVisual.Opacity, 0f, MotionTokens.Short, linear, TimeSpan.Zero));
            },
            completed);
    }

    // --------------------------------------------------------------- utilities

    /// <summary>
    /// Snaps an element back to its resting state without animating. Callers
    /// use it when an interaction is cut short, so the next entry starts from a
    /// known place instead of inheriting a half-finished translation.
    /// </summary>
    public static void Reset(UIElement element)
    {
        var visual = VisualOf(element);
        visual.StopAnimation(Opacity);
        visual.StopAnimation(Scale);
        visual.CenterPoint = Vector3.Zero;
        visual.Scale = Vector3.One;
        visual.Opacity = 1f;
        ParkTranslated(visual, 0f, 0f);
    }
}
