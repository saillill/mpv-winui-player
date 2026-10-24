"""Does a cited line RANGE still contain what the reader is told it does?

`Styles.xaml:880-1021` was cited as an authority for spacing and typography. The file
has since grown by ~145 lines, so the range now covers a visual-state storyboard and
a Style's setters -- adjacent material, but not the block the citation was about.

A single line number can be re-derived exactly (a key is on one line). A range cannot:
the right answer is "the block moved and is now N1-N2", and that needs the block's
boundaries, not a delta. So this does not rewrite anything. It reports, for each range
in the docs, what the first and last cited lines actually contain, and says whether the
range still looks like a coherent block (both ends inside the same top-level resource
element) or has drifted.

That distinction is the point: a range whose start and end sit in different resource
elements is definitely stale, while one that spans a plausible block may just be wider
than intended. Neither is repaired automatically here -- a wrong guess would put a
confident-looking wrong citation in the authority document.
"""
import io
import os
import re
import sys

REPO = r"C:\Users\z2637\Documents\DeepSeek\mpv"
DOCS = ["AGENTS.md", r"docs\settings-design-contract.md", r"docs\deploy-layout.md"]
RANGE = re.compile(r"([A-Za-z0-9_\-./\\]+\.(?:cs|xaml|py|ps1|lua|idl|json|md)):(\d+)-(\d+)")


def resolve(path):
    p = path.replace("/", os.sep)
    direct = os.path.join(REPO, p)
    if os.path.exists(direct):
        return direct
    base = os.path.basename(p)
    hits = []
    for root, dirs, files in os.walk(REPO):
        dirs[:] = [d for d in dirs if d not in
                   ("bin", "obj", "dist", "libs", "node_modules", ".git", "_work")]
        if base in files:
            hits.append(os.path.join(root, base))
    return hits[0] if hits else None


def element_at(rows, line):
    """Nearest enclosing top-level <Style ...> / <ControlTemplate ...> tag above `line`."""
    for i in range(min(line, len(rows)) - 1, -1, -1):
        s = rows[i].lstrip()
        if s.startswith(("<Style ", "<ControlTemplate ", "<DataTemplate ", "<Style x:",
                         "<ResourceDictionary", "class ", "public ", "private ",
                         "internal ", "def ", "function ")):
            return s[:70]
    return "(none)"


def main():
    stale, fine = [], 0
    for doc in DOCS:
        dpath = os.path.join(REPO, doc)
        if not os.path.exists(dpath):
            continue
        text = io.open(dpath, encoding="utf-8-sig").read()
        for m in RANGE.finditer(text):
            raw, a, b = m.group(1), int(m.group(2)), int(m.group(3))
            target = resolve(raw)
            if target is None:
                continue
            rows = io.open(target, encoding="utf-8-sig", errors="replace").read().split("\n")
            if a > len(rows) or b > len(rows):
                stale.append((doc, raw, a, b, "past EOF (%d lines)" % len(rows), ""))
                continue
            ea, eb = element_at(rows, a), element_at(rows, b)
            if ea == eb:
                fine += 1
            else:
                stale.append((doc, raw, a, b, ea, eb))

    print("ranges inside one element:", fine)
    print("ranges whose ends are in DIFFERENT elements (stale):", len(stale))
    for doc, raw, a, b, ea, eb in stale[:12]:
        print("  %-26s %s:%d-%d" % (doc, raw, a, b))
        print("      start is in: %s" % ea)
        print("      end   is in: %s" % eb)
    return 0


if __name__ == "__main__":
    sys.exit(main())
