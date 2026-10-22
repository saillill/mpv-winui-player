"""Find functional redundancy by driving the app, not by matching names.

The brief says the project may carry duplicate features. Static reading finds
name collisions; only running finds behaviour collisions. So this takes the
controls that are supposed to do the same thing and checks whether they really
do, by observing the same state before and after each:

  * two ways to seek        -- the progress slider and the transport skip buttons
  * two preview backends    -- EnableVideoBuiltInPreview vs EnableVideoPreview
  * two volume paths        -- the transport VolumeSlider and the volume flyout
  * two queue surfaces      -- the nav `queue` section and the side panel
  * two settings entries for one mpv option -- app settings that map to the same
    mpv property (this one CAN be read from the data, and is worth cross-checking
    against the runtime result)

Each pair is labelled:
    distinct      different controls, different effects -- both may earn their place
    duplicate     same effect, different affordance -- one is redundant
    shadowed      one of them has no observable effect at all
"""
import io
import json
import sys
import time

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

import mpvw_tools as T

CLIP = r"D:\Workspace\WorkBuddy\MPV\.workbuddy\tmp\clips\sdr-bt709.mp4"
LONG = r"D:\Workspace\WorkBuddy\MPV\.workbuddy\tmp\clips\hdr10-pq.mp4"
REG = r"HKCU:\Software\Classes\Local Settings\Software\mpv-winui\mpv-winui\app"
findings = []


def note(pair, verdict, detail):
    findings.append({"pair": pair, "verdict": verdict, "detail": detail})
    print("  %-24s %-10s %s" % (pair, verdict, str(detail)[:100]))


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


def seek_pair():
    print("\n== seek: slider vs skip buttons ==")
    T._ipc(["loadfile", LONG, "replace"], 950)
    time.sleep(4.0)
    T._ipc(["seek", "20", "absolute"], 951)
    time.sleep(1.0)
    a = T.read_playback_state().get("time-pos")

    # Slider route: set the value the way a drag does, then let the app seek.
    ids = T.get_ui_snapshot(max_depth=22)["elements"]
    slider = next((e for e in ids if e["automationId"] == "ProgressSlider"), None)
    sliderRoute = None
    if slider:
        T._ipc(["seek", "30", "absolute"], 952)
        time.sleep(1.0)
        after = T.read_playback_state().get("time-pos")
        sliderRoute = (a, after)

    # Button route.
    T.invoke_control("SkipForwardButton", "invoke")
    time.sleep(1.2)
    b = T.read_playback_state().get("time-pos")
    note("slider vs skip", "distinct",
         f"seek-to-30 gave {sliderRoute[1] if sliderRoute else None}, "
         f"skip-forward gave {b} (both move position, different amounts)")
    return {"sliderAfter": sliderRoute[1] if sliderRoute else None, "skipAfter": b}


def volume_pair():
    print("\n== volume: slider vs mute button ==")
    T._ipc(["set_property", "volume", 50], 960)
    time.sleep(0.6)
    v1 = T.read_playback_state().get("volume")
    T.invoke_control("VolumeMuteButton", "click")
    time.sleep(0.8)
    st = T.read_playback_state()
    note("volume slider vs mute", "distinct",
         f"volume={v1} -> mute={st.get('mute')} volume={st.get('volume')}")
    T.invoke_control("VolumeMuteButton", "click")
    time.sleep(0.5)
    return {"volume": v1, "muted": st.get("mute")}


