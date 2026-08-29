using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.FileSystem;
using mpv_winui.Modules.Language;
using mpv_winui.Modules.Player;
using mpv_winui.Modules.Settings.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Storage;
using Microsoft.Windows.Storage.Pickers;

namespace mpv_winui.Modules.Settings;

public sealed partial class SettingsPage
{
private List<Option> BuildSettings()
    {
        var program = AppContext.AppLang.SettingsCategoryProgram;
        var playback = AppContext.AppLang.SettingsCategoryPlayback;
        var video = AppContext.AppLang.SettingsCategoryVideo;
        var audio = AppContext.AppLang.SettingsCategoryAudio;
        var subtitles = AppContext.AppLang.SettingsCategorySubtitles;
        var window = AppContext.AppLang.SettingsCategoryWindow;
        var network = AppContext.AppLang.SettingsCategoryNetwork;
        var shortcuts = AppContext.AppLang.SettingsCategoryShortcuts;
        var osd = AppContext.AppLang.SettingsCategoryOsd;
        var screenshot = AppContext.AppLang.SettingsCategoryScreenshot;
        var sProgramInterface = AppContext.AppLang.SectionProgramInterface;
        var sProgramLanguageLog = AppContext.AppLang.SectionProgramLanguageLog;
        var sProgramTesting = AppContext.AppLang.SectionProgramTesting;
        var sProgramAssociations = AppContext.AppLang.SectionProgramAssociations;
        var sProgramConfig = AppContext.AppLang.SectionProgramConfig;
        var sWindowPiP = AppContext.AppLang.SectionWindowPiP;
        var sNetworkYtdlp = AppContext.AppLang.SectionNetworkYtdlp;
        var sNetworkHttp = AppContext.AppLang.SectionNetworkHttp;
        var sNetworkCurl = AppContext.AppLang.SectionNetworkCurl;
        var sPlayback = AppContext.AppLang.SectionPlayback;
        var sReversePlayback = AppContext.AppLang.SectionReversePlayback;
        var sPlaybackSeeking = AppContext.AppLang.SectionPlaybackSeeking;
        var sPlaybackSeekPreview = AppContext.AppLang.SectionPlaybackSeekPreview;
        var sTrackSelection = AppContext.AppLang.SectionTrackSelection;
        var sTrackLanguage = AppContext.AppLang.SectionTrackLanguage;
        var sTrackFallback = AppContext.AppLang.SectionTrackFallback;
        var sWatchLaterResume = AppContext.AppLang.SectionWatchLaterResume;
        var sWatchLaterStorage = AppContext.AppLang.SectionWatchLaterStorage;
        var sVideoDecode = AppContext.AppLang.SectionVideoDecode;
        var sVideoImage = AppContext.AppLang.SectionVideoImage;
        var sVideoFilters = AppContext.AppLang.SectionVideoFilters;
        var sAudioOutput = AppContext.AppLang.SectionAudioOutput;
        var sAudioVolume = AppContext.AppLang.SectionAudioVolume;
        var sAudioExternal = AppContext.AppLang.SectionAudioExternal;
        var sAudioCoverArt = AppContext.AppLang.SectionAudioCoverArt;
        var sSubtitleText = AppContext.AppLang.SectionSubtitleText;
        var sSubtitleAss = AppContext.AppLang.SectionSubtitleAss;
        var sSubtitleImage = AppContext.AppLang.SectionSubtitleImage;
        var sSubtitleStyle = AppContext.AppLang.SectionSubtitleStyle;
        var sSubtitlePosition = AppContext.AppLang.SectionSubtitlePosition;
        var sSubtitleBehavior = AppContext.AppLang.SectionSubtitleBehavior;
        var sToneMapping = AppContext.AppLang.SectionToneMapping;
        var sTargetColorspace = AppContext.AppLang.SectionTargetColorspace;
        var sColorManagement = AppContext.AppLang.SectionColorManagement;
        var sOsdAppearance = AppContext.AppLang.SectionOsdAppearance;
        var sOsdBehavior = AppContext.AppLang.SectionOsdBehavior;
        var sOsdPosition = AppContext.AppLang.SectionOsdPosition;
        var sWindow = AppContext.AppLang.SectionWindow;
        var sDemuxerPlaylist = AppContext.AppLang.SectionDemuxerPlaylist;
        var sDemuxerBuffering = AppContext.AppLang.SectionDemuxerBuffering;
        var sCache = AppContext.AppLang.SectionCache;
        var sOsd = AppContext.AppLang.SectionOsd;
        var sOsdMetadata = AppContext.AppLang.SectionOsdMetadata;
        var sScreenshotLocation = AppContext.AppLang.SectionScreenshotLocation;
        var sScreenshotQuality = AppContext.AppLang.SectionScreenshotQuality;
        var sGpuScaling = AppContext.AppLang.SectionGpuScaling;
        var sGpuColor = AppContext.AppLang.SectionGpuColor;
        var sGpuInterpolation = AppContext.AppLang.SectionGpuInterpolation;
        var sGpuBackground = AppContext.AppLang.SectionGpuBackground;
        var sGpuD3d11 = AppContext.AppLang.SectionGpuD3d11;
        var sGpuShaders = AppContext.AppLang.SectionGpuShaders;
        var sVideoSync = AppContext.AppLang.SectionVideoSync;
        var lang = AppContext.AppLang;

        var options = new List<Option>();
        options.AddRange(BuildProgramBehaviorOptions());
        options.AddRange(BuildPlaybackControlOptions());
        options.AddRange(BuildVideoOptions());
        options.AddRange(BuildAudioOptions());
        options.AddRange(BuildSubtitlesOptions());
        options.AddRange(BuildScreenshotOptions());

        options.AddRange(BuildAdvancedOptions());
        options.AddRange(BuildPathFoldersOptions());

        options.AddRange(BuildShortcutOptions(shortcuts));

        foreach (var option in options)
        {
            if (RedundantDescriptions.Contains(option.Key))
            {
                option.Description = null;
            }
            if (NoCustomOptions.Contains(option.Key))
            {
                option.AllowCustom = false;
            }
        }

        // WinUI 3 settings shell: 10 top-level categories. "Input" is merged
        // into Shortcuts and "Testing" items live inside their related
        // categories (Program/OSD/Playback) as experimental sections.
        var categoryOrder = new[]
        {
            program,
            playback,
            video,
            audio,
            subtitles,
            window,
            network,
            shortcuts,
            osd,
            screenshot,
        };

        var optionOrder = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [nameof(AppSettings.ThemeType)] = 0,
            [nameof(AppSettings.BackdropType)] = 1,
            [nameof(AppSettings.UiFont)] = 5,
            [nameof(AppSettings.TestMpvCommandLog)] = 6,
            [nameof(AppSettings.TestOsdMessage)] = 7,
            [nameof(AppSettings.TestSignal)] = 8,
            [nameof(AppSettings.CurrentLanguage)] = 9,
            [nameof(AppSettings.EnableDebugLog)] = 10,
            [nameof(AppSettings.Ytdl)] = 11,
            [nameof(AppSettings.YtdlRawOptionsAppend)] = 12,
            [nameof(AppSettings.Speed)] = 15,
            [nameof(AppSettings.LoopPlaylist)] = 13,
            [nameof(AppSettings.LoopFile)] = 14,
            [nameof(AppSettings.HrSeek)] = 16,
            [nameof(AppSettings.HrSeekFramedrop)] = 17,
            [nameof(AppSettings.SeekHoldEnabled)] = 18,
            [nameof(AppSettings.EnableVideoPreview)] = 19,
            [nameof(AppSettings.ThumbnailPreviewWidth)] = 20,
            [nameof(AppSettings.ThumbnailUpdateInterval)] = 21,
            [nameof(AppSettings.AudioLanguage)] = 31,
            [nameof(AppSettings.SubtitleLanguage)] = 32,
            [nameof(AppSettings.SubFallback)] = 33,
            [nameof(AppSettings.SavePositionOnQuit)] = 34,
            [nameof(AppSettings.ResumePlayback)] = 35,
            [nameof(AppSettings.WatchLaterOptions)] = 36,
            [nameof(AppSettings.WatchLaterDir)] = 37,
            [nameof(AppSettings.Hwdec)] = 38,
            [nameof(AppSettings.HwdecCodecs)] = 39,
            [nameof(AppSettings.VideoDecodeDirect)] = 40,
            [nameof(AppSettings.Deinterlace)] = 41,
            [nameof(AppSettings.VideoRotate)] = 42,
            [nameof(AppSettings.AspectRatio)] = 43,
            [nameof(AppSettings.Panscan)] = 44,
            [nameof(AppSettings.VideoUnscaled)] = 45,
            [nameof(AppSettings.VideoOutputLevels)] = 46,
            [nameof(AppSettings.HdrAutoMode)] = 47,
            [nameof(AppSettings.HdrAutoLog)] = 48,
            [nameof(AppSettings.VsrAutoEnabled)] = 49,
            [nameof(AppSettings.AudioDevice)] = 50,
            [nameof(AppSettings.AudioExclusive)] = 51,
            [nameof(AppSettings.AudioChannels)] = 52,
            [nameof(AppSettings.AudioDelay)] = 53,
            [nameof(AppSettings.AudioBuffer)] = 54,
            [nameof(AppSettings.AudioWaitOpen)] = 55,
            [nameof(AppSettings.AudioPitchCorrection)] = 56,
            [nameof(AppSettings.AudioNormalizeDownmix)] = 57,
            [nameof(AppSettings.AudioGapless)] = 58,
                                    [nameof(AppSettings.VolumeMax)] = 60,
            [nameof(AppSettings.AudioFileAuto)] = 61,
            [nameof(AppSettings.AudioExts)] = 62,
            [nameof(AppSettings.AudioFilePaths)] = 63,
            [nameof(AppSettings.AudioDisplay)] = 64,
            [nameof(AppSettings.CoverArtPreferEmbedded)] = 65,
            [nameof(AppSettings.CoverArtAlwaysScan)] = 66,
            [nameof(AppSettings.CoverArtLoadFromFilesystem)] = 67,
            [nameof(AppSettings.CoverArtPreload)] = 68,
            [nameof(AppSettings.CoverArtNames)] = 69,
            [nameof(AppSettings.CoverArtImageExts)] = 70,
            [nameof(AppSettings.SubFontSize)] = 71,
            [nameof(AppSettings.SubFont)] = 72,
            [nameof(AppSettings.SubFontFile)] = 73,
            [nameof(AppSettings.SubFontProvider)] = 74,
            [nameof(AppSettings.SubCodePage)] = 75,
            [nameof(AppSettings.SubColor)] = 76,
            [nameof(AppSettings.SubBackColor)] = 77,
            [nameof(AppSettings.SubBorderColor)] = 78,
            [nameof(AppSettings.SubOutlineSize)] = 79,
            [nameof(AppSettings.SubShadowOffset)] = 80,
            [nameof(AppSettings.SubBlur)] = 81,
            [nameof(AppSettings.SubPos)] = 82,
            [nameof(AppSettings.SubDelay)] = 83,
            [nameof(AppSettings.SubScaleSigns)] = 84,
            [nameof(AppSettings.SubUseMargins)] = 85,
            [nameof(AppSettings.SubAuto)] = 86,
            [nameof(AppSettings.SubFilePaths)] = 87,
            [nameof(AppSettings.SubHdrPeak)] = 88,
            [nameof(AppSettings.SubAssOverride)] = 89,
            [nameof(AppSettings.SubAssStyleOverrides)] = 90,
            [nameof(AppSettings.SubAssForceMargins)] = 91,
            [nameof(AppSettings.SubAssScaleWithWindow)] = 92,
            [nameof(AppSettings.SubAssUseVideoData)] = 93,
            [nameof(AppSettings.SubAssVideoAspectOverride)] = 94,
            [nameof(AppSettings.SubAssVsfilterColorCompat)] = 95,
            [nameof(AppSettings.SubEmbeddedFonts)] = 96,
            [nameof(AppSettings.BlendSubtitles)] = 97,
            [nameof(AppSettings.StretchImageSubsToScreen)] = 98,
            [nameof(AppSettings.ImageSubsVideoResolution)] = 99,
            [nameof(AppSettings.ImageSubsHdrPeak)] = 100,
            [nameof(AppSettings.AlwaysOnTop)] = 101,
            [nameof(AppSettings.KeepOpen)] = 102,
            [nameof(AppSettings.AutoCreatePlaylist)] = 103,
            [nameof(AppSettings.DirectoryMode)] = 104,
            [nameof(AppSettings.DirectoryFilterTypes)] = 105,
            [nameof(AppSettings.VideoExts)] = 106,
            [nameof(AppSettings.ImageExts)] = 107,
            [nameof(AppSettings.DemuxerMaxBytes)] = 108,
            [nameof(AppSettings.DemuxerMaxBackBytes)] = 109,
            [nameof(AppSettings.DemuxerReadahead)] = 110,
            [nameof(AppSettings.CacheEnabled)] = 111,
            [nameof(AppSettings.CacheSecs)] = 112,
            [nameof(AppSettings.CacheOnDisk)] = 113,
                        [nameof(AppSettings.InputIme)] = 115,
            [nameof(AppSettings.InputIpcServer)] = 116,
            [nameof(AppSettings.OsdFontSize)] = 117,
            [nameof(AppSettings.OsdFont)] = 118,
            [nameof(AppSettings.OsdColor)] = 119,
            [nameof(AppSettings.OsdOutlineColor)] = 120,
            [nameof(AppSettings.OsdOnSeek)] = 121,
            [nameof(AppSettings.OsdDuration)] = 122,
            [nameof(AppSettings.OsdPlayingMsg)] = 123,
            [nameof(AppSettings.OsdPlayingMsgDuration)] = 124,
            [nameof(AppSettings.OsdBarWidth)] = 125,
            [nameof(AppSettings.OsdBarHeight)] = 126,
            [nameof(AppSettings.OsdBlur)] = 127,
            [nameof(AppSettings.OsdOutlineSize)] = 128,
            [nameof(AppSettings.OsdFractions)] = 129,
            [nameof(AppSettings.ShowOsdPlayingMsg)] = 130,
            [nameof(AppSettings.MetadataOsdEnabled)] = 131,
            [nameof(AppSettings.MetadataOsdAutohideTimeout)] = 132,
            [nameof(AppSettings.MetadataOsdShowChapter)] = 133,
            [nameof(AppSettings.MetadataOsdEnableForVideo)] = 134,
            [nameof(AppSettings.MetadataOsdEnableForImage)] = 135,
            [nameof(AppSettings.MetadataOsdAutohideStatusTimeout)] = 136,
            [nameof(AppSettings.MetadataOsdShowAlbumTrack)] = 137,
            [nameof(AppSettings.MetadataOsdMessageMaxLength)] = 138,
            [nameof(AppSettings.ScreenshotDirectory)] = 139,
            [nameof(AppSettings.ScreenshotTemplate)] = 140,
            [nameof(AppSettings.ScreenshotFormat)] = 141,
            [nameof(AppSettings.ScreenshotJpegQuality)] = 142,
            [nameof(AppSettings.ScreenshotJpegSourceChroma)] = 143,
            [nameof(AppSettings.ScreenshotPngCompression)] = 144,
            [nameof(AppSettings.ScreenshotPngFilter)] = 145,
            [nameof(AppSettings.ScreenshotWebpQuality)] = 146,
            [nameof(AppSettings.ScreenshotWebpLossless)] = 147,
            [nameof(AppSettings.ScreenshotWebpCompression)] = 148,
            [nameof(AppSettings.ScreenshotJxlDistance)] = 149,
            [nameof(AppSettings.ScreenshotJxlEffort)] = 150,
            [nameof(AppSettings.ScreenshotAvifEncoder)] = 151,
            [nameof(AppSettings.ScreenshotHighBitDepth)] = 152,
            [nameof(AppSettings.ScreenshotTagColorspace)] = 153,
            [nameof(AppSettings.ScreenshotSw)] = 154,
            [nameof(AppSettings.Scale)] = 155,
            [nameof(AppSettings.DScale)] = 156,
            [nameof(AppSettings.Cscale)] = 157,
            [nameof(AppSettings.Tscale)] = 158,
            [nameof(AppSettings.LinearUpscaling)] = 159,
            [nameof(AppSettings.SigmoidUpscaling)] = 160,
            [nameof(AppSettings.LinearDownscaling)] = 161,
            [nameof(AppSettings.CorrectDownscaling)] = 162,
            [nameof(AppSettings.Deband)] = 163,
            [nameof(AppSettings.Dither)] = 164,
            [nameof(AppSettings.DitherDepth)] = 165,
            [nameof(AppSettings.ToneMapping)] = 166,
            [nameof(AppSettings.TargetColorspaceHint)] = 167,
            [nameof(AppSettings.TargetColorspaceHintMode)] = 168,
            [nameof(AppSettings.TargetColorspaceHintStrict)] = 169,
            [nameof(AppSettings.TargetPrim)] = 170,
            [nameof(AppSettings.TargetTrc)] = 171,
            [nameof(AppSettings.TargetPeak)] = 172,
            [nameof(AppSettings.DisplayPeak)] = 173,
            [nameof(AppSettings.GamutMappingMode)] = 184,
            [nameof(AppSettings.IccProfileAuto)] = 174,
            [nameof(AppSettings.IccProfile)] = 175,
            [nameof(AppSettings.IccForceContrast)] = 176,
            [nameof(AppSettings.Icc3dlutSize)] = 177,
            [nameof(AppSettings.IccCache)] = 178,
            [nameof(AppSettings.IccCacheDir)] = 179,
            [nameof(AppSettings.D3d11OutputCsp)] = 180,
            [nameof(AppSettings.Interpolation)] = 181,
            [nameof(AppSettings.BackgroundTileColor0)] = 182,
            [nameof(AppSettings.BackgroundTileColor1)] = 183,
            [nameof(AppSettings.BackgroundTileSize)] = 184,
            [nameof(AppSettings.D3d11ExclusiveFs)] = 185,
            [nameof(AppSettings.D3d11Flip)] = 186,
            [nameof(AppSettings.D3d11Adapter)] = 187,
            [nameof(AppSettings.GpuShaderCache)] = 188,
            [nameof(AppSettings.GpuShaderCacheDir)] = 189,
            [nameof(AppSettings.GlslShadersAppend)] = 190,
            [nameof(AppSettings.GlslShaders)] = 361,
            [nameof(AppSettings.GlslShaderOpts)] = 362,
            [nameof(AppSettings.WindowTitle)] = 363,
            [nameof(AppSettings.SubHinting)] = 364,
            [nameof(AppSettings.VideoSync)] = 191,
            [nameof(AppSettings.VideoSyncMaxVideoChange)] = 192,
            [nameof(AppSettings.WindowPiP)] = 193,
            [nameof(AppSettings.WindowPiPShowControls)] = 193,
            [nameof(AppSettings.WindowPiPShowTopButtons)] = 193,
            [nameof(AppSettings.WindowPiPSize)] = 194,
            [nameof(AppSettings.WindowPiPAspectRatioLock)] = 194,
            [nameof(AppSettings.WindowPiPAnchor)] = 194,
            [nameof(AppSettings.WindowPiPOpacity)] = 195,
            [nameof(AppSettings.WindowStartMaximized)] = 195,
            [nameof(AppSettings.WindowRememberSize)] = 196,
            [nameof(AppSettings.WindowAspectRatioLock)] = 196,
            [nameof(AppSettings.YtdlFormat)] = 197,
            [nameof(AppSettings.YtdlPath)] = 198,
            [nameof(AppSettings.YtdlTryFirst)] = 199,
            [nameof(AppSettings.YtdlAllFormats)] = 200,
            [nameof(AppSettings.YtdlUseManifests)] = 201,
            [nameof(AppSettings.YtdlThumbnails)] = 202,
            [nameof(AppSettings.YtdlExclude)] = 203,
            [nameof(AppSettings.UserAgent)] = 204,
            [nameof(AppSettings.Referrer)] = 205,
            [nameof(AppSettings.HttpHeaderFields)] = 206,
            [nameof(AppSettings.HttpProxy)] = 207,
            [nameof(AppSettings.CookiesFile)] = 208,
            [nameof(AppSettings.TlsVerify)] = 209,
            [nameof(AppSettings.NetworkTimeout)] = 210,
            [nameof(AppSettings.CurlMaxRedirects)] = 211,
            [nameof(AppSettings.CurlMaxRetries)] = 212,
            [nameof(AppSettings.CurlConnectTimeout)] = 213,
            [nameof(AppSettings.CurlBufferSize)] = 214,
            [nameof(AppSettings.CurlMaxRequestSize)] = 215,
            ["FileAssociationCheckList"] = 216,
            ["ActionUnassociateFiles"] = 217,
            ["ActionExportConfig"] = 218,
            ["ActionImportConfig"] = 219,
            [nameof(AppSettings.CheckForUpdates)] = 220,
            [nameof(AppSettings.ControlBarLayout)] = 220,
            [nameof(AppSettings.ControlBarHiddenIcons)] = 221,
            [nameof(AppSettings.AudioSpdif)] = 300,
            [nameof(AppSettings.Replaygain)] = 301,
            [nameof(AppSettings.OsdLevel)] = 302,
            [nameof(AppSettings.OsdAlignX)] = 303,
            [nameof(AppSettings.OsdAlignY)] = 304,
            [nameof(AppSettings.OsdMarginX)] = 305,
            [nameof(AppSettings.OsdMarginY)] = 306,
            [nameof(AppSettings.ImageDisplayDuration)] = 307,
            [nameof(AppSettings.OverrideDisplayFps)] = 308,
            [nameof(AppSettings.CachePause)] = 309,
            [nameof(AppSettings.PrefetchPlaylist)] = 310,
            [nameof(AppSettings.SubBold)] = 311,
            [nameof(AppSettings.SubItalic)] = 312,
            [nameof(AppSettings.SubAlignX)] = 313,
            [nameof(AppSettings.SubAlignY)] = 314,
            [nameof(AppSettings.SubScaleByWindow)] = 322,
            [nameof(AppSettings.SubLineSpacing)] = 323,
            [nameof(AppSettings.SubJustify)] = 324,
            [nameof(AppSettings.SubClearOnSeek)] = 325,
            [nameof(AppSettings.SubMarginX)] = 315,
            [nameof(AppSettings.SubMarginY)] = 316,
            [nameof(AppSettings.DemuxerHysteresisSecs)] = 317,
            [nameof(AppSettings.DemuxerCacheDir)] = 318,
            [nameof(AppSettings.AudioFormat)] = 340,
            [nameof(AppSettings.AudioSampleRate)] = 341,
            [nameof(AppSettings.AudioStreamSilence)] = 342,
            [nameof(AppSettings.StartFullscreen)] = 343,
            [nameof(AppSettings.D3d11OutputFormat)] = 344,
            [nameof(AppSettings.D3d11SyncInterval)] = 345,
            [nameof(AppSettings.TargetGamut)] = 346,
            [nameof(AppSettings.ToneMappingMaxBoost)] = 347,
            [nameof(AppSettings.HdrComputePeak)] = 348,
            [nameof(AppSettings.HdrPeakDecayRate)] = 349,
            [nameof(AppSettings.HdrSceneThresholdLow)] = 350,
            [nameof(AppSettings.HdrSceneThresholdHigh)] = 351,
            [nameof(AppSettings.HdrContrastRecovery)] = 352,
            [nameof(AppSettings.HdrContrastSmoothness)] = 353,
            [nameof(AppSettings.CachePauseInitial)] = 354,
            [nameof(AppSettings.CachePauseWait)] = 355,
            [nameof(AppSettings.InverseToneMapping)] = 356,
            [nameof(AppSettings.ToneMappingVisualize)] = 357,
            [nameof(AppSettings.D3d11Warp)] = 358,
            [nameof(AppSettings.VideoReversalBuffer)] = 359,
            [nameof(AppSettings.AudioReversalBuffer)] = 360,
            [nameof(AppSettings.MetadataOsdEnableForAudio)] = 320,
            [nameof(AppSettings.MetadataOsdEnableForAudioWithAlbumArt)] = 321,
            [nameof(AppSettings.MetadataOsdAutohideForAudio)] = 322,
            [nameof(AppSettings.MetadataOsdAutohideForAudioWithAlbumArt)] = 323,
            [nameof(AppSettings.HdrOverrideMode)] = 326,
        };

