"""One command that says whether the tree is currently healthy.

A "done" claim needs all of: the build, the static checkers, the behaviour verifiers, and
deployment consistency. Doing those by hand every round costs attention that belongs on
the defect, and skipping one is how a regression ships.

Runs in this order, stopping at the first stage that fails:

  1. check-xaml          fast, and every XAML edit can break it
  2. build               0 errors
  3. the 17 check-*.py   every one must exit 0
  4. deploy --Verify     identical N, differing 0
  5. verifiers           e2e, settings, settings_rows, screenshot

Exit 0 only when every stage passed. Prints the exact command to re-run a failed stage.

Deliberately NOT included: audit_domains.py (its summary is not evidence -- see its own
header), and the analyser's 24 WUI findings (all proven false positives; counting them
would train whoever reads this to ignore the number).
"""
import os
import re
import subprocess
import sys

REPO = r"C:\Users\z2637\Documents\DeepSeek\mpv"
MCP = os.path.join(REPO, "tools", "mvpw-mcp")
PWSH = "pwsh"


def run(cmd, cwd=REPO, timeout=1800):
    """Run a command and decode its output as UTF-8, whatever the console code page is.

    **Measured failure that this fixes.** With `text=True` and no encoding, Python decodes
    with the ANSI code page (GBK here), but `pwsh` and MSBuild write UTF-8. The result was
    mojibake that made every CJK pattern unmatched:

        real:      失败!  - 失败:     1，通过:   495
        decoded:   澶辫触!  - 澶辫触:     1锛岄€氳繃:   495

    That is why `s_build` reported "no recognisable error line" for a build that had
    printed one, and why its detail line said `0 error line(s)` for a failing build: the
    Chinese suite summaries were unreadable to the checker. ASCII markers (`: error CS`)
    survived, which is exactly why the fault was invisible until a test-only failure was
    injected.

    Errors are replaced rather than dropped: a garbled line still prints, so a future
    encoding surprise is visible instead of silently swallowing the evidence.
    """
    r = subprocess.run(cmd, cwd=cwd, capture_output=True, timeout=timeout)
    out = (r.stdout or b"").decode("utf-8", "replace") + \
          (r.stderr or b"").decode("utf-8", "replace")
    return r.returncode, out


def stage(name, fn):
    print("\n[%s]" % name)
    ok, detail = fn()
    print("   " + ("PASS" if ok else "FAIL") + (": " + detail if detail else ""))
    return ok


def s_xaml():
    code, out = run([sys.executable, r"tools\check-xaml.py"])
    ok = code == 0 and "OK:" in out
    return ok, out.strip().splitlines()[-1][:110] if out.strip() else ""


def s_build():
    """build.ps1 must exit 0. The exit code is the reading; the error lines are decoration.

    **What the first version got wrong.** It decided with
    `ok = code == 0 and not code_errs` but then reported
    `"%d error line(s)" % len(code_errs)` -- so a build that exited 1 with no
    `: error CS` line printed "0 error line(s)", which reads like a clean build and is
    exactly the "healthy-looking output from a failed step" shape this repo has been bitten
    by before (AGENTS.md: a failed build followed by a deploy that copied stale binaries).

    Measured: `build.ps1` runs two test suites (`mpv-conf-test`, `mpv-winrt-test`) after
    compiling, so a failing unit test exits 1 with zero compiler errors. Injecting
    `NarrowWidth = 260` (compiles fine, breaks TightPitchFitsTheNarrowestPortraitBar) made
    the gate print `FAIL: 0 error line(s)` -- the failure was caught only because the
    `and` happened to be there, and the message named nothing.

    So the detail line now says what actually failed: the exit code, and the test-suite
    summary when the output contains one. A non-zero exit with no error lines is exactly
    the case a reader must be able to see.
    """
    code, out = run([PWSH, "-NoProfile", "-File", r".\build.ps1",
                     "-Configuration", "Release", "-Platform", "x64"])
    code_errs = [l for l in out.splitlines() if ": error CS" in l]
    xaml_errs = [l for l in out.splitlines()
                 if re.search(r"error (MSB|XamlCompiler)", l)]
    # dotnet prints its suites in the OS language. Measured on this machine, both lines
    # arrive on **stdout** (stderr was empty, 0 bytes):
    #   `  失败 TightPitchFitsTheNarrowestPortraitBar [15 ms]`   (a failing test)
    #   `失败!  - 失败:     1，通过:   495，已跳过:     0，总计:   496`
    # The first pattern must not use \w after the word: this SDK writes a CJK test name
    # there and `\w` does not match it under the default re flags, which is why the first
    # attempt missed both lines and reported "no recognisable error line".
    test_fail = [l for l in out.splitlines()
                 if re.search(r"^\s*(失败|Failed)\s+\S", l)
                 or re.search(r"(失败|Failed)!\s*[-–]", l)
                 or re.search(r"(失败|Failed):\s*[1-9]", l)]

    ok = code == 0 and not code_errs

    if ok:
        return True, "exit 0, 0 errors"

    bits = ["exit %d" % code]
    if code_errs:
        bits.append("%d CS error(s)" % len(code_errs))
    if xaml_errs:
        bits.append("%d MSB/XAML error(s)" % len(xaml_errs))
    if test_fail:
        bits.append("test suite failed: " + test_fail[0].strip()[:70])
    if not (code_errs or xaml_errs or test_fail):
        # Say so explicitly: a non-zero exit with nothing recognisable is not a pass.
        bits.append("no recognisable error line -- read the raw output")
    return False, "; ".join(bits)


