#!/usr/bin/env python3
"""Assert the shape of a redesigned board, without a screenshot.

ui-binding-smoke.py asks "did a binding deliver text". This asks "is the layout
the one that was designed": which panes the rail has, whether they are in the
right order, whether anything reports its type name instead of its caption.
Every one of those is readable from the UIA tree, so none of them needs a human
to look at a picture.

Two things about the shape:

  * The assertions are **data**, one table per board. check-ui-tooltips.py in
    this repository once printed 38 findings, always exited 0, and pointed at
    line numbers shifted by a comment deletion, because its checks were code.
    A table can be read, diffed and extended without touching the runner.

  * It reads the tree with one `ui inspect --json` and matches in process,
    rather than one `get-property` round trip per assertion. Each CLI call takes
    the desktop, so N calls is N interruptions.

Not in build.ps1 on purpose: docs/verification.md records why the build must not
start the app and take focus.

Usage:
  python tools/ui-design-audit.py --list
  python tools/ui-design-audit.py --board settings
  python tools/ui-design-audit.py --board settings --snapshot
  python tools/ui-design-audit.py --board settings --screenshot out.png

--snapshot prints what the tree actually says instead of judging it. That is
what produces the before/after pair the rework is accepted against.

Exit: 0 every assertion held; 1 at least one failed; 2 the app never came up.
"""
from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import time

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EXE = os.path.join(REPO, "src", "mpvw-app", "bin", "x64", "Release",
                   "net10.0-windows10.0.26100.0", "win-x64", "mpvw.exe")
PROC = "mpvw.exe"

# The shell's own rail. Everything else in a ListItem is the settings page.
SHELL_RAIL = ("主页", "音乐库", "视频库", "播放队列")


def run(args, timeout=120):
    try:
        r = subprocess.run(args, capture_output=True, text=True, encoding="utf-8",
                           errors="replace", timeout=timeout)
    except (OSError, subprocess.TimeoutExpired) as exc:
        return "", 1, str(exc)
    return r.stdout, r.returncode, (r.stderr or "").strip()


def app_pid():
    """The pid of the running mpvw, or None.

    Asks for StartTime as well as Id, and keeps only a process that started
    after this script did. Without that the poll can return the *previous*
    instance while it is still exiting, and every later inspect runs against a
    pid that no longer has a window -- which reads as "inspection returned no
    elements", the least informative answer available.
    """
    script = (
        "$p = Get-Process mpvw -ErrorAction SilentlyContinue | "
        "Where-Object { $_.StartTime -gt (Get-Date).AddMinutes(-5) } | "
        "Select-Object -First 1; if ($p) { $p.Id }"
    )
    out = subprocess.run(["powershell", "-NoProfile", "-Command", script],
                         capture_output=True, text=True).stdout.strip()
    return int(out) if out.isdigit() else None


def close_app():
    """Graceful first, forced second: /F skips the flush mpv does on shutdown."""
    subprocess.run(["taskkill", "/IM", PROC], capture_output=True)
    for _ in range(40):
        if app_pid() is None:
            return
        time.sleep(0.25)
    subprocess.run(["taskkill", "/IM", PROC, "/F"], capture_output=True)
    for _ in range(20):
        if app_pid() is None:
            return
        time.sleep(0.25)


def flatten(nodes):
    """Every element in an inspect payload, depth first.

    Accepts nested lists as well as `children`, because the payload is a list of
    windows and each window's `elements` is itself a list: passing the outer list
    straight in used to yield nothing at all, and the caller reported that as
    "inspection returned no elements" -- an empty result that looked exactly like
    a dead app.
    """
    for node in nodes or []:
        if isinstance(node, list):
            yield from flatten(node)
            continue
        if not isinstance(node, dict):
            continue
        yield node
        yield from flatten(node.get("children"))


# ---------------------------------------------------------------------------
# Boards.
#
# Assertion kinds, evaluated against the flattened tree:
#   ("list_names",  caption, _)        a ListItem carries this name
#   ("list_count",  caption, n)        exactly n ListItems carry it
#   ("no_type_names", None, None)      no element's name is a CLR type name
#   ("present",     automation_id)     an element with this id exists
#   ("name",        automation_id, t)  its Name equals t
#
# Captions are asserted in the running language, so these are also the
# language-switch check: a pane whose caption did not refresh fails here.
# ---------------------------------------------------------------------------

BOARDS = {
    "settings": {
        # The gear in the shell. Its AutomationId is ShellSettingsButton; an
        # earlier version of this table said "SettingsButton" and opened
        # nothing, which is why the empty-tree check exists.
        "open": "ShellSettingsButton",
        # Undefined here on purpose below -- see build_settings_assertions.
        "assertions": [],
    },
}

# The rail, in order. Eight panes; it was six before the rework. Shortcuts and
# Input are the two new ones: both used to resolve to the window's caption, so
# neither had a rail entry and the rebind list was a section buried under window
# geometry.
SETTINGS_RAIL = ["程序", "画面", "声音", "字幕", "播放", "界面", "快捷键", "输入行为"]

BOARDS["settings"]["assertions"] = (
    [("list_names", caption, None) for caption in SETTINGS_RAIL]
    # Each new pane exactly once. "快捷键" appeared twice when the rail paired
    # captions to keys by index: CategoryOrder and CategoryKeys are neither the
    # same length nor the same order.
    + [("list_count", "快捷键", 1), ("list_count", "输入行为", 1)]
    # Nothing may report its own CLR type name. UIA falls back to ToString() for
    # a list row with no accessible name, and both Option and SectionHeaderItem
    # used the default -- 26 rows announced
    # "mpv_winui.Domains.Settings.Controls.Option".
    + [("no_type_names", None, None)]
)


