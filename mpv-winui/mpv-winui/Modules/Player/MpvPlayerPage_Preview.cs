using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using mpv_winui.Modules.Common.Utils;
using mpv_winrt;
using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;

namespace mpv_winui.Modules.Player
{
    public sealed partial class MpvPlayerPage
    {
        private const double PreviewCardWidth = 248;
        private const double PreviewCardHeight = 143;

        private Point _lastPreviewPoint;
        private int _previewGeneration;
        private DispatcherQueueTimer? _previewThrottleTimer;
        private (double HoverSec, double RelativeX, double RelativeY)? _pendingPreview;

        private void SetupPreview()
        {
            if (AppContext.AppSetting.EnableVideoPreview)
            {
                PlayerControl.PreviewUpdateRequested += PlayerControl_PreviewUpdateRequested;
                PlayerControl.PreviewClearRequested += PlayerControl_PreviewClearRequested;
                _mediaPlayer.PreviewChanged += MediaPlayer_PreviewChanged;
                _previewThrottleTimer = DispatcherQueue.CreateTimer();
                _previewThrottleTimer.Interval = TimeSpan.FromMilliseconds(40);
                _previewThrottleTimer.Tick += PreviewThrottleTick;
            }
        }

        private void CleanupPreview()
        {
            _pendingPreview = null;
            if (_previewThrottleTimer is { } timer)
            {
                timer.Stop();
                timer.Tick -= PreviewThrottleTick;
                _previewThrottleTimer = null;
            }
            PlayerControl.PreviewUpdateRequested -= PlayerControl_PreviewUpdateRequested;
            PlayerControl.PreviewClearRequested -= PlayerControl_PreviewClearRequested;
            _mediaPlayer.PreviewChanged -= MediaPlayer_PreviewChanged;
            HidePreview();
        }

        private void PlayerControl_PreviewUpdateRequested(object? sender, (double HoverSec, double RelativeX, double RelativeY) args)
        {
            _lastPreviewPoint = PlayerControl.TransformToVisual(PlayerView).TransformPoint(new Point(args.RelativeX, args.RelativeY));
            // Coalesce the high-frequency pointer stream: thumbfast re-renders
            // on every hover-sec change, so only the latest position is sent
            // to mpv (max ~25 updates/second while scrubbing).
            _pendingPreview = args;
            _previewThrottleTimer?.Start();

            if (PreviewCard.Visibility == Visibility.Visible)
            {
                UpdatePreviewCardPosition();
            }
        }

        private void PlayerControl_PreviewClearRequested(object? sender, EventArgs e)
        {
            _pendingPreview = null;
            _previewThrottleTimer?.Stop();
            _mediaPlayer.ClearPreview();
            HidePreview();
        }

        private void PreviewThrottleTick(DispatcherQueueTimer sender, object args)
        {
            sender.Stop();
            if (_pendingPreview is not { } preview)
            {
                return;
            }

            _pendingPreview = null;
            _mediaPlayer.SetHoverSec(preview.HoverSec);
            _mediaPlayer.SetDrawPreview(0, 0, 0, 0);
        }

        private void MediaPlayer_PreviewChanged(MpvMediaPlayer sender, MpvPreviewInfo? info)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (info is null || string.IsNullOrEmpty(info.Path))
                {
                    HidePreview();
                    return;
                }

                LoadPreviewAsync(info).FireAndForget(OnException);
            });
        }

        private async Task LoadPreviewAsync(MpvPreviewInfo info)
        {
            var generation = _previewGeneration;
            try
            {
                byte[]? bytes = null;
                for (var attempt = 0; attempt < 3 && bytes is null; attempt++)
                {
                    try
                    {
                        bytes = await File.ReadAllBytesAsync(info.Path);
                    }
                    catch (IOException)
                    {
                        // thumbfast replaces the frame file while rendering; retry briefly.
                        await Task.Delay(40);
                    }
                }
                if (bytes is null)
                {
                    return;
                }
                if (bytes.Length < info.Width * info.Height * 4)
                {
                    return;
                }

                var bitmap = SoftwareBitmap.CreateCopyFromBuffer(
                    bytes.AsBuffer(),
                    BitmapPixelFormat.Bgra8,
                    (int)info.Width,
                    (int)info.Height,
                    BitmapAlphaMode.Ignore);
                var source = new SoftwareBitmapSource();
                await source.SetBitmapAsync(bitmap);

                // The preview may have been hidden while the frame was being
                // decoded; ignore stale loads so the card does not pop back in.
                if (generation != _previewGeneration)
                {
                    return;
                }

                PreviewImage.Source = source;
                PreviewCard.Visibility = Visibility.Visible;
                UpdatePreviewCardPosition();
            }
            catch (Exception)
            {
                // The thumbnail file may be replaced between renders; keep the last frame.
            }
        }

        private void UpdatePreviewCardPosition()
        {
            var viewWidth = PlayerView.ActualWidth;
            var viewHeight = PlayerView.ActualHeight;
            if (viewWidth <= 0 || viewHeight <= 0)
            {
                return;
            }

            var x = _lastPreviewPoint.X - PreviewCardWidth / 2;
            var y = _lastPreviewPoint.Y - PreviewCardHeight - 12;
            x = Math.Clamp(x, 0, viewWidth - PreviewCardWidth);
            y = Math.Clamp(y, 0, viewHeight - PreviewCardHeight);
            PreviewCard.Margin = new Microsoft.UI.Xaml.Thickness(x, y, 0, 0);
        }

        private void HidePreview()
        {
            _previewGeneration++;
            PreviewImage.Source = null;
            PreviewCard.Visibility = Visibility.Collapsed;
        }
    }
}
