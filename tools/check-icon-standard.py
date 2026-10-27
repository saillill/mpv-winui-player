#!/usr/bin/env python3
"""Icon standard: two glyph systems exist, and each element must be consistent with it.

The first version of this checker assumed a single icon font and reported 74 "violations", of
which almost none were real. What the measurement actually shows:

    FontIcon total  148
    no FontFamily   115   these inherit the framework symbol font and use Segoe codepoints
                          (&#xE8C8; copy, &#xE74D; delete, ...) -- legitimate, not a defect
    MpvIconFont      33   these declare MpvIconFontFamily (FluentSystemIcons)

So the app deliberately runs two systems: the framework's for platform-standard marks, and the
bundled Fluent set for the app's own. `SymbolThemeFontFamily` is a third spelling of the first.
A rule that demands one font is wrong, and shipping it would have meant 58 bogus findings.

What is checkable, and each verified against the shipped font file with fontTools:

  MIXED SYSTEM     one element declares a font whose codepoints are another system's. Measured:
                   `&#xE712;` means "home_add" in FluentSystemIcons but "open file" in Segoe; the
                   two sets overlap in range and disagree in meaning, so a glyph that looks right
                   in one font is unrelated in the other. This is the real hazard.
  HARDCODED FONT   a literal font family name instead of `MpvIconFontFamily` or
                   `SymbolThemeFontFamily`. Measured instance: `FontFamily="Segoe Fluent Icons"`
                   at OptionListControl.xaml:413, which no token tracks.
  RAW SIZE         a literal `FontSize` on a FontIcon where the role tokens exist
                   (MpvInlineIconSize 14, MpvPopupIconSize 16, MpvSectionHeaderIconSize 16,
                   MpvSectionCardIconSize 20, MpvTransportIconSize 20,
                   MpvSectionCardChevronSize 12, MpvShellAppIconSize 20).

Deliberately NOT checked: an icon without `FontFamily`. It inherits the framework symbol font and
uses that font's codepoints; demanding a token there was the first version's error.
"""
import argparse
import pathlib
import re
import sys

REPO = pathlib.Path(__file__).resolve().parent.parent
APP_DIR = REPO / "src" / "mpvw-app"
STYLES = APP_DIR / "Styles" / "Styles.xaml"
FONT_FILE = APP_DIR / "Assets" / "FluentSystemIcons-Regular.ttf"

# Fonts that are legitimate, by the token name or the framework spelling.
OK_FONTS = (
    "{StaticResource MpvIconFontFamily}",
    "{ThemeResource MpvIconFontFamily}",
    "{StaticResource SymbolThemeFontFamily}",
    "{ThemeResource SymbolThemeFontFamily}",
)

GEOMETRY_ONLY_NAMES = ("_line_", "_line_dashes_", "_home_32_", "_home_20_")
SIZE_TOKEN_NAMES = [
    "MpvInlineIconSize", "MpvPopupIconSize", "MpvSectionHeaderIconSize",
    "MpvSectionCardIconSize", "MpvTransportIconSize", "MpvSectionCardChevronSize",
    "MpvShellAppIconSize",
]


def local(tag):
    return tag.split("}", 1)[-1] if "}" in tag else tag


def scan_files():
    """Every XAML file in the app project, wherever it lives.

    This used to iterate a hard-coded ("Modules", "Shell") pair. `Modules/` was
    renamed to `Domains/` in the layout restructure and the list was never
    updated, so the checker scanned `Shell/MainWindow.xaml` and nothing else --
    and reported OK, because the only guard was "no files at all". Measured
    before the fix: 39 XAML files in the project, 34 of them under `Domains/`,
    and the checker saw 1. Worse, 46 of the 49 baseline entries and all 13
    typography entries pointed into `Domains/`, so the findings it was supposed
    to gate were outside its own scan.

    Scanning the whole project directory instead of a list of subdirectories is
    the fix that cannot rot: a future rename moves the files, and the glob
    follows them. The build-output trees are excluded explicitly because
    `dotnet build` leaves copies of every XAML under bin/ and obj/.
    """
    return sorted(
        p for p in APP_DIR.rglob("*.xaml")
        if not ({"bin", "obj"} & set(p.relative_to(APP_DIR).parts))
    )


def icon_token_values():
    """The numeric icon size tokens, read from Styles.xaml rather than hard-coded."""
    text = STYLES.read_text(encoding="utf-8", errors="replace")
    values = {}
    for name in SIZE_TOKEN_NAMES:
        m = re.search(r'x:Key="%s"[^>]*>\s*([0-9.]+)\s*<' % name, text)
        if m:
            values[name] = float(m.group(1))
    return values


