"""Does a launch cycle actually leave the owner's focus alone?

The complaint this answers: during an audit the player window kept coming to the front
over the work in progress. Three earlier attempts failed for measured reasons (recorded
in mpvw_tools.py); this checks the current one from the outside, three cycles in a row,
by watching GetForegroundWindow before and after each launch.
"""
import ctypes
import ctypes.wintypes as wt
import os
import sys
import time

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "tools", "mvpw-mcp"))

import mpvw_tools as T

u32 = ctypes.WinDLL("user32", use_last_error=True)
u32.GetForegroundWindow.restype = wt.HWND

results = []
for i in range(3):
    before = u32.GetForegroundWindow()
    T.close_app()
    time.sleep(2.0)
    T.launch_app(snapshot=True)
    after = u32.GetForegroundWindow()
    kept = (before == after)
    results.append(kept)
    print("cycle %d: before=%s after=%s -> %s"
          % (i + 1, before, after, "focus kept" if kept else "FOCUS TAKEN"))
    T.close_app()
    time.sleep(1.0)

print("\n%d/3 cycles left the foreground window alone" % sum(results))
sys.exit(0 if all(results) else 1)
