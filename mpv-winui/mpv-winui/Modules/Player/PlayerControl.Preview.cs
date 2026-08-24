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
    /// Seek-bar hover preview request pipeline (pointer tracking and hooks).
    /// </summary>
    public sealed partial class PlayerControl
    {
            private void ProgressSlider_PointerEntered(object sender, PointerRoutedEventArgs e)
            {
                _isDragging = true;
            }
    
            private void ProgressSlider_PointerPressed(object sender, PointerRoutedEventArgs e)
            {
                _isDragging = true;
                _suppressBufferingUntil = Environment.TickCount64 + 2000;
                if (AppContext.AppSetting.EnableVideoPreview && ProgressSlider.Maximum > 0)
                {
                    UpdatePreview(ProgressSlider.Value / ProgressSlider.Maximum);
                }
            }
    
            private void ProgressSlider_PointerReleased(object sender, PointerRoutedEventArgs e)
            {
                _isDragging = false;
                _suppressBufferingUntil = Environment.TickCount64 + 500;
            }
    
            private void ProgressSlider_Tapped(object sender, TappedRoutedEventArgs e)
            {
                // Click anywhere on the track to seek there (the thumb drag path
                // sets the same value, so clicking the thumb is harmless).
                if (ProgressSlider.Maximum <= 0)
                {
                    return;
                }
                var point = e.GetPosition(ProgressSlider);
                if (ProgressSlider.ActualWidth <= 0)
                {
                    return;
                }
                var ratio = Math.Clamp(point.X / ProgressSlider.ActualWidth, 0, 1);
                ProgressSlider.Value = ratio * ProgressSlider.Maximum;
            }
    
            private void ProgressSlider_PointerMoved(object sender, PointerRoutedEventArgs e)
            {
                if (_isDragging)
                {
                    UpdatePreview(e);
                }
            }
    
            private void ProgressSlider_PointerExited(object sender, PointerRoutedEventArgs e)
            {
                _isDragging = false;
                ClearPreview();
            }
    
            private void UpdatePreview(PointerRoutedEventArgs e)
            {
                if (MediaPlayer == null || MediaPlayer.Duration <= 0)
                {
                    return;
                }
    
                var point = e.GetCurrentPoint(ProgressSlider);
                UpdatePreview(point.Position.X / ProgressSlider.ActualWidth);
            }
    
            private void UpdatePreview(double fraction)
            {
                if (MediaPlayer == null || MediaPlayer.Duration <= 0)
                {
                    return;
                }
    
                fraction = Math.Clamp(fraction, 0, 1);
                var hoverSec = fraction * MediaPlayer.Duration;
                var controlPoint = ProgressSlider.TransformToVisual(this).TransformPoint(new Point(fraction * ProgressSlider.ActualWidth, 0));
    
                PreviewUpdateRequested?.Invoke(this, (hoverSec, controlPoint.X, controlPoint.Y));
            }
    
            private void ClearPreview()
            {
                PreviewClearRequested?.Invoke(this, EventArgs.Empty);
            }
    
            /// <summary>Attaches or detaches the progress-bar preview hooks (live toggle support).</summary>
            public void EnablePreviewEvents(bool enabled)
            {
                if (enabled)
                {
                    ProgressSlider.PointerEntered += ProgressSlider_PointerEntered;
                    ProgressSlider.PointerMoved += ProgressSlider_PointerMoved;
                    ProgressSlider.PointerExited += ProgressSlider_PointerExited;
                }
                else
                {
                    ProgressSlider.PointerEntered -= ProgressSlider_PointerEntered;
                    ProgressSlider.PointerMoved -= ProgressSlider_PointerMoved;
                    ProgressSlider.PointerExited -= ProgressSlider_PointerExited;
                    _isDragging = false;
                    ClearPreview();
                }
            }
    
    }
}
