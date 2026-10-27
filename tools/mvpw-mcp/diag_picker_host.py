"""Is the picker dialog hosted by another process?

The previous check looked only at windows whose ProcessId equals the app's and
concluded "no dialog appeared". That is the wrong filter for this dialog: the
Windows App SDK pickers are the modern shell pickers, which are hosted by a
separate process, so the app's own window list never grows.

So this compares ALL top-level windows before and after the click, and reports any
new ones with their owning process. That distinguishes the two cases honestly:

  a new window owned by another process  -> the dialog opened, the filter was wrong
  no new window at all                   -> the click did not reach the picker
"""
import subprocess
import os
import sys
import time

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "tools", "mvpw-mcp"))

import psutil
import uiautomation as auto

import mpvw_tools as T
import paths

DEPLOY = paths.deploy_dir()
def all_windows():
    out = {}
    for w in auto.GetRootControl().GetChildren():
        try:
            key = (w.ClassName, w.Name or "")
            out[key] = w.ProcessId
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
    print("app pid:", pid)
    before = all_windows()
    print("top-level windows before:", len(before))

    T.invoke_control("ShellNav_videos", "select")
    time.sleep(2.0)
    T.invoke_control("VideoAddFolderButton", "invoke")
    time.sleep(4.0)

    after = all_windows()
    new = {k: v for k, v in after.items() if k not in before}
    print("top-level windows after:", len(after))
    print("new windows:", len(new))
    for (cls, name), owner in new.items():
        try:
            pname = psutil.Process(owner).name()
        except Exception:
            pname = "?"
        print("   %-40s %-30s owner=%s(%s)" % (cls[:40], name[:30], pname, owner))

    print("app still alive:", T._pid() is not None)

    # Leave nothing running.
    for (cls, name), owner in new.items():
        try:
            for w in auto.GetRootControl().GetChildren():
                if w.ClassName == cls and (w.Name or "") == name:
                    w.GetWindowPattern().Close()
        except Exception:
            continue
    T.close_app()

    print("\nRESULT: dialog opened =", bool(new))
    return 0 if new else 1


if __name__ == "__main__":
    sys.exit(main())
