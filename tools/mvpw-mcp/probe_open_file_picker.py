"""Probe: does clicking an open-file button start a picker on THIS machine?

**This is a probe, not a verifier, and it deliberately prints no VERDICT line.** A check that
cannot distinguish "the button does nothing" from "this machine has no picker" would be either a
false green or a false red, and either one is worse than no check.

Measured 2026-10-26, queue section, deployed build:

    click QueueOpenFileButton  ->  handler ran (logged "queue open-file handler entered")
                                   no new windowed process
                                   no new titled window
                                   no error, no exception, no log line

The click reaches the handler, the handler calls OpenFileFromShell, and from there nothing
observable happens. What this probe cannot tell you is WHY, and there is a specific reason to
hesitate before calling it a defect in the new button:

* `OpenFileFromShell()` -> `OpenFileAsync()` -> `new FileOpenPicker(_appWindow.Id)`. That is the
  SAME call every other open path in the app makes, including the shell's own menu entry.
* No other open-file button exists on this machine to compare against: Home's empty state
  (`LibraryOpenFileButton`) is not rendered because this library has folders bound, and the
  video section renders 添加文件夹 rather than an open-file button. The "does the pre-existing
  path also fail?" experiment that would settle it cannot be run here as-is.
* The WinAppSDK picker is brokered out of process. It can fail for reasons that are properties
  of the machine (no interactive shell session, a broker that is not running) and raise
  nothing back into the app.

Next steps for whoever picks this up, in order of how much each would settle:

1. Run this probe on a machine where an open-file dialog is known to work by hand, and click
   any existing open-file entry to get the baseline. Beating this probe there proves the button.
2. If it also fails there, the defect is in the picker plumbing, not in this button, and the
   queue button is correct by construction.
3. If it works there but fails here, the button is fine and this machine cannot answer.

Run:
    python tools/mvpw-mcp/probe_open_file_picker.py
"""
from __future__ import annotations

import os
import subprocess
import sys
import time

import uiautomation as auto

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import background_window as BW  # noqa: E402
import mpvw_tools as T  # noqa: E402


def _titled_windows() -> set[str]:
    out = set()
    for w in auto.GetRootControl().GetChildren():
        try:
            name = (w.Name or "").strip()
            if name:
                out.add(name)
        except Exception:
            continue
    return out


def _processes() -> set[str]:
    out = subprocess.run(
        ["pwsh", "-NoProfile", "-Command",
         "Get-Process | ForEach-Object { $_.ProcessName }"],
        capture_output=True, text=True, encoding="utf-8", errors="replace").stdout
    return {ln.strip() for ln in (out or "").splitlines() if ln.strip()}


def _hwnd() -> int:
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


def main() -> int:
    T.close_app()
    if not T.launch_app().get("pid"):
        print("could not launch the app")
        return 1

    hwnd = _hwnd()
    try:
        ctx = BW.OnScreen(hwnd, settle=4.0) if hwnd else _nullctx()
        with ctx:
            T.invoke_control("ShellNav_queue", "click")
            time.sleep(2.5)

            windows_before = _titled_windows()
            procs_before = _processes()

            T.invoke_control("QueueOpenFileButton", "click")
            time.sleep(5.0)

            windows_appeared = sorted(_titled_windows() - windows_before)
            procs_appeared = sorted(_processes() - procs_before)

            print("new titled windows : %s" % (windows_appeared or "NONE"))
            print("new processes      : %s" % (procs_appeared or "NONE"))

            if windows_appeared or procs_appeared:
                print("\nA picker appeared. The button works on this machine.")
            else:
                print("\nNothing appeared. Read this file's docstring before concluding "
                      "anything about the button: it is not by itself evidence against it.")
    finally:
        T.close_app()

    return 0


class _nullctx:
    def __enter__(self):
        return None

    def __exit__(self, *a):
        return False


if __name__ == "__main__":
    sys.exit(main())
