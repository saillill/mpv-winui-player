# -*- coding: utf-8 -*-
"""Regenerate the settings category/section maps in SettingsPage.xaml.cs.

The settings window follows the option layout of the mpv manual
(DOCS/man/options.rst). This script is the single source of truth for:

  * the official categories (categoryOrder / categoryMap),
  * the per-category section captions (sectionOrder / sectionMap),
  * the exact option order inside each section (optionOrder).

Usage:
    python tools/generate-settings-category-map.py all

Print the generated fragments to stdout and replace the corresponding
region in SettingsPage.xaml.cs (from `var categoryOrder = new[]` up to the
closing `};` of `sectionMap`). The script validates that every setting key
is mapped exactly once.
"""
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
SRC = REPO / "mpv-winui" / "mpv-winui" / "Modules" / "Settings" / "SettingsPage.xaml.cs"

src = SRC.read_text(encoding="utf-8")
keys = re.findall(r"Key = nameof\(AppContext\.AppSetting\.(\w+)\)", src)
assert len(keys) == 193, len(keys)

# key -> (category var, section var)
# Category vars: program playback watchLater video audio subtitles window demuxer
#                cache network input shortcuts osd screenshot testing
# Section vars:  sProgramInterface sProgramLanguageLog sProgramNetwork sProgramTesting
#                sPlayback sPlaybackSeeking sPlaybackSeekPreview
#                sTrackLanguage sTrackFallback
#                sWatchLaterResume sWatchLaterStorage
#                sVideoDecode sVideoImage sVideoHdr sVideoFilters sVideoUpscaling
#                sAudioOutput sAudioVolume sAudioExternal sAudioCoverArt
#                sSubtitleText sSubtitleAss sSubtitleImage
#                sWindow sDemuxerPlaylist sDemuxerBuffering sCache sInput
#                sOsd sOsdMetadata
#                sScreenshotLocation sScreenshotQuality
#                sGpuScaling sGpuColor sGpuInterpolation sGpuBackground
#                sGpuD3d11 sGpuShaders sVideoSync
MAP = {
    # Program Behavior
    "ThemeType": ("program", "sProgramInterface"),
    "BackdropType": ("program", "sProgramInterface"),
    "ThemeAccentColor": ("program", "sProgramInterface"),
    "ThemeOpacity": ("program", "sProgramInterface"),
    "ThemeLuminosity": ("program", "sProgramInterface"),
    "UiFont": ("program", "sProgramInterface"),
    "TestMpvCommandLog": ("testing", "sProgramTesting"),
    "TestOsdMessage": ("testing", "sProgramTesting"),
    "TestSignal": ("testing", "sProgramTesting"),
    "CurrentLanguage": ("program", "sProgramLanguageLog"),
    "EnableDebugLog": ("program", "sProgramLanguageLog"),
    "Ytdl": ("network", "sProgramNetwork"),
    "YtdlRawOptionsAppend": ("network", "sProgramNetwork"),
    # Playback Control
    "LoopFile": ("playback", "sPlayback"),
    "LoopPlaylist": ("playback", "sPlayback"),
    "Speed": ("playback", "sPlayback"),
    "HrSeek": ("playback", "sPlaybackSeeking"),
    "HrSeekFramedrop": ("playback", "sPlaybackSeeking"),
    "SeekHoldEnabled": ("playback", "sPlaybackSeeking"),
    "EnableVideoPreview": ("playback", "sPlaybackSeekPreview"),
    "ThumbfastQuality": ("playback", "sPlaybackSeekPreview"),
    "ThumbfastNetwork": ("playback", "sPlaybackSeekPreview"),
    "ThumbfastMinDuration": ("playback", "sPlaybackSeekPreview"),
    "ThumbfastPrecise": ("playback", "sPlaybackSeekPreview"),
    "ThumbfastMaxWidth": ("playback", "sPlaybackSeekPreview"),
    "ThumbfastMaxHeight": ("playback", "sPlaybackSeekPreview"),
    "ThumbfastSpawnFirst": ("playback", "sPlaybackSeekPreview"),
    "ThumbfastThreads": ("playback", "sPlaybackSeekPreview"),
    "ThumbfastFrequency": ("playback", "sPlaybackSeekPreview"),
    "ThumbfastDirectIo": ("playback", "sPlaybackSeekPreview"),
    "ThumbfastQuitAfterInactivity": ("playback", "sPlaybackSeekPreview"),
    # Track Selection
    "AudioLanguage": ("subtitles", "sTrackLanguage"),
    "SubtitleLanguage": ("subtitles", "sTrackLanguage"),
    "SubFallback": ("subtitles", "sTrackFallback"),
    # Watch Later
    "SavePositionOnQuit": ("watchLater", "sWatchLaterResume"),
    "ResumePlayback": ("watchLater", "sWatchLaterResume"),
    "WatchLaterOptions": ("watchLater", "sWatchLaterStorage"),
    "WatchLaterDir": ("watchLater", "sWatchLaterStorage"),
    # Video
    "Hwdec": ("video", "sVideoDecode"),
    "HwdecCodecs": ("video", "sVideoDecode"),
    "VideoDecodeDirect": ("video", "sVideoDecode"),
    "Deinterlace": ("video", "sVideoImage"),
    "VideoRotate": ("video", "sVideoImage"),
    "AspectRatio": ("video", "sVideoImage"),
    "Panscan": ("video", "sVideoImage"),
    "VideoUnscaled": ("video", "sVideoImage"),
    "VideoOutputLevels": ("video", "sVideoImage"),
    "HdrAutoMode": ("video", "sVideoFilters"),
    "HdrAutoLog": ("video", "sVideoFilters"),
    "VsrAutoEnabled": ("video", "sVideoFilters"),
    # Audio
    "AudioDevice": ("audio", "sAudioOutput"),
    "AudioExclusive": ("audio", "sAudioOutput"),
    "AudioChannels": ("audio", "sAudioOutput"),
    "AudioDelay": ("audio", "sAudioOutput"),
    "AudioBuffer": ("audio", "sAudioOutput"),
    "AudioWaitOpen": ("audio", "sAudioOutput"),
    "AudioPitchCorrection": ("audio", "sAudioOutput"),
    "AudioNormalizeDownmix": ("audio", "sAudioOutput"),
    "AudioGapless": ("audio", "sAudioOutput"),
    "Volume": ("audio", "sAudioVolume"),
    "VolumeMax": ("audio", "sAudioVolume"),
    "AudioFileAuto": ("audio", "sAudioExternal"),
    "AudioExts": ("audio", "sAudioExternal"),
    "AudioFilePaths": ("audio", "sAudioExternal"),
    "AudioDisplay": ("audio", "sAudioCoverArt"),
    "CoverArtPreferEmbedded": ("audio", "sAudioCoverArt"),
    "CoverArtAlwaysScan": ("audio", "sAudioCoverArt"),
    "CoverArtLoadFromFilesystem": ("audio", "sAudioCoverArt"),
    "CoverArtPreload": ("audio", "sAudioCoverArt"),
    "CoverArtNames": ("audio", "sAudioCoverArt"),
    "CoverArtImageExts": ("audio", "sAudioCoverArt"),
    # Subtitles
    "SubFontSize": ("subtitles", "sSubtitleText"),
    "SubFont": ("subtitles", "sSubtitleText"),
    "SubFontFile": ("subtitles", "sSubtitleText"),
    "SubFontProvider": ("subtitles", "sSubtitleText"),
    "SubCodePage": ("subtitles", "sSubtitleText"),
    "SubColor": ("subtitles", "sSubtitleText"),
    "SubBackColor": ("subtitles", "sSubtitleText"),
    "SubBorderColor": ("subtitles", "sSubtitleText"),
    "SubOutlineSize": ("subtitles", "sSubtitleText"),
    "SubShadowOffset": ("subtitles", "sSubtitleText"),
    "SubBlur": ("subtitles", "sSubtitleText"),
    "SubPos": ("subtitles", "sSubtitleText"),
    "SubDelay": ("subtitles", "sSubtitleText"),
    "SubScaleSigns": ("subtitles", "sSubtitleText"),
    "SubUseMargins": ("subtitles", "sSubtitleText"),
    "SubAuto": ("subtitles", "sSubtitleText"),
    "SubFilePaths": ("subtitles", "sSubtitleText"),
    "SubHdrPeak": ("subtitles", "sSubtitleText"),
    "SubAssOverride": ("subtitles", "sSubtitleAss"),
    "SubAssStyleOverrides": ("subtitles", "sSubtitleAss"),
    "SubAssForceMargins": ("subtitles", "sSubtitleAss"),
    "SubAssScaleWithWindow": ("subtitles", "sSubtitleAss"),
    "SubAssUseVideoData": ("subtitles", "sSubtitleAss"),
    "SubAssVideoAspectOverride": ("subtitles", "sSubtitleAss"),
    "SubAssVsfilterColorCompat": ("subtitles", "sSubtitleAss"),
    "SubEmbeddedFonts": ("subtitles", "sSubtitleAss"),
    "BlendSubtitles": ("subtitles", "sSubtitleAss"),
    "StretchImageSubsToScreen": ("subtitles", "sSubtitleImage"),
    "ImageSubsVideoResolution": ("subtitles", "sSubtitleImage"),
    "ImageSubsHdrPeak": ("subtitles", "sSubtitleImage"),
    # Window
    "AlwaysOnTop": ("window", "sWindow"),
    "KeepOpen": ("window", "sWindow"),
    # Demuxer
    "AutoCreatePlaylist": ("demuxer", "sDemuxerPlaylist"),
    "DirectoryMode": ("demuxer", "sDemuxerPlaylist"),
    "DirectoryFilterTypes": ("demuxer", "sDemuxerPlaylist"),
    "VideoExts": ("demuxer", "sDemuxerPlaylist"),
    "ImageExts": ("demuxer", "sDemuxerPlaylist"),
    "DemuxerMaxBytes": ("demuxer", "sDemuxerBuffering"),
    "DemuxerMaxBackBytes": ("demuxer", "sDemuxerBuffering"),
    "DemuxerReadahead": ("demuxer", "sDemuxerBuffering"),
    # Cache
    "CacheEnabled": ("cache", "sCache"),
    "CacheSecs": ("cache", "sCache"),
    "CacheOnDisk": ("cache", "sCache"),
    "CacheDirectory": ("cache", "sCache"),
    # Input
    "InputIme": ("input", "sInput"),
    "InputIpcServer": ("input", "sInput"),
    # OSD
    "OsdFontSize": ("osd", "sOsd"),
    "OsdFont": ("osd", "sOsd"),
    "OsdColor": ("osd", "sOsd"),
    "OsdOutlineColor": ("osd", "sOsd"),
    "OsdOnSeek": ("osd", "sOsd"),
    "OsdDuration": ("osd", "sOsd"),
    "OsdPlayingMsg": ("osd", "sOsd"),
    "OsdPlayingMsgDuration": ("osd", "sOsd"),
    "OsdBarWidth": ("osd", "sOsd"),
    "OsdBarHeight": ("osd", "sOsd"),
    "OsdBlur": ("osd", "sOsd"),
    "OsdOutlineSize": ("osd", "sOsd"),
    "OsdFractions": ("osd", "sOsd"),
    "ShowOsdPlayingMsg": ("osd", "sOsd"),
    "MetadataOsdEnabled": ("osd", "sOsdMetadata"),
    "MetadataOsdAutohideTimeout": ("osd", "sOsdMetadata"),
    "MetadataOsdShowChapter": ("osd", "sOsdMetadata"),
    "MetadataOsdEnableForVideo": ("osd", "sOsdMetadata"),
    "MetadataOsdEnableForImage": ("osd", "sOsdMetadata"),
    "MetadataOsdAutohideStatusTimeout": ("osd", "sOsdMetadata"),
    "MetadataOsdShowAlbumTrack": ("osd", "sOsdMetadata"),
    "MetadataOsdMessageMaxLength": ("osd", "sOsdMetadata"),
    # Screenshot
    "ScreenshotDirectory": ("screenshot", "sScreenshotLocation"),
    "ScreenshotTemplate": ("screenshot", "sScreenshotLocation"),
    "ScreenshotFormat": ("screenshot", "sScreenshotQuality"),
    "ScreenshotJpegQuality": ("screenshot", "sScreenshotQuality"),
    "ScreenshotJpegSourceChroma": ("screenshot", "sScreenshotQuality"),
    "ScreenshotPngCompression": ("screenshot", "sScreenshotQuality"),
    "ScreenshotPngFilter": ("screenshot", "sScreenshotQuality"),
    "ScreenshotWebpQuality": ("screenshot", "sScreenshotQuality"),
    "ScreenshotWebpLossless": ("screenshot", "sScreenshotQuality"),
    "ScreenshotWebpCompression": ("screenshot", "sScreenshotQuality"),
    "ScreenshotJxlDistance": ("screenshot", "sScreenshotQuality"),
    "ScreenshotJxlEffort": ("screenshot", "sScreenshotQuality"),
    "ScreenshotAvifEncoder": ("screenshot", "sScreenshotQuality"),
    "ScreenshotHighBitDepth": ("screenshot", "sScreenshotQuality"),
    "ScreenshotTagColorspace": ("screenshot", "sScreenshotQuality"),
    "ScreenshotSw": ("screenshot", "sScreenshotQuality"),
    # GPU renderer options
    "Scale": ("video", "sGpuScaling"),
    "DScale": ("video", "sGpuScaling"),
    "Cscale": ("video", "sGpuScaling"),
    "Tscale": ("video", "sGpuScaling"),
    "LinearUpscaling": ("video", "sGpuScaling"),
    "SigmoidUpscaling": ("video", "sGpuScaling"),
    "LinearDownscaling": ("video", "sGpuScaling"),
    "CorrectDownscaling": ("video", "sGpuScaling"),
    "Deband": ("video", "sGpuScaling"),
    "Dither": ("video", "sGpuScaling"),
    "DitherDepth": ("video", "sGpuScaling"),
    "ToneMapping": ("video", "sGpuColor"),
    "TargetColorspaceHint": ("video", "sGpuColor"),
    "TargetColorspaceHintMode": ("video", "sGpuColor"),
    "TargetColorspaceHintStrict": ("video", "sGpuColor"),
    "TargetPrim": ("video", "sGpuColor"),
    "TargetTrc": ("video", "sGpuColor"),
    "TargetPeak": ("video", "sGpuColor"),
    "GamutMappingMode": ("video", "sGpuColor"),
    "IccProfileAuto": ("video", "sGpuColor"),
    "IccProfile": ("video", "sGpuColor"),
    "IccForceContrast": ("video", "sGpuColor"),
    "Icc3dlutSize": ("video", "sGpuColor"),
    "IccCache": ("video", "sGpuColor"),
    "IccCacheDir": ("video", "sGpuColor"),
    "D3d11OutputCsp": ("video", "sGpuColor"),
    "Interpolation": ("video", "sGpuInterpolation"),
    "BackgroundTileColor0": ("video", "sGpuBackground"),
    "BackgroundTileColor1": ("video", "sGpuBackground"),
    "BackgroundTileSize": ("video", "sGpuBackground"),
    "D3d11ExclusiveFs": ("video", "sGpuD3d11"),
    "D3d11Flip": ("video", "sGpuD3d11"),
    "D3d11Adapter": ("video", "sGpuD3d11"),
    "GpuShaderCache": ("video", "sGpuShaders"),
    "GpuShaderCacheDir": ("video", "sGpuShaders"),
    "GlslShadersAppend": ("video", "sGpuShaders"),
    # Video Sync
    "VideoSync": ("video", "sVideoSync"),
    "VideoSyncMaxVideoChange": ("video", "sVideoSync"),
}

