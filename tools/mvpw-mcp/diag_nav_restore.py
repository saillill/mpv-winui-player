"""After selecting a nav item once, does the nav pane come back?

The audit succeeds on the first key and fails on every one after it. `back_to_library`
is supposed to return the app to the library view between steps; if it does not, the
pane stays hidden and every later `ShellNav_*` lookup fails -- which the audit reports as
three broken nav items.

This follows the exact sequence the audit uses, printing the id set after every step, so
the transition where the pane disappears is visible instead of inferred.
"""
import ctypes
import ctypes.wintypes as wt
import os
import sys
import time

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "tools", "mvpw-mcp"))

import mpvw_tools as T


def nav_ids():
    ids = T.get_ui_snapshot(max_depth=10, redact_names=True)["elements"]
    return sorted(e["automationId"] for e in ids
                  if (e.get("automationId") or "").startswith("ShellNav"))


def main():
    T.close_app()
    time.sleep(2.0)
    T.launch_app()

    print("after launch        :", nav_ids())
    T.invoke_control("ShellNav_home", "select")
    time.sleep(1.8)
    print("after nav home      :", nav_ids())

    # The audit's own helper, called the way the audit calls it.
    T._ipc(["stop"], 700) if False else None
    try:
        T._ipc(["stop"], 700)
        print("stop sent           : ok")
    except Exception as e:
        print("stop sent           : FAILED (%s)" % str(e)[:60])
    time.sleep(1.2)
    print("after stop          :", nav_ids())

    # And can the pane be restored by the back button, which is what the app exposes?
    ids = {e["automationId"] for e in
           T.get_ui_snapshot(max_depth=10, redact_names=True)["elements"]}
    print("ShellBackButton     :", "ShellBackButton" in ids)
    if "ShellBackButton" in ids:
        try:
            T.invoke_control("ShellBackButton", "invoke")
            time.sleep(2.2)
            print("after back button   :", nav_ids())
        except Exception as e:
            print("back failed         :", str(e)[:70])

    T.close_app()
    return 0


if __name__ == "__main__":
    sys.exit(main())
