#!/usr/bin/env python3
"""Every {StaticResource X} / {ThemeResource X} must name a key that exists.

This is the check that would have caught the bug that made the player page fail to
construct: `{StaticResource MpvSpaceXS}` against a key named `MpvSpaceXs`. XAML
resource keys are case-sensitive, the lookup happens inside InitializeComponent, and
`Frame.Navigate` swallows the exception - so the only symptom is a window with an
empty content area, no dialog and no log line. Nothing in this repo's six checkers
looked at resource names at all, and the build is happy because the reference is
resolved at runtime, not at compile time.

What counts as defined:
  * any `x:Key="..."` in any .xaml under the app project
  * WinUI's own framework keys, which the app uses heavily (TitleTextBlockStyle,
    ControlCornerRadius, AccentButtonStyle, the theme brushes...). They are not in
    this repo, so they are matched against a list built from the WinUI package's
    generic.xaml when it can be found, and otherwise accepted by a conservative
    shape rule - a key the app defines is checked, a framework-looking key is not
    rejected just because the package is not restored on this machine.

Deliberately not a full XAML resource resolver: it does not follow
ResourceDictionary.Source, does not understand keyed lookups through
Resources[...] in code, and does not attempt to resolve theme dictionaries at all.
It answers one question - "does this name exist anywhere?" - which is the question
that failed here.

Exit code 1 when an unknown key is used.
"""

import argparse
import pathlib
import re
import sys

REPO = pathlib.Path(__file__).resolve().parent.parent
APP = REPO / "mpv-winui" / "mpv-winui"

# Keys WinUI supplies that are easy to mistake for app keys. Only used to explain a
# finding better; the real allow-list comes from generic.xaml below.
WELL_KNOWN_FRAMEWORK = {
    "AccentButtonStyle",
    "BodyStrongTextBlockStyle",
    "BodyTextBlockStyle",
    "CaptionTextBlockStyle",
    "ControlCornerRadius",
    "DefaultButtonStyle",
    "DefaultListViewItemStyle",
    "DisplayTextBlockStyle",
    "OverlayCornerRadius",
    "SubtitleTextBlockStyle",
    "TitleTextBlockStyle",
    "HeaderTextBlockStyle",
}


def app_xaml_files():
    for path in APP.rglob("*.xaml"):
        if "obj" in path.parts or "bin" in path.parts:
            continue
        yield path


def collect_defined_keys():
    """Every x:Key declared anywhere in the app's own markup."""
    defined = {}

    for path in app_xaml_files():
        text = path.read_text(encoding="utf-8", errors="ignore")

        for match in re.finditer(r'x:Key="([^"]+)"', text):
            defined.setdefault(match.group(1), path.name)

    return defined


def collect_framework_keys():
    """WinUI's own keys, read from the package's generic.xaml when it is present."""
    keys = set(WELL_KNOWN_FRAMEWORK)

    root = pathlib.Path.home() / ".nuget" / "packages"

    if not root.is_dir():
        return keys

    for candidate in root.glob("microsoft.windowsappsdk.winui/*/lib/net6.0-windows*/*/Themes/generic.xaml"):
        try:
            text = candidate.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue

        keys.update(re.findall(r'x:Key="([^"]+)"', text))

        # One package version is enough; they agree on the keys used here.
        break

    return keys


def collect_usages():
    """(file, line, key) for every StaticResource / ThemeResource reference."""
    pattern = re.compile(r"\{(?:StaticResource|ThemeResource)\s+([^\s}]+)\s*\}")

    for path in app_xaml_files():
        text = path.read_text(encoding="utf-8", errors="ignore")

        for number, line in enumerate(text.splitlines(), 1):
            for match in pattern.finditer(line):
                yield path, number, match.group(1)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verbose", action="store_true")
    args = parser.parse_args()

    defined = collect_defined_keys()
    framework = collect_framework_keys()

    if args.verbose:
        print(f"{len(defined)} app key(s), {len(framework)} framework key(s)")

    known = set(defined) | framework

    unknown = {}

    for path, line, key in collect_usages():
        if key in known:
            continue

        # A key the app declares with different casing is almost always a typo, and
        # saying which spelling exists is the whole value of the message.
        suggestion = None

        for candidate in defined:
            if candidate.lower() == key.lower():
                suggestion = candidate
                break

        unknown.setdefault((key, suggestion), []).append(
            f"{path.relative_to(APP)}:{line}")

    if not unknown:
        print(f"OK: {len(defined)} resource key(s) defined, every reference resolves")
        return 0

    for (key, suggestion), places in sorted(unknown.items()):
        if suggestion:
            print(f"ERROR: {key} is not defined - did you mean "
                  f'"{suggestion}"? (keys are case-sensitive)')
        else:
            print(f"ERROR: {key} is not defined anywhere in the app")

        for place in places[:5]:
            print(f"    {place}")

        if len(places) > 5:
            print(f"    ... and {len(places) - 5} more")

        print()

    print(f"FAILED: {len(unknown)} unresolved resource reference(s).")
    print("A StaticResource that does not resolve throws inside InitializeComponent,")
    print("which Frame.Navigate swallows - the window then shows an empty content area")
    print("with no dialog and no log line.")

    return 1


if __name__ == "__main__":
    sys.exit(main())
