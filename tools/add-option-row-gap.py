"""Add the option/control column gap to the settings row templates.

The row templates are two-column grids whose right column holds the control.
Most declared no ColumnSpacing at all, so the description ran flush into the
control and the pair read as one paragraph. This inserts a shared spacing
token on the grid that wraps each row's label-and-description block.

Kept as a script rather than a dozen hand edits because the files are CRLF and
each has a slightly different grid signature; the script matches on the
structure it needs and reports what it changed, so a template that does not
match is visible instead of silently skipped.

Usage:
    python tools/add-option-row-gap.py [--dry]
"""

import io
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
CTL = REPO / "mpv-winui" / "mpv-winui" / "Domains" / "Settings" / "Controls"

TOKEN = '{StaticResource MpvOptionControlGap}'

# Each entry: file -> the exact grid opening tag to rewrite, plus its
# replacement. Written out rather than pattern-matched because the five are
# genuinely shaped differently and a loose regex would hit the wrong grid.
TARGETS = [
    # (file, old_open_tag, new_open_tag)
    (
        "OptionActionControl.xaml",
        '<Grid VerticalAlignment="Center">',
        f'<Grid ColumnSpacing="{TOKEN}" VerticalAlignment="Center">',
    ),
    (
        "OptionDoubleControl.xaml",
        '<Grid VerticalAlignment="Center">',
        f'<Grid ColumnSpacing="{TOKEN}" VerticalAlignment="Center">',
    ),
    (
        "OptionIntegerControl.xaml",
        '<Grid VerticalAlignment="Center">',
        f'<Grid ColumnSpacing="{TOKEN}" VerticalAlignment="Center">',
    ),
    (
        "OptionColorControl.xaml",
        '<Grid ColumnSpacing="{StaticResource MpvSpaceS}">',
        f'<Grid ColumnSpacing="{TOKEN}">',
    ),
    (
        "OptionStringListControl.xaml",
        '<Grid ColumnSpacing="{StaticResource MpvSpaceM}">',
        f'<Grid ColumnSpacing="{TOKEN}">',
    ),
]


def main():
    dry = "--dry" in sys.argv
    for name, old, new in TARGETS:
        path = CTL / name
        if not path.exists():
            print(f"MISSING  {name}")
            continue
        raw = path.read_bytes()
        crlf = raw.count(b"\r\n") > 0
        text = raw.decode("utf-8-sig")
        if new in text:
            print(f"SKIP     {name}  (already has the gap)")
            continue
        if text.count(old) != 1:
            print(f"AMBIGUOUS {name}  ({text.count(old)} matches for {old!r})")
            continue
        text = text.replace(old, new, 1)
        if not dry:
            # Preserve the BOM state and the line endings we found.
            bom = raw[:3] == b"\xef\xbb\xbf"
            data = text.encode("utf-8")
            if bom:
                data = b"\xef\xbb\xbf" + data
            path.write_bytes(data)
        print(f"PATCHED  {name}  crlf={crlf}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
