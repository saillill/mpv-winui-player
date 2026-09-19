"""Audit the settings window's conflict handling.

Three tiers decide what a row does when it conflicts with another setting:

  ComputeVisible   the row does not apply  -> hidden
  ComputeEnabled   mpv ignores it          -> shown but disabled
  ComputeWarning   it applies but is odd   -> shown with a note

A fourth tier (mpv.conf override) warns when a hand-written line outranks
the row; see check-mpvconf-precedence.py.

What this checks is the wiring between them, which is where this kind of
code rots:

  1. Every setting a rule reads must be in WarningDependencyKeys, or the
     rule never re-runs when that setting changes -- a warning appears
     only after an unrelated refresh, or not at all.
  2. Every key in WarningDependencyKeys must actually be read by a rule,
     or it is a dead entry left behind by a deleted rule.

Run:  python tools/check-conflict-rules.py
"""
import glob
import re
import sys

ROOT = "mpv-winui/mpv-winui"
FILES = sorted(glob.glob(f"{ROOT}/Modules/Settings/SettingsPage.Actions*.cs"))


def read(path):
    return open(path, encoding="utf-8-sig").read()


def rule_bodies():
    """Source of the three rule methods, concatenated."""
    out = []
    for path in FILES:
        src = read(path)
        for name in ("ComputeWarning", "ComputeVisible", "ComputeEnabled"):
            m = re.search(
                r"(?:private|internal|public)\s+static\s+(?:string\?|bool)\s+"
                + name + r"\s*\([^)]*\)\s*\{",
                src)
            if not m:
                continue
            start = src.index("{", m.start())
            depth = 0
            i = start
            while i < len(src):
                if src[i] == "{":
                    depth += 1
                elif src[i] == "}":
                    depth -= 1
                    if depth == 0:
                        break
                i += 1
            out.append((name, src[start:i + 1]))
    return out


def main():
    bodies = rule_bodies()
    if not bodies:
        print("FAIL: no rule methods found (renamed?)")
        return 1

    # Settings each rule reads: s.Foo / !s.Foo / s.Foo != ...
    read_keys = set()
    for name, body in bodies:
        read_keys |= set(re.findall(r"\bs\.([A-Za-z_][A-Za-z0-9_]*)", body))

    # The declared dependency set.
    dep_src = ""
    for path in FILES:
        src = read(path)
        m = re.search(r"WarningDependencyKeys\s*=\s*new[^\{]*\{(.*?)\n?\s*\};", src, re.S)
        if m:
            dep_src = m.group(1)
            break
    declared = set(re.findall(r"nameof\(AppSettings\.([A-Za-z0-9_]+)\)", dep_src))

    print(f"rules found          : {', '.join(n for n, _ in bodies)}")
    print(f"settings read by rules: {len(read_keys)}")
    print(f"declared dependencies : {len(declared)}")

    problems = []

    # 1. a rule reads a setting that is not a declared dependency
    for key in sorted(read_keys - declared):
        problems.append(
            f"rule reads AppSettings.{key} but it is not in WarningDependencyKeys "
            f"(the rule will not re-run when {key} changes)")

    # 2. a declared dependency no rule reads
    for key in sorted(declared - read_keys):
        problems.append(
            f"WarningDependencyKeys lists AppSettings.{key} but no rule reads it "
            f"(dead entry -- a rule was probably deleted)")

    if problems:
        for p in problems:
            print("FAIL:", p)
        return 1

    print("OK: dependency set matches what the rules actually read")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
