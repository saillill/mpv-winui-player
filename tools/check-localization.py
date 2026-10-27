#!/usr/bin/env python3
"""Check localization consistency for mpv-winui-player.

* Every writable AppLang property must exist in each Languages/<lang>.json.
* menus.json labelKey values must exist in AppLang; action values must be in
  the whitelist parsed from MpvPlayerPage_MenuBar.cs.
* menus.json structure must match tools/menus.schema.json (validated inline,
  without a jsonschema dependency).
* Optional --xaml-audit prints hardcoded UI strings found in XAML.

Exit code 1 on errors, 2 on internal failure.
"""

import argparse
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

for stream in (sys.stdout, sys.stderr):
    try:
        stream.reconfigure(encoding="utf-8", errors="replace")
    except (AttributeError, ValueError):
        pass


def parse_applang_props(src: Path) -> set[str]:
    text = src.read_text(encoding="utf-8")
    return set(re.findall(r"public string (\w+) \{ get; set; \}", text))


def parse_applang_props_all() -> set[str]:
    """AppLang properties from the hand-written class AND every partial.

    AppLang is spread over three files and the check used to read one of them:

      * AppLang.cs               -- the hand-written vocabulary
      * AppLang.Gen.cs           -- the 724 SchemaLabel_*/SchemaDesc_* the
                                    generator emits for the 362 schema rows
      * AppLang.Sections.Gen.cs  -- the generated section captions and
                                    descriptions (SectionGen*/SectionDescGen*)

    Reading only the first had two opposite effects, and both looked like real
    findings. The 724 generated properties were reported as "extra keys" --
    language-file entries no AppLang property claims -- which is backwards. The
    26 `SectionGen*` keys were reported the same way for the same reason. And
    underneath that noise the actual defect was invisible: all nine language
    files are missing every one of the 724 generated keys.

    Globbed rather than listed, so a fourth partial cannot reintroduce the bug.

    Note this deliberately does NOT include CustomizeStrings: those keys are a
    companion table *by design* (see its class docstring -- reflection cannot
    tell an English property default from a user-set value, so the customize
    shell keeps its own surface). They still show up as extra keys, which is
    now correct and expected; the message says so.
    """
    lang_dir = ROOT / "src" / "mpvw-app" / "Lexicon" / "Language"
    props: set[str] = set()
    for path in sorted(lang_dir.glob("AppLang*.cs")):
        props |= parse_applang_props(path)
    return props


def _summarise(keys) -> str:
    """Count keys by family: SchemaLabel_Mpv_x and SchemaLabel_Mpv_y are one line.

    A family is the leading CamelCase word -- matched lazily up to the next
    capital, which is what keeps `CustomizeAddCategoryOption` from collapsing to
    its whole self and `SchemaLabel_Mpv_ad` from doing the same. Without this, a
    systematic gap (724 keys) and an incidental one (6 keys) look the same on
    the console: an unreadable wall that people learn to scroll past, which is
    how the 724 stayed invisible for a week.
    """
    counts: dict[str, int] = {}
    for key in keys:
        m = re.match(r"([A-Z][a-z]+)", key)
        family = (m.group(1) + "*") if m else key
        counts[family] = counts.get(family, 0) + 1
    return ", ".join(f"{n}x {fam}" for fam, n in
                     sorted(counts.items(), key=lambda kv: (-kv[1], kv[0])))


def parse_menu_actions(src: Path) -> set[str]:
    text = src.read_text(encoding="utf-8")
    # Actions are registered via RegisterMenuAction("id", handler) in the
    # static constructor (no more switch case list).
    return set(re.findall(r'RegisterMenuAction\("([\w-]+)"', text))


