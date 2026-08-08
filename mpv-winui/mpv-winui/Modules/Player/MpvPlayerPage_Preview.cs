using Microsoft.UI.Xaml;
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

        private void SetupPreview()
        {
            if (AppContext.AppSetting.EnableVideoPreview)
            {
                PlayerControl.PreviewUpdateRequested += PlayerControl_PreviewUpdateRequested;
                PlayerControl.PreviewClearRequested += PlayerControl_PreviewClearRequested;
                _mediaPlayer.PreviewChanged += MediaPlayer_PreviewChanged;
            }
        }

        private void CleanupPreview()
        {
            PlayerControl.PreviewUpdateRequested -= PlayerControl_PreviewUpdateRequested;
            PlayerControl.PreviewClearRequested -= PlayerControl_PreviewClearRequested;
            _mediaPlayer.PreviewChanged -= MediaPlayer_PreviewChanged;
        }

        private void PlayerControl_PreviewUpdateRequested(object? sender, (double HoverSec, double RelativeX, double RelativeY) args)
        {
            _lastPreviewPoint = PlayerControl.TransformToVisual(PlayerView).TransformPoint(new Point(args.RelativeX, args.RelativeY));
            _mediaPlayer.SetHoverSec(args.HoverSec);
            _mediaPlayer.SetDrawPreview(0, 0, 0, 0);

            if (PreviewCard.Visibility == Visibility.Visible)
            {
                UpdatePreviewCardPosition();
            }
        }

        private void PlayerControl_PreviewClearRequested(object? sender, EventArgs e)
        {
            _mediaPlayer.ClearPreview();
            HidePreview();
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
            try
            {
                var bytes = await File.ReadAllBytesAsync(info.Path);
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
            PreviewImage.Source = null;
            PreviewCard.Visibility = Visibility.Collapsed;
        }
    }
}
