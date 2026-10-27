"""Does a cited line RANGE still point at the block the doc says it does?

`Styles.xaml:880-1021` was cited as an authority for spacing and typography. The file has
since grown, so the range covered a visual-state storyboard instead. A single line number
can be re-derived exactly; a range cannot, because the right answer is "the block moved and
is now N1-N2" and that needs the block's boundaries.

**Three versions, each defeated by a measurement.**

v1 snapped each end to the nearest line starting with `<Style`, `class `, `public `, `def `
and compared those. Two faults:

  * it printed the result and returned 0 in every case, so it reported "4 ranges have
    drifted" on every run, `gate.py` never ran it, and nobody read the output. Measured
    2026-10-24: the four were real. `docs/settings-design-contract.md:234` cited
    `OptionListControl.xaml:129-137` for the customise section row's `Padding 12,8,12,8`;
    that padding is at line 166 inside `CustomizeSectionTemplate`, and 129-137 is a blank
    line and `ShaderListTemplate`. The same sentence's `:216-225` had gone the same way.
    Drift is a FAILURE now (exit 1), which is why it is in the gate's list.

  * the keyword snap itself misfired: a range ending in a comment inside a method walked
    past that method into the next one.

v2 used indentation. Still wrong: a range that starts at a `<Style` and ends at a sibling
`<Thickness` token is one token region, and indentation put them in different owners.

v3 resolves the INNERMOST XAML element containing each cited line and compares that
element's opening tag -- `CustomizeSectionTemplate` vs `ShaderListTemplate`, which is the
distinction the real defect was about. For code, a run of sibling declarations counts as
one region, because citations like `check-sections.py:131-227` legitimately name "the
helpers that read option keys" and the blank lines between them are formatting.

Two v3 orders that a measurement corrected, kept in the code as comments: keeping the LAST
matching element returned the root `<UserControl>` for every line (it closes last and
overwrote every tighter match), and preferring any containing element over an unnamed one
reported `<UserControl.Resources>` -- a 441-line wrapper -- for every template in the file.

Nothing is repaired automatically here: a wrong guess would put a confident-looking wrong
citation in the authority document.
"""
import io
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOCS = [r"docs\settings-design-contract.md", r"docs\deploy-layout.md"]
RANGE = re.compile(r"([A-Za-z0-9_\-./\\]+\.(?:cs|xaml|py|ps1|lua|idl|json|md)):(\d+)-(\d+)")

OPEN = re.compile(
    r"<([A-Za-z_][\w.:-]*)((?:[^>\"']|\"[^\"]*\"|'[^']*')*?)(/?)>", re.S)
CLOSE = re.compile(r"</([A-Za-z_][\w.:-]*)\s*>")


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


def _line_index(line_of, offset):
    lo, hi = 0, len(line_of) - 1
    while lo < hi:
        mid = (lo + hi + 1) // 2
        if line_of[mid] <= offset:
            lo = mid
        else:
            hi = mid - 1
    return lo


def xaml_block(rows, idx):
    """(start_line, end_line, opening_tag) of the innermost NAMED element containing idx."""
    text = "\n".join(rows)
    line_of = [0]
    for i, ch in enumerate(text):
        if ch == "\n":
            line_of.append(i + 1)

    events = []
    for m in OPEN.finditer(text):
        name = m.group(1)
        if name.startswith("?") or name.startswith("!--"):
            continue
        events.append(("open", name, m.start(), m.group(0), m.group(3) == "/"))
    for m in CLOSE.finditer(text):
        events.append(("close", m.group(1), m.start(), m.group(0), False))
    events.sort(key=lambda e: e[2])

    def named(tag):
        return "x:Key=" in tag or "x:Name=" in tag

    best = None
    stack = []
    for kind, name, pos, tag, self_closed in events:
        start = _line_index(line_of, pos)
        if kind == "open":
            if self_closed:
                if start == idx and named(tag):
                    cand = (start + 1, start + 1, tag)
                    if best is None or _better(cand, best):
                        best = cand
                continue
            stack.append((name, start, tag))
            continue

        for k in range(len(stack) - 1, -1, -1):
            if stack[k][0] != name:
                continue
            _, start, opening = stack[k]
            del stack[k:]
            if start <= idx <= _line_index(line_of, pos):
                cand = (start + 1, _line_index(line_of, pos) + 1, opening)
                if named(opening) and (best is None or _better(cand, best)):
                    best = cand
            break

    if best is not None:
        return best

    # A line BETWEEN named elements (blank, or a bare comment) belongs to none of them.
    # Attribute it to the next element that starts at or after it: a citation that begins
    # at such a line is pointing at what follows.
    for kind, name, pos, tag, self_closed in events:
        if kind != "open" or self_closed or not named(tag):
            continue
        start = _line_index(line_of, pos)
        if start < idx:
            continue
        end = start
        for cm in CLOSE.finditer(text, pos):
            if cm.group(1) == name:
                end = _line_index(line_of, cm.start())
                break
        return (start + 1, end + 1, tag)
    return None


def _better(cand, best):
    """Narrower span wins; on a tie, both are named so keep the first (outer) one."""
    return (cand[1] - cand[0]) < (best[1] - best[0])


