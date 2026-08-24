namespace mpv_winui.Modules.Settings
{
    /// <summary>
    /// Bundled Lua plugin switches (metadata OSD, cover art, VSR/HDR helpers).
    /// </summary>
    public partial class AppSettings
    {
        public bool VsrAutoEnabled
        {
            get => _dataSetting.GetValue(nameof(VsrAutoEnabled), false);
            set => _dataSetting.SetValue(nameof(VsrAutoEnabled), value);
        }

        public string HdrAutoMode
        {
            get => _dataSetting.GetValue(nameof(HdrAutoMode), "auto");
            set => _dataSetting.SetValue(nameof(HdrAutoMode), value);
        }

        public bool SeekHoldEnabled
        {
            get => _dataSetting.GetValue(nameof(SeekHoldEnabled), true);
            set => _dataSetting.SetValue(nameof(SeekHoldEnabled), value);
        }

        public bool MetadataOsdEnabled
        {
            get => _dataSetting.GetValue(nameof(MetadataOsdEnabled), true);
            set => _dataSetting.SetValue(nameof(MetadataOsdEnabled), value);
        }

        public int MetadataOsdAutohideTimeout
        {
            get => _dataSetting.GetValue(nameof(MetadataOsdAutohideTimeout), 5);
            set => _dataSetting.SetValue(nameof(MetadataOsdAutohideTimeout), value);
        }

        public bool CoverArtPreferEmbedded
        {
            get => _dataSetting.GetValue(nameof(CoverArtPreferEmbedded), false);
            set => _dataSetting.SetValue(nameof(CoverArtPreferEmbedded), value);
        }

        public bool MetadataOsdShowChapter
        {
            get => _dataSetting.GetValue(nameof(MetadataOsdShowChapter), false);
            set => _dataSetting.SetValue(nameof(MetadataOsdShowChapter), value);
        }

        public bool CoverArtAlwaysScan
        {
            get => _dataSetting.GetValue(nameof(CoverArtAlwaysScan), false);
            set => _dataSetting.SetValue(nameof(CoverArtAlwaysScan), value);
        }

        public bool MetadataOsdEnableForVideo
        {
            get => _dataSetting.GetValue(nameof(MetadataOsdEnableForVideo), false);
            set => _dataSetting.SetValue(nameof(MetadataOsdEnableForVideo), value);
        }

        public bool MetadataOsdEnableForImage
        {
            get => _dataSetting.GetValue(nameof(MetadataOsdEnableForImage), false);
            set => _dataSetting.SetValue(nameof(MetadataOsdEnableForImage), value);
        }

        public int MetadataOsdAutohideStatusTimeout
        {
            get => _dataSetting.GetValue(nameof(MetadataOsdAutohideStatusTimeout), 5);
            set => _dataSetting.SetValue(nameof(MetadataOsdAutohideStatusTimeout), value);
        }

        public bool MetadataOsdShowAlbumTrack
        {
            get => _dataSetting.GetValue(nameof(MetadataOsdShowAlbumTrack), false);
            set => _dataSetting.SetValue(nameof(MetadataOsdShowAlbumTrack), value);
        }

        public bool CoverArtLoadFromFilesystem
        {
            get => _dataSetting.GetValue(nameof(CoverArtLoadFromFilesystem), true);
            set => _dataSetting.SetValue(nameof(CoverArtLoadFromFilesystem), value);
        }

        public bool CoverArtPreload
        {
            get => _dataSetting.GetValue(nameof(CoverArtPreload), false);
            set => _dataSetting.SetValue(nameof(CoverArtPreload), value);
        }

        public int MetadataOsdMessageMaxLength
        {
            get => _dataSetting.GetValue(nameof(MetadataOsdMessageMaxLength), 96);
            set => _dataSetting.SetValue(nameof(MetadataOsdMessageMaxLength), value);
        }

        public string CoverArtNames
        {
            get => _dataSetting.GetValue(nameof(CoverArtNames), "cover;folder;album;front");
            set => _dataSetting.SetValue(nameof(CoverArtNames), value);
        }

        public string CoverArtImageExts
        {
            get => _dataSetting.GetValue(nameof(CoverArtImageExts), "jpg;jpeg;png;bmp;gif;webp");
            set => _dataSetting.SetValue(nameof(CoverArtImageExts), value);
        }

        // ===== Plugin script options (script-opts/*.conf) =====
        public bool MetadataOsdEnableForAudio
        {
            get => _dataSetting.GetValue(nameof(MetadataOsdEnableForAudio), true);
            set => _dataSetting.SetValue(nameof(MetadataOsdEnableForAudio), value);
        }

        public bool MetadataOsdEnableForAudioWithAlbumArt
        {
            get => _dataSetting.GetValue(nameof(MetadataOsdEnableForAudioWithAlbumArt), true);
            set => _dataSetting.SetValue(nameof(MetadataOsdEnableForAudioWithAlbumArt), value);
        }

        public bool MetadataOsdAutohideForAudio
        {
            get => _dataSetting.GetValue(nameof(MetadataOsdAutohideForAudio), false);
            set => _dataSetting.SetValue(nameof(MetadataOsdAutohideForAudio), value);
        }

        public bool MetadataOsdAutohideForAudioWithAlbumArt
        {
            get => _dataSetting.GetValue(nameof(MetadataOsdAutohideForAudioWithAlbumArt), false);
            set => _dataSetting.SetValue(nameof(MetadataOsdAutohideForAudioWithAlbumArt), value);
        }

        public string HdrOverrideMode
        {
            get => _dataSetting.GetValue(nameof(HdrOverrideMode), string.Empty);
            set => _dataSetting.SetValue(nameof(HdrOverrideMode), value);
        }
    }
}
