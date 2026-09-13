using Microsoft.UI.Xaml;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.Settings.Controls;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.UI;

namespace mpv_winui.Modules.Player
{
    /// <summary>
    /// Overlay panel show/hide animation (windowed and PiP paths).
    /// Split from PlayerControl.xaml.cs (audit C5); content is byte-identical.
    /// </summary>
    public sealed partial class PlayerControl
    {
        // Shared panel animation timing. Windowed show/hide use 180/150ms;
        // overlay (fullscreen/PiP) intentionally keeps 180ms for both
        // directions so the bar feels symmetric (see AGENTS.md).
        private const double PanelShowMs = 180;
        private const double PanelHideMs = 150;

        private void StopPanelAnimations()
        {
            _panelGridVisual?.StopAnimation("Opacity");
            _panelGradientVisual?.StopAnimation("Opacity");
            if (_panelGridVisual is not null)
            {
                _panelGridVisual.Opacity = 1f;
            }
            if (_panelGradientVisual is not null)
            {
                _panelGradientVisual.Opacity = 1f;
            }
            _panelAnimationTimer.Stop();
            _panelAnimationTimer.Tick -= PanelAnimationTick;
            _showStoryboard?.Stop();
            _showStoryboard = null;
            _hideStoryboard?.Stop();
            _hideStoryboard = null;
            _panelAnimating = false;
        }

        /// <summary>
        /// Overlay tween: the main window uses a XAML Storyboard on the render
        /// transform. PiP (a second top-level window) cannot use a Storyboard
        /// (crashes the compositor) and must not animate the composition
        /// `Offset` (layout owns it, which strands the bar after zoom), so it
        /// uses a short DispatcherTimer tween on the XAML values instead.
        /// </summary>
        private void StartPanelAnimation(bool show)
        {
            if (_panelAnimating && _panelAnimationShow == show)
            {
                return;
            }

            _panelAnimating = true;
            _panelAnimationShow = show;

            if (!_isPiPHost)
            {
                StartMainWindowPanelAnimation(show);
                return;
            }

            StartPiPPanelAnimation(show);
        }

        /// <summary>
        /// Main-window overlay tween: XAML Storyboard on the render transform.
        /// TranslateTransform is layout-independent, so a resize/zoom while the
        /// bar is hidden cannot leave it stranded above the bottom edge (the
        /// composition Visual.Offset path snaps back to a stale layout value
        /// when the grid is Collapsed).
        /// </summary>
        private void StartMainWindowPanelAnimation(bool show)
        {
            // Stop only the in-flight storyboard. Do not call
            // StopPanelAnimations() here: it clears _panelAnimating, which
            // defeats the same-direction re-entry guard in StartPanelAnimation
            // and lets every pointer move restart the pop-out animation
            // (the bar visibly twitches).
            _showStoryboard?.Stop();
            _showStoryboard = null;
            _hideStoryboard?.Stop();
            _hideStoryboard = null;

            if (show)
            {
                ControlPanelGrid.Visibility = Visibility.Visible;
            }

            ControlPanelGradient.Opacity = 1;
            ControlPanelGrid.Opacity = 1;
            TranslateVertical.Y = 0;

            var storyboard = new Storyboard
            {
                Duration = TimeSpan.FromMilliseconds(PanelShowMs),
            };
            AddPanelAnimation(storyboard, "Opacity", show ? 0 : 1, show ? 1 : 0, PanelShowMs, ControlPanelGrid);
            AddPanelAnimation(storyboard, "Opacity", show ? 0 : 1, show ? 1 : 0, PanelShowMs, ControlPanelGradient);
            AddPanelAnimation(storyboard, "(UIElement.RenderTransform).(TranslateTransform.Y)", show ? 48 : 0, show ? 0 : 48, PanelShowMs, ControlPanelGrid);
            storyboard.Completed += (_, _) => PanelAnimationCompleted(show);
            storyboard.Begin();

            if (show)
            {
                _showStoryboard = storyboard;
            }
            else
            {
                _hideStoryboard = storyboard;
            }
        }

