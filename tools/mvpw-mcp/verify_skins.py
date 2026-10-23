"""Does the control bar actually work under the Centre and Compact skins?

The skin files are whole ControlTemplate copies (`CenterPlayerControlStyle.xaml`
line 15, `CompactPlayerControlStyle.xaml`) whose ContentPresenter count is 0,
while all 109 code-behind fields are wired to element names in
`PlayerControl.xaml`'s own content tree and the repo has no OnApplyTemplate /
GetTemplateChild at all. Under WinUI template semantics that predicts: layout
changes, handlers dead.

Rather than argue it, this loads a clip over the mpv pipe and then, for each of
the three skins, asks the real PlayPauseButton (found by AutomationId, i.e. the
live visual tree) to toggle `pause` and reads `pause` back.

If the Standard skin toggles and the other two do not, the prediction is
confirmed. If all three toggle, the fields survive because the template is not
actually being applied -- which would itself contradict the style lookup.

Run: python tools/mvpw-mcp/verify_skins.py
"""
import json
import subprocess
import sys
import time

import mpvw_tools as T

CLIP = r"D:\Workspace\WorkBuddy\MPV\.workbuddy\tmp\clips\sdr-bt709.mp4"
REG_PATH = r"HKCU:\Software\Classes\Local Settings\Software\mpv-winui\mpv-winui\app"


def set_skin(value):
    """PlayerStyle is a registry string; 'Standard' / 'Centre' / 'Compact'."""
    ps = (
        f'Set-ItemProperty -Path "{REG_PATH}" -Name PlayerStyle -Value "{value}" '
        f'-Type String'
    )
    r = subprocess.run(["pwsh", "-NoProfile", "-Command", ps],
                       capture_output=True, text=True)
    return r.returncode == 0, (r.stderr or "").strip()


def get_pause():
    return T.read_playback_state().get("pause")


def main():
    results = {}
    T.close_app()

    print("clip: %s" % CLIP)
    for skin in ("Standard", "Centre", "Compact"):
        ok, err = set_skin(skin)
        if not ok:
            results[skin] = {"error": "could not set registry: " + err}
            continue

        T.close_app()
        # snapshot=True reads mpv's status inside the one-second window where the
        # pipe accepts a client. Without it there is no pause value at all, and the
        # second read this used to attempt is simply not available (see the module
        # docstring).
        info = T.launch_app(snapshot=True)
        before = get_pause()

        # Load the clip through the app itself, so the player view is live.
        T._ipc(["loadfile", CLIP, "replace"], 900)
        time.sleep(3.0)

        # The transport exists only while a file is loaded. Click it and then read
        # the BUTTON's state through UIA rather than asking mpv again.
        clicked = None
        try:
            T.invoke_control("PlayPauseButton", "invoke")
            clicked = True
        except T.ToolError as e:
            results[skin] = {"error": str(e), "pauseBefore": before}
            T.close_app()
            continue

        time.sleep(1.2)
        # Is the element still addressable and enabled after the skin switch?
        snap = T.get_ui_snapshot(max_depth=12, redact_names=True)
        by_id = {e["automationId"]: e for e in snap["elements"] if e["automationId"]}
        button = by_id.get("PlayPauseButton")

        results[skin] = {
            "pauseBefore": before,
            "clicked": clicked,
            # enabled + present is the observable claim: a skin that broke the
            # transport would leave the control missing or disabled.
            "transportUsable": bool(button) and button.get("enabled", True),
            "hasPlayPause": "PlayPauseButton" in by_id,
            "hasProgress": "ProgressSlider" in by_id,
        }
        print("  %-9s pauseBefore=%s clicked=%s usable=%s  ids(play/pbar)=%s/%s" % (
            skin, before, results[skin]["clicked"], results[skin]["transportUsable"],
            results[skin]["hasPlayPause"], results[skin]["hasProgress"]))
        T.close_app()

    set_skin("")
    T.close_app()

    print("\n" + json.dumps(results, indent=1, ensure_ascii=False))
    out = r"C:\Users\z2637\Documents\DeepSeek\mpv\_work\mcp\skin-toggle.json"
    with open(out, "w", encoding="utf-8") as f:
        json.dump(results, f, ensure_ascii=False, indent=1)
    print("wrote " + out)

    working = [k for k, v in results.items() if v.get("transportUsable")]
    print("\nskins whose transport stays usable after the switch: %s"
          % (working or "none"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
