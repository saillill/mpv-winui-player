"""Read what the app itself reports, from the directory it actually writes to.

The correction that makes this script useful: the app logs to
`%LOCALAPPDATA%\\mpv-winui\\mpv-winui\\logs`, NOT `%LOCALAPPDATA%\\mpv-winui\\logs`.

`AppData.ResolveLocalData` goes through
`ApplicationData.GetForUnpackaged("mpv-winui", "mpv-winui")`, and that API composes
`<LocalAppData>\\<publisher>\\<product>` for unpackaged apps. The `logs` folder one level up
holds only `display-info.log`, which is written by hand in `MpvPlayerPage_Display.cs` with a
plain `Path.Combine`.

The earlier version of this script looked in the wrong directory, found files from September,
and concluded "the app has not written a log in 29 days" -- a fabricated defect that would
have sent someone to fix working logging. Measured after the correction: today's log is
64 KB with 527 ERROR/WARN lines.

**Second fault, and the one that mattered more.** The script read the WHOLE of the newest
daily file. A daily file accumulates every launch since midnight, so a defect fixed at 03:14
keeps being reported until the day rolls over, and "distinct problems" mixed two different
questions -- "what has this app ever logged today" and "what is wrong with it now".

Measured 2026-10-24 with the tree green: the whole-file scan reported 15 distinct problems;
a clean launch in the same minute produced 3. The twelve that disappeared included
`CommandString rejected: no-osd set edition auto` and a FATAL for the same option -- both
already recorded in QUEUE.md as fixed in cfb43d3 and excluded. This script was the only
thing still claiming them.

So the script now takes a BASELINE point in the file before a session and reports from it:

    python tools/ui-repair/scan-app-log.py --baseline   # prints the current byte offset
    python tools/ui-repair/scan-app-log.py --since N    # reports only what happened since

With neither, it prints the whole-file view and marks it HISTORICAL in both the console and
the report, because a fixed defect still present in today's file is history, not a finding.

Both directories are checked, and the script says which one it used, so a future move is
visible instead of silent.
"""
import glob
import io
import os
import re
import sys
import time

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
LOCAL = os.environ.get("LOCALAPPDATA", "")

# Checked in order; the first that has mpv-winui.*.log.txt wins. The second is the one that
# actually receives NLog output today.
CANDIDATES = [
    os.path.join(LOCAL, "mpv-winui", "mpv-winui", "logs"),
    os.path.join(LOCAL, "mpv-winui", "logs"),
]
OUT = os.path.join(REPO, "_work", "nightly")

BUCKETS = {
    "ERROR": re.compile(r"\|\s*ERROR\s*\|"),
    "WARN": re.compile(r"\|\s*WARN\s*\|"),
    "FATAL": re.compile(r"\|\s*FATAL\s*\|"),
}


def latest_log():
    """(path, age_days, dir_used) for the newest app log, or (None, None, None)."""
    for d in CANDIDATES:
        files = glob.glob(os.path.join(d, "mpv-winui.*.log.txt"))
        if not files:
            continue
        newest = max(files, key=os.path.getmtime)
        age = (time.time() - os.path.getmtime(newest)) / 86400.0
        return newest, age, d
    return None, None, None


def normalise(line):
    line = re.sub(r"\d{4}-\d{2}-\d{2}[\sT][\d:.,]+", "<time>", line)
    line = re.sub(r"\b\d+\.\d+\b", "<n>", line)
    line = re.sub(r"\b\d{2,}\b", "<n>", line)
    return line.strip()[:160]


def collect(text):
    found = {name: [] for name in BUCKETS}
    for line in text.splitlines():
        for name, pat in BUCKETS.items():
            if pat.search(line):
                found[name].append(line.strip())
                break
    distinct = {}
    for name in ("ERROR", "WARN", "FATAL"):
        for h in found[name]:
            key = (name, normalise(h))
            distinct[key] = distinct.get(key, 0) + 1
    return found, distinct


def report_body(distinct, title, note):
    lines = ["# 应用自己报告的异常", "", title, ""]
    lines += note
    lines.append("")
    if distinct:
        lines += ["| 级别 | 次数 | 消息 |", "|---|---:|---|"]
        for (lvl, msg), n in sorted(distinct.items(), key=lambda kv: -kv[1]):
            safe = msg.replace("|", "\\|")
            lines.append("| %s | %d | `%s` |" % (lvl, n, safe[:150]))
    else:
        lines.append("无 ERROR / WARN。应用没有报告任何问题。")
    return lines


