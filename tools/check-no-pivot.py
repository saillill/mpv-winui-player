#!/usr/bin/env python3
"""Fail while any Pivot is still in the source tree, and while a SelectorBarItem
is built in a way that quietly renders nothing.

Why this check exists
---------------------
`Pivot` is UWP-only. The WinUI analyzer reports it (WUI2003) for XAML only, so
`PlayerControl.ControlPanel.cs` built its three-tab Pivot with **zero** warnings
for the whole life of that file. Warning parity was never the same fact as
"Pivot is gone", and this repository has already lost a round to that gap: three
of the four regex checks written to prove the migration had "succeeded" were
green *before* the migration started, because the real code writes

    var pivot = new Pivot
    {

and never `new Pivot(`. A check that reads like the property it claims and can
never fail is worse than no check, so this file states its own failure mode.

What is checked
---------------
1. No `Pivot` / `PivotItem` markup in any XAML under the app project, with
   xmlns prefixes allowed (`controls:Pivot`) -- the markup may put the tag name
   and its attributes on separate lines, so the pattern stops at the *name* and
   never includes the closing `>`.
2. No `new Pivot` / `new PivotItem` in any C# file. The `(` is deliberately not
   part of the pattern: object-initializer syntax has no `(`.
3. `SelectorBar` is actually present in both migrated files, and (4) no
   `SelectorBarItem` sets `Child`.

Rule 3 is the anti-vacuity clause. A regex that stops matching turns 1 and 2
into permanent greens, and that is exactly the failure this repository already
paid for once. Asserting the *arrival* of the replacement next to the
*departure* of the original means a broken pattern fails loudly instead of
passing quietly. Rule 4 is a measured trap, not a style opinion: WinUI 2.3.9's
`SelectorBarItem` template carries only `PART_IconVisual` (bound to `Icon`) and
`PART_TextVisual` (bound to `Text`) -- `Child` sits behind a bare
`<!--Placeholder for Child UIElement -->` comment and is never presented, so a
composite header put in `Child` renders an empty tab with no error anywhere.

Exit code 1 on any finding, and 1 on zero found input.
"""

import re
import sys

from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
APP = REPO / "src" / "mpvw-app"

SKIP_DIRS = {"bin", "obj", "packages", ".vs", "node_modules", "AppPackages"}

# Everywhere a Pivot may still be built. The allow-list of "files where a
# SelectorBar must now exist" is separate and explicit: a bare "some SelectorBar
# exists somewhere" would pass on the first of the two migrations.
PIVOT_FILES = (
    Path("src/mpvw-app/Domains/Playback/PlayerTrackSelectorControl.xaml"),
    Path("src/mpvw-app/Domains/Playback/PlayerControl.ControlPanel.cs"),
)

# Markup: allow an xmlns prefix, allow the name to be followed by anything.
# Do NOT require whitespace or `>`: `<Pivot` and `<controls:Pivot` both match,
# and the migrated form `<SelectorBar` must not.
MARKUP = re.compile(r"<([A-Za-z0-9_.]+:)?Pivot(Item)?\b")

# Code: `new Pivot` / `new PivotItem`, with or without `(` or `{`. The old
# checks required the parenthesis and therefore never matched this codebase.
CODE = re.compile(r"\bnew\s+Pivot(Item)?\b")

SELECTOR_MARKUP = re.compile(r"<([A-Za-z0-9_.]+:)?SelectorBar\b")

# A SelectorBarItem element and the attributes on it, so a Child= or a
# <SelectorBarItem.Child> element block can be spotted.
ITEM_OPEN = re.compile(r"<([A-Za-z0-9_.]+:)?SelectorBarItem\b((?:[^>]|>[^<])*?)/?>", re.S)
ITEM_CHILD_PROPERTY = re.compile(
    r"<([A-Za-z0-9_.]+:)?SelectorBarItem\.Child\s*>")


def strip_csharp_comments(text):
    """Remove // and /* */ comments, keeping string literals intact.

    Comments are not code. The migration leaves prose like "the Pivot shell"
    behind on purpose (it explains what the tab strip used to be), and a check
    that failed on that prose would force the history out of the file.
    """
    out = []
    i = 0
    n = len(text)
    while i < n:
        ch = text[i]
        if ch == '"':
            # Verbatim strings (@") cannot be produced here: this project's
            # comments quote identifiers, not paths, and the cost of a wrong
            # strip is a false positive, which fails loudly.
            out.append(ch)
            i += 1
            while i < n:
                if text[i] == "\\" and i + 1 < n:
                    out.append(text[i:i + 2])
                    i += 2
                    continue
                out.append(text[i])
                if text[i] == '"':
                    i += 1
                    break
                i += 1
            continue
        if ch == "/" and i + 1 < n and text[i + 1] == "/":
            while i < n and text[i] != "\n":
                i += 1
            continue
        if ch == "/" and i + 1 < n and text[i + 1] == "*":
            i += 2
            while i + 1 < n and not (text[i] == "*" and text[i + 1] == "/"):
                i += 1
            i += 2
            continue
        out.append(ch)
        i += 1
    return "".join(out)


