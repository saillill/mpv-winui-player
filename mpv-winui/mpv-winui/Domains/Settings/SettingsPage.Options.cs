using mpv_winui.Domains.SettingsModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Platform.FileSystem;
using mpv_winui.Lexicon.Language;
using mpv_winui.Domains.Playback;
using mpv_winui.Domains.Settings.Controls;
using mpv_winui.Domains.Settings.Layout;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Storage;
using Microsoft.Windows.Storage.Pickers;

namespace mpv_winui.Domains.Settings;

public sealed partial class SettingsPage
{
    /// <summary>Section captions that fold into a card on the category
    /// overview, as finally resolved by <see cref="ApplyFoldFloor"/> and
    /// <see cref="ApplyCategoryFloor"/>.
    ///
    /// Held on the page because "does this section fold" is a question about the
    /// section and the rows cannot answer it. A pinned row is deliberately not
    /// behind the card, so the rows of a folded section carry two different
    /// AdvancedSection values by design; asking a row -- which is what
    /// SectionSummaries did, through g.First() -- makes the answer depend on
    /// which row sorts first, and merely reordering the list flipped the section
    /// between its card and a wall of rows. The set is the one place the answer
    /// lives.</summary>
    private IReadOnlySet<string> FoldedSections { get; set; } =
        new HashSet<string>(StringComparer.Ordinal);

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
        var sPlayerAppearance = AppContext.AppLang.SectionPlayerAppearance;
        var sProgramLanguageLog = AppContext.AppLang.SectionProgramLanguageLog;
        var sProgramTesting = AppContext.AppLang.SectionProgramTesting;
        var sProgramAssociations = AppContext.AppLang.SectionProgramAssociations;
        var sProgramConfig = AppContext.AppLang.SectionProgramConfig;
        var sWindowPiP = AppContext.AppLang.SectionWindowPiP;
        var sNetworkHttp = AppContext.AppLang.SectionNetworkHttp;
        var sNetworkCurl = AppContext.AppLang.SectionNetworkCurl;
        var sPlayback = AppContext.AppLang.SectionPlayback;
        var sReversePlayback = AppContext.AppLang.SectionReversePlayback;
        var sPlaybackSeeking = AppContext.AppLang.SectionPlaybackSeeking;
        var sPlaybackSeekPreview = AppContext.AppLang.SectionPlaybackSeekPreview;
        var sWatchLaterStorage = AppContext.AppLang.SectionWatchLaterStorage;
        var sLibraryFolders = AppContext.AppLang.SectionLibraryFolders;
        var sVideoDecode = AppContext.AppLang.SectionVideoDecode;
        var sVideoImage = AppContext.AppLang.SectionVideoImage;
        var sVideoFilters = AppContext.AppLang.SectionVideoFilters;
        var sAudioOutput = AppContext.AppLang.SectionAudioOutput;
        var sAudioVolume = AppContext.AppLang.SectionAudioVolume;
        var sAudioExternal = AppContext.AppLang.SectionAudioExternal;
        var sAudioCoverArt = AppContext.AppLang.SectionAudioCoverArt;
        var sSubtitleAss = AppContext.AppLang.SectionSubtitleAss;
        var sSubtitleStyle = AppContext.AppLang.SectionSubtitleStyle;
        var sSubtitlePosition = AppContext.AppLang.SectionSubtitlePosition;
        var sSubtitleBehavior = AppContext.AppLang.SectionSubtitleBehavior;
        var sSubtitleFilter = AppContext.AppLang.SectionSubtitleFilter;
        var sToneMapping = AppContext.AppLang.SectionToneMapping;
        var sTargetColorspace = AppContext.AppLang.SectionTargetColorspace;
        var sColorManagement = AppContext.AppLang.SectionColorManagement;
        var sOsdAppearance = AppContext.AppLang.SectionOsdAppearance;
        var sOsdBehavior = AppContext.AppLang.SectionOsdBehavior;
        var sOsdPosition = AppContext.AppLang.SectionOsdPosition;
        var sWindow = AppContext.AppLang.SectionWindow;
        var sOsdText = AppContext.AppLang.SectionOsdText;
        var sWindowBehavior = AppContext.AppLang.SectionWindowBehavior;
        var sVideoGeometry = AppContext.AppLang.SectionVideoGeometry;
        var sDemuxerPlaylist = AppContext.AppLang.SectionDemuxerPlaylist;
        var sDemuxerBuffering = AppContext.AppLang.SectionDemuxerBuffering;
        var sCache = AppContext.AppLang.SectionCache;
        var sOsdMetadata = AppContext.AppLang.SectionOsdMetadata;
        var sScreenshotLocation = AppContext.AppLang.SectionScreenshotLocation;
        var sScreenshotQuality = AppContext.AppLang.SectionScreenshotQuality;
        var sGpuScaling = AppContext.AppLang.SectionGpuScaling;
        var sGpuBackground = AppContext.AppLang.SectionGpuBackground;
        var sGpuD3d11 = AppContext.AppLang.SectionGpuD3d11;
        var sGpuShaders = AppContext.AppLang.SectionGpuShaders;
        var sVideoSync = AppContext.AppLang.SectionVideoSync;

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

        // The schema-generated rows. 548 mpv options that no hand-written
        // row covers, one Option each, built by GeneratedOptions.cs. They
        // arrive with Category, Section and AdvancedSection already set --
        // the generator knows the group, not us -- so they only need to be
        // in the list before the sectionMap pass below resolves SectionId.
        options.AddRange(BuildGeneratedOptions());

