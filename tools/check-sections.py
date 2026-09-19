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
  * every SettingsCategory* property has a row, and vice versa;
  * every language file carries a real translation for every section and
    category caption -- see below;
  * repeated option sort keys are surfaced as warnings, because optionOrder is
    deliberately both an ordering key and a grouping label.

Icon collisions are reported as warnings rather than errors: a section card is
scanned by its glyph, so two sections sharing one is a real readability
problem, but it is a design choice rather than a broken invariant. Three pairs
shared a glyph when the table was introduced.

Why the translations are checked here and not in check-localization.py: that
script asserts every AppLang property has a key in every file, which a key
whose value is still the English source satisfies. Section captions are the
one family where "translated" is not optional -- they are the tree's
navigation, and the table above is useless if the caption rendered next to the
icon is in the wrong language. Residual-English is reported as a warning (a
caption that is genuinely the same word in two languages is not a defect), a
missing key as an error.

The residual-English warnings currently emitted are all of the harmless kind
and should stay: "Direct3D 11" is a product name, "HTTP" is a protocol, and
"OSD" is left untranslated in Chinese, Japanese and Korean interfaces too. Do
not "fix" those by inventing translations.

Exit code 1 on errors.
"""

import json
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
SRC = REPO / "mpv-winui" / "mpv-winui"

APPLANG = SRC / "Modules" / "Language" / "AppLang.cs"
CATALOG = SRC / "Modules" / "Settings" / "Layout" / "SettingsSections.cs"
LANGUAGES = SRC / "Languages"
OPTIONS = (SRC / "Modules" / "Settings" / "SettingsPage.Options.cs",
           SRC / "Modules" / "Settings" / "SettingsPage.Options.Program.cs")


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


def caption_translations():
    """Load every language file as {filename: parsed-json-or-exception}.

    Parsing failures are captured rather than raised so the caller can report
    them alongside the other problems instead of dying on the first bad file.
    """
    files = sorted(LANGUAGES.glob("*.json"))
    loaded = {}
    for path in files:
        try:
            loaded[path.name] = json.loads(path.read_text(encoding="utf-8-sig"))
        except json.JSONDecodeError as exc:
            loaded[path.name] = exc
    return loaded


def option_sort_keys():
    """The [nameof(AppSettings.X)] = N entries from every options file.

    Returned as {value: [names]}, because repeats are meaningful rather than
    accidental -- see the sort-key section of main().
    """
    keys = {}
    for path in OPTIONS:
        if not path.exists():
            continue
        text = path.read_text(encoding="utf-8-sig")
        for name, value in re.findall(
                r'\[nameof\(AppSettings\.(\w+)\)\]\s*=\s*(\d+),', text):
            keys.setdefault(int(value), []).append(name)
    return keys


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

    # --- caption translations ------------------------------------------------
    # Every section caption, every section description and every category
    # caption must exist in every language file. Values equal to the en-US text
    # are a warning, not an error.
    caption_props = sorted(row_names | row_descs | cat_names)
    files = caption_translations()
    if not files:
        errors.append(f"no language files found under {LANGUAGES}")
    baseline = files.get("en-US.json", {})
    if isinstance(baseline, Exception):
        errors.append(f"en-US.json is not valid JSON: {baseline}")
        baseline = {}
    for name, data in sorted(files.items()):
        if isinstance(data, Exception):
            errors.append(f"{name} is not valid JSON: {data}")
            continue
        missing = [p for p in caption_props if p not in data]
        if missing:
            errors.append(f"{name} is missing {len(missing)} caption key(s): "
                          f"{', '.join(missing[:5])}"
                          + (" ..." if len(missing) > 5 else ""))
        if name == "en-US.json":
            continue
        untranslated = [p for p in caption_props
                        if p in data and data[p] == baseline.get(p)]
        if untranslated:
            warnings.append(f"{name}: {len(untranslated)} caption(s) still in English: "
                            f"{', '.join(untranslated[:5])}"
                            + (" ..." if len(untranslated) > 5 else ""))

    # --- sort keys -----------------------------------------------------------
    # A repeated sort key is NOT an error here. optionOrder is deliberately
    # overloaded: it is both the ordering key and a grouping label. The PiP
    # controls share 193 on purpose (WindowPiP, WindowPiPShowControls,
    # WindowPiPShowTopButtons), and the Window block shares 194-196 the same
    # way. LINQ's OrderBy is stable, so equal keys keep their source order --
    # which is exactly the intended "these belong together, in written order".
    #
    # The real hazard is different: a duplicate makes the on-screen order
    # depend on where the lines physically sit in the file, so reordering the
    # file silently reorders the UI. That is worth a warning, not a failure.
    sort_keys = option_sort_keys()
    for value, names in sorted(sort_keys.items()):
        if len(names) > 1:
            warnings.append(f"sort key {value} is shared by {len(names)} options "
                            f"({', '.join(sorted(names)[:4])}) -- they keep source "
                            f"order, so moving those lines reorders the UI")

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
          f"{len(by_icon)} distinct icons, {shared} shared, "
          f"{len(sort_keys)} sort keys, {len(files)} language files")
    return 0


if __name__ == "__main__":
    sys.exit(main())
