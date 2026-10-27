#!/usr/bin/env python3
"""Copy the hand-written Chinese titles and help into the language files.

The English side of MpvConfOptions/labels.hand.json goes in through
tools/gen-schema-options.py, which owns the generated rows' English text. The
Chinese side has no such owner -- the language files are hand-maintained -- so it
is applied here, from the same file, to keep one place to write a label.

Only zh-CN is written. The other languages have no generated-row strings at all
and fall back to the English ones, which is the existing arrangement; copying
simplified Chinese into zh-TW would be a guess, not a translation.

Keys for rows that no longer exist are dropped on the way through. This script
only ever wrote additions, so a removed option used to leave its
SchemaLabel_/SchemaDesc_ pair behind in zh-CN.json forever -- 156 pairs of them
by 2026-10-11, from the options the generator's deny list had taken out. The
authority for what may exist is AppLang.Gen.cs: it is the only other file that
references these keys, so any Schema* key it does not declare is stale by
definition.

    python tools/apply-hand-labels.py
    python tools/apply-hand-labels.py --check
"""
import argparse
import json
import pathlib
import re
import sys

REPO = pathlib.Path(__file__).resolve().parent.parent
APP = REPO / "mpv-winui" / "mpv-winui"
HAND = APP / "MpvConfOptions" / "labels.hand.json"
GEN_LANG = APP / "Lexicon" / "Language" / "AppLang.Gen.cs"
TARGETS = [APP / "Languages" / "zh-CN.json"]


def prop_of(name: str) -> str:
    return "Mpv_" + re.sub(r"[^0-9A-Za-z]", "_", name)


def live_keys() -> set[str]:
    """Every Schema* key the generated AppLang declares."""
    text = GEN_LANG.read_text(encoding="utf-8-sig")
    return set(re.findall(r"public string (Schema(?:Label|Desc)_\w+)", text))


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    hand = json.loads(HAND.read_text(encoding="utf-8"))
    hand = {k: v for k, v in hand.items() if not k.startswith("_")}
    live = live_keys()

    changed = 0
    for path in TARGETS:
        data = json.loads(path.read_text(encoding="utf-8"))
        before = json.dumps(data, ensure_ascii=False, sort_keys=True)
        for key in [k for k in data
                    if k.startswith(("SchemaLabel_", "SchemaDesc_")) and k not in live]:
            del data[key]
        for name, entry in hand.items():
            prop = prop_of(name)
            if "zh" in entry:
                data[f"SchemaLabel_{prop}"] = entry["zh"]
            if "zhDesc" in entry:
                data[f"SchemaDesc_{prop}"] = entry["zhDesc"]
        after = json.dumps(data, ensure_ascii=False, sort_keys=True)
        if before != after:
            changed += 1
            if not args.check:
                path.write_text(
                    json.dumps(data, ensure_ascii=False, indent=2) + "\n",
                    encoding="utf-8",
                )
        print(f"{path.name}: {'updated' if before != after else 'up to date'}")

    if args.check and changed:
        print(f"FAIL: {changed} file(s) stale")
        return 1
    print(f"OK: {len(hand)} hand-written label(s) applied")
    return 0


if __name__ == "__main__":
    sys.exit(main())
