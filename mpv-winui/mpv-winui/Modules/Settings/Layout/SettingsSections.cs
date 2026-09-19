using mpv_winui.Modules.Language;
using System;
using System.Collections.Generic;

namespace mpv_winui.Modules.Settings.Layout;

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
    internal static readonly Category[] Categories =
    [
        new("program", l => l.SettingsCategoryProgram),
        new("playback", l => l.SettingsCategoryPlayback),
        new("video", l => l.SettingsCategoryVideo),
        new("audio", l => l.SettingsCategoryAudio),
        new("subtitles", l => l.SettingsCategorySubtitles),
        new("window", l => l.SettingsCategoryWindow),
        new("network", l => l.SettingsCategoryNetwork),
        new("shortcuts", l => l.SettingsCategoryShortcuts),
        new("osd", l => l.SettingsCategoryOsd),
        new("screenshot", l => l.SettingsCategoryScreenshot),
        // Last on purpose. Everything here is exposed because the option
        // exists and works, not because a normal user would want it: renderer
        // internals, scaler tuning parameters. Keeping it one category rather
        // than scattering the same options through Video is what lets the rest
        // of the tree stay a description of what the player does rather than a
        // copy of the mpv manual.
        new("advanced", l => l.SettingsCategoryAdvanced),
    ];

    /// <summary>
    /// Sections, in the order the catalog used to list them. That order is not
    /// load-bearing -- the window's order comes from the section map in
    /// <c>SettingsPage.Options.cs</c> -- but it is kept so this table reads as
    /// a faithful replacement rather than a reshuffle.
    ///
    /// The icon is a Segoe Fluent Icons glyph; see
    /// <c>docs/settings-ui-review.md</c> for how they are chosen (rendered and
    /// inspected, not guessed from the code point).
    /// </summary>
    internal static readonly Section[] All =
    [
        new("SectionProgramInterface", "\uE771", l => l.SectionProgramInterface, l => l.SectionDescProgramInterface),
        new("SectionProgramLanguageLog", "\uE8C1", l => l.SectionProgramLanguageLog, l => l.SectionDescProgramLanguageLog),
        new("SectionProgramConfig", "\uECC5", l => l.SectionProgramConfig, l => l.SectionDescProgramConfig),
        new("SectionProgramAssociations", "\uE8E5", l => l.SectionProgramAssociations, l => l.SectionDescProgramAssociations),
        new("SectionProgramTesting", "\uE9D9", l => l.SectionProgramTesting, l => l.SectionDescProgramTesting),
        new("SectionPlayback", "\uE102", l => l.SectionPlayback, l => l.SectionDescPlayback),
        new("SectionPlaybackSeeking", "\uE786", l => l.SectionPlaybackSeeking, l => l.SectionDescPlaybackSeeking),
        new("SectionPlaybackSeekPreview", "\uE720", l => l.SectionPlaybackSeekPreview, l => l.SectionDescPlaybackSeekPreview),
        new("SectionReversePlayback", "\uE8AB", l => l.SectionReversePlayback, l => l.SectionDescReversePlayback),
        new("SectionWatchLaterStorage", "\uE8B7", l => l.SectionWatchLaterStorage, l => l.SectionDescWatchLaterStorage),
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
        new("SectionSubtitleSecondary", "\uE8B1", l => l.SectionSubtitleSecondary, l => l.SectionDescSubtitleSecondary),
        new("SectionSubtitleStyle", "\uE8D2", l => l.SectionSubtitleStyle, l => l.SectionDescSubtitleStyle),
        new("SectionSubtitlePosition", "\uE7C4", l => l.SectionSubtitlePosition, l => l.SectionDescSubtitlePosition),
        new("SectionSubtitleAss", "\uE713", l => l.SectionSubtitleAss, l => l.SectionDescSubtitleAss),
        new("SectionSubtitleImage", "\uE91B", l => l.SectionSubtitleImage, l => l.SectionDescSubtitleImage),
        new("SectionOsdBehavior", "\uE7EE", l => l.SectionOsdBehavior, l => l.SectionDescOsdBehavior),
        new("SectionOsdAppearance", "\uE2B1", l => l.SectionOsdAppearance, l => l.SectionDescOsdAppearance),
        new("SectionOsdPosition", "\uE787", l => l.SectionOsdPosition, l => l.SectionDescOsdPosition),
        new("SectionOsdMetadata", "\uEA8F", l => l.SectionOsdMetadata, l => l.SectionDescOsdMetadata),
        new("SectionScreenshotLocation", "\uE8B5", l => l.SectionScreenshotLocation, l => l.SectionDescScreenshotLocation),
        new("SectionScreenshotQuality", "\uE740", l => l.SectionScreenshotQuality, l => l.SectionDescScreenshotQuality),
        new("SectionWindow", "\uE745", l => l.SectionWindow, l => l.SectionDescWindow),
        new("SectionWindowBehavior", "\uE7A6", l => l.SectionWindowBehavior, l => l.SectionDescWindowBehavior),
        new("SectionWindowPiP", "\uEE49", l => l.SectionWindowPiP, l => l.SectionDescWindowPiP),
        new("SectionAdvancedRenderer", "\uE7B3", l => l.SectionAdvancedRenderer, l => l.SectionDescAdvancedRenderer),
        new("SectionAdvancedScaler", "\uE8F4", l => l.SectionAdvancedScaler, l => l.SectionDescAdvancedScaler),
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
