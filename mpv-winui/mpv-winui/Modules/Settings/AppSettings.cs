using mpv_winui.Modules.AppModel;
using System;
using System.IO;

namespace mpv_winui.Modules.Settings
{
    public class AppSettings
    {
        private readonly IDataSetting _dataSetting;

        public AppSettings()
        {
            _dataSetting = PackageHelper.IsPackaged ? new AppDataSetting("app-settings") : new UnpackageAppDataSetting("app");
        }

        public const string ThemeType_Auto = "Auto";
        public const string ThemeType_Light = "Light";
        public const string ThemeType_Dark = "Dark";
        public string ThemeType
        {
            get => _dataSetting.GetValue(nameof(ThemeType), ThemeType_Auto);
            set => _dataSetting.SetValue(nameof(ThemeType), value);
        }

        public const string BackdropType_Acrylic = "Acrylic";
        public const string BackdropType_Mica = "Mica";
        public string BackdropType
        {
            get => _dataSetting.GetValue(nameof(BackdropType), BackdropType_Acrylic);
            set => _dataSetting.SetValue(nameof(BackdropType), value);
        }

        public bool EnableDebugLog
        {
            get => _dataSetting.GetValue(nameof(EnableDebugLog), false);
            set => _dataSetting.SetValue(nameof(EnableDebugLog), value);
        }

        public string CurrentLanguage
        {
            get => _dataSetting.GetValue(nameof(CurrentLanguage), string.Empty);
            set => _dataSetting.SetValue(nameof(CurrentLanguage), value);
        }

        public ulong AppVersion
        {
            get => _dataSetting.GetValue(nameof(AppVersion), (ulong)0);
            set => _dataSetting.SetValue(nameof(AppVersion), value);
        }

        public int PatchVersion
        {
            get => _dataSetting.GetValue(nameof(PatchVersion), 0);
            set => _dataSetting.SetValue(nameof(PatchVersion), value);
        }

        public int LastVideoVolume
        {
            get => _dataSetting.GetValue(nameof(LastVideoVolume), 50);
            set => _dataSetting.SetValue(nameof(LastVideoVolume), value);
        }

        public int LastAudioVolume
        {
            get => _dataSetting.GetValue(nameof(LastAudioVolume), 50);
            set => _dataSetting.SetValue(nameof(LastAudioVolume), value);
        }

        public string WindowPositionAndSize
        {
            get => _dataSetting.GetValue(nameof(WindowPositionAndSize), string.Empty);
            set => _dataSetting.SetValue(nameof(WindowPositionAndSize), value);
        }

        public bool EnableVideoPreview
        {
            get => _dataSetting.GetValue(nameof(EnableVideoPreview), false);
            set => _dataSetting.SetValue(nameof(EnableVideoPreview), value);
        }

        public string Hwdec
        {
            get => _dataSetting.GetValue(nameof(Hwdec), "auto");
            set => _dataSetting.SetValue(nameof(Hwdec), value);
        }

        public int VolumeMax
        {
            get => _dataSetting.GetValue(nameof(VolumeMax), 100);
            set => _dataSetting.SetValue(nameof(VolumeMax), value);
        }

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

        public int Volume
        {
            get => _dataSetting.GetValue(nameof(Volume), 100);
            set => _dataSetting.SetValue(nameof(Volume), value);
        }

        public string CacheDirectory
        {
            get => _dataSetting.GetValue(nameof(CacheDirectory), string.Empty);
            set => _dataSetting.SetValue(nameof(CacheDirectory), value);
        }

        public string WatchLaterDir
        {
            get => _dataSetting.GetValue(nameof(WatchLaterDir), string.Empty);
            set => _dataSetting.SetValue(nameof(WatchLaterDir), value);
        }

        public string IccCacheDir
        {
            get => _dataSetting.GetValue(nameof(IccCacheDir), string.Empty);
            set => _dataSetting.SetValue(nameof(IccCacheDir), value);
        }

