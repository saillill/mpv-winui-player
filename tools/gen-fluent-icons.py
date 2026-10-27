"""Regenerate Infrastructure/Common/View/FluentIcons.Gen.cs.

    python tools/gen-fluent-icons.py

Downloads the icons from Microsoft's official Fluent UI System Icons repository
and rewrites the generated constants file. The SVGs are not stored in this repo:
they are fetched fresh, so the file always reflects what Microsoft actually
publishes rather than a stale copy of it.

Only the constants are generated. The helpers that turn them into Geometry and
PathIcon objects live in the hand-written FluentIcons.cs, so running this can
never clobber them. Same split as AppLang.cs / AppLang.Gen.cs.

To add an icon, add an entry to ICONS below and run the script. Do not write
path data by hand: an invented path renders as a confident-looking wrong shape
and nothing in this project's checks would notice.
"""

import pathlib
import re
import sys
import urllib.error
import urllib.parse
import urllib.request

REPO_RAW = "https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/assets"
OUT = pathlib.Path(__file__).resolve().parent.parent / "mpv-winui/mpv-winui/Infrastructure/Common/View/FluentIcons.Gen.cs"

# member name -> (icon folder in Microsoft's repo, svg file name)
ICONS = [
    ("Search", "Search", "ic_fluent_search_24_regular.svg"),
    ("FolderOpen", "Folder Open", "ic_fluent_folder_open_24_regular.svg"),
    ("Folder", "Folder", "ic_fluent_folder_24_regular.svg"),
    ("FolderAdd", "Folder Add", "ic_fluent_folder_add_24_regular.svg"),
    ("Home", "Home", "ic_fluent_home_24_regular.svg"),
    ("Video", "Video", "ic_fluent_video_24_regular.svg"),
    ("Music", "Music Note 2", "ic_fluent_music_note_2_24_regular.svg"),
    ("History", "History", "ic_fluent_history_24_regular.svg"),
    ("List", "Apps List", "ic_fluent_apps_list_24_regular.svg"),
    # Transport. Filled, not outline: these sit on the bar at 20-24px where a
    # 1px outline reads as a hairline, which is why Microsoft's own player draws
    # its transport glyphs solid.
    ("Play", "Play", "ic_fluent_play_24_filled.svg"),
    ("Pause", "Pause", "ic_fluent_pause_24_filled.svg"),
    # Kept alongside Play so the play button can be either a ring plus a solid
    # triangle, or one self-contained circular glyph, without a round trip to
    # the icon repository.
    ("PlayCircle", "Play Circle", "ic_fluent_play_circle_24_regular.svg"),
    # The play button's other half. Kept beside PlayCircle so the button can be
    # drawn as one self-contained circular glyph in both states.
    ("PauseCircle", "Pause Circle", "ic_fluent_pause_circle_24_regular.svg"),
    # The control bar's repeat/shuffle pair, replacing three hand-drawn path
    # sets (Center and Compact control-bar styles, and ControlBarIcons.cs).
    ("ArrowShuffle", "Arrow Shuffle", "ic_fluent_arrow_shuffle_24_regular.svg"),
    ("ArrowRepeatAll", "Arrow Repeat All", "ic_fluent_arrow_repeat_all_24_regular.svg"),
    # The library's media-kind and empty-state glyphs. The official Media
    # Player uses the film frame for video and the record sleeve for a
    # playlist; both were rendered from source and compared against the
    # official screenshots before being chosen. ("Video" above is the camera
    # glyph and is deliberately not either of these.)
    ("Filmstrip", "Filmstrip", "ic_fluent_filmstrip_24_regular.svg"),
    ("Album", "Album", "ic_fluent_album_24_regular.svg"),
    # The playing page's three tool keys, restored beside the transport bar.
    # Camera for the screenshot, Info for media info, and Filmstrip is already
    # there for the playlist side panel. All three were rendered from source
    # before being wired up.
    ("Camera", "Camera", "ic_fluent_camera_24_regular.svg"),
    ("Info", "Info", "ic_fluent_info_24_regular.svg"),
    # The shell's open menu: a document, a folder (already present above) and a
    # globe for the URL leg. Sourced, not drawn.
    ("File", "Document", "ic_fluent_document_24_regular.svg"),
    ("Globe", "Globe", "ic_fluent_globe_24_regular.svg"),
    # The queue's two actions. Delete is the trash can the official queue uses;
    # Add is the plus.
    ("Delete", "Delete", "ic_fluent_delete_24_regular.svg"),
    ("Add", "Add", "ic_fluent_add_24_regular.svg"),
]


def fetch(folder: str, filename: str) -> str:
    url = f"{REPO_RAW}/{urllib.parse.quote(folder)}/SVG/{filename}"
    try:
        with urllib.request.urlopen(url, timeout=30) as response:
            return response.read().decode("utf-8")
    except urllib.error.URLError as error:
        # Loudly, because the alternative is silently emitting a file with
        # missing icons - and a missing icon is an empty PathIcon, which looks
        # like a layout bug rather than a fetch failure.
        sys.exit(f"could not download {url}: {error}")


def main() -> int:
    constants = []
    provenance = []

    for member, folder, filename in ICONS:
        svg = fetch(folder, filename)
        paths = re.findall(r'd="([^"]*)"', svg)
        if len(paths) != 1:
            sys.exit(f"{filename}: expected exactly 1 path, found {len(paths)}")

        data = paths[0].strip()
        # XAML's geometry mini-language: F1 is the non-zero fill rule, which is
        # the rule these icons are drawn for and the prefix the repo's existing
        # path icons already carry.
        if not data.startswith("F"):
            data = "F1 " + data

        constants.append(f'    public const string {member} =\n        "{data}";\n')
        provenance.append(f"///   {member:<12} assets/{folder}/SVG/{filename}")

    OUT.write_text(
        """// <auto-generated>
// Regenerate with: python tools/gen-fluent-icons.py
// Source: https://github.com/microsoft/fluentui-system-icons (MIT)
//
// Do not edit by hand. The helpers that turn these into Geometry and PathIcon
// objects live in FluentIcons.cs, which is hand-written, so regenerating this
// file cannot lose them.
// </auto-generated>

namespace mpv_winui.Infrastructure.Common.View;

public static partial class FluentIcons
{
    // Provenance, one line per constant below.
"""
        + "\n".join(provenance)
        + "\n\n"
        + "\n".join(constants)
        + "}\n",
        encoding="utf-8",
    )

    print(f"wrote {OUT.relative_to(OUT.parents[4])} ({OUT.stat().st_size} bytes)")
    for member, folder, filename in ICONS:
        print(f"  {member:<12} <- assets/{folder}/SVG/{filename}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
