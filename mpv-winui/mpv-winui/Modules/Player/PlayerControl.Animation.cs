using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Animation;
using mpv_winui.Infrastructure.Motion;
using System;
using System.Numerics;
using Windows.Foundation;

namespace mpv_winui.Modules.Player
{
    /// <summary>
    /// Overlay panel show/hide animation, shared by the windowed bar and the
    /// PiP bar.
    ///
    /// This used to be two implementations of the same motion: a XAML Storyboard
    /// on the main window's render transform, and a 16 ms DispatcherTimer tween
    /// driving XAML values for PiP. Both had a reason at the time - PiP is a
    /// second top-level window where a Storyboard crashes the compositor, and
    /// neither could animate <c>Visual.Offset</c> without the bar being stranded
    /// by a later layout pass.
    ///
    /// Neither reason survives. The slide now rides the "Translation"
    /// property-set entry, which layout does not own and which behaves the same
    /// in any window, so there is one code path, it runs on the compositor, and
    /// it uses the official durations and curves instead of the local 180/150 ms
    /// pair that used to live in this file.
    ///
    /// Show and hide are deliberately asymmetric: 167 ms on the move curve to
    /// present, 83 ms on the exit curve to dismiss. A dismissal should feel
    /// quicker than an introduction, and the exit curve is only ever used with
    /// the fade that travels alongside it.
    ///
    /// That is the slide. The bar's host - the row the video area shares with
    /// it, which is Auto-sized in windowed mode - also moves now, so the video
    /// area grows into the space instead of jumping when the row is dropped.
    /// That one cannot ride the compositor (a height is a layout property) and
    /// so is a dependent XAML animation, with two consequences worth knowing:
    /// it runs after the slide rather than with it, and it can only use an
    /// approximate easing, because this project's WinUI projection exposes no
    /// way to write a cubic bezier on the XAML side (CubicEase ships without
    /// the control-point members here). Its durations are still the official
    /// tiers, which is the part that carries the Fluent intent.
    /// </summary>
    public sealed partial class PlayerControl
    {
        private void StopPanelAnimations()
        {
            MotionBehaviors.Reset(ControlPanelGrid);
            ResetGradientVisual();
            // The host's height animation is a XAML-side one, so nothing about
            // resetting the composition visual stops it. Left running it would
            // still fire its Completed and collapse the host mid-show.
            StopPanelHostAnimation();
            _panelAnimating = false;
        }

        /// <summary>
        /// Composition opacity multiplies XAML opacity, so the gradient's XAML
        /// value is managed separately (it parks at 0.01 when hidden, to stay
        /// hit-testable) while the composition value is always returned to 1.
        /// </summary>
        private void ResetGradientVisual()
        {
            var visual = ElementCompositionPreview.GetElementVisual(ControlPanelGradient);
            visual.StopAnimation("Opacity");
            visual.Opacity = 1f;
        }

        /// <summary>
        /// The host's height, as measured just before it was taken down, so the
        /// way back up has a number to animate to. Windowed mode leaves the
        /// host Auto-sized, and neither end of a height animation can be Auto.
        /// </summary>
        private double _panelHostHeight;

        /// <summary>
        /// The in-flight host-height animation, kept so it can be stopped.
        /// Without a handle, a show starting inside the 83 ms of a hide would
        /// let the hide's Completed callback land mid-show and collapse the host
        /// under it.
        /// </summary>
        private Storyboard? _panelHostStoryboard;

        /// <summary>
        /// Whether the video area may ride the bar's transition at all.
        ///
        /// Never while the user is dragging or resizing the window. That path
        /// has to lay out exactly and immediately - MainWindow publishes
        /// IsDragResizing for exactly that contract (set between
        /// WM_ENTERSIZEMOVE and WM_EXITSIZEMOVE) - and an animated height
        /// fighting it per frame is the conflict this transition exists to
        /// avoid repeating. Conservative on purpose: when in doubt, snap.
        /// </summary>
        private static bool ShouldAnimatePanelHost() => !MainWindow.IsDragResizing;

