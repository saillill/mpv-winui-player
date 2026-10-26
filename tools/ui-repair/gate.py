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
    """Run one verifier and judge it by its exit code AND its own verdict line.

    **The defect this replaces.** The old rule was

        ok = code == 0 and ("OK" in tail or "PASS" in tail or "passed -> OK" in tail)

    where `tail` was the LAST non-empty line. That is a substring test, not a verdict, and
    it fails in both directions:

      * false PASS -- any output whose final line happens to contain "OK" passes, even if
        every assertion above it failed. The word "OK" appears in ordinary progress text,
        in a table of per-item results, and in the phrase "not OK".
      * blind to silence -- a verifier whose assertions never ran (a dead instrument, an
        empty result set, a regex that stopped matching) usually still exits 0 and its last
        line still looks fine. Five playback verifiers sent `loadfile` over IPC and never
        exercised the app at all; this gate called that 8/8 PASS while the app could not
        play a video.

    So a verifier now has to do two things the old code never asked for:

      1. print a machine-readable verdict line the gate can parse exactly, and
      2. prove it can fail, on demand, via --self-test.

    Both are required. (2) is the rule this repo already states for checkers -- "a checker
    that has never been seen to fail is not evidence" -- applied to behaviour verifiers.
    """
    def inner():
        # (1) the verifier's own explicit verdict
        code, out = run([sys.executable, os.path.join(MCP, script)])
        m = re.search(r"^VERDICT:\s*(PASS|FAIL)\b", out, re.M)
        if not m:
            last = [l.strip() for l in out.splitlines() if l.strip()]
            return False, ("no 'VERDICT: PASS|FAIL' line -- this verifier predates the "
                           "contract and cannot be trusted; tail: %s"
                           % (last[-1][:80] if last else "<empty>"))
        stated_pass = m.group(1) == "PASS"
        if code != 0 and stated_pass:
            return False, "printed VERDICT: PASS but exited %d -- the two disagree" % code
        if code == 0 and not stated_pass:
            return False, "printed VERDICT: FAIL but exited 0 -- the two disagree"
        if not stated_pass:
            return False, "verifier says FAIL"

        # (2) and it must be able to report a failure
        scode, sout = run([sys.executable, os.path.join(MCP, script), "--self-test"])
        sm = re.search(r"^SELF-TEST\s+(PASSED|FAILED)\b", sout, re.M)
        if not sm:
            return False, ("no 'SELF-TEST PASSED|FAILED' line -- this verifier has never "
                           "been shown to detect a broken subject")
        if scode != 0 or sm.group(1) != "PASSED":
            return False, "self-test did not pass (exit %d, %s)" % (scode, sm.group(1))
        return True, "verdict PASS, self-test proved it can fail"
    return inner


def main():
    stages = [
        ("check-xaml", s_xaml),
        ("build", s_build),
        ("static checkers", s_checkers),
        ("deploy consistency", s_deploy),
        # Playback first among the behaviour verifiers, and it is the reason this file was
        # rewritten: the old stage list had no playback verifier that used the app's own
        # open path, so "8/8 stages passed" was printed while a video could not be played
        # at all. A gate that cannot see the product's primary feature is not a gate.
        ("verify_playback", verifier("verify_playback.py")),
        ("verify_e2e", verifier("verify_e2e.py")),
        ("verify_settings", verifier("verify_settings.py")),
        ("verify_settings_rows", verifier("verify_settings_rows.py")),
        ("verify_screenshot", verifier("verify_screenshot.py")),
        # The UI verifiers, added with the defects they prove. Each one measures a property of
        # the real window -- image content, control geometry, what is on screen -- because every
        # defect in the queue was found by measurement and none by reading the XAML. They are in
        # the gate so that a future edit which re-breaks one of them fails HERE rather than in
        # somebody's screenshot review.
        ("verify_music_cards", verifier("verify_music_cards.py")),
        ("verify_letter_rail", verifier("verify_letter_rail.py")),
        ("verify_queue_button", verifier("verify_queue_button.py")),
        ("verify_home_button", verifier("verify_home_button.py")),
        ("verify_overlay_buttons", verifier("verify_overlay_buttons.py")),
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