        // The overview carries the sections people tune regularly;
        // fine-grained, seldom-batch-adjusted sections (decoding details,
        // GPU internals, HDR/color math, OSD look, screenshot quality, ...)
        // go behind entry cards. Per-option pins pull the handful of common
        // switches back onto the overview even when they live in an advanced
        // section, so e.g. the hardware-decode switch stays on the Video
        // overview while its codec/decode-detail mates hide in the card.
        //
        // That makes the COMPLEMENT the load-bearing half: every section not
        // listed here puts its options straight on the overview, and a
        // category whose sections are all listed opens to an empty page with
        // nothing but cards. Network and OSD both used to look like that --
        // two categories you could not actually set anything from.
        // A section belongs here only if it presents three or more rows once
        // the pins below are taken out. One or two rows behind a card cost a
        // click and a breadcrumb to reveal almost nothing, and the card is
        // itself a row, so a two-row card is longer than the rows it hides.
        // Below that floor the section is flattened instead -- see the
        // separate list further down for why flattening is written as its own
        // decision rather than left as "not listed here".
        var advancedSections = new HashSet<string>(StringComparer.Ordinal)
        {
            // Program: both of these were cards until the floor was counted
            // properly. Associations keeps two rows and Testing one, so each
            // was a click that revealed less than its own header -- the exact
            // submenu the floor exists to prevent. TestSignal, the two file
            // association rows and UnassociateFiles now sit on the overview.
            //
            // Playback: DemuxerPlaylist (6) stays folded. Seeking (3) does
            // not: three rows is under the floor, so it is flat. The three
            // that used to sit here did not survive either -- SeekPreview
            // keeps only 2 rows past its pinned switch, and ReversePlayback
            // and WatchLaterStorage are 2 rows each.
            //
            // sPlayback itself is folded: 8 rows of resume-on-quit, A/B loop
            // marks and playlist looping. Speed and SavePositionOnQuit are
            // pinned back below, so folding it leaves the two rows people
            // actually reach for on the overview and moves the loop plumbing
            // behind a card.
            sPlayback,

            // Picture controls everyone recognises: brightness, contrast,
            // saturation, hue, sharpen, rotate. Those stay on the overview,
            // and so does hardware decoding -- it is the one Video setting
            // people are told to look for.
            //
            // Everything else in Video is a tuning knob and belongs behind a
            // card: D3D11 internals (adapter, warp, flip model), scaling and
            // chroma-upsampling algorithms, frame interpolation, tone-mapping
            // maths (peak decay rate, scene thresholds), colour-space targets
            // and shader paths. None of those are recognisable to a normal
            // user, so none of them earn a first-level row.
            //
            // VideoDecode and VideoFilters are flat: three rows each is under
            // the floor. VideoDecode used to be folded to hide the single row
            // left beside the pinned Hwdec switch, and still only has Hwdec-
            // Codecs behind it, which is a one-row card.
            //
            // GpuBackground is flat for the same reason -- three rows.
            sVideoSync,
            sColorManagement, sGpuScaling, sGpuShaders, sGpuD3d11,
            sToneMapping, sTargetColorspace, sVideoImage,

            // AudioExternal is flat: three rows is under the floor. CoverArt
            // (7) keeps its card.
            sAudioCoverArt,

            // Subtitles used to keep Style (18) and Behavior (12) on the
            // overview on the argument that they carry font size, colour and
            // track selection, and leaving nothing to set on open would
            // reproduce the complaint Network and OSD once drew.
            //
            // At 30 rows that argument stops holding: the "nothing to set"
            // failure needs the category to open empty, and it does not --
            // SubFontSize is pinned onto the overview, and the four other
            // sections are cards. Style in particular is 18 rows of font
            // metrics and colour pickers, which is the wall of rows the fold
            // exists to prevent, not a shortcut anyone scans.
            sSubtitleStyle, sSubtitleBehavior,
            sSubtitlePosition, sSubtitleFilter, sSubtitleAss,

            // OSD is a decorative overlay, and every one of its sections is
            // tuning: the level row is the one thing people change, and it is
            // pinned back onto the overview below. Appearance (11), Text (9),
            // Behavior (8), Position (5) and Metadata (12) are all font
            // metrics, colours and tag switches -- 45 rows in one category,
            // which is exactly the wall of rows the fold exists to prevent.
            // Only Appearance was previously listed, on the theory that the
            // font knobs are reached often; at 11 rows next to 9 more of the
            // same thing one click away, the split was arbitrary rather than
            // useful, and the user reported the leftover rows as the problem.
            sOsdBehavior, sOsdAppearance, sOsdText, sOsdPosition, sOsdMetadata,

            sScreenshotQuality,
            // "Cache" is a switch people flip per-file, and yt-dlp is the
            // reason half of the user base is here at all -- both stay
            // visible. The transport plumbing (curl knobs, HTTP headers)
            // is the part nobody opens twice.
            //
            // CacheEnabled is pinned below, so folding the section costs the
            // switch nothing; the three rows that leave the overview with it
            // (cache-on-disk, its size and its directory) are setup you do
            // once, not a per-file toggle.
            sCache,
            sNetworkCurl,
            sDemuxerBuffering, sDemuxerPlaylist,
            sNetworkHttp,

            // AudioOutput is 15 rows of device, exclusive-mode, channel-map
            // and buffer tuning. AudioDevice is pinned, so the picker people
            // actually use stays on the overview and the rest folds.
            sAudioOutput,

            // Window is the other 8-row section that was flat: fullscreen-on-
            // launch, border, title bar, aspect-ratio lock, auto resize. All
            // of them are set once and forgotten, and AlwaysOnTop is pinned
            // below, so the one toggle worth a first-level row survives the
            // fold. WindowBehavior (4) and WindowPiP (7) sit next to it on
            // the overview and are what people come to this category for.
            sWindow,
        };

        // The fold set was a list of sections; it is now a lower bound applied
        // to each section's final row count, because a list cannot know how
        // many rows survive the pins. The two failures that produced this:
        // ProgramTesting ended up a card holding ONE row (TestSignal) after
        // its switches were pinned, and ProgramAssociations held TWO -- a
        // card with one row behind it is a click that costs more than the row
        // it hides. Both were listed as advanced and measured only by eye.
        //
        // So the decision is made by ApplyFoldFloor below, once the pins and
        // the sections are both known.

