#!/usr/bin/env python3
"""Generate the mpv option schema from the bundled mpv build.

`mpv --list-options` prints every option with its type, its range or its
enumerated values, its default, and whether it wants a file path. That is
almost exactly the shape the mpv.conf editor's schema wants, so the schema does
not have to be hand-written -- and a hand-written one is how the editor ended
up with nine entries and therefore nothing to show on a fresh install.

What the listing cannot supply is the prose. mpv keeps that in its manual, so
each entry carries a link to the option's manual anchor and a description
assembled from the machine-readable facts (type, range, choices, default,
file-path requirement). That is honest: the editor shows what it knows and
offers the manual for the rest.

Usage:
    python tools/gen-mpv-options.py --mpv /path/to/mpv.exe
    python tools/gen-mpv-options.py --mpv mpv --out <file.json>

Output goes to mpv-winui/mpv-winui/MpvConfOptions/mpv-options.json by default,
which the project ships so the editor works with no manual setup.
"""

import argparse
import json
import re
import shutil
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
DEFAULT_OUT = REPO / "mpv-winui" / "mpv-winui" / "MpvConfOptions" / "mpv-options.json"

MANUAL = "https://mpv.io/manual/master/#options-"

# mpv's option names cluster by prefix; this maps them to the groups the editor
# shows. Ordered: the first match wins, so narrower prefixes come first.
GROUP_RULES = [
    ("icc", "Colour & HDR"),
    ("target", "Colour & HDR"),
    ("tone-mapping", "Colour & HDR"),
    ("hdr", "Colour & HDR"),
    ("gamma", "Colour & HDR"),
    ("gamut", "Colour & HDR"),
    ("dither", "Colour & HDR"),
    ("vf", "Video & rendering"),
    ("vo", "Video & rendering"),
    ("gpu", "Video & rendering"),
    ("glsl", "Video & rendering"),
    ("vd", "Video & rendering"),
    ("video", "Video & rendering"),
    ("hwdec", "Video & rendering"),
    ("angle", "Video & rendering"),
    ("d3d11", "Video & rendering"),
    ("opengl", "Video & rendering"),
    ("vulkan", "Video & rendering"),
    ("sws", "Video & rendering"),
    ("zimg", "Video & rendering"),
    ("deband", "Video & rendering"),
    ("interpolation", "Video & rendering"),
    ("deinterlace", "Video & rendering"),
    ("sigmoid", "Video & rendering"),
    ("linear", "Video & rendering"),
    ("lut", "Video & rendering"),
    ("correct", "Video & rendering"),
    ("display", "Video & rendering"),
    ("egl", "Video & rendering"),
    ("mf", "Video & rendering"),
    ("image", "Video & rendering"),
    ("background", "Video & rendering"),
    ("scale", "Video & rendering"),
    ("dscale", "Video & rendering"),
    ("cscale", "Video & rendering"),
    ("tscale", "Video & rendering"),
    ("secondary-sub", "Subtitles"),
    ("subs", "Subtitles"),
    ("sub", "Subtitles"),
    ("slang", "Subtitles"),
    ("osd", "OSD"),
    ("audio", "Audio"),
    ("ao", "Audio"),
    ("ad", "Audio"),
    ("af", "Audio"),
    ("openal", "Audio"),
    ("replaygain", "Audio"),
    ("gapless", "Audio"),
    ("volume", "Audio"),
    ("mute", "Audio"),
    ("demuxer", "Streaming & cache"),
    ("cache", "Streaming & cache"),
    ("stream", "Streaming & cache"),
    ("network", "Streaming & cache"),
    ("http", "Streaming & cache"),
    ("tls", "Streaming & cache"),
    ("curl", "Streaming & cache"),
    ("ytdl", "Streaming & cache"),
    ("cookies", "Streaming & cache"),
    ("proxy", "Streaming & cache"),
    ("referrer", "Streaming & cache"),
    ("user-agent", "Streaming & cache"),
    ("alang", "Tracks"),
    ("vlang", "Tracks"),
    ("aid", "Tracks"),
    ("vid", "Tracks"),
    ("sid", "Tracks"),
    ("track", "Tracks"),
    ("screenshot", "Player & window"),
    ("watch", "Player & window"),
    ("playlist", "Player & window"),
    ("input", "Player & window"),
    ("script", "Player & window"),
    ("load", "Player & window"),
    ("loop", "Player & window"),
    ("ab-loop", "Player & window"),
    ("speed", "Player & window"),
    ("autofit", "Player & window"),
    ("geometry", "Player & window"),
    ("window", "Player & window"),
    ("border", "Player & window"),
    ("fullscreen", "Player & window"),
    ("ontop", "Player & window"),
    ("fs", "Player & window"),
    ("cursor", "Player & window"),
    ("mouse", "Player & window"),
    ("idle", "Player & window"),
    ("keep-open", "Player & window"),
    ("pause", "Player & window"),
    ("shuffle", "Player & window"),
    ("start", "Player & window"),
    ("end", "Player & window"),
    ("length", "Player & window"),
    ("files", "Player & window"),
    ("frames", "Player & window"),
    ("hr-seek", "Player & window"),
    ("term", "Player & window"),
    ("msg", "Player & window"),
    ("clipboard", "Player & window"),
    ("cover", "Player & window"),
    ("dvd", "Player & window"),
    ("cdda", "Player & window"),
    ("bluray", "Player & window"),
    ("disc", "Player & window"),
    ("show", "Player & window"),
    ("force", "Player & window"),
    ("list", "Player & window"),
    ("native", "Player & window"),
]

