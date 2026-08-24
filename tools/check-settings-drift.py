#!/usr/bin/env python3
"""Check settings drift for mpv-winui-player.

Three sources must stay in sync:
  * AppSettings properties (the setting store),
  * MpvSettings.ToCommand cases (the live mpv mapping),
  * SettingsPage.Options*.cs keys (the settings window tree).

This script reports:
  * AppSettings properties with no MpvSettings mapping (the setting would be
    stored but never applied to mpv),
  * MpvSettings mappings that reference a non-existent AppSettings property
    (dead mapping),
  * AppSettings properties not shown in the settings window (either covered
    by scripts/config, or genuinely missing from the UI).

Exit code 1 on problems.
"""

import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
SRC = REPO / "mpv-winui" / "mpv-winui" / "Modules" / "Settings"

for stream in (sys.stdout, sys.stderr):
    try:
        stream.reconfigure(encoding="utf-8", errors="replace")
    except (AttributeError, ValueError):
        pass


def parse_appsettings_props(src: Path) -> set[str]:
    text = src.read_text(encoding="utf-8")
    return set(re.findall(r"public (?:string|bool|int|double|float|long|string\[\]|IReadOnlyList<[^>]+>|List<[^>]+>|TimeSpan|uint)\s+(\w+)\s*\{", text))


def parse_mpvsettings_cases(src: Path) -> set[str]:
    text = src.read_text(encoding="utf-8")
    # key switch expression cases: nameof(AppSettings.X) => ...
    return set(re.findall(r'nameof\(AppSettings\.(\w+)\)\s*=>', text))


def parse_option_keys(src_dir: Path) -> set[str]:
    keys: set[str] = set()
    for f in sorted(src_dir.glob("SettingsPage.Options*.cs")):
        text = f.read_text(encoding="utf-8")
        keys |= set(re.findall(r"Key = nameof\(AppContext\.AppSetting\.(\w+)\)", text))
        # non-AppSettings option keys (e.g. "Preset", shortcut entries)
        keys |= set(re.findall(r'Key = "([A-Za-z0-9_]+)"', text))
    return keys


# AppSettings properties that intentionally have no MpvSettings mapping:
#   * UI-only settings (theme, control bar, window, language, ...),
#   * plugin script options written by PluginConfigWriter,
#   * per-feature legacy migration markers.
# Adding a new mpv-facing setting here is a bug; the check keeps this list tight.
UNMAPPED_OK = {
    # UI-only
    "ThemeType", "ThemeAccentColor", "ThemeOpacity", "ThemeLuminosity", "BackdropType",
    "UiFont", "ThemeRecentColors", "ControlBarLayout", "ControlBarHiddenIcons",
    "ControlBarHiddenIconsClassic", "ControlBarHiddenIconsModernX",
    "CurrentLanguage", "WindowRememberSize", "WindowPositionAndSize", "WindowStartMaximized",
    "WindowPiP", "WindowPiPSize", "EnableDebugLog", "TestMpvCommandLog", "TestOsdMessage",
    "TestSignal", "FileAssociationExts", "SettingsSearchHistory", "CheckForUpdates",
    "DisplayPeak",
    "ControlBarCustomOrder", "ControlBarCustomOrderClassic", "ControlBarCustomOrderModernX",
    "ControlBarZonesClassic", "ControlBarZonesModernX",
    "ControlBarOrderStyleMigrated", "WindowPiPRect", "WindowPiPOpacity",
    "LastVideoVolume", "LastAudioVolume", "AudioVolume", "PatchVersion",
    "EnableVideoPreview",
    "ThumbnailPreviewWidth", "ThumbnailUpdateInterval",
    # UI-only settings persisted by their own handlers (playlist width from
    # the resize grip, window title from MainWindow).
    "PlaylistWidth", "WindowTitle",
    # PluginConfigWriter channel (script-opts/*.conf)
    "HdrAutoLog", "MetadataOsdEnabled", "MetadataOsdAutohideTimeout",
    "MetadataOsdShowChapter", "MetadataOsdEnableForVideo", "MetadataOsdEnableForImage",
    "MetadataOsdEnableForAudio", "MetadataOsdEnableForAudioWithAlbumArt",
    "MetadataOsdAutohideForAudio", "MetadataOsdAutohideForAudioWithAlbumArt",
    "MetadataOsdAutohideStatusTimeout", "MetadataOsdShowAlbumTrack",
    "MetadataOsdMessageMaxLength", "CoverArtPreferEmbedded", "CoverArtAlwaysScan",
    "CoverArtLoadFromFilesystem", "CoverArtPreload", "CoverArtNames", "CoverArtImageExts",
    "HdrOverrideMode",
    "CoverArtExts", "MetadataOsdShowAlbum",
    # ManagedMpvConfig channel (ytdl_hook script options in mpv.conf)
    "YtdlPath", "YtdlAllFormats", "YtdlThumbnails", "YtdlExclude", "YtdlTryFirst", "YtdlUseManifests",
    # Special cases handled outside ToCommand
    "Volume",               # passed at mpv Initialize, not applied at runtime
    "ShowOsdPlayingMsg",    # merged into the OsdPlayingMsg case
    "Speed",                # applied once at startup by MpvPlayerPage; deliberately
                            # absent from ApplyAll so a reset never clobbers live speed
    "LoopFile",             # controlled by the status-bar repeat button
    "LoopPlaylist",         # controlled by the bundled mpv.conf
}


def main() -> int:
    appsettings = parse_appsettings_props(SRC / "AppSettings.cs")
    mpvsettings = parse_mpvsettings_cases(SRC / "MpvSettings.cs")
    option_keys = parse_option_keys(SRC)

    if not appsettings:
        print("ERROR: could not parse AppSettings properties", file=sys.stderr)
        return 2

    errors: list[str] = []
    warnings: list[str] = []

    unmapped = appsettings - mpvsettings
    for key in sorted(unmapped):
        if key in UNMAPPED_OK:
            continue
        errors.append(f"AppSettings.{key} has no MpvSettings.ToCommand mapping")

    dead = mpvsettings - appsettings
    for key in sorted(dead):
        errors.append(f"MpvSettings maps unknown property '{key}' (not in AppSettings)")

    # Properties not surfaced in the settings tree. Many are intentional
    # (computed, per-style, config-only) so this is a warning, not an error.
    not_shown = appsettings - option_keys
    for key in sorted(not_shown):
        warnings.append(f"AppSettings.{key} not shown in the settings window")

    for warning in warnings:
        print(f"WARNING: {warning}")
    for error in errors:
        print(f"ERROR: {error}", file=sys.stderr)
    if errors:
        return 1
    print(f"OK: {len(appsettings)} AppSettings props, {len(mpvsettings)} mpv mappings, {len(option_keys)} option keys")
    return 0


if __name__ == "__main__":
    sys.exit(main())
