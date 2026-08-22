using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using mpv_winrt;
using mpv_winui.Modules.Common.Utils;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;

namespace mpv_winui.Modules.Player
{
    public sealed partial class MpvPlayerPage
    {
        private const double PreviewCardWidth = 248;
        private const double PreviewCardHeight = 143;

        private Point _lastPreviewPoint;
        private DispatcherQueueTimer? _previewThrottleTimer;
        private (double HoverSec, double RelativeX, double RelativeY)? _pendingPreview;

        // In-process software preview: a second libmpv instance renders into
        // PreviewImage via mpv_render_context (no external mpv.exe / thumbfast).
        private MpvPreviewer? _previewer;
        private Task? _previewerInitTask;
        private bool _previewerCleanedUp;
        private string _previewLoadedPath = string.Empty;
        private double _lastPreviewSec = -1;
        private (string Path, double Sec)? _pendingPreviewerRequest;

        // False when the pending request comes from the MediaOpened warmup:
        // the previewer then just loads the file without showing the card.
        private bool _pendingPreviewShow = true;

        private void SetupPreview()
        {
            _logger.Debug("SetupPreview called, enabled={}", AppContext.AppSetting.EnableVideoPreview);
            if (AppContext.AppSetting.EnableVideoPreview)
            {
                _previewerCleanedUp = false;
                PlayerControl.PreviewUpdateRequested += PlayerControl_PreviewUpdateRequested;
                PlayerControl.PreviewClearRequested += PlayerControl_PreviewClearRequested;
                _mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
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
            _mediaPlayer.MediaOpened -= MediaPlayer_MediaOpened;
            HidePreview();
            DestroyPreviewer();
        }

        // Thumbfast-style worker warmup: feed the file to the preview
        // instance as soon as playback starts, so the first hover shows a
        // thumbnail instead of waiting for a second mpv to boot.
        private void MediaPlayer_MediaOpened(object? sender, object? args)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    var path = CurrentPlaybackPath();
                    if (string.IsNullOrEmpty(path))
                    {
                        return;
                    }

                    if (_previewer is not null)
                    {
                        WarmupPreviewerWith(path);
                        return;
                    }

                    _pendingPreviewerRequest = (path, 0);
                    _pendingPreviewShow = false;
                    _previewerInitTask ??= InitializePreviewerAsync();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "preview warmup failed");
                }
            });
        }

        private void WarmupPreviewerWith(string path)
        {
            if (string.Equals(_previewLoadedPath, path, StringComparison.Ordinal))
            {
                return;
            }
            _previewLoadedPath = path;
            _lastPreviewSec = -1;
            Task.Run(() => _previewer?.LoadFile(path)).FireAndForget(OnException);
        }

        private void PlayerControl_PreviewUpdateRequested(object? sender, (double HoverSec, double RelativeX, double RelativeY) args)
        {
            // Coalesce the high-frequency pointer stream; only the latest hover
            // position is sent to the previewer (max ~25 updates/sec).
            _pendingPreview = args;
            _previewThrottleTimer?.Start();
        }

        private void PlayerControl_PreviewClearRequested(object? sender, EventArgs e)
        {
            _pendingPreview = null;
            _previewThrottleTimer?.Stop();
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
            _lastPreviewPoint = PlayerControl.TransformToVisual(PlayerView).TransformPoint(new Point(preview.RelativeX, preview.RelativeY));
            ShowPreviewAt(preview.HoverSec);
        }

        private void ShowPreviewAt(double hoverSec)
        {
            try
            {
                var path = CurrentPlaybackPath();
                _logger.Debug("built-in preview hover, path={}, sec={}", path ?? "<null>", hoverSec);
                if (string.IsNullOrEmpty(path))
                {
                    HidePreview();
                    return;
                }

                if (_previewer is not null)
                {
                    ShowPreviewAtCore(path, hoverSec);
                    return;
                }

                _pendingPreviewerRequest = (path, hoverSec);
                _pendingPreviewShow = true;
                _previewerInitTask ??= InitializePreviewerAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "built-in preview update failed");
                HidePreview();
            }
        }

        private async Task InitializePreviewerAsync()
        {
            try
            {
                var scale = XamlRoot?.RasterizationScale ?? 1.0;
                if (scale < 1.0)
                {
                    scale = 1.0;
                }

                var renderWidth = (uint)Math.Max(1, Math.Ceiling(PreviewImage.Width * scale));
                var renderHeight = (uint)Math.Max(1, Math.Ceiling(PreviewImage.Height * scale));
                var previewer = new MpvPreviewer();
                await previewer.Initialize(PreviewImage, renderWidth, renderHeight);

                if (_previewerCleanedUp)
                {
                    await Task.Run(() => previewer.Destroy());
                    return;
                }

                _previewer = previewer;
                if (_pendingPreviewerRequest is { } pending)
                {
                    _pendingPreviewerRequest = null;
                    if (_pendingPreviewShow)
                    {
                        ShowPreviewAtCore(pending.Path, pending.Sec);
                    }
                    else
                    {
                        // Warmup request: load the file silently, the card
                        // only appears on a real hover.
                        WarmupPreviewerWith(pending.Path);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "built-in preview initialization failed");
                _previewer = null;
            }
            finally
            {
                _previewerInitTask = null;
            }
        }

        private string? CurrentPlaybackPath()
        {
            foreach (var item in _allPlaylistItems)
            {
                if (item.IsCurrent && !string.IsNullOrEmpty(item.Path))
                {
                    return item.Path;
                }
            }

            if (!string.IsNullOrEmpty(_mediaPlayer.CurrentPath))
            {
                return _mediaPlayer.CurrentPath;
            }

            return _pendingPaths?.FirstOrDefault()?.Path;
        }

        private void ShowPreviewAtCore(string path, double sec)
        {
            if (_previewer is null)
            {
                _logger.Debug("built-in preview core skipped, previewer not ready");
                return;
            }

            _logger.Debug("built-in preview core, path={}, sec={}", path, sec);
            if (!string.Equals(_previewLoadedPath, path, StringComparison.Ordinal))
            {
                _previewLoadedPath = path;
                _lastPreviewSec = -1;
                Task.Run(() =>
                {
                    _previewer.LoadFile(path);
                    _previewer.SetPosition(sec);
                    _previewer.Pause();
                }).FireAndForget(OnException);
                _lastPreviewSec = sec;
            }
            else if (Math.Abs(sec - _lastPreviewSec) > 0.25)
            {
                // Keyframe seeks snap to the nearest keyframe anyway (usually
                // seconds apart), so requests finer than this only burn seek
                // commands without changing the shown frame.
                _previewer.SetPosition(sec);
                _lastPreviewSec = sec;
            }

            PreviewCard.Visibility = Visibility.Visible;
            UpdatePreviewCardPosition();
        }

        private void DestroyPreviewer()
        {
            _previewerCleanedUp = true;
            _pendingPreviewerRequest = null;
            _previewLoadedPath = string.Empty;
            _lastPreviewSec = -1;

            if (_previewer is { } previewer)
            {
                _previewer = null;
                Task.Run(() => previewer.Destroy()).FireAndForget(OnException);
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
            PreviewCard.Visibility = Visibility.Collapsed;
        }
    }
}
