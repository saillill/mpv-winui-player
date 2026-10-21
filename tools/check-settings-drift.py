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


def parse_appsettings_props(src_dir: Path) -> set[str]:
    keys: set[str] = set()
    for f in sorted(src_dir.glob("AppSettings*.cs")):
        text = f.read_text(encoding="utf-8")
        keys |= set(re.findall(r"public (?:string|bool|int|double|float|long|string\[\]|IReadOnlyList<[^>]+>|List<[^>]+>|TimeSpan|uint)\s+(\w+)\s*\{", text))
    return keys


def parse_mpvsettings_cases(src: Path) -> set[str]:
    text = src.read_text(encoding="utf-8")
    # key switch expression cases: nameof(AppSettings.X) => ...
    mapped = set(re.findall(r'nameof\(AppSettings\.(\w+)\)\s*=>', text))

    # The schema-generated rows are mapped through one `_ => GeneratedCommand`
    # arm plus a name-map dictionary, not through a case each: 362 arms would
    # be 362 lines of switch that the generator cannot refresh. The mapping is
    # real all the same -- GeneratedCommand builds "set <name> <literal>" from
    # the dictionary entry -- so the dictionary keys are read here as if they
    # were cases. Missing this is how the check reported all 362 as unmapped.
    gen = src.parent / "MpvSettings.Gen.cs"
    if gen.exists():
        gen_text = gen.read_text(encoding="utf-8-sig")
        mapped |= set(re.findall(
            r'\[nameof\(AppSettings\.(\w+)\)\]', gen_text))
    return mapped


def parse_option_keys(src_dir: Path) -> set[str]:
    keys: set[str] = set()
    for f in sorted(src_dir.glob("SettingsPage.Options*.cs")):
        text = f.read_text(encoding="utf-8")
        keys |= set(re.findall(r"Key = nameof\(AppContext\.AppSetting\.(\w+)\)", text))
        # non-AppSettings option keys (e.g. "Preset", shortcut entries)
        keys |= set(re.findall(r'Key = "([A-Za-z0-9_]+)"', text))
    return keys


