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
    public partial class AppSettings
    {
        private readonly IDataSetting _dataSetting;
        private readonly CachedDataSetting _settingsCache;

        public AppSettings()
        {
            IDataSetting backend = PackageHelper.IsPackaged
                ? new AppDataSetting("app-settings")
                : new UnpackageAppDataSetting("app");
            _settingsCache = new CachedDataSetting(backend);
            _dataSetting = _settingsCache;
            MigrateLegacyDefaults();
        }

        /// <summary>Flushes pending in-memory settings to the backend (used on app exit).</summary>
        public void Flush() => _settingsCache.Flush();

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
                // Runs on every launch regardless of the schema version, gated
                // by its own flag, so already-migrated installs still receive
                // the per-style split.
                MigrateBarOrderPerStyle();

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

        /// <summary>
        /// The custom order used to be a single value shared by every layout
        /// style, which made editing one style reorder the other. Split it per
        /// style; migrate the old value into both once. Runs on every launch
        /// (before the schema-version gate) so existing installs get the split.
        /// </summary>
        private void MigrateBarOrderPerStyle()
        {
            const string barOrderMigratedKey = "ControlBarOrderStyleMigrated";
            if (!_dataSetting.GetValue(barOrderMigratedKey, false))
            {
                // Legacy registry key name kept as a literal: the property was
                // removed, only this one-time migration still reads it.
                var legacyOrder = _dataSetting.GetValue("ControlBarCustomOrder", string.Empty);
                if (!string.IsNullOrEmpty(legacyOrder))
                {
                    if (string.IsNullOrEmpty(_dataSetting.GetValue(nameof(ControlBarCustomOrderClassic), string.Empty)))
                    {
                        _dataSetting.SetValue(nameof(ControlBarCustomOrderClassic), legacyOrder);
                    }
                    if (string.IsNullOrEmpty(_dataSetting.GetValue(nameof(ControlBarCustomOrderModernX), string.Empty)))
                    {
                        _dataSetting.SetValue(nameof(ControlBarCustomOrderModernX), legacyOrder);
                    }
                }
                _dataSetting.SetValue(barOrderMigratedKey, true);
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

        public string ThemeType
        {
            get => _dataSetting.GetValue(nameof(ThemeType), ThemeType_Auto);
            set => _dataSetting.SetValue(nameof(ThemeType), value);
        }

        public const string BackdropType_Acrylic = "Acrylic";

        public const string BackdropType_Mica = "Mica";

        public const string BackdropType_None = "None";

        public string BackdropType
        {
            get => _dataSetting.GetValue(nameof(BackdropType), BackdropType_Mica);
            set => _dataSetting.SetValue(nameof(BackdropType), value);
        }

        /// <summary>Recently picked colors for the color-picker controls, semicolon-separated hex values.</summary>
        public string ThemeRecentColors
        {
            get => _dataSetting.GetValue(nameof(ThemeRecentColors), string.Empty);
            set => _dataSetting.SetValue(nameof(ThemeRecentColors), value);
        }

        /// <summary>UI font family for the app interface. Empty follows the system font.</summary>
        public string UiFont
        {
            get => _dataSetting.GetValue(nameof(UiFont), string.Empty);
            set => _dataSetting.SetValue(nameof(UiFont), value);
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

        public int LastVideoVolume
        {
            get => _dataSetting.GetValue(nameof(LastVideoVolume), 50);
            set => _dataSetting.SetValue(nameof(LastVideoVolume), value);
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

        public int Volume
        {
            get => _dataSetting.GetValue(nameof(Volume), 100);
            set => _dataSetting.SetValue(nameof(Volume), value);
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

        public double Speed
        {
            get => _dataSetting.GetValue(nameof(Speed), 1.0);
            set => _dataSetting.SetValue(nameof(Speed), value);
        }

        /// <summary>截图目录：默认 Windows 官方推荐位置 图片\Screenshots（C:\Users\&lt;用户&gt;\Pictures\Screenshots）。</summary>
        private static readonly string DefaultScreenshotDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");

        public string DScale
        {
            get => _dataSetting.GetValue(nameof(DScale), "bicubic");
            set => _dataSetting.SetValue(nameof(DScale), value);
        }

        public bool InverseToneMapping
        {
            get => _dataSetting.GetValue(nameof(InverseToneMapping), false);
            set => _dataSetting.SetValue(nameof(InverseToneMapping), value);
        }

        public string Icc3dlutSize
        {
            get => _dataSetting.GetValue(nameof(Icc3dlutSize), "auto");
            set => _dataSetting.SetValue(nameof(Icc3dlutSize), value);
        }

        public bool StartFullscreen
        {
            get => _dataSetting.GetValue(nameof(StartFullscreen), false);
            set => _dataSetting.SetValue(nameof(StartFullscreen), value);
        }

        public string WindowTitle
        {
            get => _dataSetting.GetValue(nameof(WindowTitle), string.Empty);
            set => _dataSetting.SetValue(nameof(WindowTitle), value);
        }

        private static string LanguageDefaultSubtitleFont()
        {
            return AppContext.AppSetting.CurrentLanguage switch
            {
                "zh-CN" => "Microsoft YaHei",
                "zh-TW" => "Microsoft JhengHei",
                "ja-JP" => "Yu Gothic UI",
                "ko-KR" => "Malgun Gothic",
                _ => "Segoe UI",
            };
        }

        public bool AlwaysOnTop
        {
            get => _dataSetting.GetValue(nameof(AlwaysOnTop), false);
            set => _dataSetting.SetValue(nameof(AlwaysOnTop), value);
        }

        /// <summary>
        /// Display peak luminance in nits; 0 = auto-detect from the monitor
        /// (fallback 1000 when detection is unavailable).
        /// </summary>
        public int DisplayPeak
        {
            get => _dataSetting.GetValue(nameof(DisplayPeak), 0);
            set => _dataSetting.SetValue(nameof(DisplayPeak), value);
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

        public bool InputIme
        {
            get => _dataSetting.GetValue(nameof(InputIme), true);
            set => _dataSetting.SetValue(nameof(InputIme), value);
        }

        public int IccForceContrast
        {
            get => _dataSetting.GetValue(nameof(IccForceContrast), 0);
            set => _dataSetting.SetValue(nameof(IccForceContrast), value);
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

        /// <summary>Keep the video aspect ratio while resizing the PiP window (both the native and pointer resize paths).</summary>
        public bool WindowPiPAspectRatioLock
        {
            get => _dataSetting.GetValue(nameof(WindowPiPAspectRatioLock), true);
            set => _dataSetting.SetValue(nameof(WindowPiPAspectRatioLock), value);
        }

        /// <summary>Screen corner the PiP window claims on entry ("top-left"/"top-right"/"bottom-left"/"bottom-right").</summary>
        public string WindowPiPAnchor
        {
            get => _dataSetting.GetValue(nameof(WindowPiPAnchor), "bottom-right");
            set => _dataSetting.SetValue(nameof(WindowPiPAnchor), value);
        }

        /// <summary>Keep the video aspect ratio while drag-resizing the main window.</summary>
        public bool WindowAspectRatioLock
        {
            get => _dataSetting.GetValue(nameof(WindowAspectRatioLock), true);
            set => _dataSetting.SetValue(nameof(WindowAspectRatioLock), value);
        }

        /// <summary>Last PiP window position+size ("x,y,w,h"); empty restores the default bottom-right placement.</summary>
        /// <summary>Show the top overlay buttons (back / exit) in the PiP window.</summary>
        public bool WindowPiPShowTopButtons
        {
            get => _dataSetting.GetValue(nameof(WindowPiPShowTopButtons), true);
            set => _dataSetting.SetValue(nameof(WindowPiPShowTopButtons), value);
        }

        /// <summary>Show the hover control bar at the bottom of the PiP window.</summary>
        public bool WindowPiPShowControls
        {
            get => _dataSetting.GetValue(nameof(WindowPiPShowControls), true);
            set => _dataSetting.SetValue(nameof(WindowPiPShowControls), value);
        }

        /// <summary>PiP window opacity (0.2..1), used for the quick-settings slider.</summary>
        public double WindowPiPOpacity
        {
            get => _dataSetting.GetValue(nameof(WindowPiPOpacity), 1.0);
            set => _dataSetting.SetValue(nameof(WindowPiPOpacity), value);
        }

        /// <summary>Auto-resize window on video resolution change.</summary>
        public bool AutoWindowResize { get => _dataSetting.GetValue(nameof(AutoWindowResize), true); set => _dataSetting.SetValue(nameof(AutoWindowResize), value); }

        /// <summary>Picture brightness adjustment (-100..100).</summary>
        /// <summary>Picture contrast adjustment (-100..100).</summary>
        /// <summary>Picture saturation adjustment (-100..100).</summary>
        /// <summary>Picture gamma adjustment (-100..100).</summary>
        /// <summary>Picture hue adjustment (-100..100).</summary>
        /// <summary>Sharpening strength (0..5, gpu-next only).</summary>
        /// <summary>Auto-resize window on video resolution change.</summary>
        /// <summary>Decoder-level stereo downmix for multichannel audio.</summary>
        public string ThumbnailPreviewWidth
        {
            get => _dataSetting.GetValue(nameof(ThumbnailPreviewWidth), "248");
            set => _dataSetting.SetValue(nameof(ThumbnailPreviewWidth), value);
        }

        /// <summary>
        /// Preview refresh interval while hovering, ms (40..600). 150 default:
        /// every tick is a keyframe seek of the preview instance whose decode
        /// and I/O compete with playback — 25/s (the old default) made the
        /// main video stutter on slower storage; ~7/s keeps the strip fluid.
        /// </summary>
        public int ThumbnailUpdateInterval
        {
            get => _dataSetting.GetValue(nameof(ThumbnailUpdateInterval), 150);
            set => _dataSetting.SetValue(nameof(ThumbnailUpdateInterval), value);
        }

        /// <summary>Playlist panel width in pixels (280-420), persisted across sessions.</summary>
        public int PlaylistWidth
        {
            get => _dataSetting.GetValue(nameof(PlaylistWidth), 320);
            set => _dataSetting.SetValue(nameof(PlaylistWidth), value);
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

        /// <summary>Custom order for the 原版 (classic) layout — separate from
        /// the 居中 one so editing one style never reorders the other.</summary>
        public string ControlBarCustomOrderClassic
        {
            get => _dataSetting.GetValue(nameof(ControlBarCustomOrderClassic), string.Empty);
            set => _dataSetting.SetValue(nameof(ControlBarCustomOrderClassic), value);
        }

        /// <summary>Custom order for the 居中 (modernx) layout.</summary>
        public string ControlBarCustomOrderModernX
        {
            get => _dataSetting.GetValue(nameof(ControlBarCustomOrderModernX), string.Empty);
            set => _dataSetting.SetValue(nameof(ControlBarCustomOrderModernX), value);
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

        public string VideoExts
        {
            get => _dataSetting.GetValue(nameof(VideoExts), "3g2,3gp,avi,flv,m2ts,m4v,mj2,mkv,mov,mp4,mpeg,mpg,ogv,rmvb,ts,webm,wmv,y4m,rm");
            set => _dataSetting.SetValue(nameof(VideoExts), value);
        }

        public string ImageExts
        {
            get => _dataSetting.GetValue(nameof(ImageExts), "avif,bmp,gif,heic,heif,j2k,jp2,jpeg,jpg,jxl,png,qoi,tga,tif,tiff,webp");
            set => _dataSetting.SetValue(nameof(ImageExts), value);
        }

        public string AudioExts
        {
            get => _dataSetting.GetValue(nameof(AudioExts), "aac,ac3,aiff,ape,au,dts,eac3,flac,m4a,mka,mp3,oga,ogg,ogm,opus,thd,wav,wma,wv");
            set => _dataSetting.SetValue(nameof(AudioExts), value);
        }

        public double OverrideDisplayFps
        {
            get => _dataSetting.GetValue(nameof(OverrideDisplayFps), 0.0);
            set => _dataSetting.SetValue(nameof(OverrideDisplayFps), value);
        }

        /// <summary>Per-id control-bar zone overrides for the modernx layout ("id:0,id:2").</summary>
        public string ControlBarZonesModernX
        {
            get => _dataSetting.GetValue(nameof(ControlBarZonesModernX), string.Empty);
            set => _dataSetting.SetValue(nameof(ControlBarZonesModernX), value);
        }

        /// <summary>Per-id control-bar zone overrides for the classic layout ("id:0,id:2").</summary>
        public string ControlBarZonesClassic
        {
            get => _dataSetting.GetValue(nameof(ControlBarZonesClassic), string.Empty);
            set => _dataSetting.SetValue(nameof(ControlBarZonesClassic), value);
        }
    }
}
