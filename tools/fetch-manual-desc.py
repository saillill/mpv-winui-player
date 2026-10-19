#!/usr/bin/env python3
"""Extract one line of prose per mpv option from the official manual.

The mpv manual is a single page: every option's `link` in mpv-options.json is an
anchor on https://mpv.io/manual/master/. So one download describes all of them,
and the result is checked in as MpvConfOptions/manual-desc.en.json so the
generator does not need the network.

Why this file exists at all: mpv-options.json carries no prose. Its `desc` is a
type line -- "Float; Range 0 to 6; Default: 0" -- which says what a row accepts
and nothing about what it does. The manual has the sentence, so the sentence is
what the settings window shows.

    python tools/fetch-manual-desc.py            # refresh the checked-in file
    python tools/fetch-manual-desc.py --check    # fail if it would change
"""
import argparse
import html
import json
import pathlib
import re
import sys
import urllib.request

REPO = pathlib.Path(__file__).resolve().parent.parent
APP = REPO / "mpv-winui" / "mpv-winui"
SCHEMA = APP / "MpvConfOptions" / "mpv-options.json"
OUT = APP / "MpvConfOptions" / "manual-desc.en.json"
MANUAL = "https://mpv.io/manual/master/"

# Each option lives in a <dt> followed by a <dd> whose first paragraph is the
# description. A <dt> often names several options at once -- the whole
# --vd-lavc-skip* family shares one entry -- so every name in the <dt> is given
# that paragraph. Taking only the first name is what left those options
# described by nothing. The second paragraph is usually a caveat, so only the
# first is taken: one sentence of what it does beats two of what can go wrong.
ENTRY = re.compile(r"<dt>(.*?)</dt>\s*<dd>(.*?)</dd>", re.S)
NAME = re.compile(r'<span class="pre">--([a-z0-9][a-z0-9\-]*)')
TT = re.compile(r"<tt[^>]*>(.*?)</tt>", re.S)
SPAN = re.compile(r'<span class="pre">.*?</span>', re.S)


def plain(fragment: str) -> str:
    """Tags stripped, entities decoded, whitespace collapsed."""
    text = re.sub(r"<[^>]+>", "", fragment)
    return re.sub(r"\s+", " ", html.unescape(text)).strip()


def is_example(dt: str) -> bool:
    """Whether this <dt> is a command line rather than an option signature.

    The manual mixes two things in the same <dt>/<dd> shape. A signature names
    one or more options and nothing else; an example is a whole command line.
    Measured on 2026-10-10, taking both made `--end` and `--length` describe
    themselves with the example text: the entry `--start=30 --end=40` appears
    earlier in the document than `--end=<relative time>`, and first-mention-wins
    therefore handed `end` its example. The row then read "Seeks to 30 seconds,
    plays 10 seconds, and exits" -- a fact about that one command line, not
    about the option.

    The two are told apart by how mpv joins several options inside one <tt>:
    a signature separates them with a comma (which lands either between the
    spans or at the end of the first one), an example separates them with a
    space. So `--ab-loop-a=<time>, --ab-loop-b=<time>` stays, and
    `--start=30 --end=40`, `mpv --af=... --speed=1.2`, `sub-add <url> [...]`
    go. The skipped set is 21 entries in the current manual and contains no
    option signature -- verified by diffing the output, which changes exactly
    the two descriptions above and neither loses nor gains a key.
    """
    for block in TT.findall(dt):
        spans = list(SPAN.finditer(block))
        for a, b in zip(spans, spans[1:]):
            between = plain(block[a.end():b.start()])
            if between != "," and not plain(block[a.start():a.end()]).endswith(","):
                return True
    return False


def first_paragraph(block: str) -> str:
    return plain(block.split("</p>", 1)[0])


def build() -> dict:
    # A User-Agent is required: mpv.io answers the default urllib one with 403,
    # which reads as "the manual moved" rather than "the request looked odd".
    request = urllib.request.Request(MANUAL, headers={"User-Agent": "curl/8"})
    with urllib.request.urlopen(request, timeout=60) as response:
        raw = response.read().decode("utf-8", errors="replace")

    descs = {}
    for match in ENTRY.finditer(raw):
        if is_example(match.group(1)):
            continue
        text = first_paragraph(match.group(2))
        if not text:
            continue
        for name in NAME.findall(match.group(1)):
            # First mention wins: names repeat in cross-references and in the
            # changelog appendix.
            descs.setdefault(name, text)

    schema = json.loads(SCHEMA.read_text(encoding="utf-8-sig"))
    wanted = sorted({e["name"] for e in schema})
    return {n: descs[n] for n in wanted if n in descs}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    descs = build()
    text = json.dumps(descs, ensure_ascii=False, indent=1) + "\n"

    if args.check:
        current = OUT.read_text(encoding="utf-8") if OUT.exists() else ""
        print("OK: manual-desc.en.json up to date" if current == text else "FAIL: stale")
        return 0 if current == text else 1

    OUT.write_text(text, encoding="utf-8")
    print(f"wrote {OUT.relative_to(REPO)}  ({len(descs)} option(s))")
    return 0


if __name__ == "__main__":
    sys.exit(main())
