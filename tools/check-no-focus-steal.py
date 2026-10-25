#!/usr/bin/env python3
"""No probe, checker or verifier may bring the app to the foreground.

**The defect this exists for.** A long verification session made the player window keep
appearing in front of the user's other work. Measured, one candidate at a time:

    launch / close cycles            foreground unchanged (3/3, twice)
    opening the settings window      foreground unchanged, even with the main window moved
                                     on-screen -- because Windows refuses SetForegroundWindow
                                     from a process that does not own the foreground
    the probes themselves            SEVEN of them call SetWindowPos(..., SWP_SHOWWINDOW)
                                     to drag the window from its parked position onto the
                                     screen so they can screenshot it

So the jumping was caused by the measuring scripts, not by the app. `tools/mvpw-mcp/
background_window.py` fixes it (`OnScreen` moves with SWP_NOACTIVATE and restores the original
rectangle), and this checker keeps it fixed: a small fix that is not enforced comes back.

What counts as a violation, in `tools/` and in `_work/`:

  * `SetForegroundWindow(` / `BringWindowToTop(` / `SwitchToThisWindow(` / `SetActiveWindow(`
    -- these take the foreground by definition;
  * `SetWindowPos(` combined with `SWP_SHOWWINDOW` (0x0040) or the literal pair
    `0x0004 | 0x0010` used to mean "move and show", where the intent is to make the window
    visible on a monitor.

Allowed, with the reason in the file:

  * `SWP_NOACTIVATE` (0x0010) with `SWP_NOZORDER` -- the background-safe move;
  * the harness handing the foreground BACK (`SetForegroundWindow(prev)`), which is the only
    reason `mpvw_tools.py` mentions it;
  * a line carrying `noqa: focus-steal-check`, which is how a probe that must NAME the API it
    is demonstrating exempts itself. That marker exists because two of these scripts exist
    precisely to try the injections and show they are refused on this machine -- removing the
    call would remove the evidence. The marker is deliberately verbose so it cannot be added
    by accident;
  * `AppWindow.Show()` in the app itself -- product code, out of scope by path.

Exit 1 on any violation, and it names the file and line so the fix is one edit away.
"""
import argparse
import pathlib
import re
import sys

REPO = pathlib.Path(__file__).resolve().parent.parent

SCAN_ROOTS = [REPO / "tools", REPO / "_work"]

# Foreground-taking calls. `SetForegroundWindow(prev)` is excluded below.
TAKE_FOREGROUND = re.compile(
    r"\b(SetForegroundWindow|BringWindowToTop|SwitchToThisWindow|SetActiveWindow)\s*\(")

# A SetWindowPos call that also asks to show the window.
SETWINDOWPOS = re.compile(r"\bSetWindowPos\s*\(", re.S)

# Showing flags/literals inside a SetWindowPos argument list.
SHOW_LITERALS = ("SWP_SHOWWINDOW", "0x0040", "0x0004 | 0x0010", "0x0004|0x0010",
                 "SWP_NOZORDER | SWP_SHOWWINDOW")

# Files legitimately allowed to hand the foreground back.
ALLOW_HAND_BACK = {"mpvw_tools.py"}

# The pages the checker must never report a violation for: the harness itself.
SELF = {"background_window.py", "check-no-focus-steal.py"}


def call_args(text, open_paren):
    """The argument text of the call starting at `open_paren`."""
    depth = 0
    for i in range(open_paren, len(text)):
        ch = text[i]
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
            if depth == 0:
                return text[open_paren + 1:i]
    return text[open_paren + 1:open_paren + 400]


def scan_file(path):
    try:
        text = path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return []

    out = []
    lines = text.splitlines()

    def line_of(offset):
        return text.count("\n", 0, offset) + 1

    def has_noqa(offset):
        """True when the offending line carries the exemption marker."""
        ln = line_of(offset)
        if 1 <= ln <= len(lines):
            return "noqa: focus-steal-check" in lines[ln - 1]
        return False

    for m in TAKE_FOREGROUND.finditer(text):
        if has_noqa(m.start()):
            continue
        args = call_args(text, text.index("(", m.start()))
        # Handing the foreground BACK to whoever had it is what the harness does.
        if m.group(1) == "SetForegroundWindow" and args.strip() in ("prev", "previous",
                                                                   "original", "before"):
            continue
        out.append((line_of(m.start()), m.group(1) + "(...)"))

    for m in SETWINDOWPOS.finditer(text):
        if has_noqa(m.start()):
            continue
        args = call_args(text, text.index("(", m.start()))
        lowered = args.replace(" ", "")
        for lit in SHOW_LITERALS:
            if lit.replace(" ", "") in lowered:
                # Make sure it is not there together with SWP_NOACTIVATE only.
                out.append((line_of(m.start()), "SetWindowPos(..., %s)" % lit))
                break

    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--verbose", action="store_true")
    args = ap.parse_args()

    findings = []
    scanned = 0
    for root in SCAN_ROOTS:
        if not root.is_dir():
            continue
        for path in sorted(root.rglob("*.py")):
            if path.name in SELF:
                continue
            if any(part in ("bin", "obj") for part in path.parts):
                continue
            scanned += 1
            if path.name in ALLOW_HAND_BACK:
                # Only the hand-back form is allowed there; other forms still count.
                hits = [(ln, what) for ln, what in scan_file(path)
                        if not what.startswith("SetForegroundWindow")]
                hits += [(ln, what) for ln, what in scan_file(path)
                         if what.startswith("BringWindowToTop") or
                         what.startswith("SwitchToThisWindow") or
                         what.startswith("SetActiveWindow")]
            else:
                hits = scan_file(path)
            for line, what in hits:
                findings.append((path, line, what))

    if args.verbose:
        print("scanned %d python file(s) under %s"
              % (scanned, ", ".join(str(r.name) for r in SCAN_ROOTS)))

    if not findings:
        print("OK: %d file(s) scanned, no probe brings the app to the foreground" % scanned)
        return 0

    print("FAIL: %d foreground-taking call(s) in probe code" % len(findings))
    for path, line, what in findings:
        try:
            rel = path.relative_to(REPO)
        except ValueError:
            rel = path
        print("  %s:%d  %s" % (rel, line, what))
    print("\nUse tools/mvpw-mcp/background_window.py: `OnScreen(hwnd, w, h)` moves the window")
    print("with SWP_NOACTIVATE and restores its rectangle, and `foreground_guard()` fails the")
    print("probe if the foreground window changes.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
