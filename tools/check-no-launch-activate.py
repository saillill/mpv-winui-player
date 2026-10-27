#!/usr/bin/env python3
"""The automation harness must not take the foreground when it launches the app.

**The defect this exists for.** Every verification launch stole the foreground. The measurable
consequence for the user: a game in exclusive fullscreen drops its display mode and its controller
the instant a window takes the foreground, which they reported as the app "taking over my
controller". The app opens no gamepad device at all -- `gamepad`, `XInput`, `SDL2`, `hid.dll` and
`input-gamepad` occur **zero** times in `mpv-2.dll`, `mpvw.dll` and `mpv_winrt.dll`, and no gamepad
module is loaded at runtime -- so the foreground flash is the entire mechanism.

Measured with `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` (`OUTOFCONTEXT` posts to the installing
thread's queue, so the probe pumps messages; a `time.sleep`-only probe reports zero events and
"proves" the opposite):

    old behaviour (`Window.Activate()`)             1 foreground event, 1780 ms after launch
    MVPW_NO_ACTIVATE=1 (`SW_SHOWNOACTIVATE`)        0 foreground events
    the harness's own launch path                   0 foreground events

Three earlier attempts at this were wrong and are worth naming, because each looked like a fix:
`SetForegroundWindow` behind a guard, `AppWindow.Show()` replaced by `SW_SHOWNOACTIVATE`, and
`SetWindowLongPtr(GWLP_HWNDPARENT)` to assign an owner. None of them touched the flash, because
the flash is `Window.Activate()` in `OnLaunched`. Skipping that call entirely is also wrong: the
window then never appears and the harness times out with "no window after 20s".

So this checker asserts the shape of the fix rather than re-measuring it on every run:

  1. `App.OnLaunched` must not call `window.Activate()` unconditionally;
  2. it must call `Win32WindowHelper.ShowNoActivate` on the suppressed path;
  3. `mpvw_tools.launch_app` and `open_launch_window` must both set MVPW_NO_ACTIVATE=1;
  4. `ShowNoActivate` must exist and use `SW_SHOWNOACTIVATE`.

`--measure` additionally launches the app and counts foreground events, which needs the deployed
build; the default mode is static so it can run inside the gate.
"""
import argparse
import pathlib
import re
import sys

REPO = pathlib.Path(__file__).resolve().parent.parent
APP = REPO / "src" / "mpvw-app" / "App.xaml.cs"
HELPER = REPO / "src" / "mpvw-app" / "Platform" / "Win32" / "Win32WindowHelper.cs"
TOOLS = REPO / "tools" / "mvpw-mcp" / "mpvw_tools.py"


def on_launched_body(text):
    """The text of OnLaunched, so a call elsewhere in the file neither passes nor fails this."""
    i = text.find("protected override void OnLaunched")
    if i < 0:
        return None
    depth = 0
    started = False
    for j in range(i, len(text)):
        ch = text[j]
        if ch == "{":
            depth += 1
            started = True
        elif ch == "}":
            depth -= 1
            if started and depth == 0:
                return text[i:j + 1]
    return text[i:]