        /// <summary>
        /// The durations a XAML-side animation is supposed to quote. MotionTokens
        /// holds the same numbers for the composition side; XAML cannot read
        /// those, so these come from the framework's own keys of the matching
        /// name, which is what that module asks XAML to use.
        /// </summary>
        private static TimeSpan FrameworkDuration(string key, TimeSpan fallback) =>
            Application.Current.Resources.TryGetValue(key, out var value) && value is Duration duration
                ? duration.TimeSpan
                : fallback;

        /// <summary>
        /// One of the motion module's curves, as the two control points a XAML
        /// spline wants.
        ///
        /// XAML cannot hand these to a plain easing class - CubicEase in this
        /// projection has no control points at all (measured: it fails to
        /// compile, CS0117) - but SplineDoubleKeyFrame takes a KeySpline, and a
        /// KeySpline's control points mean exactly what the composition side
        /// means by them. So the host gets the official curve itself rather
        /// than an approximation; only the container differs.
        ///
        /// The Mode is kept as documentation of which half of the pair a caller
        /// means; a raw spline has no equivalent knob, which is the point.
        /// </summary>
        private static (Vector2 Control1, Vector2 Control2) Curve(Vector2 control1, Vector2 control2, EasingMode mode)
        {
            _ = mode;
            return (control1, control2);
        }

        /// <summary>
        /// Stops the host-height animation and leaves the height unowned, so the
        /// Auto sizing the mode setter relies on comes back.
        /// </summary>
        private void StopPanelHostAnimation()
        {
            _panelHostStoryboard?.Stop();
            _panelHostStoryboard = null;
        }

        /// <summary>
        /// The video area's share of the hide/show transition.
        ///
        /// The bar's own slide is composition-side and was never the problem.
        /// What jumped was this: in windowed mode the bar's host is Auto-sized,
        /// so collapsing it right after the slide took its whole height out of
        /// the page in a single frame and the video area snapped wider.
        ///
        /// Height is a layout property, so this cannot ride the compositor the
        /// way the slide does - it runs as a dependent XAML animation, which the
        /// framework drives one frame at a time. That per-frame layout is the
        /// cost, and it is why the callers gate this to the bar's own
        /// auto-hide/show and let everything else snap.
        ///
        /// The curve is the module's own, carried on a KeySpline rather than an
        /// easing class, so it is exact - see Curve above.
        /// </summary>
        private void AnimatePanelHostTo(
            double to,
            TimeSpan duration,
            (Vector2 Control1, Vector2 Control2) curve,
            Action? completed)
        {
            StopPanelHostAnimation();

            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new SplineDoubleKeyFrame
            {
                KeyTime = KeyTime.FromTimeSpan(duration),
                Value = to,
                KeySpline = new KeySpline
                {
                    ControlPoint1 = new Point(curve.Control1.X, curve.Control1.Y),
                    ControlPoint2 = new Point(curve.Control2.X, curve.Control2.Y),
                },
            });

            var storyboard = new Storyboard();
            Storyboard.SetTarget(animation, ControlPanelGradient);
            Storyboard.SetTargetProperty(animation, "Height");
            storyboard.Children.Add(animation);

            if (completed is not null)
            {
                storyboard.Completed += (_, _) =>
                {
                    _panelHostStoryboard = null;
                    completed();
                };
            }

            _panelHostStoryboard = storyboard;
            storyboard.Begin();
        }

