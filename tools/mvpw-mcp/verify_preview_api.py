"""Verify osc-preview-api. Second version.

The first version failed on two things that were both test bugs, not
implementation bugs, and the difference matters:

  * `script-list` does not exist on the bundled mpv, so "not loaded" was an
    absent property, not an absent script. A property that is unavailable
    proves nothing either way. Replaced with a positive signal: the script
    registers `script-message preview-request`, so sending that message and
    seeing the mirror appear proves the script is running.
  * close_app returned before the process was gone under psutil's cache; the
    check now re-scans instead of trusting one pass.

The three things worth proving:

  1. the bridge is live (it answers a script-message it registered),
  2. a draw-preview map carrying hover-sec is mirrored to the app,
  3. clearing propagates.
"""
import json
import os
import sys
import time

import psutil

import mpvw_tools as T

CLIP = r"D:\Workspace\WorkBuddy\MPV\.workbuddy\tmp\clips\sdr-bt709.mp4"


def mpv_alive() -> bool:
    for _ in range(3):
        if not any((p.info["name"] or "").lower() == "mpvw.exe"
                   for p in psutil.process_iter(["name"])):
            return False
        time.sleep(0.5)
    return True


def get_prop(name, rid):
    return T._ipc(["get_property", name], rid)


def main():
    failures = []

    def check(label, ok, detail=""):
        print(("  PASS  " if ok else "  FAIL  ") + label +
              ("" if ok else "  " + str(detail)[:400]))
        if not ok:
            failures.append(label)

    T.close_app()
    T.launch_app()
    time.sleep(2.0)
    T._ipc(["loadfile", CLIP, "replace"], 960)
    time.sleep(3.0)
    check("clip loaded", T.read_playback_state().get("filename") is not None)

    # ---- 1. the bridge is running: it registered script-message preview-request
    payload = json.dumps({"x": 10, "y": 20, "w": 192, "h": 108, "hover-sec": 7.25})
    # script-message-to <script> <message> <args...>
    T._ipc(["script-message-to", "preview_api_bridge", "preview-request", payload], 961)
    time.sleep(0.8)
    r = get_prop("user-data/mpvw/preview-request", 962)
    mirrored = r.get("data") if r.get("error") == "success" else None
    print("  mirrored after script-message:", json.dumps(mirrored)[:200] if mirrored else r)
    check("bridge answers script-message (script is live)", mirrored is not None, r)

    if isinstance(mirrored, dict):
        check("script-message payload carries hover-sec",
              mirrored.get("hover-sec") == 7.25, mirrored)

    # ---- 2. a draw-preview map written the way osc.lua writes it
    node = {"x": 100, "y": 200, "w": 192, "h": 108, "hover-sec": 12.5}
    r = T._ipc(["set_property", "user-data/osc/draw-preview", node], 963)
    check("draw-preview accepted", r.get("error") == "success", r)
    # The property observer runs on mpv's event loop, not on the write. A fixed
    # 0.8 s delay was not enough here (measured: the mirror appeared, just
    # later than that), so poll for the expected value instead of guessing.
    data = None
    for _ in range(24):
        time.sleep(0.25)
        r = get_prop("user-data/mpvw/preview-request", 964)
        data = r.get("data") if r.get("error") == "success" else None
        if isinstance(data, dict) and data.get("hover-sec") == 12.5:
            break
    data = r.get("data") if r.get("error") == "success" else None
    print("  mirrored after draw-preview:", json.dumps(data)[:200] if data else r)
    check("draw-preview mirrored to the app", data is not None, r)
    if isinstance(data, dict):
        check("mirror carries the rectangle",
              (data.get("w"), data.get("h"), data.get("x")) == (192, 108, 100), data)
        check("mirror carries hover-sec from inside the map",
              data.get("hover-sec") == 12.5, data)

    # ---- 3. clearing
    T._ipc(["set_property", "user-data/osc/draw-preview", None], 965)
    time.sleep(0.6)
    r = get_prop("user-data/mpvw/preview-request", 966)
    check("clear propagates",
          r.get("error") != "success" or r.get("data") is None, r)

    T.close_app()
    check("no mpvw process left", not mpv_alive())

    print("\n%s (%d failure(s))" % ("OK" if not failures else "FAILED", len(failures)))
    return 0 if not failures else 1


if __name__ == "__main__":
    sys.exit(main())
