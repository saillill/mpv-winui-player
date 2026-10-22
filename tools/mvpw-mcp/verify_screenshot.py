"""Cover the one tool the e2e run never exercised: trigger_screenshot.

It asks mpv itself to write a screenshot (screenshot-to-file). Nothing loaded
means mpv has nothing to write, so the check loads a clip first, triggers, and
verifies a non-empty file appeared at the path mpv reported.
"""
import os
import sys
import time

import mpvw_tools as T

CLIP = r"D:\Workspace\WorkBuddy\MPV\.workbuddy\tmp\clips\sdr-bt709.mp4"
OUT = r"C:\Users\z2637\Documents\DeepSeek\mpv\_work\mcp\mpv-shot.png"

T.close_app()
T.launch_app()
time.sleep(1.5)
T._ipc(["loadfile", CLIP, "replace"], 902)
time.sleep(3.0)

st = T.read_playback_state()
print("loaded: filename=%r idle=%r" % (st.get("filename"), st.get("idle-active")))

if os.path.exists(OUT):
    os.remove(OUT)

r = T.trigger_screenshot("png", OUT)
print("reply:", r["reply"])
print("file exists:", os.path.exists(OUT),
      "size:", os.path.getsize(OUT) if os.path.exists(OUT) else 0)

ok = os.path.exists(OUT) and os.path.getsize(OUT) > 1000
T.close_app()
print("PASS" if ok else "FAIL")
sys.exit(0 if ok else 1)
