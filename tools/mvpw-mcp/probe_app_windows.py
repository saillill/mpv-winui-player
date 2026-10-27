"""Probe: what window does mpvw actually own, and which one receives hover input?

**A probe: no VERDICT line.** It answers one question that has blocked every hover measurement
in this session, and it does not pretend to answer anything about the product.

The question. Three separate attempts to observe hover behaviour -- the letter rail's wave, the
music card's overlay, the pixel comparisons -- each concluded "no change on hover", and each of
those conclusions is worthless until this is answered: which HWND is being driven?

What is known so far, measured with EnumWindows on a running instance:

    "mpv smtc"   rect=(50, 50, 1696, 928)   visible=0
    "MPV"        rect=(25, 25, 1671, 922)   visible=1

and PowerShell's MainWindowHandle returns the FIRST of those. Every probe that positioned or
foregrounded "the window" was therefore talking to an invisible frame -- which is why
SetWindowPos on it reported (-32000, -32000) and why UIA then found no navigation elements at
all. It is also why the same probes printed "nothing to hover" while the app was plainly on
screen.

What this prints: every top-level window the process owns, with its title, rect, visibility and
whether it is the one UIA uses as the app root. Run it before writing any hover probe; guessing
this wastes a round each time.

Run:
    python tools/mvpw-mcp/probe_app_windows.py
"""
from __future__ import annotations

import ctypes
import os
import subprocess
import sys
import time
from ctypes import wintypes

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mpvw_tools as T  # noqa: E402

user32 = ctypes.WinDLL("user32", use_last_error=True)
user32.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
user32.GetWindowRect.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.RECT)]
user32.IsWindowVisible.argtypes = [wintypes.HWND]
user32.GetWindowTextLengthW.argtypes = [wintypes.HWND]
user32.GetWindowTextW.argtypes = [wintypes.HWND, wintypes.LPWSTR, ctypes.c_int]
user32.EnumWindows.argtypes = [ctypes.c_void_p, wintypes.LPARAM]


def _pids() -> set[int]:
    out = subprocess.run(["pwsh", "-NoProfile", "-Command", "(Get-Process mpvw).Id"],
                         capture_output=True, text=True, encoding="utf-8",
                         errors="replace").stdout.strip()
    try:
        return {int(x) for x in out.split()}
    except ValueError:
        return set()


def _windows(pids: set[int]) -> list[tuple]:
    out = []

    def cb(h, l):
        pid = wintypes.DWORD()
        user32.GetWindowThreadProcessId(h, ctypes.byref(pid))
        if pid.value not in pids:
            return True
        length = user32.GetWindowTextLengthW(h)
        buf = ctypes.create_unicode_buffer(length + 1)
        user32.GetWindowTextW(h, buf, length + 1)
        r = wintypes.RECT()
        user32.GetWindowRect(h, ctypes.byref(r))
        out.append((h, buf.value, (r.left, r.top, r.right, r.bottom),
                    bool(user32.IsWindowVisible(h))))
        return True

    user32.EnumWindows(
        ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, wintypes.LPARAM)(cb), 0)
    return out


def main() -> int:
    T.close_app()
    if not T.launch_app().get("pid"):
        print("could not launch the app")
        return 1

    try:
        time.sleep(6.0)
        pids = _pids()
        print("process ids: %s" % sorted(pids))
        if not pids:
            print("the app is not running, so there is nothing to enumerate")
            return 1

        windows = _windows(pids)
        print("\ntop-level windows this process owns:\n")
        for hwnd, title, rect, visible in windows:
            width = rect[2] - rect[0]
            height = rect[3] - rect[1]
            flag = ""
            if visible and width > 600 and height > 400 and rect[0] > -10000:
                flag = "  <- looks like the real frame"
            print("  hwnd=%-12s visible=%-5s rect=%-30s size=%dx%d title=%r%s"
                  % (hwnd, visible, rect, width, height, title, flag))

        real = [w for w in windows
                if w[3] and (w[2][2] - w[2][0]) > 600 and (w[2][3] - w[2][1]) > 400
                and w[2][0] > -10000]
        print()
        if real:
            print("Usable frame(s): %s" % [w[1] for w in real])
            print("A hover probe must drive the FIRST of these, by title and rect -- not")
            print("MainWindowHandle, which returns the hidden smtc window.")
        else:
            print("No on-screen frame right now. Either the app minimised itself or the")
            print("window was moved off screen by an earlier probe. Both are recoverable by")
            print("showing it, but a hover probe cannot run until one exists.")
    finally:
        T.close_app()

    return 0


if __name__ == "__main__":
    sys.exit(main())
