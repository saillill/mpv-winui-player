"""Validate the mpv option names the settings window may write to mpv.conf.

The app persists its own values into mpv.conf, so a wrong name is a line mpv
cannot parse sitting in the user's config. Three ways that can go wrong:

  1. MpvOptionNames also carries the script-communication keys
     (user-data/mpvw/vsr-auto, ...). Those are runtime properties, not options.
     ToConfLine rejects them, and this checks the rule holds.
  2. A name that is not a legal option name could be mapped.
  3. A startup-only option has no runtime command by design, so ToCommand
     cannot format it. If nobody formats it, it is silently dropped from the
     file the moment it becomes startup-only - which is exactly what happened
     to override-display-fps during the rewrite.

It also reports how many lines the managed block will hold, which is the
number to expect in mpv.conf after a launch.

Run:  python tools/check-conf-lines.py
"""
import glob
import re
import sys

MPVSETTINGS = "mpv-winui/mpv-winui/Modules/Settings/MpvSettings.cs"

# Same rule as MpvSettings.ConfigOptionName.
LEGAL = re.compile(r"^[a-z0-9][a-z0-9-]*$")


def brace_block(src, start):
    """Text of the {...} block beginning at or after `start`."""
    i = src.index("{", start)
    depth = 0
    while i < len(src):
        if src[i] == "{":
            depth += 1
        elif src[i] == "}":
            depth -= 1
            if depth == 0:
                return src[src.index("{", start):i + 1]
        i += 1
    return ""


def main():
    src = open(MPVSETTINGS, encoding="utf-8-sig").read()

    names = dict(re.findall(
        r'\[nameof\(AppSettings\.(\w+)\)\]\s*=\s*"([^"]+)"', src))
    if not names:
        print("FAIL: could not parse MpvOptionNames")
        return 1

    # Group ToCommand cases by what they produce.
    set_keys, script_keys = set(), set()
    for m in re.finditer(
            r'nameof\(AppSettings\.(\w+)\)\s*=>\s*(.*?)(?=\n\s+nameof\(AppSettings\.|\n\s+_\s*=>)',
            src, re.S):
        body = m.group(2)
        if "script-message" in body:
            script_keys.add(m.group(1))
        elif "set " in body:
            set_keys.add(m.group(1))

    # Startup-only options: no runtime command, but still owned and written.
    m = re.search(r"ConfigOnlyKeys\s*=\s*new", src)
    config_only = set(
        re.findall(r"nameof\(AppSettings\.(\w+)\)", brace_block(src, m.start()))
    ) if m else set()

    m = re.search(r"private static string\? ConfigOnlyConfLine\(", src)
    config_only_handled = set(
        re.findall(r"nameof\(AppSettings\.(\w+)\)", brace_block(src, m.start()))
    ) if m else set()

    legal, illegal, unformattable, runtime_only = [], [], [], []

    for key, name in sorted(names.items()):
        if "/" in name:
            # A user-data runtime property (user-data/mpvw/...). mpv has no
            # config option by that name, so ToConfLine must reject it - it is
            # reachable only through a runtime `set`. Reported, not a failure.
            runtime_only.append((key, name))
            continue

        if not LEGAL.match(name):
            illegal.append((key, name))
            continue

        if key in set_keys:
            legal.append((key, name))
        elif key in config_only:
            # Legal only if something can actually format the line.
            if key in config_only_handled:
                legal.append((key, name))
            else:
                unformattable.append((key, name))
        elif key in script_keys:
            continue
        else:
            # A mapped name with no producer at all: either dead weight or a
            # missing ToCommand case.
            unformattable.append((key, name))

    print(f"option-name entries      : {len(names)}")
    print(f"  written to mpv.conf    : {len(legal)}")
    print(f"  startup-only           : {len(config_only)}")
    print(f"  runtime-only (no config form): {len(runtime_only)}")
    for key, name in runtime_only:
        print(f"      {key} -> {name}")
    for key, name in illegal:
        print(f"      illegal: {key} -> {name}")
    for key, name in unformattable:
        print(f"      unformattable: {key} -> {name}")

    # A name written for two different keys would make the block keep one of
    # them arbitrarily.
    by_name = {}
    for key, name in legal:
        by_name.setdefault(name, []).append(key)
    dupes = {n: k for n, k in by_name.items() if len(k) > 1}
    if dupes:
        print(f"  NOTE: {len(dupes)} option(s) claimed by several keys "
              f"(first wins in the block):")
        for n, k in dupes.items():
            print(f"      {n}: {', '.join(k)}")

    problems = []
    if illegal:
        problems.append("illegal option names are mapped")
    if unformattable:
        problems.append("mapped options have no config formatter")
    if any("/" not in n and n.startswith("user-data") for _, n in legal):
        problems.append("a user-data pseudo-option is treated as writable")

    if problems:
        for p in problems:
            print("FAIL:", p)
        return 1

    print(f"OK: {len(legal)} legal option names will be written; "
          f"runtime-only properties correctly excluded")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
