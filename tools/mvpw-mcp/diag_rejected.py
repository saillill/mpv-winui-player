"""Why was each of the 76 startup commands rejected? Measure, per line.

The rejection log is a list of symptoms, and they are not one cause. Three
candidates, and they need different fixes:

  A. the line has no value            `set sub-files `        -> malformed
  B. the value is quoted with a unit  `set ... "1.000 MiB"`   -> wrong grammar
  C. the option is not settable at runtime, or does not exist under that name
                                                              -> the row should
                                                                 not emit at all

For each rejected line this asks mpv two questions that cannot damage the pipe:
does the option exist (`get_property`), and is the value the app wrote the kind
the option accepts? It never sends the malformed line itself -- that is what
makes mpv drop the connection.

Output is a per-cause table, so the fix can be sized before it is written.
"""
import io
import json
import re
import sys
import time

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

import mpvw_tools as T
import os

# Repo root, derived from this file (it lives at <repo>/tools/...), so renaming or
# moving the checkout does not silently repoint these paths at an old copy.
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

LOG = (r"C:\Users\z2637\AppData\Local\mpv-winui\mpv-winui\logs"
       r"\mpv-winui.2026-10-22.log.txt")
OUT = REPO + r"\_work\mcp\rejected.json"


def rejected_lines():
    seen = []
    for line in io.open(LOG, encoding="utf-8", errors="replace"):
        m = re.search(r"CommandString rejected: (.+?)\s*$", line)
        if not m:
            continue
        cmd = m.group(1)
        if cmd not in seen:
            seen.append(cmd)
    return seen


def split_line(cmd):
    """'no-osd set opt value' -> (opt, value_or_None)."""
    body = cmd
    if body.startswith("no-osd "):
        body = body[len("no-osd "):]
    m = re.match(r"set\s+(\S+)(?:\s+(.*))?$", body, re.S)
    if not m:
        return None, None
    return m.group(1), m.group(2)


def main():
    lines = rejected_lines()
    print("distinct rejected commands:", len(lines))

    T.close_app()
    T.launch_app()
    time.sleep(3.0)

    rows = []
    for i, cmd in enumerate(lines):
        opt, value = split_line(cmd)
        if opt is None:
            rows.append({"cmd": cmd, "cause": "unparseable"})
            continue

        exists = T._ipc(["get_property", opt], 700 + i)
        if exists.get("error") != "success":
            rows.append({"cmd": cmd, "option": opt, "value": value,
                         "cause": "not a runtime property",
                         "error": exists.get("error")})
            continue
        # The option exists. Is the value the problem?
        if value is None or value == "":
            cause = "empty value"
        elif re.match(r'^"[^"]*"$', value or ""):
            cause = "quoted value"
        elif re.match(r"^-?\d+$", value or ""):
            cause = "numeric value, option exists"
        else:
            cause = "plain value, option exists"
        rows.append({"cmd": cmd, "option": opt, "value": value,
                     "cause": cause, "current": exists.get("data")})

    T.close_app()

    by_cause = {}
    for r in rows:
        by_cause.setdefault(r["cause"], []).append(r)
    print()
    for cause, rs in sorted(by_cause.items(), key=lambda kv: -len(kv[1])):
        print("%-36s %d" % (cause, len(rs)))
        for r in rs[:14]:
            print("      %-34s value=%r  current=%r" % (
                r.get("option"), r.get("value"), r.get("current")))
        if len(rs) > 14:
            print("      ... %d more" % (len(rs) - 14))

    with open(OUT, "w", encoding="utf-8") as f:
        json.dump({"rows": rows, "byCause": {k: len(v) for k, v in by_cause.items()}},
                  f, ensure_ascii=False, indent=1)
    print("\nwrote", OUT)


if __name__ == "__main__":
    sys.exit(main())