        /// <summary>
        /// Windowed and overlay both come through here.
        /// </summary>
        private void StartPanelAnimation(bool show)
        {
            if (_panelAnimating && _panelAnimationShow == show)
            {
                return;
            }

            _panelAnimating = true;
            _panelAnimationShow = show;

            if (show)
            {
                ControlPanelGrid.Visibility = Visibility.Visible;
            }

            // The XAML opacity is the REST value and the composition value does
            // the fading. Driving both would square the ramp. It is not always 1:
            // with nothing playing the bar rests dimmed, and writing 1 here is what
            // used to erase that state one frame after SetTransportEnabled set it.
            ApplyTransportRestOpacity();
            ControlPanelGradient.Opacity = 1;

            // The video area moves on the same beat as the slide, not after it:
            // the host's height runs 0 -> its measured height while the bar
            // slides in (167 ms, move curve) and
            // measured height -> 0 while the bar slides out (83 ms, exit curve).
            // Each finishing callback releases the height back to Auto, so the
            // steady state is the one the mode setter expects.
            //
            // The height is sampled on the way down because windowed mode leaves
            // the host Auto-sized and Auto is not an endpoint anything animates
            // from. Sampling on the way down means a bar whose content changed
            // while it was hidden restores to the old figure and settles the
            // last few pixels a frame later; measuring fresh instead would need
            // a layout pass between showing the host and starting this, which
            // costs a full frame of the bar at full height - worse.
            if (!_overlayMode && ShouldAnimatePanelHost())
            {
                if (show)
                {
                    if (_panelHostHeight > 0)
                    {
                        ControlPanelGradient.Visibility = Visibility.Visible;
                        ControlPanelGradient.Height = 0;
                        AnimatePanelHostTo(
                            _panelHostHeight,
                            FrameworkDuration(MotionTokens.FrameworkDurationMediumKey, MotionTokens.Medium),
                            Curve(MotionTokens.MoveControl1, MotionTokens.MoveControl2, EasingMode.EaseOut),
                            () => ControlPanelGradient.Height = double.NaN);
                    }
                }
                else if (ControlPanelGradient.ActualHeight > 0)
                {
                    _panelHostHeight = ControlPanelGradient.ActualHeight;
                    AnimatePanelHostTo(
                        0,
                        FrameworkDuration(MotionTokens.FrameworkDurationShortKey, MotionTokens.Short),
                        Curve(MotionTokens.ExitControl1, MotionTokens.ExitControl2, EasingMode.EaseIn),
                        () =>
                        {
                            ControlPanelGradient.Visibility = Visibility.Collapsed;
                            ControlPanelGradient.Height = double.NaN;
                        });
                }
            }

            if (show)
            {
                MotionBehaviors.PanelReveal(
                    ControlPanelGrid,
                    ControlPanelGradient,
                    MotionTokens.PlayerPanelOffset,
                    () => PanelAnimationCompleted(true));
            }
            else
            {
                MotionBehaviors.PanelConceal(
                    ControlPanelGrid,
                    ControlPanelGradient,
                    MotionTokens.PlayerPanelOffset,
                    () => PanelAnimationCompleted(false));
            }
        }

        private void PanelAnimationCompleted(bool show)
        {
            _panelAnimating = false;
            MotionBehaviors.Reset(ControlPanelGrid);
            ResetGradientVisual();

            if (show)
            {
                ControlPanelGradient.Opacity = 1;
                ApplyTransportRestOpacity();
                _controlPanelIsVisible = true;
            }
            else
            {
                ControlPanelGrid.Visibility = Visibility.Collapsed;
                ApplyTransportRestOpacity();

                // Windowed mode: take the whole host border down, not just the
                // grid - the Auto-sized page row would otherwise keep a white
                // (window background) strip below the video after the bar hides.
                // Overlay mode keeps it mounted: there it is the fade mask the
                // video dissolves into, not a background box.
                //
                // The animated path started in StartPanelAnimation and collapses
                // the host from its own completion callback, so this is the
                // fallback for when nothing is in flight (a drag, or a host with
                // nothing to measure) - there, correctness means snapping.
                if (!_overlayMode && _panelHostStoryboard is null)
                {
                    ControlPanelGradient.Visibility = Visibility.Collapsed;
                }

                // Keep the gradient mounted and hit-testable. WinUI skips
                // hit-testing for elements at exactly zero opacity, so leave a
                // barely visible floor value.
                ControlPanelGradient.Opacity = 0.01;
                _controlPanelIsVisible = false;
            }
        }
    }
}
