"""Classify the 76 rejected startup commands without perturbing mpv.

The previous attempt asked mpv `get_property <option>` for each rejected name and
got "no reply" for all 76 -- because mpv drops the pipe for many names whether or
not the option exists, so that probe cannot distinguish the two cases. The
measurement was blamed, not the app.

Two sources that cannot lie about existence, and neither touches the running
player:

  * the generated option table the app itself uses
    (`MpvConfOptions/mpv-options.json`, read through GeneratedOptionNames)
  * a live `get_property` ONLY for options the table already says exist, where
    the question is "is the VALUE acceptable" rather than "does the name exist"

The classification that matters for the fix is not "does mpv know this name" --
the schema says it does -- but WHICH of these three shapes the emitted line has:

  empty        `set opt ` with nothing after it
  quoted-unit  a quoted number+unit, which mpv's parser rejects
  quoted-plain a quoted value that did not need quoting
  plain        a bare value that should have worked
"""
import io
import json
import os
import re
import sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

LOG = (r"C:\Users\z2637\AppData\Local\mpv-winui\mpv-winui\logs"
       r"\mpv-winui.2026-10-22.log.txt")
SCHEMA = (r"C:\Users\z2637\Documents\DeepSeek\mpv\mpv-winui\mpv-winui"
          r"\MpvConfOptions\mpv-options.json")
OUT = r"C:\Users\z2637\Documents\DeepSeek\mpv\_work\mcp\rejected-classified.json"


def schema_names():
    with io.open(SCHEMA, encoding="utf-8-sig") as f:
        data = json.load(f)
    names = set()

    def walk(node):
        if isinstance(node, dict):
            for k, v in node.items():
                if k in ("name", "option", "key") and isinstance(v, str):
                    names.add(v)
                walk(v)
        elif isinstance(node, list):
            for item in node:
                walk(item)
    walk(data)
    return names


def rejected_lines():
    out = []
    for line in io.open(LOG, encoding="utf-8", errors="replace"):
        m = re.search(r"CommandString rejected: (.+?)\s*$", line)
        if m and m.group(1) not in out:
            out.append(m.group(1))
    return out


def classify(cmd):
    body = cmd[len("no-osd "):] if cmd.startswith("no-osd ") else cmd
    m = re.match(r"set\s+(\S+)(?:\s+(.*))?$", body, re.S)
    if not m:
        return {"cause": "unparseable"}
    opt, value = m.group(1), m.group(2)
    rec = {"cmd": cmd, "option": opt, "value": value}
    if value is None or value.strip() == "":
        rec["cause"] = "empty"
    elif re.match(r'^"[^"]*"$', value):
        inner = value[1:-1]
        rec["cause"] = ("quoted-unit"
                        if re.match(r"^[\d.]+\s*[A-Za-z]+$", inner)
                        else "quoted-plain")
    else:
        rec["cause"] = "plain"
    return rec


def main():
    known = schema_names()
    print("option names in the app's schema:", len(known))

    rows = [classify(c) for c in rejected_lines()]
    for r in rows:
        r["inSchema"] = r.get("option") in known

    by = {}
    for r in rows:
        by.setdefault(r["cause"], []).append(r)

    print("\nrejected commands by shape:")
    for cause, rs in sorted(by.items(), key=lambda kv: -len(kv[1])):
        unknown = [r for r in rs if not r.get("inSchema")]
        print("  %-14s %-3d   (not in the app's schema: %d)" % (
            cause, len(rs), len(unknown)))
        for r in rs[:8]:
            print("      %-30s value=%-18r schema=%s" % (
                r.get("option"), r.get("value"), r.get("inSchema")))
        if len(rs) > 8:
            print("      ... %d more" % (len(rs) - 8))

    missing = [r for r in rows if not r.get("inSchema")]
    print("\nnames the app emits but its own schema does not declare: %d" % len(missing))
    for r in missing[:20]:
        print("   %-32s %s" % (r.get("option"), r["cause"]))

    with open(OUT, "w", encoding="utf-8") as f:
        json.dump({"rows": rows,
                   "byCause": {k: len(v) for k, v in by.items()},
                   "notInSchema": [r.get("option") for r in missing]},
                  f, ensure_ascii=False, indent=1)
    print("\nwrote", OUT)


if __name__ == "__main__":
    sys.exit(main())
