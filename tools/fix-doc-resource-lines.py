"""Refresh the resource-key line numbers the design contract cites.

The contract points at `Styles.xaml:900` and `:960` for MpvOptionLabelStyle,
MpvOptionDescriptionStyle and MpvOptionErrorStyle. All three keys still exist, at
lines 1045, 1058 and 1105 -- roughly 145 lines further down, because the file grew
above them. That is ordinary drift, not a defect, but a doc that sends the reader to
the wrong place costs them a search every time.

This finds each `x:Key="..."` the doc cites, rewrites the citation to the real line,
and refuses if a key is missing entirely (which WOULD be a defect, and is the reason
to check rather than to bulk-replace numbers).

Deliberately not automated: shifting every number in the docs by a computed delta.
Line numbers in prose are per-reference, and a blanket shift would "fix" citations
that are deliberately about the upstream file rather than this one -- compare-upstream
.md cites MpvPlayerPage_Input.cs:60, which is the upstream name and line, and the
file here is MpvPlayerPage_InputForwarding.cs. Bulk edits would corrupt that.
"""
import io
import os
import re
import sys

REPO = r"C:\Users\z2637\Documents\DeepSeek\mpv"
DOC = os.path.join(REPO, r"docs\settings-design-contract.md")
XAML = os.path.join(REPO, r"mpv-winui\mpv-winui\Styles\Styles.xaml")

KEYS = ["MpvOptionLabelStyle", "MpvOptionDescriptionStyle", "MpvOptionErrorStyle",
        "MpvOptionControlGap", "MpvCardPadding"]


def key_lines():
    rows = io.open(XAML, encoding="utf-8-sig").read().split("\n")
    found = {}
    for i, line in enumerate(rows, 1):
        for k in KEYS:
            if 'x:Key="%s"' % k in line:
                found[k] = i
    return found


def main():
    real = key_lines()
    print("keys found in Styles.xaml:")
    for k in KEYS:
        print("  %-28s %s" % (k, real.get(k, "MISSING")))
    if len(real) != len(KEYS):
        sys.exit("a cited key is missing -- that is a real defect, not drift")

    text = io.open(DOC, encoding="utf-8-sig").read()
    edits = 0
    for key, line in real.items():
        # `Styles.xaml:NNN` within ~120 characters of the key name.
        pattern = re.compile(r"Styles\.xaml:(\d+)")
        for m in pattern.finditer(text):
            window = text[max(0, m.start() - 160):m.end() + 160]
            if key not in window:
                continue
            if int(m.group(1)) == line:
                continue
            old = m.group(0)
            new = "Styles.xaml:%d" % line
            text = text.replace(old, new, 1)
            print("  %-28s %s -> %s" % (key, old, new))
            edits += 1

    if edits:
        io.open(DOC, "wb").write(text.encode("utf-8-sig"))
    print("\nedits:", edits)


if __name__ == "__main__":
    sys.exit(main())
