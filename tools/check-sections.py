#!/usr/bin/env python3
"""Check the settings section catalog against AppLang.

A settings section used to be identified by its localized caption. Three
places worked around that independently: SectionMeta() compared the caption
against 51 hardcoded branches for an icon and a description, this project's
layout code reflected over the AppLang property names to derive a stable id,
and the persisted layout stored that id. SettingsSections now holds all of it
in one table, so the caption, the icon and the description cannot drift apart.

That only holds if the table and AppLang agree, which is what this checks:

  * every Section* property in AppLang (excluding the SectionDesc* ones) has
    exactly one row in the table, and no row names a property that is gone;
  * every row's description accessor is the "SectionDesc" property that
    belongs to its section;
  * ids are unique;
  * every SettingsCategory* property has a row, and vice versa.

Icon collisions are reported as warnings rather than errors: a section card is
scanned by its glyph, so two sections sharing one is a real readability
problem, but it is a design choice rather than a broken invariant. Three pairs
shared a glyph when the table was introduced.

Exit code 1 on errors.
"""

import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
SRC = REPO / "mpv-winui" / "mpv-winui"

APPLANG = SRC / "Modules" / "Language" / "AppLang.cs"
CATALOG = SRC / "Modules" / "Settings" / "Layout" / "SettingsSections.cs"


def applang_props():
    text = APPLANG.read_text(encoding="utf-8-sig")
    names = re.findall(r'public string ([A-Za-z0-9_]+) \{ get; set; \}', text)
    sections = {n for n in names
                if n.startswith("Section") and not n.startswith("SectionDesc")}
    descs = {n for n in names if n.startswith("SectionDesc")}
    categories = {n for n in names if n.startswith("SettingsCategory")}
    return sections, descs, categories


def catalog_rows():
    text = CATALOG.read_text(encoding="utf-8-sig")
    rows = re.findall(
        r'new\("(Section[A-Za-z0-9]+)",\s*"(\\u[0-9A-Fa-f]{4})",'
        r'\s*l => l\.(Section[A-Za-z0-9]+),\s*l => l\.(SectionDesc[A-Za-z0-9]+)\)',
        text)
    cats = re.findall(
        r'new\("([a-z]+)",\s*l => l\.(SettingsCategory[A-Za-z0-9]+)\)', text)
    return rows, cats


def main():
    sections, descs, categories = applang_props()
    rows, cats = catalog_rows()

    errors = []
    warnings = []

    ids = [r[0] for r in rows]
    for dup in sorted({i for i in ids if ids.count(i) > 1}):
        errors.append(f"duplicate section id: {dup}")

    row_names = {r[2] for r in rows}
    for name in sorted(sections - row_names):
        errors.append(f"AppLang.{name} has no row in SettingsSections")
    for name in sorted(row_names - sections):
        errors.append(f"SettingsSections names {name}, which AppLang does not declare")

    row_descs = {r[3] for r in rows}
    for name in sorted(descs - row_descs):
        errors.append(f"AppLang.{name} is never used as a description")
    for name in sorted(row_descs - descs):
        errors.append(f"SettingsSections uses {name}, which AppLang does not declare")

    for _, _, cap, desc in rows:
        expected = "SectionDesc" + cap[len("Section"):]
        if desc != expected:
            errors.append(f"{cap} is paired with {desc}, expected {expected}")

    cat_names = {c[1] for c in cats}
    for name in sorted(categories - cat_names):
        errors.append(f"AppLang.{name} has no row in SettingsSections.Categories")
    for name in sorted(cat_names - categories):
        errors.append(f"SettingsSections.Categories names {name}, which AppLang does not declare")

    keys = [c[0] for c in cats]
    for dup in sorted({k for k in keys if keys.count(k) > 1}):
        errors.append(f"duplicate category key: {dup}")

    by_icon = {}
    for _, icon, cap, _ in rows:
        by_icon.setdefault(icon, []).append(cap)
    for icon, caps in sorted(by_icon.items()):
        if len(caps) > 1:
            warnings.append(f"{icon} is shared by {', '.join(sorted(caps))}")

    for warning in warnings:
        print(f"WARNING: {warning}")
    for error in errors:
        print(f"ERROR: {error}", file=sys.stderr)
    if errors:
        return 1

    shared = sum(1 for caps in by_icon.values() if len(caps) > 1)
    print(f"OK: {len(rows)} sections, {len(cats)} categories, "
          f"{len(by_icon)} distinct icons, {shared} shared")
    return 0


if __name__ == "__main__":
    sys.exit(main())
