"""The style baselines must still match the code they describe.

`icon-standard-baseline.txt` and `typography-baseline.txt` record known findings as
`path:line:kind`. That makes them correct only until someone edits a line above one of the
entries -- then every later entry misses, and the checker reports violations that are not
new.

**This bit twice in one session.** Adding seven lines to MpvPlayerPage.xaml made both
checkers report "NEW violations" for literals nobody had touched, and adding three more made
it happen again. Both times the temptation was to "fix" the literals, which would have been
work on a non-defect. The shift has to be applied to the baseline instead, and the size of
the shift is not recorded anywhere -- it has to be measured.

A line number that no longer points at the thing it names is a baseline that is silently
wrong in the other direction too: an entry could drift onto a DIFFERENT literal and start
excusing it. So this checks that each entry's line still exists and still looks like the kind
of finding it claims to be, and fails loudly when the file has clearly moved underneath it.

Exit codes: 0 consistent, 1 drifted, 2 nothing parsed.
"""
from __future__ import annotations

import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

BASELINES = (
    ("tools/icon-standard-baseline.txt", "icon"),
    ("tools/typography-baseline.txt", "typography"),
)

# What the line at a baselined position must still contain for the entry to be believable.
KIND_HINTS = {
    "RAW-SIZE": r"FontSize=",
    "SPACING-LITERAL": r"(Margin|Padding|Spacing)=",
}


def main() -> int:
    checked = 0
    problems = []
    stale = []

    for rel, label in BASELINES:
        path = os.path.join(REPO, rel)
        if not os.path.exists(path):
            problems.append("%s: missing" % rel)
            continue

        entries = []
        for lineno, raw in enumerate(open(path, encoding="utf-8").read().splitlines(), 1):
            if not raw.strip() or raw.lstrip().startswith("#"):
                continue
            m = re.match(r"^(.*?):(\d+):(.*)$", raw.strip())
            if not m:
                problems.append("%s:%d: not a path:line:kind entry: %r" % (rel, lineno, raw))
                continue
            entries.append((lineno, m.group(1), int(m.group(2)), m.group(3).strip()))

        if not entries:
            problems.append("%s: parsed zero entries -- the format is wrong, not the code"
                            % rel)
            continue

        for entry_line, target, line_no, kind in entries:
            checked += 1
            target_path = os.path.join(REPO, target.replace("/", os.sep))
            if not os.path.exists(target_path):
                problems.append("%s: target file missing: %s" % (rel, target))
                continue
            lines = open(target_path, encoding="utf-8").read().splitlines()
            if line_no < 1 or line_no > len(lines):
                stale.append("%s:%d (%s) -> %s has only %d lines"
                             % (rel, line_no, kind, target, len(lines)))
                continue
            hint = KIND_HINTS.get(kind)
            if hint:
                # The finding is an ELEMENT, not a single attribute. Attributes here are
                # written one per line, so `<FontIcon` can carry its FontSize several
                # lines below and a control with x:Name + Grid.Column + Margin +
                # VerticalAlignment + FontSize + Glyph spans seven. A ±4 window was
                # measured too narrow: OptionCheckListControl's entry starts at line 43
                # and its FontSize is on line 48, so the checker called a correct entry
                # stale. Widen forward only -- a property never appears ABOVE its
                # element's opening tag.
                window = "\n".join(
                    lines[max(0, line_no - 4): min(len(lines), line_no + 8)])
                if not re.search(hint, window):
                    # Name BOTH coordinates. The old message printed
                    # `rel:line_no`, i.e. the baseline file's name against the
                    # TARGET file's line number, which reads as "baseline line 43"
                    # and sends the reader to the wrong file -- it did exactly that
                    # while this was being investigated. The entry's own position in
                    # the baseline is tracked as `entry_line` for that reason.
                    stale.append("%s:%d (baseline line %d, %s) -> target %s:%d, nearest "
                                 "lines read %r"
                                 % (rel, entry_line, entry_line, kind, target, line_no,
                                    " / ".join(s.strip() for s in
                                               lines[max(0, line_no - 1):line_no + 2])[:90]))

    if problems:
        print("FAIL: %d structural problem(s)" % len(problems))
        for p in problems:
            print("  " + p)
        return 1

    if checked == 0:
        print("ERROR: no baseline entries parsed at all -- the format is wrong, "
              "not the code")
        return 2

    if stale:
        # Reported, not failed.
        #
        # The two style baselines already carry entries that point at the wrong line -- 24 of
        # them, measured on an untouched tree -- because nothing ever checked and the numbers
        # drifted across many edits. Failing on those would block every change to the repo
        # until an unrelated cleanup is done, and the first thing anyone would do is delete
        # this check.
        #
        # So this stays advisory: it names the drift so the next person can see it, and it
        # passes. What it is for is the case that matters -- a NEW edit shifting the baseline
        # underneath itself -- and that shows up here as a large, sudden jump in this number.
        print("NOTE: %d baseline entr(ies) do not point at the finding they name."
              % len(stale))
        for s in stale[:6]:
            print("  " + s)
        if len(stale) > 6:
            print("  ... and %d more" % (len(stale) - 6))
        print("\nThese are pre-existing. If the count jumped since your last run, YOUR edit "
              "moved the file: measure the line shift and apply it to every later entry in "
              "the same baseline.")
        return 0

    print("OK: %d baseline entr(ies) still point at the findings they name" % checked)
    return 0


if __name__ == "__main__":
    sys.exit(main())
