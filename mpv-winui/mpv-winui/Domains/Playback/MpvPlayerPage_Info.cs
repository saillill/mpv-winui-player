using Microsoft.UI.Dispatching;
using mpv_winrt;
using mpv_winui.Platform.AppModel;
using mpv_winui.Modules.Common.Utils;

namespace mpv_winui.Domains.Playback
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
            //
            // INFO for the 0x0 edge only, DEBUG for every other report. The
            // asymmetry is deliberate: a file that loads and then reports
            // `video=0x0` is a defect the user feels (nothing appears), while a
            // file that simply has no picture reports 0x0 as normal. Logging the
            // 0x0 transition at INFO is what makes the next occurrence visible
            // without debug logging enabled at all -- the state that hid this for
            // two days (see SettingValueCodec for why debug logging was dark).
            if (args.VideoWidth > 0 && args.VideoHeight > 0)
            {
                _logger.Debug("media info changed: video={}x{} file={}",
                    args.VideoWidth, args.VideoHeight, args.Filename);
                PlayChain.SurfaceSized((int)args.VideoWidth, (int)args.VideoHeight, args.Filename);
            }
            else if (_videoWidth > 0 || _videoHeight > 0)
            {
                // Had a real size, now 0x0. This is the shape of "the file loaded
                // and no picture appeared", and it is why the aspect fit is then
                // skipped as "no active video" further down the chain.
                PlayChain.SurfaceLost(string.Format(
                    "mpv reported {0}x{1} after {2}x{3}, file now {4}",
                    args.VideoWidth, args.VideoHeight, _videoWidth, _videoHeight,
                    args.Filename));
            }
            else
            {
                _logger.Debug("media info changed: video={}x{} file={}",
                    args.VideoWidth, args.VideoHeight, args.Filename);
            }

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
                    _mediaTitle = args.MediaTitle;

                    UpdatePageTitle(args.MediaTitle);

                    if (_musicPageVisible)
                    {
                        ApplyMusicMetadata();
                    }
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