        // Sections that must NOT fold even though they share a category with
        // ones that do. There is no such set today -- the floor decided
        // everything -- but the flat ones are named here rather than left
        // implicit, because "absent from the set above" was how a section
        // ended up with a one-row card that nobody measured.
        //
        // Flat by the three-row floor:
        //   sPlaybackSeekPreview  (2 left  after the pinned preview switch)
        //   sReversePlayback      (2 rows)
        //   sWatchLaterStorage    (2 rows)
        //   sProgramConfig        (2 left  after the pinned update check)
        //   sVideoDecode          (1 left  after the pinned Hwdec switch)
        //
        // Flat because the category would open empty otherwise: AudioVolume
        // (2), ProgramLanguageLog (2) and ProgramInterface (4) are the only
        // non-card sections their categories have left, and a category with
        // one card and no rows reads as broken.
        // Entries people reach for daily: language switch, update check,
        // theme, hardware decode, resume-on-quit, output device, subtitle
        // font size, stay-on-top, cache switch, OSD level.
        var pinnedOverviewOptions = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(AppSettings.CurrentLanguage),   // language
            nameof(AppSettings.CheckForUpdates),   // update toggle
            nameof(AppSettings.ThemeType),         // theme mode
            nameof(AppSettings.Hwdec),             // hardware decoding
            nameof(AppSettings.SavePositionOnQuit),// resume on exit
            nameof(AppSettings.AudioDevice),       // output device
            nameof(AppSettings.SubFontSize),       // subtitle font size
            nameof(AppSettings.AlwaysOnTop),       // keep window on top
            nameof(AppSettings.CacheEnabled),      // cache switch
            nameof(AppSettings.OsdLevel),          // OSD visibility level
            nameof(AppSettings.EnableVideoPreview),// seek preview switch
        };

        // Whether a section folds is read off its FINAL row count, so it is
        // decided after sectionMap resolves every row's Section and after the
        // pins are applied -- see ApplyFoldFloor below. Deciding it here, from
        // the section list alone, is what left a one-row card behind.

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

            // No AdvancedSection here, and the reason is an ordering trap worth
            // spelling out. The flag is derived from the row's own Section, and
            // at this point almost no row has one: hand-written rows are filed
            // by sectionMap, which does not run until much further down, and
            // only the generated rows (and a few hand files) carry an inline
            // Section. Marking here therefore reached the generated half and
            // silently skipped the rest, which left 25 of the 37 folded
            // sections with no marked row at all -- their cards were never
            // built and every row spilled onto the overview. The three
            // Subtitle sections were worse: part of each was marked, so the
            // section-level decision fell through to whichever row happened to
            // sort first and the same section flipped between a card and a wall
            // of rows as soon as anything reordered the list. It is derived
            // after sectionMap instead; see the loop that resolves Section.
        }

        // WinUI 3 settings shell: six top-level domains, ordered the way someone
        // works through a new player - set it up, then the three parts a media
        // file is made of, then the two surfaces around them.
        //
        // This replaced nine categories that followed the mpv manual's chapters
        // (Desktop / Playback / Video / Audio / Subtitles / Window / Network /
        // Shortcuts / OSD). A manual's structure is for looking things up by
        // name; it gave Network its own top-level heading because the manual has
        // a chapter for it, while "streaming stutters" is a playback problem and
        // belongs beside cache and buffering. Shortcuts and OSD were two more
        // headings holding four and five sections - the same bucket problem the
        // old Advanced category had.
        //
        // network, shortcuts and osd still exist as variables and still feed
        // rows; they now resolve to the Playback and Interface captions (see
        // AppLang.cs). They must not appear in this array: it is the rail's
        // order, and three entries sharing one caption would draw the same
        // domain three times.
        var categoryOrder = new[]
        {
            program,
            video,
            audio,
            subtitles,
            playback,
            window,
        };

        var optionOrder = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [nameof(AppSettings.PlayerStyle)] = 0,
            [nameof(AppSettings.ThemeType)] = 1,
            [nameof(AppSettings.BackdropType)] = 1,
            [nameof(AppSettings.UiFont)] = 5,
            [nameof(AppSettings.TestMpvCommandLog)] = 6,
            [nameof(AppSettings.TestOsdMessage)] = 7,
            [nameof(AppSettings.TestSignal)] = 8,
            [nameof(AppSettings.OsdScale)] = 2201,
            [nameof(AppSettings.OsdSpacing)] = 2202,
            [nameof(AppSettings.OsdBold)] = 2203,
            [nameof(AppSettings.OsdItalic)] = 2204,
            [nameof(AppSettings.OsdJustify)] = 2205,
            [nameof(AppSettings.OsdShadowOffset)] = 2206,
            [nameof(AppSettings.OsdFontsDir)] = 2207,
            [nameof(AppSettings.OsdFontProvider)] = 2208,
            [nameof(AppSettings.OsdShaper)] = 2209,
            [nameof(AppSettings.OsdBackColor)] = 2301,
            [nameof(AppSettings.OsdSelectedColor)] = 2302,
            [nameof(AppSettings.OsdSelectedOutlineColor)] = 2303,
            [nameof(AppSettings.OsdScaleByWindow)] = 2401,
            [nameof(AppSettings.OsdMarginYOffset)] = 2501,
            // Order keys are only compared within a section, so the leading
            // two digits name the section (15 behavior, 16 style, 17 position,
            // 18 filter, 19 ass) and the last two order rows inside it.
            [nameof(AppSettings.SubVisibility)] = 1501,
            [nameof(AppSettings.SubsMatchOsLanguage)] = 1502,
            [nameof(AppSettings.SubsWithMatchingAudio)] = 1503,
            [nameof(AppSettings.SubsFallbackForced)] = 1504,
            [nameof(AppSettings.SubScale)] = 1601,
            [nameof(AppSettings.SubSpacing)] = 1602,
            [nameof(AppSettings.SubOutlineColor)] = 1603,
            [nameof(AppSettings.SubGauss)] = 1604,
            [nameof(AppSettings.SubMarginYOffset)] = 1701,
            [nameof(AppSettings.SubAssJustify)] = 1702,
            [nameof(AppSettings.SubAssPruneDelay)] = 1703,
            [nameof(AppSettings.SubFixTiming)] = 1801,
            [nameof(AppSettings.SubFixTimingThreshold)] = 1802,
            [nameof(AppSettings.SubForcedEventsOnly)] = 1803,
            [nameof(AppSettings.SubShaper)] = 1804,
            [nameof(AppSettings.SubBitmapMaxSize)] = 1805,
            [nameof(AppSettings.SubFilterSdh)] = 1806,
            [nameof(AppSettings.SubFilterSdhEnclosures)] = 1807,
            [nameof(AppSettings.SubFilterSdhHarder)] = 1808,
            [nameof(AppSettings.SecondarySubAssOverride)] = 1901,
            [nameof(AppSettings.AbLoopA)] = 1001,
            [nameof(AppSettings.AbLoopB)] = 1002,
            [nameof(AppSettings.AbLoopCount)] = 1003,
            [nameof(AppSettings.CursorAutohide)] = 1101,
            [nameof(AppSettings.CursorAutohideFsOnly)] = 1102,
            [nameof(AppSettings.WindowBorder)] = 1103,
            [nameof(AppSettings.TitleBar)] = 1104,
            [nameof(AppSettings.VideoZoom)] = 1201,
            [nameof(AppSettings.VideoPanX)] = 1202,
            [nameof(AppSettings.VideoPanY)] = 1203,
            [nameof(AppSettings.VideoAlignX)] = 1204,
            [nameof(AppSettings.VideoAlignY)] = 1205,
            [nameof(AppSettings.SecondarySubVisibility)] = 1301,
            [nameof(AppSettings.SecondarySubDelay)] = 1302,
            [nameof(AppSettings.SecondarySubPos)] = 1303,
            [nameof(AppSettings.SecondarySubScale)] = 1304,
            [nameof(AppSettings.GpuApi)] = 1401,
            [nameof(AppSettings.GpuContext)] = 1402,
            [nameof(AppSettings.ScaleAntiring)] = 1403,
            [nameof(AppSettings.CscaleAntiring)] = 1404,
            [nameof(AppSettings.CurrentLanguage)] = 9,
            [nameof(AppSettings.EnableDebugLog)] = 10,
            [nameof(AppSettings.Speed)] = 15,
            [nameof(AppSettings.LoopPlaylist)] = 13,
            [nameof(AppSettings.LoopFile)] = 14,
            [nameof(AppSettings.HrSeek)] = 16,
            [nameof(AppSettings.HrSeekFramedrop)] = 17,
            [nameof(AppSettings.SeekHoldEnabled)] = 18,
            [nameof(AppSettings.EnableVideoPreview)] = 19,
            [nameof(AppSettings.ThumbnailPreviewWidth)] = 20,
            [nameof(AppSettings.ThumbnailUpdateInterval)] = 21,
            [nameof(AppSettings.AudioLanguage)] = 31,            [nameof(AppSettings.SubtitleLanguage)] = 32,
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
            // The six picture controls, in the order a user reads them:
            // brightness, contrast, saturation, gamma, hue, sharpen. They sit
            // in the VideoImage section, and until now they had no order key at
            // all, so they fell to int.MaxValue and landed after every other row
            // of the section -- below the level and geometry rows they belong
            // beside. Prefix 20 is the next free one; 0-116 and the 15xx-25xx
            // families are taken.
            [nameof(AppSettings.PictureBrightness)] = 2001,
            [nameof(AppSettings.PictureContrast)] = 2002,
            [nameof(AppSettings.PictureSaturation)] = 2003,
            [nameof(AppSettings.PictureGamma)] = 2004,
            [nameof(AppSettings.PictureHue)] = 2005,
            [nameof(AppSettings.PictureSharpen)] = 2006,
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
            // ac3/dts downmix for the audio decoder, in the AudioOutput section.
            // It had no order key, so it sorted after the whole section rather
            // than beside the other output-shaped rows it belongs with.
            [nameof(AppSettings.AdLavcDownmix)] = 2101,
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
            // Ties with Import: the escape hatch is declared right after it in
            // BuildProgramBehaviorOptions, and OrderBy is stable, so this keeps
            // the two config-file buttons adjacent without renumbering 220+.
            ["ActionOpenMpvConf"] = 219,
            [nameof(AppSettings.CheckForUpdates)] = 220,
            [nameof(AppSettings.ControlBarLayout)] = 220,
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

            // Window: whether autoresizing the window follows the video. It sits
            // in the Window section with the other sizing rows and needs an
            // order key for the same reason the picture controls above did --
            // without one it fell to int.MaxValue and sorted past them.
            [nameof(AppSettings.AutoWindowResize)] = 2601,
        };

        // Parsed input.conf bindings keep their original order after the capture row.
        var shortcutOrder = 1000;
        foreach (var o in options.Where(o => o.Key.StartsWith("Shortcut:", StringComparison.Ordinal)))
        {
            optionOrder[o.Key] = shortcutOrder++;
        }

