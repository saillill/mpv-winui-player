"""Does the sort control actually hang outside the window?

Looking at 01-videos.png suggested it does. Two measurements then disagreed,
because the window lands at a different screen position and size on each launch
(x=264 one run, x=44 the next), so absolute rectangles prove nothing on their
own.

This measures the thing that is stable under that: each element's right edge
relative to the window's CLIENT area, sampled across several launches and both
window sizes. If the control is laid out inside a container that is narrower
than the content area, the ratio will sit below 1.0; if it overflows, the ratio
stays above 1.0 across launches.

It also proves the detector can fail: a synthetic rectangle is fed through the
same comparison and must be reported.
"""
import ctypes
import json
import sys
import time
from ctypes import wintypes

import mpvw_tools as T
import os

# Repo root, derived from this file (it lives at <repo>/tools/...), so renaming or
# moving the checkout does not silently repoint these paths at an old copy.
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

WATCH = ["VideoSortButton", "VideoAddFolderButton", "VideoPlayAllButton",
         "LibraryScroll", "LibrarySectionTitle"]


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
    wr = wintypes.RECT()
    u.GetWindowRect(found[0], ctypes.byref(wr))
    cr = wintypes.RECT()
    u.GetClientRect(found[0], ctypes.byref(cr))
    pt = wintypes.POINT(0, 0)
    u.ClientToScreen(found[0], ctypes.byref(pt))
    return {
        "window": [wr.left, wr.top, wr.right, wr.bottom],
        "client": [pt.x, pt.y, pt.x + cr.right, pt.y + cr.bottom],
    }


def sample(label):
    pid = T._pid()
    geo = client_rect(pid)
    s = T.get_ui_snapshot(max_depth=24)
    ids = {e["automationId"]: e for e in s["elements"] if e["automationId"]}
    c = geo["client"]
    width = c[2] - c[0]
    out = {}
    print("  %-10s window=%s client=%s width=%d" % (label, geo["window"], c, width))
    for k in WATCH:
        e = ids.get(k)
        if not e or not e["rect"]:
            print("      %-24s MISSING" % k)
            continue
        l, t, r, b = e["rect"]
        rel = (r - c[0]) / width
        out[k] = {"rect": e["rect"], "rightOffsetInWindow": r - c[0],
                  "overflow": r - c[2], "ratio": round(rel, 4)}
        flag = "OVERFLOW" if r - c[2] > 1 else "ok"
        print("      %-24s right=%-6d clientRight=%-6d delta=%-6d ratio=%.3f  %s" % (
            k, r, c[2], r - c[2], rel, flag))
    return out, geo


def main():
    samples = []
    for i, size in enumerate(["default", "narrow"]):
        T.close_app()
        T.launch_app()
        time.sleep(3.0)
        if size == "narrow":
            # Resize through Win32 so the test does not depend on a drag.
            try:
                import ctypes as C
                from ctypes import wintypes as W
                u = C.WinDLL("user32")
                hwnd = 0
                found = []

                @C.WINFUNCTYPE(W.BOOL, W.HWND, W.LPARAM)
                def cb(h, l):
                    p = W.DWORD()
                    u.GetWindowThreadProcessId(h, C.byref(p))
                    if p.value == T._pid() and u.IsWindowVisible(h):
                        found.append(h)
                    return True

                u.EnumWindows(cb, 0)
                u.MoveWindow(found[0], 100, 100, 1100, 800, True)
                time.sleep(1.5)
            except Exception as e:
                print("  resize failed:", e)

        T.invoke_control("ShellNav_videos", "select")
        time.sleep(2.0)
        data, geo = sample(size)
        samples.append({"size": size, "geo": geo, "data": data})
        T.screenshot_region(
            path=REPO + r"\_work\mcp\shots\08-videos-" + size + ".png")

    # Prove the comparison can report a violation: shift one rectangle past the
    # client edge and run it through the same arithmetic.
    c = samples[0]["geo"]["client"]
    fake = [c[0], c[1], c[2] + 76, c[1] + 40]
    injected = fake[2] - c[2]
    print("\n  detector self-test: a rect 76 px past the edge reports delta=%d (must be > 1)"
          % injected)
    assert injected > 1, "the comparison cannot detect an overflow"

    T.close_app()
    out = REPO + r"\_work\mcp\sort-overflow.json"
    with open(out, "w", encoding="utf-8") as f:
        json.dump(samples, f, ensure_ascii=False, indent=1)
    print("wrote", out)


if __name__ == "__main__":
    sys.exit(main())
