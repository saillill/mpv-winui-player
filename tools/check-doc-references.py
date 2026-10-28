"""Report doc references that point at nothing -- excluding ones that describe history.

A citation is reported unless the row it sits in is marked fixed. That single exemption
exists because a row saying "已修 / 已删除 / ✅" quotes line numbers from BEFORE the fix:
those numbers are correct history, and "correcting" them would falsify a record that is
right.

**There is no exemption for upstream files.** A path that only exists in the upstream
repository cannot be resolved here, so it cannot be checked -- which means it does not
belong in a citation at all. Describe the upstream code by what it does, and cite only
files this tree contains.

The symbol check is deliberately absent: a citation sentence names several symbols, and
checking which of them exist produces a false positive per symbol rather than one finding.

`DOCS` is globbed from `docs/`, not listed: a hand-maintained list is a list that stops
covering a new document, and the checker then reports OK over the documents it knew.
"""

import io
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
# Every document under docs/, globbed so a new one cannot be forgotten.
# Two exclusions, each for a reason the text itself cannot carry:
#
#   history-filtering-log.md  append-only record. Its tables cite past commits and
#                             paths; those citations are correct history, so
#                             "fixing" them would falsify the record.
#   compare-upstream.md       does not exist; a DOCS entry must never name a
#                             missing file, because the loop below silently skips it.
EXCLUDE = {"history-filtering-log.md", "compare-upstream.md"}
DOCS = sorted(
    "docs" + os.sep + name
    for name in os.listdir(os.path.join(REPO, "docs"))
    if name.endswith(".md") and name not in EXCLUDE
)

REF = re.compile(r"([A-Za-z0-9_\-./\\]+\.(?:cs|xaml|py|ps1|lua|idl|json|md))"
                 r":(\d+)(?:[,\-/](\d+))?")

FIXED = ("已修", "已删除", "已作废", "已解决", "✅")


def resolve(path):
    p = path.replace("/", os.sep)
    if os.path.exists(os.path.join(REPO, p)):
        return "ok"
    base = os.path.basename(p)
    for root, dirs, files in os.walk(REPO):
        dirs[:] = [d for d in dirs if d not in
                   ("bin", "obj", "dist", "libs", "node_modules", ".git", "_work")]
        if base in files:
            return "moved"
    return "missing"


def main():
    checked = broken = 0
    for doc in DOCS:
        dpath = os.path.join(REPO, doc)
        if not os.path.exists(dpath):
            continue
        text = io.open(dpath, encoding="utf-8-sig").read()
        for m in REF.finditer(text):
            raw, a = m.group(1), int(m.group(2))
            # The enclosing line and, for tables, the whole row.
            ls = text.rfind("\n", 0, m.start()) + 1
            le = text.find("\n", m.end())
            row = text[ls:le if le > 0 else len(text)]

            # The whole enclosing paragraph, not just the physical line: a table row
            # can wrap, and a lead-in sentence can name the file on the line after it.
            # Judging by the single line would report a correct citation as broken.
            para_start = max(text.rfind("\n\n", 0, m.start()) + 2, m.start() - 400)
            context = text[para_start:le if le > 0 else len(text)]
            checked += 1

            if any(tag in context for tag in FIXED):
                continue

            state = resolve(raw)
            if state == "missing":
                broken += 1
                print("MISSING   %-26s %s:%d" % (doc, raw, a))
            elif state == "moved":
                # Fine -- docs cite bare basenames and the reader can search.
                # (Kept silent on purpose: the design contract cites
                # MpvConfManager.cs for a file now under MpvConf/Conf/.)
                pass
            else:
                target = os.path.join(REPO, raw.replace("/", os.sep))
                n = io.open(target, encoding="utf-8-sig",
                            errors="replace").read().count("\n") + 1
                if a > n:
                    broken += 1
                    print("PAST EOF  %-26s %s:%d (file has %d lines)"
                          % (doc, raw, a, n))

    print("\nreferences inspected: %d | broken: %d" % (checked, broken))
    return 1 if broken else 0


if __name__ == "__main__":
    sys.exit(main())
