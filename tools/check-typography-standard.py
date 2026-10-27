#!/usr/bin/env python3
"""Typography standard: one type scale, one spacing scale, no free-floating numbers.

The app has two token families in `Styles.xaml`, and both are complete:

    spacing   MpvSpaceXxs 2, MpvSpaceXs 4, MpvSpaceCompact 6, MpvSpaceS 8, MpvSpaceM 12,
              MpvSpaceL 16
    type      MpvPageHeadingFontSize 40, MpvPanelTitleFontSize 20, MpvShellTitleFontSize 16,
              MpvPopupLabelFontSize 14, MpvPopupTitleFontSize 12,
              MpvTransportTimeFontSize 12, MpvPopupCaptionFontSize 11,
              MpvBreadcrumbLineHeight 32

A literal that duplicates one of them is not a rendering bug -- the pixels are identical today.
It is a maintenance defect with a specific, measured cost: change the token and every literal
that meant the same thing silently keeps the old value, so the two drift apart and the drift is
invisible in review. That is the whole reason a token exists.

What is checked, in `Modules/` and `Shell/`:

  SPACING-LITERAL   `Margin`/`Padding`/`Spacing`/`RowSpacing`/`ColumnSpacing` carrying a single
                    number that equals a `MpvSpace*` token.
  FONT-LITERAL      `FontSize` on a TEXT element (not an icon -- the icon checker owns those)
                    equal to a type token.
  LINEWEIGHT-LITERAL  `LineHeight` equal to `MpvBreadcrumbLineHeight`.

Multi-value shorthands (`Margin="8,0,8,0"`) are NOT flagged: replacing one number in a shorthand
with a resource means rewriting the whole attribute, and the shorthand expresses a relationship
(here "8 horizontally, nothing vertically") that a token cannot. Flagging them would produce
hundreds of findings the author would rightly ignore, which is how a checker teaches people to
ignore checkers.

Known findings live in `tools/typography-baseline.txt`; anything new fails the run.
"""
import argparse
import pathlib
import re
import sys

REPO = pathlib.Path(__file__).resolve().parent.parent
APP_DIR = REPO / "src" / "mpvw-app"
STYLES = APP_DIR / "Styles" / "Styles.xaml"
BASELINE_FILE = REPO / "tools" / "typography-baseline.txt"

SPACING_TOKENS = ["MpvSpaceXxs", "MpvSpaceXs", "MpvSpaceCompact", "MpvSpaceS", "MpvSpaceM",
                  "MpvSpaceL"]
FONT_TOKENS = ["MpvPageHeadingFontSize", "MpvPanelTitleFontSize", "MpvShellTitleFontSize",
               "MpvPopupLabelFontSize", "MpvPopupTitleFontSize", "MpvTransportTimeFontSize",
               "MpvPopupCaptionFontSize"]
LINEHEIGHT_TOKENS = ["MpvBreadcrumbLineHeight"]

SPACING_ATTRS = ("Margin", "Padding", "Spacing", "RowSpacing", "ColumnSpacing")

# A text element: FontSize here is type, not an icon.
TEXT_ELEMENTS = ("TextBlock", "RichTextBlock", "TextBox", "AutoSuggestBox", "TextCommandBarFlyout")


def read_tokens():
    text = STYLES.read_text(encoding="utf-8", errors="replace")
    out = {}
    for name in SPACING_TOKENS + FONT_TOKENS + LINEHEIGHT_TOKENS:
        m = re.search(r'x:Key="%s"[^>]*>\s*([0-9.]+)\s*<' % name, text)
        if m:
            out[name] = float(m.group(1))
    return out


def scan_files():
    out = []
    for sub in ("Modules", "Shell"):
        base = APP_DIR / sub
        if base.is_dir():
            out.extend(sorted(base.rglob("*.xaml")))
    return out


def attr_values(text, name):
    """Every value of `name="..."`, with its offset. Quote-aware so `=` inside is safe."""
    for m in re.finditer(r'\b%s="([^"]*)"' % name, text):
        yield m.group(1), m.start()


