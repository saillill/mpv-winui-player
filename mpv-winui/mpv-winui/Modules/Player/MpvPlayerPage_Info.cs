using Microsoft.UI.Dispatching;
using mpv_winrt;
using mpv_winui.Modules.AppModel;
using mpv_winui.Modules.Common.Utils;

namespace mpv_winui.Modules.Player
{
    public sealed partial class MpvPlayerPage
    {
        private uint _videoWidth;
        private uint _videoHeight;
        private DispatcherQueueTimer? _aspectFitTimer;

        private void MpvPlayerPage_MediaInfoChanged(MediaInfoChangedEventArgs args)
        {
            // Diagnostics for the aspect lock: this handler is the only place
            // that publishes the video ratio, so log what actually arrives.
            _logger.Debug("media info changed: video={}x{}", args.VideoWidth, args.VideoHeight);

            // The main window's aspect-lock reads these (mpv reports 0x0 when
            // nothing is playing, which clears the lock's "active video" gate).
            if (args.VideoWidth > 0 && args.VideoHeight > 0)
            {
                MainWindow.HasActiveVideo = true;
                MainWindow.CurrentVideoAspect = (double)args.VideoWidth / args.VideoHeight;

                // Publish the ratio and then honour it — the actual fit runs on
                // the UI thread via ScheduleAspectFit(): AppWindow calls are
                // thread-affine and media info arrives on the mpv event thread,
                // so calling it here would silently fail. (The diagnostic logs
                // the window type only when it is unexpectedly not MainWindow.)
                if (App.Window is not MainWindow)
                {
                    _logger.Debug("aspect fit trigger: windowType={}", App.Window?.GetType().FullName ?? "<null>");
                }
            }
            else
            {
                MainWindow.HasActiveVideo = false;
            }
            _videoWidth = args.VideoWidth > 0 ? (uint)args.VideoWidth : 0;
            _videoHeight = args.VideoHeight > 0 ? (uint)args.VideoHeight : 0;
            DispatcherQueue.RunAsync(() =>
            {
                if (!string.IsNullOrEmpty(args.MediaTitle))
                {
                    UpdatePageTitle(args.MediaTitle);
                }
                else if (!string.IsNullOrEmpty(args.Filename))
                {
                    UpdatePageTitle(args.Filename);
                }
                else
                {
                    UpdatePageTitle(PackageHelper.AppName);
                }

                // Second fit trigger: media info can arrive before the player
                // page is wired up (or with 0x0 first), so also run the fit
                // shortly after the file has loaded, on the UI thread.
                if (_videoWidth > 0 && _videoHeight > 0)
                {
                    ScheduleAspectFit();
                }

                // The playlist's current row shows WxH; the refresh is
                // debounced by the shared playlist timer.
                RefreshPlaylistAsync();
            });
        }

        private void UpdatePageTitle(string title)
        {
            if (App.Window is MainWindow window)
            {
                window.UpdateTitle(title);
            }
        }

        /// <summary>
        /// Runs the aspect fit once, ~250ms after the file has loaded: at that
        /// point dwidth/dheight are final (filters settled). Restarting the
        /// timer on every media-info change debounces the reconfig storm.
        /// </summary>
        private void ScheduleAspectFit()
        {
            if (_aspectFitTimer is null)
            {
                _aspectFitTimer = DispatcherQueue.CreateTimer();
                _aspectFitTimer.Interval = System.TimeSpan.FromMilliseconds(250);
                _aspectFitTimer.IsRepeating = false;
                _aspectFitTimer.Tick += (_, _) =>
                {
                    if (App.Window is MainWindow window)
                    {
                        window.ApplyVideoAspectToWindow();
                    }
                };
            }

            _aspectFitTimer.Stop();
            _aspectFitTimer.Start();
        }
    }
}