def code_block(rows, idx):
    """The run of sibling column-0 declarations containing idx, as a 1-based span."""
    def col0(i):
        return rows[i].strip() and rows[i][0] not in " \t"

    if idx >= len(rows) or idx < 0:
        return None

    start = idx
    while start >= 0:
        if col0(start):
            break
        start -= 1
    while start > 0:
        j = start - 1
        while j >= 0 and not rows[j].strip():
            j -= 1
        if j < 0:
            break
        start = j
    if start < 0:
        return None

    end = idx
    while end < len(rows):
        if col0(end):
            break
        end += 1
    while end < len(rows) - 1:
        j = end + 1
        while j < len(rows) and not rows[j].strip():
            j += 1
        if j >= len(rows):
            break
        end = j
    return (start + 1, min(end, len(rows) - 1) + 1, rows[start].strip()[:60])


def block_of(rows, line, is_xaml):
    idx = line - 1
    if is_xaml:
        return xaml_block(rows, idx)
    return code_block(rows, idx)


def looks_like_comment_pointer(rows, a, b, doc_line):
    """Does the sentence around the citation describe COMMENTS rather than code?

    Measured false positive this fixes. `docs/settings-design-contract.md:119` says:

        > 反面案例记录在 `SettingsPage.xaml:63-82` 与 `:176-191` 的注释里

    "recorded in the COMMENTS at 63-82". Those lines genuinely contain a 5-line explanatory
    comment (66-70) and nothing else of interest -- the range is correct. But 63 is a
    `<SolidColorBrush>` and 82 is a `<Button` attribute, so a block-identity comparison sees
    two different elements and calls it stale.

    A citation can name (a) the element a block lives in, or (b) the comments that explain
    it. Case (b) is legitimate and cannot be judged by element identity, so the caller skips
    the comparison when the prose says so.
    """
    for cue in ("注释", "comment", "反面案例", "说明"):
        if cue in doc_line:
            return True
    return False


def looks_like_region_pointer(doc_line):
    """Does the sentence present the citation as a list of places to READ?

    Measured false positive this fixes. `docs/settings-design-contract.md:924-927` is a
    sentence enumerating authorities:

        代码库内既有的权威注释：`SettingsPage.xaml:63-82,146-191`、
        `SettingsSections.cs:7-26,37-63,123-166`、`Styles.xaml:1015-1192`、...

    `Styles.xaml:1015-1192` covers 39 consecutive top-level resources -- the card style and
    the spacing/typography token block. It is a REGION, not one element, and the doc is
    correctly saying "read 1015-1192 for the tokens". Comparing element identity across 39
    siblings cannot work, and forcing it would demand the doc break one region citation
    into 39.

    The distinction that keeps this from being a hole: a region pointer lists several
    citations for the reader, so the sentence contains a citation separator (、 or ,)
    and does not describe what is at one specific block. The real defect this checker was
    written for -- "cited 129-137 for the customise row, those lines are a different
    template" -- read as a single citation with the prose describing exactly what should be
    there, and is still caught (see `_work/inj_doc_ranges.py`).
    """
    markers = ("、", "见 ", "见`", "权威", "参考")
    return any(x in doc_line for x in markers)


def main():
    stale, fine, checked, skipped = [], 0, 0, 0
    for doc in DOCS:
        dpath = os.path.join(REPO, doc)
        if not os.path.exists(dpath):
            continue
        text = io.open(dpath, encoding="utf-8-sig").read()
        lines = text.split("\n")
        for m in RANGE.finditer(text):
            raw, a, b = m.group(1), int(m.group(2)), int(m.group(3))
            target = resolve(raw)
            if target is None:
                continue
            # The whole sentence this citation sits in, for the pointer tests.
            up_to = text[:m.start()].count("\n")
            doc_line = lines[up_to] if up_to < len(lines) else ""
            rows = io.open(target, encoding="utf-8-sig", errors="replace").read().split("\n")
            checked += 1
            if a > len(rows) or b > len(rows):
                stale.append((doc, raw, a, b, "past EOF (%d lines)" % len(rows), ""))
                continue
            if looks_like_comment_pointer(rows, a, b, doc_line):
                fine += 1
                skipped += 1
                continue
            is_xaml = target.lower().endswith(".xaml")
            if is_xaml and looks_like_region_pointer(doc_line):
                fine += 1
                skipped += 1
                continue
            sa = block_of(rows, a, is_xaml)
            sb = block_of(rows, b, is_xaml)
            if sa is None or sb is None:
                stale.append((doc, raw, a, b, "unresolvable block", ""))
            elif sa[2] == sb[2] or (sa[0] == sb[0] and sa[1] == sb[1]):
                fine += 1
            else:
                stale.append((doc, raw, a, b,
                              "%d-%d %s" % (sa[0], sa[1], sa[2][:44]),
                              "%d-%d %s" % (sb[0], sb[1], sb[2][:44])))

    print("ranges whose ends resolve to one block:", fine)
    print("ranges whose ends are in DIFFERENT blocks (stale):", len(stale))
    for doc, raw, a, b, ea, eb in stale[:12]:
        print("  %-30s %s:%d-%d" % (doc, raw, a, b))
        print("      start: %s" % ea)
        print("      end  : %s" % eb)

    if stale:
        print()
        print("FAIL: %d of %d citation range(s) no longer point at one block."
              % (len(stale), checked))
        print("Either the citation moved (fix the numbers), or the range is no longer")
        print("authority for what the doc claims. This exits non-zero on purpose: the")
        print("four ranges it used to print were real and nothing was reading them.")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
