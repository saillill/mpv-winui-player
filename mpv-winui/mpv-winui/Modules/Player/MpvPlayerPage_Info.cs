using mpv_winrt;
using mpv_winui.Modules.AppModel;
using mpv_winui.Modules.Common.Utils;

namespace mpv_winui.Modules.Player
{
    public sealed partial class MpvPlayerPage
    {
        private uint _videoWidth;
        private uint _videoHeight;

        private void MpvPlayerPage_MediaInfoChanged(MpvMediaPlayer player, MediaInfoChangedEventArgs args)
        {
            // The main window's aspect-lock reads these (mpv reports 0x0 when
            // nothing is playing, which clears the lock's "active video" gate).
            if (args.VideoWidth > 0 && args.VideoHeight > 0)
            {
                MainWindow.HasActiveVideo = true;
                MainWindow.CurrentVideoAspect = (double)args.VideoWidth / args.VideoHeight;
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
    }
}
