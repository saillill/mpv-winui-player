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
        var watchLater = AppContext.AppLang.SettingsCategoryWatchLater;
        var video = AppContext.AppLang.SettingsCategoryVideo;
        var audio = AppContext.AppLang.SettingsCategoryAudio;
        var subtitles = AppContext.AppLang.SettingsCategorySubtitles;
        var window = AppContext.AppLang.SettingsCategoryWindow;
        var cache = AppContext.AppLang.SettingsCategoryCache;
        var network = AppContext.AppLang.SettingsCategoryNetwork;
        var input = AppContext.AppLang.SettingsCategoryInput;
        var shortcuts = AppContext.AppLang.SettingsCategoryShortcuts;
        var osd = AppContext.AppLang.SettingsCategoryOsd;
        var screenshot = AppContext.AppLang.SettingsCategoryScreenshot;
        var testing = AppContext.AppLang.SettingsCategoryTesting;
        var sProgramInterface = AppContext.AppLang.SectionProgramInterface;
        var sProgramLanguageLog = AppContext.AppLang.SectionProgramLanguageLog;
        var sProgramNetwork = AppContext.AppLang.SectionProgramNetwork;
        var sProgramTesting = AppContext.AppLang.SectionProgramTesting;
        var sProgramAssociations = AppContext.AppLang.SectionProgramAssociations;
        var sProgramConfig = AppContext.AppLang.SectionProgramConfig;
        var sWindowPiP = AppContext.AppLang.SectionWindowPiP;
        var sNetworkYtdlp = AppContext.AppLang.SectionNetworkYtdlp;
        var sNetworkHttp = AppContext.AppLang.SectionNetworkHttp;
        var sNetworkCurl = AppContext.AppLang.SectionNetworkCurl;
        var sShortcutsReset = AppContext.AppLang.SectionShortcutsReset;
        var sPlayback = AppContext.AppLang.SectionPlayback;
        var sPlaybackSeeking = AppContext.AppLang.SectionPlaybackSeeking;
        var sPlaybackSeekPreview = AppContext.AppLang.SectionPlaybackSeekPreview;
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
        var sWindow = AppContext.AppLang.SectionWindow;
        var sDemuxerPlaylist = AppContext.AppLang.SectionDemuxerPlaylist;
        var sDemuxerBuffering = AppContext.AppLang.SectionDemuxerBuffering;
        var sCache = AppContext.AppLang.SectionCache;
        var sInput = AppContext.AppLang.SectionInput;
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

        options.Add(new Option
        {
            Key = "ShortcutReset",
            Label = lang.SettingsResetShortcuts,
            Category = shortcuts,
            Description = lang.SettingsHelpResetShortcuts,
            Type = OptionType.Action,
            ActionKind = OptionActionKind.Button,
            ActionLabel = lang.SettingsResetShortcuts,
            ActionHandler = _ => ResetShortcuts(),
            ActionStatus = () => _actionStatus,
        });

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

                                var categoryOrder = new[]
        {
            program,
            playback,
            watchLater,
            video,
            audio,
            subtitles,
            window,
            cache,
            network,
            input,
            shortcuts,
            osd,
            screenshot,
            testing,
        };

        var optionOrder = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [nameof(AppSettings.ThemeType)] = 0,
            [nameof(AppSettings.BackdropType)] = 1,
            [nameof(AppSettings.ThemeAccentColor)] = 2,
            [nameof(AppSettings.ThemeOpacity)] = 3,
            [nameof(AppSettings.ThemeLuminosity)] = 4,
            [nameof(AppSettings.UiFont)] = 5,
            [nameof(AppSettings.TestMpvCommandLog)] = 6,
            [nameof(AppSettings.TestOsdMessage)] = 7,
            [nameof(AppSettings.TestSignal)] = 8,
            [nameof(AppSettings.CurrentLanguage)] = 9,
            [nameof(AppSettings.EnableDebugLog)] = 9,
            [nameof(AppSettings.Ytdl)] = 11,
            [nameof(AppSettings.YtdlRawOptionsAppend)] = 12,
            [nameof(AppSettings.LoopFile)] = 13,
            [nameof(AppSettings.LoopPlaylist)] = 14,
            [nameof(AppSettings.Speed)] = 15,
            [nameof(AppSettings.HrSeek)] = 16,
            [nameof(AppSettings.HrSeekFramedrop)] = 17,
            [nameof(AppSettings.SeekHoldEnabled)] = 18,
            [nameof(AppSettings.EnableVideoPreview)] = 19,
            [nameof(AppSettings.ThumbfastQuality)] = 20,
            [nameof(AppSettings.ThumbfastNetwork)] = 21,
            [nameof(AppSettings.ThumbfastMinDuration)] = 22,
            [nameof(AppSettings.ThumbfastPrecise)] = 23,
            [nameof(AppSettings.ThumbfastMaxWidth)] = 24,
            [nameof(AppSettings.ThumbfastMaxHeight)] = 25,
            [nameof(AppSettings.ThumbfastSpawnFirst)] = 26,
            [nameof(AppSettings.ThumbfastThreads)] = 27,
            [nameof(AppSettings.ThumbfastFrequency)] = 28,
            [nameof(AppSettings.ThumbfastDirectIo)] = 29,
            [nameof(AppSettings.ThumbfastQuitAfterInactivity)] = 30,
            [nameof(AppSettings.AudioLanguage)] = 49,
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
            [nameof(AppSettings.GamutMappingMode)] = 173,
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
            [nameof(AppSettings.VideoSync)] = 191,
            [nameof(AppSettings.VideoSyncMaxVideoChange)] = 192,
            [nameof(AppSettings.WindowPiP)] = 193,
            [nameof(AppSettings.WindowPiPSize)] = 194,
            [nameof(AppSettings.WindowStartMaximized)] = 195,
            [nameof(AppSettings.WindowRememberSize)] = 196,
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
            [nameof(AppSettings.ControlBarLayout)] = 220,
            [nameof(AppSettings.ControlBarHiddenIcons)] = 221,
            ["ShortcutReset"] = 2000,
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
            [sPlayback] = 2,
            [sPlaybackSeeking] = 3,
            [sPlaybackSeekPreview] = 4,
            [sWatchLaterResume] = 5,
            [sWatchLaterStorage] = 6,
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
            [sProgramNetwork] = 30,
            [sInput] = 31,
            [sOsd] = 32,
            [sOsdMetadata] = 33,
            [sScreenshotLocation] = 34,
            [sScreenshotQuality] = 35,
            [sProgramTesting] = 36,
            [sShortcutsReset] = 38,
            [sNetworkYtdlp] = 39,
            [sNetworkHttp] = 40,
            [sNetworkCurl] = 41,
            [sWindowPiP] = 42,
            [sProgramAssociations] = 43,
            [sProgramConfig] = 44,
        };

        var sectionMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // program
            [nameof(AppSettings.ThemeType)] = sProgramInterface,
            [nameof(AppSettings.BackdropType)] = sProgramInterface,
            [nameof(AppSettings.ThemeAccentColor)] = sProgramInterface,
            [nameof(AppSettings.ThemeOpacity)] = sProgramInterface,
            [nameof(AppSettings.ThemeLuminosity)] = sProgramInterface,
            [nameof(AppSettings.UiFont)] = sProgramInterface,
            [nameof(AppSettings.CurrentLanguage)] = sProgramLanguageLog,
            [nameof(AppSettings.CheckForUpdates)] = sProgramInterface,
            // preset keys are option keys, not AppSettings properties
            [nameof(AppSettings.EnableDebugLog)] = sProgramTesting,
            // playback
            [nameof(AppSettings.LoopFile)] = sPlayback,
            [nameof(AppSettings.LoopPlaylist)] = sPlayback,
            [nameof(AppSettings.Speed)] = sPlayback,
            [nameof(AppSettings.HrSeek)] = sPlaybackSeeking,
            [nameof(AppSettings.HrSeekFramedrop)] = sPlaybackSeeking,
            [nameof(AppSettings.SeekHoldEnabled)] = sPlaybackSeeking,
            [nameof(AppSettings.EnableVideoPreview)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbfastQuality)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbfastNetwork)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbfastMinDuration)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbfastPrecise)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbfastMaxWidth)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbfastMaxHeight)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbfastSpawnFirst)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbfastThreads)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbfastFrequency)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbfastDirectIo)] = sPlaybackSeekPreview,
            [nameof(AppSettings.ThumbfastQuitAfterInactivity)] = sPlaybackSeekPreview,
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
            [nameof(AppSettings.HdrAutoMode)] = sVideoFilters,
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
            [nameof(AppSettings.ToneMapping)] = sGpuColor,
            [nameof(AppSettings.TargetColorspaceHint)] = sGpuColor,
            [nameof(AppSettings.TargetColorspaceHintMode)] = sGpuColor,
            [nameof(AppSettings.TargetColorspaceHintStrict)] = sGpuColor,
            [nameof(AppSettings.TargetPrim)] = sGpuColor,
            [nameof(AppSettings.TargetTrc)] = sGpuColor,
            [nameof(AppSettings.TargetPeak)] = sGpuColor,
            [nameof(AppSettings.GamutMappingMode)] = sGpuColor,
            [nameof(AppSettings.IccProfileAuto)] = sGpuColor,
            [nameof(AppSettings.IccProfile)] = sGpuColor,
            [nameof(AppSettings.IccForceContrast)] = sGpuColor,
            [nameof(AppSettings.Icc3dlutSize)] = sGpuColor,
            [nameof(AppSettings.IccCache)] = sGpuColor,
            [nameof(AppSettings.IccCacheDir)] = sGpuColor,
            [nameof(AppSettings.D3d11OutputCsp)] = sGpuColor,
            [nameof(AppSettings.Interpolation)] = sGpuInterpolation,
            [nameof(AppSettings.BackgroundTileColor0)] = sGpuBackground,
            [nameof(AppSettings.BackgroundTileColor1)] = sGpuBackground,
            [nameof(AppSettings.BackgroundTileSize)] = sGpuBackground,
            [nameof(AppSettings.D3d11ExclusiveFs)] = sGpuD3d11,
            [nameof(AppSettings.D3d11Flip)] = sGpuD3d11,
            [nameof(AppSettings.D3d11Adapter)] = sGpuD3d11,
            [nameof(AppSettings.GpuShaderCache)] = sGpuShaders,
            [nameof(AppSettings.GpuShaderCacheDir)] = sGpuShaders,
            [nameof(AppSettings.GlslShadersAppend)] = sGpuShaders,
            [nameof(AppSettings.VideoSync)] = sVideoSync,
            [nameof(AppSettings.VideoSyncMaxVideoChange)] = sVideoSync,
            // audio
            [nameof(AppSettings.AudioDevice)] = sAudioOutput,
            [nameof(AppSettings.AudioExclusive)] = sAudioOutput,
            [nameof(AppSettings.AudioChannels)] = sAudioOutput,
            [nameof(AppSettings.AudioDelay)] = sAudioOutput,
            [nameof(AppSettings.AudioBuffer)] = sAudioOutput,
            [nameof(AppSettings.AudioWaitOpen)] = sAudioOutput,
            [nameof(AppSettings.AudioGapless)] = sAudioOutput,
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
            [nameof(AppSettings.AudioLanguage)] = sAudioOutput,
            [nameof(AppSettings.SubtitleLanguage)] = sTrackLanguage,
            [nameof(AppSettings.SubFallback)] = sTrackFallback,
            [nameof(AppSettings.SubFontSize)] = sSubtitleText,
            [nameof(AppSettings.SubFont)] = sSubtitleText,
            [nameof(AppSettings.SubFontFile)] = sSubtitleText,
            [nameof(AppSettings.SubFontProvider)] = sSubtitleText,
            [nameof(AppSettings.SubCodePage)] = sSubtitleText,
            [nameof(AppSettings.SubColor)] = sSubtitleText,
            [nameof(AppSettings.SubBackColor)] = sSubtitleText,
            [nameof(AppSettings.SubBorderColor)] = sSubtitleText,
            [nameof(AppSettings.SubOutlineSize)] = sSubtitleText,
            [nameof(AppSettings.SubShadowOffset)] = sSubtitleText,
            [nameof(AppSettings.SubBlur)] = sSubtitleText,
            [nameof(AppSettings.SubPos)] = sSubtitleText,
            [nameof(AppSettings.SubDelay)] = sSubtitleText,
            [nameof(AppSettings.SubScaleSigns)] = sSubtitleText,
            [nameof(AppSettings.SubUseMargins)] = sSubtitleText,
            [nameof(AppSettings.SubAuto)] = sSubtitleText,
            [nameof(AppSettings.SubFilePaths)] = sSubtitleText,
            [nameof(AppSettings.SubHdrPeak)] = sSubtitleText,
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
            [nameof(AppSettings.WindowPiP)] = sWindowPiP,
            [nameof(AppSettings.WindowPiPSize)] = sWindowPiP,
            [nameof(AppSettings.WindowStartMaximized)] = sWindow,
            [nameof(AppSettings.WindowRememberSize)] = sWindow,
            // demuxer
            [nameof(AppSettings.AutoCreatePlaylist)] = sDemuxerPlaylist,
            [nameof(AppSettings.DirectoryMode)] = sDemuxerPlaylist,
            [nameof(AppSettings.DirectoryFilterTypes)] = sDemuxerPlaylist,
            [nameof(AppSettings.VideoExts)] = sDemuxerPlaylist,
            [nameof(AppSettings.ImageExts)] = sDemuxerPlaylist,
            [nameof(AppSettings.DemuxerMaxBytes)] = sCache,
            [nameof(AppSettings.DemuxerMaxBackBytes)] = sCache,
            [nameof(AppSettings.DemuxerReadahead)] = sCache,
            // cache
            [nameof(AppSettings.CacheEnabled)] = sCache,
            [nameof(AppSettings.CacheSecs)] = sCache,
            [nameof(AppSettings.CacheOnDisk)] = sCache,
                        // network
            [nameof(AppSettings.Ytdl)] = sProgramNetwork,
            [nameof(AppSettings.YtdlRawOptionsAppend)] = sProgramNetwork,
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
            [nameof(AppSettings.NetworkTimeout)] = sNetworkHttp,
            [nameof(AppSettings.CurlMaxRedirects)] = sNetworkCurl,
            [nameof(AppSettings.CurlMaxRetries)] = sNetworkCurl,
            [nameof(AppSettings.CurlConnectTimeout)] = sNetworkCurl,
            [nameof(AppSettings.CurlBufferSize)] = sNetworkCurl,
            [nameof(AppSettings.CurlMaxRequestSize)] = sNetworkCurl,
            // input
            [nameof(AppSettings.InputIme)] = sInput,
            [nameof(AppSettings.InputIpcServer)] = sInput,
            // shortcuts
            ["ShortcutReset"] = sShortcutsReset,
            // program actions
            ["FileAssociationCheckList"] = sProgramAssociations,
            ["ActionUnassociateFiles"] = sProgramAssociations,
            ["ActionExportConfig"] = sProgramConfig,
            ["ActionImportConfig"] = sProgramConfig,
            [nameof(AppSettings.ControlBarLayout)] = sProgramInterface,
            [nameof(AppSettings.ControlBarHiddenIcons)] = sProgramInterface,
            // osd
            [nameof(AppSettings.OsdFontSize)] = sOsd,
            [nameof(AppSettings.OsdFont)] = sOsd,
            [nameof(AppSettings.OsdColor)] = sOsd,
            [nameof(AppSettings.OsdOutlineColor)] = sOsd,
            [nameof(AppSettings.OsdOnSeek)] = sOsd,
            [nameof(AppSettings.OsdDuration)] = sOsd,
            [nameof(AppSettings.OsdPlayingMsg)] = sOsd,
            [nameof(AppSettings.OsdPlayingMsgDuration)] = sOsd,
            [nameof(AppSettings.OsdBarWidth)] = sOsd,
            [nameof(AppSettings.OsdBarHeight)] = sOsd,
            [nameof(AppSettings.OsdBlur)] = sOsd,
            [nameof(AppSettings.OsdOutlineSize)] = sOsd,
            [nameof(AppSettings.OsdFractions)] = sOsd,
            [nameof(AppSettings.ShowOsdPlayingMsg)] = sOsd,
            [nameof(AppSettings.MetadataOsdEnabled)] = sOsdMetadata,
            [nameof(AppSettings.MetadataOsdAutohideTimeout)] = sOsdMetadata,
            [nameof(AppSettings.MetadataOsdShowChapter)] = sOsdMetadata,
            [nameof(AppSettings.MetadataOsdEnableForVideo)] = sOsdMetadata,
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

    /// <summary>Options whose presets cover every legal value; the list control must not add a "Custom" entry.</summary>
    }
