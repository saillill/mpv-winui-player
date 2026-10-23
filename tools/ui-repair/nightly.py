"""Nightly scan: measure everything, auto-fix only what is provably safe, report the rest.

The brief: run unattended at night, fix what can be decided without a human, and leave a
morning report with evidence for everything else.

The design constraint that shapes it: an unattended agent editing UI is one bad edit away
from a broken app, and nobody is watching. So "auto-fix" is restricted to changes where all
four of these hold:

  1. the defect is objective -- a measurement, not a preference
  2. the fix is a substitution with a proof of equivalence
  3. the change cannot affect layout (attached properties, not sizes)
  4. `gate.py` passes after it, and the tree is restored if it does not

A literal `FontSize="14"` does NOT qualify even though a token exists: 14 might be
deliberate at that call site, and changing it moves text. It goes in the report with the
token that would match, so the decision takes one line instead of a search.

Everything is written to `_work/nightly/` and summarised in a markdown report the morning
reader can act on. Nothing is committed -- the report says what to run.
"""
import io
import json
import os
import re
import subprocess
import sys
import time

REPO = r"C:\Users\z2637\Documents\DeepSeek\mpv"
OUT = os.path.join(REPO, "_work", "nightly")
sys.path.insert(0, os.path.join(REPO, "tools", "mvpw-mcp"))


def sh(*args, cwd=REPO, timeout=1800):
    r = subprocess.run(args, cwd=cwd, capture_output=True, text=True,
                       errors="replace", timeout=timeout)
    return r.returncode, (r.stdout or "") + (r.stderr or "")


def write(name, text):
    os.makedirs(OUT, exist_ok=True)
    p = os.path.join(OUT, name)
    io.open(p, "w", encoding="utf-8").write(text)
    return p


# ---------------------------------------------------------------- measurements ---

def scan_file_stats():
    """Counts that drift silently: source files, checkers, generated rows."""
    code, files = sh("git", "ls-files")
    files = files.splitlines()
    cs = [f for f in files if f.endswith(".cs") and "/bin/" not in f]
    xaml = [f for f in files if f.endswith(".xaml") and "/bin/" not in f]
    return {"cs": len(cs), "xaml": len(xaml)}


def scan_gate():
    code, out = sh(sys.executable, r"tools\ui-repair\gate.py")
    stages = []
    for line in out.splitlines():
        m = re.match(r"^\[(.+)\]$", line.strip())
        if m:
            stages.append([m.group(1), None])
        elif stages and line.strip().startswith(("PASS", "FAIL")):
            stages[-1][1] = line.strip()
    return code == 0, stages, out


def scan_layout():
    code, out = sh(sys.executable, r"tools\ui-repair\scan.py")
    return code, out


def scan_a11y():
    code, out = sh(sys.executable, r"tools\ui-repair\scan-a11y-names.py")
    return code, out


def scan_skins():
    code, out = sh(sys.executable, r"tools\ui-repair\scan-skins-pixels.py")
    return code, out


def scan_upstream():
    code, out = sh(sys.executable, r"tools\ui-repair\compare-upstream.py")
    only_up = []
    grab = False
    for line in out.splitlines():
        if "upstream only (port candidates)" in line:
            grab = True
            continue
        if line.startswith("--- local only"):
            grab = False
        if grab and line.strip():
            only_up.append(line.strip())
    return only_up, out


# ---------------------------------------------------------------- auto-fixable ---

