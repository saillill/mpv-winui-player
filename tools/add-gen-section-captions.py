#!/usr/bin/env python3
"""Insert the schema-generated section captions into every language file.

The 9 generated sections are declared in SettingsSections.cs and their
captions live in AppLang.Sections.Gen.cs, but the page reads captions from
Languages/<lang>.json at runtime -- so a section whose key is absent from a
language file renders as an empty card header. check-sections.py treats that
as an error on purpose: "declared in the catalog, absent from a language" is
the shape of a section that was renamed on one side only.

This script is the mechanical half of adding them: it puts each key in
alphabetical position (the order the files already use) with the text from
the generated partial.

Language coverage
-----------------
Only zh-CN and zh-TW get the Chinese caption and description that the
generator wrote. The other languages get the English text, which is what
they fall back to today for any untranslated key -- and it keeps
check-localization.py's completeness number honest, because a value equal to
en-US counts as residue. Translating them properly is a separate pass; this
script only makes the keys exist.

Idempotent: a key that is already present is left exactly as it is, so a
real translation is never overwritten by a rerun.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
APP = REPO / "mpv-winui" / "mpv-winui"
LANGUAGES = APP / "Languages"
GEN_SECTIONS = APP / "Modules" / "Language" / "AppLang.Sections.Gen.cs"

# Which files take the generated Chinese text. The generator's captions are
# written in Simplified; zh-TW is mapped through the same table because the
# two share the vocabulary here (视频/字幕/音频/轨道) and a wrong-glyph
# caption is better than an empty header -- but it is called out so a proper
# zh-TW pass knows to revisit it.
CHINESE_FILES = {"zh-CN.json", "zh-TW.json"}


def parse_generated() -> dict[str, str]:
    """Property -> literal from AppLang.Sections.Gen.cs."""
    text = GEN_SECTIONS.read_text(encoding="utf-8-sig")
    return dict(re.findall(
        r'public string (Section(?:Desc)?Gen\w+) \{ get; set; \} = "([^"]*)";',
        text))


def english_text(prop: str) -> str:
    """English caption/description for a generated section property.

    Derived from the property name rather than a second table: the names are
    "SectionGen" + the schema group with the punctuation dropped, so
    SectionGenVideoRendering -> "Video rendering". A description gets the
    same words with a suffix, which is what the zh source says too.
    """
    stem = prop.removeprefix("SectionDesc").removeprefix("Section")
    stem = stem.removeprefix("Gen")
    words = re.sub(r"(?<!^)(?=[A-Z])", " ", stem)
    return words if not prop.startswith("SectionDesc") else f"{words} options"


def main() -> int:
    gen = parse_generated()
    if not gen:
        print("ERROR: no generated section captions found", file=sys.stderr)
        return 2

    # Sorted by key so the inserts land in the files' own alphabetical order.
    props = sorted(gen)
    total = 0
    for path in sorted(LANGUAGES.glob("*.json")):
        raw = path.read_text(encoding="utf-8-sig")
        data = json.loads(raw)
        added: list[str] = []
        merging: dict[str, str] = {}
        for prop in props:
            if prop in data:
                continue
            if path.name in CHINESE_FILES:
                merging[prop] = gen[prop]
            else:
                merging[prop] = english_text(prop)

        if not merging:
            print(f"{path.name}: up to date")
            continue

        # Rebuild in key order rather than appending: the files are sorted,
        # and an unsorted tail would show up as churn on the next diff.
        merged = dict(sorted({**data, **merging}.items()))
        path.write_text(
            json.dumps(merged, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
            newline="\n",
        )
        added = sorted(merging)
        total += len(added)
        print(f"{path.name}: +{len(added)} keys")

    print(f"inserted {total} caption key(s) across"
          f" {len(list(LANGUAGES.glob('*.json')))} files")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
