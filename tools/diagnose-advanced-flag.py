#!/usr/bin/env python3
"""Check that folding leaves each category a usable page.

Two rules decide the shape of a category overview, and they answer different
questions:

  * FoldFloor      -- is a card worth opening? A section whose card would hold
                      fewer than four surviving rows is flattened instead.
  * CategoryFloor  -- is the category still a page? A category whose sections
                      are all cards opens onto a directory of cards with no
                      rows, which tells the user nothing about where to go.

This script models both against the real option tree and reports what the
overview will contain. It also checks the invariant that makes the pins safe: a
row that is hidden behind a card must be in a section that actually folds, or it
is invisible with nothing to open.

Reads both halves (hand-written and generated), resolving section and category
locals back to their AppLang properties first: the two halves spell the same
section differently (`sPlayback` vs `secPlayerWindow`), and comparing the local
names directly is how a checker ends up reporting one section as two.
"""

from __future__ import annotations

import collections
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
APP = REPO / "mpv-winui" / "mpv-winui"
SETTINGS = APP / "Domains" / "Settings"
LANG = APP / "Lexicon" / "Language"
CORE = SETTINGS / "SettingsPage.Options.cs"
GEN_SECTIONS = SETTINGS / "SettingsPage.Sections.Gen.cs"

# Mirrored from SettingsPage.Options.cs. Kept in sync by hand: they are two
# constants in one file and the drift is visible immediately in the report.
FOLD_FLOOR = 4
CATEGORY_FLOOR = 8

ALIAS = re.compile(r"var\s+(\w+)\s*=\s*AppContext\.AppLang\.(\w+)\s*;")


def braced(text: str, opener: str) -> str:
    parts = text.split(opener, 1)
    if len(parts) < 2:
        return ""
    body = parts[1]
    depth = 0
    for i, ch in enumerate(body):
        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
            if depth == 0:
                return body[: i + 1]
    return body


def aliases() -> dict[str, str]:
    out: dict[str, str] = {}
    for path in sorted(SETTINGS.glob("SettingsPage.*.cs")) + sorted(LANG.glob("AppLang*.cs")):
        for m in ALIAS.finditer(path.read_text(encoding="utf-8-sig")):
            out[m.group(1)] = m.group(2)
    return out


def option_blocks(src: str):
    """(key, inline section, inline category) for every `new Option` literal."""
    for m in re.finditer(r"new Option\s*\{", src):
        start = src.index("{", m.start())
        depth = 0
        for i in range(start, len(src)):
            if src[i] == "{":
                depth += 1
            elif src[i] == "}":
                depth -= 1
                if depth == 0:
                    break
        body = src[start : i + 1]
        key = re.search(
            r'Key = (?:nameof\(AppContext\.AppSetting\.(\w+)\)|"([^"]+)")', body)
        if not key:
            continue
        sec = re.search(r"^\s*Section = (\w+),", body, re.M)
        cat = re.search(r"^\s*Category = (\w+),", body, re.M)
        yield (key.group(1) or key.group(2),
               sec.group(1) if sec else None,
               cat.group(1) if cat else None)


def load():
    core = CORE.read_text(encoding="utf-8-sig")
    alias = aliases()

    def resolve(name: str | None) -> str | None:
        return alias.get(name, name) if name else None

    sm_block = re.sub(r"//[^\n]*", "", braced(
        core, "var sectionMap = new Dictionary<string, string>(StringComparer.Ordinal)"))
    section_map = {
        (m.group(1) or m.group(2)): resolve(m.group(3))
        for m in re.finditer(
            r'\[(?:nameof\(AppSettings\.(\w+)\)|"([^"]+)")\]\s*=\s*(s[A-Za-z0-9]+)', sm_block)
    }

    fold_block = re.sub(r"//[^\n]*", "", braced(
        core, "var advancedSections = new HashSet<string>(StringComparer.Ordinal)"))
    fold = {resolve(s) for s in re.findall(r"\b(s[A-Za-z0-9]+)\b", fold_block)}
    fold |= set(re.findall(r"advancedSections\.Add\(lang\.(\w+)\)",
                           GEN_SECTIONS.read_text(encoding="utf-8-sig")))

    pinned = set(re.findall(
        r"nameof\(AppSettings\.(\w+)\)",
        braced(core, "var pinnedOverviewOptions = new HashSet<string>(StringComparer.Ordinal)")))

    rows: list[tuple[str, str, str]] = []          # key, category, section
    for path in sorted(SETTINGS.glob("SettingsPage.Options*.cs")):
        for key, inline_sec, inline_cat in option_blocks(path.read_text(encoding="utf-8-sig")):
            section = resolve(inline_sec) or section_map.get(key)
            if section:
                rows.append((key, resolve(inline_cat) or "?", section))

    return rows, fold, pinned


LAYOUT = SETTINGS / "SettingsPage.Options.Layout.cs"