def s_checkers():
    import glob
    # The list is the glob, which already covered check-doc-ranges.py -- and that is the
    # point worth recording: the checker was IN the gate and the gate was green, while it
    # printed "4 ranges have drifted" and exited 0 on every run. Presence in this list is
    # not evidence; the exit code is. It exits 1 on drift now.
    #
    # A checker that is missing from tools/ is a failure rather than a silently shorter
    # list, because a renamed or deleted checker would otherwise reduce the gate's
    # coverage without saying so.
    tools = sorted(glob.glob(os.path.join(REPO, "tools", "check-*.py")))
    if not tools:
        return False, "no check-*.py found -- the glob matched nothing"
    bad = []
    for p in tools:
        code, out = run([sys.executable, p])
        if code != 0:
            # The FIRST line that names the problem, not the last line of output. Measured:
            # check-doc-ranges ends with three lines of explanatory prose, so "the last
            # line" made the gate's detail read
            #     check-doc-ranges.py: four ranges it used to print were real ...
            # which names no finding at all. A failure summary has to say what failed.
            lines = [l.strip() for l in out.splitlines() if l.strip()]
            reason = ""
            for l in lines:
                if re.search(r"^(FAIL|ERROR|FATAL)\b", l) or ":" in l and "%" in l:
                    reason = l
                    break
            if not reason:
                reason = lines[-1] if lines else "(no output)"
            bad.append("%s: %s" % (os.path.basename(p), reason[:110]))
    return (not bad), ("%d/%d passed" % (len(tools) - len(bad), len(tools))
                       if not bad else "; ".join(bad[:3]))


def s_deploy():
    code, out = run([PWSH, "-NoProfile", "-File", r"tools\deploy-test.ps1",
                     "-CloseRunning", "-Verify"])
    m = re.search(r"differing:\s*(\d+)", out)
    diff = int(m.group(1)) if m else None
    ok = code == 0 and diff == 0
    return ok, "differing=%s" % diff


def verifier(script):
    def inner():
        code, out = run([sys.executable, os.path.join(MCP, script)])
        last = [l.strip() for l in out.splitlines() if l.strip()]
        tail = last[-1][:100] if last else ""
        ok = code == 0 and ("OK" in tail or "PASS" in tail
                            or "passed -> OK" in tail)
        return ok, tail
    return inner


def main():
    stages = [
        ("check-xaml", s_xaml),
        ("build", s_build),
        ("static checkers", s_checkers),
        ("deploy consistency", s_deploy),
        ("verify_e2e", verifier("verify_e2e.py")),
        ("verify_settings", verifier("verify_settings.py")),
        ("verify_settings_rows", verifier("verify_settings_rows.py")),
        ("verify_screenshot", verifier("verify_screenshot.py")),
    ]

    results = []
    for name, fn in stages:
        ok = stage(name, fn)
        results.append((name, ok))
        if not ok:
            print("\nstopped at the first failure: %s" % name)
            print("fix it, then re-run: python tools/ui-repair/gate.py")
            return 1

    print("\n" + "=" * 56)
    print("gate: %d/%d stages passed -- tree is healthy"
          % (sum(1 for _, o in results if o), len(results)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
