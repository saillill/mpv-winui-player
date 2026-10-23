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

Both directories are checked, and the script says which one it used, so a future move is
visible instead of silent.
"""
import glob
import io
import os
import re
import sys
import time

REPO = r"C:\Users\z2637\Documents\DeepSeek\mpv"
LOCAL = os.environ.get("LOCALAPPDATA", "")

# Checked in order; the first that has mpv-winui.*.log.txt wins. The second is the one that
# actually receives NLog output today.
CANDIDATES = [
    os.path.join(LOCAL, "mpv-winui", "mpv-winui", "logs"),
    os.path.join(LOCAL, "mpv-winui", "logs"),
]
OUT = os.path.join(REPO, "_work", "nightly")


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


def main():
    os.makedirs(OUT, exist_ok=True)
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

    text = io.open(f, encoding="utf-8", errors="replace").read()
    stale = age > 1.0

    buckets = {
        "ERROR": re.compile(r"\|\s*ERROR\s*\|"),
        "WARN": re.compile(r"\|\s*WARN\s*\|"),
        "FATAL": re.compile(r"\|\s*FATAL\s*\|"),
    }
    found = {}
    for name, pat in buckets.items():
        hits = [l.strip() for l in text.splitlines() if pat.search(l)]
        found[name] = hits

    for name in ("FATAL", "ERROR", "WARN"):
        hits = found[name]
        print("  %-6s %d line(s)" % (name, len(hits)))
        for h in hits[:6]:
            print("        " + h[:130])

    # Distinct messages, which is what a morning reader needs: 200 repeats of one warning
    # is one problem, not two hundred.
    def normalise(line):
        line = re.sub(r"\d{4}-\d{2}-\d{2}[\sT][\d:.,]+", "<time>", line)
        line = re.sub(r"\b\d+\.\d+\b", "<n>", line)
        line = re.sub(r"\b\d{2,}\b", "<n>", line)
        return line[:160]

    distinct = {}
    for name in ("ERROR", "WARN"):
        for h in found[name]:
            distinct.setdefault((name, normalise(h)), 0)
            distinct[(name, normalise(h))] += 1

    print("\ndistinct problems: %d" % len(distinct))
    for (lvl, msg), n in sorted(distinct.items(), key=lambda kv: -kv[1])[:12]:
        print("   %-5s x%-3d %s" % (lvl, n, msg))

    lines = ["# 应用自己报告的异常", "",
             "日志：`%s`" % os.path.basename(f),
             "",
             "目录：`%s`（NLog 经 `GetForUnpackaged` 解析；同级的 `mpv-winui\\logs`"
             "只有 `display-info.log`）" % used_dir,
             ""]
    if stale:
        lines += ["**这份日志是历史数据**（%.1f 天前）。" % age, ""]
    if distinct:
        lines += ["| 级别 | 次数 | 消息 |", "|---|---:|---|"]
        for (lvl, msg), n in sorted(distinct.items(), key=lambda kv: -kv[1]):
            safe = msg.replace("|", "\\|")
            lines.append("| %s | %d | `%s` |" % (lvl, n, safe[:150]))
    else:
        lines.append("无 ERROR / WARN。应用没有报告任何问题。")
    lines += ["", "## 为什么这一类可靠",
              "",
              "这是应用自己写的日志，不是我发明的模式匹配。"
              "`WARN` / `ERROR` 是代码里明确判定为异常的分支，"
              "所以每一条都对应一个真实决策点。", ""]
    p = os.path.join(OUT, "app-log.md")
    io.open(p, "w", encoding="utf-8").write("\n".join(lines))
    print("\nwritten:", p)
    return 0


if __name__ == "__main__":
    sys.exit(main())