        public string GpuShaderCacheDir
        {
            get => _dataSetting.GetValue(nameof(GpuShaderCacheDir), string.Empty);
            set => _dataSetting.SetValue(nameof(GpuShaderCacheDir), value);
        }

        public string Deinterlace
        {
            get => _dataSetting.GetValue(nameof(Deinterlace), "auto");
            set => _dataSetting.SetValue(nameof(Deinterlace), value);
        }

        public string AspectRatio
        {
            get => _dataSetting.GetValue(nameof(AspectRatio), "auto");
            set => _dataSetting.SetValue(nameof(AspectRatio), value);
        }

        public int SubFontSize
        {
            get => _dataSetting.GetValue(nameof(SubFontSize), 42);
            set => _dataSetting.SetValue(nameof(SubFontSize), value);
        }

        public double SubDelay
        {
            get => _dataSetting.GetValue(nameof(SubDelay), 0.0);
            set => _dataSetting.SetValue(nameof(SubDelay), value);
        }

        public double Speed
        {
            get => _dataSetting.GetValue(nameof(Speed), 1.0);
            set => _dataSetting.SetValue(nameof(Speed), value);
        }

        public int SubPos
        {
            get => _dataSetting.GetValue(nameof(SubPos), 100);
            set => _dataSetting.SetValue(nameof(SubPos), value);
        }

        public string AudioLanguage
        {
            get => _dataSetting.GetValue(nameof(AudioLanguage), string.Empty);
            set => _dataSetting.SetValue(nameof(AudioLanguage), value);
        }

        public string SubtitleLanguage
        {
            get => _dataSetting.GetValue(nameof(SubtitleLanguage), string.Empty);
            set => _dataSetting.SetValue(nameof(SubtitleLanguage), value);
        }

        public string AudioDevice
        {
            get => _dataSetting.GetValue(nameof(AudioDevice), "auto");
            set => _dataSetting.SetValue(nameof(AudioDevice), value);
        }

        /// <summary>截图目录：默认 Windows 官方推荐位置 图片\Screenshots（C:\Users\&lt;用户&gt;\Pictures\Screenshots）。</summary>
        private static readonly string DefaultScreenshotDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");

        public string ScreenshotDirectory
        {
            get
            {
                var v = _dataSetting.GetValue(nameof(ScreenshotDirectory), string.Empty);
                return string.IsNullOrWhiteSpace(v) ? DefaultScreenshotDirectory : v;
            }
            set => _dataSetting.SetValue(nameof(ScreenshotDirectory), value);
        }

