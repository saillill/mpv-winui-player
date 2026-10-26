"""Are the player's floating buttons actually reachable, or is something drawn over them?

The user's report: 视频和音频的播放悬浮按钮、菜单悬浮按钮图标均不正确，且处于被遮挡状态.

This measures the second half of that claim. Two things had to be fixed before the number
could be believed, both found by running it:

1. `ControlFromPoint` returns the top-most element, which in WinUI 3 is usually a
   PaneControl wrapper rather than the Button. Comparing with `is` reported EVERY control on
   the player bar as covered, including ones plainly visible. The check now accepts the
   control, its ancestors, and anything inside its own rectangle.
2. With another application's window maximised over the player, all nine samples came back
   as a foreign `GroupControl (0,0,3842,2078)` and the button read as hidden while it was
   merely behind the browser. That is a broken measurement, not a UI defect, so it is now
   reported as `measurement_invalid` and this script brings the player to the front first.

Run:
    python tools/mvpw-mcp/verify_overlay_buttons.py
    python tools/mvpw-mcp/verify_overlay_buttons.py --self-test
"""
from __future__ import annotations

import ctypes
import os
import subprocess
import sys
import time
from ctypes import wintypes

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import background_window as BW  # noqa: E402
import mpvw_tools as T  # noqa: E402
import ui_measure as M  # noqa: E402

CLIP = r"C:\Users\z2637\AppData\Local\Temp\probe-clip.mp4"

# The transport controls the report names. `MoreButton` is added only when the overflow
# table says the menu has content, so its absence is not a failure on its own -- it is
# reported and checked separately.
BUTTONS = (
    "PlayPauseButton",
    "PreviousTrackButton",
    "NextTrackButton",
    "VolumeMuteButton",
    "TrackSelectionButton",
    "PiPButton",
    "FullWindowButton",
    "ControlPanelButton",
)

user32 = ctypes.WinDLL("user32", use_last_error=True)

# Raise the app to the top of the Z order WITHOUT giving it focus.
#
# `SetForegroundWindow` is what check-no-focus-steal.py forbids in probe code, and it is
# right to: it moves the keyboard away from whoever is typing. But this measurement cannot
# work with the player behind another window -- measured, a maximised browser made all nine
# samples of PlayPauseButton come back as a foreign GroupControl spanning the whole screen,
# and the button read as "hidden" while it was merely behind the browser.
#
# SWP_NOACTIVATE is the difference that matters: the window comes to the front, the focus
# does not move, and foreground_guard() below proves it.
SWP_NOSIZE = 0x0001
SWP_NOMOVE = 0x0002
SWP_NOACTIVATE = 0x0010
HWND_TOP = 0
user32.SetWindowPos.argtypes = [wintypes.HWND, wintypes.HWND, ctypes.c_int,
                                ctypes.c_int, ctypes.c_int, ctypes.c_int,
                                ctypes.c_uint]


def _raise_without_activating(hwnd: int) -> None:
    user32.SetWindowPos(hwnd, HWND_TOP, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE)


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


def main() -> int:
    if "--self-test" in sys.argv or os.environ.get("MVPR_SELFTEST") == "1":
        failures = []
        if M.assert_not_obscured.__doc__ is None:
            failures.append("assert_not_obscured has no docstring")

        class Box:
            def __init__(self, l, t, r, b):
                self.BoundingRectangle = type("R", (), {"left": l, "top": t,
                                                        "right": r, "bottom": b})()
                self.Name = self.AutomationId = "probe"
                self.ControlTypeName = "ButtonControl"
                self.ProcessId = 0

        # A control with no rectangle must be reported as unmeasurable, not as visible.
        res = M.assert_not_obscured(Box(0, 0, 0, 0))
        if res["ok"]:
            failures.append("a control with no rectangle was reported as fine")
        good = not failures
        print("SELF-TEST %s: an unmeasurable control is reported as a failure"
              % ("PASSED" if good else "FAILED"))
        for f in failures:
            print("  " + f)
        return 0 if good else 1

    if not os.path.exists(CLIP):
        print("FAIL: clip missing: %s" % CLIP)
        print("VERDICT: FAIL -- nothing measured")
        return 1

    T.close_app()
    # Launch WITH the clip so the overlay bar is up: the transport buttons only exist in
    # the playing state, and measuring them on the library page would report them absent.
    subprocess.Popen([T.EXE, CLIP], cwd=T.DEPLOY_DIR)
    time.sleep(9.0)

    hwnd = _hwnd()
    if not hwnd:
        print("FAIL: no main window")
        print("VERDICT: FAIL -- nothing measured")
        T.close_app()
        return 1

    # Decisive: the measurement is only meaningful with the player at the top of the Z order.
    # SWP_NOACTIVATE raises it without taking the keyboard, and foreground_guard() fails the
    # run if the foreground window changes anyway.
    with BW.foreground_guard():
        _raise_without_activating(hwnd)
        time.sleep(2.5)

        results = {}
        for aid in BUTTONS:
            try:
                c = T._find(aid)
            except Exception:
                c = None
            if c is None:
                results[aid] = {"present": False}
                continue
            res = M.assert_not_obscured(c)
            res["present"] = True
            res["rect"] = M.rect_of(c)
            results[aid] = res
            tag = ("INVALID" if res.get("measurement_invalid")
                   else "COVERED" if res.get("ok") is False else "visible")
            print("  %-24s %-30s %s" % (aid, res["rect"], tag))

    # Closing must happen outside the guard: destroying the window changes the foreground
    # window by definition, and the guard would (correctly) call that focus theft. The first
    # version closed inside the `finally` of this block and the run aborted after every
    # measurement had already succeeded.
    T.close_app()

    good = True

    def expect(label, ok, detail=""):
        nonlocal good
        print("  %-5s %s%s" % ("OK" if ok else "FAIL", label,
                               "" if ok else "  " + str(detail)[:160]))
        good &= ok

    print("\n--- checks ---")
    invalid = [a for a, r in results.items() if r.get("measurement_invalid")]
    expect("the measurement was valid for every button", not invalid, invalid)

    missing = [a for a in ("PlayPauseButton", "ControlPanelButton") if not results[a].get("present")]
    expect("the two buttons the report names are present", not missing, missing)

    covered = [a for a, r in results.items()
               if r.get("present") and r.get("ok") is False and not r.get("measurement_invalid")]
    print("        covered: %s" % (covered or "none"))
    expect("no transport button is drawn over", not covered, covered)

    print("\nVERDICT: %s -- overlay transport buttons are reachable at their own pixels"
          % ("PASS" if good else "FAIL"))
    return 0 if good else 1


if __name__ == "__main__":
    sys.exit(main())
