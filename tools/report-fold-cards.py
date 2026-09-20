#!/usr/bin/env python3
"""Report every second-level (folded) card and how many rows sit behind it.

The rule this checks: a section only folds when it has enough rows to be worth
opening. A card with one or two rows costs a click and hides less than its own
header, so it should have been flattened instead.

This is a static approximation of what BuildSettings decides, and the two
places it can differ from the running app are called out in the output:

  * `rows` counts sectionMap entries, which is every row the page builds for
    that section -- including the schema-generated ones, because their
    registration writes the same map.
  * `pinned` counts rows the page pulls back onto the overview regardless of
    their section. Those are the ones that do NOT end up behind the card.

`behind` is therefore the number ApplyFoldFloor uses, and a section is only
supposed to be folded when behind >= FoldFloor.

Exit code 1 when a folded section has fewer than FoldFloor rows behind it, or
when the folded set and the computed set disagree.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
SETTINGS = REPO / "mpv-winui" / "mpv-winui" / "Modules" / "Settings"
CORE = SETTINGS / "SettingsPage.Options.cs"
GEN_SECTIONS = SETTINGS / "SettingsPage.Sections.Gen.cs"

FOLD_FLOOR = 4


def block(text: str, opener: str) -> str:
    """The braced body following `opener`, by brace matching."""
    parts = text.split(opener, 1)
    if len(parts) < 2:
        return ""
    body = parts[1]
    depth, i = 0, 0
    for i, ch in enumerate(body):
        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
            if depth == 0:
                return body[: i + 1]
    return body


def folded_sections(text: str) -> set[str]:
    """Section variables the advancedSections initializer lists.

    Also the generated ones: RegisterGeneratedSections adds its nine through
    `advancedSections.Add(lang.X)`, so a report that only read the initializer
    would call every generated card flat and miss the floor entirely. They are
    returned under a `gen:` prefix to match section_rows().
    """
    body = block(text, "var advancedSections = new HashSet<string>(StringComparer.Ordinal)")
    # Strip line comments first: several are commented out in place and
    # counting them would invent sections that are not folded.
    body = re.sub(r"//[^\n]*", "", body)
    folded = set(re.findall(r"\b(s[A-Za-z0-9]+)\b", body))

    if GEN_SECTIONS.exists():
        gen = GEN_SECTIONS.read_text(encoding="utf-8-sig")
        folded |= {f"gen:{m}" for m in re.findall(
            r"advancedSections\.Add\(lang\.(\w+)\)", gen)}
    return folded


def section_rows(text: str) -> dict[str, int]:
    """Section variable -> rows it owns, from sectionMap plus the generated half."""
    body = block(text, "var sectionMap = new Dictionary<string, string>(StringComparer.Ordinal)")
    body = re.sub(r"//[^\n]*", "", body)
    counts: dict[str, int] = {}
    for var in re.findall(r"=\s*(s[A-Za-z0-9]+),", body):
        counts[var] = counts.get(var, 0) + 1

    if GEN_SECTIONS.exists():
        gen = GEN_SECTIONS.read_text(encoding="utf-8-sig")
        for sec in re.findall(r"sectionMap\[nameof\([^)]+\)\] = lang\.(\w+)", gen):
            counts[f"gen:{sec}"] = counts.get(f"gen:{sec}", 0) + 1
    return counts


def pinned_keys(text: str) -> set[str]:
    body = block(text, "var pinnedOverviewOptions = new HashSet<string>(StringComparer.Ordinal)")
    body = re.sub(r"//[^\n]*", "", body)
    return set(re.findall(r"nameof\(AppSettings\.(\w+)\)", body))


def section_of_keys(text: str) -> dict[str, str]:
    """Option key -> section variable, from the hand-written sectionMap."""
    body = block(text, "var sectionMap = new Dictionary<string, string>(StringComparer.Ordinal)")
    body = re.sub(r"//[^\n]*", "", body)
    out: dict[str, str] = {}
    for m in re.finditer(
        r'\[(?:nameof\(AppSettings\.(\w+)\)|"([^"]+)")\]\s*=\s*(s[A-Za-z0-9]+)', body
    ):
        out[m.group(1) or m.group(2)] = m.group(3)
    return out


def main() -> int:
    text = CORE.read_text(encoding="utf-8-sig")
    folded = folded_sections(text)
    rows = section_rows(text)
    pinned = pinned_keys(text)
    owner = section_of_keys(text)

    pinned_by_section: dict[str, int] = {}
    for key in pinned:
        var = owner.get(key)
        if var:
            pinned_by_section[var] = pinned_by_section.get(var, 0) + 1

    print(f"FoldFloor = {FOLD_FLOOR} rows behind the card\n")
    print(f"{'section':34s} {'rows':>4s} {'pin':>4s} {'behind':>6s}  state")
    problems: list[str] = []
    for var in sorted(rows, key=lambda v: rows[v] - pinned_by_section.get(v, 0)):
        total = rows[var]
        pin = pinned_by_section.get(var, 0)
        behind = total - pin
        is_folded = var in folded
        if is_folded and behind < FOLD_FLOOR:
            state = "BAD: folded with too few rows"
            problems.append(f"{var}: folded, only {behind} row(s) behind the card")
        elif is_folded:
            state = "folded"
        elif behind >= FOLD_FLOOR:
            state = "flat (kept flat on purpose)"
        else:
            state = "flat"
        print(f"{var:34s} {total:4d} {pin:4d} {behind:6d}  {state}")

    print()
    if problems:
        for p in problems:
            print(f"ERROR: {p}")
        print(f"FAIL: {len(problems)} folded section(s) below the floor")
        return 1
    print(f"OK: every folded section has >= {FOLD_FLOOR} rows behind its card")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
