"""What does mpv accept for `edition`, measured against calibrated controls?

**This file's earlier conclusion was wrong, and the probe was the reason.** It reported
"quoting was not the whole story" because it saw both spellings rejected. What it actually
did was send the wrong request shape, and it had no control to reveal that -- so a
`set volume` would have "failed" the same way.

Measured 2026-10-24 with the corrected shape and a pair of controls:

    shape                                               control volume   control bad
    ["command", ["set", ...]]  (the old, wrong shape)   invalid parameter  invalid parameter
    ["set", ...]               (what _ipc expects)      success            error running command

With the correct shape every edition spelling succeeds:
    set edition auto / "auto" / 1 / 0                   -> success
and `edition always` -> error running command, which is the one genuinely unsupported
value in the schema (enum: no/yes/auto).

So the option is NOT "unsettable on a running player", and the app is not wrong to send it.
The IPC channel cannot exercise `mpv_command_string` at all (that is an ABI call, not an IPC
command: every string form returns "invalid parameter", including `set volume 70`), so the
string-parsing path this file was originally about cannot be tested from here. `_work/
probe_abi_calibrate.py` does it properly, in-process, where the control pair does separate
good from bad.

Controls first. If the volume control fails, this script prints that the probe is unusable
and concludes nothing -- the rule that would have caught the original mistake.
"""
import json
import sys

sys.path.insert(0, r"C:\Users\z2637\Documents\DeepSeek\mpv\tools\mvpw-mcp")

import mpvw_tools as T

# (label, command array). The two controls decide whether anything else is meaningful.
CASES = [
    ("CONTROL volume",        ["set", "volume", "55"]),
    ("CONTROL bad option",    ["set", "zzz-not-an-option", "1"]),
    ("edition auto",          ["set", "edition", "auto"]),
    ("edition number 1",      ["set", "edition", "1"]),
    ("edition number 0",      ["set", "edition", "0"]),
    ("edition always (bad)",  ["set", "edition", "always"]),
    ("no-osd edition auto",   ["no-osd", "set", "edition", "auto"]),
]

if __name__ == "__main__":
    T.close_app()
    T.open_launch_window()

    rid = 0
    results = {}
    for label, cmd in CASES:
        rid += 1
        r = T._ipc(cmd, rid, retry=False)
        results[label] = r.get("error") or r.get("noReply")
        print("  %-24s %-34s -> %s" % (label, json.dumps(cmd), results[label]))

    # Calibration: both controls must give the verdict they are supposed to give.
    good = results.get("CONTROL volume") == "success"
    bad = results.get("CONTROL bad option") == "error running command"

    rid += 1
    vol = T._ipc(["get_property", "volume"], rid, retry=False).get("data")
    rid += 1
    ed = T._ipc(["get_property", "edition"], rid, retry=False).get("data")

    try:
        T._close_pipe()
    except Exception:
        pass
    T.close_app()

    print("\n  volume read back: %s   edition read back: %s" % (vol, ed))
    if not (good and bad and vol == 55.0):
        print("\nPROBE UNUSABLE: the controls did not separate good from bad")
        print("  (volume success=%s, bad-option rejected=%s, volume==55 %s)"
              % (good, bad, vol == 55.0))
        print("Concluding nothing about `edition`.")
        sys.exit(3)

    print("\nCalibrated. Verdict:")
    for label, err in results.items():
        if label.startswith("CONTROL"):
            continue
        print("  %-24s %s" % (label, err))
    sys.exit(0)
