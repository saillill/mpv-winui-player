"""Every folded section, with its category and row count -- the list D13 judges.

D13 needs one semantic question answered per section: is it a *subdivision* of
what its category is about (so it should expand in place), or a *parallel
feature block* (so it should stay a card that navigates)? The answer is a
judgement, but the list it is a judgement about is data, and the data has to
come from the code: the fold set is computed after two floors run, and rows reach
their section through `sectionMap` rather than from their own literal.

So this imports check-advanced-flag.py and reuses its loader instead of writing a
fourth parser -- that loader already handles the aliases, both halves of the tree
and the resolution order, and it is the one the build enforces.

Usage: python tools/list-folded-sections.py
"""
import collections
import importlib.util
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CHECKER = os.path.join(REPO, "tools", "check-advanced-flag.py")


def load_checker():
    spec = importlib.util.spec_from_file_location("advanced", CHECKER)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main() -> int:
    checker = load_checker()
    rows, fold, pinned = checker.load()
    if not rows:
        raise SystemExit("the shared loader parsed 0 rows -- refusing to report")

    folded = checker.folded_sections(rows, fold, pinned) if hasattr(
        checker, "folded_sections") else None

    # Apply the two floors the way the checker does, so "folded" here means
    # what the build enforces rather than what the hand-written set says.
    def hidden(key, section, folds):
        return section in folds and key not in pinned

    behind = collections.Counter()
    for key, _cat, section in rows:
        if hidden(key, section, fold):
            behind[section] += 1
    folded_set = {s for s in behind if behind[s] >= checker.FOLD_FLOOR}
    for cat in sorted({c for _k, c, _s in rows}):
        def visible():
            return sum(1 for k, c, s in rows
                       if c == cat and not hidden(k, s, folded_set))
        while visible() < checker.CATEGORY_FLOOR:
            candidates = sorted(
                ((s, sum(1 for k, c, s2 in rows
                         if c == cat and s2 == s and hidden(k, s, folded_set)))
                 for s in {s for _k, c, s in rows if c == cat and s in folded_set}),
                key=lambda t: (t[1], t[0]))
            if not candidates:
                break
            folded_set.discard(candidates[0][0])

    by_category = collections.defaultdict(lambda: collections.Counter())
    pinned_by_section = collections.Counter()
    for key, category, section in rows:
        by_category[category][section] += 1
        if key in pinned:
            pinned_by_section[section] += 1

    print("%-34s %-32s %5s %7s %s"
          % ("category", "section", "rows", "pinned", "shape"))
    print("-" * 100)
    total = 0
    for category in sorted(by_category):
        for section in sorted(by_category[category]):
            n = by_category[category][section]
            p = pinned_by_section[section]
            # A section folds only if it is in the resolved fold set. The floors
            # can take it back out, so the counter alone would over-report.
            shape = "FOLDED" if section in folded_set else "flat"
            print("%-34s %-32s %5d %7d %s" % (category, section, n, p, shape))
            if shape == "FOLDED":
                total += 1
    print()
    print("%d section(s) still folded", total)

    if folded is not None:
        print("%d section(s) per the checker's own resolution" % len(folded))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