# AppSettings properties that intentionally have no MpvSettings mapping.
#
# Every entry here is a property that changes what THIS APP does and can never
# become a libmpv option. That distinction is the whole point of the list: the
# error below means "a setting is stored and never reaches mpv", so the only
# correct way to silence it for an app-owned setting is to say so here, with a
# reason. Adding a fake `ToCommand` arm instead would write app state into
# mpv.conf; deleting the check would lose the signal for the next real omission.
#
# Seven entries were missing when this list was audited (2026-09-21). They are
# not new settings -- they had simply never been classified, so the check had
# been failing on them for as long as it existed, which is what made the one
# real problem in this area (EnableVideoPreview, below) indistinguishable from
# the noise.
UNMAPPED_OK = {
    # UI-only
    #
    # The two library folder lists change which files the app lists. mpv has no
    # option for "the folders my libraries scan" - it is the app's own index, and
    # writing it into mpv.conf would mean nothing to mpv.
    "VideoLibraryFolders", "MusicLibraryFolders",
    "ThemeType", "BackdropType",
    "UiFont", "ThemeRecentColors", "ControlBarLayout", "ControlBarHiddenIcons",
    "ControlBarHiddenIconsClassic", "ControlBarHiddenIconsModernX",
    "CurrentLanguage", "WindowRememberSize", "WindowPositionAndSize", "WindowStartMaximized",
    "WindowPiP", "WindowPiPSize", "EnableDebugLog", "TestMpvCommandLog", "TestOsdMessage",
    "TestSignal", "FileAssociationExts", "SettingsSearchHistory", "CheckForUpdates",
    "DisplayPeak",
    "ControlBarCustomOrderClassic", "ControlBarCustomOrderModernX",
    "ControlBarZonesClassic", "ControlBarZonesModernX",
    "WindowPiPShowTopButtons", "WindowPiPShowControls",
    "WindowPiPOpacity", "WindowPiPAspectRatioLock", "WindowPiPAnchor",
    "WindowAspectRatioLock",
    "LastVideoVolume", 
    # Which control-bar skin to draw. Read by MpvPlayerPage_PlayControl.cs.
    "PlayerStyle",
    # Preview behaviour: app-side wiring of the in-process previewer and of the
    # built-in second-libmpv instance. Read by MpvPlayerPage_Preview.cs and
    # MpvPlayerPage_BuiltInPreview.cs; `Mpv_preview_*` is not a thing.
    "EnableVideoPreview",
    "EnableVideoBuiltInPreview", "KeepVideoBuiltInPreviewAlive", "BuiltInPreviewAliveTimeout",
    "ThumbnailPreviewWidth", "ThumbnailUpdateInterval",
    # Input forwarding: whether the app forwards Win32 mouse input to libmpv at
    # all, and whether it restricts that to disc navigation. Read by
    # MpvPlayerPage_Mouse.cs. The mpv option these would otherwise name is about
    # mpv's OWN window, which this app does not use.
    "EnableMouseInput", "EnableMouseInputDiscNavOnly",
    # Whether the mpv.conf editor keeps a backup before writing. Read by
    # MpvConfEditorPage.xaml.cs. Nothing to do with mpv itself.
    "EnableSaveBackup",
    # UI-only settings persisted by their own handlers (window title from
    # MainWindow). PlaylistWidth used to be here too; the resizable playlist
    # column it belonged to was removed with the split layout, so the setting
    # no longer exists.
    "WindowTitle",
    # PluginConfigWriter channel (script-opts/*.conf)
    "HdrAutoLog", "MetadataOsdEnabled", "MetadataOsdAutohideTimeout",
    "MetadataOsdShowChapter", "MetadataOsdEnableForVideo", "MetadataOsdEnableForImage",
    "MetadataOsdEnableForAudio", "MetadataOsdEnableForAudioWithAlbumArt",
    "MetadataOsdAutohideForAudio", "MetadataOsdAutohideForAudioWithAlbumArt",
    "MetadataOsdAutohideStatusTimeout", "MetadataOsdShowAlbumTrack",
    "MetadataOsdMessageMaxLength", "CoverArtPreferEmbedded", "CoverArtAlwaysScan",
    "CoverArtLoadFromFilesystem", "CoverArtPreload", "CoverArtNames", "CoverArtImageExts",
    "HdrOverrideMode",
    
    # ManagedMpvConfig channel (ytdl_hook script options in mpv.conf)
    
    # Special cases handled outside ToCommand
    "Volume",               # passed at mpv Initialize, not applied at runtime
    "ShowOsdPlayingMsg",    # merged into the OsdPlayingMsg case
    # Speed was listed here until 2026-09-21, and its presence was itself the
    # finding: it is NOT app-owned, it was simply unmapped, while the check's
    # message ("has no ToCommand mapping") was accurate and being silenced by
    # hand. MpvSettings.ToCommand now has a Speed arm, so the property no longer
    # lands in `appsettings - mpvsettings` at all. Kept out of ApplyAll by
    # MpvSettings.SessionStateKeys instead, which is where that decision belongs
    # -- it is about reset semantics, not about whether the value reaches mpv.
    "LoopFile",             # controlled by the status-bar repeat button
    "LoopPlaylist",         # controlled by the bundled mpv.conf
}


def main() -> int:
    appsettings = parse_appsettings_props(SRC)
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

    # The exemption list has to keep up with the property store. It used to be read
    # only as a skip set, so a deleted property's exemption stayed behind and the
    # list went on describing settings that no longer existed. Nine had accumulated.
    # An exemption that outlives its subject is worse than none: the next reader
    # treats the list as a description of the code.
    stale_exemptions = UNMAPPED_OK - appsettings
    for key in sorted(stale_exemptions):
        errors.append(
            f"UNMAPPED_OK names '{key}', which is not an AppSettings property any more "
            f"- remove the exemption or restore the property")

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
