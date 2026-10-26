"""Minimal reproduction of the native-preview crash.

diag_crash.py showed: with EnableVideoPreview=native on, loading a clip leaves
the app dead. It also showed a second signal in the same run -- "cannot open
\\\\.\\pipe\\mpvpipe: error 2" -- which happens when the process has gone but
psutil has not caught up, i.e. an app that exits *during startup*, not during
playback.

That distinction decides which code path to look at:

  exit during startup  -> something read at Initialize/MpvPlayer construction
  exit during playback -> the preview request path

This reproduces it step by step and records, for each, whether the window ever
appeared:

  1. native off, load            (control)
  2. native on,  no media        (does the app come up at all?)
  3. native on,  load            (the failing case)
  4. native on,  load a different clip  (is it the clip or the setting?)
"""
import json
import os
import subprocess
import sys
import time

import psutil

import mpvw_tools as T

# Fixture directory for the probe clips this file loads. Derived from __file__
# (this file lives at <repo>/tools/...), so archiving or renaming anything
# outside the repo cannot silently point these at a path that no longer exists.
# The clips themselves live in tools/mvpw-mcp/fixtures/ and are committed.
FIXTURES = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "tools", "mvpw-mcp", "fixtures")

# Repo root, derived from this file (it lives at <repo>/tools/...), so renaming or
# moving the checkout does not silently repoint these paths at an old copy.
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

A = os.path.join(FIXTURES, "sdr-bt709.mp4")
B = os.path.join(FIXTURES, "hdr10-pq.mp4")
LONG = REPO + r"\_work\mcp\clips\long-60s.mp4"
REG = r"HKCU:\Software\Classes\Local Settings\Software\mpv-winui\mpv-winui\app"


def reg(name, value=None):
    if value is None:
        r = subprocess.run(
            ["pwsh", "-NoProfile", "-Command",
             f'(Get-ItemProperty -Path "{REG}" -Name {name} -ErrorAction SilentlyContinue).{name}'],
            capture_output=True, text=True)
        return (r.stdout or "").strip()
    subprocess.run(["pwsh", "-NoProfile", "-Command",
                    f'Set-ItemProperty -Path "{REG}" -Name {name} -Value "{value}" -Type String'],
                   capture_output=True, text=True)


def case(label, native, clip=None):
    reg("EnableVideoBuiltInPreview", "False" if native else "True")
    reg("EnableVideoPreview", "True" if native else "False")
    T.close_app()
    try:
        T.launch_app(wait_seconds=20)
        launched = True
    except T.ToolError as e:
        print("  %-28s LAUNCH FAILED: %s" % (label, e))
        return {"label": label, "launched": False, "error": str(e)}

    alive_before_load = T._pid() is not None
    if clip and alive_before_load:
        try:
            T._ipc(["loadfile", clip, "replace"], 900)
        except T.ToolError as e:
            print("  %-28s ipc failed: %s" % (label, e))
        time.sleep(6.0)

    alive_after = T._pid() is not None
    st = None
    if alive_after:
        try:
            st = T.read_playback_state()
        except T.ToolError:
            st = None
    print("  %-28s launched=%-5s aliveAfterLoad=%-5s playing=%r" % (
        label, launched, alive_after,
        (st or {}).get("filename")))
    T.close_app()
    return {"label": label, "launched": launched,
            "aliveBeforeLoad": alive_before_load, "aliveAfter": alive_after,
            "playing": (st or {}).get("filename")}


def main():
    out = []
    out.append(case("native off, load sdr", False, A))
    out.append(case("native on, no media", True, None))
    out.append(case("native on, load sdr", True, A))
    out.append(case("native on, load hdr", True, B))
    out.append(case("native on, load long", True, LONG))

    reg("EnableVideoBuiltInPreview", "True")
    reg("EnableVideoPreview", "False")

    path = REPO + r"\_work\mcp\native-crash.json"
    with open(path, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
    print("\nwrote", path)
    print("restored defaults; process left:", T._pid() is not None)


if __name__ == "__main__":
    sys.exit(main())