def main():
    os.makedirs(OUT, exist_ok=True)

    baseline = None
    for i, a in enumerate(sys.argv):
        if a == "--baseline":
            f, _age, _d = latest_log()
            print(os.path.getsize(f) if f else 0)
            return 0
        if a == "--since" and i + 1 < len(sys.argv):
            baseline = int(sys.argv[i + 1])

    f, age, used_dir = latest_log()
    if not f:
        print("no mpv-winui.*.log.txt in any of:")
        for d in CANDIDATES:
            print("   %s%s" % (d, "" if os.path.isdir(d) else "  (missing)"))
        print("FINDING: the app is not writing its own log at all.")
        return 1

    print("log dir : %s" % used_dir)
    print("log     : %s (%.0f KB, %.1f day(s) old)"
          % (os.path.basename(f), os.path.getsize(f) / 1024, age))

    if age > 1.0:
        print("\nWARNING: the newest app log is %.1f days old." % age)
        print("  A launch today does not produce one, so anything below is HISTORICAL.")

    raw = open(f, "rb").read()
    stale = age > 1.0

    if baseline is None:
        whole = raw.decode("utf-8", "replace")
        found, distinct = collect(whole)
        print("\n== HISTORICAL: the entire daily file ==")
        print("   This is NOT a list of current defects: the file accumulates every launch")
        print("   since midnight, so a defect fixed an hour ago is still here.")
        print("   Use --baseline before a session, then --since <n> after it.")
        for name in ("FATAL", "ERROR", "WARN"):
            print("  %-6s %d line(s)" % (name, len(found[name])))
            for h in found[name][:4]:
                print("        " + h[:130])
        print("\n  distinct (historical): %d" % len(distinct))
        for (lvl, msg), n in sorted(distinct.items(), key=lambda kv: -kv[1])[:12]:
            print("   %-5s x%-3d %s" % (lvl, n, msg[:120]))

        lines = report_body(distinct, "日志：`%s`（历史整份）" % os.path.basename(f), [
            "目录：`%s`（NLog 经 `GetForUnpackaged` 解析；同级的 `mpv-winui\\logs`"
            "只有 `display-info.log`）" % used_dir,
            "",
            "**这是历史数据，不是当前缺陷清单。** 整份日文件累积了当天每次启动，"
            "一小时前已修好的问题仍会留在这里。要判断当前状态，先用 `--baseline` "
            "取偏移，会话结束后用 `--since <n>`。",
        ])
    else:
        baseline = max(0, min(baseline, len(raw)))
        fresh = raw[baseline:].decode("utf-8", "replace")
        found, distinct = collect(fresh)
        print("\n== THIS SESSION: %d byte(s) after offset %d =="
              % (len(raw) - baseline, baseline))
        for name in ("FATAL", "ERROR", "WARN"):
            print("  %-6s %d line(s)" % (name, len(found[name])))
            for h in found[name][:6]:
                print("        " + h[:130])
        print("\n  distinct (this session): %d" % len(distinct))
        for (lvl, msg), n in sorted(distinct.items(), key=lambda kv: -kv[1])[:12]:
            print("   %-5s x%-3d %s" % (lvl, n, msg[:120]))

        lines = report_body(
            distinct, "日志：`%s`（本次会话）" % os.path.basename(f),
            ["目录：`%s`（NLog 经 `GetForUnpackaged` 解析）" % used_dir, "",
             "**统计范围**：偏移 `%d` 之后的 %d 字节，即本次会话写入的部分。"
             % (baseline, len(raw) - baseline)])

    if stale:
        lines.insert(3, "**注意：这份日志本身是历史数据**（%.1f 天前）。" % age)

    lines += ["", "## 为什么这一类可靠",
              "",
              "这是应用自己写的日志，不是我发明的模式匹配。"
              "`WARN` / `ERROR` 是代码里明确判定为异常的分支，"
              "所以每一条都对应一个真实决策点。",
              "",
              "## 但必须限定时间窗口",
              "",
              "实测（2026-10-24，树全绿的同一分钟）：整份日文件报 15 条不同问题，"
              "同一次干净启动只产生 3 条。消失的 12 条里有 `set edition auto` 的 "
              "FATAL 与 WARN —— 那在 `cfb43d3` 已修好，QUEUE.md 也早已排除，"
              "只有这个脚本还在报它。整份扫描把「今天曾经坏过」当成「现在坏着」。", ""]

    p = os.path.join(OUT, "app-log.md")
    io.open(p, "w", encoding="utf-8").write("\n".join(lines))
    print("\nwritten:", p)
    return 0


if __name__ == "__main__":
    sys.exit(main())
