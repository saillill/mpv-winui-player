using System;

namespace mpv_winui.Domains.Playlist
{
    public class WatchLaterItem
    {
        public string Path
        {
            get; set;
        } = string.Empty;

        public DateTimeOffset? Time
        {
            get; set;
        }
    }
}
