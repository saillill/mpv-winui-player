using mpv_winui.Lexicon.Language;
using System;
using System.Collections.Generic;

namespace mpv_winui.Domains.Settings.Layout;

/// <summary>
/// The one place that knows what a settings category and section is.
///
/// Categories and sections used to be identified by their localized caption,
/// which every consumer then had to work around separately: a 51-branch
/// string comparison in <c>SectionMeta</c> for the icon and description, a
/// reflection sweep over AppLang property names here for a stable id, and a
/// stored id in the layout draft. Three answers to one question. This table is
/// the answer instead.
///
/// The <c>Id</c> is the AppLang property name, which is stable across
/// languages -- that is what a persisted layout stores. Captions, icons and
/// descriptions are resolved from the table on demand, so switching language
/// never orphans a layout, an icon or a rename.
///
/// Note the descriptions are their own AppLang properties ("SectionDescXxx"),
/// not a naming convention applied to the caption: the pairing is explicit
/// here, and one of them happens to share its text with the section it
/// describes, which is why a caption-keyed lookup was ambiguous.
/// </summary>
internal static class SettingsSections
{
    internal sealed record Category(string Key, Func<AppLang, string> Caption);

    internal sealed record Section(
        string Id,
        string Icon,
        Func<AppLang, string> Caption,
        Func<AppLang, string> Description);

    /// <summary>Top-level categories, in settings-window order.</summary>
    ///
    /// Six domains, ordered the way someone works through a new player: set it
    /// up (Program), then the three things a media file consists of (Video,
    /// Audio, Subtitles), then the two surfaces around them (Playback for how
    /// files are opened and moved through, Interface for what you see and
    /// click).
    ///
    /// The nine categories this replaces followed the mpv manual's chapters.
    /// That is a reference document's structure, not a user's: it gives Network
    /// its own top-level heading because the manual has a chapter for it, while
    /// "streaming stutters" is a playback problem and belongs beside cache and
    /// buffering. Shortcuts and OSD were two more top-level headings holding
    /// four and five sections - the same "bucket" problem the old Advanced
    /// category had.
    ///
    /// Several SettingsCategory* properties now resolve to the same caption on
    /// purpose; see the comment on them in AppLang.cs. This array lists each
    /// domain once, in order.
    internal static readonly Category[] Categories =
    [
        new("program", l => l.SettingsCategoryProgram),
        new("video", l => l.SettingsCategoryVideo),
        new("audio", l => l.SettingsCategoryAudio),
        new("subtitles", l => l.SettingsCategorySubtitles),
        new("playback", l => l.SettingsCategoryPlayback),
        new("interface", l => l.SettingsCategoryWindow),
    ];

