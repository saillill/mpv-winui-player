"""A page field may not be named after a property on the data it binds.

The defect this prevents: MpvPlayerPage.xaml declared `x:Name="AlbumTitle"` for the
album page's heading, which generated a field of that name on the page class -
while the album CARD template bound `{x:Bind AlbumTitle}` against LibraryEntry's
property of the same name. The WindowsAppSDK 2.3.9 XAML compiler answered with
WMC9999, an internal error that names no file and no line, and the repository
spent a long time blaming {x:Bind} itself. Renaming the page field fixed it.

The compiler DOES flag the collision, so nothing is silent here - but it flags it
as an internal error, which sends the reader to the wrong place. This check names
the actual conflict.
"""

import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
APP = REPO / "src" / "mpvw-app"

# Every type whose properties a page may bind. Each one is a record or class whose
# public properties are legal {x:Bind} paths, so each one can collide with a field.
SOURCES = [
    APP / "Domains" / "Playback" / "LibraryEntry.cs",
    APP / "Domains" / "Playback" / "LibraryEntry.Selection.cs",
    APP / "Domains" / "Library" / "VideoLibraryTypes.cs",
]

NAME = re.compile(r'x:Name="([^"]+)"')
PROPERTY = re.compile(
    r"^\s*(?:public|internal)\s+(?:sealed\s+|static\s+|override\s+)*"
    r"(?:required\s+)?[A-Za-z_][\w<>?\[\], .]*?\s+([A-Za-z_]\w*)\s*"
    r"(?:\{|=>|$)",
    re.M)


def source_properties():
    props = {}
    for path in SOURCES:
        if not path.is_file():
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        # Comments carry property-looking text; strip them before matching, the
        # same mistkae the option generator made once (a comment mentioning an
        # option name made the option look hand-written).
        text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
        text = re.sub(r"^\s*//.*$", "", text, flags=re.M)
        for name in PROPERTY.findall(text):
            props.setdefault(name, path.name)
    return props


def main() -> int:
    props = source_properties()
    if len(props) < 10:
        # A regex that stopped matching is a check that always passes. This
        # repository's rule: reject input you cannot trust.
        print("FAIL: only %d properties found across %d source(s); the parser "
              "is not reading what it thinks it is" % (len(props), len(SOURCES)),
              file=sys.stderr)
        return 2

    problems = 0
    scanned = 0
    for path in sorted(APP.rglob("*.xaml")):
        if {"bin", "obj", "packages"}.intersection(path.relative_to(APP).parts):
            continue
        scanned += 1
        text = path.read_text(encoding="utf-8", errors="replace")
        text = re.sub(r"<!--.*?-->", "", text, flags=re.S)
        for match in NAME.finditer(text):
            name = match.group(1)
            if name in props:
                line = text.count("\n", 0, match.start()) + 1
                print("ERROR: %s:%d: x:Name=%r collides with a property of the "
                      "same name on %s. The XAML compiler reports this as "
                      "WMC9999 with no file or line; rename the field."
                      % (path.relative_to(REPO), line, name, props[name]),
                      file=sys.stderr)
                problems += 1

    if scanned == 0:
        print("FAIL: no .xaml files found under %s" % APP, file=sys.stderr)
        return 2

    if problems:
        return 1

    print("OK: %d XAML file(s), %d bound property name(s), no page field shadows one"
          % (scanned, len(props)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
