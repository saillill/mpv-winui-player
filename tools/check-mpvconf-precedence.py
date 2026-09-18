#!/usr/bin/env python3
"""Check the mpv.conf precedence plumbing stays consistent.

Three things have to line up for a settings row to correctly report "mpv.conf
overrides this", and each one can drift silently:

  1. `MpvSettings.ToCommand` decides what mpv option a settings key writes.
  2. `MpvSettings.MpvOptionNames` is the reverse map the override check reads.
     A key present in one and missing from the other means the row can never
     report being overridden - the bug this tool exists to catch.
  3. Every key in that map must be a real AppSettings property, or the entry is
     dead weight left behind by a rename.

Run from the repository root:  python tools/check-mpvconf-precedence.py
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SETTINGS = ROOT / "mpv-winui/mpv-winui/Modules/Settings/MpvSettings.cs"

# "set <name>" / "no-osd set <name>", the only shapes that name an mpv option.
SET_NAME = re.compile(r"set ([a-z0-9][a-z0-9/-]*)\s")
CASE = re.compile(r"nameof\(AppSettings\.(\w+)\)\s*=>\s*(.+?),\n", re.S)
TABLE_ENTRY = re.compile(r'\[nameof\(AppSettings\.(\w+)\)\]\s*=\s*"([^"]+)"')
# AppSettings has two declaration shapes for one property:
#     public string Hwdec          <- name on the line after the modifier
#     {
# and the compact one-liner:
#     public bool AdLavcDownmix { get => ...; set => ...; }
# Both have to be matched, or a whole class of keys reads as renamed-away.
PROP_BLOCK = re.compile(r"^\s*public\s+(?!partial\s+class)[\w<>?\[\]]+\s+(\w+)\s*$", re.M)
PROP_INLINE = re.compile(r"^\s*public\s+(?!partial\s+class)[\w<>?\[\]]+\s+(\w+)\s*\{\s*get\b", re.M)


def read() -> str:
    if not SETTINGS.exists():
        sys.exit(f"ERROR: {SETTINGS} not found - run from the repository root")
    return SETTINGS.read_text(encoding="utf-8")


def to_command_map(src: str) -> dict[str, str]:
    start = src.index("var cmd = key switch")
    end = src.index("_ => null,\n        };", start)
    body = src[start:end]

    result: dict[str, str] = {}
    for match in CASE.finditer(body):
        key, expr = match.group(1), match.group(2)
        name = SET_NAME.search(expr)
        if name:
            result[key] = name.group(1)
    return result


def declared_map(src: str) -> dict[str, str]:
    start = src.index("MpvOptionNames = new")
    end = src.index("};", start)
    return dict(TABLE_ENTRY.findall(src[start:end]))


def appsettings_properties() -> set[str]:
    """Property names across the partial AppSettings files, plus the interfaces."""
    names: set[str] = set()
    for path in (ROOT / "mpv-winui/mpv-winui/Modules/Settings").glob("AppSettings*.cs"):
        text = path.read_text(encoding="utf-8")
        names.update(PROP_BLOCK.findall(text))
        names.update(PROP_INLINE.findall(text))
    return names


def main() -> int:
    src = read()
    live = to_command_map(src)
    table = declared_map(src)
    props = appsettings_properties()

    problems: list[str] = []

    missing = sorted(set(live) - set(table))
    if missing:
        problems.append(
            f"{len(missing)} key(s) write an mpv option but are absent from "
            f"MpvOptionNames, so they can never report being overridden: "
            + ", ".join(missing)
        )

    stale = sorted(set(table) - set(live))
    if stale:
        problems.append(
            f"{len(stale)} key(s) are in MpvOptionNames but no longer write an mpv "
            f"option: " + ", ".join(stale)
        )

    unknown = sorted(set(table) - props)
    if unknown:
        problems.append(
            f"{len(unknown)} key(s) are not AppSettings properties (renamed or "
            f"removed?): " + ", ".join(unknown)
        )

    mismatched = sorted(
        key for key in set(live) & set(table) if live[key] != table[key]
    )
    if mismatched:
        for key in mismatched:
            problems.append(
                f"{key}: ToCommand writes '{live[key]}' but the map says '{table[key]}'"
            )

    print(f"settings keys writing an mpv option : {len(live)}")
    print(f"MpvOptionNames entries              : {len(table)}")
    print(f"AppSettings properties              : {len(props)}")

    if problems:
        print()
        for problem in problems:
            print(f"ERROR: {problem}")
        return 1

    print()
    print("OK: the option-name map matches ToCommand exactly")
    return 0


if __name__ == "__main__":
    sys.exit(main())
