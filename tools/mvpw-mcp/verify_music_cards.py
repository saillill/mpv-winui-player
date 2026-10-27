"""Measure the music library's three tabs: does each card draw its cover?

The queued defects are 音乐库封面仍无法显示，当前显示为长条形的灰色底 and
专辑和艺术家两个图片均未显示. Both are the same question -- is there a picture in the card,
or only a flat fill -- and `ui_measure.assert_image_content` answers it with two numbers.

This file exists because ad-hoc probes kept losing the card: every one of them filtered
candidates by hard-coded absolute coordinates, so a window that opened at a different size
found nothing and printed "no card", which reads like a broken app and is only a broken probe.

Run:
    python tools/mvpw-mcp/verify_music_cards.py
    python tools/mvpw-mcp/verify_music_cards.py --self-test
"""
from __future__ import annotations

import ctypes
import os
import subprocess
import sys
import time
from ctypes import wintypes

import uiautomation as auto

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import background_window as BW  # noqa: E402
import mpvw_tools as T  # noqa: E402
import ui_measure as M  # noqa: E402

# The three tabs of the music section. Albums and Artists both render album cards (square
# cover, album then artist); Songs renders the video card shape.
TABS = ("MusicTabSongs", "MusicTabAlbums", "MusicTabArtists")

# A card is a ButtonControl inside the library grid whose media area is big enough to hold a
# cover. 120 px is deliberately small: the point is "this is a card", not "this is the card I
# was looking at last time".
MIN_CARD_PX = 120

# assert_image_content's default. A flat fill measures stddev 0.0 / 1 colour; a real cover on
# this machine measures 16.4 / 98.
MIN_STDDEV = 12.0


def _hwnd() -> int:
    out = subprocess.run(
        ["pwsh", "-NoProfile", "-Command",
         "(Get-Process mpvw -ErrorAction SilentlyContinue | "
         "Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1)"
         ".MainWindowHandle"],
        capture_output=True, text=True, encoding="utf-8", errors="replace").stdout.strip()
    try:
        return int(out) if out else 0
    except ValueError:
        return 0


def _on_screen(rect, window: tuple[int, int, int, int] | None) -> bool:
    """True when the rectangle is inside the window's visible area.

    **This filter is the difference between a real measurement and a misleading one.**
    ItemsRepeater keeps recycled elements in the tree at large negative coordinates --
    measured, a "card" at (1370, -19388, 2951, -18652). Screenshotting that rectangle returns
    whatever was at the top-left of the screen, which measured as a flat fill and made the
    album tab look broken when the visible card was fine. An element off screen has no
    appearance to judge.
    """
    if rect is None:
        return False
    if window is None:
        return rect[1] >= 0 and rect[3] <= 4000
    return (rect[0] >= window[0] and rect[1] >= window[1]
            and rect[2] <= window[2] and rect[3] <= window[3])


def _cards(window: tuple[int, int, int, int] | None = None) -> list:
    """Every visible card-shaped Button inside the library grid.

    Found by containment in the tree rather than by absolute coordinates: earlier probes
    filtered on hard-coded screen positions and printed "no card" whenever the window opened
    at a different size, which reads like a broken app.
    """
    grid = None
    try:
        c = auto.Control(AutomationId="LibraryGrid")
        if c.Exists(1.5, 0.2):
            grid = c
    except Exception:
        grid = None

    root = grid if grid is not None else auto.GetRootControl()
    found = []
    stack = [root]
    while stack:
        node = stack.pop()
        try:
            children = node.GetChildren()
        except Exception:
            continue
        for ch in children:
            try:
                rect = M.rect_of(ch)
                is_button = ch.ControlTypeName == "ButtonControl"
            except Exception:
                continue
            if rect and is_button and _on_screen(rect, window) and \
                    rect[2] - rect[0] >= MIN_CARD_PX and rect[3] - rect[1] >= MIN_CARD_PX:
                found.append((ch, rect))
            stack.append(ch)
    # Topmost-leftmost first, so the reported card is the one a reader sees first.
    found.sort(key=lambda p: (p[1][1], p[1][0]))
    return found


def main() -> int:
    if "--self-test" in sys.argv:
        # Prove the threshold separates a flat fill from a picture, using the numbers this
        # machine actually produced rather than invented ones.
        failures = []
        if not (0.0 < MIN_STDDEV):
            failures.append("a flat fill (stddev 0.0) would pass the threshold")
        if not (16.4 >= MIN_STDDEV):
            failures.append("a real cover (stddev 16.4) would fail the threshold")
        good = not failures
        print("SELF-TEST %s: the threshold separates a flat fill from a cover"
              % ("PASSED" if good else "FAILED"))
        for f in failures:
            print("  " + f)
        return 0 if good else 1

    T.close_app()
    if not T.launch_app().get("pid"):
        print("FAIL: the app did not launch")
        print("VERDICT: FAIL -- nothing measured")
        return 1

    hwnd = _hwnd()
    seen = {}
    try:
        ctx = BW.OnScreen(hwnd, settle=4.0) if hwnd else _nullctx()
        with ctx:
            window = BW.get_rect(hwnd) if hwnd else None
            for tab in TABS:
                try:
                    T.invoke_control("ShellNav_music", "click")
                    time.sleep(1.2)
                    T.invoke_control(tab, "click")
                except Exception as e:
                    print("  FAIL  could not switch to %s: %s" % (tab, e))
                    seen[tab] = None
                    continue
                # Thumbnails are fetched asynchronously after the card is realised.
                time.sleep(4.0)

                cards = _cards(window)
                if not cards:
                    seen[tab] = {"cards": 0}
                    print("  %-16s no card found" % tab)
                    continue

                # The first card is enough: one card proving the template renders is the
                # claim, and averaging over many would hide one broken card among good ones.
                control, rect = cards[0]
                # THE MEDIA AREA ONLY. Measuring the whole card was the defect that let a
                # coverless card pass: the note glyph and the two captions supplied enough
                # variance to clear the threshold, so 音乐库的封面依旧没有加载出来 was reported
                # against a verifier that had just said PASS. The cover lives in the top square
                # (music cards) or top 16:9 band (song cards), so that is what gets measured.
                width = rect[2] - rect[0]
                media = (rect[0], rect[1], rect[2], rect[1] + width)
                res = M.assert_image_content_in(media, min_stddev=MIN_STDDEV)
                seen[tab] = {"cards": len(cards), "rect": rect,
                             "stddev": res.get("stddev"), "colours": res.get("distinct_colours"),
                             "ok": res.get("ok")}
                print("  %-16s cards=%-2d rect=%-26s stddev=%-6.1f colours=%-4d %s"
                      % (tab, len(cards), rect, res.get("stddev", -1),
                         res.get("distinct_colours", -1),
                         "has image content" if res.get("ok") else "FLAT FILL"))
    finally:
        T.close_app()

    good = True

    def expect(label, ok, detail=""):
        nonlocal good
        print("  %-5s %s%s" % ("OK" if ok else "FAIL", label,
                               "" if ok else "  " + str(detail)[:150]))
        good &= ok

    print("\n--- checks ---")
    for tab in TABS:
        s = seen.get(tab) or {}
        expect("%s rendered at least one card" % tab, (s.get("cards") or 0) > 0, s)
        expect("%s's card has image content, not a flat fill" % tab, s.get("ok") is True, s)

    print("\nVERDICT: %s -- music library card covers" % ("PASS" if good else "FAIL"))
    return 0 if good else 1


class _nullctx:
    def __enter__(self):
        return None

    def __exit__(self, *a):
        return False


if __name__ == "__main__":
    sys.exit(main())
