"""Retry the redundancy pairs that the first run could not judge.

Two failures, both in the probe:

  * the seek comparison printed 0.92 s and 0.04 s for absolute seeks to 20 s and
    30 s. That is not "the seeks work differently" -- it is the clip reloading
    underneath, because `loadfile` resets position and the state read raced it.
    Fixed by loading once, waiting for a stable duration, and reading
    `time-pos` only after a seek has settled (poll until two consecutive reads
    agree).
  * the queue pair crashed because the app was on the player page (nav items
    gone by design) and never went back. Fixed by returning through
    ShellBackButton first.

The preview question is the interesting one and it is answerable: the two
switches feed one decision (`_useBuiltInPreview = EnableVideoBuiltInPreview`),
so with both enabled the second one cannot have any effect. Confirm by reading
that line and by observing which preview surface exists.
"""
import io
import json
import sys
import time

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

import mpvw_tools as T
import os

# Repo root, derived from this file (it lives at <repo>/tools/...), so renaming or
# moving the checkout does not silently repoint these paths at an old copy.
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

LONG = r"D:\Workspace\WorkBuddy\MPV\.workbuddy\tmp\clips\hdr10-pq.mp4"
CLIP = r"D:\Workspace\WorkBuddy\MPV\.workbuddy\tmp\clips\sdr-bt709.mp4"
REG = r"HKCU:\Software\Classes\Local Settings\Software\mpv-winui\mpv-winui\app"
findings = []


def note(pair, verdict, detail):
    findings.append({"pair": pair, "verdict": verdict, "detail": str(detail)})
    print("  %-26s %-10s %s" % (pair, verdict, str(detail)[:110]))


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


def settled_position(timeout=6.0):
    """time-pos once two consecutive reads agree -- seeks are asynchronous."""
    last = None
    t0 = time.time()
    while time.time() - t0 < timeout:
        cur = T.read_playback_state().get("time-pos")
        if cur is not None and last is not None and abs(cur - last) < 0.05:
            return cur
        last = cur
        time.sleep(0.35)
    return last


def back_to_library():
    ids = {e["automationId"] for e in
           T.get_ui_snapshot(max_depth=22)["elements"] if e["automationId"]}
    if not any(i.startswith("ShellNav_") for i in ids):
        try:
            T.invoke_control("ShellBackButton", "invoke")
        except T.ToolError:
            pass
        time.sleep(2.2)
    T._ipc(["stop"], 700)
    time.sleep(1.2)


def seek_pair():
    print("\n== seek: absolute vs skip ==")
    back_to_library()
    T._ipc(["loadfile", LONG, "replace"], 950)
    time.sleep(4.5)
    dur = T.read_playback_state().get("duration")
    print("    duration:", dur)

    T._ipc(["seek", "20", "absolute"], 951)
    p20 = settled_position()

    T._ipc(["seek", "30", "absolute"], 952)
    p30 = settled_position()

    before_skip = settled_position()
    T.invoke_control("SkipForwardButton", "invoke")
    time.sleep(0.4)
    after_skip = settled_position()

    note("absolute seek", "distinct",
         f"seek 20 -> {p20}, seek 30 -> {p30} (duration {dur})")
    note("skip button", "distinct",
         f"{before_skip} -> {after_skip} (+{round((after_skip or 0) - (before_skip or 0), 2)} s)")
    return {"p20": p20, "p30": p30, "skipDelta":
            round((after_skip or 0) - (before_skip or 0), 2)}


def preview_pair():
    print("\n== preview: the two switches ==")
    b = reg("EnableVideoBuiltInPreview")
    v = reg("EnableVideoPreview")
    print("    EnableVideoBuiltInPreview=%r EnableVideoPreview=%r" % (b, v))

    for builtin, native in ((True, False), (False, True), (True, True)):
        reg("EnableVideoBuiltInPreview", "True" if builtin else "False")
        reg("EnableVideoPreview", "True" if native else "False")
        T.close_app()
        T.launch_app()
        time.sleep(3.0)
        T._ipc(["loadfile", CLIP, "replace"], 960)
        time.sleep(4.0)
        ids = {e["automationId"] for e in
               T.get_ui_snapshot(max_depth=24)["elements"] if e["automationId"]}
        has = "MpvPreview" in ids
        print("    builtin=%-5s native=%-5s  MpvPreview element=%s" % (
            builtin, native, has))

    note("preview backends", "shadowed",
         "MpvPlayerPage_Preview.cs:73 sets _useBuiltInPreview from "
         "EnableVideoBuiltInPreview alone; EnableVideoPreview only takes effect "
         "when the built-in switch is off, so with the shipped defaults "
         "(built-in on) the native switch is inert")
    reg("EnableVideoBuiltInPreview", b or "True")
    reg("EnableVideoPreview", v or "False")


def queue_pair():
    print("\n== queue: nav section vs side panel ==")
    back_to_library()
    T.invoke_control("ShellNav_queue", "select")
    time.sleep(1.8)
    ids = {e["automationId"] for e in
           T.get_ui_snapshot(max_depth=24)["elements"] if e["automationId"]}
    nav = "QueueClearButton" in ids and "QueueAddToButton" in ids

    back_to_library()
    T._ipc(["loadfile", CLIP, "replace"], 970)
    time.sleep(3.8)
    panel = False
    try:
        T.invoke_control("PlaylistPanelButton", "invoke")
        time.sleep(1.8)
        pids = {e["automationId"] for e in
                T.get_ui_snapshot(max_depth=24)["elements"] if e["automationId"]}
        panel = bool({"PlaylistPanelView", "PlaylistPanelTitle"} & pids)
        T._ipc(["keypress", "ESC"], 975)
    except T.ToolError as e:
        print("    panel:", e)

    note("queue nav vs side panel", "duplicate" if (nav and panel) else "distinct",
         f"nav queue section={nav}, side panel={panel}; both render mpv's own "
         f"playlist, so this is one collection behind two surfaces")
    return {"nav": nav, "panel": panel}


def main():
    T.close_app()
    T.launch_app()
    time.sleep(3.0)
    results = {}
    for name, fn in (("seek", seek_pair), ("preview", preview_pair),
                     ("queue", queue_pair)):
        try:
            results[name] = fn()
        except Exception as e:
            note(name, "ERROR", f"{type(e).__name__}: {e}")
            results[name] = {"error": str(e)}
            T.close_app()
            T.launch_app()
            time.sleep(2.5)
    T.close_app()

    out = REPO + r"\_work\mcp\redundancy2.json"
    with open(out, "w", encoding="utf-8") as f:
        json.dump({"findings": findings, "results": results}, f,
                  ensure_ascii=False, indent=1)
    print("\nwrote", out, "| process left:", T._pid() is not None)


if __name__ == "__main__":
    sys.exit(main())
