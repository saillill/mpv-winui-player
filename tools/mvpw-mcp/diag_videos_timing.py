"""Which transition leaves only four ids, and does parking change it?

Facts measured so far, which disagree with each other in a way that needs pinning down:

  launch x4, parked       53 ids each, all fine
  launch x4, foreground   53 ids each, all fine
  music -> videos, parked 4 ids
  music -> videos, front  4 ids (once), 57 ids (once)

So launch is fine either way and the collapse is tied to the music -> videos transition,
not to where the window is. Both prior runs that reported 57 were the SECOND time the
transition was made in that process; the 4-id runs were the first. This run tests that
hypothesis directly by making the transition three times in one process and printing the
table, with parking as a flag so the same process shape is used for both.

If first-transition-only is confirmed, the cause is one-time initialisation of the video
library view (an async scan or a first-layout pass), and the honest fix is for probes to
wait for the specific element rather than for a fixed sleep -- the app is not broken; the
probe is early.
"""
import os
import sys
import time

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "tools", "mvpw-mcp"))

import mpvw_tools as T


def ids():
    return [e["automationId"] for e in
            T.get_ui_snapshot(max_depth=12, redact_names=True)["elements"]
            if e.get("automationId")]


def main():
    print("PUSH_BACK =", T._PUSH_BACK)
    T.close_app()
    time.sleep(2.0)
    T.launch_app()

    for i in range(3):
        T.invoke_control("ShellNav_music", "select")
        time.sleep(1.6)
        m = len(ids())

        T.invoke_control("ShellNav_videos", "select")
        # Measure at three points to separate "slow" from "absent".
        time.sleep(0.6)
        t1 = len(ids())
        time.sleep(1.4)
        t2 = len(ids())
        time.sleep(2.0)
        t3 = len(ids())

        add = "VideoAddFolderButton" in ids()
        print("  round %d: music=%-3d videos @0.6s=%-3d @2.0s=%-3d @4.0s=%-3d addFolder=%s"
              % (i + 1, m, t1, t2, t3, add))

    T.close_app()
    return 0


if __name__ == "__main__":
    sys.exit(main())