    /// <summary>
    /// Sections, in the order the catalog used to list them. That order is not
    /// load-bearing -- the window's order comes from the section map in
    /// <c>SettingsPage.Options.cs</c> -- but it is kept so this table reads as
    /// a faithful replacement rather than a reshuffle.
    ///
    /// The icon is a Segoe Fluent Icons glyph; see
    /// <c>docs/settings-contract.md</c> §2 for how they are chosen (rendered and
    /// inspected, not guessed from the code point).
    /// </summary>
    internal static readonly Section[] All =
    [
        new("SectionProgramLanguageLog", "\uE8C1", l => l.SectionProgramLanguageLog, l => l.SectionDescProgramLanguageLog),
        new("SectionProgramConfig", "\uECC5", l => l.SectionProgramConfig, l => l.SectionDescProgramConfig),
        new("SectionProgramAssociations", "\uE8E5", l => l.SectionProgramAssociations, l => l.SectionDescProgramAssociations),
        new("SectionProgramTesting", "\uE9D9", l => l.SectionProgramTesting, l => l.SectionDescProgramTesting),
        new("SectionPlayerAppearance", "\uE7F8", l => l.SectionPlayerAppearance, l => l.SectionDescPlayerAppearance),
        new("SectionPlayback", "\uE102", l => l.SectionPlayback, l => l.SectionDescPlayback),
        new("SectionPlaybackSeeking", "\uE786", l => l.SectionPlaybackSeeking, l => l.SectionDescPlaybackSeeking),
        new("SectionPlaybackSeekPreview", "\uE720", l => l.SectionPlaybackSeekPreview, l => l.SectionDescPlaybackSeekPreview),
        new("SectionReversePlayback", "\uE8AB", l => l.SectionReversePlayback, l => l.SectionDescReversePlayback),
        new("SectionWatchLaterStorage", "\uE8B7", l => l.SectionWatchLaterStorage, l => l.SectionDescWatchLaterStorage),
        // The two libraries' bound folders. Sits in Video rather than in Audio because
        // both rows do the same thing to the same kind of thing, and an unbind that
        // lives in two categories depending on media kind is harder to find than one.
        new("SectionLibraryFolders", "\uE8B7", l => l.SectionLibraryFolders, l => l.SectionDescLibraryFolders),
        new("SectionVideoDecode", "\uE714", l => l.SectionVideoDecode, l => l.SectionDescVideoDecode),
        new("SectionVideoImage", "\uE7F4", l => l.SectionVideoImage, l => l.SectionDescVideoImage),
        new("SectionVideoFilters", "\uE70F", l => l.SectionVideoFilters, l => l.SectionDescVideoFilters),
        new("SectionVideoSync", "\uE895", l => l.SectionVideoSync, l => l.SectionDescVideoSync),
        new("SectionVideoGeometry", "\uE7C2", l => l.SectionVideoGeometry, l => l.SectionDescVideoGeometry),
        new("SectionGpuScaling", "\uE71D", l => l.SectionGpuScaling, l => l.SectionDescGpuScaling),
        new("SectionGpuD3d11", "\uE964", l => l.SectionGpuD3d11, l => l.SectionDescGpuD3d11),
        new("SectionColorManagement", "\uE790", l => l.SectionColorManagement, l => l.SectionDescColorManagement),
        new("SectionGpuShaders", "\uE9D2", l => l.SectionGpuShaders, l => l.SectionDescGpuShaders),
        new("SectionGpuBackground", "\uE756", l => l.SectionGpuBackground, l => l.SectionDescGpuBackground),
        new("SectionToneMapping", "\uE706", l => l.SectionToneMapping, l => l.SectionDescToneMapping),
        new("SectionTargetColorspace", "\uE914", l => l.SectionTargetColorspace, l => l.SectionDescTargetColorspace),
        new("SectionDemuxerBuffering", "\uEC4E", l => l.SectionDemuxerBuffering, l => l.SectionDescDemuxerBuffering),
        new("SectionDemuxerPlaylist", "\uE8FD", l => l.SectionDemuxerPlaylist, l => l.SectionDescDemuxerPlaylist),
        new("SectionCache", "\uE74E", l => l.SectionCache, l => l.SectionDescCache),
        new("SectionNetworkHttp", "\uE702", l => l.SectionNetworkHttp, l => l.SectionDescNetworkHttp),
        new("SectionNetworkCurl", "\uE8EA", l => l.SectionNetworkCurl, l => l.SectionDescNetworkCurl),
        new("SectionAudioVolume", "\uE76E", l => l.SectionAudioVolume, l => l.SectionDescAudioVolume),
        new("SectionAudioOutput", "\uE7F6", l => l.SectionAudioOutput, l => l.SectionDescAudioOutput),
        new("SectionAudioExternal", "\uE8D6", l => l.SectionAudioExternal, l => l.SectionDescAudioExternal),
        new("SectionAudioCoverArt", "\uEB9F", l => l.SectionAudioCoverArt, l => l.SectionDescAudioCoverArt),
        new("SectionSubtitleBehavior", "\uE7DE", l => l.SectionSubtitleBehavior, l => l.SectionDescSubtitleBehavior),
        new("SectionSubtitleStyle", "\uE8D2", l => l.SectionSubtitleStyle, l => l.SectionDescSubtitleStyle),
        new("SectionSubtitlePosition", "\uE7C4", l => l.SectionSubtitlePosition, l => l.SectionDescSubtitlePosition),
        new("SectionSubtitleFilter", "\uE8D4", l => l.SectionSubtitleFilter, l => l.SectionDescSubtitleFilter),
        new("SectionSubtitleAss", "\uE713", l => l.SectionSubtitleAss, l => l.SectionDescSubtitleAss),
        new("SectionOsdBehavior", "\uE7EE", l => l.SectionOsdBehavior, l => l.SectionDescOsdBehavior),
        new("SectionOsdAppearance", "\uE2B1", l => l.SectionOsdAppearance, l => l.SectionDescOsdAppearance),
        new("SectionOsdText", "\uED1F", l => l.SectionOsdText, l => l.SectionDescOsdText),
        new("SectionOsdPosition", "\uE787", l => l.SectionOsdPosition, l => l.SectionDescOsdPosition),
        new("SectionOsdMetadata", "\uEA8F", l => l.SectionOsdMetadata, l => l.SectionDescOsdMetadata),
        new("SectionScreenshotLocation", "\uE8B5", l => l.SectionScreenshotLocation, l => l.SectionDescScreenshotLocation),
        new("SectionScreenshotQuality", "\uE740", l => l.SectionScreenshotQuality, l => l.SectionDescScreenshotQuality),
        new("SectionWindow", "\uE745", l => l.SectionWindow, l => l.SectionDescWindow),
        new("SectionWindowBehavior", "\uE7A6", l => l.SectionWindowBehavior, l => l.SectionDescWindowBehavior),
        new("SectionWindowPiP", "\uEE49", l => l.SectionWindowPiP, l => l.SectionDescWindowPiP),
        // SectionAdvancedRenderer and SectionAdvancedScaler were removed with the
        // category they belonged to. gpu-api and gpu-context are now in the D3D11
        // section, and the two antiringing knobs in the scaler section - a section
        // that exists to describe them, which the old pair only half did.

        // ---- schema-generated sections ----
        // One per mpv option group, holding the 548 options that have no
        // hand-written row. They are listed here rather than in the generator
        // because the icon and the description are choices, not data: the
        // schema says which group an option belongs to but nothing about how
        // it should read in this window.
        //
        // The glyphs are free ones, not the nine their categories already
        // use: the same table serves the icon for both the section card and
        // the category rail, so a generated section reusing its category's
        // glyph would show two identical icons a level apart. These are
        // picked from the codepoints the hand-written 46 do not occupy, so
        // `check-sections.py` stays green without one of them having to move.
        new("SectionGenColourHdr", "\uE701", l => l.SectionGenColourHdr, l => l.SectionDescGenColourHdr),
        new("SectionGenSubtitles", "\uE703", l => l.SectionGenSubtitles, l => l.SectionDescGenSubtitles),
        new("SectionGenOsd", "\uE704", l => l.SectionGenOsd, l => l.SectionDescGenOsd),
        new("SectionGenAudio", "\uE705", l => l.SectionGenAudio, l => l.SectionDescGenAudio),
        // "Streaming & cache" was one 50-row folder doing three unrelated jobs:
        // opening a URL, how much read-ahead to keep, and the demuxer's own
        // probing behaviour for raw or unusual containers. The first is what
        // someone arrives for; the third is a mechanism that only matters when a
        // file plays wrong. Split accordingly - see STREAM_SUBTHEMES.
        new("SectionGenStreamNetwork", "\uE710", l => l.SectionGenStreamNetwork, l => l.SectionDescGenStreamNetwork),
        new("SectionGenStreamCache", "\uE9A9", l => l.SectionGenStreamCache, l => l.SectionDescGenStreamCache),
        new("SectionGenStreamDemuxer", "\uEA80", l => l.SectionGenStreamDemuxer, l => l.SectionDescGenStreamDemuxer),
        new("SectionGenTracks", "\uE708", l => l.SectionGenTracks, l => l.SectionDescGenTracks),

        // "Video & rendering" and "Player & window" are no longer one folder
        // each. mpv groups by subsystem, so those two held 254 and 128 options
        // between them: opening Video gave a 254-row wall under a single
        // heading, and Player & window mixed the window frame with input
        // bindings, script loading, disc devices and screenshots.
        //
        // They are now split by what an option changes -- the axis someone
        // arrives with -- in VIDEO_SUBTHEMES / WINDOW_SUBTHEMES in
        // tools/gen-schema-options.py. Each sub-folder carries its own glyph
        // because a section card is recognised by its icon; four video folders
        // in the same film strip would defeat the split.
        new("SectionGenVideoOutput", "\uE7B3", l => l.SectionGenVideoOutput, l => l.SectionDescGenVideoOutput),
        new("SectionGenVideoDecode", "\uE7C1", l => l.SectionGenVideoDecode, l => l.SectionDescGenVideoDecode),
        new("SectionGenVideoScale", "\uE8B2", l => l.SectionGenVideoScale, l => l.SectionDescGenVideoScale),
        new("SectionGenVideoProcess", "\uE8A5", l => l.SectionGenVideoProcess, l => l.SectionDescGenVideoProcess),
        new("SectionGenWindowFrame", "\uE8B9", l => l.SectionGenWindowFrame, l => l.SectionDescGenWindowFrame),
        new("SectionGenWindowInput", "\uE90F", l => l.SectionGenWindowInput, l => l.SectionDescGenWindowInput),
        new("SectionGenWindowScripts", "\uEA37", l => l.SectionGenWindowScripts, l => l.SectionDescGenWindowScripts),
        new("SectionGenWindowMedia", "\uE81C", l => l.SectionGenWindowMedia, l => l.SectionDescGenWindowMedia),
        new("SectionGenPlayerWindow", "\uE709", l => l.SectionGenPlayerWindow, l => l.SectionDescGenPlayerWindow),

        // mpv's `General` group split into themes. It was one folder called
        // 常规选项 holding 100 rows that had nothing in common -- encoder
        // output beside terminal verbosity beside X11 names beside Lua paths
        // -- and it was the only card on the Program page, so 桌面 opened
        // onto six real settings and then a bin. That is the old Advanced
        // bucket again, which the hand-written tree spent several rounds
        // removing; the generator had quietly put it back.
        //
        // The split is drawn along lines mpv's own vocabulary already
        // contains, and every one of the 100 options lands in exactly one
        // theme (the generator reports any that do not).
        // Two of the themes that split came to are gone: every option in them
        // (terminal verbosity, and the Wayland / X11 / VA-API set) was one this
        // application cannot reach, so the generator now denies them and the
        // sections have nothing left to hold. A caption for an empty section is
        // what this table must not keep, hence their absence rather than a
        // placeholder.
        //
        // 章节与时间线 and 窗口与界面 were added on 2026-10-11, when the two
        // theme lists they replace turned out to hold options that contradicted
        // their own captions: 编码输出 carried the Matroska chapter options and
        // the seek index, and 渲染与色彩微调 carried the backdrop material, the
        // taskbar progress and the screen-edge snapping. Both lists had been
        // written by guessing from the option name, so the fix was to file each
        // option by what it does -- and, for the seven whose subject already has
        // a section (audio, subtitles, streaming), to file them there instead of
        // in a General theme at all (SECTION_OVERRIDE in the generator).
        new("SectionGenGeneralEncoding", "\uE718", l => l.SectionGenGeneralEncoding, l => l.SectionDescGenGeneralEncoding),
        new("SectionGenGeneralChapters", "\uE736", l => l.SectionGenGeneralChapters, l => l.SectionDescGenGeneralChapters),
        new("SectionGenGeneralDesktop", "\uE737", l => l.SectionGenGeneralDesktop, l => l.SectionDescGenGeneralDesktop),
        new("SectionGenGeneralScripting", "\uE716", l => l.SectionGenGeneralScripting, l => l.SectionDescGenGeneralScripting),
        new("SectionGenGeneralRendering", "\uE719", l => l.SectionGenGeneralRendering, l => l.SectionDescGenGeneralRendering),
    ];

