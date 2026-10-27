"""Report doc references that point at nothing -- excluding ones that describe history.

Two false alarms in the first version, both worth encoding as rules because a checker
that cries wolf gets ignored and then misses the real thing:

  1. A `file:line` inside a row already marked fixed (the design contract's A-28 says
     "MpvConfManager.cs:65,67 调用 ManagedMpvConfig.MpvConfPath ... ✅ 已修"). Those
     line numbers describe the code BEFORE the fix, and they are correct history: the
     commit that added `onBeforeSave` had exactly those two calls on those two lines,
     verified with `git show <commit>^:...`. Reporting them as stale would push someone
     to "correct" a record that is right.

  2. A reference to the UPSTREAM file. compare-upstream.md cites
     MpvPlayerPage_Input.cs:60 and spells out that this is the upstream filename while
     the local one is MpvPlayerPage_InputForwarding.cs. A file that does not exist here
     by design is not a broken link.

So: a reference is only reported when it is in a row that is NOT marked fixed and NOT
described as upstream, and the file it names resolves ambiguously or not at all. The
symbol check is dropped entirely -- the previous version reported 11 cases and every
one was a false positive from a citation sentence that names several symbols.
"""
import io
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOCS = [r"docs\settings-contract.md", r"docs\mpv-conf.md",
        r"docs\deploy-layout.md", r"docs\localization.md",
        r"docs\compare-upstream.md", r"docs\upstream-gap.md"]

REF = re.compile(r"([A-Za-z0-9_\-./\\]+\.(?:cs|xaml|py|ps1|lua|idl|json|md))"
                 r":(\d+)(?:[,\-/](\d+))?")

FIXED = ("已修", "已删除", "已作废", "已解决", "✅")
UPSTREAM = ("上游", "upstream", "ikas-mc", "原版")


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

            # Look at the enclosing bullet too, not just the physical line: a
            # citation is often on its own line under a lead-in that says what it
            # refers to ("上游对这两个函数的全部调用点都在 ..." followed by the
            # filenames). Checking only the line reported the upstream reference in
            # compare-upstream.md as missing, four times over four runs.
            para_start = max(text.rfind("\n\n", 0, m.start()) + 2, m.start() - 400)
            context = text[para_start:le if le > 0 else len(text)]
            checked += 1

            if any(tag in context for tag in FIXED):
                continue
            if any(tag in context for tag in UPSTREAM):
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
