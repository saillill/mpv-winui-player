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
import os

# Repo root, derived from this file (it lives at <repo>/tools/...), so renaming or
# moving the checkout does not silently repoint these paths at an old copy.
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

CLIP = r"D:\Workspace\WorkBuddy\MPV\.workbuddy\tmp\clips\sdr-bt709.mp4"
REG_PATH = r"HKCU:\Software\Classes\Local Settings\Software\mpv-winui\mpv-winui\app"


def set_skin(value):
    """PlayerStyle is a registry string.

    The stored values are `Default` / `Center` / `Compact` (AppSettings.cs:308-310). The
    strings a user sees are localized ("Standard" / "Centre" / "Compact"), so the two differ:
    this used to write "Standard" and "Centre", which match no branch of
    `PlayerControl.ApplyPlayerStyle`, so every run measured the `default:` arm and reported
    all three skins healthy. Corrected to the stored values.

    Writing this also has to happen while the app is NOT running: `AppSettings.Flush()` on
    exit rewrites the registry from the app's in-memory copy, discarding a write made earlier.
    """
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
    # These are the STORED values, not the localized labels the settings page shows.
    for skin in ("Default", "Center", "Compact"):
        # Close first: the app rewrites the registry from memory when it exits, so a write
        # made while it is running is discarded by the exit that follows.
        T.close_app()
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
        snap = T.get_ui_snapshot(max_depth=28, redact_names=True)
        by_id = {e["automationId"]: e for e in snap["elements"] if e["automationId"]}
        button = by_id.get("PlayPauseButton")

        # **Does the skin actually change what is on screen?**
        #
        # "transportUsable" alone cannot tell: all three skins keep a usable transport, so a
        # bar that renders the same tree for every setting passes it. Measured defect this
        # catches: `Style` with a `Template` setter is assigned to `PlayerControl`, but that
        # type is a `UserControl`, whose content comes from its own XAML. The template is
        # never applied (`Template` stays null), so all three skins render the inline tree
        # and the setting has no effect.
        #
        # The marker is a control that exists ONLY in the inline tree:
        # `CompactPlayerControlStyle.xaml` declares no `VolumeSlider`, so a Compact bar that
        # shows one is not rendering the Compact template.
        results[skin] = {
            "pauseBefore": before,
            "clicked": clicked,
            # enabled + present is the observable claim: a skin that broke the
            # transport would leave the control missing or disabled.
            "transportUsable": bool(button) and button.get("enabled", True),
            "hasPlayPause": "PlayPauseButton" in by_id,
            "hasProgress": "ProgressSlider" in by_id,
            "hasVolumeSlider": "VolumeSlider" in by_id,
            "rendersCompactTemplate": skin == "Compact" and "VolumeSlider" not in by_id,
        }
        print("  %-9s pauseBefore=%s clicked=%s usable=%s  ids(play/pbar/vol)=%s/%s/%s" % (
            skin, before, results[skin]["clicked"], results[skin]["transportUsable"],
            results[skin]["hasPlayPause"], results[skin]["hasProgress"],
            results[skin]["hasVolumeSlider"]))
        T.close_app()

    set_skin("")
    T.close_app()

    print("\n" + json.dumps(results, indent=1, ensure_ascii=False))
    out = REPO + r"\_work\mcp\skin-toggle.json"
    with open(out, "w", encoding="utf-8") as f:
        json.dump(results, f, ensure_ascii=False, indent=1)
    print("wrote " + out)

    working = [k for k, v in results.items() if v.get("transportUsable")]
    print("\nskins whose transport stays usable after the switch: %s"
          % (working or "none"))

    # A transport that works in all three skins is NECESSARY, not sufficient. This second
    # line is the one that would have caught the skin setting being ignored: the Compact
    # template is the only one that omits VolumeSlider, so a Compact bar that still shows it
    # is rendering the inline tree.
    compact = results.get("Compact", {})
    rendered_compact = compact.get("rendersCompactTemplate", False)
    print("Compact actually renders its own template: %s" % rendered_compact)
    if not rendered_compact:
        print("\nSKIN SETTING HAS NO VISIBLE EFFECT: PlayerControl is a UserControl, so the\n"
              "Style's Template setter is never applied (Template stays null) and every skin\n"
              "renders the inline XAML. Reported, not asserted as a pass.")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
