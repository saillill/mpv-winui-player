"""Identify what is drawn in the 12 px strip at the window's own edges.

Sampling 03-control-panel.png showed saturated pixels (an orange at x=2, a blue
at x=4) at both the left and the right edge, inside the window frame, while the
video starts at x=12. In the library screenshots the same strip looked like grey
noise, which was dismissed as the window shadow -- that dismissal was wrong:
grey is what a blurred window shadow looks like, saturated orange and blue are
not.

The two candidates:
  A. the WinUI window border / drop shadow composited by DWM -- would be
     uniform in colour and would not change between views;
  B. something the app draws: a 1-2 px strip of the VIDEO, a stray element at
     x<0, or the pane's own surface stretching past the client area.

Distinguish by asking the automation tree for elements whose rectangle has a
negative x relative to the client area, and by cropping the strip from several
views (library, playing, audio) to see whether the colours track the content.
"""
import ctypes
import json
import sys
import time
from ctypes import wintypes

from PIL import Image

import mpvw_tools as T
import os

# Repo root, derived from this file (it lives at <repo>/tools/...), so renaming or
# moving the checkout does not silently repoint these paths at an old copy.
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

CLIP = r"D:\Workspace\WorkBuddy\MPV\.workbuddy\tmp\clips\sdr-bt709.mp4"
SHOTS = REPO + r"\_work\mcp\shots"


def client_rect(pid):
    u = ctypes.WinDLL("user32")
    found = []

    @ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    def cb(h, l):
        p = wintypes.DWORD()
        u.GetWindowThreadProcessId(h, ctypes.byref(p))
        if p.value == pid and u.IsWindowVisible(h):
            found.append(h)
        return True

    u.EnumWindows(cb, 0)
    if not found:
        return None
    cr = wintypes.RECT()
    u.GetClientRect(found[0], ctypes.byref(cr))
    pt = wintypes.POINT(0, 0)
    u.ClientToScreen(found[0], ctypes.byref(pt))
    return [pt.x, pt.y, pt.x + cr.right, pt.y + cr.bottom]


def scan_negative(pid):
    """Elements whose left edge is left of the client area."""
    c = client_rect(pid)
    s = T.get_ui_snapshot(max_depth=24)
    out = []
    for e in s["elements"]:
        r = e["rect"]
        if r and r[0] < c[0] - 1:
            out.append({"id": e["automationId"] or e["type"],
                        "rect": r, "leftOverflow": c[0] - r[0]})
    return c, out


def strip_profile(path, label):
    im = Image.open(path).convert("RGB")
    w, h = im.size
    print("  %-14s %dx%d" % (label, w, h))
    for y in (h // 4, h // 2, 3 * h // 4):
        row = [im.getpixel((x, y)) for x in range(0, 14)]
        saturated = sum(1 for p in row if max(p) - min(p) > 60)
        print("      y=%-5d saturated=%d/%d  row=%s" % (
            y, saturated, len(row), row[:8]))


def main():
    T.close_app()
    T.launch_app()
    time.sleep(3.0)

    print("== library view ==")
    c, neg = scan_negative(T._pid())
    print("  client:", c)
    print("  elements left of the client area:", len(neg))
    for e in neg:
        print("      %-28s rect=%s left by %d" % (e["id"], e["rect"], e["leftOverflow"]))

    print("\n== playing view ==")
    T._ipc(["loadfile", CLIP, "replace"], 900)
    time.sleep(4.0)
    c2, neg2 = scan_negative(T._pid())
    print("  client:", c2)
    print("  elements left of the client area:", len(neg2))
    for e in neg2:
        print("      %-28s rect=%s left by %d" % (e["id"], e["rect"], e["leftOverflow"]))

    # Fresh captures at the same window position, then compare the strip colours.
    T.screenshot_region(path=SHOTS + r"\09-strip-playing.png")
    T._ipc(["stop"], 910)
    time.sleep(1.5)
    T.screenshot_region(path=SHOTS + r"\09-strip-library.png")
    T.close_app()

    print("\n== strip profiles (x = 0..13) ==")
    for name in ("09-strip-playing", "09-strip-library"):
        strip_profile(SHOTS + "\\" + name + ".png", name)

    out = REPO + r"\_work\mcp\strip.json"
    with open(out, "w", encoding="utf-8") as f:
        json.dump({"library": {"client": c, "negative": neg},
                   "playing": {"client": c2, "negative": neg2}},
                  f, ensure_ascii=False, indent=1)
    print("wrote", out)


if __name__ == "__main__":
    sys.exit(main())