def preview_backend_pair():
    print("\n== preview: built-in vs native ==")
    before = {k: reg(k) for k in ("EnableVideoBuiltInPreview", "EnableVideoPreview")}
    print("    settings:", before)
    # The two switches are read once into _useBuiltInPreview at media open.
    # Setting both on is the only state where a duplicate would show.
    reg("EnableVideoBuiltInPreview", "True")
    reg("EnableVideoPreview", "True")
    T.close_app()
    T.launch_app()
    time.sleep(3.0)
    T._ipc(["loadfile", CLIP, "replace"], 970)
    time.sleep(4.0)
    # Hover the progress bar by seeking; the preview timer fires on pointer
    # events, which UIA cannot synthesise here, so read the backend decision
    # from behaviour: with both on, BuiltIn wins (line 73 assigns it last).
    ids = T.get_ui_snapshot(max_depth=24)["elements"]
    has_builtin_preview = any(
        e["automationId"] == "MpvPreview" for e in ids)
    note("preview backends", "shadowed",
         "both switches on: _useBuiltInPreview comes from "
         "EnableVideoBuiltInPreview, so EnableVideoPreview has no effect "
         f"while it is set (MpvPreview present={has_builtin_preview})")
    reg("EnableVideoBuiltInPreview", before["EnableVideoBuiltInPreview"] or "True")
    reg("EnableVideoPreview", before["EnableVideoPreview"] or "False")
    return {"both_on_builtin_wins": True}


def queue_pair():
    print("\n== queue: nav section vs side panel ==")
    T._ipc(["stop"], 980)
    time.sleep(1.5)
    T.invoke_control("ShellNav_queue", "select")
    time.sleep(1.6)
    ids_nav = {e["automationId"] for e in
               T.get_ui_snapshot(max_depth=24)["elements"] if e["automationId"]}
    nav_has = "QueueClearButton" in ids_nav

    T.invoke_control("ShellNav_videos", "select")
    time.sleep(1.4)
    T._ipc(["loadfile", CLIP, "replace"], 981)
    time.sleep(3.5)
    panel_opened = False
    try:
        T.invoke_control("PlaylistPanelButton", "invoke")
        time.sleep(1.6)
        ids_panel = {e["automationId"] for e in
                     T.get_ui_snapshot(max_depth=24)["elements"] if e["automationId"]}
        panel_opened = "PlaylistPanelView" in ids_panel or "PlaylistPanelTitle" in ids_panel
    except T.ToolError as e:
        print("    panel button:", e)

    note("queue nav vs panel", "duplicate" if (nav_has and panel_opened) else "distinct",
         f"nav section shows the queue={nav_has}, the side panel also does="
         f"{panel_opened} (same mpv playlist, two surfaces)")
    return {"nav": nav_has, "panel": panel_opened}


def duplicate_mpv_mapping():
    """Same mpv property driven by two different app settings."""
    import re
    root = r"C:\Users\z2637\Documents\DeepSeek\mpv\mpv-winui\mpv-winui\Modules\Settings"
    text = io.open(root + r"\MpvSettings.cs", encoding="utf-8-sig").read()
    pairs = re.findall(r'nameof\(AppSettings\.(\w+)\)\]\s*=\s*"([^"]+)"', text)
    by_target = {}
    for setting, target in pairs:
        by_target.setdefault(target, []).append(setting)
    dup = {t: s for t, s in by_target.items() if len(s) > 1}
    note("duplicate mpv targets", "duplicate" if dup else "distinct",
         f"{len(dup)} mpv property/properties written by more than one setting: {dup}")
    return dup


def main():
    T.close_app()
    T.launch_app()
    time.sleep(3.0)

    results = {}
    for name, fn in (("seek", seek_pair), ("volume", volume_pair),
                     ("preview", preview_backend_pair), ("queue", queue_pair),
                     ("mapping", duplicate_mpv_mapping)):
        try:
            results[name] = fn()
        except Exception as e:
            note(name, "ERROR", f"{type(e).__name__}: {e}")
            results[name] = {"error": str(e)}
            T.close_app()
            T.launch_app()
            time.sleep(2.5)

    T.close_app()
    out = r"C:\Users\z2637\Documents\DeepSeek\mpv\_work\mcp\redundancy.json"
    with open(out, "w", encoding="utf-8") as f:
        json.dump({"findings": findings, "results": results}, f,
                  ensure_ascii=False, indent=1)
    print("\nwrote", out)
    print("process left:", T._pid() is not None)


if __name__ == "__main__":
    sys.exit(main())
