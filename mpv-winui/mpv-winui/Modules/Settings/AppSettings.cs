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
            get => _dataSetting.GetValue(nameof(SubFont), "Source Han Sans SC");
            set => _dataSetting.SetValue(nameof(SubFont), value);
        }

        public string SubCodePage
        {
            get => _dataSetting.GetValue(nameof(SubCodePage), "GB18030");
            set => _dataSetting.SetValue(nameof(SubCodePage), value);
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
    }
}
