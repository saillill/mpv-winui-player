"""Do the docs cite tools and paths that a clean checkout actually has?

`check-doc-references.py` validates `file:line` citations against the working
tree. That is the wrong question for anything a reader is told to RUN or READ:
the working tree on this machine also holds material that `.gitignore` keeps out
of the repository, so a citation can resolve here and be a dead end for everyone
else. Four documents named such paths before this checker existed:

    docs/verification.md:51            tools/ui-repair/gate.py
    docs/settings-contract.md:232      tools/mvpw-mcp/
    data/docs/preview-api.md:97        tools/mvpw-mcp/verify_preview_api.py
    data/docs/README.md               docs/audit-and-roadmap-20260828.md

Every one of them resolved locally, so no existing checker could see the
problem. The question this one asks is `git ls-files`, not `os.path.exists`.

What counts as a citation: a `tools/...` or `docs/...` path with a file
extension, in any document the documentation checkers already own (the same
roots `check-doc-freshness.py` uses). History records are exempt for the same
reason they are exempt there: they cite what existed when they were written.

Exit codes
  0  every cited path is tracked
  1  a cited path is untracked, ignored, or missing
  2  no documents matched, or no citations were found -- a scan that stopped
     resolving must not read as a clean bill of health
"""
from __future__ import annotations

import os
import re
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# Append-only or self-declared history: its paths are correct as history.
EXCLUDE_DOCS = {
    "history-filtering-log.md",
}

CITATION = re.compile(
    r"(?:^|[\s`'\"(])((?:tools|docs|src|tests|data)/[A-Za-z0-9_./\\-]+"
    r"\.(?:py|ps1|sh|md|json|csproj|vcxproj|yml|yaml))"
)

# A path inside a fenced block is a command the reader is told to run, which is
# exactly the case this checker exists for, so fences are NOT skipped.


def documents() -> list[str]:
    """docs/*.md, README.md, src/**/README.md, data/docs/*.md.

    The same roots check-doc-freshness.py polices, so a document cannot be
    governed by one checker and ungoverned by this one.
    """
    out: list[str] = []
    docs = os.path.join(REPO, "docs")
    if os.path.isdir(docs):
        for name in sorted(os.listdir(docs)):
            if name.endswith(".md") and name not in EXCLUDE_DOCS:
                out.append(os.path.join("docs", name))
    for name in ("README.md",):
        if os.path.isfile(os.path.join(REPO, name)):
            out.append(name)
    for root, dirs, files in os.walk(os.path.join(REPO, "src")):
        dirs[:] = [d for d in dirs if d.lower() not in {"bin", "obj", "packages"}]
        for f in files:
            if f.lower() == "readme.md":
                out.append(os.path.relpath(os.path.join(root, f), REPO))
    data_docs = os.path.join(REPO, "data", "docs")
    if os.path.isdir(data_docs):
        for f in sorted(os.listdir(data_docs)):
            if f.endswith(".md"):
                out.append(os.path.join("data", "docs", f))
    return out


def tracked() -> set[str]:
    """Every path git knows about, normalised to forward slashes."""
    try:
        res = subprocess.run(
            ["git", "ls-files", "-z"],
            cwd=REPO,
            capture_output=True,
            check=True,
        )
    except (OSError, subprocess.CalledProcessError) as exc:
        print(f"FAIL: cannot list tracked files ({exc}). A scan that cannot tell")
        print("      tracked from untracked must not report success.")
        raise SystemExit(2)
    return {p.replace("\\", "/") for p in res.stdout.decode("utf-8").split("\0") if p}


def main() -> int:
    known = tracked()
    docs = documents()

    if not docs:
        print("FAIL: no documents found -- the scan paths are wrong, not the docs")
        return 2

    broken: list[tuple[str, int, str, str]] = []
    seen = 0
    for doc in docs:
        path = os.path.join(REPO, doc)
        try:
            text = open(path, encoding="utf-8-sig", errors="replace").read()
        except OSError as exc:
            print(f"FAIL: cannot read {doc}: {exc}")
            return 2
        for lineno, line in enumerate(text.splitlines(), 1):
            # A line that records a fix or a deletion is describing the past.
            if any(mark in line for mark in ("已修", "已删除", "已作废", "✅")):
                continue
            for m in CITATION.finditer(line):
                ref = m.group(1).replace("\\", "/").rstrip(".,;:)")
                seen += 1
                if ref in known:
                    continue
                if os.path.exists(os.path.join(REPO, ref)):
                    reason = "exists here but is NOT tracked (ignored by .gitignore)"
                else:
                    reason = "does not exist"
                broken.append((doc, lineno, ref, reason))

    if seen == 0:
        print("FAIL: parsed zero citations -- the pattern stopped matching, which")
        print("      must not read as 'nothing is broken'.")
        return 2

    if broken:
        for doc, lineno, ref, reason in broken:
            print(f"{doc}:{lineno} cites {ref} -- {reason}")
        print()
        print(f"FAIL: {len(broken)} of {seen} citation(s) point at something a")
        print("      clean checkout does not contain.")
        print("Either track the file, or describe it in words instead of a path.")
        return 1

    print(f"OK: {seen} tool/doc citation(s) across {len(docs)} document(s), all tracked")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    raise SystemExit(main())