LINE = re.compile(r"^ --([A-Za-z0-9][A-Za-z0-9.-]*)\s+(.*)$")
DEFAULT = re.compile(r"\(default:\s*(.*?)\)\s*(?:\[file\])?\s*$")
RANGE = re.compile(r"\((-?[\d.]+)\s+to\s+(-?[\d.]+)\)")
# The choice list ends at the first '(' -- what follows is a qualifier such as
# "(or an integer)" or "(default: ...)", and splitting the whole remainder on
# whitespace turns those words into bogus choices.
CHOICES = re.compile(r"^Choices:\s*([^(]*?)\s*(?:\(|$)")
OR_INTEGER = re.compile(r"\(or an integer\)")

SIMPLE_TYPE = {
    "Flag": "bool",
    "String": "string",
    "Integer": "int",
    "Float": "float",
    "Double": "float",
    "ByteSize": "raw",
    "Color": "raw",
    "Time": "raw",
    "Geometry": "raw",
    "Window size": "raw",
    "String list": "array",
    "Key/value list": "array",
    "Object settings list": "array",
    "Audio channels or channel map": "raw",
    "Relative time or percent position": "raw",
    "Print": "raw",
}

BOOL_WORDS = {"yes", "no"}


def group_for(name):
    # Prefix must match the whole first segment: a bare startswith() would send
    # "volume" to Video because it begins with "vo", and "adaptive" to Audio
    # because it begins with "ad".
    for prefix, group in GROUP_RULES:
        if name == prefix or name.startswith(prefix + "-"):
            return group
    return "General"


