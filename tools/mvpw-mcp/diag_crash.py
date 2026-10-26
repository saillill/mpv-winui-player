"""Did this round break the app, or did it always crash on this clip?

A 57 MB minidump for mpvw.exe exists at 12:26. That is the window during this
session's audits, so it has to be explained before anything else is claimed.

The two candidate causes are distinguishable:

  A. the accessible-name change -- it only adds AutomationProperties.SetName
     calls, which cannot crash a media load; but "cannot" is a claim, so test it
     by reverting the three files to the previous commit, building, and running
     the same sequence.
  B. a pre-existing crash on this media file -- hdr10-pq.mp4 is a 2-second
     synthetic HDR clip, and the audit drove it into the native preview path,
     the HDR auto-switch script and the built-in preview at once.

Cheapest discriminator first: run the exact sequence that preceded the dump
against the CURRENT build and see whether it reproduces. Reproducible on the
current build, then bisect. Not reproducible, then find what the audit did
differently (it changed several registry settings, including both preview
switches and the theme).
"""
import json
import os
import subprocess
import sys
import time

import psutil

import mpvw_tools as T

CLIP = r"D:\Workspace\WorkBuddy\MPV\.workbuddy\tmp\clips\hdr10-pq.mp4"
REG = r"HKCU:\Software\Classes\Local Settings\Software\mpv-winui\mpv-winui\app"
DUMPS = os.path.join(os.environ["LOCALAPPDATA"], "CrashDumps")


def dumps():
    if not os.path.isdir(DUMPS):
        return []
    return sorted(
        (p for p in os.listdir(DUMPS) if p.startswith("mpvw")),
        key=lambda n: os.path.getmtime(os.path.join(DUMPS, n)))


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


def attempt(label, steps):
    before = set(dumps())
    T.close_app()
    T.launch_app()
    time.sleep(3.0)
    ok = True
    try:
        steps()
    except Exception as e:
        print("    step failed: %s: %s" % (type(e).__name__, e))
        ok = False
    time.sleep(1.5)
    alive = T._pid() is not None
    new = set(dumps()) - before
    print("  %-34s app alive=%-5s new dump=%s" % (label, alive, bool(new)))
    T.close_app()
    return alive, new


def main():
    print("dumps before:", dumps())
    print("current settings: builtin=%r native=%r theme=%r style=%r" % (
        reg("EnableVideoBuiltInPreview"), reg("EnableVideoPreview"),
        reg("ThemeType"), reg("PlayerStyle")))

    results = {}

    def load_and_wait():
        T._ipc(["loadfile", CLIP, "replace"], 900)
        time.sleep(6.0)
        T.read_playback_state()

    results["load hdr clip"] = attempt("load hdr10-pq.mp4", load_and_wait)

    def load_then_panel():
        T._ipc(["loadfile", CLIP, "replace"], 901)
        time.sleep(4.0)
        T.invoke_control("ControlPanelButton", "invoke")
        time.sleep(2.0)

    results["load + control panel"] = attempt("load + open control panel", load_then_panel)

    def load_both_previews():
        reg("EnableVideoBuiltInPreview", "False")
        reg("EnableVideoPreview", "True")
        T.close_app()
        T._ipc(["loadfile", CLIP, "replace"], 902)
        time.sleep(6.0)

    results["native preview path"] = attempt("native preview switch", load_both_previews)

    reg("EnableVideoBuiltInPreview", "True")
    reg("EnableVideoPreview", "False")
    print("\ndumps after:", dumps())
    print("restored preview switches; process left:", T._pid() is not None)


if __name__ == "__main__":
    sys.exit(main())
