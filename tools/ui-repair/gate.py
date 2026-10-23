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
    r = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True,
                       errors="replace", timeout=timeout)
    return r.returncode, (r.stdout or "") + (r.stderr or "")


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
    code, out = run([PWSH, "-NoProfile", "-File", r".\build.ps1",
                     "-Configuration", "Release", "-Platform", "x64"])
    errs = [l for l in out.splitlines() if re.search(r"error (CS|MSB|XamlCompiler)", l)]
    code_errs = [l for l in out.splitlines() if ": error CS" in l]
    ok = code == 0 and not code_errs
    return ok, "%d error line(s)" % len(code_errs) if not ok else "0 errors"


def s_checkers():
    import glob
    tools = glob.glob(os.path.join(REPO, "tools", "check-*.py"))
    bad = []
    for p in tools:
        code, out = run([sys.executable, p])
        if code != 0:
            last = out.strip().splitlines()[-1][:100] if out.strip() else ""
            bad.append("%s: %s" % (os.path.basename(p), last))
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
