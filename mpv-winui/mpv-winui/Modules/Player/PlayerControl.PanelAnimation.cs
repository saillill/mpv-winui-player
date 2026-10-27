using Microsoft.UI.Xaml;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using mpv_winrt;
using mpv_winui.Modules.Common.View;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Domains.Settings.Controls;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.UI;
namespace mpv_winui.Modules.Player
{
    /// <summary>
    /// Overlay mode plus the gradient panel show/hide animation state machine.
    /// </summary>
    public sealed partial class PlayerControl
    {
            public bool IsVisible()
            {
                return _controlPanelIsVisible;
            }
    
            public void ShowControlPanel()
            {
                if (_overlayMode)
                {
                    _hideDelayTimer.Stop();
                    // Re-show even while a hide animation is still running: the
                    // visible flag only flips when the animation completes, so
                    // checking it alone would swallow the re-show and make the bar
                    // pop out only after the hide finished (felt like flicker).
                    if (!_controlPanelIsVisible || (_panelAnimating && !_panelAnimationShow))
                    {
                        StartPanelAnimation(true);
                        OnPanelVisibleChanged?.Invoke(false);
                    }
                    return;
                }
    
                if (!_controlPanelIsVisible)
                {
                    StopPanelAnimations();
                    ControlPanelGradient.Visibility = Visibility.Visible;
                    ControlPanelGrid.Visibility = Visibility.Visible;
                    _controlPanelIsVisible = true;
                    StartPanelAnimation(true);
                }
    
                OnPanelVisibleChanged?.Invoke(false);
            }
    
            public void HideControlPanel()
            {
                if (_overlayMode)
                {
                    if (_volumeFlyoutOpen)
                    {
                        // The user is interacting with the volume flyout above
                        // the bar; retracting under their slider feels broken.
                        return;
                    }
                    // Debounce: a transient exit (pointer on the mask edge) must
                    // not start and restart the retract animation repeatedly.
                    _hideDelayTimer.Tick -= HideDelayTimer_Tick;
                    _hideDelayTimer.Tick += HideDelayTimer_Tick;
                    _hideDelayTimer.Start();
                    return;
                }
    
                if (_controlPanelIsVisible)
                {
                    StopPanelAnimations();
                    _controlPanelIsVisible = false;
                    StartPanelAnimation(false);
                }
    
                OnPanelVisibleChanged?.Invoke(true);
            }
    
            public void SetOverlayMode(bool overlay)
            {
                if (_overlayMode == overlay)
                {
                    return;
                }
                _overlayMode = overlay;
    
                if (overlay)
                {
                    _lastOverlayActivity = new(double.NaN, double.NaN);
                    // Fullscreen mask: the gradient shell extends above the bar so
                    // the video fades into the controls, matching the mpv-lazy /
                    // ModernX bottom fade (120px, ~90% black at the base).
                    ControlPanelGradient.Height = 120;
                    // PiP-style overlay: no solid panel box, only the gradient
                    // fade. The dark element theme keeps glyphs white so they
                    // stay readable on the gradient without a visible border.
                    ControlPanelGrid.Background = null;
                    ControlPanelGrid.RequestedTheme = ElementTheme.Dark;
                    ControlPanelGradient.Background = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(0, 1),
                        GradientStops =
                        {
                            new GradientStop { Offset = 0, Color = Color.FromArgb(0, 0, 0, 0) },
                            new GradientStop { Offset = 1, Color = Color.FromArgb(230, 0, 0, 0) },
                        },
                    };
                    ControlPanelGradient.Opacity = 1;
                    ApplyTransportRestOpacity();
                    TranslateVertical.Y = 0;
                    ControlPanelGrid.Visibility = Visibility.Visible;
                    _controlPanelIsVisible = true;
                    ShowControlPanel();
                    RestartOverlayIdleTimer();
                }
                else
                {
                    _lastOverlayActivity = new(double.NaN, double.NaN);
                    // Windowed mode: the shell hugs the bar content (Auto) so no
                    // extra background strip remains between the video and the
                    // controls.
                    ControlPanelGradient.Height = double.NaN;
                    ControlPanelGradient.Background = null;
                    ControlPanelGradient.Opacity = 1;
                    ControlPanelGrid.Background = null;
                    ControlPanelGrid.RequestedTheme = ElementTheme.Default;
                    _overlayIdleTimer.Stop();
                    // Reset any mid-animation state: leaving overlay while the
                    // bar was fading out used to leave it at partial opacity or
                    // collapsed, which the user saw as an incompletely expanded
                    // windowed bar after double-click fullscreen.
                    StopPanelAnimations();
                    ControlPanelGradient.Visibility = Visibility.Visible;
                    ApplyTransportRestOpacity();
                    TranslateVertical.Y = 0;
                    ControlPanelGrid.Visibility = Visibility.Visible;
                    _controlPanelIsVisible = true;
                    ShowControlPanel();
                }
            }
    
            /// <summary>
            /// Activity-based overlay bar: any pointer movement over the player
            /// (video, mask or bar) expands the bar and restarts the idle timer;
            /// when the mouse stops moving, the bar retracts.
            /// </summary>
            private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
            {
                if (!_overlayMode)
                {
                    return;
                }
    
                NotifyOverlayPointerActivity(e.GetCurrentPoint(RootGrid).Position);
            }
    
            /// <summary>Called on pointer activity from the video area as well.</summary>
            public void NotifyOverlayPointerActivity(Point position)
            {
                if (!_overlayMode)
                {
                    return;
                }
    
                // Ignore sub-threshold jitter / duplicate events: they used to
                // restart the idle timer repeatedly, so the bar stayed visible for
                // many seconds after the mouse actually stopped moving.
                if (!double.IsNaN(_lastOverlayActivity.X)
                    && Math.Abs(position.X - _lastOverlayActivity.X) < OverlayMoveThreshold
                    && Math.Abs(position.Y - _lastOverlayActivity.Y) < OverlayMoveThreshold)
                {
                    return;
                }
    
                _lastOverlayActivity = position;
                ShowControlPanel();
                RestartOverlayIdleTimer();
            }
    
            private void RestartOverlayIdleTimer()
            {
                _overlayIdleTimer.Stop();
                _overlayIdleTimer.Start();
            }
    
            private void OverlayIdleTimer_Tick(object? sender, object e)
            {
                _overlayIdleTimer.Stop();
                if (_overlayMode && (_controlPanelIsVisible || _panelAnimating))
                {
                    StartPanelAnimation(false);
                    OnPanelVisibleChanged?.Invoke(true);
                }
            }
    
            private void HideDelayTimer_Tick(object? sender, object e)
            {
                _hideDelayTimer.Stop();
                if (_overlayMode && (_controlPanelIsVisible || _panelAnimating))
                {
                    StartPanelAnimation(false);
                    OnPanelVisibleChanged?.Invoke(true);
                }
            }
    
            // While a slider owns focus its arrow keys are the slider's own input;
            // the keyboard hook (input-forwarding partial) checks UiFocusInSlider to
            // avoid forwarding them to mpv as well (double seek).
    
    }
}
