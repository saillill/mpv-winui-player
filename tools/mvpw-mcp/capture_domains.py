"""Capture one screenshot per functional domain, for looking at rather than
asserting on.

The geometry audit answers "is the element there and plausibly sized". It
cannot answer "does this look wrong" -- overlapping text, a control drawn over
its own label, a pane painted the same colour as the content behind it, an icon
that landed outside its button. Those are exactly what the brief asks about
("显示错乱"), and the only way to find them is to render each state and look.

Each capture is full-window at native scale, named after the domain and the
state it shows, into _work/mcp/shots/.
"""
import os
import sys
import time

import mpvw_tools as T

# Repo root, derived from this file (it lives at <repo>/tools/...), so renaming or
# moving the checkout does not silently repoint these paths at an old copy.
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

CLIP = r"D:\Workspace\WorkBuddy\MPV\.workbuddy\tmp\clips\sdr-bt709.mp4"
AUDIO = r"D:\Workspace\WorkBuddy\MPV\.workbuddy\tmp\clips\tagged.mp3"
OUT = REPO + r"\_work\mcp\shots"

REG = r"HKCU:\Software\Classes\Local Settings\Software\mpv-winui\mpv-winui\app"


def reg(name, value=None):
    import subprocess
    if value is None:
        r = subprocess.run(
            ["pwsh", "-NoProfile", "-Command",
             f'(Get-ItemProperty -Path "{REG}" -Name {name} -ErrorAction SilentlyContinue).{name}'],
            capture_output=True, text=True)
        return (r.stdout or "").strip()
    subprocess.run(["pwsh", "-NoProfile", "-Command",
                    f'Set-ItemProperty -Path "{REG}" -Name {name} -Value "{value}" -Type String'],
                   capture_output=True, text=True)


def shot(name):
    path = os.path.join(OUT, name + ".png")
    T.screenshot_region(path=path)
    print("  " + path)
    return path


def back():
    ids = T.get_ui_snapshot(max_depth=22)["elements"]
    have_nav = any(e["automationId"].startswith("ShellNav_")
                   for e in ids if e["automationId"])
    if not have_nav:
        try:
            T.invoke_control("ShellBackButton", "invoke")
        except T.ToolError:
            pass
        time.sleep(2.2)
    T._ipc(["stop"], 705)
    time.sleep(1.0)


def main():
    os.makedirs(OUT, exist_ok=True)
    T.close_app()
    reg("ThemeType", "Dark")
    T.launch_app()
    time.sleep(3.0)

    print("\n-- library, dark --")
    back()
    for key in ("home", "music", "videos", "queue"):
        T.invoke_control("ShellNav_" + key, "select")
        time.sleep(1.8)
        shot("01-" + key)

    print("\n-- playing --")
    T._ipc(["loadfile", CLIP, "replace"], 810)
    time.sleep(4.0)
    shot("02-playing")
    # Overlay bar: move the mouse over the picture area so the bar shows.
    print("\n-- player menus / panels --")
    for aid, name in (("PlaybackInfoBar", "info-bar"),
                      ("VolumeMuteButton", "volume"),
                      ("TrackSelectionButton", "tracks"),
                      ("ControlPanelButton", "control-panel"),
                      ("MoreButton", "more-menu"),
                      ("FullWindowButton", "full-window")):
        try:
            T.invoke_control(aid, "invoke" if aid != "VolumeMuteButton" else "click")
            time.sleep(1.4)
            shot("03-" + name)
            # Close whatever opened, with Escape equivalent.
            T._ipc(["keypress", "ESC"], 820)
            time.sleep(0.6)
        except T.ToolError as e:
            print("  skip %-20s %s" % (aid, e))

    print("\n-- audio playing --")
    T._ipc(["loadfile", AUDIO, "replace"], 830)
    time.sleep(3.5)
    shot("04-audio")

    print("\n-- light theme --")
    back()
    reg("ThemeType", "Light")
    T.close_app()
    T.launch_app()
    time.sleep(3.0)
    shot("05-light-home")

    print("\n-- skins --")
    for skin in ("Centre", "Compact"):
        reg("PlayerStyle", skin)
        T.close_app()
        T.launch_app()
        time.sleep(3.0)
        T._ipc(["loadfile", CLIP, "replace"], 840)
        time.sleep(3.5)
        shot("06-skin-" + skin.lower())
        T._ipc(["stop"], 845)
        time.sleep(1.0)

    print("\n-- settings --")
    reg("PlayerStyle", "")
    reg("ThemeType", "Dark")
    T.close_app()
    T.launch_app()
    time.sleep(3.0)
    pid = T._pid()
    T.invoke_control("ShellSettingsButton", "invoke")
    time.sleep(3.5)
    import uiautomation as auto
    for w in auto.GetRootControl().GetChildren():
        try:
            if w.ProcessId == pid and w.Name != "MPV" and w.NativeWindowHandle != 0:
                r = w.BoundingRectangle
                T.screenshot_region(
                    path=os.path.join(OUT, "07-settings.png"),
                    rect=[int(r.left), int(r.top), int(r.right), int(r.bottom)])
                print("  " + os.path.join(OUT, "07-settings.png"))
                w.GetWindowPattern().Close()
                break
        except Exception:
            continue
    time.sleep(1.5)

    T.close_app()
    reg("ThemeType", "Auto")
    print("\ndone; no process left:",
          T._pid() is None)


if __name__ == "__main__":
    sys.exit(main())