def walk_menus(items, props: set[str], actions: set[str], errors: list[str], path: str = "root"):
    for item in items:
        if item.get("separator"):
            continue
        if "children" in item:
            walk_menus(
                item["children"],
                props,
                actions,
                errors,
                path + "/" + str(item.get("id", "?")),
            )
            continue
        label = item.get("labelKey")
        if label and label not in props:
            errors.append(f"menus.json: unknown labelKey '{label}' at {path}")
        action = item.get("action")
        if action and action not in actions:
            errors.append(f"menus.json: unknown action '{action}' at {path}")
        if not action and not item.get("mpvCommand"):
            errors.append(f"menus.json: item has neither action nor mpvCommand at {path}")


def validate_menu_schema(items, errors: list[str], path: str = "root"):
    """Structural validation mirroring tools/menus.schema.json.

    Kept dependency-free (no jsonschema package): every item is either a
    separator, a submenu (children), or a leaf with id + labelKey and one of
    action/mpvCommand.
    """
    for index, item in enumerate(items):
        item_path = f"{path}[{index}]"
        if not isinstance(item, dict):
            errors.append(f"menus.json: item at {item_path} is not an object")
            continue
        if item.get("separator") is True:
            continue
        if "children" in item:
            if not isinstance(item["children"], list):
                errors.append(f"menus.json: children at {item_path} is not an array")
                continue
            if not item.get("id"):
                errors.append(f"menus.json: submenu at {item_path} has no id")
            validate_menu_schema(
                item["children"],
                errors,
                item_path + "/" + str(item.get("id", "?")),
            )
            continue
        if not item.get("id"):
            errors.append(f"menus.json: item at {item_path} has no id")
        if not item.get("labelKey"):
            errors.append(f"menus.json: item at {item_path} has no labelKey")
        if not item.get("action") and not item.get("mpvCommand"):
            errors.append(f"menus.json: item at {item_path} has neither action nor mpvCommand")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--xaml-audit",
        action="store_true",
        help="also scan XAML for hardcoded UI strings",
    )
    parser.add_argument(
        "--languages",
        default="",
        metavar="zh-CN,en-US",
        help=(
            "only check these language files (comma-separated stems). The default "
            "checks all nine, which is the honest state: the generated Schema* "
            "layer and the seven non-CJK languages are known-incomplete and "
            "registered as an exception in docs/settings-design-contract.md 7."
        ),
    )
    args = parser.parse_args()

    props = parse_applang_props_all()
    if not props:
        print("ERROR: could not parse AppLang properties", file=sys.stderr)
        return 2

    errors: list[str] = []
    warnings: list[str] = []
    lang_dir = ROOT / "src" / "mpvw-app" / "Languages"
    lang_files = sorted(lang_dir.glob("*.json"))

    selected = [s.strip() for s in args.languages.split(",") if s.strip()]
    if selected:
        wanted = {s.lower() for s in selected}
        lang_files = [p for p in lang_files if p.stem.lower() in wanted]
        unknown = wanted - {p.stem.lower() for p in lang_files}
        if unknown:
            print(f"ERROR: --languages names files that do not exist: {sorted(unknown)}",
                  file=sys.stderr)
            return 2
        if not lang_files:
            print("ERROR: --languages selected no files", file=sys.stderr)
            return 2
    en_data: dict[str, str] = {}
    for path in lang_files:
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        keys = set(data.keys())
        missing = props - keys
        extra = keys - props
        if missing:
            # Summarise by family when the gap is systematic. A list of 724
            # names tells you nothing; "362x SchemaLabel_*, 362x SchemaDesc_*"
            # tells you the generated layer was never translated, which is the
            # actual state. Families are the leading CamelCase word followed by
            # "_", so SchemaLabel_Mpv_x -> SchemaLabel_*.
            #
            # Schema* is reported separately: it is generated from mpv's own
            # option table, so "746 missing" is one decision about the generated
            # layer, while a missing Shell* or Library* key is a label a person
            # still has to write.
            generated = {k for k in missing if k.startswith(("SchemaLabel_", "SchemaDesc_"))}
            hand_written = missing - generated
            if hand_written:
                errors.append(
                    f"{path.name}: missing {len(hand_written)} hand-written AppLang key(s): "
                    f"{_summarise(hand_written)}"
                    + (f" (e.g. {sorted(hand_written)[0]})" if len(hand_written) > 12 else ""))
            if generated:
                warnings.append(
                    f"{path.name}: missing {len(generated)} generated Schema* key(s) "
                    f"(known exception, see settings-design-contract 7)")
        if extra:
            # Two very different things ended up in `extra`, and reporting them
            # as one "expected" warning is how eleven dead keys stayed in the
            # tree: every file carried them, the checker said "expected", and
            # nothing distinguished them from the customize table that really
            # does live here on purpose.
            #
            #   * the customize-mode shell's companion table (CustomizeStrings)
            #     plus ApplyAndExit -- read by CustomizeStrings.Load, and having
            #     no AppLang property is the design (see its class docstring);
            #   * everything else -- no AppLang property AND no reader. Those
            #     are keys nothing asks for: a setting that was removed, or a
            #     generated section that no longer exists. They are dead weight
            #     in nine files that every future translator pays for.
            expected = {k for k in extra
                        if k.startswith("Customize") or k == "ApplyAndExit"}
            stale = extra - expected
            if expected:
                warnings.append(
                    f"{path.name}: {len(expected)} companion-table key(s) with no AppLang "
                    f"property (expected): {_summarise(expected)}")
            if stale:
                errors.append(
                    f"{path.name}: {len(stale)} stale key(s) with no AppLang property and "
                    f"no reader: {_summarise(stale)}"
                    + (f" (e.g. {sorted(stale)[0]})" if len(stale) > 12 else ""))
        if path.name == "en-US.json":
            en_data = data

    # Translation completeness: a translated value is one that differs from
    # the English source. Keys whose value still equals en-US are either
    # intentional (proper nouns, identical spelling) or untranslated residue.
    if en_data:
        for path in lang_files:
            if path.name == "en-US.json":
                continue
            data = json.loads(path.read_text(encoding="utf-8-sig"))
            untranslated = [
                key for key in en_data
                if key in data and data[key] == en_data[key] and key in props
            ]
            total = len(props)
            translated = total - len(untranslated)
            pct = translated / total * 100 if total else 100
            print(
                f"COMPLETENESS {path.stem}: {translated}/{total} ({pct:.0f}%); "
                f"residue: {', '.join(sorted(untranslated)[:6])}{'…' if len(untranslated) > 6 else ''}"
            )

    menus_path = ROOT / "src" / "mpvw-app" / "Menus" / "menus.json"
    if menus_path.exists():
        menus = json.loads(menus_path.read_text(encoding="utf-8-sig"))
        actions = parse_menu_actions(
            ROOT / "src" / "mpvw-app" / "Domains" / "Playback" / "MpvPlayerPage_MenuBar.cs"
        )
        validate_menu_schema(menus, errors)
        walk_menus(menus, props, actions, errors)
    else:
        warnings.append("menus.json not found (bundled default missing)")

    if args.xaml_audit:
        pattern = re.compile(r'(?:Text|Title|ToolTipService\.ToolTip)="([^"{][^"]*)"')
        xaml_dir = ROOT / "src" / "mpvw-app"
        for xaml in sorted(xaml_dir.rglob("*.xaml")):
            rel = xaml.relative_to(ROOT)
            if any(part in {"bin", "obj", "publish"} for part in rel.parts):
                continue
            for line_no, line in enumerate(xaml.read_text(encoding="utf-8").splitlines(), 1):
                for match in pattern.finditer(line):
                    print(f"XAML {xaml.relative_to(ROOT)}:{line_no}: {match.group(0)}")

    for warning in warnings:
        print(f"WARNING: {warning}")
    for error in errors:
        print(f"ERROR: {error}", file=sys.stderr)
    if errors:
        return 1
    print(f"OK: {len(props)} AppLang props, {len(lang_files)} language files")
    return 0


if __name__ == "__main__":
    sys.exit(main())