def check_file(path, tokens):
    text = path.read_text(encoding="utf-8", errors="replace")
    findings = []

    spacing_values = {tokens[n] for n in SPACING_TOKENS if n in tokens}
    font_values = {tokens[n] for n in FONT_TOKENS if n in tokens}
    line_values = {tokens[n] for n in LINEHEIGHT_TOKENS if n in tokens}

    def report(offset, kind, msg):
        findings.append((text.count("\n", 0, offset) + 1, kind, msg))

    # ---- SPACING ----
    for attr in SPACING_ATTRS:
        for value, offset in attr_values(text, attr):
            # Single number only: a shorthand carries a relationship a token cannot express.
            if not re.fullmatch(r"[0-9.]+", value.strip()):
                continue
            number = float(value)
            if number in spacing_values:
                name = next(n for n in SPACING_TOKENS if tokens.get(n) == number)
                report(offset, "SPACING-LITERAL",
                       '%s="%s" duplicates %s' % (attr, value, name))

    # ---- FONT SIZE on text ----
    # Only when the nearest enclosing element start is a text element, so an icon's FontSize is
    # left to check-icon-standard.py.
    for value, offset in attr_values(text, "FontSize"):
        if not re.fullmatch(r"[0-9.]+", value.strip()):
            continue
        number = float(value)
        if number not in font_values:
            continue
        head = text.rfind("<", 0, offset)
        if head < 0:
            continue
        tag = re.match(r"<([\w:]+)", text[head:])
        if tag is None or tag.group(1).split(":")[-1] not in TEXT_ELEMENTS:
            continue
        name = next(n for n in FONT_TOKENS if tokens.get(n) == number)
        report(offset, "FONT-LITERAL", 'FontSize="%s" duplicates %s' % (value, name))

    # ---- LINE HEIGHT ----
    for value, offset in attr_values(text, "LineHeight"):
        if not re.fullmatch(r"[0-9.]+", value.strip()):
            continue
        if float(value) in line_values:
            name = next(n for n in LINEHEIGHT_TOKENS if tokens.get(n) == float(value))
            report(offset, "LINEHEIGHT-LITERAL",
                   'LineHeight="%s" duplicates %s' % (value, name))

    return findings


def load_baseline():
    if not BASELINE_FILE.exists():
        return set()
    out = set()
    for line in BASELINE_FILE.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if line and not line.startswith("#"):
            out.add(line)
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--verbose", action="store_true")
    ap.add_argument("--update-baseline", action="store_true")
    args = ap.parse_args()

    tokens = read_tokens()
    if not tokens:
        print("FAIL: no typography tokens readable from Styles.xaml")
        return 1

    files = scan_files()
    if not files:
        print("FAIL: no XAML found under Modules/ or Shell/")
        return 1

    findings = []
    for path in files:
        for lineno, kind, msg in check_file(path, tokens):
            try:
                key = "%s:%d:%s" % (path.relative_to(REPO).as_posix(), lineno, kind)
            except ValueError:
                key = "%s:%d:%s" % (path, lineno, kind)
            findings.append((path, lineno, kind, msg, key))

    if args.update_baseline:
        lines = ["# Findings already measured and accepted for now. One per line:",
                 "#   <repo-relative path>:<line>:<kind>",
                 "# Regenerate with: python tools/check-typography-standard.py --update-baseline",
                 ""]
        lines.extend(f[4] for f in findings)
        BASELINE_FILE.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\r\n")
        print("baseline written: %d entr(ies)" % len(findings))
        return 0

    baseline = load_baseline()
    fresh = [(p, l, k, m) for p, l, k, m, key in findings if key not in baseline]
    known = len(findings) - len(fresh)

    if args.verbose:
        print("tokens: " + ", ".join("%s=%g" % (k, v) for k, v in sorted(tokens.items())))

    if not fresh:
        print("OK: %d XAML file(s), no NEW typography violations (%d known)"
              % (len(files), known))
        return 0

    print("FAIL: %d NEW typography violation(s) (%d known in the baseline)"
          % (len(fresh), known))
    by_kind = {}
    for path, lineno, kind, msg in fresh:
        by_kind.setdefault(kind, []).append((path, lineno, msg))
    for kind in sorted(by_kind):
        print("\n%s (%d):" % (kind, len(by_kind[kind])))
        for path, lineno, msg in by_kind[kind][:20]:
            try:
                rel = path.relative_to(REPO)
            except ValueError:
                rel = path
            print("  %s:%d  %s" % (rel, lineno, msg))
    return 1


if __name__ == "__main__":
    sys.exit(main())