        private void StartPiPPanelAnimation(bool show)
        {
            if (show)
            {
                ControlPanelGrid.Visibility = Visibility.Visible;
            }

            // Composition opacity multiplies the XAML opacity, so keep the
            // XAML base at 1 and animate only the composition value; the 16ms
            // timer drives just the slide (TranslateTransform), which is
            // layout-independent. This is smoother than tweening three XAML
            // properties per timer tick.
            ControlPanelGradient.Opacity = 1;
            ControlPanelGrid.Opacity = 1;
            TranslateVertical.Y = show ? 48 : 0;

            EnsurePanelVisuals();
            if (_panelCompositor is not null && _panelGridVisual is not null && _panelGradientVisual is not null)
            {
                _panelGridVisual.StopAnimation("Opacity");
                _panelGradientVisual.StopAnimation("Opacity");

                var ease = _panelCompositor.CreateCubicBezierEasingFunction(
                    new System.Numerics.Vector2(0.215f, 0.61f),
                    new System.Numerics.Vector2(0.355f, 1f));
                var opacity = _panelCompositor.CreateScalarKeyFrameAnimation();
                opacity.Duration = TimeSpan.FromMilliseconds(PanelShowMs);
                opacity.InsertKeyFrame(0f, show ? 0f : 1f);
                opacity.InsertKeyFrame(1f, show ? 1f : 0f, ease);
                _panelGridVisual.StartAnimation("Opacity", opacity);
                _panelGradientVisual.StartAnimation("Opacity", opacity);
            }

            _panelAnimationStart = Environment.TickCount64;
            _panelAnimationTimer.Tick -= PanelAnimationTick;
            _panelAnimationTimer.Tick += PanelAnimationTick;
            _panelAnimationTimer.Start();
        }

        private void PanelAnimationTick(object? sender, object e)
        {
            const double durationMs = PanelShowMs;
            var elapsed = Environment.TickCount64 - _panelAnimationStart;
            var t = Math.Clamp(elapsed / durationMs, 0, 1);
            var eased = 1 - Math.Pow(1 - t, 3); // ease-out cubic

            TranslateVertical.Y = _panelAnimationShow ? 48 * (1 - eased) : 48 * eased;

            if (t >= 1)
            {
                _panelAnimationTimer.Stop();
                PanelAnimationCompleted(_panelAnimationShow);
            }
        }

        private void EnsurePanelVisuals()
        {
            if (_panelGridVisual is null)
            {
                _panelGridVisual = ElementCompositionPreview.GetElementVisual(ControlPanelGrid);
                _panelGradientVisual = ElementCompositionPreview.GetElementVisual(ControlPanelGradient);
                _panelCompositor = _panelGridVisual.Compositor;
            }
        }

        private void PanelAnimationCompleted(bool show)
        {
            _panelAnimating = false;
            _panelGridVisual?.StopAnimation("Opacity");
            _panelGradientVisual?.StopAnimation("Opacity");
            if (_panelGridVisual is not null)
            {
                _panelGridVisual.Opacity = 1f;
            }
            if (_panelGradientVisual is not null)
            {
                _panelGradientVisual.Opacity = 1f;
            }

            if (show)
            {
                ControlPanelGradient.Opacity = 1;
                ControlPanelGrid.Opacity = 1;
                TranslateVertical.Y = 0;
                _controlPanelIsVisible = true;
            }
            else
            {
                ControlPanelGrid.Visibility = Visibility.Collapsed;
                ControlPanelGrid.Opacity = 1;
                TranslateVertical.Y = 0;
                // Keep the gradient mounted and hit-testable. WinUI skips
                // hit-testing for elements at exactly zero opacity, so
                // leave a barely visible floor value.
                ControlPanelGradient.Opacity = 0.01;
                _controlPanelIsVisible = false;
            }
        }

        private void AddPanelAnimation(
            Storyboard storyboard,
            string property,
            double from,
            double to,
            double milliseconds,
            FrameworkElement? target = null)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromMilliseconds(milliseconds),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(animation, target ?? ControlPanelGrid);
            Storyboard.SetTargetProperty(animation, property);
            storyboard.Children.Add(animation);
        }
    }
}
