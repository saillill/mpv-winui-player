"""Accessible identity, measured at runtime instead of from XAML.

The XAML-only version of this audit reported "396 interactive elements, 0 with
AutomationProperties.AutomationId" -- and that number is meaningless, because
WinUI derives an element's AutomationId from `x:Name` when none is set. The
running app exposes 56 ids in the library view and 36 while playing, none of
them declared in XAML: `git grep AutomationProperties -- '*.xaml'` finds 39
attributes in the whole tree, mostly `AutomationProperties.Name`.

So the honest measurement is: of the interactive elements actually on screen,
how many carry an id (addressable), and of those, how many carry a NAME
(announceable). An element with an id but an empty Name is still unreadable by a
screen reader; an element with neither cannot be tested or read.

The XAML scan is kept, but only as a weak signal for elements that never render
on the states probed here -- it is labelled as such rather than reported as a
count of defects.
"""
import io
import json
import sys
import time

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

import mpvw_tools as T

CLIPS = [r"D:\Workspace\WorkBuddy\MPV\.workbuddy\tmp\clips\sdr-bt709.mp4"]
OUT = r"C:\Users\z2637\Documents\DeepSeek\mpv\_work\mcp\a11y-runtime.json"

INTERACTIVE = {
    "ButtonControl", "CheckBoxControl", "ComboBoxControl", "EditControl",
    "ListItemControl", "MenuItemControl", "RadioButtonControl",
    "ScrollBarControl", "SliderControl", "TabItemControl", "TreeItemControl",
}


def survey(label):
    s = T.get_ui_snapshot(max_depth=24, redact_names=False)
    interactive = [e for e in s["elements"] if e["type"] in INTERACTIVE]
    with_id = [e for e in interactive if e["automationId"]]
    with_name = [e for e in interactive if e["name"]]
    id_no_name = [e for e in with_id if not e["name"]]
    neither = [e for e in interactive
               if not e["automationId"] and not e["name"]]
    print("  %-14s interactive=%-4d id=%-4d name=%-4d id-but-no-name=%-4d neither=%d" % (
        label, len(interactive), len(with_id), len(with_name),
        len(id_no_name), len(neither)))
    if id_no_name:
        for e in id_no_name[:10]:
            print("      id without a name: %-26s %s" % (e["automationId"], e["type"]))
    return {
        "label": label,
        "interactive": len(interactive),
        "withId": len(with_id),
        "withName": len(with_name),
        "idWithoutName": [{"id": e["automationId"], "type": e["type"]}
                          for e in id_no_name],
        "neither": len(neither),
    }


def main():
    T.close_app()
    T.launch_app()
    time.sleep(3.0)
    results = []

    results.append(survey("home"))
    for key in ("music", "videos", "queue"):
        T.invoke_control("ShellNav_" + key, "select")
        time.sleep(1.6)
        results.append(survey(key))

    T._ipc(["loadfile", CLIPS[0], "replace"], 900)
    time.sleep(4.0)
    results.append(survey("playing"))

    # Player panels: each adds controls, and each is where the unaddressable
    # ones would show up if they existed.
    for aid in ("ControlPanelButton", "TrackSelectionButton", "PlaylistPanelButton"):
        try:
            T.invoke_control(aid, "invoke")
            time.sleep(1.4)
            results.append(survey("+" + aid))
            T._ipc(["keypress", "ESC"], 910)
            time.sleep(0.6)
        except T.ToolError as e:
            print("  %-14s unavailable: %s" % (aid, e))

    T._ipc(["stop"], 920)
    time.sleep(1.2)
    T.close_app()

    total_int = sum(r["interactive"] for r in results)
    total_id = sum(r["withId"] for r in results)
    total_noname = sum(len(r["idWithoutName"]) for r in results)
    print("\nsummary across %d states: interactive instances=%d, with id=%d, "
          "id-without-name=%d" % (len(results), total_int, total_id, total_noname))

    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(results, f, ensure_ascii=False, indent=1)
    print("wrote", OUT, "| process left:", T._pid() is not None)


if __name__ == "__main__":
    sys.exit(main())
