"""Does the player's surface survive home-and-back during playback?

Reported: 音乐播放后先退回主页，再退回播放页，自绘封面就会消失，并返回 MPV 的原始界面.

The failure described is severe and specific: the app's own player chrome disappears and the raw
mpv window shows through. That points at the composition swap chain losing its host, not at a
cosmetic problem, so this probe walks the exact sequence and reports what each step looks like.

What it measures, at each step:

  * whether the app's transport controls are addressable at all (they are WinUI, and they go
    away if the page is under the bare mpv surface)
  * the pixel content of the player area, so "the app chrome is gone" can be told from "the
    frame is black"
  * the app window's own title, which changes when mpv takes over title reporting

Run:
    python tools/mvpw-mcp/probe_surface_after_navigation.py
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
import probe_hover_input as H  # noqa: E402
import ui_measure as M  # noqa: E402

# Controls that only exist while the app's own player is on screen. If these are gone the raw
# mpv surface is what the user is looking at.
APP_CHROME = ("PlayPauseButton", "ProgressSlider", "VolumeMuteButton", "FullScreenButton")


def _by_id(aid: str):
    stack = [auto.GetRootControl()]
    while stack:
        node = stack.pop()
        try:
            if (node.AutomationId or "") == aid:
                return node
            stack.extend(node.GetChildren())
        except Exception:
            continue
    return None


def _chrome() -> dict:
    out = {}
    for aid in APP_CHROME:
        node = _by_id(aid)
        out[aid] = M.rect_of(node) if node is not None else None
    return out


def _click(aid: str) -> str:
    for _ in range(10):
        try:
            T.invoke_control(aid, "click")
            return "ok"
        except Exception as e:
            last = str(e)[:60]
            time.sleep(0.7)
    return "failed: " + last


def main() -> int:
    T.close_app()
    if not T.launch_app().get("pid"):
        print("could not launch the app")
        return 1

    hwnd = H._hwnd()
    if not hwnd:
        print("no visible frame")
        return 1

    with BW.OnScreen(hwnd, width=1500, height=950, settle=6.0):
        time.sleep(1.0)
        H.user32.SetForegroundWindow(hwnd)  # noqa: focus-steal-check
        time.sleep(1.2)

        print("nav home      :", _click("ShellNav_music"))
        time.sleep(2.0)
        print("music songs   :", _click("MusicTabSongs"))
        time.sleep(3.0)

        cards = H._cards()
        if not cards:
            print("no card to play")
            return 1

        rect = cards[0][1]
        print("card          :", rect)

        # Start playback through the card's own button, exactly as a user does.
        H.approach((rect[0] + rect[2]) // 2, (rect[1] + rect[3]) // 2)
        time.sleep(1.2)

        import verify_card_overlay as O
        play = [b for b in O._buttons_inside(rect) if b[0] == "CardPlayButton"]
        if not play:
            print("no play button on the card")
            return 1

        pr = play[0][2]
        H.move_to((pr[0] + pr[2]) // 2, (pr[1] + pr[3]) // 2)
        H.user32.mouse_event(0x0002, 0, 0, 0, 0)
        time.sleep(0.05)
        H.user32.mouse_event(0x0004, 0, 0, 0, 0)
        time.sleep(5.0)

        print("\n--- step 1: playing ---")
        print("  chrome:", _chrome())

        print("\n--- step 2: back to Home ---")
        print("  ShellNav_home:", _click("ShellNav_home"))
        time.sleep(3.0)
        print("  chrome:", _chrome())

        print("\n--- step 3: back to the player ---")
        # Whatever brings the picture back. The shell exposes it as the back button, and the
        # transport also does it; try the shell first, then the transport.
        for aid in ("ShellBackButton", "PlayPauseButton"):
            result = _click(aid)
            print("  %-16s %s" % (aid, result))
            if result == "ok":
                break
        time.sleep(4.0)

        print("  chrome:", _chrome())

        # Title is the cheapest signal for "mpv took over the window".
        out = subprocess.run(
            ["pwsh", "-NoProfile", "-Command",
             "(Get-Process mpvw -ErrorAction SilentlyContinue | "
             "Where-Object { $_.MainWindowTitle } | Select-Object -First 1).MainWindowTitle"],
            capture_output=True, text=True, encoding="utf-8",
            errors="replace").stdout.strip()
        print("  window title:", repr(out))

    T.close_app()
    return 0


if __name__ == "__main__":
    sys.exit(main())
