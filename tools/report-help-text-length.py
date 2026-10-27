#!/usr/bin/env python3
"""Report the longest help / description strings in the zh-CN language file.

Read-only. Prints a length-sorted table so the wording can be trimmed by
hand, and flags the ones long enough that a two-line description cannot
hold them at any reasonable window width.

Key groups of interest:
  SettingsHelp*   -- per-option help text under each row
  SectionDesc*    -- the one-liner under a section card / section header
  *Description*   -- a few option descriptions declared inline
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
LANG = REPO / "mpv-winui" / "mpv-winui" / "Languages" / "zh-CN.json"
APP = REPO / "mpv-winui" / "mpv-winui" / "Lexicon" / "Language" / "AppLang.cs"

# Groups whose text actually shows up beside an option control.
GROUPS = (
    ("SettingsHelp", re.compile(r"^SettingsHelp")),
    ("SectionDesc", re.compile(r"^SectionDesc")),
    ("OtherHelp", re.compile(r"(Help|Description|Desc)$")),
)


def load() -> dict[str, str]:
    if not LANG.exists():
        sys.exit(f"missing language file: {LANG}")
    data = json.loads(LANG.read_text(encoding="utf-8-sig"))
    return {k: v for k, v in data.items() if isinstance(v, str) and v.strip()}


def bucket(key: str) -> str | None:
    for name, pat in GROUPS:
        if pat.search(key):
            return name
    return None


def main() -> None:
    strings = load()
    print(f"language file : {LANG.relative_to(REPO)}")
    print(f"total entries : {len(strings)}")
    print()

    rows: dict[str, list[tuple[str, str]]] = {name: [] for name, _ in GROUPS}
    for key, val in strings.items():
        name = bucket(key)
        if name:
            rows[name].append((key, val))

    for name, _pat in GROUPS:
        items = sorted(rows[name], key=lambda kv: (-len(kv[1]), kv[0]))
        if not items:
            continue
        lens = [len(v) for _k, v in items]
        avg = sum(lens) / len(lens)
        over = [kv for kv in items if len(kv[1]) > 40]
        print(f"=== {name} === count={len(items)} avg={avg:.1f} max={lens[0]}")
        print(f"    >40 chars: {len(over)}")
        for key, val in items[:25]:
            mark = "  <<<" if len(val) > 40 else ""
            print(f"    [{len(val):3d}] {key}")
            print(f"          {val}{mark}")
        print()

    # Cross-check: every SettingsHelp* key present in AppLang.cs but absent
    # from the language file would render as the literal key in the UI.
    if APP.exists():
        src = APP.read_text(encoding="utf-8-sig")
        declared = set(re.findall(r'"((?:SettingsHelp|SectionDesc)\w+)"', src))
        have = set(strings)
        missing = sorted(declared - have)
        print(f"=== declared in AppLang.cs but missing zh-CN: {len(missing)} ===")
        for key in missing[:20]:
            print(f"    {key}")


if __name__ == "__main__":
    main()
