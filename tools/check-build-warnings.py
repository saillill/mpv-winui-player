#!/usr/bin/env python3
"""Count and classify every build warning. Exit 1 if any are not in the budget.

The budget is per CODE, not a single number: "80 warnings" is not a target you
can make progress against, while "WMC1506 x128, WUI2011 x14, ..." is. A code
that reappears after its count was driven to zero is a regression, which is the
point of running this in a chain.

Why this exists at all: the repository carries no TreatWarningsAsErrors, so a
warning is a message nobody is obliged to read. Every one of the 80 here is a
defect that was already known and already ignored. The budget file turns
"ignored" into "counted", and the count is the only thing that makes progress
visible.

Usage:
  python tools/check-build-warnings.py             # fast: rebuild the app only
  python tools/check-build-warnings.py --full      # also the C++ and test projects
  python tools/check-build-warnings.py --write-budget
"""
from __future__ import annotations

import collections
import io
import os
import re
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BUDGET = os.path.join(REPO, "tools", "build-warning-budget.txt")
APP = os.path.join("src", "mpvw-app", "mpv-winui.csproj")
NATIVE = os.path.join("src", "mpvw-native", "mpv-winrt.vcxproj")
TESTS = os.path.join("tests", "native", "mpv-winrt-test.csproj")

# The app build prints each warning twice: once as it happens, once in the
# trailing summary block. Dividing by the number of passes keeps the count equal
# to the number of distinct warning sites, which is what a person fixes.
WARNING = re.compile(r"\bwarning\s+([A-Z]{2,4}\d{4})\b")
SITE = re.compile(r"([A-Za-z0-9_.\\:/-]+\.(?:xaml|cs))\((\d+),(\d+)\): warning ([A-Z]{2,4}\d{4})")


def build(args: list[str]) -> tuple[list[str], int]:
    try:
        res = subprocess.run(args, cwd=REPO, capture_output=True, text=True,
                             encoding="utf-8", errors="replace")
    except OSError as exc:
        print(f"FAIL: cannot run {args[0]}: {exc}")
        return [], 2
    return (res.stdout + "\n" + res.stderr).splitlines(), res.returncode


def app_args() -> list[str]:
    # --no-incremental is load-bearing, and this was measured the hard way: an
    # incremental run right after a successful build reported 2 warnings instead
    # of 80, because MSBuild skipped the compile it had already done and replayed
    # only the XAML pass. A loop that reports 2 in one state and 80 in another is
    # not a loop. Cost of being right: ~60 s.
    return ["dotnet", "build", APP, "-c", "Release", "-p:Platform=x64",
            "-p:GenerateAppxPackageOnBuild=false", "-p:AppxPackageSigningEnabled=false",
            "-v:m", "--no-incremental"]


def full_args() -> list[list[str]]:
    return [
        ["msbuild", NATIVE, "/t:Rebuild", "/p:Configuration=Release", "/p:Platform=x64",
         "/p:VisualStudioVersion=17.0", "/m", "/v:m", "/nologo"],
        ["dotnet", "build", TESTS, "-c", "Release", "-p:Platform=x64", "-v:m", "--no-incremental"],
    ]


def tally(lines: list[str]) -> collections.Counter:
    """Distinct (file, line, column, code) sites, not raw message counts.

    A count of messages moves whenever MSBuild decides to repeat a block; a
    count of sites moves only when the code changes, which is what the budget
    has to mean.
    """
    sites: set[tuple[str, int, int, str]] = set()
    loose: collections.Counter = collections.Counter()
    for line in lines:
        m = SITE.search(line)
        if m:
            path = os.path.basename(m.group(1).replace("\\", "/"))
            sites.add((path, int(m.group(2)), int(m.group(3)), m.group(4)))
            continue
        for code in WARNING.findall(line):
            # Package and project-level warnings carry no file position.
            loose[code] += 1
    counts: collections.Counter = collections.Counter()
    for _path, _line, _col, code in sites:
        counts[code] += 1
    # Loose warnings arrive twice per site in dirs.proj-style output.
    for code, n in loose.items():
        counts[code] += max(1, n // 2)
    return counts


def read_budget() -> dict[str, int]:
    if not os.path.isfile(BUDGET):
        return {}
    out: dict[str, int] = {}
    for line in io.open(BUDGET, encoding="utf-8"):
        line = line.split("#", 1)[0].strip()
        if not line:
            continue
        code, _, count = line.partition("=")
        out[code.strip()] = int(count.strip())
    return out


def write_budget(counts: collections.Counter) -> None:
    with io.open(BUDGET, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("# Accepted build warnings, one code per line: CODE = COUNT\n")
        fh.write("# Regenerate with: python tools/check-build-warnings.py --write-budget\n")
        fh.write("# A code not listed here, or a count above its number, fails the check.\n")
        fh.write("#\n")
        fh.write("# Target is zero for every code. A non-zero number is a defect being\n")
        fh.write("# worked off, not a limit to live within.\n")
        for code in sorted(counts):
            fh.write(f"{code} = {counts[code]}\n")
    print(f"budget written: {sum(counts.values())} site(s) across {len(counts)} code(s)")


def main() -> int:
    lines, code = build(app_args())
    if code != 0:
        print(f"FAIL: the app build failed (exit {code}); warnings are not the question")
        for line in lines:
            if ": error " in line:
                print("  " + line.strip()[:200])
        return code

    counts = tally(lines)
    failed = False

    if "--full" in sys.argv:
        for args in full_args():
            extra, rc = build(args)
            if rc != 0:
                print(f"FAIL: {' '.join(args[:2])} failed (exit {rc})")
                failed = True
            for k, v in tally(extra).items():
                counts[k] += v

    total = sum(counts.values())
    print(f"measured: {total} warning site(s) across {len(counts)} code(s)")
    for c in sorted(counts, key=lambda k: (-counts[k], k)):
        print(f"  {c:10s} {counts[c]:4d}")

    if "--write-budget" in sys.argv:
        write_budget(counts)
        return 1 if failed else 0

    budget = read_budget()
    if not budget:
        print("FAIL: tools/build-warning-budget.txt is missing or empty")
        return 2

    worse: list[tuple[str, int, int]] = []
    for c, n in sorted(counts.items()):
        allowed = budget.get(c)
        if allowed is None:
            worse.append((c, -1, n))
        elif n > allowed:
            worse.append((c, allowed, n))
    for c, allowed in sorted(budget.items()):
        if c not in counts and allowed:
            print(f"  improved: {c} {allowed} -> 0, tighten the budget")

    if worse:
        print(f"FAIL: {len(worse)} code(s) at or above budget")
        for c, allowed, n in worse:
            was = "not in the budget" if allowed < 0 else f"budget {allowed}"
            print(f"  {c}: {was}, now {n}")
        return 1
    if failed:
        return 1

    print(f"OK: {total} warning site(s), none above budget")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    raise SystemExit(main())