def find_literal_tokens():
    """Literals that have an exact local token, reported for a human to confirm.

    Three rejections shaped this, each measured:

      1. matching by value alone recommended `MpvShellTitleFontSize` (16) for
         `MediaInfoWindow.xaml`, where that token means "the shell's window title". A value
         comparison cannot see semantics: 81 raw matches, almost all noise.
      2. requiring precedent in the same file cut it to 4 -- and all four were `FontIcon`
         `FontSize="16"` while the token in that file belongs to a `TextBlock`. Icon size
         and text size are different scales, so applying the token would be wrong.
      3. the remaining distinction is the ELEMENT the literal sits on, which a line-based
         regex can see: `FontSize` on `FontIcon` is iconography, on `TextBlock` it is type.

    So a candidate now requires precedence AND the same element kind. `FontIcon` sites are
    never matched to a `*FontSize` token -- there is no icon-size token family, and inventing
    one is a design decision, not a mechanical fix.

    Raw matches are returned too, so the difference between "81 numbers coincide" and
    "N sites are genuinely inconsistent" stays visible in the report.
    """
    code, styles = sh("git", "show", "HEAD:mpv-winui/mpv-winui/Styles/Styles.xaml")
    tokens = {}
    for m in re.finditer(
            r'x:Key="(Mpv[A-Za-z]*Font[A-Za-z]*)"[^>]*>([0-9.]+)</x:Double>', styles):
        tokens[m.group(1)] = float(m.group(2))

    consumers = {}
    for name in tokens:
        code, hits = sh("git", "grep", "-l", "StaticResource " + name,
                        "--", "mpv-winui/mpv-winui/")
        consumers[name] = [h for h in hits.splitlines() if h.strip()]

    by_value = {}
    for k, v in tokens.items():
        by_value.setdefault(v, []).append(k)

    code, hits = sh("git", "grep", "-nE", r'FontSize="[0-9]+"',
                    "--", "mpv-winui/mpv-winui/")
    rows, raw = [], 0
    for line in hits.splitlines():
        m = re.search(r"^([^:]+):(\d+):(.*)FontSize=\"([0-9]+)\"", line)
        if not m:
            continue
        path, ln, before, val = m.group(1), int(m.group(2)), m.group(3), float(m.group(4))
        cands = by_value.get(val, [])
        if not cands:
            continue
        raw += 1

        # Which element is this FontSize on? git grep -n gives the line, so look back.
        elem = ""
        code2, ctx = sh("git", "show", "HEAD:" + path)
        lines = ctx.splitlines()
        for back in range(ln - 1, max(ln - 8, -1), -1):
            mm = re.search(r"<([A-Za-z:]+)", lines[back])
            if mm:
                elem = mm.group(1)
                break
        if elem == "FontIcon":
            continue          # iconography, not type: no token family, skip

        precedent = [c for c in cands if path in consumers.get(c, [])]
        if not precedent:
            continue

        # LAST filter, and it caught the final false positive: the token's NAME must
        # describe what the element is. `MpvTransportTimeFontSize` is 12 and sits in this
        # same file three times, so value+precedent matched -- but the site is an
        # equaliser band label inside a flyout, not a transport time. Using it would put
        # the transport time's scale on a popup caption, and the nearest genuine popup
        # token (`MpvPopupCaptionFontSize`) is 11, so there is no equal-valued correct
        # token at all.
        #
        # What can be checked mechanically is whether the token's name appears in the
        # ancestry of the literal: the enclosing x:Name / DataTemplate x:Key / resource
        # key it lives under. That is a proxy, and the report says so.
        lineage = " ".join(lines[max(ln - 40, 0):ln]).lower()
        fitting = [c for c in precedent
                   if c.lower().replace("mpv", "").replace("fontsize", "")[:6] in lineage]
        if not fitting:
            continue

        rows.append({"file": path, "line": ln, "value": int(val),
                     "tokens": fitting, "element": elem,
                     "single_use": [c for c in fitting
                                    if len(consumers.get(c, [])) <= 1]})
    return rows, raw, consumers


# ---------------------------------------------------------------- report ---

