#!/usr/bin/env python3
"""Find catalog sections that no option files under, and option sections with no row.

check-sections.py proves the catalog (SettingsSections) and AppLang agree. It
cannot see whether a section actually carries anything: a section row with a
caption and an icon that nothing files under is dead weight and renders as an
empty bucket in the customize tree.

The authoritative mapping is `sectionMap` in SettingsPage.Options.cs:

    [nameof(AppSettings.Speed)] = sPlayback,

keyed by option key and valued by a local alias for an AppLang caption. The
aliases are declared in the same file (`var sPlayback = AppContext.AppLang...`).

A handful of options bypass the map and set the section inline
(`Section = sProgramTesting`), so both paths have to be counted or those
sections look empty.

Anything filed under a section whose caption has no catalog row is the opposite
bug -- it would have no icon and no description.

Report only; exit code 0.
"""

import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
SRC = REPO / "src" / "mpvw-app"
CATALOG = SRC / "Domains" / "Settings" / "Layout" / "SettingsSections.cs"
PAGE = SRC / "Domains" / "Settings" / "SettingsPage.Options.cs"
# The schema-generated rows register their sections here instead of in PAGE.
# See generated_sections() for why this file has to be read.
GEN_SECTIONS = SRC / "Domains" / "Settings" / "SettingsPage.Sections.Gen.cs"

ALIAS = re.compile(
    r'var\s+(s[A-Za-z0-9_]+)\s*=\s*AppContext\.AppLang\.(Section[A-Za-z0-9]+)')
# [nameof(AppSettings.Foo)] = sBar,   /  ["Shortcut:whatever"] = sBar,
ENTRY = re.compile(
    r'\[(?:nameof\([A-Za-z0-9_.]+\)|"[^"]*")\]\s*=\s*([A-Za-z0-9_]+)')
# Inline escape hatch: `Section = sBar,` on an Option initialiser. Guarded so
# `option.Section = ...` and `AdvancedSection = ...` do not match.
INLINE = re.compile(r'(?<![\w.])Section\s*=\s*([A-Za-z0-9_]+)')
# The generated half names the AppLang property directly rather than through a
# local alias: `sectionMap[nameof(...)] = lang.SectionGenAudio;`.
GEN_REF = re.compile(r'lang\.(Section[A-Za-z0-9]+)')


def generated_sections() -> dict[str, int]:
    """Sections registered by RegisterGeneratedSections(), by AppLang name.

    This file had the same blind spot the other checkers did: it reads the
    sectionMap literal in SettingsPage.Options.cs and nothing else, so the 13
    sections the generator registers were invisible and every one of them was
    reported as EMPTY -- "in catalog, no option files under it". They are in
    fact the *only* sections the 362 generated rows use, so the report was
    exactly inverted: the sections holding over half the tree were the ones
    being called empty.

    Read from the AppLang property name (SectionGenAudio) rather than a local
    variable, because that is the form the catalog compares in. Matching a
    variable name here would also pick up `var` declarations that do not exist
    in generated output.
    """
    if not GEN_SECTIONS.exists():
        # A checkout without the generated files is a legitimate state: the
        # pages that need them do not build either. Not an error.
        return {}
    text = GEN_SECTIONS.read_text(encoding="utf-8-sig")
    counts: dict[str, int] = {}
    for name in GEN_REF.findall(text):
        counts[name] = counts.get(name, 0) + 1
    return counts


def main():
    catalog = re.findall(
        r'new\("(Section[A-Za-z0-9]+)"',
        CATALOG.read_text(encoding="utf-8-sig"))

    page = PAGE.read_text(encoding="utf-8-sig")
    aliases = dict(ALIAS.findall(page))

    # Scope to the dictionary literal so unrelated `[x] = y` elsewhere is ignored.
    start = page.index("var sectionMap")
    end = page.index("\n        };", start)
    block = page[start:end]

    used = {}
    unresolved = []
    for m in ENTRY.finditer(block):
        rhs = m.group(1)
        name = aliases.get(rhs)
        if name is None:
            unresolved.append(rhs)
        else:
            used[name] = used.get(name, 0) + 1

    # The generated half registers its sections in its own file, in one call
    # from BuildSettings(). Union them in: the two halves are one map at
    # runtime, and a check that knew only about the hand-written half reported
    # every generated section as empty. See generated_sections().
    generated = generated_sections()
    for name, count in generated.items():
        used[name] = used.get(name, 0) + count

    # Options that set their section inline instead of through the map.
    inline_hits = 0
    for other in sorted((SRC / "Domains" / "Settings").glob("SettingsPage.Options*.cs")):
        body = other.read_text(encoding="utf-8-sig")
        for m in INLINE.finditer(body):
            name = aliases.get(m.group(1))
            if name:
                used[name] = used.get(name, 0) + 1
                inline_hits += 1

    empty = [i for i in catalog if i not in used]
    orphan = [u for u in used if u not in catalog]

    print(f"aliases declared        : {len(aliases)}")
    print(f"catalog sections        : {len(catalog)}")
    print(f"sectionMap entries      : {sum(used.values()) - inline_hits}")
    print(f"inline Section= sites   : {inline_hits}")
    print(f"distinct sections used  : {len(used)}")
    print()
    if unresolved:
        print(f"UNRESOLVED ({len(unresolved)}) - value is not a known alias:")
        for u in sorted(set(unresolved)):
            print(f"  {u}")
        print()
    if empty:
        print(f"EMPTY ({len(empty)}) - in catalog, no option files under it:")
        for i in empty:
            print(f"  {i}")
    else:
        print("EMPTY: none")
    print()
    if orphan:
        print(f"ORPHAN ({len(orphan)}) - option files under it, no catalog row:")
        for o in orphan:
            print(f"  {o}  ({used[o]} options)")
    else:
        print("ORPHAN: none")
    print()
    print("options per section:")
    for name, n in sorted(used.items(), key=lambda kv: -kv[1]):
        flag = "" if name in catalog else "   <-- ORPHAN"
        print(f"  {n:4}  {name}{flag}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