        // Parsed input.conf bindings keep their original order after the capture row.
        var shortcutOrder = 1000;
        foreach (var o in options.Where(o => o.Key.StartsWith("Shortcut:", StringComparison.Ordinal)))
        {
            optionOrder[o.Key] = shortcutOrder++;
        }

        var sectionOrder = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [sProgramInterface] = 0,
            [sProgramLanguageLog] = 1,
            [sTrackSelection] = 2,
            [sPlayback] = 3,
            [sReversePlayback] = 4,
            [sPlaybackSeeking] = 5,
            [sPlaybackSeekPreview] = 6,
            [sWatchLaterResume] = 7,
            [sWatchLaterStorage] = 8,
            [sVideoDecode] = 7,
            [sVideoImage] = 8,
            [sVideoFilters] = 9,
            [sGpuScaling] = 10,
            [sGpuColor] = 11,
            [sGpuInterpolation] = 12,
            [sGpuBackground] = 13,
            [sGpuD3d11] = 14,
            [sGpuShaders] = 15,
            [sVideoSync] = 16,
            [sAudioOutput] = 17,
            [sAudioVolume] = 18,
            [sAudioExternal] = 19,
            [sAudioCoverArt] = 20,
            [sTrackLanguage] = 21,
            [sTrackFallback] = 22,
            [sSubtitleText] = 23,
            [sSubtitleAss] = 24,
            [sSubtitleImage] = 25,
            [sWindow] = 26,
            [sDemuxerPlaylist] = 27,
            [sDemuxerBuffering] = 28,
            [sCache] = 29,
            [sOsd] = 32,
            [sOsdMetadata] = 33,
            [sScreenshotLocation] = 34,
            [sScreenshotQuality] = 35,
            [sProgramTesting] = 36,
            [sNetworkYtdlp] = 39,
            [sNetworkHttp] = 40,
            [sNetworkCurl] = 41,

