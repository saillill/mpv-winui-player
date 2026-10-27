"""Report the effective (category, section) grouping of the settings tree.

Two sources assign a section and neither is visible from reading one file:
Category is set inline on each Option, Section comes from the sectionMap in
SettingsPage.Options.cs -- except for a handful of options that declare
Section inline instead, and those inline values win.

This computes the merge and prints the per-group row count, flagging the
groups that fall outside the 3-row floor the fold rule needs. Read-only.

Usage:
    PY="C:/Users/z2637/.workbuddy/binaries/python/envs/default/Scripts/python.exe"
    "$PY" tools/report-settings-groups.py
"""

import collections
import json
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
SET = REPO / "mpv-winui" / "mpv-winui" / "Domains" / "Settings"

OPTION_FILES = [
    "SettingsPage.Options.Advanced.cs",
    "SettingsPage.Options.Audio.cs",
    "SettingsPage.Options.Playback.cs",
    "SettingsPage.Options.Program.cs",
    "SettingsPage.Options.Screenshot.cs",
    "SettingsPage.Options.Subtitles.cs",
    "SettingsPage.Options.Video.cs",
    "SettingsPage.Options.PathFolders.cs",
]

BLOCK = re.compile(r"new Option\s*\{(.*?)\n            \},", re.S)


def collect():
    rows = []
    for name in OPTION_FILES:
        src = (SET / name).read_text(encoding="utf-8-sig")
        for block in BLOCK.findall(src):
            key = re.search(
                r"Key\s*=\s*nameof\(AppContext\.AppSetting\.(\w+)\)", block)
            if not key:
                continue
            cat = re.search(r"^\s*Category\s*=\s*(\w+)", block, re.M)
            sec = re.search(r"^\s*Section\s*=\s*(\w+)", block, re.M)
            rows.append((key.group(1), cat.group(1) if cat else None,
                         sec.group(1) if sec else None, name))
    return rows


def main():
    rows = collect()
    core = (SET / "SettingsPage.Options.cs").read_text(encoding="utf-8-sig")

    map_body = re.search(
        r"var sectionMap = new Dictionary<string, string>\(StringComparer\.Ordinal\)\s*\{(.*?)\n        \};",
        core, re.S).group(1)
    section_map = dict(re.findall(
        r"\[nameof\(AppSettings\.(\w+)\)\]\s*=\s*(\w+)", map_body))

    adv_body = re.search(
        r"var advancedSections = new HashSet<string>\(StringComparer\.Ordinal\)\s*\{(.*?)\n        \};",
        core, re.S).group(1)
    folded = set(re.findall(r"\b(s[A-Z]\w+)\b", adv_body))

    pin_body = re.search(
        r"var pinnedOverviewOptions = new HashSet<string>\(StringComparer\.Ordinal\)\s*\{(.*?)\n        \};",
        core, re.S).group(1)
    pinned = set(re.findall(r"nameof\(AppSettings\.(\w+)\)", pin_body))

    # Inline Section wins over the map -- that is what the page does.
    eff = {}
    for key, cat, inline, _src in rows:
        eff[key] = (cat, inline if inline else section_map.get(key))

    groups = collections.defaultdict(list)
    for key, (cat, sec) in eff.items():
        groups[(cat, sec)].append(key)

    print(f"options           : {len(rows)}")
    print(f"sectionMap entries: {len(section_map)}")
    print(f"folded sections   : {len(folded)}")
    print(f"pinned rows       : {len(pinned)}")
    print()

    unknown = [k for k, (_c, s) in eff.items() if s is None]
    if unknown:
        print(f"!! no section: {unknown}")
        print()

    print("=== 分组（按分类）===")
    for cat in sorted({c for c, _ in groups}):
        print(f"\n[{cat}]")
        for (c, s), keys in sorted(groups.items(),
                                   key=lambda kv: (kv[0][0], -len(kv[1]))):
            if c != cat:
                continue
            is_folded = s in folded
            free = [k for k in keys if k not in pinned] if is_folded else keys
            mark = "折叠" if is_folded else "平铺"
            warn = ""
            # The rule: a folded group must present at least 3 rows behind
            # its card, since 1-2 is not worth a level of nesting.
            if is_folded and len(free) <= 2:
                warn = "  <== 折叠后只剩 %d 行，应平铺" % len(free)
            if (not is_folded) and len(keys) >= 8:
                warn = "  <== 平铺 %d 行，考虑折叠" % len(keys)
            print(f"  {str(s):26} 共{len(keys):3}  {mark}  卡后{len(free):3}{warn}")

    print()
    print("=== 需要处理的（需求 2：折叠后 <3 行）===")
    bad = [((c, s), [k for k in ks if k not in pinned])
           for (c, s), ks in groups.items()
           if s in folded and len([k for k in ks if k not in pinned]) <= 2]
    if not bad:
        print("  (无)")
    for (c, s), ks in sorted(bad, key=lambda x: (x[0][0], len(x[1]))):
        print(f"  {c:11} {s:26} {len(ks)} 行: {ks}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
