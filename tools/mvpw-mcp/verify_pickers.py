"""Does the migrated folder picker still open?

The migration replaced Windows.Storage.Pickers with Microsoft.Windows.Storage
.Pickers. Those return PickFolderResult instead of StorageFolder, and take a
WindowId in the constructor instead of needing InitializeWithWindow -- so the
thing that could plausibly break is the dialog never appearing, or the app dying
when it is asked for.

UIA cannot choose a folder in the system dialog, so this asserts the two things
it CAN: the app survives the click, and a second top-level window appears while
the dialog is up. It then cancels by closing that window.
"""
import subprocess
import os
import sys
import time

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "tools", "mvpw-mcp"))

import psutil
import uiautomation as auto

import mpvw_tools as T

DEPLOY = r"C:\Users\z2637\Documents\DeepSeek\mpv-deploy"


def alive():
    return any((p.info["name"] or "").lower() == "mpvw.exe"
               for p in psutil.process_iter(["name"]))


def windows_of(pid):
    out = []
    for w in auto.GetRootControl().GetChildren():
        try:
            if w.ProcessId == pid:
                out.append((w.ClassName, (w.Name or "")[:60]))
        except Exception:
            continue
    return out


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
    print("pid:", pid)
    if pid is None:
        sys.exit("the app did not start")

    print("windows before:", windows_of(pid))

    T.invoke_control("ShellNav_videos", "select")
    time.sleep(2.0)

    try:
        T.invoke_control("VideoAddFolderButton", "invoke")
    except T.ToolError as e:
        print("could not click the add-folder button:", e)
        T.close_app()
        sys.exit(1)

    time.sleep(3.0)
    print("app alive after the click:", alive())
    after = windows_of(pid)
    print("windows after:", after)

    before_count = 1
    opened = len(after) > before_count
    print("a dialog window appeared:", opened)

    # Close whatever opened, then the app, and leave nothing behind.
    for w in auto.GetRootControl().GetChildren():
        try:
            if w.ProcessId == pid and w.ClassName != "WinUIDesktopWin32WindowClass":
                w.GetWindowPattern().Close()
                print("closed dialog:", w.ClassName)
        except Exception:
            continue
    time.sleep(1.0)
    T.close_app()

    ok = alive() is False and opened
    print("PASS" if ok else "FAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
