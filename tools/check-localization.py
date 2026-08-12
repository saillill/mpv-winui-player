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


def parse_menu_actions(src: Path) -> set[str]:
    text = src.read_text(encoding="utf-8")
    return set(re.findall(r'case "([\w-]+)":', text))


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
    args = parser.parse_args()

    applang = ROOT / "mpv-winui" / "mpv-winui" / "Modules" / "Language" / "AppLang.cs"
    props = parse_applang_props(applang)
    if not props:
        print("ERROR: could not parse AppLang properties", file=sys.stderr)
        return 2

    errors: list[str] = []
    warnings: list[str] = []
    lang_dir = ROOT / "mpv-winui" / "mpv-winui" / "Languages"
    lang_files = sorted(lang_dir.glob("*.json"))
    for path in lang_files:
        data = json.loads(path.read_text(encoding="utf-8"))
        keys = set(data.keys())
        missing = props - keys
        extra = keys - props
        if missing:
            errors.append(f"{path.name}: missing keys: {', '.join(sorted(missing))}")
        if extra:
            warnings.append(
                f"{path.name}: extra keys (not AppLang props): {', '.join(sorted(extra))}"
            )

    menus_path = ROOT / "mpv-winui" / "mpv-winui" / "Menus" / "menus.json"
    if menus_path.exists():
        menus = json.loads(menus_path.read_text(encoding="utf-8"))
        actions = parse_menu_actions(
            ROOT / "mpv-winui" / "mpv-winui" / "Modules" / "Player" / "MpvPlayerPage_MenuBar.cs"
        )
        validate_menu_schema(menus, errors)
        walk_menus(menus, props, actions, errors)
    else:
        warnings.append("menus.json not found (bundled default missing)")

    if args.xaml_audit:
        pattern = re.compile(r'(?:Text|Title|ToolTipService\.ToolTip)="([^"{][^"]*)"')
        xaml_dir = ROOT / "mpv-winui" / "mpv-winui"
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