        public string ScreenshotTemplate
        {
            get => _dataSetting.GetValue(nameof(ScreenshotTemplate), string.Empty);
            set => _dataSetting.SetValue(nameof(ScreenshotTemplate), value);
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

        public string ScreenshotFormat
        {
            get => _dataSetting.GetValue(nameof(ScreenshotFormat), "png");
            set => _dataSetting.SetValue(nameof(ScreenshotFormat), value);
        }

        public int ScreenshotJpegQuality
        {
            get => _dataSetting.GetValue(nameof(ScreenshotJpegQuality), 90);
            set => _dataSetting.SetValue(nameof(ScreenshotJpegQuality), value);
        }

        public string VideoSync
        {
            get => _dataSetting.GetValue(nameof(VideoSync), "audio");
            set => _dataSetting.SetValue(nameof(VideoSync), value);
        }

        public bool Interpolation
        {
            get => _dataSetting.GetValue(nameof(Interpolation), false);
            set => _dataSetting.SetValue(nameof(Interpolation), value);
        }

        public bool CorrectDownscaling
        {
            get => _dataSetting.GetValue(nameof(CorrectDownscaling), true);
            set => _dataSetting.SetValue(nameof(CorrectDownscaling), value);
        }

        public string Scale
        {
            get => _dataSetting.GetValue(nameof(Scale), "lanczos");
            set => _dataSetting.SetValue(nameof(Scale), value);
        }

        public string DScale
        {
            get => _dataSetting.GetValue(nameof(DScale), "bicubic");
            set => _dataSetting.SetValue(nameof(DScale), value);
        }

        public string VideoRotate
        {
            get => _dataSetting.GetValue(nameof(VideoRotate), "no");
            set => _dataSetting.SetValue(nameof(VideoRotate), value);
        }

        public bool Deband
        {
            get => _dataSetting.GetValue(nameof(Deband), false);
            set => _dataSetting.SetValue(nameof(Deband), value);
        }

        public bool LinearDownscaling
        {
            get => _dataSetting.GetValue(nameof(LinearDownscaling), true);
            set => _dataSetting.SetValue(nameof(LinearDownscaling), value);
        }

        public bool SigmoidUpscaling
        {
            get => _dataSetting.GetValue(nameof(SigmoidUpscaling), true);
            set => _dataSetting.SetValue(nameof(SigmoidUpscaling), value);
        }

        public string ToneMapping
        {
            get => _dataSetting.GetValue(nameof(ToneMapping), "bt.2390");
            set => _dataSetting.SetValue(nameof(ToneMapping), value);
        }

        public string DitherDepth
        {
            get => _dataSetting.GetValue(nameof(DitherDepth), "no");
            set => _dataSetting.SetValue(nameof(DitherDepth), value);
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

        public string VideoOutputLevels
        {
            get => _dataSetting.GetValue(nameof(VideoOutputLevels), "auto");
            set => _dataSetting.SetValue(nameof(VideoOutputLevels), value);
        }

        public string VideoDecodeDirect
        {
            get => _dataSetting.GetValue(nameof(VideoDecodeDirect), "auto");
            set => _dataSetting.SetValue(nameof(VideoDecodeDirect), value);
        }

        public bool IccProfileAuto
        {
            get => _dataSetting.GetValue(nameof(IccProfileAuto), false);
            set => _dataSetting.SetValue(nameof(IccProfileAuto), value);
        }

        public string Icc3dlutSize
        {
            get => _dataSetting.GetValue(nameof(Icc3dlutSize), "auto");
            set => _dataSetting.SetValue(nameof(Icc3dlutSize), value);
        }

        public int DemuxerMaxBytes
        {
            get => _dataSetting.GetValue(nameof(DemuxerMaxBytes), 1024);
            set => _dataSetting.SetValue(nameof(DemuxerMaxBytes), value);
        }

        public string AudioChannels
        {
            get => _dataSetting.GetValue(nameof(AudioChannels), "auto");
            set => _dataSetting.SetValue(nameof(AudioChannels), value);
        }

        public bool AudioExclusive
        {
            get => _dataSetting.GetValue(nameof(AudioExclusive), false);
            set => _dataSetting.SetValue(nameof(AudioExclusive), value);
        }

        public bool AudioPitchCorrection
        {
            get => _dataSetting.GetValue(nameof(AudioPitchCorrection), true);
            set => _dataSetting.SetValue(nameof(AudioPitchCorrection), value);
        }

        public bool AudioNormalizeDownmix
        {
            get => _dataSetting.GetValue(nameof(AudioNormalizeDownmix), false);
            set => _dataSetting.SetValue(nameof(AudioNormalizeDownmix), value);
        }

        public string AudioFileAuto
        {
            get => _dataSetting.GetValue(nameof(AudioFileAuto), "fuzzy");
            set => _dataSetting.SetValue(nameof(AudioFileAuto), value);
        }

        public string AudioDisplay
        {
            get => _dataSetting.GetValue(nameof(AudioDisplay), "embedded-first");
            set => _dataSetting.SetValue(nameof(AudioDisplay), value);
        }

        public double AudioDelay
        {
            get => _dataSetting.GetValue(nameof(AudioDelay), 0.0);
            set => _dataSetting.SetValue(nameof(AudioDelay), value);
        }

        public string SubAssOverride
        {
            get => _dataSetting.GetValue(nameof(SubAssOverride), "force");
            set => _dataSetting.SetValue(nameof(SubAssOverride), value);
        }

        public double SubBlur
        {
            get => _dataSetting.GetValue(nameof(SubBlur), 0.0);
            set => _dataSetting.SetValue(nameof(SubBlur), value);
        }

        public string SubAuto
        {
            get => _dataSetting.GetValue(nameof(SubAuto), "fuzzy");
            set => _dataSetting.SetValue(nameof(SubAuto), value);
        }

        public string SubFont
        {
            get => _dataSetting.GetValue(nameof(SubFont), "sans-serif");
            set => _dataSetting.SetValue(nameof(SubFont), value);
        }

        public string SubFontProvider
        {
            get => _dataSetting.GetValue(nameof(SubFontProvider), "auto");
            set => _dataSetting.SetValue(nameof(SubFontProvider), value);
        }

        public string SubCodePage
        {
            get => _dataSetting.GetValue(nameof(SubCodePage), "GB18030");
            set => _dataSetting.SetValue(nameof(SubCodePage), value);
        }

        public bool SubAssScaleWithWindow
        {
            get => _dataSetting.GetValue(nameof(SubAssScaleWithWindow), false);
            set => _dataSetting.SetValue(nameof(SubAssScaleWithWindow), value);
        }

        public string BlendSubtitles
        {
            get => _dataSetting.GetValue(nameof(BlendSubtitles), "no");
            set => _dataSetting.SetValue(nameof(BlendSubtitles), value);
        }

        public string SubFallback
        {
            get => _dataSetting.GetValue(nameof(SubFallback), "default");
            set => _dataSetting.SetValue(nameof(SubFallback), value);
        }

        public double SubOutlineSize
        {
            get => _dataSetting.GetValue(nameof(SubOutlineSize), 1.5);
            set => _dataSetting.SetValue(nameof(SubOutlineSize), value);
        }

        public double SubShadowOffset
        {
            get => _dataSetting.GetValue(nameof(SubShadowOffset), 2.0);
            set => _dataSetting.SetValue(nameof(SubShadowOffset), value);
        }

        public bool SubEmbeddedFonts
        {
            get => _dataSetting.GetValue(nameof(SubEmbeddedFonts), true);
            set => _dataSetting.SetValue(nameof(SubEmbeddedFonts), value);
        }

        public bool SubUseMargins
        {
            get => _dataSetting.GetValue(nameof(SubUseMargins), true);
            set => _dataSetting.SetValue(nameof(SubUseMargins), value);
        }

        public bool SubAssForceMargins
        {
            get => _dataSetting.GetValue(nameof(SubAssForceMargins), true);
            set => _dataSetting.SetValue(nameof(SubAssForceMargins), value);
        }

        public bool StretchImageSubsToScreen
        {
            get => _dataSetting.GetValue(nameof(StretchImageSubsToScreen), true);
            set => _dataSetting.SetValue(nameof(StretchImageSubsToScreen), value);
        }

        public int OsdFontSize
        {
            get => _dataSetting.GetValue(nameof(OsdFontSize), 20);
            set => _dataSetting.SetValue(nameof(OsdFontSize), value);
        }

        public string OsdFont
        {
            get => _dataSetting.GetValue(nameof(OsdFont), "sans-serif");
            set => _dataSetting.SetValue(nameof(OsdFont), value);
        }

        public string OsdOnSeek
        {
            get => _dataSetting.GetValue(nameof(OsdOnSeek), "msg");
            set => _dataSetting.SetValue(nameof(OsdOnSeek), value);
        }

        public int OsdDuration
        {
            get => _dataSetting.GetValue(nameof(OsdDuration), 2000);
            set => _dataSetting.SetValue(nameof(OsdDuration), value);
        }

        public bool VsrAutoEnabled
        {
            get => _dataSetting.GetValue(nameof(VsrAutoEnabled), true);
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

        public int CacheSecs
        {
            get => _dataSetting.GetValue(nameof(CacheSecs), 0);
            set => _dataSetting.SetValue(nameof(CacheSecs), value);
        }

        public int ScreenshotPngCompression
        {
            get => _dataSetting.GetValue(nameof(ScreenshotPngCompression), 4);
            set => _dataSetting.SetValue(nameof(ScreenshotPngCompression), value);
        }

        public int ScreenshotWebpQuality
        {
            get => _dataSetting.GetValue(nameof(ScreenshotWebpQuality), 100);
            set => _dataSetting.SetValue(nameof(ScreenshotWebpQuality), value);
        }

        public bool ScreenshotHighBitDepth
        {
            get => _dataSetting.GetValue(nameof(ScreenshotHighBitDepth), false);
            set => _dataSetting.SetValue(nameof(ScreenshotHighBitDepth), value);
        }

        public bool ScreenshotTagColorspace
        {
            get => _dataSetting.GetValue(nameof(ScreenshotTagColorspace), true);
            set => _dataSetting.SetValue(nameof(ScreenshotTagColorspace), value);
        }

        public bool ScreenshotSw
        {
            get => _dataSetting.GetValue(nameof(ScreenshotSw), false);
            set => _dataSetting.SetValue(nameof(ScreenshotSw), value);
        }

        public bool AlwaysOnTop
        {
            get => _dataSetting.GetValue(nameof(AlwaysOnTop), false);
            set => _dataSetting.SetValue(nameof(AlwaysOnTop), value);
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

        public bool Ytdl
        {
            get => _dataSetting.GetValue(nameof(Ytdl), true);
            set => _dataSetting.SetValue(nameof(Ytdl), value);
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

        public string Cscale
        {
            get => _dataSetting.GetValue(nameof(Cscale), "lanczos");
            set => _dataSetting.SetValue(nameof(Cscale), value);
        }

        public string Tscale
        {
            get => _dataSetting.GetValue(nameof(Tscale), "oversample");
            set => _dataSetting.SetValue(nameof(Tscale), value);
        }

        public bool LinearUpscaling
        {
            get => _dataSetting.GetValue(nameof(LinearUpscaling), false);
            set => _dataSetting.SetValue(nameof(LinearUpscaling), value);
        }

        public string Dither
        {
            get => _dataSetting.GetValue(nameof(Dither), "fruit");
            set => _dataSetting.SetValue(nameof(Dither), value);
        }

        public double Panscan
        {
            get => _dataSetting.GetValue(nameof(Panscan), 0.0);
            set => _dataSetting.SetValue(nameof(Panscan), value);
        }

        public string SubFilePaths
        {
            get => _dataSetting.GetValue(nameof(SubFilePaths), "sub;Subs;subtitles");
            set => _dataSetting.SetValue(nameof(SubFilePaths), value);
        }

        public int SubHdrPeak
        {
            get => _dataSetting.GetValue(nameof(SubHdrPeak), 100);
            set => _dataSetting.SetValue(nameof(SubHdrPeak), value);
        }

        public int ImageSubsHdrPeak
        {
            get => _dataSetting.GetValue(nameof(ImageSubsHdrPeak), 10000);
            set => _dataSetting.SetValue(nameof(ImageSubsHdrPeak), value);
        }

        public string SubAssStyleOverrides
        {
            get => _dataSetting.GetValue(nameof(SubAssStyleOverrides), string.Empty);
            set => _dataSetting.SetValue(nameof(SubAssStyleOverrides), value);
        }

        public string OsdPlayingMsg
        {
            get => _dataSetting.GetValue(nameof(OsdPlayingMsg), "${filename}");
            set => _dataSetting.SetValue(nameof(OsdPlayingMsg), value);
        }

        public int OsdPlayingMsgDuration
        {
            get => _dataSetting.GetValue(nameof(OsdPlayingMsgDuration), 3000);
            set => _dataSetting.SetValue(nameof(OsdPlayingMsgDuration), value);
        }

        public int OsdBarWidth
        {
            get => _dataSetting.GetValue(nameof(OsdBarWidth), 100);
            set => _dataSetting.SetValue(nameof(OsdBarWidth), value);
        }

        public string TargetColorspaceHint
        {
            get => _dataSetting.GetValue(nameof(TargetColorspaceHint), "yes");
            set => _dataSetting.SetValue(nameof(TargetColorspaceHint), value);
        }

        public string TargetPrim
        {
            get => _dataSetting.GetValue(nameof(TargetPrim), string.Empty);
            set => _dataSetting.SetValue(nameof(TargetPrim), value);
        }

        public string TargetTrc
        {
            get => _dataSetting.GetValue(nameof(TargetTrc), string.Empty);
            set => _dataSetting.SetValue(nameof(TargetTrc), value);
        }

        public int TargetPeak
        {
            get => _dataSetting.GetValue(nameof(TargetPeak), 0);
            set => _dataSetting.SetValue(nameof(TargetPeak), value);
        }

        public bool IccCache
        {
            get => _dataSetting.GetValue(nameof(IccCache), true);
            set => _dataSetting.SetValue(nameof(IccCache), value);
        }

        public bool GpuShaderCache
        {
            get => _dataSetting.GetValue(nameof(GpuShaderCache), true);
            set => _dataSetting.SetValue(nameof(GpuShaderCache), value);
        }

        public int DemuxerMaxBackBytes
        {
            get => _dataSetting.GetValue(nameof(DemuxerMaxBackBytes), 512);
            set => _dataSetting.SetValue(nameof(DemuxerMaxBackBytes), value);
        }

        public bool HdrAutoLog
        {
            get => _dataSetting.GetValue(nameof(HdrAutoLog), false);
            set => _dataSetting.SetValue(nameof(HdrAutoLog), value);
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

        public int ThumbfastQuality
        {
            get => _dataSetting.GetValue(nameof(ThumbfastQuality), 1);
            set => _dataSetting.SetValue(nameof(ThumbfastQuality), value);
        }

        public string D3d11OutputCsp
        {
            get => _dataSetting.GetValue(nameof(D3d11OutputCsp), string.Empty);
            set => _dataSetting.SetValue(nameof(D3d11OutputCsp), value);
        }

        public bool D3d11ExclusiveFs
        {
            get => _dataSetting.GetValue(nameof(D3d11ExclusiveFs), false);
            set => _dataSetting.SetValue(nameof(D3d11ExclusiveFs), value);
        }

        public bool D3d11Flip
        {
            get => _dataSetting.GetValue(nameof(D3d11Flip), true);
            set => _dataSetting.SetValue(nameof(D3d11Flip), value);
        }

        public string HwdecCodecs
        {
            get => _dataSetting.GetValue(nameof(HwdecCodecs), "all");
            set => _dataSetting.GetValue(nameof(HwdecCodecs), value);
        }

        public bool InputIme
        {
            get => _dataSetting.GetValue(nameof(InputIme), true);
            set => _dataSetting.GetValue(nameof(InputIme), value);
        }

        public string TargetColorspaceHintMode
        {
            get => _dataSetting.GetValue(nameof(TargetColorspaceHintMode), string.Empty);
            set => _dataSetting.GetValue(nameof(TargetColorspaceHintMode), value);
        }

        public bool TargetColorspaceHintStrict
        {
            get => _dataSetting.GetValue(nameof(TargetColorspaceHintStrict), true);
            set => _dataSetting.GetValue(nameof(TargetColorspaceHintStrict), value);
        }

        public string GamutMappingMode
        {
            get => _dataSetting.GetValue(nameof(GamutMappingMode), string.Empty);
            set => _dataSetting.GetValue(nameof(GamutMappingMode), value);
        }

        public string SubColor
        {
            get => _dataSetting.GetValue(nameof(SubColor), string.Empty);
            set => _dataSetting.GetValue(nameof(SubColor), value);
        }

        public bool ImageSubsVideoResolution
        {
            get => _dataSetting.GetValue(nameof(ImageSubsVideoResolution), false);
            set => _dataSetting.GetValue(nameof(ImageSubsVideoResolution), value);
        }

        public string AudioFilePaths
        {
            get => _dataSetting.GetValue(nameof(AudioFilePaths), string.Empty);
            set => _dataSetting.GetValue(nameof(AudioFilePaths), value);
        }

        public int VideoSyncMaxVideoChange
        {
            get => _dataSetting.GetValue(nameof(VideoSyncMaxVideoChange), 5);
            set => _dataSetting.GetValue(nameof(VideoSyncMaxVideoChange), value);
        }

        public bool ScreenshotJpegSourceChroma
        {
            get => _dataSetting.GetValue(nameof(ScreenshotJpegSourceChroma), true);
            set => _dataSetting.GetValue(nameof(ScreenshotJpegSourceChroma), value);
        }

        public int ScreenshotPngFilter
        {
            get => _dataSetting.GetValue(nameof(ScreenshotPngFilter), 5);
            set => _dataSetting.GetValue(nameof(ScreenshotPngFilter), value);
        }

        public bool ScreenshotWebpLossless
        {
            get => _dataSetting.GetValue(nameof(ScreenshotWebpLossless), true);
            set => _dataSetting.GetValue(nameof(ScreenshotWebpLossless), value);
        }

        public int ScreenshotWebpCompression
        {
            get => _dataSetting.GetValue(nameof(ScreenshotWebpCompression), 0);
            set => _dataSetting.GetValue(nameof(ScreenshotWebpCompression), value);
        }

        public int ScreenshotJxlDistance
        {
            get => _dataSetting.GetValue(nameof(ScreenshotJxlDistance), 0);
            set => _dataSetting.GetValue(nameof(ScreenshotJxlDistance), value);
        }

        public int ScreenshotJxlEffort
        {
            get => _dataSetting.GetValue(nameof(ScreenshotJxlEffort), 4);
            set => _dataSetting.GetValue(nameof(ScreenshotJxlEffort), value);
        }

        public double OsdBarHeight
        {
            get => _dataSetting.GetValue(nameof(OsdBarHeight), 1.8);
            set => _dataSetting.GetValue(nameof(OsdBarHeight), value);
        }

        public double OsdBlur
        {
            get => _dataSetting.GetValue(nameof(OsdBlur), 0.0);
            set => _dataSetting.GetValue(nameof(OsdBlur), value);
        }

        public double OsdOutlineSize
        {
            get => _dataSetting.GetValue(nameof(OsdOutlineSize), 0.8);
            set => _dataSetting.GetValue(nameof(OsdOutlineSize), value);
        }

        public bool OsdFractions
        {
            get => _dataSetting.GetValue(nameof(OsdFractions), true);
            set => _dataSetting.GetValue(nameof(OsdFractions), value);
        }

        public string WatchLaterOptions
        {
            get => _dataSetting.GetValue(nameof(WatchLaterOptions), "start,vid,aid,sid");
            set => _dataSetting.GetValue(nameof(WatchLaterOptions), value);
        }

        public bool SubScaleSigns
        {
            get => _dataSetting.GetValue(nameof(SubScaleSigns), true);
            set => _dataSetting.GetValue(nameof(SubScaleSigns), value);
        }

        public bool ThumbfastNetwork
        {
            get => _dataSetting.GetValue(nameof(ThumbfastNetwork), false);
            set => _dataSetting.GetValue(nameof(ThumbfastNetwork), value);
        }

        public int ThumbfastMinDuration
        {
            get => _dataSetting.GetValue(nameof(ThumbfastMinDuration), 0);
            set => _dataSetting.GetValue(nameof(ThumbfastMinDuration), value);
        }

        public int ThumbfastPrecise
        {
            get => _dataSetting.GetValue(nameof(ThumbfastPrecise), 0);
            set => _dataSetting.GetValue(nameof(ThumbfastPrecise), value);
        }

        public bool MetadataOsdShowChapter
        {
            get => _dataSetting.GetValue(nameof(MetadataOsdShowChapter), false);
            set => _dataSetting.GetValue(nameof(MetadataOsdShowChapter), value);
        }

        public bool CoverArtAlwaysScan
        {
            get => _dataSetting.GetValue(nameof(CoverArtAlwaysScan), false);
            set => _dataSetting.GetValue(nameof(CoverArtAlwaysScan), value);
        }
    }
}
