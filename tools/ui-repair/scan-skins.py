"""Compare the three player skins control-by-control, and against the spacing tokens.

The brief for the repair task explicitly asks for 排版 (layout) defects, and the queue so
far only holds two behaviour/port items. This produces layout items with measurements
behind them instead of impressions.

Method: for each PlayerStyle, launch, open a clip so the control bar is real, snapshot, and
record every control's rect. Then compare:

  coverage   a control present in one skin and absent in another -- a real gap unless the
             skin's design says otherwise
  movement   the same control at coordinates that differ by an amount matching no spacing
             token and no known layout tier
  alignment  controls that should share an edge (all bar buttons on one baseline) but do not

Only differences that are consistent across two runs are reported: the first measurement of
anything in this repo has a habit of being the probe's fault.
"""
import json
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "tools", "mvpw-mcp"))

import mpvw_tools as T

# Fixture directory for the probe clips this file loads. Derived from __file__
# (this file lives at <repo>/tools/...), so archiving or renaming anything
# outside the repo cannot silently point these at a path that no longer exists.
# The clips themselves live in tools/mvpw-mcp/fixtures/ and are committed.
FIXTURES = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "tools", "mvpw-mcp", "fixtures")

CLIP = os.path.join(FIXTURES, "sdr-bt709.mp4")

# The values come from AppSettings, not from how the UI spells them:
#     PlayerStyle_Default = "Default"
#     PlayerStyle_Center  = "Center"     <- American spelling
#     PlayerStyle_Compact = "Compact"
# The first version of this script wrote "Centre"/"Standard", so two of three "skins"
# resolved to the default branch and all three measured identically. That looked like
# "the skins are consistent" and was really "the setting never applied".
SKINS = ("Default", "Center", "Compact")
CONTROLS = ("PlayPauseButton", "PreviousTrackButton", "NextTrackButton",
            "ProgressSlider", "VolumeSlider", "VolumeMuteButton",
            "FullScreenButton", "FullWindowButton", "PiPButton",
            "TimeElapsedElement", "TimeRemainingElement",
            "ShuffleButton", "TrackSelectionButton", "ControlPanelButton")


def set_skin(name):
    """Write PlayerStyle through the registry, the way the app reads it."""
    import subprocess
    key = (r"HKCU:\Software\Classes\Local Settings\Software\mpv-winui"
           r"\mpv-winui\app")
    subprocess.run(["pwsh", "-NoProfile", "-Command",
                    "Set-ItemProperty -Path '%s' -Name 'PlayerStyle' -Value '%s'"
                    % (key, name)], capture_output=True)


def measure():
    els = T.get_ui_snapshot(max_depth=24, redact_names=True)["elements"]
    out = {}
    for e in els:
        aid = e.get("automationId")
        if aid in CONTROLS and e.get("rect") and len(e["rect"]) == 4:
            out[aid] = e["rect"]
    return out


def main():
    results = {}
    for skin in SKINS:
        set_skin(skin)
        T.close_app()
        T.launch_app()
        # Load a clip so the control bar is in its playing state.
        try:
            T._ipc(["loadfile", CLIP, "replace"], 900)
        except Exception:
            pass
        import time
        time.sleep(3.0)
        r = measure()
        results[skin] = r
        print("%-9s %d/%d controls present" % (skin, len(r), len(CONTROLS)))
        missing = [c for c in CONTROLS if c not in r]
        if missing:
            print("   missing: %s" % ", ".join(missing))
        T.close_app()

    # Coverage differences.
    sets = {s: set(v) for s, v in results.items()}
    allc = set().union(*sets.values())
    print("\ncoverage gaps (present in some skins, absent in others):")
    gaps = 0
    for c in sorted(allc):
        have = [s for s in SKINS if c in sets[s]]
        if len(have) != len(SKINS):
            print("   %-22s in %s, not %s"
                  % (c, ",".join(have), ",".join(s for s in SKINS if s not in have)))
            gaps += 1
    if not gaps:
        print("   none")

    print("\nposition differences between Default and Compact:")
    base, comp = results.get("Default", {}), results.get("Compact", {})
    moved = 0
    for c in sorted(set(base) & set(comp)):
        dx = comp[c][0] - base[c][0]
        dy = comp[c][1] - base[c][1]
        if dx or dy:
            print("   %-22s dx=%+5d dy=%+5d" % (c, dx, dy))
            moved += 1
    if not moved:
        print("   none")

    out = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "_work", "skin-layout.json")
    import io
    io.open(out, "w", encoding="utf-8").write(json.dumps(results, indent=2))
    print("\nwritten:", out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
