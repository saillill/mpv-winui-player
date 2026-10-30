using System;
using System.Collections.Generic;

namespace mpv_winui.Domains.Settings;

/// <summary>
/// Which folded sections open in place, and which navigate to a page.
///
/// The rule, in the words it was given: a section is a subdivision only when it
/// is clearly one part of what its category is about; parallel features are not
/// sections. So:
///
///   * **In place** -- the several sides of one thing. Subtitles have a style,
///     a behaviour, a position and an ASS handler; those are four ways to talk
///     about subtitles, and a reader adjusting one usually wants the others
///     within reach. The video pipeline is the same shape: output, decode,
///     scale, process, colour, HDR, sync are stages of one path.
///
///   * **Navigate** -- separate jobs that happen to share a category. Screenshots
///     are not part of video playback; the window frame, the media-key handling,
///     the script pipeline and the config profile are five unrelated subjects
///     that the "Interface" pane happens to hold. Expanding one of those in
///     place would drop ten to twenty rows of an unrelated subject between you
///     and the ones you were reading.
///
/// This is a judgement, which is why it is a table with one line per section and
/// not a rule over row counts: `SectionGenStreamDemuxer` has 27 rows and
/// navigates, `SectionGenVideoScale` has 5 and expands, and no arithmetic
/// separates them.
///
/// A section that is not listed here keeps the old behaviour -- a card that
/// navigates. That is the safe default: it is what every section did before.
/// </summary>
internal static class SectionShapes
{
    /// <summary>Sections that expand where they sit.</summary>
    internal static readonly IReadOnlySet<string> Inline =
        new HashSet<string>(StringComparer.Ordinal)
        {
            // --- Audio: one output chain, described end to end ---------------
            "SectionAudioOutput",
            "SectionGenAudio",
            "SectionGenAudioOut",
            "SectionGenAudioDecode",
            "SectionGenResample",

            // --- OSD: one overlay, from text to metadata ---------------------
            "SectionGenOsd",
            "SectionOsdText",
            "SectionOsdAppearance",
            "SectionOsdMetadata",

            // --- Subtitles: the thing a category can most obviously own ------
            "SectionGenSubtitles",
            "SectionGenSubtitlesSource",
            "SectionSubtitleStyle",
            "SectionSubtitleBehavior",
            "SectionSubtitlePosition",
            "SectionSubtitleAss",

            // --- Video: one pipeline, stage by stage -------------------------
            "SectionGenVideoOutput",
            "SectionGenVideoDecode",
            "SectionGenVideoScale",
            "SectionGenVideoProcess",
            "SectionGpuScaling",
            "SectionGpuD3d11",
            "SectionGpuShaders",
            "SectionToneMapping",
            "SectionTargetColorspace",
            "SectionColorManagement",
            "SectionGenColourHdr",
            "SectionGenFrameSync",
            "SectionVideoSync",
            "SectionGenGeometry",
            "SectionVideoImage",
        };

    internal static bool ExpandsInline(string? sectionId) =>
        sectionId is not null && Inline.Contains(sectionId);
}
