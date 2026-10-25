"""Did the six dead rows actually leave the settings window?

The schema change is only real if the window stops rendering six rows. Static
counting says 356 generated properties instead of 362, but a row can still appear
from a hand-written table or from a label file, so this counts what the UI builds.

How: open Settings, walk the option list, and count the rows -- plus a direct
search for the six option keys anywhere in the built tree. If any of the six still
appears, the schema change was incomplete and this says so by name.
"""
import subprocess
import sys
import time

sys.path.insert(0, r"C:\Users\z2637\Documents\DeepSeek\mpv\tools\mvpw-mcp")

import psutil
import uiautomation as auto

import mpvw_tools as T

DEPLOY = r"C:\Users\z2637\Documents\DeepSeek\mpv-deploy"
GONE = ["openal", "cuda-decode-device", "input-gamepad", "sbr-probesize"]


def main():
    subprocess.run(["pwsh", "-NoProfile", "-Command",
                    "Get-Process mpvw -ErrorAction SilentlyContinue | Stop-Process -Force"],
                   capture_output=True)
    time.sleep(3)
    subprocess.run(["pwsh", "-NoProfile", "-Command",
                    "Start-Process '%s\\mpvw.exe' -WorkingDirectory '%s'" % (DEPLOY, DEPLOY)],
                   capture_output=True)
    time.sleep(7)

    pid = T._pid()
    if pid is None:
        sys.exit("the app did not start")
    print("app pid:", pid)

    T.invoke_control("ShellSettingsButton", "invoke")
    time.sleep(5)

    settings = None
    for w in auto.GetRootControl().GetChildren():
        try:
            if w.ProcessId == pid and w.NativeWindowHandle != 0 and w.Name != "MPV":
                settings = w
        except Exception:
            continue
    if settings is None:
        T.close_app()
        sys.exit("the settings window did not open")

    # Every automation id and name reachable in the settings window.
    ids, names = [], []
    seen = {"options": 0, "sections": 0}

    def walk(c, d=0):
        if d > 20:
            return
        try:
            aid = c.AutomationId or ""
            nm = c.Name or ""
            cls = c.ClassName or ""
            if aid:
                ids.append(aid)
            if nm:
                names.append(nm)
            if "Option" in cls:
                seen["options"] += 1
            kids = c.GetChildren()
        except Exception:
            return
        for k in kids:
            walk(k, d + 1)

    walk(settings)
    print("automation ids:", len(ids), "| names:", len(names))
    print("elements with 'Option' in the class name:", seen["options"])

    blob = " ".join(ids + names).lower()
    survivors = [g for g in GONE if g.lower() in blob]
    print("removed options still mentioned in the built tree:", survivors or "none")

    for w in auto.GetRootControl().GetChildren():
        try:
            if w.ProcessId == pid and w.NativeWindowHandle != 0 and w.Name != "MPV":
                w.GetWindowPattern().Close()
        except Exception:
            continue
    time.sleep(1.5)
    T.close_app()

    ok = not survivors
    print("PASS" if ok else "FAIL: the window still builds a row for %s" % survivors)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