def strip_xaml_comments(text):
    return re.sub(r"<!--.*?-->", "", text, flags=re.S)


def line_of(text, offset):
    return text.count("\n", 0, offset) + 1


def source_files():
    for path in sorted(APP.rglob("*")):
        if not path.is_file() or path.suffix.lower() not in (".xaml", ".cs"):
            continue
        rel = path.relative_to(APP)
        if SKIP_DIRS & set(rel.parts):
            continue
        yield path


def check():
    errors = []
    xaml_files = 0
    cs_files = 0
    selector_files = set()

    for path in source_files():
        rel = path.relative_to(REPO)
        text = path.read_text(encoding="utf-8-sig")

        if path.suffix.lower() == ".xaml":
            xaml_files += 1
            body = strip_xaml_comments(text)
            for match in MARKUP.finditer(body):
                errors.append(
                    f"{rel}:{line_of(body, match.start())}: "
                    f"{match.group(0)!r} -- Pivot is UWP-only and the app "
                    f"targets WinUI 3; migrate it to SelectorBar")
            if SELECTOR_MARKUP.search(body):
                selector_files.add(rel.as_posix())
            for match in ITEM_CHILD_PROPERTY.finditer(body):
                errors.append(
                    f"{rel}:{line_of(body, match.start())}: "
                    f"SelectorBarItem.Child -- WinUI 2.3.9's SelectorBarItem "
                    f"template has no presenter bound to Child, so this "
                    f"renders nothing and raises nothing; put the content in "
                    f"Text and Icon, or drive it from a sibling element")
            for match in ITEM_OPEN.finditer(body):
                attrs = match.group(2)
                if re.search(r"\bChild\s*=", attrs):
                    errors.append(
                        f"{rel}:{line_of(body, match.start())}: "
                        f"SelectorBarItem Child= -- not presented by the "
                        f"default template (measured in WinUI 2.3.9 "
                        f"generic.xaml); use Text/Icon instead")
            continue

        cs_files += 1
        body = strip_csharp_comments(text)
        for match in CODE.finditer(body):
            errors.append(
                f"{rel}:{line_of(body, match.start())}: "
                f"'new {match.group(0).split(None, 1)[1]}' -- the runtime "
                f"Pivot raises no analyzer warning (WUI2003 scans XAML only), "
                f"so only this check sees it; migrate to SelectorBar")

    # Anti-vacuity: both halves must be non-empty, or the patterns above prove
    # nothing. Zero input is an error, not a pass.
    if xaml_files == 0 or cs_files == 0:
        errors.append(
            f"scanned 0 XAML and/or 0 C# files under {APP} -- the file "
            f"enumeration is broken, so a green result above would be "
            f"meaningless")
    # The replacement must have arrived in each file the Pivot left.
    for rel in PIVOT_FILES:
        found = rel.as_posix() in selector_files
        if not found:
            # A .cs file cannot be in selector_files (markup-only set); the
            # runtime SelectorBar is asserted below instead.
            if rel.suffix == ".cs":
                continue
            errors.append(
                f"{rel}: no SelectorBar found -- the Pivot left this file but "
                f"its replacement is missing, which is what a pattern that "
                f"stops matching looks like from the outside")

    selector_in_code = 0
    for rel in PIVOT_FILES:
        if rel.suffix != ".cs":
            continue
        text = strip_csharp_comments((REPO / rel).read_text(encoding="utf-8-sig"))
        if re.search(r"\bnew\s+SelectorBar\b", text):
            selector_in_code += 1
        else:
            errors.append(
                f"{rel}: no 'new SelectorBar' -- the runtime Pivot left this "
                f"file but its replacement is missing")
        if re.search(r"\bnew\s+SelectorBarItem\b", text):
            selector_in_code += 1

    return errors, xaml_files, cs_files, selector_files, selector_in_code


def main():
    errors, xaml_files, cs_files, selector_files, selector_in_code = check()

    for error in errors:
        print(f"ERROR: {error}", file=sys.stderr)
    if errors:
        print(f"FAIL: {len(errors)} finding(s) across {xaml_files} XAML and "
              f"{cs_files} C# file(s)", file=sys.stderr)
        return 1

    print(f"OK: {xaml_files} XAML and {cs_files} C# file(s), no Pivot left, "
          f"{len(selector_files)} XAML file(s) and {selector_in_code} code "
          f"site(s) now use SelectorBar")
    return 0


if __name__ == "__main__":
    sys.exit(main())