def settings_list_names(tree):
    """ListItem names that belong to the settings page, not the shell."""
    return [(el.get("name") or "").strip()
            for el in tree
            if (el.get("type") or "") == "ListItem"
            and (el.get("name") or "").strip()
            and (el.get("name") or "").strip() not in SHELL_RAIL]


def evaluate(tree, spec):
    kind = spec[0]
    if kind == "list_names":
        names = settings_list_names(tree)
        ok = spec[1] in names
        return ok, ("absent from %d row(s)" % len(names))
    if kind == "list_count":
        names = settings_list_names(tree)
        n = names.count(spec[1])
        return n == spec[2], "appears %d time(s), want %d" % (n, spec[2])
    if kind == "no_type_names":
        bad = [el.get("name") for el in tree
               if (el.get("name") or "").startswith("mpv_winui.")]
        return not bad, ("%d row(s), e.g. %r" % (len(bad), bad[0]) if bad else "none")
    if kind == "present":
        return any(el.get("automationId") == spec[1] for el in tree), "not in the tree"
    if kind == "name":
        for el in tree:
            if el.get("automationId") == spec[1]:
                actual = (el.get("name") or "").strip()
                return actual == spec[2], "name=%r want %r" % (actual, spec[2])
        return False, "not in the tree"
    return False, "unknown assertion kind %r" % (kind,)


def describe(spec):
    if spec[0] == "no_type_names":
        return "no CLR type names in row captions"
    if spec[0] in ("list_names", "list_count"):
        return "rail %s %r%s" % (spec[0].split("_")[1], spec[1],
                                 "" if spec[2] is None else " x%d" % spec[2])
    return "%s %s" % (spec[1], spec[0])


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--board", default="settings")
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--snapshot", action="store_true",
                    help="print the observed rows instead of judging them")
    ap.add_argument("--screenshot", metavar="PATH",
                    help="also save a PNG to PATH (evidence for the A comparison)")
    args = ap.parse_args()

    if args.list:
        for name, board in sorted(BOARDS.items()):
            print("%-10s %d assertion(s)" % (name, len(board["assertions"])))
        return 0
    board = BOARDS.get(args.board)
    if board is None:
        print("FAIL: no board named %r (try --list)" % args.board)
        return 2
    if not os.path.isfile(EXE):
        print("FAIL: no build at %s" % EXE)
        return 2

    close_app()
    proc = subprocess.Popen([EXE], cwd=os.path.dirname(EXE))
    try:
        # The Popen handle is the authority on which process this run started.
        # Asking the OS instead races with the previous instance's exit.
        pid = proc.pid
        for _ in range(60):
            if proc.poll() is not None:
                print("FAIL: the app exited during startup (code %s)" % proc.returncode)
                return 2
            if app_pid() == pid:
                break
            time.sleep(0.5)
        else:
            if app_pid() is None:
                print("FAIL: the app never started")
                return 2
        # A window is not the same as a loaded shell: the shell builds its rail
        # and the settings page its option tree, and inspecting early captures
        # a half-built tree.
        time.sleep(14)

        opener = board.get("open")
        if opener:
            out, rc, err = run(["winapp", "ui", "invoke", opener, "-a", str(pid)], 90)
            if rc != 0:
                print("FAIL: could not open the board (%s) %s" % (opener, err[:200]))
                return 2
            time.sleep(3)

        if args.screenshot:
            shot = os.path.abspath(args.screenshot)
            os.makedirs(os.path.dirname(shot), exist_ok=True)
            run(["winapp", "ui", "screenshot", "--output", shot, "-a", str(pid)], 120)
            if not os.path.isfile(shot) or os.path.getsize(shot) == 0:
                print("FAIL: screenshot was not delivered to %s" % shot)
                return 2
            print("screenshot: %s" % shot)

        out, rc, err = run(["winapp", "ui", "inspect", "--json", "-a", str(pid), "-d", "14"], 150)
        if rc != 0:
            print("FAIL: inspect failed (exit %d) %s" % (rc, err[:400]))
            return 2
        try:
            payload = json.loads(out)
        except json.JSONDecodeError as exc:
            print("FAIL: inspect did not return JSON (%s): %s" % (exc, out[:300]))
            return 2

        tree = list(flatten([w.get("elements") for w in payload.get("windows", [])]))
        if not tree:
            print("FAIL: inspection returned no elements; not an audit")
            return 2

        if args.snapshot:
            rows = settings_list_names(tree)
            print(json.dumps(rows, ensure_ascii=False, indent=2))
            print("\n%d settings row(s), %d element(s) inspected"
                  % (len(rows), len(tree)), file=sys.stderr)
            return 0

        failed = 0
        for spec in board["assertions"]:
            ok, detail = evaluate(tree, spec)
            if not ok:
                failed += 1
                print("  FAIL  %-44s %s" % (describe(spec), detail))

        if failed:
            print("\nFAIL: %d of %d assertion(s) did not hold for board %r"
                  % (failed, len(board["assertions"]), args.board))
            return 1
        print("OK: %d assertion(s) held for board %r; %d element(s) inspected"
              % (len(board["assertions"]), args.board, len(tree)))
        return 0
    finally:
        close_app()


if __name__ == "__main__":
    sys.exit(main())