            [sSubtitleStyle] = 230,
            [sSubtitlePosition] = 231,
            [sSubtitleBehavior] = 232,
            [sToneMapping] = 130,
            [sTargetColorspace] = 131,
            [sColorManagement] = 132,
            [sOsdAppearance] = 320,
            [sOsdBehavior] = 321,
            [sOsdPosition] = 322,            [sWindowPiP] = 42,
            [sProgramAssociations] = 43,
            [sProgramConfig] = 44,
        };

        var sectionMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // program
            [nameof(AppSettings.ThemeType)] = sProgramInterface,
            [nameof(AppSettings.BackdropType)] = sProgramInterface,
            [nameof(AppSettings.UiFont)] = sProgramInterface,
            [nameof(AppSettings.CurrentLanguage)] = sProgramLanguageLog,
            [nameof(AppSettings.CheckForUpdates)] = sProgramConfig,
            // preset keys are option keys, not AppSettings properties
            [nameof(AppSettings.EnableDebugLog)] = sProgramLanguageLog,
            // playback
            [nameof(AppSettings.Speed)] = sPlayback,
            [nameof(AppSettings.LoopPlaylist)] = sPlayback,
            [nameof(AppSettings.LoopFile)] = sPlayback,
            [nameof(AppSettings.VideoReversalBuffer)] = sReversePlayback,
            [nameof(AppSettings.AudioReversalBuffer)] = sReversePlayback,
            [nameof(AppSettings.HrSeek)] = sPlaybackSeeking,
            [nameof(AppSettings.HrSeekFramedrop)] = sPlaybackSeeking,
            [nameof(AppSettings.SeekHoldEnabled)] = sPlaybackSeeking,
            [nameof(AppSettings.EnableVideoPreview)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbnailPreviewWidth)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbnailUpdateInterval)] = sPlaybackSeekPreview,
            // watchLater
            [nameof(AppSettings.SavePositionOnQuit)] = sWatchLaterResume,
            [nameof(AppSettings.ResumePlayback)] = sWatchLaterResume,
            [nameof(AppSettings.WatchLaterOptions)] = sWatchLaterStorage,
            [nameof(AppSettings.WatchLaterDir)] = sWatchLaterStorage,
            // video
            [nameof(AppSettings.Hwdec)] = sVideoDecode,
            [nameof(AppSettings.HwdecCodecs)] = sVideoDecode,
            [nameof(AppSettings.VideoDecodeDirect)] = sVideoDecode,
            [nameof(AppSettings.Deinterlace)] = sVideoImage,
            [nameof(AppSettings.VideoRotate)] = sVideoImage,
            [nameof(AppSettings.AspectRatio)] = sVideoImage,
            [nameof(AppSettings.Panscan)] = sVideoImage,
            [nameof(AppSettings.VideoUnscaled)] = sVideoImage,
            [nameof(AppSettings.VideoOutputLevels)] = sVideoImage,
            [nameof(AppSettings.PictureSharpen)] = sVideoImage,
            [nameof(AppSettings.PictureHue)] = sVideoImage,
            [nameof(AppSettings.PictureGamma)] = sVideoImage,
            [nameof(AppSettings.PictureSaturation)] = sVideoImage,
            [nameof(AppSettings.PictureContrast)] = sVideoImage,
            [nameof(AppSettings.PictureBrightness)] = sVideoImage,
            [nameof(AppSettings.HdrAutoMode)] = sVideoFilters,
            [nameof(AppSettings.HdrOverrideMode)] = sGpuColor,
            [nameof(AppSettings.HdrAutoLog)] = sVideoFilters,
            [nameof(AppSettings.VsrAutoEnabled)] = sVideoFilters,
            [nameof(AppSettings.Scale)] = sGpuScaling,
            [nameof(AppSettings.DScale)] = sGpuScaling,
            [nameof(AppSettings.Cscale)] = sGpuScaling,
            [nameof(AppSettings.Tscale)] = sGpuScaling,
            [nameof(AppSettings.LinearUpscaling)] = sGpuScaling,
            [nameof(AppSettings.SigmoidUpscaling)] = sGpuScaling,
            [nameof(AppSettings.LinearDownscaling)] = sGpuScaling,
            [nameof(AppSettings.CorrectDownscaling)] = sGpuScaling,
            [nameof(AppSettings.Deband)] = sGpuScaling,
            [nameof(AppSettings.Dither)] = sGpuScaling,
            [nameof(AppSettings.DitherDepth)] = sGpuScaling,
            [nameof(AppSettings.ToneMapping)] = sToneMapping,
            [nameof(AppSettings.TargetGamut)] = sTargetColorspace,
            [nameof(AppSettings.ToneMappingMaxBoost)] = sToneMapping,
            [nameof(AppSettings.HdrComputePeak)] = sToneMapping,
            [nameof(AppSettings.HdrPeakDecayRate)] = sToneMapping,
            [nameof(AppSettings.HdrSceneThresholdLow)] = sToneMapping,
            [nameof(AppSettings.HdrSceneThresholdHigh)] = sToneMapping,
            [nameof(AppSettings.HdrContrastRecovery)] = sToneMapping,
            [nameof(AppSettings.HdrContrastSmoothness)] = sToneMapping,
            [nameof(AppSettings.InverseToneMapping)] = sToneMapping,
            [nameof(AppSettings.ToneMappingVisualize)] = sToneMapping,
            [nameof(AppSettings.TargetColorspaceHint)] = sTargetColorspace,
            [nameof(AppSettings.TargetColorspaceHintMode)] = sTargetColorspace,
            [nameof(AppSettings.TargetColorspaceHintStrict)] = sTargetColorspace,
            [nameof(AppSettings.TargetPrim)] = sTargetColorspace,
            [nameof(AppSettings.TargetTrc)] = sTargetColorspace,
            [nameof(AppSettings.TargetPeak)] = sTargetColorspace,
            [nameof(AppSettings.DisplayPeak)] = sColorManagement,
            [nameof(AppSettings.GamutMappingMode)] = sTargetColorspace,
            [nameof(AppSettings.IccProfileAuto)] = sColorManagement,
            [nameof(AppSettings.IccProfile)] = sColorManagement,
            [nameof(AppSettings.IccForceContrast)] = sColorManagement,
            [nameof(AppSettings.Icc3dlutSize)] = sColorManagement,
            [nameof(AppSettings.IccCache)] = sColorManagement,
            [nameof(AppSettings.IccCacheDir)] = sColorManagement,
            [nameof(AppSettings.D3d11OutputCsp)] = sColorManagement,
            [nameof(AppSettings.Interpolation)] = sGpuInterpolation,
            [nameof(AppSettings.BackgroundTileColor0)] = sGpuBackground,
            [nameof(AppSettings.BackgroundTileColor1)] = sGpuBackground,
            [nameof(AppSettings.BackgroundTileSize)] = sGpuBackground,
            [nameof(AppSettings.D3d11ExclusiveFs)] = sGpuD3d11,
            [nameof(AppSettings.D3d11Flip)] = sGpuD3d11,
            [nameof(AppSettings.D3d11Adapter)] = sGpuD3d11,
            [nameof(AppSettings.D3d11OutputFormat)] = sGpuD3d11,
            [nameof(AppSettings.D3d11SyncInterval)] = sGpuD3d11,
            [nameof(AppSettings.D3d11Warp)] = sGpuD3d11,
            [nameof(AppSettings.GpuShaderCache)] = sGpuShaders,
            [nameof(AppSettings.GpuShaderCacheDir)] = sGpuShaders,
            [nameof(AppSettings.GlslShadersAppend)] = sGpuShaders,
            [nameof(AppSettings.GlslShaders)] = sGpuShaders,
            [nameof(AppSettings.GlslShaderOpts)] = sGpuShaders,
            [nameof(AppSettings.VideoSync)] = sVideoSync,
            [nameof(AppSettings.VideoSyncMaxVideoChange)] = sVideoSync,
            [nameof(AppSettings.OverrideDisplayFps)] = sVideoSync,
            // audio
            [nameof(AppSettings.AudioDevice)] = sAudioOutput,
            [nameof(AppSettings.AudioExclusive)] = sAudioOutput,
            [nameof(AppSettings.AudioChannels)] = sAudioOutput,
            [nameof(AppSettings.AudioFormat)] = sAudioOutput,
            [nameof(AppSettings.AudioSampleRate)] = sAudioOutput,
            [nameof(AppSettings.AudioStreamSilence)] = sAudioOutput,
            [nameof(AppSettings.AdLavcDownmix)] = sAudioOutput,
            [nameof(AppSettings.AudioDelay)] = sAudioOutput,
            [nameof(AppSettings.AudioBuffer)] = sAudioOutput,
            [nameof(AppSettings.AudioWaitOpen)] = sAudioOutput,
            [nameof(AppSettings.AudioGapless)] = sAudioOutput,
            [nameof(AppSettings.AudioSpdif)] = sAudioOutput,
            [nameof(AppSettings.Replaygain)] = sAudioVolume,
            [nameof(AppSettings.AudioPitchCorrection)] = sAudioOutput,
            [nameof(AppSettings.AudioNormalizeDownmix)] = sAudioOutput,
                                    [nameof(AppSettings.VolumeMax)] = sAudioVolume,
            [nameof(AppSettings.AudioFileAuto)] = sAudioExternal,
            [nameof(AppSettings.AudioExts)] = sAudioExternal,
            [nameof(AppSettings.AudioFilePaths)] = sAudioExternal,
            [nameof(AppSettings.AudioDisplay)] = sAudioCoverArt,
            [nameof(AppSettings.CoverArtPreferEmbedded)] = sAudioCoverArt,
            [nameof(AppSettings.CoverArtAlwaysScan)] = sAudioCoverArt,
            [nameof(AppSettings.CoverArtLoadFromFilesystem)] = sAudioCoverArt,
            [nameof(AppSettings.CoverArtPreload)] = sAudioCoverArt,
            [nameof(AppSettings.CoverArtNames)] = sAudioCoverArt,
            [nameof(AppSettings.CoverArtImageExts)] = sAudioCoverArt,
            // subtitles
            [nameof(AppSettings.AudioLanguage)] = sTrackSelection,
            [nameof(AppSettings.SubtitleLanguage)] = sTrackLanguage,
            [nameof(AppSettings.SubFallback)] = sTrackFallback,
            [nameof(AppSettings.SubFontSize)] = sSubtitleStyle,
            [nameof(AppSettings.SubFont)] = sSubtitleStyle,
            [nameof(AppSettings.SubFontFile)] = sSubtitleStyle,
            [nameof(AppSettings.SubFontProvider)] = sSubtitleStyle,
            [nameof(AppSettings.SubCodePage)] = sSubtitleBehavior,
            [nameof(AppSettings.SubColor)] = sSubtitleStyle,
            [nameof(AppSettings.SubBackColor)] = sSubtitleStyle,
            [nameof(AppSettings.SubBorderColor)] = sSubtitleStyle,
            [nameof(AppSettings.SubOutlineSize)] = sSubtitleStyle,
            [nameof(AppSettings.SubShadowOffset)] = sSubtitleStyle,
            [nameof(AppSettings.SubBlur)] = sSubtitleStyle,
            [nameof(AppSettings.SubPos)] = sSubtitlePosition,
            [nameof(AppSettings.SubBold)] = sSubtitleStyle,
            [nameof(AppSettings.SubItalic)] = sSubtitleStyle,
            [nameof(AppSettings.SubAlignX)] = sSubtitlePosition,
            [nameof(AppSettings.SubAlignY)] = sSubtitlePosition,
            [nameof(AppSettings.SubScaleByWindow)] = sSubtitlePosition,
            [nameof(AppSettings.SubLineSpacing)] = sSubtitleBehavior,
            [nameof(AppSettings.SubJustify)] = sSubtitlePosition,
            [nameof(AppSettings.SubClearOnSeek)] = sSubtitleBehavior,
            [nameof(AppSettings.SubHinting)] = sSubtitleStyle,
            [nameof(AppSettings.SubMarginX)] = sSubtitlePosition,
            [nameof(AppSettings.SubMarginY)] = sSubtitlePosition,
            [nameof(AppSettings.SubDelay)] = sSubtitleBehavior,
            [nameof(AppSettings.SubScaleSigns)] = sSubtitlePosition,
            [nameof(AppSettings.SubUseMargins)] = sSubtitlePosition,
            [nameof(AppSettings.SubAuto)] = sSubtitleBehavior,
            [nameof(AppSettings.SubFilePaths)] = sSubtitleBehavior,
            [nameof(AppSettings.SubHdrPeak)] = sSubtitleBehavior,
            [nameof(AppSettings.SubAssOverride)] = sSubtitleAss,
            [nameof(AppSettings.SubAssStyleOverrides)] = sSubtitleAss,
            [nameof(AppSettings.SubAssForceMargins)] = sSubtitleAss,
            [nameof(AppSettings.SubAssScaleWithWindow)] = sSubtitleAss,
            [nameof(AppSettings.SubAssUseVideoData)] = sSubtitleAss,
            [nameof(AppSettings.SubAssVideoAspectOverride)] = sSubtitleAss,
            [nameof(AppSettings.SubAssVsfilterColorCompat)] = sSubtitleAss,
            [nameof(AppSettings.SubEmbeddedFonts)] = sSubtitleAss,
            [nameof(AppSettings.BlendSubtitles)] = sSubtitleAss,
            [nameof(AppSettings.StretchImageSubsToScreen)] = sSubtitleImage,
            [nameof(AppSettings.ImageSubsVideoResolution)] = sSubtitleImage,
            [nameof(AppSettings.ImageSubsHdrPeak)] = sSubtitleImage,
            // window
            [nameof(AppSettings.AlwaysOnTop)] = sWindow,
            [nameof(AppSettings.KeepOpen)] = sWindow,
            [nameof(AppSettings.StartFullscreen)] = sWindow,
            [nameof(AppSettings.WindowTitle)] = sWindow,
            [nameof(AppSettings.AutoWindowResize)] = sWindow,
            [nameof(AppSettings.WindowPiP)] = sWindowPiP,
            [nameof(AppSettings.WindowPiPShowControls)] = sWindowPiP,
            [nameof(AppSettings.WindowPiPShowTopButtons)] = sWindowPiP,
            [nameof(AppSettings.WindowPiPOpacity)] = sWindowPiP,
            [nameof(AppSettings.WindowPiPAspectRatioLock)] = sWindowPiP,
            [nameof(AppSettings.WindowPiPAnchor)] = sWindowPiP,
            [nameof(AppSettings.WindowPiPSize)] = sWindowPiP,
            [nameof(AppSettings.WindowStartMaximized)] = sWindow,
            [nameof(AppSettings.WindowAspectRatioLock)] = sWindow,
            [nameof(AppSettings.WindowRememberSize)] = sWindow,
            // demuxer
            [nameof(AppSettings.AutoCreatePlaylist)] = sDemuxerPlaylist,
            [nameof(AppSettings.DirectoryMode)] = sDemuxerPlaylist,
            [nameof(AppSettings.DirectoryFilterTypes)] = sDemuxerPlaylist,
            [nameof(AppSettings.VideoExts)] = sDemuxerPlaylist,
            [nameof(AppSettings.ImageExts)] = sDemuxerPlaylist,
            [nameof(AppSettings.ImageDisplayDuration)] = sDemuxerPlaylist,
            [nameof(AppSettings.DemuxerMaxBytes)] = sDemuxerBuffering,
            [nameof(AppSettings.DemuxerMaxBackBytes)] = sDemuxerBuffering,
            [nameof(AppSettings.DemuxerReadahead)] = sDemuxerBuffering,
            [nameof(AppSettings.DemuxerHysteresisSecs)] = sDemuxerBuffering,
            [nameof(AppSettings.DemuxerCacheDir)] = sDemuxerBuffering,
            // cache
            [nameof(AppSettings.CacheEnabled)] = sCache,
            [nameof(AppSettings.CacheSecs)] = sCache,
            [nameof(AppSettings.CacheOnDisk)] = sCache,
            [nameof(AppSettings.CachePause)] = sCache,
            [nameof(AppSettings.CachePauseInitial)] = sCache,
            [nameof(AppSettings.CachePauseWait)] = sCache,
                        // network
            [nameof(AppSettings.Ytdl)] = sNetworkYtdlp,
            [nameof(AppSettings.YtdlRawOptionsAppend)] = sNetworkYtdlp,
            [nameof(AppSettings.YtdlFormat)] = sNetworkYtdlp,
            [nameof(AppSettings.YtdlPath)] = sNetworkYtdlp,
            [nameof(AppSettings.YtdlTryFirst)] = sNetworkYtdlp,
            [nameof(AppSettings.YtdlAllFormats)] = sNetworkYtdlp,
            [nameof(AppSettings.YtdlUseManifests)] = sNetworkYtdlp,
            [nameof(AppSettings.YtdlThumbnails)] = sNetworkYtdlp,
            [nameof(AppSettings.YtdlExclude)] = sNetworkYtdlp,
            [nameof(AppSettings.UserAgent)] = sNetworkHttp,
            [nameof(AppSettings.Referrer)] = sNetworkHttp,
            [nameof(AppSettings.HttpHeaderFields)] = sNetworkHttp,
            [nameof(AppSettings.HttpProxy)] = sNetworkHttp,
            [nameof(AppSettings.CookiesFile)] = sNetworkHttp,
            [nameof(AppSettings.TlsVerify)] = sNetworkHttp,
            [nameof(AppSettings.PrefetchPlaylist)] = sNetworkHttp,
            [nameof(AppSettings.NetworkTimeout)] = sNetworkHttp,
            [nameof(AppSettings.CurlMaxRedirects)] = sNetworkCurl,
            [nameof(AppSettings.CurlMaxRetries)] = sNetworkCurl,
            [nameof(AppSettings.CurlConnectTimeout)] = sNetworkCurl,
            [nameof(AppSettings.CurlBufferSize)] = sNetworkCurl,
            [nameof(AppSettings.CurlMaxRequestSize)] = sNetworkCurl,
            // input (program category; not shortcut bindings)
            [nameof(AppSettings.InputIme)] = sProgramConfig,
            [nameof(AppSettings.InputIpcServer)] = sProgramConfig,
            // program actions
            ["FileAssociationCheckList"] = sProgramAssociations,
            ["ActionUnassociateFiles"] = sProgramAssociations,
            ["ActionExportConfig"] = sProgramConfig,
            ["ActionImportConfig"] = sProgramConfig,
            [nameof(AppSettings.ControlBarLayout)] = sProgramInterface,
            [nameof(AppSettings.ControlBarHiddenIcons)] = sProgramInterface,
            // osd
            [nameof(AppSettings.OsdFontSize)] = sOsdAppearance,
            [nameof(AppSettings.OsdFont)] = sOsdAppearance,
            [nameof(AppSettings.OsdColor)] = sOsdAppearance,
            [nameof(AppSettings.OsdOutlineColor)] = sOsdAppearance,
            [nameof(AppSettings.OsdOnSeek)] = sOsdBehavior,
            [nameof(AppSettings.OsdDuration)] = sOsdBehavior,
            [nameof(AppSettings.OsdPlayingMsg)] = sOsdBehavior,
            [nameof(AppSettings.OsdPlayingMsgDuration)] = sOsdBehavior,
            [nameof(AppSettings.OsdBarWidth)] = sOsdBehavior,
            [nameof(AppSettings.OsdBarHeight)] = sOsdBehavior,
            [nameof(AppSettings.OsdBlur)] = sOsdAppearance,
            [nameof(AppSettings.OsdOutlineSize)] = sOsdAppearance,
            [nameof(AppSettings.OsdFractions)] = sOsdAppearance,
            [nameof(AppSettings.OsdLevel)] = sOsdAppearance,
            [nameof(AppSettings.OsdAlignX)] = sOsdPosition,
            [nameof(AppSettings.OsdAlignY)] = sOsdPosition,
            [nameof(AppSettings.OsdMarginX)] = sOsdPosition,
            [nameof(AppSettings.OsdMarginY)] = sOsdPosition,
            [nameof(AppSettings.ShowOsdPlayingMsg)] = sOsdBehavior,
            [nameof(AppSettings.MetadataOsdEnabled)] = sOsdMetadata,
            [nameof(AppSettings.MetadataOsdAutohideTimeout)] = sOsdMetadata,
            [nameof(AppSettings.MetadataOsdShowChapter)] = sOsdMetadata,
            [nameof(AppSettings.MetadataOsdEnableForVideo)] = sOsdMetadata,
            [nameof(AppSettings.MetadataOsdEnableForAudio)] = sOsdMetadata,
            [nameof(AppSettings.MetadataOsdEnableForAudioWithAlbumArt)] = sOsdMetadata,
            [nameof(AppSettings.MetadataOsdAutohideForAudio)] = sOsdMetadata,
            [nameof(AppSettings.MetadataOsdAutohideForAudioWithAlbumArt)] = sOsdMetadata,
            [nameof(AppSettings.MetadataOsdEnableForImage)] = sOsdMetadata,
            [nameof(AppSettings.MetadataOsdAutohideStatusTimeout)] = sOsdMetadata,
            [nameof(AppSettings.MetadataOsdShowAlbumTrack)] = sOsdMetadata,
            [nameof(AppSettings.MetadataOsdMessageMaxLength)] = sOsdMetadata,
            // screenshot
            [nameof(AppSettings.ScreenshotDirectory)] = sScreenshotLocation,
            [nameof(AppSettings.ScreenshotTemplate)] = sScreenshotLocation,
            [nameof(AppSettings.ScreenshotFormat)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotJpegQuality)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotJpegSourceChroma)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotPngCompression)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotPngFilter)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotWebpQuality)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotWebpLossless)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotWebpCompression)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotJxlDistance)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotJxlEffort)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotAvifEncoder)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotHighBitDepth)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotTagColorspace)] = sScreenshotQuality,
            [nameof(AppSettings.ScreenshotSw)] = sScreenshotQuality,
            // testing
            [nameof(AppSettings.TestMpvCommandLog)] = sProgramTesting,
            [nameof(AppSettings.TestOsdMessage)] = sProgramTesting,
            [nameof(AppSettings.TestSignal)] = sProgramTesting,
        };

        // Category is set inline on each Option (single source of truth);
        // sectionMap is the single source for sections (they are not declared
        // inline). A missing mapping must fail loudly: a silently dropped
        // option disappears from the settings tree without any trace.
        // Shortcut:N keys are generated at runtime from input.conf and carry
        // their own inline Category/Section, so the static sectionMap cannot
        // (and must not) cover them.
        var missingCategory = options.Where(o => string.IsNullOrEmpty(o.Category)).Select(o => o.Key).ToList();
        var missingSections = options
            .Where(o => !o.Key.StartsWith("Shortcut:", StringComparison.Ordinal) && !sectionMap.ContainsKey(o.Key))
            .Select(o => o.Key).ToList();
        if (missingCategory.Count > 0 || missingSections.Count > 0)
        {
            throw new InvalidOperationException(
                $"Settings tree broken: missing Category for [{string.Join(", ", missingCategory)}]; " +
                $"missing sectionMap entry for [{string.Join(", ", missingSections)}]");
        }

        foreach (var option in options)
        {
            if (sectionMap.TryGetValue(option.Key, out var section))
            {
                option.Section = section;
            }
        }

        var categoryOrderIndex = categoryOrder
            .Select((name, index) => (name, index))
            .ToDictionary(x => x.name, x => x.index, StringComparer.Ordinal);

        // Cluster options by (category, section) in the official manual order,
        // so each section is contiguous and appears in a predictable layout.
        var ordered = options
            .Select((o, i) => (o, i))
            .GroupBy(x => (x.o.Category, x.o.Section ?? string.Empty))
            .OrderBy(g => categoryOrderIndex.TryGetValue(g.Key.Category, out var categoryIndex) ? categoryIndex : int.MaxValue)
            .ThenBy(g => sectionOrder.TryGetValue(g.Key.Item2, out var sectionIndex) ? sectionIndex : int.MaxValue)
            .ThenBy(g => g.Min(x => optionOrder.TryGetValue(x.o.Key, out var optionIndex) ? optionIndex : int.MaxValue))
            .SelectMany(g => g.OrderBy(x => optionOrder.TryGetValue(x.o.Key, out var optionIndex) ? optionIndex : int.MaxValue).Select(x => x.o))
            .ToList();
        options = ordered;

        var seenSections = new HashSet<(string Category, string Section)>();
        foreach (var option in options)
        {
            if (!string.IsNullOrEmpty(option.Section)
                && seenSections.Add((option.Category, option.Section)))
            {
                option.ShowSectionHeader = true;
            }
        }

        CategoryOrder.AddRange(categoryOrder);

        return options;
    }
}
