#!/usr/bin/env python3
"""Check that every .xaml file is well-formed XML.

XAML is XML, so the standard parser can tell us two things the markup compiler
would otherwise only tell us at build time -- and this repo has shipped a
mistake of the second kind before:

  * unbalanced or mismatched tags;
  * "--" inside a comment. XML forbids a double hyphen anywhere in a comment
    body, and the markup compiler rejects the file outright. It is easy to
    introduce by writing a dash-heavy sentence in prose, and impossible to
    notice by reading.

Neither is a style opinion, so both are errors here. Run this before a build
when the machine cannot run one.

Exit code 1 on any problem.
"""

import re
import sys
import xml.etree.ElementTree as ET

from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
ROOT = REPO / "mpv-winui"

COMMENT = re.compile(r"<!--(.*?)-->", re.S)


def main():
    problems = 0

    for path in sorted(ROOT.rglob("*.xaml")):
        rel = path.relative_to(REPO)
        text = path.read_text(encoding="utf-8", errors="replace")

        try:
            ET.fromstring(text)
        except ET.ParseError as exc:
            # position can point one past the last line (an unclosed root ends
            # the document rather than the file), so the context lookup has to
            # be bounds-checked or the report itself raises.
            line_no = exc.position[0] if exc.position else 0
            lines = text.splitlines()
            context = lines[line_no - 1] if 0 < line_no <= len(lines) else ""
            print(f"ERROR: {rel}:{line_no}: not well-formed: {exc}", file=sys.stderr)
            if context:
                print(f"       {context.strip()[:120]}", file=sys.stderr)
            problems += 1

        for match in COMMENT.finditer(text):
            if "--" in match.group(1):
                line_no = text.count("\n", 0, match.start()) + 1
                body = " ".join(match.group(1).split())[:90]
                print(f"ERROR: {rel}:{line_no}: '--' is not allowed inside a "
                      f"comment: {body}", file=sys.stderr)
                problems += 1

    if problems:
        return 1

    count = len(list(ROOT.rglob("*.xaml")))
    print(f"OK: {count} XAML files well-formed, no illegal comment content")
    return 0


if __name__ == "__main__":
    sys.exit(main())
