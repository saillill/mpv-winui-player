"""What does mpv actually accept for `edition`?

The log now shows BOTH forms rejected:

    x11  CommandString rejected: no-osd set edition "auto"     <- before the fix
    x2   CommandString rejected: no-osd set edition auto       <- after the fix

So quoting was not the whole story. Two possibilities:

  a) `edition` refuses to be set at all on a running player -- it selects which edition of
     a multi-edition title to play and is only meaningful when a file is loading, so `set`
     fails regardless of spelling. In that case the fault is that the app tries, and the fix
     is to stop sending it (the same treatment `player-operation-mode` and
     `demuxer-rawvideo-size` already get);
  b) `auto` is not a settable value for it, and only a number works.

One request per launch, because the pipe serves one conversation per process.
"""
import sys

sys.path.insert(0, r"C:\Users\z2637\Documents\DeepSeek\mpv\tools\mvpw-mcp")

import mpvw_tools as T


def ask(commands, label):
    T.close_app()
    import time
    time.sleep(2.0)
    T.open_launch_window(4.0)
    try:
        r = T._ipc(commands, retry=False)
    except Exception as e:
        r = {"exception": str(e)[:90]}
    T.close_app()
    print("%-34s -> %s" % (label, str(r)[:120]))
    return r


print("== what mpv says about the property itself ==")
ask(["get_property", "edition"], "get_property edition")

print("\n== set, bare token ==")
ask(["set", "edition", "auto"], "set edition auto")

print("\n== set, quoted, as one line (what CommandString does) ==")
ask(["set", "edition", "auto".join(['"', '"'])], 'set edition "auto"')

print("\n== a numeric value ==")
ask(["set", "edition", "1"], "set edition 1")

print("\n== is it readable before anything is playing? ==")
ask(["get_property", "edition"], "get_property edition (idle)")
