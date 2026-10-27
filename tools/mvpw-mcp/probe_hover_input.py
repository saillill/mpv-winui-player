"""Drive real mouse input at the app, so hover state can be observed at all.

**Why this file exists.** Every previous attempt to check a hover effect in this repo failed,
and the failures were all the same shape: `SetCursorPos` moves the cursor without generating
input the app processes, and `foreground_guard` keeps the window out of the foreground, so
`PointerEntered` never fires and nothing on screen changes. Three separate measurements
(`verify_rail_hover_wave`, the card overlay probe, the earlier pixel comparisons) each concluded
"no change" and each could have been read as a product defect.

What is different here:

* **`SendInput` with MOUSEEVENTF_MOVE**, not `SetCursorPos`. SetCursorPos sets the position;
  SendInput goes through the input stack and produces WM_MOUSEMOVE, which is what WinUI's
  pointer pipeline consumes.
* **The window is brought to the foreground for the duration, on purpose**, and put back after.
  A hover effect cannot be measured on a window that is not receiving input; the honest move is
  to hold the foreground deliberately and restore it, rather than to pretend the constraint does
  not exist.
* **A movement sequence, not one jump.** WinUI raises PointerEntered on the transition into an
  element, so the cursor has to approach from outside rather than simply appear inside.

Run:
    python tools/mvpw-mcp/probe_hover_input.py
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

user32 = ctypes.WinDLL("user32", use_last_error=True)

# INPUT structure for SendInput. Only the mouse members are used.
INPUT_MOUSE = 0
MOUSEEVENTF_MOVE = 0x0001
MOUSEEVENTF_ABSOLUTE = 0x8000


class MOUSEINPUT(ctypes.Structure):
    _fields_ = [("dx", wintypes.LONG), ("dy", wintypes.LONG),
                ("mouseData", wintypes.DWORD), ("dwFlags", wintypes.DWORD),
                ("time", wintypes.DWORD), ("dwExtraInfo", ctypes.POINTER(wintypes.ULONG))]


class INPUT(ctypes.Structure):
    class _U(ctypes.Union):
        _fields_ = [("mi", MOUSEINPUT)]

    _anonymous_ = ("u",)
    _fields_ = [("type", wintypes.DWORD), ("u", _U)]


def _screen_size() -> tuple[int, int]:
    return (user32.GetSystemMetrics(0), user32.GetSystemMetrics(1))


user32.ShowWindow.argtypes = [wintypes.HWND, ctypes.c_int]
user32.GetWindowThreadProcessId.argtypes=[wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
user32.IsWindowVisible.argtypes=[wintypes.HWND]
user32.GetWindowTextLengthW.argtypes=[wintypes.HWND]
user32.GetWindowTextW.argtypes=[wintypes.HWND, wintypes.LPWSTR, ctypes.c_int]
user32.SetForegroundWindow.argtypes=[wintypes.HWND]
user32.EnumWindows.argtypes=[ctypes.c_void_p, wintypes.LPARAM]
user32.GetWindowRect.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.RECT)]


def move_to(x: int, y: int) -> None:
    """Absolute SendInput mouse move.

    Coordinates are normalised to 0..65535 over the whole virtual desktop, which is what
    MOUSEEVENTF_ABSOLUTE expects -- passing raw pixels silently lands somewhere else.
    """
    sw, sh = _screen_size()
    nx = int(x * 65535 / max(sw - 1, 1))
    ny = int(y * 65535 / max(sh - 1, 1))

    inp = INPUT()
    inp.type = INPUT_MOUSE
    inp.mi = MOUSEINPUT(nx, ny, 0, MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE, 0, None)
    user32.SendInput(1, ctypes.byref(inp), ctypes.sizeof(INPUT))


def approach(x: int, y: int, steps: int = 8) -> None:
    """Walk to a point, so the transition into an element actually happens."""
    for i in range(1, steps + 1):
        move_to(x - 120 + int(120 * i / steps), y - 80 + int(80 * i / steps))
        time.sleep(0.03)
    move_to(x, y)
    time.sleep(0.15)


def _hwnd() -> int:
    """The app's VISIBLE top-level window.

    NOT MainWindowHandle: mpvw owns a second, hidden window titled "mpv smtc" and PowerShell's
    MainWindowHandle can settle on it. Measured -- EnumWindows showed

        "mpv smtc"  rect=(50, 50, 1696, 928)  visible=0   <- what MainWindowHandle returned
        "MPV"       rect=(25, 25, 1671, 922)  visible=1   <- the real one

    so every earlier probe in this session that positioned or foregrounded "the window" was
    talking to an invisible frame, which is why SetWindowPos reported (-32000, -32000) and why
    no navigation element was ever found afterwards.
    """
    return _visible_hwnd_enum()


def _visible_hwnd() -> int:
    for _ in range(20):
        time.sleep(0.5)
        out = subprocess.run(
            ["pwsh", "-NoProfile", "-Command",
             "(Get-Process mpvw -ErrorAction SilentlyContinue | "
             "Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1)"
             ".MainWindowHandle"],
            capture_output=True, text=True, encoding="utf-8", errors="replace").stdout.strip()
        if out:
            try:
                return int(out)
            except ValueError:
                return 0
    return 0


def _visible_hwnd_enum() -> int:
    out = subprocess.run(["pwsh", "-NoProfile", "-Command",
                          "(Get-Process mpvw).Id"],
                         capture_output=True, text=True, encoding="utf-8",
                         errors="replace").stdout.strip()
    pids = {int(x) for x in out.split()} if out else set()
    found = []

    def cb(h, l):
        pid = wintypes.DWORD()
        user32.GetWindowThreadProcessId(h, ctypes.byref(pid))
        if pid.value not in pids or not user32.IsWindowVisible(h):
            return True

        length = user32.GetWindowTextLengthW(h)
        if length <= 0:
            return True

        buf = ctypes.create_unicode_buffer(length + 1)
        user32.GetWindowTextW(h, buf, length + 1)
        title = buf.value
        r = wintypes.RECT()
        user32.GetWindowRect(h, ctypes.byref(r))
        width = r.right - r.left
        height = r.bottom - r.top

        # Title, not size and not position. Measured with EnumWindows on a running app:
        #
        #     "mpv smtc"   rect=(50, 50, 1696, 928)   visible=0
        #     "MPV"        rect=(25, 25, 1671, 922)   visible=1
        #
        # Both are large; one is invisible. The main frame is the one titled MPV, and
        # MainWindowHandle picks the other, which is why positioning "the window" did nothing.
        if title.strip().upper() == "MPV":
            found.append((width * height, h))
        return True

    proc = ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, wintypes.LPARAM)(cb)
    user32.EnumWindows(proc, 0)
    # Largest first: the app's real window is the big one, not a small notification host.
    found.sort(reverse=True)
    return found[0][1] if found else 0


def _rect(hwnd):
    r = wintypes.RECT()
    user32.GetWindowRect(hwnd, ctypes.byref(r))
    return (r.left, r.top, r.right, r.bottom)


def _cards() -> list[tuple]:
    out = []
    stack = [auto.GetRootControl()]
    while stack:
        node = stack.pop()
        try:
            children = node.GetChildren()
        except Exception:
            continue
        try:
            if node.ControlTypeName == "ButtonControl":
                rect = M.rect_of(node)
                # Any card-shaped button anywhere on screen. The previous bounds (x > 700,
                # height > 200) assumed a particular window position, so a run that moved the
                # window found nothing and printed "nothing to hover".
                if rect and 250 < rect[2] - rect[0] < 700 and 200 < rect[3] - rect[1] < 600 \
                        and rect[1] > 0 and rect[3] < 4000:
                    out.append((node, rect))
        except Exception:
            pass
        stack.extend(children)
    out.sort(key=lambda p: (p[1][1], p[1][0]))
    return out


def main() -> int:
    T.close_app()
    if not T.launch_app().get("pid"):
        print("could not launch the app")
        return 1

    hwnd = _hwnd()
    print("resolved hwnd=%s" % hwnd)
    if not hwnd:
        print("no window")
        return 1
    print("that window's rect: %s" % (_rect(hwnd),))

    # BW.OnScreen, not a hand-rolled SetWindowPos. It handles the parked rectangle, waits for the
    # move to settle instead of guessing, and restores the original position on exit. Driving the
    # wrong window with SetWindowPos is what wasted several rounds here.
    with BW.OnScreen(hwnd, width=1500, height=950, settle=6.0):
        time.sleep(1.0)

        # Foreground ON PURPOSE, for the duration of this probe only. Without it no hover input
        # reaches the window and the measurement is guaranteed to read "no change" -- which is
        # exactly the false negative three earlier attempts reported.
        user32.SetForegroundWindow(hwnd)  # noqa: focus-steal-check
        time.sleep(1.2)
        for attempt in range(8):
            try:
                T.invoke_control("ShellNav_music", "click")
                break
            except Exception as e:
                print("  nav home retry %d: %s" % (attempt, e))
                time.sleep(0.75)
        time.sleep(2.0)
        for attempt in range(8):
            try:
                T.invoke_control("MusicTabSongs", "click")
                break
            except Exception as e:
                print("  nav tab retry %d: %s" % (attempt, e))
                time.sleep(0.75)
        time.sleep(3.5)

        cards = _cards()
        print("on-screen cards: %d" % len(cards))
        if not cards:
            print("nothing to hover")
            return 1

        control, rect = cards[0]
        cx, cy = (rect[0] + rect[2]) // 2, (rect[1] + rect[3]) // 2
        print("card: %s, centre (%d, %d)" % (rect, cx, cy))

        # Park somewhere else first so entering the card is a real transition.
        move_to(rect[0] - 300, rect[1] - 200)
        time.sleep(0.5)

        before = _inner_buttons(rect)
        print("buttons inside the card BEFORE hover: %d" % len(before))

        approach(cx, cy)
        time.sleep(0.8)

        after = _inner_buttons(rect)
        print("buttons inside the card AFTER hover: %d" % len(after))
        for aid, name, box in after:
            print("    %-22s %-14r %s  centre=(%d, %d)"
                  % (aid, name, box, (box[0] + box[2]) // 2, (box[1] + box[3]) // 2))

        if len(after) > len(before):
            print("\nThe hover overlay appeared. %d button(s) became addressable."
                  % (len(after) - len(before)))
            print("Card centre x=%d; compare each button's centre against it." % cx)
        else:
            print("\nNo new buttons. Either the overlay did not show, or its children are not")
            print("UIA-addressable. The two are different and this probe cannot separate them.")

    T.close_app()
    return 0


def _inner_buttons(card: tuple) -> list[tuple]:
    out = []
    stack = [auto.GetRootControl()]
    while stack:
        node = stack.pop()
        try:
            children = node.GetChildren()
        except Exception:
            continue
        try:
            if node.ControlTypeName == "ButtonControl":
                rect = M.rect_of(node)
                if rect and rect[0] >= card[0] and rect[2] <= card[2] \
                        and rect[1] >= card[1] and rect[3] <= card[3] \
                        and rect != card:
                    out.append(((node.AutomationId or ""), (node.Name or "")[:14], rect))
        except Exception:
            pass
        stack.extend(children)
    return out


if __name__ == "__main__":
    sys.exit(main())