// Section display order within a category. Values that collide are
        // harmless: ordering groups first by category, then by the minimum
        // option order inside the group, so an equal section index only
        // matters between two sections of the same category. The single-option
        // sections (testing, interpolation, track language/fallback, ...) were
        // folded into their neighbours, so their keys no longer appear here.
        // Order within each category. The value only ever compares against
        // another section from the SAME category (the options are grouped by
        // category first), so the numbers are a per-category sequence, not a
        // global one. Blocked by category, and every section of a category
        // gets a number, so a newly added section cannot silently fall to
        // int.MaxValue and jump to the bottom of its category.
        var sectionOrder = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            // program -- Interface, LanguageLog, Config, then the two folding
            // sections. Testing and Associations used to be numbered from
            // inside the "advanced" category's removed block and collided with
            // Config at index 2; the collision was invisible (the order fell
            // through to the minimum optionOrder inside each group) but the
            // table claimed two sections were at the same position, so the
            // next person to add a sibling would have been reading a lie.
            [sPlayerAppearance] = 0,
            [sProgramLanguageLog] = 2,
            [sProgramConfig] = 3,
            [sProgramTesting] = 4,
            [sProgramAssociations] = 5,

            // playback
            [sPlayback] = 0,
            [sReversePlayback] = 1,
            [sPlaybackSeeking] = 2,
            [sPlaybackSeekPreview] = 3,
            [sWatchLaterStorage] = 5,

            // video
            //
            // The folder lists come first: "what is in my library" is the question this
            // section answers. The numbers stay dense from 0 - the table is a
            // per-category sequence where the smallest sorts first, so "before 0" is
            // expressed by renumbering, not by a negative value.
            [sLibraryFolders] = 0,
            [sVideoDecode] = 1,
            [sVideoImage] = 2,
            [sVideoFilters] = 3,
            [sVideoSync] = 4,
            [sGpuScaling] = 5,
            [sGpuBackground] = 6,
            [sGpuD3d11] = 7,
            [sGpuShaders] = 7,
            [sToneMapping] = 8,
            [sTargetColorspace] = 9,
            [sColorManagement] = 10,
            [sScreenshotLocation] = 11,
            [sScreenshotQuality] = 12,

            // The five video-geometry rows (zoom, pan x/y, align x/y) are the
            // direct-manipulation controls, so they end Video rather than
            // interrupting the render stack that Decode..ColorManagement walks
            // in pipeline order. Like the other two late additions below, this
            // section previously had no entry at all and fell to int.MaxValue,
            // which happened to land it here already -- the order is now
            // written down instead of being an accident of a missing key.
            [sVideoGeometry] = 13,

            // audio
            [sAudioOutput] = 0,
            [sAudioVolume] = 1,
            [sAudioExternal] = 2,
            [sAudioCoverArt] = 3,

            // subtitles -- five sections. Style and Behavior stay on the
            // overview (font size and track choice are the two things people
            // actually open Subtitles for); Position, Filter and Ass are past
            // the point where a section reads as one screen and go behind
            // cards.
            [sSubtitleStyle] = 0,
            [sSubtitleBehavior] = 1,
            [sSubtitlePosition] = 2,
            [sSubtitleFilter] = 3,
            [sSubtitleAss] = 4,

            // window -- the four window-behaviour rows (cursor autohide and its
            // fullscreen-only variant, the border, the title bar) sit between
            // the launch/sizing section and PiP. WindowBehavior had no entry,
            // so it sorted to int.MaxValue and rendered after PiP -- a
            // five-option floating window's section showing below the four
            // rows that describe the main window itself.
            [sWindow] = 0,
            [sWindowBehavior] = 1,
            [sWindowPiP] = 2,

            // network
            [sCache] = 0,
            [sDemuxerBuffering] = 1,
            [sDemuxerPlaylist] = 2,
            [sNetworkHttp] = 3,
            [sNetworkCurl] = 4,

            // osd -- Behaviour, the text metrics (Appearance, Text), then the
            // two positional sections. OsdText carries the nine rows that set
            // how OSD text is drawn (font, scale, spacing, bold/italic,
            // justify, shadow, fonts dir, provider, shaper), so it belongs
            // directly after Appearance. It had NO entry here at all, which
            // silently sorted it to int.MaxValue -- past Metadata, at the
            // bottom of the category, on the far side of the very sections it
            // is the metrics for.
            [sOsdBehavior] = 0,
            [sOsdAppearance] = 1,
            [sOsdText] = 2,
            [sOsdPosition] = 3,
            [sOsdMetadata] = 4,
        };

        var sectionMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // program
            [nameof(AppSettings.PlayerStyle)] = sPlayerAppearance,
            [nameof(AppSettings.ThemeType)] = sPlayerAppearance,
            [nameof(AppSettings.BackdropType)] = sPlayerAppearance,
            [nameof(AppSettings.UiFont)] = sPlayerAppearance,
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
            // Resume playback rides with plain Playback, not with the
            // watch-later storage section: it is the switch, the storage
            // section is where the file goes.
            [nameof(AppSettings.SavePositionOnQuit)] = sPlayback,
            [nameof(AppSettings.ResumePlayback)] = sPlayback,
            // watchLater
            [nameof(AppSettings.WatchLaterOptions)] = sWatchLaterStorage,
            [nameof(AppSettings.WatchLaterDir)] = sWatchLaterStorage,
            // library folders
            [nameof(AppSettings.VideoLibraryFolders)] = sLibraryFolders,
            [nameof(AppSettings.MusicLibraryFolders)] = sLibraryFolders,
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
            [nameof(AppSettings.HdrOverrideMode)] = sToneMapping,
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
            [nameof(AppSettings.Interpolation)] = sVideoSync,
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
            // subtitles -- five sections grouped by what the option changes,
            // not by the mpv prefix alone: the bare `sub-` prefix covers
            // 52 options spanning three different concerns, so "style",
            // "placement" and "loading" split on meaning. `secondary-sub-*`
            // and the bare `subs-*` matchers live with the section they
            // belong to semantically rather than getting headings of their
            // own; both used to be two- and four-option sections.
            //
            // Note this file used to declare two parallel alias sets
            // (sSubtitle* and sSub*) for the same AppLang strings, which is
            // how a section ended up with two spellings and how the option
            // count above reported doubled numbers. One id per section now.
            [nameof(AppSettings.AudioLanguage)] = sAudioOutput,
            [nameof(AppSettings.SubtitleLanguage)] = sSubtitleBehavior,
            [nameof(AppSettings.SubFallback)] = sSubtitleBehavior,
            [nameof(AppSettings.SubsFallbackForced)] = sSubtitleBehavior,
            [nameof(AppSettings.SubsMatchOsLanguage)] = sSubtitleBehavior,
            [nameof(AppSettings.SubsWithMatchingAudio)] = sSubtitleBehavior,
            [nameof(AppSettings.SubAuto)] = sSubtitleBehavior,
            [nameof(AppSettings.SubFilePaths)] = sSubtitleBehavior,
            [nameof(AppSettings.SubDelay)] = sSubtitleBehavior,
            [nameof(AppSettings.SubVisibility)] = sSubtitleBehavior,
            [nameof(AppSettings.SubClearOnSeek)] = sSubtitleBehavior,
            [nameof(AppSettings.SubCodePage)] = sSubtitleBehavior,
            [nameof(AppSettings.SecondarySubVisibility)] = sSubtitleBehavior,
            // style
            [nameof(AppSettings.SubFontSize)] = sSubtitleStyle,
            [nameof(AppSettings.SubFont)] = sSubtitleStyle,
            [nameof(AppSettings.SubFontFile)] = sSubtitleStyle,
            [nameof(AppSettings.SubFontProvider)] = sSubtitleStyle,
            [nameof(AppSettings.SubColor)] = sSubtitleStyle,
            [nameof(AppSettings.SubBackColor)] = sSubtitleStyle,
            [nameof(AppSettings.SubBorderColor)] = sSubtitleStyle,
            [nameof(AppSettings.SubOutlineColor)] = sSubtitleStyle,
            [nameof(AppSettings.SubOutlineSize)] = sSubtitleStyle,
            [nameof(AppSettings.SubShadowOffset)] = sSubtitleStyle,
            [nameof(AppSettings.SubBold)] = sSubtitleStyle,
            [nameof(AppSettings.SubItalic)] = sSubtitleStyle,
            [nameof(AppSettings.SubBlur)] = sSubtitleStyle,
            [nameof(AppSettings.SubGauss)] = sSubtitleStyle,
            [nameof(AppSettings.SubSpacing)] = sSubtitleStyle,
            [nameof(AppSettings.SubLineSpacing)] = sSubtitleStyle,
            [nameof(AppSettings.SubHinting)] = sSubtitleStyle,
            // The brightness a text subtitle is drawn at on an HDR output is a
            // property of how the text looks, so it sits with the colours above
            // rather than with the track-selection knobs. It used to be filed
            // under Behavior; that is how it went missing when the subtitles
            // map was rebuilt -- the entry was dropped while the option itself
            // stayed, and BuildSettings() throws on exactly that mismatch.
            [nameof(AppSettings.SubHdrPeak)] = sSubtitleStyle,
            // position -- including the ASS layout knobs and the second track's
            // placement, which are about where a line lands, not what it looks
            // like.
            [nameof(AppSettings.SubPos)] = sSubtitlePosition,
            [nameof(AppSettings.SubAlignX)] = sSubtitlePosition,
            [nameof(AppSettings.SubAlignY)] = sSubtitlePosition,
            [nameof(AppSettings.SubJustify)] = sSubtitlePosition,
            [nameof(AppSettings.SubMarginX)] = sSubtitlePosition,
            [nameof(AppSettings.SubMarginY)] = sSubtitlePosition,
            [nameof(AppSettings.SubMarginYOffset)] = sSubtitlePosition,
            [nameof(AppSettings.SubUseMargins)] = sSubtitlePosition,
            [nameof(AppSettings.SubScale)] = sSubtitlePosition,
            [nameof(AppSettings.SubScaleByWindow)] = sSubtitlePosition,
            [nameof(AppSettings.SubScaleSigns)] = sSubtitlePosition,
            [nameof(AppSettings.SubAssJustify)] = sSubtitlePosition,
            [nameof(AppSettings.SubAssScaleWithWindow)] = sSubtitlePosition,
            [nameof(AppSettings.SubAssForceMargins)] = sSubtitlePosition,
            [nameof(AppSettings.SubAssPruneDelay)] = sSubtitlePosition,
            // filter -- accessibility clean-up, timing repair, and the ceiling
            // on bitmap subtitle size.
            [nameof(AppSettings.SubFilterSdh)] = sSubtitleFilter,
            [nameof(AppSettings.SubFilterSdhEnclosures)] = sSubtitleFilter,
            [nameof(AppSettings.SubFilterSdhHarder)] = sSubtitleFilter,
            [nameof(AppSettings.SubFixTiming)] = sSubtitleFilter,
            [nameof(AppSettings.SubFixTimingThreshold)] = sSubtitleFilter,
            [nameof(AppSettings.SubForcedEventsOnly)] = sSubtitleFilter,
            [nameof(AppSettings.SubShaper)] = sSubtitleFilter,
            [nameof(AppSettings.SubBitmapMaxSize)] = sSubtitleFilter,
            // ass -- renderer-specific overrides that only matter for styled
            // subtitles, plus image-subtitle handling.
            [nameof(AppSettings.SubAssOverride)] = sSubtitleAss,
            [nameof(AppSettings.SubAssStyleOverrides)] = sSubtitleAss,
            [nameof(AppSettings.SubAssUseVideoData)] = sSubtitleAss,
            [nameof(AppSettings.SubAssVideoAspectOverride)] = sSubtitleAss,
            [nameof(AppSettings.SubAssVsfilterColorCompat)] = sSubtitleAss,
            [nameof(AppSettings.SubEmbeddedFonts)] = sSubtitleAss,
            [nameof(AppSettings.BlendSubtitles)] = sSubtitleAss,
            [nameof(AppSettings.StretchImageSubsToScreen)] = sSubtitleAss,
            [nameof(AppSettings.ImageSubsVideoResolution)] = sSubtitleAss,
            [nameof(AppSettings.ImageSubsHdrPeak)] = sSubtitleAss,
            [nameof(AppSettings.SecondarySubAssOverride)] = sSubtitleAss,
            [nameof(AppSettings.SecondarySubDelay)] = sSubtitleAss,
            [nameof(AppSettings.SecondarySubPos)] = sSubtitleAss,
            [nameof(AppSettings.SecondarySubScale)] = sSubtitleAss,
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
            ["ActionOpenMpvConf"] = sProgramConfig,
            [nameof(AppSettings.ControlBarLayout)] = sPlayerAppearance,
            // ControlBarHiddenIcons is deliberately absent: it is the pre-split
            // name of a per-style pair (ControlBarHiddenIconsClassic /
            // ...ModernX), so no row can ever carry it again. The AppSettings
            // property stays, because AppSettings.cs reads it once to migrate a
            // stored value into the pair; a sectionMap or optionOrder entry
            // would only be an entry the tree can never resolve.
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
            // Format sits with location, not with the per-codec quality
            // knobs: choosing png vs jpg is the one screenshot decision
            // everyone makes, tuning jxl effort is not.
            [nameof(AppSettings.ScreenshotFormat)] = sScreenshotLocation,
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
            [nameof(AppSettings.TestMpvCommandLog)] = sProgramLanguageLog,
            [nameof(AppSettings.TestOsdMessage)] = sOsdBehavior,
            // TestSignal rides with the other experimental switches, not with
            // plain Playback: the general playback section holds speed and
            // loop modes that people use constantly, and a diagnostic tone
            // generator does not belong beside them.
            [nameof(AppSettings.TestSignal)] = sProgramTesting,
            [nameof(AppSettings.OsdScale)] = sOsdText,
            [nameof(AppSettings.OsdSpacing)] = sOsdText,
            [nameof(AppSettings.OsdBold)] = sOsdText,
            [nameof(AppSettings.OsdItalic)] = sOsdText,
            [nameof(AppSettings.OsdJustify)] = sOsdText,
            [nameof(AppSettings.OsdShadowOffset)] = sOsdText,
            [nameof(AppSettings.OsdFontsDir)] = sOsdText,
            [nameof(AppSettings.OsdFontProvider)] = sOsdText,
            [nameof(AppSettings.OsdShaper)] = sOsdText,
            [nameof(AppSettings.OsdBackColor)] = sOsdAppearance,
            [nameof(AppSettings.OsdSelectedColor)] = sOsdAppearance,
            [nameof(AppSettings.OsdSelectedOutlineColor)] = sOsdAppearance,
            [nameof(AppSettings.OsdScaleByWindow)] = sOsdBehavior,
            [nameof(AppSettings.OsdMarginYOffset)] = sOsdPosition,
            [nameof(AppSettings.AbLoopA)] = sPlayback,
            [nameof(AppSettings.AbLoopB)] = sPlayback,
            [nameof(AppSettings.AbLoopCount)] = sPlayback,
            [nameof(AppSettings.CursorAutohide)] = sWindowBehavior,
            [nameof(AppSettings.CursorAutohideFsOnly)] = sWindowBehavior,
            [nameof(AppSettings.WindowBorder)] = sWindowBehavior,
            [nameof(AppSettings.TitleBar)] = sWindowBehavior,
            [nameof(AppSettings.VideoZoom)] = sVideoGeometry,
            [nameof(AppSettings.VideoPanX)] = sVideoGeometry,
            [nameof(AppSettings.VideoPanY)] = sVideoGeometry,
            [nameof(AppSettings.VideoAlignX)] = sVideoGeometry,
            [nameof(AppSettings.VideoAlignY)] = sVideoGeometry,
            [nameof(AppSettings.GpuApi)] = sGpuD3d11,
            [nameof(AppSettings.GpuContext)] = sGpuD3d11,
            [nameof(AppSettings.ScaleAntiring)] = sGpuScaling,
            [nameof(AppSettings.CscaleAntiring)] = sGpuScaling,
        };

        // The generated rows bring their own entries. It is one call rather
        // than 548 lines here because the entries are a function of the mpv
        // schema: regenerating is how they stay current, and a hand-edited
        // copy in this file would be the one thing the generator cannot
        // refresh. Everything the method writes -- section map, section
        // order, fold set -- is consumed by the code below.
        RegisterGeneratedSections(sectionMap, sectionOrder, advancedSections);

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

            // Section captions are localized, so the layout keys on a stable id.
            // Resolved once here (the map is already cached) and carried on the
            // row, because a user-created folder has no caption in AppLang.
            option.SectionId = SettingsSections.IdFor(option.Section);

            // The fold flag, derived only now that every row knows its Section.
            //
            // Assigned in both directions rather than only being set: a row that
            // arrives with the flag already true -- which is every generated row,
            // since the generator stamps it -- has to be able to come back to
            // false, or a section the floor later un-folds keeps rows that claim
            // to be hidden.
            //
            // The flag's meaning is "this row is behind its section's card",
            // not "this row's section is advanced". That distinction is what
            // makes the pins work: a pinned row is a row the user should keep
            // seeing, so it is false even when its section folds, and the list
            // renders it above the card instead of swallowing it.
            option.AdvancedSection = advancedSections.Contains(option.Section ?? string.Empty);
            if (pinnedOverviewOptions.Contains(option.Key))
            {
                option.AdvancedSection = false;
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

        // Every row now knows its Section (resolved from sectionMap above) and
        // every row knows whether it is pinned to the overview (applied in the
        // loop above). That is exactly the input the fold decision needs, so
        // it happens here rather than where the section list was written.
        ApplyFoldFloor(options, advancedSections, pinnedOverviewOptions);

        // A card earns its click, but a category still has to be a page. Two
        // separate questions, so two separate floors -- see ApplyCategoryFloor.
        ApplyCategoryFloor(options, advancedSections, pinnedOverviewOptions);

        // The resolved set, kept for the page. Publish it as a copy: the local
        // is about to go out of scope and a copy cannot be mutated by anything
        // that later receives it.
        FoldedSections = new HashSet<string>(advancedSections, StringComparer.Ordinal);

        // Customize-mode captions are localized, so refresh them with the rest
        // of the page before the stored layout is merged in.
        foreach (var option in options)
        {
            option.Edit.Refresh();
        }

        // Hand-added options are materialized into real rows before the layout
        // runs, so they participate in ordering, folder moves and renaming
        // exactly like a built-in row.
        options.AddRange(BuildAddedOptions());

        // Section (2nd-level) hiding/reordering first, then the per-row
        // customization: an explicitly ranked row still wins over its section's
        // position, which is the more specific intent.
        ApplySectionLayout(options);
        ApplyLayout(options);

        return options;
    }

    /// <summary>
    /// Minimum rows a section must keep on the overview for its card to be
    /// worth opening. Below this the section is flattened into the category.
    /// </summary>
    private const int FoldFloor = 4;

    /// <summary>
    /// Drops sections whose card would hold fewer than <see cref="FoldFloor"/>
    /// rows, so the second-level menu never opens onto one or two options.
    ///
    /// The check runs on the row count that actually survives the pins, not on
    /// the section's raw size. Those differ whenever a pinned row is the only
    /// reason a section had a card: ProgramTesting was listed as advanced and
    /// held four rows, three of which are pinned to the overview -- so the
    /// card opened onto TestSignal alone. Counting the survivors is the only
    /// way to see that, and it needs the rows to have their final Section,
    /// which is why this runs after sectionMap rather than beside the list.
    ///
    /// One-way: a section can lose the fold mark here but never gain it. The
    /// list in BuildSettings is the intent -- a large section nobody asked to
    /// fold stays flat -- and this only vetoes.
    /// </summary>
    private static void ApplyFoldFloor(
        List<Option> options,
        ISet<string> advancedSections,
        ISet<string> pinnedOverviewOptions)
    {
        var rowsBySection = options
            .Where(o => o.Section is not null)
            .GroupBy(o => o.Section!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var pinnedBySection = options
            .Where(o => o.Section is not null && pinnedOverviewOptions.Contains(o.Key))
            .GroupBy(o => o.Section!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        foreach (var section in advancedSections.ToList())
        {
            var behindTheCard = rowsBySection.GetValueOrDefault(section)
                                - pinnedBySection.GetValueOrDefault(section);
            if (behindTheCard < FoldFloor)
            {
                advancedSections.Remove(section);

                // The rows were marked advanced when the section was; clear it
                // so they actually render on the overview. Leaving the flag set
                // would hide them behind a card the section no longer has.
                foreach (var option in options.Where(o => o.Section == section))
                {
                    if (!pinnedOverviewOptions.Contains(option.Key))
                    {
                        option.AdvancedSection = false;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Minimum rows a category must keep on its overview. Roughly one screen:
    /// the settings window bottoms out at 560px and a row is 72px, so eight is
    /// the number of rows needed for the page to not require scrolling to see
    /// what it contains.
    /// </summary>
    private const int CategoryFloor = 8;

    /// <summary>
    /// Flattens sections until each category keeps at least
    /// <see cref="CategoryFloor"/> rows of its own.
    ///
    /// <see cref="ApplyFoldFloor"/> asks whether a card is worth opening; this
    /// asks whether the category it lives in is still a page. They are different
    /// questions and a category can fail the second while every one of its cards
    /// passes the first. Network, OSD and Subtitles did exactly that: every
    /// section they own was a legitimate card, so once folding actually reached
    /// them the overview became six cards with a single pinned row between them
    /// -- a directory, not a settings page. The user has no way to tell which
    /// card to open when none of them is on screen as rows.
    ///
    /// Sections are flattened smallest-first, so the fewest cards are disturbed
    /// and the big deep sections -- the ones the fold exists for -- stay folded.
    /// Pinned rows are counted as already visible and are never the reason a
    /// section gets flattened.
    ///
    /// Runs after <see cref="ApplyFoldFloor"/> so it sees the same surviving
    /// sections the cards will be built from; a section already flattened there
    /// is simply not a candidate.
    /// </summary>
    private static void ApplyCategoryFloor(
        List<Option> options,
        ISet<string> advancedSections,
        ISet<string> pinnedOverviewOptions)
    {
        foreach (var category in options
            .Where(o => o.Category is not null && o.Section is not null)
            .GroupBy(o => o.Category!, StringComparer.Ordinal))
        {
            var rows = category.ToList();
            var visible = rows.Count(o => !o.AdvancedSection);
            if (visible >= CategoryFloor)
            {
                continue;
            }

            // Smallest card first: reaching the floor with the least disruption
            // keeps the large sections folded, which is the point of the fold.
            var candidates = rows
                .Where(o => o.AdvancedSection)
                .GroupBy(o => o.Section!, StringComparer.Ordinal)
                .Select(g => (Section: g.Key, Rows: g.Count()))
                .OrderBy(c => c.Rows)
                .ToList();

            foreach (var candidate in candidates)
            {
                if (visible >= CategoryFloor)
                {
                    break;
                }

                advancedSections.Remove(candidate.Section);
                foreach (var option in rows.Where(o => o.Section == candidate.Section))
                {
                    if (!pinnedOverviewOptions.Contains(option.Key))
                    {
                        option.AdvancedSection = false;
                    }
                }

                // Recomputed rather than incremented: a section that is entirely
                // pinned contributes nothing, and adding its row count would
                // count rows that were never hidden.
                visible = rows.Count(o => !o.AdvancedSection);
            }
        }
    }

    /// <summary>
    /// Turns each stored <see cref="CustomOption"/> into a settings row.
    ///
    /// These have no AppSettings property behind them: the value lives in the
    /// layout file and is pushed straight to mpv through the raw key, which is
    /// what lets an arbitrary mpv option be reachable without a code change.
    /// </summary>
    private List<Option> BuildAddedOptions()
    {
        if (_layout.Added.Count == 0)
        {
            return [];
        }

        var added = new List<Option>(_layout.Added.Count);
        foreach (var custom in _layout.Added)
        {
            var category = SettingsSections.CategoryCaptionFor(custom.CategoryKey);
            if (category is null)
            {
                // The category no longer exists (a build removed it): skip the
                // row rather than invent a category nobody can navigate to.
                continue;
            }

            var mpvKey = custom.MpvKey;
            var option = new Option
            {
                Key = custom.Id,
                Label = custom.Label,
                Description = custom.Description,
                Category = category,
                Type = custom.Kind switch
                {
                    CustomOptionKinds.Boolean => OptionType.Boolean,
                    CustomOptionKinds.Choice => OptionType.String,
                    _ => OptionType.String,
                },
                // The value lives in the layout file, not in AppSettings, so the
                // getter reads the stored copy: there is no built-in property to
                // consult for an option the user invented.
                Getter = () => custom.Value,
                Setter = v =>
                {
                    var text = v?.ToString() ?? string.Empty;
                    custom.Value = text;
                    SaveLayout();
                    AppContext.SendMpvCommand($"set {mpvKey} {text}");
                },
            };

            if (custom.Kind == CustomOptionKinds.Choice && custom.Choices.Count > 0)
            {
                option.Choices = custom.Choices
                    .Select(choice => new OptionChoice(choice, choice))
                    .ToList();
                // The presets are the whole vocabulary of the option, so no
                // free-text escape hatch is offered.
                option.AllowCustom = false;
            }

            // Keep the placeholder meaningful when no value has been set yet.
            if (custom.Kind != CustomOptionKinds.Boolean)
            {
                option.Placeholder = custom.Value;
            }

            added.Add(option);
        }

        return added;
    }
}