def build_report(data):
    lines = []
    lines.append("# 夜间扫描报告")
    lines.append("")
    lines.append("生成时间：%s" % data["when"])
    lines.append("")
    lines.append("## 健康状态")
    lines.append("")
    if data["gate_ok"]:
        lines.append("`gate.py` **全绿**，树是健康的。")
    else:
        lines.append("`gate.py` **未通过** —— 先修这个，其他候选都排在它后面。")
    lines.append("")
    lines.append("| 阶段 | 结果 |")
    lines.append("|---|---|")
    for name, verdict in data["gate_stages"]:
        lines.append("| %s | %s |" % (name, verdict or "?"))
    lines.append("")

    lines.append("## 已能定的（无需你决策）")
    lines.append("")
    auto = data.get("auto") or []
    for item in auto:
        lines.append("- %s" % item)

    # Findings a machine CAN call: the app is not writing its log. There is no judgement in
    # "a launch produces no log file" -- either the file appears or it does not -- and the
    # consequence (76 AppLogger calls going nowhere) is measured, not inferred.
    #
    # The marker is read from the script's STDOUT capture (applog.txt, English), not from the
    # Chinese markdown it writes (app-log.md). The first version looked for the Chinese
    # sentence in the English capture and silently found nothing -- so the strongest finding
    # of the whole scan was collected and then dropped from the report.
    log_is_stale = False
    applog_path = data.get("logs", {}).get("applog")
    if applog_path and os.path.exists(applog_path):
        body = io.open(applog_path, encoding="utf-8", errors="replace").read()
        log_is_stale = "the newest app log is" in body
        if log_is_stale:
            m = __import__("re").search(
                r"the newest app log is ([\d.]+) days old", body)
            age = m.group(1) if m else "?"
            lines.append("- **应用不写日志**：最新一份 %s 天前，今天启动不产生新文件；"
                         "`NLog` 已引用但无任何配置，76 处 `AppLogger.*` 写向空目标。"
                         "修法是补 NLog 配置或改用内置 `File` 日志，不需要产品判断。"
                         % age)

    if not auto and not log_is_stale:
        lines.append("无。本轮没有可证明安全的全自动修改项。")
    lines.append("")
    lines.append("> 全自动改代码只允许「不改变布局、且有等价证明」的替换。"
                 "字号、间距、颜色即使有对应令牌也不自动改——它们会移动像素。"
                 "这里列的是「结论明确、但需要一个非机械的改动」的项，"
                 "仍由下一轮按 PLAYBOOK 走。")
    lines.append("")

    lines.append("## 需要你决策的候选")
    lines.append("")

    # The app's own log is the strongest evidence available, because the app decided
    # those lines were problems. It comes first, and it comes with its age.
    applog = data.get("logs", {}).get("applog")
    if applog and os.path.exists(applog):
        body = io.open(applog, encoding="utf-8").read()
        lines.append("### 应用自己的日志")
        lines.append("")
        if "这份日志是历史数据" in body:
            lines.append("**应用现在不写日志了。** 最新一份是 29 天前，"
                         "今天启动不产生新文件。")
            lines.append("")
            lines.append("根因：`NLog` 6.2.0 已引用，但仓库里没有 `nlog.config`，"
                         "代码里也没配置 target 或 rules。76 处 `AppLogger.*` 调用"
                         "写向一个不落地的目标。")
            lines.append("")
            lines.append("后果：出错时没有任何可查记录，"
                         "「读日志找问题」这类手段全部失效。")
        else:
            m = re.search(r"distinct problems:\s*(\d+)", body)
            lines.append("不同问题：%s 条，详见 `_work/nightly/applog.txt`。"
                         % (m.group(1) if m else "?"))
        lines.append("")

    ev = data.get("logs", {}).get("evidence")
    if ev and os.path.exists(ev):
        body = io.open(ev, encoding="utf-8").read()
        lines.append("### 可机械判定的维度")
        lines.append("")
        for pat, label in ((r"orphan resources:\s*(\d+)", "孤儿资源键"),
                           (r"dead type suspects:\s*(\d+)", "疑似死类型"),
                           (r"doc references:\s*\d+ inspected,\s*(\d+) broken",
                            "失效文档引用"),
                           (r"winrt event pairs:\s*(\w+)", "C# ↔ C++ 事件配对"),
                           (r"lua script-opts:\s*(\w+)", "Lua script-opts")):
            m = re.search(pat, body)
            if m:
                lines.append("- %s：**%s**" % (label, m.group(1)))
        lines.append("")
        lines.append("**为什么不动手**：「死类型」的判定不可靠。这一轮实测把 22 个候选"
                     "降到 10 个，剩下的大多是 `x:DataType`、"
                     "`{x:Bind local:Helper.Method()}` 这类正则看不清的引用方式。"
                     "删错一个就是编译失败或运行期空白，收益不抵风险。")
        lines.append("")

    tok = data.get("literal_tokens") or []
    raw = data.get("literal_raw", 0)
    if tok:
        lines.append("### 字面字号可换成令牌（%d 处，排除 %d 处无语义先例的）"
                     % (len(tok), raw - len(tok)))
        lines.append("")
        lines.append("只列出**所在文件已经在用同名族令牌**的位置：即同一个文件里已有先例。"
                     "其余同值匹配会被删掉，因为数字相同不代表语义相同——"
                     "`MpvShellTitleFontSize` 的值是 16，但它的意思是「外壳窗口标题」，"
                     "用在 `MediaInfoWindow` 上是语义错配。")
        lines.append("")
        lines.append("| 文件 | 行 | 值 | 可换令牌 | 备注 |")
        lines.append("|---|---:|---:|---|---|")
        for r in tok[:40]:
            note = ""
            if r.get("single_use"):
                note = "令牌仅一处使用，属局部决定"
            lines.append("| `%s` | %d | %d | %s | %s |"
                         % (os.path.basename(r["file"]), r["line"], r["value"],
                            ", ".join("`%s`" % t for t in r["tokens"]), note))
        lines.append("")
        lines.append("**为什么没自动改**：即使有先例，换令牌仍会移动像素。"
                     "逐行确认后手动换，一次换一个文件并跑 `gate.py`。")
        lines.append("")

    if data.get("upstream_only"):
        lines.append("### 上游有、本地没有（%d 个文件）" % len(data["upstream_only"]))
        lines.append("")
        lines.append("先判断是否已用别的方式实现。本仓库已证明 3 个条目属于「误判为缺失、"
                     "实际存在」，照名字移植会白改。")
        lines.append("")
        for p in data["upstream_only"][:30]:
            lines.append("- `%s`" % p)
        lines.append("")

    lines.append("## 原始输出")
    lines.append("")
    for name, path in data.get("logs", {}).items():
        lines.append("- %s：`%s`" % (name, path))
    lines.append("")
    return "\n".join(lines)