missing = [k for k in keys if k not in MAP]
extra = [k for k in MAP if k not in keys]
assert not missing, f"missing: {missing}"
assert not extra, f"extra: {extra}"
assert len(MAP) == len(keys), (len(MAP), len(keys))

CAT_ORDER = ["program", "playback", "watchLater", "video", "audio", "subtitles",
             "window", "demuxer", "cache", "network", "input", "shortcuts",
             "osd", "screenshot", "testing"]


def emit_map():
    out = []
    out.append("        var categoryOrder = new[]")
    out.append("        {")
    for c in CAT_ORDER:
        out.append(f"            {c},")
    out.append("        };")
    out.append("")
    out.append("        var categoryMap = new Dictionary<string, string>(StringComparer.Ordinal)")
    out.append("        {")
    by_cat = {}
    for k, (c, s) in MAP.items():
        by_cat.setdefault(c, []).append(k)
    for c in CAT_ORDER:
        out.append(f"            // {c}")
        for k in by_cat.get(c, []):
            out.append(f"            [nameof(AppSettings.{k})] = {c},")
    out.append("        };")
    out.append("")
    out.append("        var optionOrder = new Dictionary<string, int>(StringComparer.Ordinal)")
    out.append("        {")
    for i, k in enumerate(MAP):
        out.append(f"            [nameof(AppSettings.{k})] = {i},")
    out.append("        };")
    out.append("")
    out.append("        var sectionOrder = new Dictionary<string, int>(StringComparer.Ordinal)")
    out.append("        {")
    seen = {}
    order = 0
    for c in CAT_ORDER:
        for k in by_cat.get(c, []):
            s = MAP[k][1]
            if s not in seen:
                seen[s] = order
                order += 1
    for s, idx in seen.items():
        out.append(f"            [{s}] = {idx},")
    out.append("        };")
    out.append("")
    out.append("        var sectionMap = new Dictionary<string, string>(StringComparer.Ordinal)")
    out.append("        {")
    by_sec = {}
    for k, (c, s) in MAP.items():
        by_sec.setdefault((c, s), []).append(k)
    for c in CAT_ORDER:
        out.append(f"            // {c}")
        for (cc, s) in sorted(by_sec, key=lambda t: (CAT_ORDER.index(t[0]), list(by_sec).index(t))):
            if cc != c:
                continue
            for k in by_sec.get((c, s), []):
                out.append(f"            [nameof(AppSettings.{k})] = {s},")
    out.append("        };")
    return "\n".join(out)


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else "all"
    if mode in ("vars", "all"):
        print("vars part (copy into BuildSettings top):")
        print("  (see SettingsPage.xaml.cs for the current declarations)")
    if mode in ("map", "all"):
        print(emit_map())


if __name__ == "__main__":
    main()
