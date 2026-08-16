using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Windowing;
using Microsoft.UI.Composition;
using mpv_winrt;
using mpv_winui.Modules.Common.Utils;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;

namespace mpv_winui.Modules.Player;

public sealed partial class PiPWindow
{
    private void RootGrid_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(RootGrid).Position;
        var height = RootGrid.ActualHeight;
        var inTopMask = position.Y <= TopMaskHeight;
        var inBottomMask = position.Y >= height - BottomMaskHeight;

        // The top buttons only react to the top mask, the status bar only to
        // the bottom mask; everywhere else both retract.
        SetTopButtonsVisible(inTopMask);
        if (inBottomMask)
        {
            PiPControls.ShowControlPanel();
        }
        else
        {
            PiPControls.HideControlPanel();
        }
    }

    private void RootGrid_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        // Leaving the window must retract everything: the status bar has its
        // idle timer, but the top buttons previously stayed visible until the
        // pointer re-entered and moved to a non-top zone.
        PiPView.SetResizeCursor(null);
        SetTopButtonsVisible(false);
        PiPControls.HideControlPanel();
    }

    private void PiPView_PointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        // Wheel over the PiP video is forwarded to mpv (input.conf volume/seek),
        // matching the main window behavior. Left press stays reserved for
        // dragging the PiP window.
        var point = e.GetCurrentPoint(PiPView);
        var props = point.Properties;
        var key = props.IsHorizontalMouseWheel
            ? (props.MouseWheelDelta > 0 ? "WHEEL_LEFT" : "WHEEL_RIGHT")
            : (props.MouseWheelDelta > 0 ? "WHEEL_UP" : "WHEEL_DOWN");

        _player?.Command(["keydown", key]);
        _player?.Command(["keyup", key]);
        e.Handled = true;
    }

    private void PiPPlayer_MediaLoaded(MpvMediaPlayer player, object? args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            // Keep the user's current (possibly natively resized) window
            // size; just re-assert the swap chain after the new video is
            // configured.
            ScheduleVideoSizeUpdate();
        });
    }

    private void PiPPlayer_MediaInfoChanged(MpvMediaPlayer player, MediaInfoChangedEventArgs args)
    {
        // Keep the aspect lock in sync with the current video (dwidth/dheight
        // are reported by mpv on VIDEO_RECONFIG).
        if (args.VideoWidth > 0 && args.VideoHeight > 0)
        {
            _videoAspect = args.VideoWidth / args.VideoHeight;
        }
    }

    private void PiPBackButton_Click(object sender, RoutedEventArgs e)
    {
        RestoreMainWindow();
    }

    private void PiPOpacitySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        // H4: window opacity. RootGrid.Opacity fades the whole PiP content
        // (video + controls); persisted so the choice survives restarts.
        var value = Math.Round(e.NewValue, 2);
        RootGrid.Opacity = value;
        AppContext.AppSetting.WindowPiPOpacity = value;
    }

    private void PiPSubtitleToggle_Click(object sender, RoutedEventArgs e)
    {
        // H5: quick subtitle toggle on the PiP window.
        _player?.Command(["no-osd", "cycle", "sub-visibility"]);
        PiPSubtitleToggle.IsChecked = !PiPSubtitleToggle.IsChecked;
    }

    private void PiPView_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        // Double-click anywhere on the PiP video restores the main window
        // (equivalent to the top-left back button). Single-click stays a
        // drag/mpv click; the double-tap only fires when no drag happened.
        if (!_draggingWindow && !_resizing)
        {
            RestoreMainWindow();
        }
    }

    private void PiPExitButton_Click(object sender, RoutedEventArgs e)
    {
        // The top-right close exits the whole player (mpv + app). Persist the
        // PiP state first so the next start opens the main window normally.
        AppContext.AppSetting.WindowPiP = false;
        Application.Current.Exit();
    }

    private void SetTopButtonsVisible(bool show)
    {
        if (_topButtonsShow == show)
        {
            return;
        }
        _topButtonsShow = show;
        StartTopButtonsAnimation(show);
    }

    private void StartTopButtonsAnimation(bool show)
    {
        if (_topButtonsAnimating && _topButtonsAnimationShow == show)
        {
            return;
        }

        _topButtonsAnimating = true;
        _topButtonsAnimationShow = show;
        if (show)
        {
            PiPBackButton.Visibility = Visibility.Visible;
            PiPExitButton.Visibility = Visibility.Visible;
            PiPBackButton.Opacity = 1;
            PiPExitButton.Opacity = 1;
        }

        EnsureTopButtonVisuals();
        if (_topButtonsCompositor is null || _topBackButtonVisual is null || _topExitButtonVisual is null)
        {
            // Composition unavailable: snap to the target state.
            TopButtonsAnimationCompleted(show);
            return;
        }

        _topBackButtonVisual.StopAnimation("Opacity");
        _topExitButtonVisual.StopAnimation("Opacity");

        var ease = _topButtonsCompositor.CreateCubicBezierEasingFunction(
            new Vector2(0.215f, 0.61f),
            new Vector2(0.355f, 1f));
        var opacity = _topButtonsCompositor.CreateScalarKeyFrameAnimation();
        opacity.Duration = TimeSpan.FromMilliseconds(180);
        // Start from the current composition value when hiding so a mid-show
        // reversal fades from where the buttons actually are.
        opacity.InsertKeyFrame(0f, show ? 0f : _topBackButtonVisual.Opacity);
        opacity.InsertKeyFrame(1f, show ? 1f : 0f, ease);

        var batch = _topButtonsCompositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        _topBackButtonVisual.StartAnimation("Opacity", opacity);
        _topExitButtonVisual.StartAnimation("Opacity", opacity);
        batch.Completed += (_, _) => TopButtonsAnimationCompleted(show);
        batch.End();
    }

    private void EnsureTopButtonVisuals()
    {
        if (_topBackButtonVisual is null)
        {
            _topBackButtonVisual = ElementCompositionPreview.GetElementVisual(PiPBackButton);
            _topExitButtonVisual = ElementCompositionPreview.GetElementVisual(PiPExitButton);
            _topButtonsCompositor = _topBackButtonVisual.Compositor;
        }
    }

    private void TopButtonsAnimationCompleted(bool show)
    {
        _topButtonsAnimating = false;
        _topBackButtonVisual?.StopAnimation("Opacity");
        _topExitButtonVisual?.StopAnimation("Opacity");
        if (_topBackButtonVisual is not null)
        {
            _topBackButtonVisual.Opacity = show ? 1f : 0f;
        }
        if (_topExitButtonVisual is not null)
        {
            _topExitButtonVisual.Opacity = show ? 1f : 0f;
        }
        PiPBackButton.Opacity = show ? 1 : 0;
        PiPExitButton.Opacity = show ? 1 : 0;
        if (!show)
        {
            // Fully hidden buttons must not stay hit-testable: an
            // invisible button still shows its tooltip on hover.
            PiPBackButton.Visibility = Visibility.Collapsed;
            PiPExitButton.Visibility = Visibility.Collapsed;
        }
    }

    private void StopTopButtonsAnimation()
    {
        _topBackButtonVisual?.StopAnimation("Opacity");
        _topExitButtonVisual?.StopAnimation("Opacity");
        _sizeUpdateTimer.Stop();
        _sizeUpdateTimer.Tick -= SizeUpdateTimer_Tick;
        _topButtonsAnimating = false;
        PiPBackButton.Visibility = Visibility.Visible;
        PiPExitButton.Visibility = Visibility.Visible;
        PiPBackButton.Opacity = 1;
        PiPExitButton.Opacity = 1;
        if (_topBackButtonVisual is not null)
        {
            _topBackButtonVisual.Opacity = 1f;
        }
        if (_topExitButtonVisual is not null)
        {
            _topExitButtonVisual.Opacity = 1f;
        }
    }
}
