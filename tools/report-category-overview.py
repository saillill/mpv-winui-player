#!/usr/bin/env python3
"""Report how many rows are visible on each category's overview.

The requirement is that no category opens onto nothing but cards: a category
whose every section is folded shows a page of entry cards with no rows under
them, which reads as broken. That can happen even when each individual card
passes the row floor, because the two rules are about different things -- one
is "is this card worth opening", the other is "does this category have a
front page at all".

Reads both halves: the hand-written sectionMap and the generated registration,
because a category's sections can come from either.
"""

from __future__ import annotations

import collections
import json
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
APP = REPO / "mpv-winui" / "mpv-winui"
SETTINGS = APP / "Modules" / "Settings"
CORE = SETTINGS / "SettingsPage.Options.cs"
# The category a row belongs to is a localized caption resolved at runtime.
# The hand-written half spells the local as `var network = AppLang.X`, the
# generated half as `var catStreamingCache = AppLang.X`. Comparing the local
# names would treat one category as two, so every reference is resolved back
# to the AppLang property it came from before anything is counted.
CAT_LOCAL = re.compile(r"var\s+(\w+)\s*=\s*AppContext\.AppLang\.(\w+)\s*;")
GEN_SECTIONS = SETTINGS / "SettingsPage.Sections.Gen.cs"
GEN_OPTIONS = SETTINGS / "SettingsPage.Options.Gen.cs"
ZH = APP / "Languages" / "zh-CN.json"

# Options that are pinned onto the overview even though their section folds.
PINNED_MARKER = "var pinnedOverviewOptions = new HashSet<string>(StringComparer.Ordinal)"


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


def option_blocks(src: str):
    """(key, category, section-or-None) for every `new Option` in a file.

    `category` is the AppLang property name, not the local variable, so rows
    from the hand-written and generated halves land in the same bucket.
    """
    aliases = {m.group(1): m.group(2) for m in CAT_LOCAL.finditer(src)}
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
            r'Key = (?:nameof\(AppContext\.AppSetting\.(\w+)\)|"([^"]+)")', body
        )
        cat = re.search(r"Category = (\w+)", body)
        sec = re.search(r"Section = (\w+)", body)
        if key and cat:
            name = cat.group(1)
            yield ((key.group(1) or key.group(2)), aliases.get(name, name),
                   sec.group(1) if sec else None)


def main() -> int:
    core = CORE.read_text(encoding="utf-8-sig")

    # key -> declared section variable (hand-written half)
    sm = re.sub(r"//[^\n]*", "", braced(
        core, "var sectionMap = new Dictionary<string, string>(StringComparer.Ordinal)"))
    owner = {
        (m.group(1) or m.group(2)): m.group(3)
        for m in re.finditer(
            r'\[(?:nameof\(AppSettings\.(\w+)\)|"([^"]+)")\]\s*=\s*(s[A-Za-z0-9]+)', sm)
    }

    # The fold set is a set of localized captions at runtime, but both halves
    # are written as either `sXxx` (hand) or `lang.SectionGenXxx` (generated).
    # Normalising to the AppLang property name makes them comparable.
    hand_fold = set(re.findall(r"\b(s[A-Za-z0-9]+)\b", re.sub(
        r"//[^\n]*", "", braced(core, "var advancedSections = new HashSet<string>(StringComparer.Ordinal)"))))
    gen_fold = set(re.findall(r"advancedSections\.Add\(lang\.(\w+)\)",
                              GEN_SECTIONS.read_text(encoding="utf-8-sig")))

    # Rows per (category, section). Hand-written rows resolve their section
    # through sectionMap; generated rows carry it inline as a local variable,
    # so they are grouped by the generator's own Category/Section pair.
    rows: collections.Counter[tuple[str, str]] = collections.Counter()
    for path in sorted(SETTINGS.glob("SettingsPage.Options*.cs")):
        for key, cat, inline in option_blocks(path.read_text(encoding="utf-8-sig")):
            sec = inline or owner.get(key)
            if sec:
                rows[(cat, sec)] += 1

    # Pinned rows are visible even when their section folds.
    pinned = set(re.findall(r"nameof\(AppSettings\.(\w+)\)",
                            braced(core, PINNED_MARKER)))
    pinned_rows: collections.Counter[tuple[str, str]] = collections.Counter()
    for path in sorted(SETTINGS.glob("SettingsPage.Options*.cs")):
        for key, cat, inline in option_blocks(path.read_text(encoding="utf-8-sig")):
            if key in pinned:
                sec = inline or owner.get(key)
                if sec:
                    pinned_rows[(cat, sec)] += 1

    # Does a section fold? A hand-written one folds when its variable is in the
    # set; a generated one folds when its AppLang property is.
    def folds(sec: str) -> bool:
        return sec in hand_fold or sec in gen_fold

    zh = json.loads(ZH.read_text(encoding="utf-8-sig"))

    by_cat: dict[str, list[tuple[str, int]]] = collections.defaultdict(list)
    for (cat, sec), n in rows.items():
        by_cat[cat].append((sec, n))

    print(f"{'category':22s} {'cols':>4s} {'flat':>4s} {'visible':>7s}  verdict")
    bad: list[str] = []
    for cat in sorted(by_cat):
        secs = by_cat[cat]
        visible = 0
        flat_secs = 0
        for sec, n in secs:
            pin = pinned_rows.get((cat, sec), 0)
            if folds(sec):
                visible += pin          # only the pinned rows show
            else:
                visible += n            # the whole section shows
                flat_secs += 1
        label = zh.get(cat, cat)
        if visible == 0:
            verdict = f"BAD: {label} opens onto cards only"
            bad.append(label)
        else:
            verdict = f"ok: {visible} row(s) on the overview"
        print(f"{cat:22s} {len(secs):4d} {flat_secs:4d} {visible:7d}  {verdict}")

    print()
    if bad:
        for b in bad:
            print(f"ERROR: {b} has every section folded, so its overview shows no rows")
        print(f"FAIL: {len(bad)} category(ies) with no flat content")
        return 1
    print("OK: every category has at least one row visible on its overview")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
