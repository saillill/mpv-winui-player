#!/usr/bin/env python3
"""Catch a MaxWidth content column that a scrolling parent has made unrunnable.

Background
----------
The settings page and the media-info panel both hold their cards in a
MaxWidth box that is meant to sit in the middle of the window:

    <ScrollViewer x:Name="BrowseHost" Padding="12,4,12,0">
        <StackPanel MaxWidth="1060" HorizontalAlignment="Center">

That reads as centring and is not. A ScrollViewer left at its default
horizontal settings (HorizontalScrollMode.Enabled, bar Auto) measures its
content against *infinite* width, because the content is allowed to be wider
than the viewport and scroll. The MaxWidth box therefore has no finite parent
width to be centred inside: it measures at 1060, gets laid out at x=0 of the
scrollable extent, and HorizontalAlignment="Center" is silently ignored. The
cards sit hard left in a window of any size, and nothing in the markup says so.

Setting HorizontalScrollMode="Disabled" is what makes the ScrollViewer impose
its own width on the content, and only then does the alignment mean anything.

This cost real time to find: the XAML looked right, the build was clean, the
deployed .xbf was in sync with the source, and the centring simply did nothing.
It is invisible without running the app, so it is worth a check.

What is checked
---------------
A ScrollViewer that is not horizontal-scroll-disabled must not directly contain
an element carrying MaxWidth. Reported as an error: the combination has no
meaning, and the two ways to fix it are both deliberate (disable horizontal
scrolling, or drop the alignment and own the left alignment honestly).

Exit code 1 on any problem.
"""

import re
import sys

from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
SRC = REPO / "mpv-winui" / "mpv-winui"

SKIP_DIRS = {"bin", "obj", "packages", ".vs", "node_modules"}

# A ScrollViewer opening tag, then whatever comes before the first child
# element's closing '>' — enough to see the tag's own attributes and the
# child's, which is all the rule needs.
SCROLLVIEWER = re.compile(r"<ScrollViewer\b([^>]*)>\s*<([A-Za-z0-9_:.]+)\b([^>]*)>", re.S)


def xaml_files():
    for path in sorted(SRC.rglob("*.xaml")):
        if SKIP_DIRS & set(path.relative_to(SRC).parts):
            continue
        if path.is_file():
            yield path


def horizontal_scroll_disabled(attrs):
    """True when the tag itself turns horizontal scrolling off.

    Either property alone is enough to make the content finite, so either
    is accepted. A value is only read from the tag: a Style cannot express
    this, and these sites set it inline.
    """
    mode = re.search(r'HorizontalScrollMode\s*=\s*"([^"]*)"', attrs)
    bar = re.search(r'HorizontalScrollBarVisibility\s*=\s*"([^"]*)"', attrs)
    return ((mode and mode.group(1) == "Disabled")
            or (bar and bar.group(1) == "Disabled"))


def check(files):
    errors = []
    checked = 0

    for path in files:
        rel = path.relative_to(REPO)
        text = path.read_text(encoding="utf-8-sig")

        for match in SCROLLVIEWER.finditer(text):
            sv_attrs, child_tag, child_attrs = match.group(1, 2, 3)
            checked += 1
            if horizontal_scroll_disabled(sv_attrs):
                continue
            if "MaxWidth" not in child_attrs:
                continue
            line = text[:match.start()].count("\n") + 1
            centring = ('HorizontalAlignment="Center"' in child_attrs)
            errors.append(
                f"{rel}:{line}: a ScrollViewer without horizontal scrolling "
                f"disabled wraps <{child_tag} MaxWidth=...>"
                + (" and asks it to centre" if centring else "")
                + " -- the ScrollViewer measures that child against infinite "
                  "width, so the MaxWidth box is laid out at the left edge and "
                  "any alignment on it is ignored; add "
                  'HorizontalScrollMode="Disabled" (or drop the MaxWidth)')

    return errors, checked, len(files)


def main():
    files = list(xaml_files())
    errors, checked, n_files = check(files)

    for error in errors:
        print(f"ERROR: {error}", file=sys.stderr)
    if errors:
        return 1

    print(f"OK: {n_files} XAML file(s), {checked} ScrollViewer child pair(s), "
          f"no MaxWidth column measured against infinite width")
    return 0


if __name__ == "__main__":
    sys.exit(main())