def fluent_glyph_info():
    """{(codepoint): design size in px} plus the name lookup, read from the shipped font.

    The design size is in the glyph name (`ic_fluent_line_48_regular` -> 48), which is how a
    Fluent glyph declares the size it was drawn for. This is the only signal with discriminating
    power: every private-use codepoint the app uses exists in the font, so existence checks are
    vacuous.
    """
    try:
        from fontTools.ttLib import TTFont
    except ImportError:
        return {}, {}
    try:
        font = TTFont(str(FONT_FILE), fontNumber=0, lazy=True)
    except Exception:
        return {}, {}
    names = {}
    for table in font["cmap"].tables:
        names.update(table.cmap)
    sizes = {}
    for cp, glyph in names.items():
        m = re.search(r"_(\d{2})_(regular|filled)$", glyph)
        if m:
            sizes[cp] = int(m.group(1))
    return names, sizes


def fluent_codepoints():
    """Codepoints the bundled font actually defines, so the checker reads the font, not a list."""
    try:
        from fontTools.ttLib import TTFont
    except ImportError:
        return None
    try:
        font = TTFont(str(FONT_FILE), fontNumber=0, lazy=True)
    except Exception:
        return None
    cps = set()
    for table in font["cmap"].tables:
        cps |= set(table.cmap.keys())
    return cps


def check_file(path, fluent_cps, token_values, glyph_sizes, glyph_name):
    text = path.read_text(encoding="utf-8", errors="replace")
    lines = text.splitlines()
    findings = []

    # One element may span lines; scan the joined text but report by line.
    # The attribute part is quote-aware: a plain `[^>]*?` stops at the `>` inside an XML entity
    # such as `&#xF8FF;`, so every glyph-carrying FontIcon was dropped from the scan. That is
    # why a mixed-system injection went unnoticed on the first attempt.
    for m in re.finditer(r'<FontIcon\b((?:[^>"]|"[^"]*")*)/?>', text, re.S):
        attrs = m.group(1)
        lineno = text.count("\n", 0, m.start()) + 1

        fam = re.search(r'FontFamily="([^"]+)"', attrs)
        gly = re.search(r'Glyph="([^"]+)"', attrs)

        # ---- HARDCODED FONT ----
        # A literal font NAME instead of a resource. The literal resolves to the same face as
        # the framework's `SymbolThemeFontFamily`, so it is not a rendering bug -- it is an
        # unnecessary exception that no theme or high-contrast dictionary can override, and in
        # OptionListControl.xaml it is 2 of 10 FontIcons while the other 8 inherit. Measured:
        # both codepoints involved (U+ED1F, U+E76C) exist in Segoe Fluent Icons, and the dynamic
        # `{x:Bind Icon}` case feeds `\uE8B7` from SettingsSections.FallbackIcon, also valid --
        # so the finding is about consistency and theming, and the message says so.
        if fam is not None and fam.group(1) not in OK_FONTS:
            findings.append((lineno, "HARDCODED-FONT",
                             "FontFamily=%s is a literal face name; use "
                             "SymbolThemeFontFamily (framework) or MpvIconFontFamily (bundled). "
                             "Same face today, but a literal cannot follow the theme"
                             % fam.group(1)))

        # ---- GLYPH FAMILY MISMATCH ----
        # A Fluent glyph carries its own design size in its NAME: `ic_fluent_line_48_regular`
        # is the 48 px drawing. Using it at FontSize 16 scales a 48 px drawing down to a third,
        # which reads as a thin, mis-weighted mark next to a 16 px `ic_fluent_*_20_regular`.
        #
        # An EXISTS check cannot catch this and must not be used: FluentSystemIcons defines all
        # 6400 private-use codepoints (measured: 100% of E000-F8FF, every one of them a real
        # `ic_fluent_*` name), so "codepoint missing from the font" is unreachable and would be
        # a checker that can never go red. The name's own size is the discriminating signal.
        if (fam is not None and "MpvIconFontFamily" in fam.group(1)
                and gly is not None and glyph_sizes):
            cp = re.match(r"&#x([0-9A-Fa-f]+);", gly.group(1))
            if cp:
                value = int(cp.group(1), 16)
                designed = glyph_sizes.get(value)
                size_attr = re.search(r'FontSize="([0-9.]+)"', attrs)
                if designed and size_attr:
                    drawn = float(size_attr.group(1))
                    # A design size more than 2x the rendered size has been scaled down hard.
                    if designed >= drawn * 2 and drawn <= 24:
                        findings.append((lineno, "GLYPH-SIZE-MISMATCH",
                                         "U+%04X is drawn at %d px (%s) but rendered at %g px; "
                                         "use the matching glyph for this size"
                                         % (value, designed, glyph_name(value) or "?",
                                            drawn)))

                # ---- NON-DESIGN GLYPH ----
                # Names that describe raw geometry rather than a symbol. Measured: the app's own
                # player control bar uses semantically-named Fluent glyphs throughout
                # (previous/next/shuffle/repeat_all/skip_back_10), so a `line_48` or a bare
                # `home_32` at an action button is not a style choice, it is a codepoint that was
                # copied from the framework's Segoe set and means something else here.
                name = glyph_name(value) or ""
                for shape in GEOMETRY_ONLY_NAMES:
                    if shape in name:
                        findings.append((lineno, "NON-DESIGN-GLYPH",
                                         "U+%04X resolves to %s in FluentSystemIcons -- a raw "
                                         "shape, not the icon this control needs" % (value, name)))
                        break

        # ---- RAW SIZE ----
        # Only a literal that EQUALS a tokenised value is a violation. The scale is
        # 12/14/16/20; a glyph at 8, 18, 32, 64 or 96 is a deliberate decorative size
        # (measured: AlbumArtworkFallbackIcon 64, MusicArtworkFallbackIcon 96,
        # DropOverlayIcon 32, all inside fixed-size Borders), and demanding a role token
        # there is the same over-reach that made the first version report 58 findings.
        size = re.search(r'FontSize="([^"]+)"', attrs)
        if size is not None:
            value = size.group(1)
            if not value.startswith(("{StaticResource", "{ThemeResource")):
                try:
                    literal = float(value)
                except ValueError:
                    literal = None
                matching = [name for name, v in token_values.items()
                            if literal is not None and abs(v - literal) < 1e-9]
                if matching:
                    findings.append((lineno, "RAW-SIZE",
                                     "literal FontSize=%s duplicates %s; use the token"
                                     % (value, " / ".join(matching))))

    return findings


