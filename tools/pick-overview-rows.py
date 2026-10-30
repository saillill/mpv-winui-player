"""Which generated rows belong on the category overview rather than behind a card.

The rule this implements, in the user's words: common settings are listed on the
category page, and only complex or rarely-changed ones go behind a fold. The
page already had a mechanism for that -- `pinnedOverviewOptions` -- holding
eleven hand-written rows. This extends it to the generated half.

Two inputs, and the difference between them matters:

  * **Fact**: the option is named in a config file this repository ships
    (`data/mpv.conf`, `data/profiles.conf`, `data/script-opts.conf`). 39 of them
    are. That is not the same as "common" -- `load-scripts` and `input-ipc-server`
    are in those files because the deployment needs them, not because a user
    tunes them -- so the fact is an input, not an answer.

  * **Judgement**: the table below. Each row says why, because the next person
    asked to change it should be able to disagree with a reason rather than with
    a number.

The output is printed as C# for `pinnedOverviewOptions`, in the source's own
nameof form. Generated rows are `Mpv_<option-name>` with dashes as underscores.

Usage: python tools/pick-overview-rows.py
"""
import glob
import json
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCHEMA = os.path.join(REPO, "src", "mpvw-app", "MpvConfOptions", "mpv-options.json")

# option name -> why a user opens the settings page looking for it.
#
# Deliberately absent, though they are in a shipped config: include, scripts,
# script-opts, log-file, msg-level, msg-module, input-ipc-server, input-conf,
# input-default-bindings, input-builtin-bindings, input-commands, load-*,
# use-filedir-conf, reset-on-next-file, write-filename-in-watch-later-config,
# watch-later-options. Those describe how mpv is wired up. They belong with the
# program category's advanced rows, and the first cut of this list had them on
# the overview, which is how the distinction got written down.
OVERVIEW = {
    # --- playback the user reaches for while watching -------------------
    #
    # pause and idle are deliberately absent. Both are mpv *options* and both
    # are also runtime state: a settings page offering "start paused" or "stay
    # idle" is describing the session, not a preference, and the player already
    # owns those.
    "speed": "playback speed; every player exposes it",
    "loop-playlist": "repeat mode, next to speed in every player's UI",
    "keep-open": "what happens at end of file",
    "volume": "volume",
    "volume-max": "the ceiling the volume slider can reach",

    # --- output ---------------------------------------------------------
    "vo": "video output driver",
    "ao": "audio output driver",
    "gpu-api": "which graphics API the renderer uses",
    "hwdec": "hardware decoding: the single most consequential video setting",
    "hwdec-codecs": "which codecs are allowed to use it",
    "gpu-shader-cache": "recompile shaders every launch or not",
    "d3d11-flip": "the presentation path on Windows",
    "d3d11-exclusive-fs": "exclusive fullscreen, a fullscreen-behaviour question",
    "d3d11-output-mode": "windowed vs composition",
    "d3d11-adapter": "which GPU to render on",
    "vulkan-device": "which GPU, on the Vulkan path",

    # --- window ---------------------------------------------------------
    "geometry": "window size and position",
    "autofit-smaller": "shrink the window to fit small videos",
    "keepaspect": "keep the aspect ratio",
    "keepaspect-window": "keep the window's aspect with the video",
    "ontop": "always on top",
    "force-window": "open a window with nothing to play",
    "auto-window-resize": "resize the window when the video changes",
    "hidpi-window-scale": "scale with the monitor's DPI",

    # --- scaling and quality --------------------------------------------
    "scale": "upscaler",
    "cscale": "chroma upscaler",
    "dscale": "downscaler",
    "tscale": "temporal scaler",
    "interpolation": "frame interpolation; expensive and immediately visible",
    "deinterlace": "deinterlacing, which some sources require",
    "deband": "debanding; visible on gradients",
    "dither": "dithering",
    "dither-depth": "dithering depth",
    "correct-downscaling": "linear-light downscaling",
    "linear-downscaling": "linear-light downscaling",
    "linear-upscaling": "linear-light upscaling",
    "sigmoid-upscaling": "sigmoid upscaling",

    # --- HDR and colour -------------------------------------------------
    "target-peak": "display peak brightness, the HDR knob people actually set",
    "target-prim": "display primaries",
    "target-trc": "display transfer curve",
    "tone-mapping": "how HDR is mapped to the display",
    "gamut-mapping-mode": "how out-of-gamut colour is mapped",
    "icc-profile-auto": "use the monitor's ICC profile",

    # --- subtitles ------------------------------------------------------
    "sub-font": "subtitle font",
    "sub-font-size": "subtitle size",
    "sub-color": "subtitle colour",
    "sub-back-color": "subtitle background",
    "sub-outline-size": "subtitle outline",
    "sub-blur": "subtitle blur",
    "sub-shadow-offset": "subtitle shadow",
    "sub-auto": "which external subtitle files load",
    "sub-file-paths": "where to look for them",
    "slang": "preferred subtitle languages",
    "alang": "preferred audio languages",
    "sub-ass-override": "whether a subtitle's own styling wins",
    "embeddedfonts": "use fonts embedded in the subtitle file",
    "sub-use-margins": "keep subtitles off the black bars",

    # --- audio ----------------------------------------------------------
    "audio-device": "output device",
    "audio-exclusive": "exclusive mode",
    "audio-channels": "channel layout",
    "audio-pitch-correction": "keep pitch when changing speed",

    # --- cache and streaming --------------------------------------------
    "cache": "the cache switch",
    "demuxer-max-bytes": "how much to cache ahead",
    "demuxer-max-back-bytes": "how much to keep behind",
    "user-agent": "the user agent for network streams",

    # --- files and screenshots ------------------------------------------
    "screenshot-dir": "where screenshots go",
    "screenshot-format": "screenshot format",
    "screenshot-template": "screenshot filename",
    "screenshot-jpeg-quality": "JPEG quality",
    "screenshot-png-compression": "PNG compression",
    "screenshot-high-bit-depth": "capture at more than 8 bits",
    "watch-later-dir": "where resume points are stored",
    "resume-playback": "resume where you left off",
    "save-position-on-quit": "same, at quit time",
    "directory-filter-types": "which files the playlist picks up",
    "directory-mode": "how folders are scanned",
    "autocreate-playlist": "build a playlist from the folder",

    # --- OSD ------------------------------------------------------------
    "osc": "the on-screen controller",
    "osd-bar": "the seek bar",
    "osd-duration": "how long messages stay",
    "osd-font-size": "OSD text size",
    "osd-on-seek": "what shows while seeking",
}


