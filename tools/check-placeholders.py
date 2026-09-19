"""Every free-text setting must either show an example, or be a path field.

A string field with no hint is a blank box: the user has to already know the
syntax to fill it in, which is exactly the case for the ones taking a
comma-separated list, a header block or a property expansion.

The hint is a C# literal and cannot be localized, so the rule is only that one
exists - the texts themselves live in the option files and are written as values
rather than prose.

Path fields are exempt, and deliberately not merely optional. They show the
placeholder AS their current value when empty (OptionStringControl
.UpdatePathDisplay), so a placeholder there must be a real default. The existing
path options qualify because theirs are resolved defaults; one with no default
gets no placeholder, and an empty box with a Browse button is the honest state.
Putting an example there would display a path the user never set.

Run:  python tools/check-placeholders.py
"""
import glob
import re
import sys

OPTION_FILES = "mpv-winui/mpv-winui/Modules/Settings/SettingsPage.Options*.cs"


def blocks(src: str):
    for m in re.finditer(r"new Option\s*\{", src):
        start = src.index("{", m.start())
        depth, i = 0, start
        while i < len(src):
            if src[i] == "{":
                depth += 1
            elif src[i] == "}":
                depth -= 1
                if depth == 0:
                    break
            i += 1
        yield src[start:i + 1]


def main() -> int:
    total = 0
    path_with_default = []   # path field whose hint IS a real resolved default
    example_hints = []       # non-path field showing an example
    missing = []

    for path in sorted(glob.glob(OPTION_FILES)):
        src = open(path, encoding="utf-8-sig").read()
        for block in blocks(src):
            # Trailing comma, so StringList is not swept in: a dropdown picks
            # from known values and needs no example in a box.
            if not re.search(r"Type = OptionType\.String\s*,", block):
                continue
            total += 1

            key = re.search(r'Key = (?:nameof\(AppContext\.AppSetting\.(\w+)\)|"([^"]+)")',
                            block)
            name = (key.group(1) or key.group(2)) if key else "<unnamed>"
            has_hint = "Placeholder" in block
            is_path = bool(re.search(r"PickFolder|PickFile|OpenFolder", block))

            if not has_hint:
                if not is_path:
                    missing.append((name, path.split("/")[-1]))
                continue

            (path_with_default if is_path else example_hints).append(name)

    print(f"free-text settings             : {total}")
    print(f"  showing a syntax example     : {len(example_hints)}")
    print(f"  path fields showing a default: {len(path_with_default)}")
    print(f"  deliberately left bare       : {total - len(example_hints) - len(path_with_default)}")

    if missing:
        for name, path in missing:
            print(f"      no hint: {name} ({path})")
        print("FAIL: a blank box asks the user to already know the syntax")
        return 1

    print("OK: every non-path free-text setting shows an example when empty")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