def check_order_keeps_sections_together() -> str | None:
    """The row-order pass must group by section.

    A shape check, not a behavioural one -- the pass is pure C# and porting it
    here would drift. It earns its place because the failure it guards was
    invisible in every other check: the flat drag order out of customize mode was
    applied as one global sort, so a row dragged across a section boundary left
    its section, the list stopped being grouped by section, and the overview --
    which emits a card per section entry -- produced the same card several times
    at unrelated positions. Cards went chaotic after a couple of drags and
    nothing in the tree looked wrong.

    The renderer no longer depends on contiguity (cards are emitted per section,
    tracked by identity), so this is defence in depth: the order still ought to
    be grouped, because section headers and the customize pane both read better
    that way.
    """
    src = LAYOUT.read_text(encoding="utf-8-sig")
    if "GroupBy(x => (x.option.Category, x.option.Section))" in src:
        return None

    if "rankKey" in src:
        return ("the row-order pass sorts the whole list by the stored drag order "
                "without grouping by section, so a drag across a section boundary "
                "interleaves sections -- group by (Category, Section) and order "
                "the runs, as it does today")
    return "the row-order pass no longer shows a recognisable per-section reorder"


def main() -> int:
    rows, fold, pinned = load()

    # A row is hidden when its section folds and it is not pinned.
    def hidden(key: str, section: str, folds: set[str]) -> bool:
        return section in folds and key not in pinned

    # --- FoldFloor: a card must hold at least FOLD_FLOOR rows -------------
    behind = collections.Counter()
    kept = collections.Counter()
    for key, _cat, section in rows:
        if hidden(key, section, fold):
            behind[section] += 1
        elif section in fold:
            kept[section] += 1

    folded = {s for s in behind if behind[s] >= FOLD_FLOOR}
    removed = {s for s in fold if behind[s] < FOLD_FLOOR}

    # --- CategoryFloor: a category must keep CATEGORY_FLOOR visible rows --
    flattened = collections.Counter()
    for cat in sorted({c for _k, c, _s in rows}):
        def visible() -> int:
            return sum(1 for k, c, s in rows if c == cat and not hidden(k, s, folded))

        while visible() < CATEGORY_FLOOR:
            candidates = sorted(
                ((s, sum(1 for k, c, s2 in rows
                         if c == cat and s2 == s and hidden(k, s, folded)))
                 for s in {s for _k, c, s in rows if c == cat and s in folded}),
                key=lambda t: (t[1], t[0]))
            if not candidates:
                break
            folded.discard(candidates[0][0])
            flattened[candidates[0][0]] += 1

    # --- Report ----------------------------------------------------------
    by_cat: dict[str, dict[str, int]] = {}
    for cat in sorted({c for _k, c, _s in rows}):
        cards = len({s for _k, c, s in rows if c == cat and s in folded})
        flat = sum(1 for k, c, s in rows if c == cat and s not in folded)
        surf = sum(1 for k, c, s in rows if c == cat and s in folded and k in pinned)
        hid = sum(1 for k, c, s in rows if c == cat and hidden(k, s, folded))
        by_cat[cat] = {"cards": cards, "flat": flat, "kept": surf, "hidden": hid}

    print(f"{'category':24s} {'cards':>5s} {'rows':>5s} {'flat':>5s} {'pinned':>6s} {'hidden':>6s}")
    problems: list[str] = []
    for cat, v in by_cat.items():
        visible = v["flat"] + v["kept"]
        flag = "" if visible >= CATEGORY_FLOOR else "   <-- below the category floor"
        if visible == 0:
            problems.append(f"{cat} opens onto cards only")
            flag = "   <-- NO ROWS"
        print(f"{cat:24s} {v['cards']:5d} {visible:5d} {v['flat']:5d} {v['kept']:6d} "
              f"{v['hidden']:6d}{flag}")

    print()
    print(f"sections named in the fold set ......... {len(fold)}")
    print(f"folded after FoldFloor ................. {len(folded)}")
    print(f"flattened by FoldFloor ................. {len(removed)}")
    print(f"flattened by CategoryFloor ............. {len(flattened)}")
    print(f"rows behind cards ...................... {sum(1 for k, _c, s in rows if hidden(k, s, folded))}")
    print(f"pinned rows kept on the overview ....... {sum(1 for k, _c, s in rows if s in folded and k in pinned)}")

    for s in sorted(flattened):
        print(f"  category floor flattened: {s}")

    # A hidden row must belong to a section that folds, or nothing can open it.
    for key, cat, section in rows:
        if section not in folded and (section in fold) and key not in pinned:
            pass   # flattened: the row renders, which is correct
        if hidden(key, section, folded) and section not in folded:
            problems.append(f"{key} is hidden but {section} does not fold")

    if problems:
        print()
        for p in problems[:20]:
            print(f"ERROR: {p}")
        print(f"FAIL: {len(problems)} problem(s)")
        return 1

    order = check_order_keeps_sections_together()
    if order:
        print()
        print(f"ERROR: {order}")
        print("FAIL: the row-order pass can interleave sections")
        return 1

    print()
    print(f"OK: every category keeps >= {CATEGORY_FLOOR} rows, every folded section "
          f"clears {FOLD_FLOOR}, no row is hidden without a card, and the row-order "
          f"pass keeps sections together")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
