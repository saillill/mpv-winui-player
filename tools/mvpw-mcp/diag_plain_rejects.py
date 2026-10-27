"""The eight rejections the empty-value fix will not explain.

Classification of the 76 rejected startup commands left two groups after the two
obvious causes (empty value, quoted unit):

  plain        `openal-direct-channels yes`, `openal-num-buffers 4`,
               `input-gamepad no`, `demuxer-rawvideo-size 0`,
               `demuxer-sbr-probesize 128`, `openal-num-samples 8192`
  quoted-plain `cuda-decode-device "auto"`, `player-operation-mode "cplayer"`

These look legal, so either mpv does not accept them as runtime `set` targets
(they are configuration-only, applied at init), or they need a different type.
The distinction matters: a config-only option must never be sent as a command,
and the app already has a `ConfigOnlyKeys` set for exactly that -- so the
question is whether these eight are missing from it.

Test each in isolation, one process per option, so a pipe-dropping name cannot
contaminate the next result. For each: does `set` work, does `set` with the
recorded value work, and does the option read back afterwards.
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

CASES = [
    ("openal-direct-channels", "yes"),
    ("openal-num-buffers", "4"),
    ("openal-num-samples", "8192"),
    ("input-gamepad", "no"),
    ("demuxer-rawvideo-size", "0"),
    ("demuxer-sbr-probesize", "128"),
    ("cuda-decode-device", "auto"),
    ("player-operation-mode", "cplayer"),
]
OUT = REPO + r"\_work\mcp\plain-rejects.json"


def probe(option, value):
    """One fresh process per option."""
    T.close_app()
    T.launch_app()
    time.sleep(2.5)
    result = {"option": option, "value": value}
    try:
        r = T._ipc(["set_property", option, value], 800)
        result["setProperty"] = r.get("error")
        r2 = T._ipc(["get_property", option], 801)
        result["readBack"] = r2.get("data") if r2.get("error") == "success" else r2.get("error")
        # The form the app actually sends: a command LINE, through command_string
        # semantics. `script-message` cannot do that, so compare with `set`.
        r3 = T._ipc(["set", option, value], 802)
        result["setCommand"] = r3.get("error")
    except T.ToolError as e:
        result["exception"] = str(e)
    T.close_app()
    return result


def main():
    rows = [probe(o, v) for o, v in CASES]
    print("%-26s %-10s %-16s %-16s %s" % (
        "option", "value", "set_property", "set_command", "read back"))
    for r in rows:
        print("%-26s %-10s %-16s %-16s %s" % (
            r["option"], r["value"], r.get("setProperty"),
            r.get("setCommand"), r.get("readBack")))

    ok = [r for r in rows if r.get("setProperty") == "success"]
    bad = [r for r in rows if r.get("setProperty") != "success"]
    print("\naccepted as a runtime property: %d" % len(ok))
    print("rejected: %d" % len(bad))
    for r in bad:
        print("   %-26s %s" % (r["option"], r.get("setProperty")))

    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(rows, f, ensure_ascii=False, indent=1)
    print("wrote", OUT)


if __name__ == "__main__":
    sys.exit(main())