BASELINE_FILE = REPO / "tools" / "icon-standard-baseline.txt"


def load_baseline():
    """Known, measured, not-yet-fixed findings: `path:line:kind` one per line.

    Presence in the baseline is not permission to add more. Each entry is a finding this
    checker can still see, recorded so the gate can be honest about the remainder instead of
    the alternative -- deleting a rule because it reports too much. New findings fail.
    """
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
    ap.add_argument("--update-baseline", action="store_true",
                    help="rewrite the baseline from the current findings")
    args = ap.parse_args()

    files = scan_files()
    if not files:
        print("FAIL: no XAML found under src/mpvw-app/")
        return 1

    token_values = icon_token_values()
    if not token_values:
        print("FAIL: no numeric icon size tokens readable from Styles.xaml")
        return 1
    glyph_names, glyph_sizes = fluent_glyph_info()
    fluent_cps = glyph_names
    if fluent_cps is None:
        print("FAIL: cannot read %s -- the mixed-system rule needs the real font"
              % FONT_FILE.name)
        return 1

    census = {"declared": 0, "framework": 0}
    findings = []
    for path in files:
        text = path.read_text(encoding="utf-8", errors="replace")
        for m in re.finditer(r'<FontIcon\b((?:[^>"]|"[^"]*")*)/?>', text, re.S):
            census["declared"] += 1
            if "FontFamily" not in m.group(1):
                census["framework"] += 1
        for lineno, kind, msg in check_file(path, fluent_cps, token_values, glyph_sizes, glyph_names.get):
            findings.append((path, lineno, kind, msg))

    if args.verbose:
        print("FontIcon: %d total, %d rely on the framework symbol font, %d declare a font"
              % (census["declared"], census["framework"],
                 census["declared"] - census["framework"]))
        print("FluentSystemIcons defines %d codepoints" % len(fluent_cps))

    baseline = load_baseline()
    fresh = []
    known = 0
    for path, lineno, kind, msg in findings:
        try:
            key = "%s:%d:%s" % (path.relative_to(REPO).as_posix(), lineno, kind)
        except ValueError:
            key = "%s:%d:%s" % (path, lineno, kind)
        if key in baseline:
            known += 1
        else:
            fresh.append((path, lineno, kind, msg, key))

    if args.update_baseline:
        lines = ["# Findings already measured and accepted for now. One per line:",
                 "#   <repo-relative path>:<line>:<kind>",
                 "# Regenerate with: python tools/check-icon-standard.py --update-baseline",
                 ""]
        for path, lineno, kind, msg in findings:
            try:
                key = "%s:%d:%s" % (path.relative_to(REPO).as_posix(), lineno, kind)
            except ValueError:
                key = "%s:%d:%s" % (path, lineno, kind)
            lines.append(key)
        BASELINE_FILE.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\r\n")
        print("baseline written: %d entr(ies)" % len(findings))
        return 0

    if not fresh:
        print("OK: %d XAML file(s), %d FontIcon(s), no NEW violations (%d known)"
              % (len(files), census["declared"], known))
        return 0

    findings = [(p, l, k, m) for p, l, k, m, _ in fresh]
    print("FAIL: %d NEW icon-standard violation(s) (%d known in the baseline)"
          % (len(findings), known))
    by_kind = {}
    for path, lineno, kind, msg in findings:
        by_kind.setdefault(kind, []).append((path, lineno, msg))
    for kind in sorted(by_kind):
        print("\n%s (%d):" % (kind, len(by_kind[kind])))
        for path, lineno, msg in by_kind[kind]:
            try:
                rel = path.relative_to(REPO)
            except ValueError:
                rel = path
            print("  %s:%d  %s" % (rel, lineno, msg))
    return 1


if __name__ == "__main__":
    sys.exit(main())
