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
SRC = REPO / "mpv-winui" / "mpv-winui"
CATALOG = SRC / "Modules" / "Settings" / "Layout" / "SettingsSections.cs"
PAGE = SRC / "Modules" / "Settings" / "SettingsPage.Options.cs"

ALIAS = re.compile(
    r'var\s+(s[A-Za-z0-9_]+)\s*=\s*AppContext\.AppLang\.(Section[A-Za-z0-9]+)')
# [nameof(AppSettings.Foo)] = sBar,   /  ["Shortcut:whatever"] = sBar,
ENTRY = re.compile(
    r'\[(?:nameof\([A-Za-z0-9_.]+\)|"[^"]*")\]\s*=\s*([A-Za-z0-9_]+)')
# Inline escape hatch: `Section = sBar,` on an Option initialiser. Guarded so
# `option.Section = ...` and `AdvancedSection = ...` do not match.
INLINE = re.compile(r'(?<![\w.])Section\s*=\s*([A-Za-z0-9_]+)')


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

    # Options that set their section inline instead of through the map.
    inline_hits = 0
    for other in sorted((SRC / "Modules" / "Settings").glob("SettingsPage.Options*.cs")):
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