    /// <summary>Fallback glyph for a caption the table does not know.</summary>
    internal const string FallbackIcon = "\uE8B7";

    private static Dictionary<string, Section>? _byCaption;
    private static Dictionary<string, string>? _categoryByCaption;

    /// <summary>Drops the caches so a language switch rebuilds them.</summary>
    internal static void Invalidate()
    {
        _byCaption = null;
        _categoryByCaption = null;
    }

    private static Dictionary<string, Section> ByCaption()
    {
        if (_byCaption is not null)
        {
            return _byCaption;
        }

        var lang = AppContext.AppLang;
        var map = new Dictionary<string, Section>(StringComparer.Ordinal);
        foreach (var section in All)
        {
            var caption = section.Caption(lang);
            if (!string.IsNullOrEmpty(caption))
            {
                map[caption] = section;
            }
        }

        _byCaption = map;
        return map;
    }

    private static Dictionary<string, string> CategoryByCaption()
    {
        if (_categoryByCaption is not null)
        {
            return _categoryByCaption;
        }

        var lang = AppContext.AppLang;
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var category in Categories)
        {
            var caption = category.Caption(lang);
            if (!string.IsNullOrEmpty(caption))
            {
                map[caption] = category.Key;
            }
        }

        _categoryByCaption = map;
        return map;
    }

    /// <summary>Section for a localized caption, or null.</summary>
    internal static Section? ForCaption(string? caption) =>
        string.IsNullOrEmpty(caption)
            ? null
            : ByCaption().TryGetValue(caption, out var section) ? section : null;

    /// <summary>Section for its stable id, or null.</summary>
    internal static Section? ForId(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        foreach (var section in All)
        {
            if (string.Equals(section.Id, id, StringComparison.Ordinal))
            {
                return section;
            }
        }

        return null;
    }

    /// <summary>Stable id for a section caption, or null when it is unknown.</summary>
    internal static string? IdFor(string? caption) => ForCaption(caption)?.Id;

    /// <summary>Current-language caption for a stable section id, or null.</summary>
    internal static string? CaptionFor(string? id) => ForId(id)?.Caption(AppContext.AppLang);

    /// <summary>Icon glyph and description line for a section caption.</summary>
    internal static (string Icon, string Description) MetaFor(string? caption)
    {
        var section = ForCaption(caption);
        return section is null
            ? (FallbackIcon, string.Empty)
            : (section.Icon, section.Description(AppContext.AppLang));
    }

    /// <summary>
    /// Just the glyph for a section caption, or the table's fallback.
    ///
    /// Separate from <see cref="MetaFor"/> because the row's leading icon asks
    /// for this once per realised row - several hundred times while a category
    /// is being browsed - and MetaFor would resolve the localized description
    /// each time only for the caller to drop it.
    /// </summary>
    internal static string IconFor(string? caption) => ForCaption(caption)?.Icon ?? FallbackIcon;

    /// <summary>Stable key of the category a caption belongs to, or null.</summary>
    internal static string? CategoryKeyFor(string? caption) =>
        string.IsNullOrEmpty(caption)
            ? null
            : CategoryByCaption().TryGetValue(caption, out var key) ? key : null;

    /// <summary>Current-language caption for a stable category key, or null.</summary>
    internal static string? CategoryCaptionFor(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        foreach (var category in Categories)
        {
            if (string.Equals(category.Key, key, StringComparison.Ordinal))
            {
                return category.Caption(AppContext.AppLang);
            }
        }

        return null;
    }
}
