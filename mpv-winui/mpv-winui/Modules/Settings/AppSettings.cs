using mpv_winui.Modules.AppModel;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace mpv_winui.Modules.Settings
{
    // MpvSettings.ApplyAll enumerates the properties reflectively; keep them
    // available under trimming/AOT.
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
    public class AppSettings
    {
        private readonly IDataSetting _dataSetting;

        public AppSettings()
        {
            _dataSetting = PackageHelper.IsPackaged ? new AppDataSetting("app-settings") : new UnpackageAppDataSetting("app");
            MigrateLegacyDefaults();
        }

        /// <summary>
        /// Schema version of the stored settings. Migrations run once per
        /// version: read the stored version, run every step above it, then
        /// persist the new version. The per-feature "Migrated" flags inside
        /// the steps keep them idempotent for installs upgraded before the
        /// version key existed (they read version 0 and re-run the steps,
        /// which no-op on the flags).
        /// </summary>
        private const int CurrentSettingsSchemaVersion = 1;
        private const string SettingsSchemaVersionKey = "SettingsSchemaVersion";

        private void MigrateLegacyDefaults()
        {
            var fromVersion = _dataSetting.GetValue(SettingsSchemaVersionKey, 0);
            try
            {
                if (fromVersion >= CurrentSettingsSchemaVersion)
                {
                    return;
                }
                if (fromVersion < 1)
                {
                    MigrateToVersion1();
                }
            }
            finally
            {
                // Persist the version even when a step early-returns, so the
                // migration runs exactly once.
                _dataSetting.SetValue(SettingsSchemaVersionKey, CurrentSettingsSchemaVersion);
            }
        }

        private void MigrateToVersion1()
        {
            // The control bar icon checklist used to be stored in a single
            // value shared by every layout style. Keep the old value for
            // existing users and copy it to both per-style keys once.
            // This must run before the early return below so already-migrated
            // installs still receive the per-style split.
            const string barIconsMigratedKey = "ControlBarIconsStyleMigrated";
            if (!_dataSetting.GetValue(barIconsMigratedKey, false))
            {
                var legacy = _dataSetting.GetValue(nameof(ControlBarHiddenIcons), string.Empty);
                if (!string.IsNullOrEmpty(legacy))
                {
                    if (string.IsNullOrEmpty(_dataSetting.GetValue(nameof(ControlBarHiddenIconsClassic), string.Empty)))
                    {
                        _dataSetting.SetValue(nameof(ControlBarHiddenIconsClassic), legacy);
                    }
                    if (string.IsNullOrEmpty(_dataSetting.GetValue(nameof(ControlBarHiddenIconsModernX), string.Empty)))
                    {
                        _dataSetting.SetValue(nameof(ControlBarHiddenIconsModernX), legacy);
                    }
                }
                _dataSetting.SetValue(barIconsMigratedKey, true);
            }

            // Older builds defaulted thumbfast's inactivity quit to 0, which
            // left orphaned mpv.exe preview processes after abrupt app exits.
            // The new default is 30 seconds; treat a stored 0 as "unset" once
            // so existing installs pick up the new default (users can still
            // set 0 explicitly afterwards).
            const string thumbfastQuitMigratedKey = "ThumbfastQuitInactivityMigrated";
            if (!_dataSetting.GetValue(thumbfastQuitMigratedKey, false))
            {
                if (_dataSetting.GetValue(nameof(ThumbfastQuitAfterInactivity), 0) == 0)
                {
                    _dataSetting.ResetKeys((string[])[nameof(ThumbfastQuitAfterInactivity)]);
                }
                _dataSetting.SetValue(thumbfastQuitMigratedKey, true);
            }

            const string migratedKey = "SubFontLanguageDefaultMigrated";
            if (_dataSetting.GetValue(migratedKey, false))
            {
                return;
            }

            if (string.Equals(_dataSetting.GetValue(nameof(SubFont), string.Empty), "sans-serif", StringComparison.Ordinal))
            {
                _dataSetting.SetValue(nameof(SubFont), string.Empty);
            }
            _dataSetting.SetValue(migratedKey, true);

            const string codePageKey = "SubCodePageAutoMigrated";
            if (!_dataSetting.GetValue(codePageKey, false))
            {
                if (string.Equals(_dataSetting.GetValue(nameof(SubCodePage), string.Empty), "GB18030", StringComparison.Ordinal))
                {
                    _dataSetting.SetValue(nameof(SubCodePage), string.Empty);
                }
                _dataSetting.SetValue(codePageKey, true);
            }
        }

        /// <summary>Clears every stored setting; defaults apply on the next read.</summary>
        public void ResetAll()
        {
            _dataSetting.ResetAll();
        }

        public void ResetKeys(IEnumerable<string> keys)
        {
            _dataSetting.ResetKeys(keys);
        }

        public IReadOnlyDictionary<string, object> ExportAll()
        {
            return _dataSetting.ExportAll();
        }

        public void ImportAll(IReadOnlyDictionary<string, object> values)
        {
            _dataSetting.ImportAll(values);
        }

        public const string ThemeType_Auto = "Auto";
        public const string ThemeType_Light = "Light";
        public const string ThemeType_Dark = "Dark";
        public const string ThemeType_Custom = "Custom";
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

        /// <summary>Accent tint color for the window backdrop (#RRGGBB / #AARRGGBB). Empty follows the system accent.</summary>
        public string ThemeAccentColor
        {
            get => _dataSetting.GetValue(nameof(ThemeAccentColor), string.Empty);
            set => _dataSetting.SetValue(nameof(ThemeAccentColor), value);
        }

        /// <summary>Backdrop transparency, 0 (opaque) to 100 (fully transparent).</summary>
        public int ThemeOpacity
        {
            get => _dataSetting.GetValue(nameof(ThemeOpacity), 30);
            set => _dataSetting.SetValue(nameof(ThemeOpacity), value);
        }

        /// <summary>Recently picked theme colors, semicolon-separated hex values.</summary>
        public string ThemeRecentColors
        {
            get => _dataSetting.GetValue(nameof(ThemeRecentColors), string.Empty);
            set => _dataSetting.SetValue(nameof(ThemeRecentColors), value);
        }

        /// <summary>Backdrop luminosity/brightness, 0 (dark) to 100 (bright).</summary>
        public int ThemeLuminosity
        {
            get => _dataSetting.GetValue(nameof(ThemeLuminosity), 40);
            set => _dataSetting.SetValue(nameof(ThemeLuminosity), value);
        }

        /// <summary>UI font family for the app interface. Empty follows the system font.</summary>
        public string UiFont
        {
            get => _dataSetting.GetValue(nameof(UiFont), string.Empty);
            set => _dataSetting.SetValue(nameof(UiFont), value);
        }

        /// <summary>Whether to show the playback-start OSD message.</summary>
        public bool ShowOsdPlayingMsg
        {
            get => _dataSetting.GetValue(nameof(ShowOsdPlayingMsg), false);
            set => _dataSetting.SetValue(nameof(ShowOsdPlayingMsg), value);
        }

        /// <summary>Log every mpv command sent by the app (testing).</summary>
        public bool TestMpvCommandLog
        {
            get => _dataSetting.GetValue(nameof(TestMpvCommandLog), false);
            set => _dataSetting.SetValue(nameof(TestMpvCommandLog), value);
        }

        /// <summary>Test signal to play: off / testsrc2 / sine.</summary>
        public string TestSignal
        {
            get => _dataSetting.GetValue(nameof(TestSignal), "off");
            set => _dataSetting.SetValue(nameof(TestSignal), value);
        }

        /// <summary>Ephemeral test OSD trigger; always reads as false.</summary>
        public bool TestOsdMessage
        {
            get => false;
            set => _ = value;
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

        /// <summary>Comma-separated recent settings-search queries (most recent first).</summary>
        public string SettingsSearchHistory
        {
            get => _dataSetting.GetValue(nameof(SettingsSearchHistory), string.Empty);
            set => _dataSetting.SetValue(nameof(SettingsSearchHistory), value);
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
            get => _dataSetting.GetValue(nameof(SubAssOverride), "scale");
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
            get
            {
                var stored = _dataSetting.GetValue(nameof(SubFont), string.Empty);
                return string.IsNullOrEmpty(stored) ? LanguageDefaultSubtitleFont() : stored;
            }
            set => _dataSetting.SetValue(nameof(SubFont), value);
        }

        private static string LanguageDefaultSubtitleFont()
        {
            return AppContext.AppSetting.CurrentLanguage switch
            {
                "zh-CN" => "Microsoft YaHei",
                "ja-JP" => "Yu Gothic UI",
                "ko-KR" => "Malgun Gothic",
                _ => "Segoe UI",
            };
        }

        public string SubFontProvider
        {
            get => _dataSetting.GetValue(nameof(SubFontProvider), "auto");
            set => _dataSetting.SetValue(nameof(SubFontProvider), value);
        }

        public string SubCodePage
        {
            get => _dataSetting.GetValue(nameof(SubCodePage), "auto");
            set => _dataSetting.SetValue(nameof(SubCodePage), value);
        }

        public string SubFontFile
        {
            get => _dataSetting.GetValue(nameof(SubFontFile), string.Empty);
            set => _dataSetting.SetValue(nameof(SubFontFile), value);
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
            set => _dataSetting.SetValue(nameof(HwdecCodecs), value);
        }

        public bool InputIme
        {
            get => _dataSetting.GetValue(nameof(InputIme), true);
            set => _dataSetting.SetValue(nameof(InputIme), value);
        }

        public string TargetColorspaceHintMode
        {
            get => _dataSetting.GetValue(nameof(TargetColorspaceHintMode), string.Empty);
            set => _dataSetting.SetValue(nameof(TargetColorspaceHintMode), value);
        }

        public bool TargetColorspaceHintStrict
        {
            get => _dataSetting.GetValue(nameof(TargetColorspaceHintStrict), true);
            set => _dataSetting.SetValue(nameof(TargetColorspaceHintStrict), value);
        }

        public string GamutMappingMode
        {
            get => _dataSetting.GetValue(nameof(GamutMappingMode), string.Empty);
            set => _dataSetting.SetValue(nameof(GamutMappingMode), value);
        }

        public string SubColor
        {
            get => _dataSetting.GetValue(nameof(SubColor), string.Empty);
            set => _dataSetting.SetValue(nameof(SubColor), value);
        }

        public bool ImageSubsVideoResolution
        {
            get => _dataSetting.GetValue(nameof(ImageSubsVideoResolution), false);
            set => _dataSetting.SetValue(nameof(ImageSubsVideoResolution), value);
        }

        public string AudioFilePaths
        {
            get => _dataSetting.GetValue(nameof(AudioFilePaths), string.Empty);
            set => _dataSetting.SetValue(nameof(AudioFilePaths), value);
        }

        public int VideoSyncMaxVideoChange
        {
            get => _dataSetting.GetValue(nameof(VideoSyncMaxVideoChange), 5);
            set => _dataSetting.SetValue(nameof(VideoSyncMaxVideoChange), value);
        }

        public bool ScreenshotJpegSourceChroma
        {
            get => _dataSetting.GetValue(nameof(ScreenshotJpegSourceChroma), true);
            set => _dataSetting.SetValue(nameof(ScreenshotJpegSourceChroma), value);
        }

        public int ScreenshotPngFilter
        {
            get => _dataSetting.GetValue(nameof(ScreenshotPngFilter), 5);
            set => _dataSetting.SetValue(nameof(ScreenshotPngFilter), value);
        }

        public bool ScreenshotWebpLossless
        {
            get => _dataSetting.GetValue(nameof(ScreenshotWebpLossless), true);
            set => _dataSetting.SetValue(nameof(ScreenshotWebpLossless), value);
        }

        public int ScreenshotWebpCompression
        {
            get => _dataSetting.GetValue(nameof(ScreenshotWebpCompression), 0);
            set => _dataSetting.SetValue(nameof(ScreenshotWebpCompression), value);
        }

        public int ScreenshotJxlDistance
        {
            get => _dataSetting.GetValue(nameof(ScreenshotJxlDistance), 0);
            set => _dataSetting.SetValue(nameof(ScreenshotJxlDistance), value);
        }

        public int ScreenshotJxlEffort
        {
            get => _dataSetting.GetValue(nameof(ScreenshotJxlEffort), 4);
            set => _dataSetting.SetValue(nameof(ScreenshotJxlEffort), value);
        }

        public double OsdBarHeight
        {
            get => _dataSetting.GetValue(nameof(OsdBarHeight), 1.8);
            set => _dataSetting.SetValue(nameof(OsdBarHeight), value);
        }

        public double OsdBlur
        {
            get => _dataSetting.GetValue(nameof(OsdBlur), 0.0);
            set => _dataSetting.SetValue(nameof(OsdBlur), value);
        }

        public double OsdOutlineSize
        {
            get => _dataSetting.GetValue(nameof(OsdOutlineSize), 0.8);
            set => _dataSetting.SetValue(nameof(OsdOutlineSize), value);
        }

        public bool OsdFractions
        {
            get => _dataSetting.GetValue(nameof(OsdFractions), true);
            set => _dataSetting.SetValue(nameof(OsdFractions), value);
        }

        public string WatchLaterOptions
        {
            get => _dataSetting.GetValue(nameof(WatchLaterOptions), "start,vid,aid,sid");
            set => _dataSetting.SetValue(nameof(WatchLaterOptions), value);
        }

        public bool SubScaleSigns
        {
            get => _dataSetting.GetValue(nameof(SubScaleSigns), true);
            set => _dataSetting.SetValue(nameof(SubScaleSigns), value);
        }

        public bool ThumbfastNetwork
        {
            get => _dataSetting.GetValue(nameof(ThumbfastNetwork), false);
            set => _dataSetting.SetValue(nameof(ThumbfastNetwork), value);
        }

        public int ThumbfastMinDuration
        {
            get => _dataSetting.GetValue(nameof(ThumbfastMinDuration), 0);
            set => _dataSetting.SetValue(nameof(ThumbfastMinDuration), value);
        }

        public int ThumbfastPrecise
        {
            get => _dataSetting.GetValue(nameof(ThumbfastPrecise), 0);
            set => _dataSetting.SetValue(nameof(ThumbfastPrecise), value);
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

        public string BackgroundTileColor0
        {
            get => _dataSetting.GetValue(nameof(BackgroundTileColor0), "#B4B4B4");
            set => _dataSetting.SetValue(nameof(BackgroundTileColor0), value);
        }

        public string BackgroundTileColor1
        {
            get => _dataSetting.GetValue(nameof(BackgroundTileColor1), "#DCDCDC");
            set => _dataSetting.SetValue(nameof(BackgroundTileColor1), value);
        }

        public int BackgroundTileSize
        {
            get => _dataSetting.GetValue(nameof(BackgroundTileSize), 128);
            set => _dataSetting.SetValue(nameof(BackgroundTileSize), value);
        }

        public string IccProfile
        {
            get => _dataSetting.GetValue(nameof(IccProfile), string.Empty);
            set => _dataSetting.SetValue(nameof(IccProfile), value);
        }

        public int IccForceContrast
        {
            get => _dataSetting.GetValue(nameof(IccForceContrast), 0);
            set => _dataSetting.SetValue(nameof(IccForceContrast), value);
        }

        public string VideoUnscaled
        {
            get => _dataSetting.GetValue(nameof(VideoUnscaled), string.Empty);
            set => _dataSetting.SetValue(nameof(VideoUnscaled), value);
        }

        public string SubBackColor
        {
            get => _dataSetting.GetValue(nameof(SubBackColor), string.Empty);
            set => _dataSetting.SetValue(nameof(SubBackColor), value);
        }

        public string SubBorderColor
        {
            get => _dataSetting.GetValue(nameof(SubBorderColor), string.Empty);
            set => _dataSetting.SetValue(nameof(SubBorderColor), value);
        }

        public string SubAssUseVideoData
        {
            get => _dataSetting.GetValue(nameof(SubAssUseVideoData), string.Empty);
            set => _dataSetting.SetValue(nameof(SubAssUseVideoData), value);
        }

        public string SubAssVideoAspectOverride
        {
            get => _dataSetting.GetValue(nameof(SubAssVideoAspectOverride), string.Empty);
            set => _dataSetting.SetValue(nameof(SubAssVideoAspectOverride), value);
        }

        public string SubAssVsfilterColorCompat
        {
            get => _dataSetting.GetValue(nameof(SubAssVsfilterColorCompat), string.Empty);
            set => _dataSetting.SetValue(nameof(SubAssVsfilterColorCompat), value);
        }

        public string AudioGapless
        {
            get => _dataSetting.GetValue(nameof(AudioGapless), "no");
            set => _dataSetting.SetValue(nameof(AudioGapless), value);
        }

        public bool AudioWaitOpen
        {
            get => _dataSetting.GetValue(nameof(AudioWaitOpen), false);
            set => _dataSetting.SetValue(nameof(AudioWaitOpen), value);
        }

        public string OsdColor
        {
            get => _dataSetting.GetValue(nameof(OsdColor), string.Empty);
            set => _dataSetting.SetValue(nameof(OsdColor), value);
        }

        public string YtdlRawOptionsAppend
        {
            get => _dataSetting.GetValue(nameof(YtdlRawOptionsAppend), string.Empty);
            set => _dataSetting.SetValue(nameof(YtdlRawOptionsAppend), value);
        }

        public string YtdlFormat
        {
            get => _dataSetting.GetValue(nameof(YtdlFormat), string.Empty);
            set => _dataSetting.SetValue(nameof(YtdlFormat), value);
        }

        public string YtdlPath
        {
            get => _dataSetting.GetValue(nameof(YtdlPath), string.Empty);
            set => _dataSetting.SetValue(nameof(YtdlPath), value);
        }

        public bool YtdlTryFirst
        {
            get => _dataSetting.GetValue(nameof(YtdlTryFirst), false);
            set => _dataSetting.SetValue(nameof(YtdlTryFirst), value);
        }

        public bool YtdlAllFormats
        {
            get => _dataSetting.GetValue(nameof(YtdlAllFormats), true);
            set => _dataSetting.SetValue(nameof(YtdlAllFormats), value);
        }

        public bool YtdlUseManifests
        {
            get => _dataSetting.GetValue(nameof(YtdlUseManifests), false);
            set => _dataSetting.SetValue(nameof(YtdlUseManifests), value);
        }

        public string YtdlThumbnails
        {
            get => _dataSetting.GetValue(nameof(YtdlThumbnails), "none");
            set => _dataSetting.SetValue(nameof(YtdlThumbnails), value);
        }

        public string YtdlExclude
        {
            get => _dataSetting.GetValue(nameof(YtdlExclude), string.Empty);
            set => _dataSetting.SetValue(nameof(YtdlExclude), value);
        }

        public string UserAgent
        {
            get => _dataSetting.GetValue(nameof(UserAgent), string.Empty);
            set => _dataSetting.SetValue(nameof(UserAgent), value);
        }

        public string Referrer
        {
            get => _dataSetting.GetValue(nameof(Referrer), string.Empty);
            set => _dataSetting.SetValue(nameof(Referrer), value);
        }

        public string HttpHeaderFields
        {
            get => _dataSetting.GetValue(nameof(HttpHeaderFields), string.Empty);
            set => _dataSetting.SetValue(nameof(HttpHeaderFields), value);
        }

        public string HttpProxy
        {
            get => _dataSetting.GetValue(nameof(HttpProxy), string.Empty);
            set => _dataSetting.SetValue(nameof(HttpProxy), value);
        }

        public string CookiesFile
        {
            get => _dataSetting.GetValue(nameof(CookiesFile), string.Empty);
            set => _dataSetting.SetValue(nameof(CookiesFile), value);
        }

        public bool TlsVerify
        {
            get => _dataSetting.GetValue(nameof(TlsVerify), true);
            set => _dataSetting.SetValue(nameof(TlsVerify), value);
        }

        public int NetworkTimeout
        {
            get => _dataSetting.GetValue(nameof(NetworkTimeout), 60);
            set => _dataSetting.SetValue(nameof(NetworkTimeout), value);
        }

        public int CurlMaxRedirects
        {
            get => _dataSetting.GetValue(nameof(CurlMaxRedirects), 16);
            set => _dataSetting.SetValue(nameof(CurlMaxRedirects), value);
        }

        public int CurlMaxRetries
        {
            get => _dataSetting.GetValue(nameof(CurlMaxRetries), 5);
            set => _dataSetting.SetValue(nameof(CurlMaxRetries), value);
        }

        public int CurlConnectTimeout
        {
            get => _dataSetting.GetValue(nameof(CurlConnectTimeout), 30);
            set => _dataSetting.SetValue(nameof(CurlConnectTimeout), value);
        }

        public int CurlBufferSize
        {
            get => _dataSetting.GetValue(nameof(CurlBufferSize), 4 * 1024 * 1024);
            set => _dataSetting.SetValue(nameof(CurlBufferSize), value);
        }

        public int CurlMaxRequestSize
        {
            get => _dataSetting.GetValue(nameof(CurlMaxRequestSize), 0);
            set => _dataSetting.SetValue(nameof(CurlMaxRequestSize), value);
        }

        // Picture-in-picture is a session state: it must never be persisted,
        // so the app always starts in the normal window.
        public bool WindowPiP
        {
            // Persisted like every other setting: the startup flow applies
            // PiP only after mpv is initialized, so a stored "true" must
            // survive restarts for that path to exist.
            get => _dataSetting.GetValue(nameof(WindowPiP), false);
            set => _dataSetting.SetValue(nameof(WindowPiP), value);
        }

        public string WindowPiPSize
        {
            get => _dataSetting.GetValue(nameof(WindowPiPSize), "480x270");
            set => _dataSetting.SetValue(nameof(WindowPiPSize), value);
        }

        /// <summary>Last PiP window position+size ("x,y,w,h"); empty restores the default bottom-right placement.</summary>
        public string WindowPiPRect
        {
            get => _dataSetting.GetValue(nameof(WindowPiPRect), string.Empty);
            set => _dataSetting.SetValue(nameof(WindowPiPRect), value);
        }

        /// <summary>PiP window opacity (0.2..1), used for the quick-settings slider.</summary>
        public double WindowPiPOpacity
        {
            get => _dataSetting.GetValue(nameof(WindowPiPOpacity), 1.0);
            set => _dataSetting.SetValue(nameof(WindowPiPOpacity), value);
        }

        /// <summary>Checks the GitHub Releases API for updates at startup.</summary>
        public bool CheckForUpdates
        {
            get => _dataSetting.GetValue(nameof(CheckForUpdates), true);
            set => _dataSetting.SetValue(nameof(CheckForUpdates), value);
        }

        public bool WindowStartMaximized
        {
            get => _dataSetting.GetValue(nameof(WindowStartMaximized), false);
            set => _dataSetting.SetValue(nameof(WindowStartMaximized), value);
        }

        public bool WindowRememberSize
        {
            get => _dataSetting.GetValue(nameof(WindowRememberSize), true);
            set => _dataSetting.SetValue(nameof(WindowRememberSize), value);
        }

        public string FileAssociationExts
        {
            get => _dataSetting.GetValue(
                nameof(FileAssociationExts),
                ".mp4;.mkv;.avi;.mov;.wmv;.flv;.webm;.m4v;.mpg;.mpeg;.ts;.m2ts;.3gp;.ogv;.rm;.rmvb;.mp3;.flac;.wav;.aac;.m4a;.ogg;.opus;.wma");
            set => _dataSetting.SetValue(nameof(FileAssociationExts), value);
        }

        public string ControlBarLayout
        {
            get => _dataSetting.GetValue(nameof(ControlBarLayout), "classic");
            set => _dataSetting.SetValue(nameof(ControlBarLayout), value);
        }

        /// <summary>
        /// Comma-separated custom order of the reorderable control-bar buttons
        /// (volume,tracks,random,speed,aspect,fullwindow,fullscreen,pip).
        /// Empty = the layout's built-in order; the transport buttons
        /// (play/prev/next/skips) are always fixed.
        /// </summary>
        public string ControlBarCustomOrder
        {
            get => _dataSetting.GetValue(nameof(ControlBarCustomOrder), string.Empty);
            set => _dataSetting.SetValue(nameof(ControlBarCustomOrder), value);
        }

        public string ControlBarHiddenIcons
        {
            get => _dataSetting.GetValue(nameof(ControlBarHiddenIcons), string.Empty);
            set => _dataSetting.SetValue(nameof(ControlBarHiddenIcons), value);
        }

        public string ControlBarHiddenIconsClassic
        {
            get => _dataSetting.GetValue(nameof(ControlBarHiddenIconsClassic), string.Empty);
            set => _dataSetting.SetValue(nameof(ControlBarHiddenIconsClassic), value);
        }

        public string ControlBarHiddenIconsModernX
        {
            get => _dataSetting.GetValue(nameof(ControlBarHiddenIconsModernX), string.Empty);
            set => _dataSetting.SetValue(nameof(ControlBarHiddenIconsModernX), value);
        }

        public string InputIpcServer
        {
            get => _dataSetting.GetValue(nameof(InputIpcServer), string.Empty);
            set => _dataSetting.SetValue(nameof(InputIpcServer), value);
        }

        public string DirectoryFilterTypes
        {
            get => _dataSetting.GetValue(nameof(DirectoryFilterTypes), "video,audio");
            set => _dataSetting.SetValue(nameof(DirectoryFilterTypes), value);
        }

        public string VideoExts
        {
            get => _dataSetting.GetValue(nameof(VideoExts), "3g2,3gp,avi,flv,m2ts,m4v,mj2,mkv,mov,mp4,mpeg,mpg,ogv,rmvb,ts,webm,wmv,y4m,rm");
            set => _dataSetting.SetValue(nameof(VideoExts), value);
        }

        public int ThumbfastMaxWidth
        {
            get => _dataSetting.GetValue(nameof(ThumbfastMaxWidth), 200);
            set => _dataSetting.SetValue(nameof(ThumbfastMaxWidth), value);
        }

        public int ThumbfastMaxHeight
        {
            get => _dataSetting.GetValue(nameof(ThumbfastMaxHeight), 2000);
            set => _dataSetting.SetValue(nameof(ThumbfastMaxHeight), value);
        }

        public bool ThumbfastSpawnFirst
        {
            get => _dataSetting.GetValue(nameof(ThumbfastSpawnFirst), false);
            set => _dataSetting.SetValue(nameof(ThumbfastSpawnFirst), value);
        }

        public int ThumbfastThreads
        {
            get => _dataSetting.GetValue(nameof(ThumbfastThreads), 6);
            set => _dataSetting.SetValue(nameof(ThumbfastThreads), value);
        }

        public double ThumbfastFrequency
        {
            get => _dataSetting.GetValue(nameof(ThumbfastFrequency), 0.15);
            set => _dataSetting.SetValue(nameof(ThumbfastFrequency), value);
        }

        public string D3d11Adapter
        {
            get => _dataSetting.GetValue(nameof(D3d11Adapter), string.Empty);
            set => _dataSetting.SetValue(nameof(D3d11Adapter), value);
        }

        public string ImageExts
        {
            get => _dataSetting.GetValue(nameof(ImageExts), "avif,bmp,gif,heic,heif,j2k,jp2,jpeg,jpg,jxl,png,qoi,svg,tga,tif,tiff,webp");
            set => _dataSetting.SetValue(nameof(ImageExts), value);
        }

        public string AudioExts
        {
            get => _dataSetting.GetValue(nameof(AudioExts), "aac,ac3,aiff,ape,au,dts,eac3,flac,m4a,mka,mp3,oga,ogg,ogm,opus,thd,wav,wma,wv");
            set => _dataSetting.SetValue(nameof(AudioExts), value);
        }

        public string OsdOutlineColor
        {
            get => _dataSetting.GetValue(nameof(OsdOutlineColor), string.Empty);
            set => _dataSetting.SetValue(nameof(OsdOutlineColor), value);
        }

        public string GlslShadersAppend
        {
            get => _dataSetting.GetValue(nameof(GlslShadersAppend), string.Empty);
            set => _dataSetting.SetValue(nameof(GlslShadersAppend), value);
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

        public bool ThumbfastDirectIo
        {
            get => _dataSetting.GetValue(nameof(ThumbfastDirectIo), true);
            set => _dataSetting.SetValue(nameof(ThumbfastDirectIo), value);
        }

        public int ThumbfastQuitAfterInactivity
        {
            get => _dataSetting.GetValue(nameof(ThumbfastQuitAfterInactivity), 30);
            set => _dataSetting.SetValue(nameof(ThumbfastQuitAfterInactivity), value);
        }

        public int AudioBuffer
        {
            get => _dataSetting.GetValue(nameof(AudioBuffer), 0);
            set => _dataSetting.SetValue(nameof(AudioBuffer), value);
        }

        public string ScreenshotAvifEncoder
        {
            get => _dataSetting.GetValue(nameof(ScreenshotAvifEncoder), string.Empty);
            set => _dataSetting.SetValue(nameof(ScreenshotAvifEncoder), value);
        }
    }
}