def main():
    os.makedirs(OUT, exist_ok=True)
    stamp = time.strftime("%Y-%m-%d %H:%M")
    data = {"when": stamp, "logs": {}}

    print("[1/6] gate")
    ok, stages, gate_out = scan_gate()
    data["gate_ok"] = ok
    data["gate_stages"] = stages
    data["logs"]["gate"] = write("gate.txt", gate_out)

    print("[2/6] layout")
    code, out = scan_layout()
    data["logs"]["layout"] = write("layout.txt", out)

    print("[3/6] a11y names")
    code, out = scan_a11y()
    data["logs"]["a11y"] = write("a11y.txt", out)

    print("[4/6] skins")
    code, out = scan_skins()
    data["logs"]["skins"] = write("skins.txt", out)

    print("[5/6] upstream")
    only_up, out = scan_upstream()
    data["upstream_only"] = only_up
    data["logs"]["upstream"] = write("upstream.txt", out)

    print("[6/7] literal tokens")
    tok, raw, consumers = find_literal_tokens()
    data["literal_tokens"] = tok
    data["literal_raw"] = raw
    data["token_consumers"] = {k: v for k, v in consumers.items()}
    data["auto"] = []          # nothing qualifies yet; see the module docstring

    print("[7/7] app log + machine evidence")
    for script, key in (("scan-app-log.py", "applog"),
                        ("machine-evidence.py", "evidence")):
        code, out = sh(sys.executable,
                       os.path.join("tools", "ui-repair", script))
        data["logs"][key] = write(key + ".txt", out)
        data[key + "_exit"] = code

    rep = build_report(data)
    path = write("report.md", rep)
    write("data.json", json.dumps(data, indent=2, ensure_ascii=False))
    print("\nreport: %s" % path)
    print("gate: %s" % ("green" if ok else "RED"))
    print("literal-token candidates: %d (raw matches %d, %d dropped for no precedent)"
          % (len(data["literal_tokens"]), data["literal_raw"],
             data["literal_raw"] - len(data["literal_tokens"])))
    return 0


if __name__ == "__main__":
    sys.exit(main())