def classify(rest):
    """Return (values, default, deprecated, desc) for the text after the name."""
    if rest.startswith("alias for ") or rest.startswith("alias "):
        return None  # aliases would duplicate the target option
    if rest.startswith("removed"):
        # Keep a value so the entry still renders as an editable text row in the
        # editor rather than arriving with no control at all.
        return ([{"type": "raw"}], None, True,
                "Removed in this mpv build; kept so old conf lines parse.")

    deprecated = "[deprecated]" in rest
    wants_file = "[file]" in rest

    # Command-line-only options (--help, --version, --list-options, --playlist,
    # --config-dir, the --h/--o short forms, and mpv's internal --{ / --})
    # cannot appear in a config file at all, so the editor must not offer them.
    # The listing marks them; skipping on the marker is what keeps the schema
    # honest rather than on a name list that would rot.
    if "[not in config files]" in rest:
        return None

    default_match = DEFAULT.search(rest)
    default = default_match.group(1).strip() if default_match else None
    default = default or None

    value = {}
    desc_bits = []

    choices_match = CHOICES.match(rest)
    if choices_match:
        words = choices_match.group(1).split()
        numeric = [w for w in words if re.fullmatch(r"-?[\d.]+", w)]
        if set(words) <= BOOL_WORDS and words:
            value["type"] = "bool"
            desc_bits.append("On or off")
        elif numeric and len(numeric) == len(words):
            value["type"] = "float" if any("." in w for w in words) else "int"
            desc_bits.append("One of: " + ", ".join(words))
        else:
            value["type"] = "string"
            value["enum"] = [{"value": w} for w in words]
            desc_bits.append("One of: " + ", ".join(words))
            if OR_INTEGER.search(rest):
                desc_bits.append("or a number")
    else:
        # "Integer (0 to 6) (default: 0)" -> take the token before the first '('
        head = rest.split("(", 1)[0].strip()
        mapped = SIMPLE_TYPE.get(head)
        if mapped is None:
            for token, t in SIMPLE_TYPE.items():
                if head.startswith(token):
                    mapped = t
                    break
        value["type"] = mapped or "raw"
        if head:
            desc_bits.append(head)

    # A range only means something for a numeric value; attaching it to a
    # string or raw value would advertise a constraint the editor cannot show.
    range_match = RANGE.search(rest)
    if range_match and value["type"] in ("int", "float"):
        value["minimum"] = float(range_match.group(1))
        value["maximum"] = float(range_match.group(2))
        desc_bits.append(f"Range {range_match.group(1)} to {range_match.group(2)}")

    if default is not None:
        desc_bits.append(f"Default: {default}")
    if wants_file:
        desc_bits.append("Expects a file path")
    if deprecated:
        desc_bits.append("Deprecated")

    return [value], default, deprecated, "; ".join(desc_bits)


def parse(listing):
    items = {}
    for line in listing.splitlines():
        if line.startswith("["):  # mpv's own startup complaints
            continue
        m = LINE.match(line)
        if not m:
            continue
        name, rest = m.group(1), m.group(2).strip()
        if name in items:
            continue
        decided = classify(rest)
        if decided is None:
            continue
        values, default, deprecated, desc = decided
        items[name] = {
            "name": name,
            "group": group_for(name),
            "desc": desc,
            "link": MANUAL + name,
            "deprecated": deprecated,
            "default": default,
            "values": values,
        }
    return [items[k] for k in sorted(items)]


def find_mpv(explicit):
    if explicit:
        return explicit
    found = shutil.which("mpv") or shutil.which("mpv.com") or shutil.which("mpv.exe")
    if found:
        return found
    sys.exit("ERROR: mpv not found. Pass --mpv <path>, or put it on PATH.")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--mpv", help="path to the mpv executable that owns this schema")
    ap.add_argument("--out", type=Path, default=DEFAULT_OUT)
    args = ap.parse_args()

    exe = find_mpv(args.mpv)
    proc = subprocess.run([exe, "--list-options"], capture_output=True, text=True)
    if proc.returncode != 0 and not proc.stdout:
        sys.exit(f"ERROR: {exe} --list-options failed: {proc.stderr.strip()[:200]}")

    items = parse(proc.stdout)

    args.out.parent.mkdir(parents=True, exist_ok=True)
    # CRLF to match the other JSON this project ships (Languages\*.json); the
    # repository checks out CRLF, so emitting LF here would show up as a
    # whole-file difference the first time anyone touches it in an editor.
    args.out.write_text(json.dumps(items, ensure_ascii=False, indent=2) + "\n",
                        encoding="utf-8", newline="\r\n")

    groups = {}
    kinds = {}
    for it in items:
        groups[it["group"]] = groups.get(it["group"], 0) + 1
        k = it["values"][0]["type"] if it["values"] else "?"
        kinds[k] = kinds.get(k, 0) + 1

    print(f"mpv       : {exe}")
    print(f"options   : {len(items)}")
    print(f"written   : {args.out.relative_to(REPO) if args.out.is_relative_to(REPO) else args.out}")
    print()
    print("by group:")
    for g, n in sorted(groups.items(), key=lambda kv: -kv[1]):
        print(f"  {n:5d}  {g}")
    print()
    print("by kind:")
    for k, n in sorted(kinds.items(), key=lambda kv: -kv[1]):
        print(f"  {n:5d}  {k}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
