namespace mpv_winui.Modules.Settings
{
    /// <summary>
    /// Playback continuity: cache/demuxer sizing, resume, loop, watch-later.
    /// </summary>
    public partial class AppSettings
    {
        public string KeepOpen
        {
            get => _dataSetting.GetValue(nameof(KeepOpen), "yes");
            set => _dataSetting.SetValue(nameof(KeepOpen), value);
        }

        public string LoopPlaylist
        {
            get => _dataSetting.GetValue(nameof(LoopPlaylist), "yes");
            set => _dataSetting.SetValue(nameof(LoopPlaylist), value);
        }

        public bool LoopFile
        {
            get => _dataSetting.GetValue(nameof(LoopFile), false);
            set => _dataSetting.SetValue(nameof(LoopFile), value);
        }

        public string WatchLaterDir
        {
            get => _dataSetting.GetValue(nameof(WatchLaterDir), string.Empty);
            set => _dataSetting.SetValue(nameof(WatchLaterDir), value);
        }

        public bool SavePositionOnQuit
        {
            get => _dataSetting.GetValue(nameof(SavePositionOnQuit), false);
            set => _dataSetting.SetValue(nameof(SavePositionOnQuit), value);
        }

        public bool ResumePlayback
        {
            get => _dataSetting.GetValue(nameof(ResumePlayback), true);
            set => _dataSetting.SetValue(nameof(ResumePlayback), value);
        }

        public bool CachePauseInitial
        {
            get => _dataSetting.GetValue(nameof(CachePauseInitial), false);
            set => _dataSetting.SetValue(nameof(CachePauseInitial), value);
        }

        public double CachePauseWait
        {
            get => _dataSetting.GetValue(nameof(CachePauseWait), 1.0);
            set => _dataSetting.SetValue(nameof(CachePauseWait), value);
        }

        public bool HrSeek
        {
            get => _dataSetting.GetValue(nameof(HrSeek), true);
            set => _dataSetting.SetValue(nameof(HrSeek), value);
        }

        public bool HrSeekFramedrop
        {
            get => _dataSetting.GetValue(nameof(HrSeekFramedrop), false);
            set => _dataSetting.SetValue(nameof(HrSeekFramedrop), value);
        }

        public bool CacheOnDisk
        {
            get => _dataSetting.GetValue(nameof(CacheOnDisk), false);
            set => _dataSetting.SetValue(nameof(CacheOnDisk), value);
        }

        public int DemuxerMaxBytes
        {
            get => _dataSetting.GetValue(nameof(DemuxerMaxBytes), 1024);
            set => _dataSetting.SetValue(nameof(DemuxerMaxBytes), value);
        }

        public int CacheSecs
        {
            get => _dataSetting.GetValue(nameof(CacheSecs), 0);
            set => _dataSetting.SetValue(nameof(CacheSecs), value);
        }

        public string CacheEnabled
        {
            get => _dataSetting.GetValue(nameof(CacheEnabled), "auto");
            set => _dataSetting.SetValue(nameof(CacheEnabled), value);
        }

        public double DemuxerReadahead
        {
            get => _dataSetting.GetValue(nameof(DemuxerReadahead), 2.0);
            set => _dataSetting.SetValue(nameof(DemuxerReadahead), value);
        }

        public string AutoCreatePlaylist
        {
            get => _dataSetting.GetValue(nameof(AutoCreatePlaylist), "same");
            set => _dataSetting.SetValue(nameof(AutoCreatePlaylist), value);
        }

        public string DirectoryMode
        {
            get => _dataSetting.GetValue(nameof(DirectoryMode), "ignore");
            set => _dataSetting.SetValue(nameof(DirectoryMode), value);
        }

        public int DemuxerMaxBackBytes
        {
            get => _dataSetting.GetValue(nameof(DemuxerMaxBackBytes), 512);
            set => _dataSetting.SetValue(nameof(DemuxerMaxBackBytes), value);
        }

        public string WatchLaterOptions
        {
            get => _dataSetting.GetValue(nameof(WatchLaterOptions), "start,vid,aid,sid");
            set => _dataSetting.SetValue(nameof(WatchLaterOptions), value);
        }

        public string DirectoryFilterTypes
        {
            get => _dataSetting.GetValue(nameof(DirectoryFilterTypes), "video,audio");
            set => _dataSetting.SetValue(nameof(DirectoryFilterTypes), value);
        }

        public bool CachePause
        {
            get => _dataSetting.GetValue(nameof(CachePause), true);
            set => _dataSetting.SetValue(nameof(CachePause), value);
        }

        public bool PrefetchPlaylist
        {
            get => _dataSetting.GetValue(nameof(PrefetchPlaylist), false);
            set => _dataSetting.SetValue(nameof(PrefetchPlaylist), value);
        }

        public double DemuxerHysteresisSecs
        {
            get => _dataSetting.GetValue(nameof(DemuxerHysteresisSecs), 0.0);
            set => _dataSetting.SetValue(nameof(DemuxerHysteresisSecs), value);
        }

        public string DemuxerCacheDir
        {
            get => _dataSetting.GetValue(nameof(DemuxerCacheDir), string.Empty);
            set => _dataSetting.SetValue(nameof(DemuxerCacheDir), value);
        }
    }
}
