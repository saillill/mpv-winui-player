using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using mpv_winui.Modules.Player.BuiltInPreview;
using mpv_winui.Modules.Common.Utils;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;

namespace mpv_winui.Modules.Player
{
    /// <summary>
    /// Seek-bar hover preview.
    ///
    /// Two rendering back ends share one pointer pipeline (the control raises
    /// <c>PreviewUpdateRequested</c> / <c>PreviewClearRequested</c>):
    ///
    /// * <b>Native</b> (<c>EnableVideoPreview</c>) - mpv draws the thumbnail
    ///   itself via <c>SetHoverSec</c> / <c>SetDrawPreview</c>, so the card is
    ///   positioned in native pixel space.
    /// * <b>Built-in</b> (<c>EnableVideoBuiltInPreview</c>) - a second libmpv
    ///   instance (<see cref="MpvPreviewer"/>) renders into
    ///   <c>MpvPreview</c>'s Image, and the card is moved by a XAML
    ///   <c>TranslateTransform</c>.
    ///
    /// Both are fed from a coalescing <see cref="DispatcherQueueTimer"/> so the
    /// high-frequency pointer stream collapses to one request per tick.
    /// </summary>
    public sealed partial class MpvPlayerPage
    {
        // ------------------------------------------------------------ shared state

        /// <summary>Coalescing timer for hover-preview requests (created lazily).</summary>
        private DispatcherQueueTimer? _previewThrottleTimer;

        /// <summary>Latest pointer sample; only the newest is used per tick.</summary>
        private (double HoverSec, double RelativeX, double RelativeY)? _pendingPreview;

        /// <summary>Consecutive idle ticks; the sampler stops itself once at rest.</summary>
        private int _previewIdleTicks;

        /// <summary>Which back end the last <see cref="SetupPreview"/> selected.</summary>
        private bool _useBuiltInPreview;

        /// <summary>
        /// Lazily creates <see cref="_previewThrottleTimer"/> bound to this page's
        /// dispatcher, with the interval taken from the current setting. Created on
        /// demand so the <c>ThumbnailUpdateInterval</c> setting always has a live
        /// target while previews are enabled.
        /// </summary>
        private DispatcherQueueTimer EnsurePreviewThrottleTimer()
        {
            if (_previewThrottleTimer is null)
            {
                _previewThrottleTimer = DispatcherQueue.CreateTimer();
                _previewThrottleTimer.Interval = TimeSpan.FromMilliseconds(
                    Math.Clamp(AppContext.AppSetting.ThumbnailUpdateInterval, 40, 600));
                // Repeating: the sampler keeps its cadence while the pointer moves
                // and stops itself after a few idle ticks.
                _previewThrottleTimer.IsRepeating = true;
                _previewThrottleTimer.Tick += PreviewThrottleTick;
            }

            return _previewThrottleTimer;
        }

        // ------------------------------------------------------------ lifecycle

        private void SetupPreview()
        {
            // The two settings are mutually exclusive renderers for the same UI;
            // prefer the built-in one when both are on (it is the richer path).
            _useBuiltInPreview = AppContext.AppSetting.EnableVideoBuiltInPreview;

            if (!AppContext.AppSetting.EnableVideoPreview && !_useBuiltInPreview)
            {
                return;
            }

            EnsurePreviewThrottleTimer();

            if (_useBuiltInPreview)
            {
                SetupBuiltInPreview();
            }

            PlayerControl.PreviewUpdateRequested += PlayerControl_PreviewUpdateRequested;
            PlayerControl.PreviewClearRequested += PlayerControl_PreviewClearRequested;
        }

        private void CleanupPreview()
        {
            _pendingPreview = null;
            _previewIdleTicks = 0;

            if (_previewThrottleTimer is { } timer)
            {
                timer.Stop();
                timer.Tick -= PreviewThrottleTick;
                _previewThrottleTimer = null;
            }

            PlayerControl.PreviewUpdateRequested -= PlayerControl_PreviewUpdateRequested;
            PlayerControl.PreviewClearRequested -= PlayerControl_PreviewClearRequested;

            if (_useBuiltInPreview)
            {
                CleanupBuiltInPreview();
                _useBuiltInPreview = false;
            }

            HidePreview();
        }

        /// <summary>
        /// Releases the preview renderer so the next hover re-creates it with the
        /// current settings (render size, resolution scale).
        /// </summary>
        private void DestroyPreviewer()
        {
            CleanupPreview();
            SetupPreview();
        }

        // ------------------------------------------------------------ pointer pipeline

        private void PlayerControl_PreviewUpdateRequested(object? sender, (double HoverSec, double X, double Y) args)
        {
            // Coalesce the high-frequency pointer stream; only the latest hover
            // position reaches the renderer. The timer samples at a fixed cadence
            // and is only *started* here - restarting it on every move postponed
            // the tick for as long as the pointer kept moving, which is what made
            // the thumbnail trail the cursor.
            _pendingPreview = (args.HoverSec, args.X, args.Y);
            if (_previewThrottleTimer is { IsRunning: false } timer)
            {
                _previewIdleTicks = 0;
                timer.Start();
            }
        }

