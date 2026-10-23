"""Is menu-data empty because no file is loaded?

Not established before this: every read of `menu-data` so far happened ~1.2 s after
launch, with no media loaded, and answered `{'data': [], 'error': 'success'}`. I was
treating that as "the menu feature is broken". But dyn_menu.lua describes input.conf
bindings for commands that apply to what is PLAYING, so an empty menu with nothing
playing is a plausible correct answer, not a defect.

The way this repo has been wrong before is exactly this: asserting a mechanism and
never testing the case that would distinguish it. So:

  connect during the launch window -> loadfile -> then poll `menu-data` in a TIGHT loop.

Tight matters. verify_preview_api.py records that sleeping between requests kills the
conversation ("sleeping first made this fail") while polling immediately works. So the
wait for the file to open is done by re-reading, not by sleeping.

  non-empty  -> the menu depends on a loaded file; the startup read was the wrong
                moment, and there is no defect
  still empty -> the script loads, a file plays, and it still produces nothing: that
                is a real finding, and the next step is the script's own log
"""
import subprocess
import sys
import time

sys.path.insert(0, r"C:\Users\z2637\Documents\DeepSeek\mpv\tools\mvpw-mcp")

import psutil

import mpvw_tools as T

CLIP = r"D:\Workspace\WorkBuddy\MPV\.workbuddy\tmp\clips\sdr-bt709.mp4"


def main():
    subprocess.run(["pwsh", "-NoProfile", "-Command",
                    "Get-Process mpvw -ErrorAction SilentlyContinue | Stop-Process -Force"],
                   capture_output=True)
    time.sleep(3)

    T.open_launch_window(4.0)

    base = T._ipc(["get_property", "menu-data"], retry=False)
    print("before loading anything :", str(base)[:130])

    load = T._ipc(["loadfile", CLIP, "replace"], retry=False)
    print("loadfile                :", str(load)[:130])

    # No sleeps from here on: keep the conversation busy.
    best = None
    deadline = time.monotonic() + 8.0
    reads = 0
    while time.monotonic() < deadline:
        r = T._ipc(["get_property", "menu-data"], retry=False)
        reads += 1
        err = r.get("error") if isinstance(r, dict) else "?"
        data = r.get("data") if isinstance(r, dict) else None
        n = len(data) if isinstance(data, list) else 0
        if n:
            best = n, data
            break
        if err != "success":
            print("  read %d stopped: %s" % (reads, str(r)[:110]))
            break

    print("reads made              :", reads)
    if best:
        n, data = best
        print("menu-data AFTER load    : %d item(s)" % n)
        for item in data[:4]:
            print("   ", str(item)[:120])
        print("\nVERDICT: the menu is populated once media is playing.")
        print("         The empty read at startup was the wrong moment, not a defect.")
    else:
        print("menu-data after load    : still empty")
        print("\nVERDICT: the script is loaded and media plays, and menu-data stays")
        print("         empty. That is a real defect; next step is the script's log.")

    T.close_app()
    print("\nclosed:", not any(
        (p.info["name"] or "").lower() == "mpvw.exe"
        for p in psutil.process_iter(["name"])))


if __name__ == "__main__":
    sys.exit(main())
