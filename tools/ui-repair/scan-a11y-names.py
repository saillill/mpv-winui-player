"""Does any interactive control lack an accessible name, in every state the app has?

This is the one defect class where an automatic fix is genuinely safe, so it decides what
the nightly job may repair without asking:

  * the failure is objective: a named control with no AutomationProperties.Name and no
    visible TextBlock is unusable by a screen reader -- not a matter of taste;
  * the fix is local: add one attached property at the control's construction site;
  * it cannot change layout: attached properties do not affect measure or arrange.

Everything else -- spacing, colour, animation, whether a control belongs on a page -- needs
a judgement that the measurements cannot supply, so those stay in the candidate list for a
human.

States are walked because this app switches content with Visibility: a control unnamed on
the library page may be fine on the player page, and a snapshot of one state would report
the other's absence as a defect.
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "tools", "mvpw-mcp"))

import mpvw_tools as T

# Fixture directory for the probe clips this file loads. Derived from __file__
# (this file lives at <repo>/tools/...), so archiving or renaming anything
# outside the repo cannot silently point these at a path that no longer exists.
# The clips themselves live in tools/mvpw-mcp/fixtures/ and are committed.
FIXTURES = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "tools", "mvpw-mcp", "fixtures")

CLIP = os.path.join(FIXTURES, "sdr-bt709.mp4")

# The states the design actually has. Each is reached by a nav item or a control, so the
# walk is repeatable rather than "click around and see".
STATES = [
    ("home", [("ShellNav_home", "select")]),
    ("music", [("ShellNav_music", "select")]),
    ("videos", [("ShellNav_videos", "select")]),
    ("queue", [("ShellNav_queue", "select")]),
]

INTERACTIVE = {"ButtonControl", "CheckBoxControl", "RadioButtonControl",
               "ComboBoxControl", "SliderControl", "EditControl",
               "MenuItemControl", "TabItemControl", "HyperlinkControl",
               "ListItemControl", "ToggleButtonControl", "SplitButtonControl"}


def unnamed():
    els = T.get_ui_snapshot(max_depth=24, redact_names=False)["elements"]
    out = []
    for e in els:
        if (e.get("type") or "") not in INTERACTIVE:
            continue
        name = (e.get("name") or "").strip()
        aid = e.get("automationId") or ""
        # No id and no name means nothing can address it either; report separately.
        if not name:
            out.append({"type": e.get("type"), "id": aid,
                        "rect": e.get("rect"), "enabled": e.get("enabled")})
    return out


def main():
    found = {}
    T.close_app()
    T.launch_app()

    for label, actions in STATES:
        for aid, act in actions:
            try:
                T.invoke_control(aid, act)
            except Exception as e:
                print("%-8s could not reach: %s" % (label, str(e)[:60]))
                break
        time.sleep(1.8)
        bad = unnamed()
        found[label] = bad
        print("%-8s %.0f elements, %d unnamed interactive"
              % (label, len(T.get_ui_snapshot(max_depth=24, redact_names=True)["elements"]),
                 len(bad)))
        for b in bad[:6]:
            print("     %-22s id=%-24s enabled=%s" % (b["type"], b["id"] or "(none)",
                                                      b["enabled"]))

    # Player state: load a clip and look again.
    try:
        T.close_app()
        T.launch_app()
        T._ipc(["loadfile", CLIP, "replace"], 900)
        time.sleep(3.0)
        bad = unnamed()
        found["playing"] = bad
        print("%-8s %d unnamed interactive" % ("playing", len(bad)))
        for b in bad[:6]:
            print("     %-22s id=%-24s" % (b["type"], b["id"] or "(none)"))
    except Exception as e:
        print("playing state skipped:", str(e)[:70])

    T.close_app()

    total = sum(len(v) for v in found.values())
    print("\ntotal unnamed interactive across states: %d" % total)
    out = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "_work", "a11y-names.json")
    import io
    io.open(out, "w", encoding="utf-8").write(
        json.dumps(found, indent=2, ensure_ascii=False))
    print("written:", out)
    return 0 if total == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
