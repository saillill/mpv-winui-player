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
    private List<Option> BuildPlaybackControlOptions()
    {
        var playback = AppContext.AppLang.SettingsCategoryPlayback;
        var window = AppContext.AppLang.SettingsCategoryWindow;
        var network = AppContext.AppLang.SettingsCategoryNetwork;
        var audio = AppContext.AppLang.SettingsCategoryAudio;
        var video = AppContext.AppLang.SettingsCategoryVideo;
        var lang = AppContext.AppLang;

        return
        [
            // ===== Playback Control =====
            new Option
            {
                Key = nameof(AppContext.AppSetting.Hwdec),
                Label = lang.SettingsHwdec,
                Category = video,
                Description = lang.SettingsHelpHwdec,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("auto-safe", lang.OptionValueAutoSafe),
                    new OptionChoice("auto", lang.OptionValueAuto),
                    new OptionChoice("no", lang.OptionValueNo),
                    new OptionChoice("d3d11va", "D3D11 VA"),
                    new OptionChoice("nvdec", "NVDEC"),
                    new OptionChoice("dxva2", "DXVA2"),
                ],
                Getter = () => AppContext.AppSetting.Hwdec,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.Hwdec), AppContext.AppSetting.Hwdec = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.HwdecCodecs),
                Label = lang.SettingsHwdecCodecs,
                Category = video,
                Description = lang.SettingsHelpHwdecCodecs,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.HwdecCodecs,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.HwdecCodecs), AppContext.AppSetting.HwdecCodecs = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.VolumeMax),
                Label = lang.SettingsVolumeMax,
                Category = audio,
                Description = lang.SettingsHelpVolumeMax,
                Type = OptionType.Integer,
                Min = 100,
                Max = 300,
                Step = 10,
                Getter = () => (double)AppContext.AppSetting.VolumeMax,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.VolumeMax), AppContext.AppSetting.VolumeMax = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.KeepOpen),
                Description = lang.SettingsHelpKeepOpen,
                Label = lang.SettingsKeepOpen,
                Category = window,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("yes", lang.OptionValueKeepOpenYes),
                    new OptionChoice("no", lang.OptionValueKeepOpenNo),
                    new OptionChoice("always", lang.OptionValueKeepOpenAlways),
                ],
                Getter = () => AppContext.AppSetting.KeepOpen,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.KeepOpen), AppContext.AppSetting.KeepOpen = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.StartFullscreen),
                Description = lang.SettingsHelpStartFullscreen,
                Label = lang.SettingsStartFullscreen,
                Category = window,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.StartFullscreen,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.StartFullscreen), AppContext.AppSetting.StartFullscreen = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.WindowTitle),
                Description = lang.SettingsHelpWindowTitle,
                Label = lang.SettingsWindowTitle,
                Category = window,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.WindowTitle,
                Setter = v =>
                {
                    AppContext.AppSetting.WindowTitle = (string)v!;
                    if (App.Window is MainWindow mainWindow)
                    {
                        mainWindow.UpdateTitle(string.Empty);
                    }
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.LoopPlaylist),
                Description = lang.SettingsHelpLoopPlaylist,
                Label = lang.SettingsLoopPlaylist,
                Category = playback,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("no", lang.OptionValueOff),
                    new OptionChoice("inf", lang.OptionValueLoopInfinite),
                    new OptionChoice("force", lang.OptionValueLoopForce),
                ],
                Getter = () => AppContext.AppSetting.LoopPlaylist,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.LoopPlaylist), AppContext.AppSetting.LoopPlaylist = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.LoopFile),
                Description = lang.SettingsHelpLoopFile,
                Label = lang.SettingsLoopFile,
                Category = playback,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.LoopFile,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.LoopFile), AppContext.AppSetting.LoopFile = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.WindowPiP),
                Label = lang.SettingsPiP,
                Category = window,
                Description = lang.SettingsHelpPiP,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.WindowPiP,
                Setter = v =>
                {
                    AppContext.AppSetting.WindowPiP = (bool)v!;
                    AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.WindowPiP), v);
                    RefreshWarningsAndEnabled();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.WindowPiPSize),
                Description = lang.SettingsHelpWindowPiPSize,
                Label = lang.SettingsPiPSize,
                Category = window,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("320x180", lang.OptionValuePiPSmall),
                    new OptionChoice("480x270", lang.OptionValuePiPMedium),
                    new OptionChoice("640x360", lang.OptionValuePiPLarge),
                ],
                Getter = () => AppContext.AppSetting.WindowPiPSize,
                Setter = v =>
                {
                    AppContext.AppSetting.WindowPiPSize = (string)v!;
                    AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.WindowPiPSize), v);
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.WindowStartMaximized),
                Label = lang.SettingsStartMaximized,
                Category = window,
                Description = lang.SettingsHelpStartMaximized,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.WindowStartMaximized,
                Setter = v => AppContext.AppSetting.WindowStartMaximized = (bool)v!
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.WindowRememberSize),
                Label = lang.SettingsRememberWindowSize,
                Category = window,
                Description = lang.SettingsHelpRememberWindowSize,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.WindowRememberSize,
                Setter = v => AppContext.AppSetting.WindowRememberSize = (bool)v!
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.SavePositionOnQuit),
                Description = lang.SettingsHelpSavePositionOnQuit,
                Label = lang.SettingsSavePositionOnQuit,
                Category = playback,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.SavePositionOnQuit,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.SavePositionOnQuit), AppContext.AppSetting.SavePositionOnQuit = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.Speed),
                Description = lang.SettingsHelpSpeed,
                Label = lang.SettingsSpeed,
                Category = playback,
                Type = OptionType.Double,
                Min = 0.25,
                Max = 4,
                Step = 0.1,
                Getter = () => AppContext.AppSetting.Speed,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.Speed), AppContext.AppSetting.Speed = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.Interpolation),
                Label = lang.SettingsInterpolation,
                Category = video,
                Description = lang.SettingsHelpInterpolation,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.Interpolation,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.Interpolation), AppContext.AppSetting.Interpolation = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ResumePlayback),
                Label = lang.SettingsResumePlayback,
                Category = playback,
                Description = lang.SettingsHelpResumePlayback,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.ResumePlayback,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ResumePlayback), AppContext.AppSetting.ResumePlayback = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CacheSecs),
                Label = lang.SettingsCacheSecs,
                Category = network,
                Description = lang.SettingsHelpCacheSecs,
                Type = OptionType.Integer,
                Min = 0,
                Max = 600,
                Step = 10,
                Getter = () => (double)AppContext.AppSetting.CacheSecs,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.CacheSecs), AppContext.AppSetting.CacheSecs = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CacheEnabled),
                Description = lang.SettingsHelpCacheEnabled,
                Label = lang.SettingsCacheEnabled,
                Category = network,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("no", lang.OptionValueNo),
                    new OptionChoice("auto", lang.OptionValueAuto),
                    new OptionChoice("yes", lang.OptionValueYes),
                ],
                Getter = () => AppContext.AppSetting.CacheEnabled,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.CacheEnabled), AppContext.AppSetting.CacheEnabled = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CachePause),
                Label = lang.SettingsCachePause,
                Category = network,
                Description = lang.SettingsHelpCachePause,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.CachePause,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.CachePause), AppContext.AppSetting.CachePause = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.DemuxerReadahead),
                Label = lang.SettingsDemuxerReadahead,
                Category = network,
                Description = lang.SettingsHelpDemuxerReadahead,
                Type = OptionType.Double,
                Min = 0,
                Max = 30,
                Step = 0.5,
                Getter = () => AppContext.AppSetting.DemuxerReadahead,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.DemuxerReadahead), AppContext.AppSetting.DemuxerReadahead = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.Ytdl),
                Label = lang.SettingsYtdl,
                Category = network,
                Description = lang.SettingsHelpYtdl,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.Ytdl,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.Ytdl), AppContext.AppSetting.Ytdl = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.YtdlRawOptionsAppend),
                Label = lang.SettingsYtdlRawOptionsAppend,
                Category = network,
                Description = lang.SettingsHelpYtdlRawOptionsAppend,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.YtdlRawOptionsAppend,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.YtdlRawOptionsAppend), AppContext.AppSetting.YtdlRawOptionsAppend = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.YtdlFormat),
                Label = lang.SettingsYtdlFormat,
                Category = network,
                Description = lang.SettingsHelpYtdlFormat,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("", lang.OptionValueAuto),
                    new OptionChoice("bv*+ba/b", "bv*+ba/b"),
                    new OptionChoice("bv*+ba", "bv*+ba"),
                    new OptionChoice("best", "best"),
                    new OptionChoice("mp4", "mp4"),
                    new OptionChoice("webm", "webm"),
                ],
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.YtdlFormat,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.YtdlFormat), AppContext.AppSetting.YtdlFormat = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.YtdlPath),
                Label = lang.SettingsYtdlPath,
                Category = network,
                Description = lang.SettingsHelpYtdlPath,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.YtdlPath,
                Setter = v =>
                {
                    AppContext.AppSetting.YtdlPath = (string)v!;
                    AppContext.WriteManagedMpvConfig();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.YtdlTryFirst),
                Label = lang.SettingsYtdlTryFirst,
                Category = network,
                Description = lang.SettingsHelpYtdlTryFirst,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.YtdlTryFirst,
                Setter = v =>
                {
                    AppContext.AppSetting.YtdlTryFirst = (bool)v!;
                    AppContext.WriteManagedMpvConfig();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.YtdlAllFormats),
                Label = lang.SettingsYtdlAllFormats,
                Category = network,
                Description = lang.SettingsHelpYtdlAllFormats,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.YtdlAllFormats,
                Setter = v =>
                {
                    AppContext.AppSetting.YtdlAllFormats = (bool)v!;
                    AppContext.WriteManagedMpvConfig();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.YtdlUseManifests),
                Label = lang.SettingsYtdlUseManifests,
                Category = network,
                Description = lang.SettingsHelpYtdlUseManifests,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.YtdlUseManifests,
                Setter = v =>
                {
                    AppContext.AppSetting.YtdlUseManifests = (bool)v!;
                    AppContext.WriteManagedMpvConfig();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.YtdlThumbnails),
                Label = lang.SettingsYtdlThumbnails,
                Category = network,
                Description = lang.SettingsHelpYtdlThumbnails,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("none", lang.OptionValueNo),
                    new OptionChoice("best", lang.OptionValueYtdlThumbnailBest),
                    new OptionChoice("all", lang.OptionValueYtdlThumbnailAll),
                ],
                Getter = () => AppContext.AppSetting.YtdlThumbnails,
                Setter = v =>
                {
                    AppContext.AppSetting.YtdlThumbnails = (string)v!;
                    AppContext.WriteManagedMpvConfig();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.YtdlExclude),
                Label = lang.SettingsYtdlExclude,
                Category = network,
                Description = lang.SettingsHelpYtdlExclude,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.YtdlExclude,
                Setter = v =>
                {
                    AppContext.AppSetting.YtdlExclude = (string)v!;
                    AppContext.WriteManagedMpvConfig();
                }
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.UserAgent),
                Label = lang.SettingsUserAgent,
                Category = network,
                Description = lang.SettingsHelpUserAgent,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.UserAgent,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.UserAgent), AppContext.AppSetting.UserAgent = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.Referrer),
                Label = lang.SettingsReferrer,
                Category = network,
                Description = lang.SettingsHelpReferrer,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.Referrer,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.Referrer), AppContext.AppSetting.Referrer = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.HttpHeaderFields),
                Label = lang.SettingsHttpHeaderFields,
                Category = network,
                Description = lang.SettingsHelpHttpHeaderFields,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.HttpHeaderFields,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.HttpHeaderFields), AppContext.AppSetting.HttpHeaderFields = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.HttpProxy),
                Label = lang.SettingsHttpProxy,
                Category = network,
                Description = lang.SettingsHelpHttpProxy,
                Type = OptionType.String,
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.HttpProxy,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.HttpProxy), AppContext.AppSetting.HttpProxy = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CookiesFile),
                Label = lang.SettingsCookiesFile,
                Category = network,
                Description = lang.SettingsHelpCookiesFile,
                Type = OptionType.String,
                AllowEmpty = true,
                PickFile = true,
                OpenFolder = true,
                FileTypeFilter = [".txt"],
                Getter = () => AppContext.AppSetting.CookiesFile,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.CookiesFile), AppContext.AppSetting.CookiesFile = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.TlsVerify),
                Label = lang.SettingsTlsVerify,
                Category = network,
                Description = lang.SettingsHelpTlsVerify,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.TlsVerify,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.TlsVerify), AppContext.AppSetting.TlsVerify = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.PrefetchPlaylist),
                Label = lang.SettingsPrefetchPlaylist,
                Category = network,
                Description = lang.SettingsHelpPrefetchPlaylist,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.PrefetchPlaylist,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.PrefetchPlaylist), AppContext.AppSetting.PrefetchPlaylist = (bool)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.NetworkTimeout),
                Label = lang.SettingsNetworkTimeout,
                Category = network,
                Description = lang.SettingsHelpNetworkTimeout,
                Type = OptionType.Integer,
                Min = 0,
                Max = 600,
                Step = 5,
                Getter = () => (double)AppContext.AppSetting.NetworkTimeout,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.NetworkTimeout), AppContext.AppSetting.NetworkTimeout = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CurlMaxRedirects),
                Label = lang.SettingsCurlMaxRedirects,
                Category = network,
                Description = lang.SettingsHelpCurlMaxRedirects,
                Type = OptionType.Integer,
                Min = 0,
                Max = 100,
                Step = 1,
                Getter = () => (double)AppContext.AppSetting.CurlMaxRedirects,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.CurlMaxRedirects), AppContext.AppSetting.CurlMaxRedirects = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CurlMaxRetries),
                Label = lang.SettingsCurlMaxRetries,
                Category = network,
                Description = lang.SettingsHelpCurlMaxRetries,
                Type = OptionType.Integer,
                Min = 0,
                Max = 100,
                Step = 1,
                Getter = () => (double)AppContext.AppSetting.CurlMaxRetries,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.CurlMaxRetries), AppContext.AppSetting.CurlMaxRetries = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CurlConnectTimeout),
                Label = lang.SettingsCurlConnectTimeout,
                Category = network,
                Description = lang.SettingsHelpCurlConnectTimeout,
                Type = OptionType.Integer,
                Min = 0,
                Max = 600,
                Step = 5,
                Getter = () => (double)AppContext.AppSetting.CurlConnectTimeout,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.CurlConnectTimeout), AppContext.AppSetting.CurlConnectTimeout = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CurlBufferSize),
                Label = lang.SettingsCurlBufferSize,
                Category = network,
                Description = lang.SettingsHelpCurlBufferSize,
                Type = OptionType.Integer,
                Min = 32768,
                Max = 64 * 1024 * 1024,
                Step = 1024 * 1024,
                Getter = () => (double)AppContext.AppSetting.CurlBufferSize,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.CurlBufferSize), AppContext.AppSetting.CurlBufferSize = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.CurlMaxRequestSize),
                Label = lang.SettingsCurlMaxRequestSize,
                Category = network,
                Description = lang.SettingsHelpCurlMaxRequestSize,
                Type = OptionType.Integer,
                Min = 0,
                Max = 1024 * 1024 * 1024,
                Step = 1024 * 1024,
                Getter = () => (double)AppContext.AppSetting.CurlMaxRequestSize,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.CurlMaxRequestSize), AppContext.AppSetting.CurlMaxRequestSize = Convert.ToInt32(v))
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AutoCreatePlaylist),
                Label = lang.SettingsAutoCreatePlaylist,
                Category = playback,
                Description = lang.SettingsHelpAutoCreatePlaylist,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("no", lang.OptionValueNo),
                    new OptionChoice("filter", lang.OptionValueAutoPlaylistFilter),
                    new OptionChoice("same", lang.OptionValueAutoPlaylistSame),
                ],
                Getter = () => AppContext.AppSetting.AutoCreatePlaylist,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AutoCreatePlaylist), AppContext.AppSetting.AutoCreatePlaylist = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.DirectoryMode),
                Description = lang.SettingsHelpDirectoryMode,
                Label = lang.SettingsDirectoryMode,
                Category = playback,
                Type = OptionType.StringList,
                Choices =
                [
                    new OptionChoice("ignore", lang.OptionValueDirModeIgnore),
                    new OptionChoice("lazy", lang.OptionValueDirModeLazy),
                    new OptionChoice("recursive", lang.OptionValueDirModeRecursive),
                    new OptionChoice("auto", lang.OptionValueAuto),
                ],
                Getter = () => AppContext.AppSetting.DirectoryMode,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.DirectoryMode), AppContext.AppSetting.DirectoryMode = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.DirectoryFilterTypes),
                Label = lang.SettingsDirectoryFilterTypes,
                Category = playback,
                Description = lang.SettingsHelpDirectoryFilterTypes,
                Type = OptionType.MultiList,
                ListSeparator = ',',
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.DirectoryFilterTypes,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.DirectoryFilterTypes), AppContext.AppSetting.DirectoryFilterTypes = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.VideoExts),
                Label = lang.SettingsVideoExts,
                Category = playback,
                Description = lang.SettingsHelpVideoExts,
                Type = OptionType.MultiList,
                ListSeparator = ',',
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.VideoExts,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.VideoExts), AppContext.AppSetting.VideoExts = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ImageExts),
                Description = lang.SettingsHelpImageExts,
                Label = lang.SettingsImageExts,
                Category = playback,
                Type = OptionType.MultiList,
                ListSeparator = ',',
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.ImageExts,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ImageExts), AppContext.AppSetting.ImageExts = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.ImageDisplayDuration),
                Label = lang.SettingsImageDisplayDuration,
                Category = playback,
                Description = lang.SettingsHelpImageDisplayDuration,
                Type = OptionType.Double,
                Min = 0,
                Max = 3600,
                Step = 0.5,
                Getter = () => AppContext.AppSetting.ImageDisplayDuration,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.ImageDisplayDuration), AppContext.AppSetting.ImageDisplayDuration = (double)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.AudioExts),
                Description = lang.SettingsHelpAudioExts,
                Label = lang.SettingsAudioExts,
                Category = audio,
                Type = OptionType.MultiList,
                ListSeparator = ',',
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.AudioExts,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.AudioExts), AppContext.AppSetting.AudioExts = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.WatchLaterOptions),
                Label = lang.SettingsWatchLaterOptions,
                Category = playback,
                Description = lang.SettingsHelpWatchLaterOptions,
                Type = OptionType.MultiList,
                ListSeparator = ',',
                AllowEmpty = true,
                Getter = () => AppContext.AppSetting.WatchLaterOptions,
                Setter = v => ApplyMpv(nameof(AppContext.AppSetting.WatchLaterOptions), AppContext.AppSetting.WatchLaterOptions = (string)v!)
            },

            new Option
            {
                Key = nameof(AppContext.AppSetting.EnableVideoPreview),
                Label = lang.SettingsVideoPreview,
                Category = playback,
                Description = lang.SettingsHelpVideoPreview,
                Type = OptionType.Boolean,
                Getter = () => AppContext.AppSetting.EnableVideoPreview,
                Setter = v =>
                {
                    AppContext.AppSetting.EnableVideoPreview = (bool)v!;
                    AppContext.NotifySettingChanged(nameof(AppContext.AppSetting.EnableVideoPreview), v);
                }
            },

        ];
    }
}
