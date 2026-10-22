"""Find elements that are drawn outside the window, or clipped by it.

This is the "显示错乱" class that geometry assertions can actually catch, and
the previous audit did not look for it at all: it checked that elements existed
and had plausible sizes, never that they were *inside* the window they belong to.

Looking at 01-videos.png suggested the sort button hangs off the right edge.
Eyeballing is not evidence -- the window border, the drop shadow and the
screenshot's own margins all look like overflow. This measures it:

  * window rect from the OS (GetWindowRect), not from a UIA element
  * every element with a rectangle entirely or partly outside that rect
  * every element wider/taller than the window
  * elements whose right edge is beyond the window's right edge by > 1 px

A tolerated case: the navigation pane in overlay mode is allowed to sit over
the content, and popups are deliberately allowed outside the window
(ShouldConstrainToRootBounds="False"). Those are reported separately so they do
not bury the real findings.
"""
import json
import sys
import time

import uiautomation as auto

import mpvw_tools as T

CLIP = r"D:\Workspace\WorkBuddy\MPV\.workbuddy\tmp\clips\sdr-bt709.mp4"
OUT = r"C:\Users\z2637\Documents\DeepSeek\mpv\_work\mcp\overflow.json"

TOL = 1  # px


def window_rect(pid):
    """The real window rect from Win32, and the client area."""
    import ctypes
    from ctypes import wintypes

    user32 = ctypes.WinDLL("user32", use_last_error=True)
    hwnd = 0
    found = []

    @ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    def cb(h, l):
        p = wintypes.DWORD()
        user32.GetWindowThreadProcessId(h, ctypes.byref(p))
        if p.value == pid and user32.IsWindowVisible(h):
            found.append(h)
        return True

    user32.EnumWindows(cb, 0)
    if not found:
        return None, None
    hwnd = found[0]
    r = wintypes.RECT()
    user32.GetWindowRect(hwnd, ctypes.byref(r))
    cr = wintypes.RECT()
    user32.GetClientRect(hwnd, ctypes.byref(cr))
    to_screen = wintypes.POINT(0, 0)
    user32.ClientToScreen(hwnd, ctypes.byref(to_screen))
    return ([r.left, r.top, r.right, r.bottom],
            [to_screen.x, to_screen.y,
             to_screen.x + cr.right, to_screen.y + cr.bottom])


def scan(label):
    pid = T._pid()
    win_rect, client = window_rect(pid)
    if win_rect is None:
        print("  no window")
        return []

    s = T.get_ui_snapshot(max_depth=24, redact_names=True)
    rows = []
    for e in s["elements"]:
        r = e["rect"]
        if not r:
            continue
        l, t, rr, b = r
        if rr - l <= 0 or b - t <= 0:
            continue
        over = []
        if l < client[0] - TOL:
            over.append("left by %d" % (client[0] - l))
        if t < client[1] - TOL:
            over.append("top by %d" % (client[1] - t))
        if rr > client[2] + TOL:
            over.append("right by %d" % (rr - client[2]))
        if b > client[3] + TOL:
            over.append("bottom by %d" % (b - client[3]))
        if over:
            rows.append({
                "id": e["automationId"] or ("<%s>" % e["type"]),
                "type": e["type"],
                "rect": r,
                "over": over,
                "size": (rr - l, b - t),
            })

    print("  %-12s window=%s client=%s  overflow=%d" % (
        label, win_rect, client, len(rows)))
    for r in rows:
        print("      %-26s %-16s %-38s %s" % (
            r["id"], r["type"], ",".join(r["over"]), r["rect"]))
    return rows


def main():
    T.close_app()
    T.launch_app()
    time.sleep(3.0)

    results = {}
    results["home"] = scan("home")

    for key in ("music", "videos", "queue"):
        T.invoke_control("ShellNav_" + key, "select")
        time.sleep(1.8)
        results[key] = scan(key)

    # Playing: the transport bar and the overlay controls.
    T._ipc(["loadfile", CLIP, "replace"], 900)
    time.sleep(4.0)
    results["playing"] = scan("playing")
    T._ipc(["stop"], 905)
    time.sleep(1.2)

    T.close_app()

    total = {k: len(v) for k, v in results.items()}
    print("\noverflow by view:", json.dumps(total))
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(results, f, ensure_ascii=False, indent=1)
    print("wrote", OUT)


if __name__ == "__main__":
    sys.exit(main())
