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
        info = T.launch_app()
        time.sleep(1.5)

        # Load the clip through the app itself, so the player view is live.
        T._ipc(["loadfile", CLIP, "replace"], 900)
        time.sleep(3.0)

        before = get_pause()
        # The transport exists only while a file is loaded.
        try:
            T.invoke_control("PlayPauseButton", "invoke")
        except T.ToolError as e:
            results[skin] = {"error": str(e), "pauseBefore": before}
            T.close_app()
            continue

        time.sleep(1.2)
        after = get_pause()

        # Is the element still addressable after the skin switch?
        snap = T.get_ui_snapshot(max_depth=12, redact_names=True)
        ids = {e["automationId"] for e in snap["elements"] if e["automationId"]}

        results[skin] = {
            "pauseBefore": before,
            "pauseAfter": after,
            "toggled": before is not None and after is not None and before != after,
            "hasPlayPause": "PlayPauseButton" in ids,
            "hasProgress": "ProgressSlider" in ids,
        }
        print("  %-9s pause %s -> %s   toggled=%s  ids(play/pbar)=%s/%s" % (
            skin, before, after, results[skin]["toggled"],
            results[skin]["hasPlayPause"], results[skin]["hasProgress"]))
        T.close_app()

    set_skin("")
    T.close_app()

    print("\n" + json.dumps(results, indent=1, ensure_ascii=False))
    out = r"C:\Users\z2637\Documents\DeepSeek\mpv\_work\mcp\skin-toggle.json"
    with open(out, "w", encoding="utf-8") as f:
        json.dump(results, f, ensure_ascii=False, indent=1)
    print("wrote " + out)

    working = [k for k, v in results.items() if v.get("toggled")]
    print("\nskins whose transport toggles pause: %s" % (working or "none"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
