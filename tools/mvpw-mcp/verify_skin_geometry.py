"""Does switching skins actually change the layout?

The previous run showed the transport toggles pause under all three skins,
which rules out "switching to Centre/Compact leaves the buttons dead". It does
not distinguish two explanations:

  A. the ControlTemplate is applied and the code-behind fields still reach the
     live elements (so everything works), or
  B. the Template setter has no effect on a UserControl, the skin setting does
     nothing visually, and the controls that toggle are the original ones.

Explanation B would be its own defect: a settings row that changes nothing.
So measure the geometry of three named controls under each skin. If the
rectangles differ, the template is being applied (A). If they are identical to
the pixel, the setting is inert (B).
"""
import json
import subprocess
import sys
import time

import mpvw_tools as T

CLIP = r"D:\Workspace\WorkBuddy\MPV\.workbuddy\tmp\clips\sdr-bt709.mp4"
REG = r"HKCU:\Software\Classes\Local Settings\Software\mpv-winui\mpv-winui\app"
WATCH = ["PlayPauseButton", "ProgressSlider", "VolumeSlider",
         "SkipBackwardButton", "FullScreenButton", "MoreButton"]

out = {}
T.close_app()

for skin in ("Standard", "Centre", "Compact"):
    subprocess.run(["pwsh", "-NoProfile", "-Command",
                    f'Set-ItemProperty -Path "{REG}" -Name PlayerStyle -Value "{skin}" -Type String'],
                   capture_output=True, text=True)
    T.close_app()
    T.launch_app()
    time.sleep(1.5)
    T._ipc(["loadfile", CLIP, "replace"], 901)
    time.sleep(3.0)

    snap = T.get_ui_snapshot(max_depth=14, redact_names=True)
    geo = {}
    for e in snap["elements"]:
        if e["automationId"] in WATCH:
            geo[e["automationId"]] = e["rect"]
    out[skin] = geo
    print("%-9s %s" % (skin, json.dumps(geo, ensure_ascii=False)))
    T.close_app()

subprocess.run(["pwsh", "-NoProfile", "-Command",
                f'Set-ItemProperty -Path "{REG}" -Name PlayerStyle -Value "" -Type String'],
               capture_output=True, text=True)
T.close_app()

base = out.get("Standard", {})
changed = {}
for skin in ("Centre", "Compact"):
    diffs = [k for k in WATCH
             if k in base and k in out.get(skin, {}) and base[k] != out[skin][k]]
    changed[skin] = diffs
    print("%-9s differs from Standard in: %s" % (skin, diffs or "NOTHING"))

path = r"C:\Users\z2637\Documents\DeepSeek\mpv\_work\mcp\skin-geometry.json"
with open(path, "w", encoding="utf-8") as f:
    json.dump({"geometry": out, "differsFromStandard": changed}, f,
              ensure_ascii=False, indent=1)
print("wrote " + path)

verdict = "A: the template is applied" if any(changed.values()) else "B: the setting is inert"
print("\nverdict -> " + verdict)
sys.exit(0)