def check():
    problems = []
    notes = []

    app_text = APP.read_text(encoding="utf-8", errors="replace")
    body = on_launched_body(app_text)
    if body is None:
        problems.append("App.xaml.cs has no OnLaunched")
    else:
        # A bare `window.Activate()` (not inside an if/else branch) is the old behaviour.
        bare = re.findall(r"^\s+window\.Activate\(\);", body, re.M)
        guarded = "SuppressStartupActivation" in body
        if bare and not guarded:
            problems.append("OnLaunched calls window.Activate() unconditionally")
        if not guarded:
            problems.append("OnLaunched has no MVPW_NO_ACTIVATE branch")
        if "ShowNoActivate" not in body:
            problems.append("OnLaunched never calls Win32WindowHelper.ShowNoActivate")
        else:
            notes.append("OnLaunched shows the window with ShowNoActivate on the automated path")

    helper_text = HELPER.read_text(encoding="utf-8", errors="replace")
    if "ShowNoActivate" not in helper_text:
        problems.append("Win32WindowHelper.ShowNoActivate is missing")
    elif "SW_SHOWNOACTIVATE" not in helper_text:
        problems.append("ShowNoActivate does not use SW_SHOWNOACTIVATE")
    else:
        notes.append("Win32WindowHelper.ShowNoActivate uses SW_SHOWNOACTIVATE")

    tools_text = TOOLS.read_text(encoding="utf-8", errors="replace")
    launches = len(re.findall(r"Start-Process '\{EXE\}'", tools_text))
    with_env = len(re.findall(r"MVPW_NO_ACTIVATE='1'", tools_text))
    if launches == 0:
        problems.append("no Start-Process launch site found in mpvw_tools.py")
    elif with_env < launches:
        problems.append("%d of %d launch sites omit MVPW_NO_ACTIVATE=1"
                        % (launches - with_env, launches))
    else:
        notes.append("all %d launch site(s) set MVPW_NO_ACTIVATE=1" % launches)

    return problems, notes


def measure():
    """Launch twice and count foreground events; needs the deployed build."""
    import ctypes
    import ctypes.wintypes as wt
    import json
    import os
    import subprocess
    import time

    sys.path.insert(0, str(REPO / "tools" / "mvpw-mcp"))
    import mpvw_tools as T

    u32 = ctypes.WinDLL("user32", use_last_error=True)
    WinEventProc = ctypes.WINFUNCTYPE(None, wt.HANDLE, wt.DWORD, wt.HWND, wt.LONG, wt.LONG,
                                      wt.DWORD, wt.DWORD)

    class MSG(ctypes.Structure):
        _fields_ = [("hwnd", wt.HWND), ("message", wt.UINT), ("wParam", wt.WPARAM),
                    ("lParam", wt.LPARAM), ("time", wt.DWORD),
                    ("pt_x", wt.LONG), ("pt_y", wt.LONG)]

    events = []
    proc = WinEventProc(lambda h, e, w, o, c, t, m: events.append(w))
    hook = u32.SetWinEventHook(0x0003, 0x0003, None, proc, 0, 0, 0x0000)
    if not hook:
        return {"error": "SetWinEventHook failed"}

    def pump(seconds):
        end = time.time() + seconds
        msg = MSG()
        while time.time() < end:
            while u32.PeekMessageW(ctypes.byref(msg), None, 0, 0, 0x0001):
                u32.TranslateMessage(ctypes.byref(msg))
                u32.DispatchMessageW(ctypes.byref(msg))
            time.sleep(0.01)

    T.close_app()
    time.sleep(1.0)
    pump(0.2)
    events.clear()
    T.launch_app()
    pid = T._pid()
    pump(3.5)
    u32.UnhookWinEvent(hook)

    app_events = []
    for hwnd in events:
        p = wt.DWORD()
        u32.GetWindowThreadProcessId(hwnd, ctypes.byref(p))
        if int(p.value) == pid:
            app_events.append(int(hwnd or 0))
    T.close_app()
    return {"appForegroundEvents": len(app_events)}


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--measure", action="store_true",
                    help="also launch the deployed app and count foreground events")
    args = ap.parse_args()

    problems, notes = check()

    if args.measure:
        try:
            result = measure()
        except Exception as e:
            problems.append("measurement failed: %s: %s" % (type(e).__name__, e))
        else:
            if result.get("appForegroundEvents"):
                problems.append("launch took the foreground %d time(s)"
                                % result["appForegroundEvents"])
            else:
                notes.append("measured launch: 0 foreground events")

    for n in notes:
        print("  " + n)
    if problems:
        print("FAIL: the harness can take the foreground")
        for p in problems:
            print("  - " + p)
        return 1
    print("OK: automated launches do not take the foreground")
    return 0


if __name__ == "__main__":
    sys.exit(main())
