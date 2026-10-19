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
using System.Globalization;
using System.Linq;
using Windows.Foundation;
using Windows.UI;
namespace mpv_winui.Modules.Player
{
    /// <summary>
    /// Chapter tick and A/B-loop markers drawn over the progress slider.
    /// </summary>
    public sealed partial class PlayerControl
    {
            private void AbLoopButton_Click(object sender, RoutedEventArgs e)
            {
                ToggleAbLoop();
                UpdateAbLoopMarks();
            }

            /// <summary>
            /// Cycles the A-B loop: first press marks A at the current position,
            /// second closes the loop at B, third clears both.
            ///
            /// mpv exposes the two points as independent properties and has no
            /// toggle of its own, so the cycle lives here, next to the markers
            /// it drives, rather than in a shared helper.
            /// </summary>
            private void ToggleAbLoop()
            {
                if (MediaPlayer is not { } player)
                {
                    return;
                }

                var a = player.AbLoopA();
                var b = player.AbLoopB();

                // Neither point set yet -> mark A at the current position.
                if (a < 0 && b < 0)
                {
                    player.Command(["ab-loop-a", Seconds(player.Position())]);
                    return;
                }

                // A set but B not -> close the loop.
                if (a >= 0 && b < 0)
                {
                    player.Command(["ab-loop-b", Seconds(player.Position())]);
                    return;
                }

                // Both set -> clear the loop.
                player.Command(["ab-loop-a", "no"]);
                player.Command(["ab-loop-b", "no"]);
            }

            /// <summary>Invariant formatting: mpv parses these as C locale numbers.</summary>
            private static string Seconds(double value) => value.ToString(CultureInfo.InvariantCulture);
    
    
            /// <summary>Positions the A/B markers on the progress bar from mpv's ab-loop properties.</summary>
            private void UpdateAbLoopMarks()
            {
                var duration = MediaPlayer?.Duration() ?? 0;
                var a = MediaPlayer?.AbLoopA() ?? -1;
                var b = MediaPlayer?.AbLoopB() ?? -1;
                var width = ProgressSlider.ActualWidth;
                if (width <= 0)
                {
                    return;
                }
    
                if (a > 0 && duration > 0)
                {
                    AbLoopMarkA.Visibility = Visibility.Visible;
                    Canvas.SetLeft(AbLoopMarkA, a / duration * width);
                }
                else
                {
                    AbLoopMarkA.Visibility = Visibility.Collapsed;
                }
    
                if (b > 0 && duration > 0)
                {
                    AbLoopMarkB.Visibility = Visibility.Visible;
                    Canvas.SetLeft(AbLoopMarkB, b / duration * width);
                }
                else
                {
                    AbLoopMarkB.Visibility = Visibility.Collapsed;
                }
            }
    
            /// <summary>
            /// Draws thin tick marks on the progress bar at chapter starts. The
            /// chapter list and brush are cached per media; SizeChanged only
            /// repositions ticks when the bar width actually changed.
            /// </summary>
            private void InvalidateChapterCache()
            {
                _cachedChapterTimes = null;
                _cachedChapterDuration = double.NaN;
                _lastChapterMarkWidth = double.NaN;
            }
    
            private void UpdateChapterMarks(bool force = false)
            {
                var duration = MediaPlayer?.Duration() ?? 0;
                var width = ProgressSlider.ActualWidth;
                if (width <= 0 || duration <= 0)
                {
                    ChapterMarksCanvas.Children.Clear();
                    return;
                }
    
                if (force
                    || _cachedChapterTimes is null
                    || Math.Abs(duration - _cachedChapterDuration) > 0.001)
                {
                    _cachedChapterTimes = MediaPlayer?.GetChapters() is { Count: > 0 } chapters
                        ? chapters.Where(c => c.Time > 0).Select(c => c.Time).ToArray()
                        : [];
                    _cachedChapterDuration = duration;
                    _chapterTickBrush ??= new SolidColorBrush(Windows.UI.Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF));
                }
    
                if (Math.Abs(width - _lastChapterMarkWidth) < 1.0 && !force)
                {
                    return;
                }
                _lastChapterMarkWidth = width;
    
                ChapterMarksCanvas.Children.Clear();
                foreach (var time in _cachedChapterTimes)
                {
                    var tick = new Border
                    {
                        Width = 1,
                        Height = 14,
                        Background = _chapterTickBrush,
                    };
                    Canvas.SetLeft(tick, time / duration * width);
                    ChapterMarksCanvas.Children.Add(tick);
                }
            }
    
    }
}