def prop_for(name: str) -> str:
    return "Mpv_" + name.replace("-", "_")


def mpv_option_to_property() -> dict[str, str]:
    """mpv option name -> AppSettings property, from MpvOptionNames.

    A hand-written row and a generated row can name the same mpv option under
    different property names -- `audio-device` is written by `AudioDevice` (hand
    written) and would also be `Mpv_audio_device` (generated). Pinning both puts
    the same setting on the overview twice, so this map is what detects it.
    """
    src = open(os.path.join(REPO, "src", "mpvw-app", "Domains", "MpvConf",
                            "MpvSettings.cs"), encoding="utf-8-sig").read()
    start = src.index("private static readonly Dictionary<string, string> MpvOptionNames")
    brace = src.index("{", start)
    depth = 0
    end = None
    for i in range(brace, len(src)):
        if src[i] == "{":
            depth += 1
        elif src[i] == "}":
            depth -= 1
            if depth == 0:
                end = i + 1
                break
    if end is None:
        raise SystemExit("could not find the end of MpvOptionNames")
    body = src[brace:end]
    return {mpv: prop for prop, mpv in re.findall(
        r'\[\s*nameof\(AppSettings\.(\w+)\)\s*\]\s*=\s*"([a-z0-9\-]+)"', body)}


def main() -> int:
    schema = json.load(open(SCHEMA, encoding="utf-8-sig"))
    by_name = {e["name"]: e for e in schema}
    deprecated = {e["name"] for e in schema if e.get("deprecated")}

    real = os.path.join(REPO, "src", "mpvw-app", "Domains", "Settings",
                        "SettingsPage.Options.cs")
    page = open(real, encoding="utf-8-sig").read()
    hand_written = set(re.findall(r"nameof\(AppSettings\.(\w+)\)", page))

    unknown = sorted(n for n in OVERVIEW if n not in by_name)
    if unknown:
        print("ERROR: these are not mpv options: %s" % ", ".join(unknown))
        print("A typo here would silently pin nothing.")
        return 1

    gone = sorted(n for n in OVERVIEW if n in deprecated)
    if gone:
        print("ERROR: these are deprecated in the schema: %s" % ", ".join(gone))
        return 1

    # A row can only be pinned if the settings model has a property for it.
    # Three did not: vo, ao and gpu-api are declared "Object settings list" in
    # mpv's output, which the generator does not turn into rows at all, and
    # vulkan-device is not wired either. Checking here rather than by build
    # failure keeps the reason next to the list.
    declared = set()
    for path in glob.glob(os.path.join(REPO, "src", "mpvw-app", "Domains",
                                       "SettingsModel", "AppSettings*.cs")):
        declared |= set(re.findall(
            r"public [^\n]*?\s(\w+)\s*\{",
            open(path, encoding="utf-8-sig").read()))
    # Only the ones that would otherwise be emitted: an option already covered
    # by a hand-written row has a property, just not this one.
    def _covered(name):
        owner_prop = mpv_option_to_property().get(name)
        return owner_prop is not None and owner_prop in hand_written

    absent = sorted(n for n in OVERVIEW
                    if prop_for(n) not in declared and not _covered(n))
    if absent:
        print("// skipped, no AppSettings property to pin:", file=sys.stderr)
        for name in absent:
            print("//   %-18s %s -- mpv calls it %r"
                  % (name, prop_for(name), by_name[name]["desc"][:38]),
                  file=sys.stderr)
        print(file=sys.stderr)

    # Already on the overview as a hand-written row? Then pinning the generated
    # twin would show the same setting twice.
    owner = mpv_option_to_property()
    overlap = sorted(n for n in OVERVIEW
                     if (o := owner.get(n)) and o in hand_written)
    generated = sorted(
        (n for n in OVERVIEW
         if n not in set(overlap) and prop_for(n) in declared),
        key=prop_for)

    print("// %d option(s) judged common; %d are generated rows, %d are already "
          "hand-written rows" % (len(OVERVIEW), len(generated), len(overlap)))
    if overlap:
        print("// already on the overview as a hand-written row, so not pinned twice:")
        print("//   " + ", ".join(overlap))
    print()
    width = max(len(prop_for(n)) for n in generated) + len("nameof(AppSettings.)") + 1
    for name in generated:
        line = "nameof(AppSettings.%s)," % prop_for(name)
        print("    %-*s // %s" % (width, line, OVERVIEW[name]))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
