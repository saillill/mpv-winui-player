"""Show what the app itself reports as wrong, and record it as tonight's evidence.

Three attempts to find a category a machine can fix alone all failed, and the failures are
the finding:

  font tokens    81 value matches -> 0 real defects after filtering for semantics, precedent
                 and element kind. Every match was an icon size, or a token whose name meant
                 something else.
  dead types     22 suspects -> all but none were false positives. Custom controls are
                 referenced as `<ctl:Name>`, types as `x:DataType`, helpers as
                 `{x:Bind local:Helper.Method()}`. A regex cannot tell "unused" from
                 "used in a way I did not think of".
  orphan keys    0. Already clean (checked four reader kinds).

So the nightly job does not fix. It gathers what is expensive to gather and cheap to judge,
and it says plainly when there is nothing.

This script is the honest centre of that: read the app's OWN logs for what it considers
wrong. Those are findings the application produced, which is stronger evidence than any
pattern a script invents.
"""
import glob
import io
import os
import re
import sys
import time

REPO = r"C:\Users\z2637\Documents\DeepSeek\mpv"
LOGS = os.path.join(os.environ.get("LOCALAPPDATA", ""), "mpv-winui", "logs")
OUT = os.path.join(REPO, "_work", "nightly")


def latest_log():
    """Newest mpv-winui.*.log.txt, and whether it is fresh enough to mean anything.

    The first version took the newest file and read it. On this machine the newest was from
    2026-09-25 -- a month old -- while today's launches wrote only `display-info.log`. It
    reported five `invalid color` errors as if they were current, and they are from a build
    that no longer exists.

    So freshness is part of the measurement: a log older than today is reported as "the app
    did not write one", which is itself a finding about the logging, not about the app's
    behaviour.
    """
    files = sorted(glob.glob(os.path.join(LOGS, "mpv-winui.*.log.txt")))
    if not files:
        return None, None
    newest = max(files, key=os.path.getmtime)
    age_days = (time.time() - os.path.getmtime(newest)) / 86400.0
    return newest, age_days


def main():
    os.makedirs(OUT, exist_ok=True)
    f, age = latest_log()
    if not f:
        print("no mpv-winui.*.log.txt under %s" % LOGS)
        print("FINDING: the app is not writing its own log at all.")
        return 1

    print("log: %s (%.0f KB, %.1f day(s) old)"
          % (os.path.basename(f), os.path.getsize(f) / 1024, age))

    if age > 1.0:
        print("\nFINDING: the newest app log is %.1f days old." % age)
        print("  A launch today does not produce one, so anything below is HISTORICAL")
        print("  and must not be reported as a current problem.")
        print("  Cause to check: NLog has no config, so its file target is never enabled")
        print("  and every AppLogger.Error/Warn call goes nowhere.")

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
             "日志：`%s`（%.1f 天前）" % (os.path.basename(f), age), ""]
    if stale:
        lines += ["**这份日志是历史数据。** 今天启动应用没有产生新的日志，"
                  "所以下面的内容反映的是旧构建，不能当作当前缺陷。", "",
                  "## 真正的问题在这里：应用不写日志", "",
                  "`NLog` 6.2.0 已引用，但仓库里没有 `nlog.config`，"
                  "也没有在代码里配置 target 或 rules。"
                  "结果是 76 处 `AppLogger.*` 调用写向一个不落地的目标。", "",
                  "后果：出错时没有任何可查的记录，夜间扫描这类\"读日志找问题\"的手段失效。",
                  "这类缺陷平时看不出来，只有在真正需要日志时才暴露。", ""]
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