        private void PlayerControl_PreviewClearRequested(object? sender, EventArgs e)
        {
            _pendingPreview = null;
            _previewIdleTicks = 0;
            _previewThrottleTimer?.Stop();
            HidePreview();
        }

        private void PreviewThrottleTick(DispatcherQueueTimer sender, object args)
        {
            if (_pendingPreview is { } preview)
            {
                _pendingPreview = null;
                _previewIdleTicks = 0;
                ShowPreviewAt(preview.HoverSec, preview.RelativeX, preview.RelativeY);
                return;
            }

            // No new hover position for a few ticks: stop until the next move.
            if (++_previewIdleTicks >= 3)
            {
                sender.Stop();
            }
        }

        private void ShowPreviewAt(double hoverSec, double relativeX, double relativeY)
        {
            try
            {
                if (_useBuiltInPreview)
                {
                    ShowBuiltInPreviewAt(hoverSec, relativeX, relativeY);
                }
                else
                {
                    ShowNativePreviewAt(hoverSec, relativeX, relativeY);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "hover preview update failed");
                HidePreview();
            }
        }

        private void HidePreview()
        {
            if (_useBuiltInPreview)
            {
                MpvPreview?.Hide();
                return;
            }

            _mediaPlayer.ClearPreview();
        }

        // ------------------------------------------------------------ native backend

        private const int NativePreviewWidth = 192;
        private const int NativePreviewHeight = 108;

        /// <summary>
        /// mpv-side preview: hand the hover time and the target rectangle (in
        /// native pixels) to the player, which composites the frame into the swap
        /// chain itself.
        /// </summary>
        private void ShowNativePreviewAt(double hoverSec, double relativeX, double relativeY)
        {
            var point = PlayerControl.TransformSliderPoint(PlayerView, relativeX, relativeY);

            var scaleX = PlayerView.CompositionScaleX;
            var scaleY = PlayerView.CompositionScaleY;

            double previewX;
            if (point.X < NativePreviewWidth / 2.0)
            {
                previewX = 0;
            }
            else if (point.X > (PlayerView.ActualWidth - (NativePreviewWidth / 2.0)))
            {
                previewX = PlayerView.ActualWidth - NativePreviewWidth;
            }
            else
            {
                previewX = point.X - (NativePreviewWidth / 2.0);
            }
            var previewY = point.Y - NativePreviewHeight - 8;

            _mediaPlayer.SetHoverSec(hoverSec);
            _mediaPlayer.SetDrawPreview(
                (int)(previewX * scaleX),
                (int)(previewY * scaleY),
                (int)(NativePreviewWidth * scaleX),
                (int)(NativePreviewHeight * scaleY));
        }

        // ---------------------------------------------------------- built-in backend

        private void ShowBuiltInPreviewAt(double hoverSec, double relativeX, double relativeY)
        {
            if (_discMenuActive)
            {
                return;
            }

            var path = _mediaPlayer.GetCurrentPath();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string discPath;
            var isDisc = TryGetDiscType(path, out var discType);
            if (isDisc)
            {
                if (null == discType)
                {
                    return;
                }
                discPath = _mediaPlayer.GetDiscPath(discType.Value);
            }
            else
            {
                discPath = string.Empty;
            }

            var currentEdition = _mediaPlayer.CurrentEdition();
            var currentVideoTrack = _mediaPlayer.CurrentVideoTrack();

            var point = PlayerControl.TransformSliderPoint(PlayerView, relativeX, relativeY);
            if (_logger.IsDebugEnabled)
            {
                _logger.Debug("BuiltInPreview UpdateRequested, HoverSec={}, RelativeX={}, RelativeY={}",
                    hoverSec, point.X, point.Y);
            }

            if (MpvPreview is null)
            {
                return;
            }

            var cardWidth = MpvPreview.ActualWidth > 0 ? MpvPreview.ActualWidth : MpvPreview.Width;
            var cardHeight = MpvPreview.ActualHeight > 0 ? MpvPreview.ActualHeight : MpvPreview.Height;
            var hostWidth = PlayerControl.ActualWidth;

            if (point.X < cardWidth / 2.0)
            {
                PreviewControlTranslation.X = 0;
            }
            else if (point.X > (hostWidth - (cardWidth / 2.0)))
            {
                PreviewControlTranslation.X = hostWidth - cardWidth;
            }
            else
            {
                PreviewControlTranslation.X = point.X - (cardWidth / 2.0);
            }
            PreviewControlTranslation.Y = point.Y - cardHeight - 8;

            MpvPreview.Show(hoverSec, path, isDisc, discType, discPath, currentEdition, currentVideoTrack);
            MpvPreview.Visibility = Visibility.Visible;
        }
    }
}
