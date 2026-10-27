"""A "More" button must use the three-dot glyph, not the hamburger.

Reported: 视频和音频的播放悬浮按钮、菜单悬浮按钮图标均不正确.

Half of that was a real, silent defect with a documented precedent. `PlayerControl.xaml`'s
app-bar More button already carried this comment:

    Segoe Fluent Icons E712 is the horizontal three-dot "More". The FluentSystemIcons face
    the rest of this bar uses has no such glyph at the codepoint that was here, so the button
    rendered as an unrelated shape.

That fix was applied to ONE of the three places that draw a More button. The two overlay
styles -- CenterPlayerControlStyle.xaml (video, full player) and CompactPlayerControlStyle.xaml
(audio, compact) -- still said E10C, which is GlobalNavButton, the hamburger menu. So the same
button showed a hamburger on the floating bar and three dots on the app bar, which is exactly
what "图标不正确" describes.

This checker is the generalisation: every control named MoreButton must use E712. It reads the
XAML rather than the rendered pixels because the glyph is a literal there and the failure mode
is a wrong literal, not a layout problem -- and because a pixel comparison cannot tell a
hamburger from three dots reliably at 16 px.

Exit codes: 0 clean, 1 findings, 2 nothing parsed (a checker that silently matches nothing
reports success forever -- see AGENTS.md).
"""
from __future__ import annotations

import glob
import os
import re
import sys

# tools/check-more-glyph.py -> repo root is one level up from tools/, not two.
REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

MORE_GLYPH = "&#xE712;"
# Codepoints that look plausible next to a More button but are something else. Listing them
# makes a future mistake name itself instead of just failing a comparison.
KNOWN_OTHER = {
    "&#xE10C;": "GlobalNavButton (hamburger)",
    "&#xE700;": "GlobalNavButton (hamburger, older alias)",
    "&#xE76F;": "Settings",
    "&#xE8B7;": "Folder",
    "&#xE8E5;": "OpenFile",
}


def main() -> int:
    pattern = os.path.join(REPO, "mpv-winui", "mpv-winui", "**", "*.xaml")
    files = sorted(glob.glob(pattern, recursive=True))
    # Never scan build output: obj/ holds a copy of every XAML as it was when the last build
    # ran, so a fixed source file keeps failing until the next build. Measured -- after the
    # fix, both findings came from obj\...\CenterPlayerControlStyle.xaml and
    # obj\...\CompactPlayerControlStyle.xaml, not from the sources that had been corrected.
    files = [f for f in files
             if os.sep + "obj" + os.sep not in f and os.sep + "bin" + os.sep not in f]
    if not files:
        print("ERROR: no XAML files found under %s" % pattern)
        return 2

    # Each Button named MoreButton, with the glyph inside its own <Button.Content> block.
    #
    # Bounded to `</Button.Content>` rather than `</Button>`, and NOT by a fixed character
    # budget: measured on CenterPlayerControlStyle.xaml, `</Button.Content>` is 1302 chars
    # after the name while `</Button>` is 11788, because the button's MenuFlyout sits between
    # them and lists a dozen menu items. A 4000-char window silently matched nothing there --
    # which the exit-2 guard below turned into an error rather than a clean bill of health.
    more_re = re.compile(
        r'x:Name="MoreButton".*?</Button\.Content>', re.S)
    # A XAML glyph is `&#xE712;` -- it starts with `&`, not `#`. The first pattern required
    # the character right after the opening quote to be `#`, so it matched nothing at all.
    # The exit-2 guard is what turned that into an error instead of a silent pass.
    glyph_re = re.compile(r'Glyph="(&#[^"]+;)"')

    checked = 0
    findings = []
    for path in files:
        try:
            text = open(path, encoding="utf-8").read()
        except OSError:
            continue
        for m in more_re.finditer(text):
            block = m.group(0)
            # Only the Button.Content region: the flyout that follows lists menu-item icons,
            # and one of those must not be mistaken for the button's own glyph.
            content = block
            cut = block.find("</Button.Content>")
            if cut != -1:
                content = block[:cut]
            g = glyph_re.search(content)
            if not g:
                rel = os.path.relpath(path, REPO)
                line = text[:m.start()].count("\n") + 1
                findings.append((rel, line, "<no FontIcon glyph in Button.Content>"))
                continue
            checked += 1
            glyph = g.group(1)
            if glyph != MORE_GLYPH:
                line = text[:m.start()].count("\n") + content[:g.start()].count("\n") + 1
                rel = os.path.relpath(path, REPO)
                findings.append((rel, line, glyph))

    if checked == 0:
        print("ERROR: found no MoreButton with a FontIcon glyph -- the pattern is wrong, "
              "not the code")
        return 2

    if findings:
        print("FAIL: %d More button(s) do not use the three-dot glyph %s"
              % (len(findings), MORE_GLYPH))
        for rel, line, glyph in findings:
            hint = KNOWN_OTHER.get(glyph, "")
            print("  %s:%d  Glyph=%s%s" % (rel, line, glyph,
                                           ("  <- " + hint) if hint else ""))
        print("\nE712 is the horizontal three-dot More. E10C is the hamburger.")
        return 1

    print("OK: %d More button(s) across %d XAML file(s) use %s"
          % (checked, len(files), MORE_GLYPH))
    return 0


if __name__ == "__main__":
    sys.exit(main())
