"""Measure the shell's window-size floors and which control-bar tiers a real window reaches.

What this answers, and why it is a tool rather than a note:

  * the shell clamps resizes at `GetMinLogicalWidth() = content + OpenPaneLength`; this
    reads the clamp by ASKING the window (SetWindowPos to something tiny, read back),
    not by re-deriving the formula, then reports the formula's prediction next to it so
    the two can disagree visibly.
  * `ControlBarAdaptiveLayout.TierFor` only enters `Narrow` below 460 DIP and `Minimal`
    below 340 or in portrait; if the floor is above those, those rules never run.

Run:  python tools/ui-repair/probe-window-tiers.py [--keep-layout]

Writes `_work/window-tiers.json`. Restores `ControlBarLayout` unless `--keep-layout`.

**No fixed sleeps.** After a resize it polls the window rect until two consecutive reads
agree, because the shell re-lays out asynchronously and a guessed duration is this repo's
recorded top cause of intermittent failure.
"""
import ctypes
import json
import os
import sys
import time
import winreg
from ctypes import wintypes

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(REPO, "tools", "mvpw-mcp"))

import mpvw_tools as T  # noqa: E402

KEY = r"Software\Classes\Local Settings\Software\mpv-winui\mpv-winui\app"
OUT = os.path.join(REPO, "_work", "window-tiers.json")

user32 = ctypes.WinDLL("user32", use_last_error=True)
user32.GetDpiForWindow.restype = wintypes.UINT
SWP_NOMOVE, SWP_NOZORDER, SWP_NOACTIVATE = 0x0002, 0x0004, 0x0010

# Mirrors MainWindow_Size.cs. Kept as literals with the source line so a change there makes
# the prediction disagree with the measurement, which is the point of reporting both.
CONTENT_FLOOR = {"classic": 250, "modernx": 400}
NAV_OPEN = 320  # MpvNavPaneWidth, Styles.xaml:1435

TIERS = [(820, "wide"), (620, "standard"), (460, "compact"), (340, "narrow")]


def read_layout():
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER, KEY, 0, winreg.KEY_READ) as h:
        try:
            return winreg.QueryValueEx(h, "ControlBarLayout")[0]
        except OSError:
            return "classic"


def write_layout(v):
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER, KEY, 0, winreg.KEY_SET_VALUE) as h:
        winreg.SetValueEx(h, "ControlBarLayout", 0, winreg.REG_SZ, v)


def tier_for(w, h):
    if h > w:
        return "minimal(portrait)"
    for lo, name in TIERS:
        if w >= lo:
            return name
    return "minimal"


def settle(win_provider, timeout=5.0):
    """Wait until two consecutive window-rect reads agree. Replaces a fixed sleep."""
    last, stable, t0 = None, 0, time.time()
    while time.time() - t0 < timeout:
        try:
            r = win_provider().BoundingRectangle
            cur = (int(r.right - r.left), int(r.bottom - r.top))
        except Exception:
            cur = None
        stable = stable + 1 if (cur is not None and cur == last) else 0
        last = cur
        if stable >= 2:
            return last
        time.sleep(0.1)
    return last


def probe_layout(layout, scale):
    T.close_app()
    T.launch_app()
    hwnd = T._window().NativeWindowHandle

    # Ask for something far below any plausible floor and read back what the shell allows.
    user32.SetWindowPos(wintypes.HWND(hwnd), None, 0, 0, 200, 640,
                        SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE)
    floor = settle(T._window)

    # Portrait: tall and narrow, to see whether the orientation branch is reachable.
    user32.SetWindowPos(wintypes.HWND(hwnd), None, 0, 0, 200, 1000,
                        SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE)
    tall = settle(T._window)

    T.close_app()

    floor_dip = (floor[0] / scale, floor[1] / scale)
    tall_dip = (tall[0] / scale, tall[1] / scale)
    predicted = CONTENT_FLOOR.get(layout, 250) + NAV_OPEN
    return {
        "layout": layout,
        "floorPx": list(floor),
        "floorDip": [round(floor_dip[0], 1), round(floor_dip[1], 1)],
        "floorPredictedDip": predicted,
        "floorMatchesFormula": abs(floor_dip[0] - predicted) <= 2.0,
        "tierAtWideFloor": tier_for(*floor_dip),
        "tallDip": [round(tall_dip[0], 1), round(tall_dip[1], 1)],
        "tierTall": tier_for(*tall_dip),
    }


def main():
    keep = "--keep-layout" in sys.argv
    original = read_layout()
    print("stored ControlBarLayout: %s" % original)
    print("dpi scale from window: probing\n")

    results = []
    try:
        # Scale comes from a launch; reuse the first one.
        T.close_app()
        T.launch_app()
        hwnd = T._window().NativeWindowHandle
        scale = (user32.GetDpiForWindow(wintypes.HWND(hwnd)) or 96) / 96.0
        T.close_app()
        print("scale: %.2f px/DIP\n" % scale)

        for layout in ("classic", "modernx"):
            write_layout(layout)
            r = probe_layout(layout, scale)
            results.append(r)
            print("%-8s floor=%6.1f DIP (formula %3d, match=%s)  tier=%s | tall tier=%s"
                  % (r["layout"], r["floorDip"][0], r["floorPredictedDip"],
                     "yes" if r["floorMatchesFormula"] else "NO",
                     r["tierAtWideFloor"], r["tierTall"]))
    finally:
        if not keep:
            write_layout(original)
            print("\nlayout restored: %s" % original)
        T.close_app()

    reached = {r["tierAtWideFloor"] for r in results} | {r["tierTall"] for r in results}
    for t in ("narrow", "minimal"):
        print("tier %-8s reachable: %s"
              % (t, "yes" if any(t in x for x in reached) else "NO"))

    open(OUT, "w", encoding="utf-8").write(json.dumps(results, indent=2, ensure_ascii=False))
    print("\nwritten:", OUT)
    return 0


if __name__ == "__main__":
    sys.exit(main())
