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

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EXE = os.path.join(REPO, "src", "mpvw-app", "bin", "x64", "Release",
                   "net10.0-windows10.0.26100.0", "win-x64", "mpvw.exe")
PROC = "mpvw.exe"

# The shell's own rail. Everything else in a ListItem is the settings page.
SHELL_RAIL = ("主页", "音乐库", "视频库", "播放队列")

# Element slugs the CLI emits, by control family. A board navigates by caption,
# and which control carries that caption differs: the settings rail is a list,
# the music tabs are radio buttons, a plain command is a button.
SLUG_PREFIXES = ("itm-", "rdo-", "rbtn-", "btn-")
# Where a card lives and how wide it is. Both thresholds identify a card;
# neither may depend on a property an assertion is judging, or that assertion
# stops being able to fail (see cards_on_screen).
# The card control names its root Button in XAML, so this id is stable and says
# nothing about what the assertions measure.
CARD_AUTOMATION_ID = "RootButton"

# How far below a card's bottom edge its captions may start. Generous enough
# for the gap and a wrapped title, small enough not to reach the next row.
CAPTION_BAND_SLACK = 90


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
    # a list row with no accessible name, and Option, SectionHeaderItem and
    # SectionCardItem all used the default -- 26 rows announced
    # "mpv_winui.Domains.Settings.Controls.Option".
    + [("no_type_names", None, None)]
)

# D13: a folded section is either a subdivision (opens in place) or a parallel
# feature block (keeps its card and navigates). The distinction lives in
# SectionShapes; this board checks that it reaches the screen.
#
# Subtitles: six of its sections are subdivisions and they all expand in place.
# The pane also holds one flat section (filter and time axis) whose rows are
# ordinary cards, which is why the card count below is not zero -- an assertion
# of zero would fail for the right reason and tell you nothing.
# The library cards. Each assertion names the failure it catches, because a board
# that only checks "something is on screen" passes on every broken implementation.
#
#   cards_all_named       Every card's accessible name was the EMPTY STRING while
#                         the template bound {Binding AlbumTitle}: ItemsRepeater
#                         sets no DataContext, so the path resolved to null. A
#                         screen reader walked the grid and read nothing.
#   card_name_is_caption  The card is named from the caption the user sees, not
#                         from whatever else happens to be non-empty.
# What this board deliberately does NOT assert, and why: "the media tile has a
# height". The card's Button is VerticalAlignment=Stretch, so the height UIA
# reports is the grid CELL, not the tile. Injecting a zero-height tile left that
# number unchanged and the board stayed green: the assertion measured the wrong
# object. The tile's presence is covered by the cover sample in
# tools/ui-design-audit.py's --screenshot path and by the pixel statistics the
# card research records, not by a tree assertion that cannot fail.
BOARDS["cards"] = {
    # The music library's gear is in the shell; the album tab is a caption.
    "open": "ShellNav_music",
    "open_caption_id": "MusicTabAlbums",
    "assertions": [
        ("cards_all_named", None, None),
        ("card_name_is_caption", None, None),
    ],
}

BOARDS["settings-sections"] = {
    "open": "ShellSettingsButton",
    "open_caption": "字幕",
    "assertions": [
        # Six expanders, one per subdivision.
        ("class_count_at_least", "Expander", 6),
        # And a collapsed expander really holds its rows back. This is the
        # assertion that would have caught the first attempt: SettingsExpander
        # with ItemsSource drew seven cards inside three collapsed headers, so
        # counting expanders alone passes on a broken implementation.
        ("collapsed_rows_hidden", None, None),
    ],
}


# A card is identified by SHAPE, not by an automation id: the cards carry none,
# and adding one to the template so a test can read it is the cart leading the
# horse. The grid's toolbar also holds Buttons, which is what a first version of
# this counted as cards before the rectangle filter.
def cards_on_screen(tree, require_name=False):
    """The library cards, found by AutomationId.

    The card control names its root Button in XAML, so that x:Name becomes the
    automation id. It is the one identifier here that is neither a caption nor a
    measurement, and that matters because the assertions judge both:

      * filtering on name made "every card has a name" report "no card found" and
        stay GREEN on a card with name='';
      * filtering on height/width dropped the zero-height tile that the third
        assertion exists to catch, and the board stayed GREEN again.

    Both were found by injecting the defect and watching the board stay green,
    which is why this repository insists on watching a checker go red.
    """
    out = []
    for el in tree:
        if el.get("automationId") != CARD_AUTOMATION_ID:
            continue
        if require_name and not (el.get("name") or "").strip():
            continue
        out.append(el)
    return out


def caption_pairs(tree):
    """(card name, the caption text that belongs to it) for every card.

    The caption is the Text whose rectangle sits in the card's own column, at or
    below the card's TOP and no further than a caption band under its BOTTOM.
    Both bounds are relative to the card, so a collapsed card - the case the
    third assertion is designed to catch - still yields a pair instead of making
    this helper report "not an audit" and masking the real finding.

    The first version used a fraction of the card's height as the band, which
    made a short card look like it had no captions: injecting the collapsed tile
    turned this assertion red first and hid the one that was supposed to catch it.
    """
    pairs = []
    for card in cards_on_screen(tree):
        cx, cy, cw = card["x"], card["y"], card["width"]
        bottom = cy + (card.get("height") or 0)
        inside = [el for el in tree
                  if (el.get("type") or "") == "Text"
                  and (el.get("name") or "").strip()
                  and el.get("y") is not None
                  and cx <= el["x"] < cx + cw
                  and cy <= el["y"] <= bottom + CAPTION_BAND_SLACK]
        if inside:
            # The caption is the FIRST such Text under the tile, which for both
            # card kinds is the title line.
            inside.sort(key=lambda e: e["y"])
            pairs.append((card["name"].strip(), inside[0]["name"].strip()))
    return pairs


def settings_list_names(tree):
    """ListItem names that belong to the settings page, not the shell."""
    return [(el.get("name") or "").strip()
            for el in tree
            if (el.get("type") or "") == "ListItem"
            and (el.get("name") or "").strip()
            and (el.get("name") or "").strip() not in SHELL_RAIL]


def evaluate(tree, spec):
    kind = spec[0]
    if kind == "class_count":
        # Matched on className, not type: a SettingsExpander surfaces to UIA as a
        # Group whose className is "SettingsExpander", and a SettingsCard keeps
        # its own className too. Counting by type would count Groups.
        n = sum(1 for el in tree
                if spec[1] in str(el.get("className") or ""))
        note = (" (%s)" % spec[3]) if len(spec) > 3 else ""
        return n == spec[2], "found %d, want %d%s" % (n, spec[2], note)
    if kind == "class_count_at_least":
        n = sum(1 for el in tree
                if spec[1] in str(el.get("className") or ""))
        return n >= spec[2], "found %d, want at least %d" % (n, spec[2])
    if kind == "collapsed_rows_hidden":
        # A collapsed expander must be header-sized, not content-sized. This is
        # the assertion the first attempt would have failed: SettingsExpander
        # with ItemsSource reported IsExpanded=False and still drew seven rows
        # inside the header band, so the expander's own state is not evidence.
        #
        # Measured as height instead. The header is ~112 DIP; the smallest
        # section in this pane holds four rows at 64 DIP each, so 400 is well
        # clear of a header and well under any expanded section.
        HEADER_MAX = 400
        expanded = [el for el in tree
                    if str(el.get("className") or "").endswith("Expander")
                    and not el.get("isOffscreen")]
        if not expanded:
            return False, "no expander rows on screen"
        tall = [el for el in expanded if (el.get("height") or 0) > HEADER_MAX]
        if tall:
            return False, ("%d expander(s) taller than %d DIP while collapsed, "
                           "e.g. %r at %s"
                           % (len(tall), HEADER_MAX,
                              (tall[0].get("name") or "")[:20],
                              tall[0].get("height")))
        return True, "%d collapsed expander(s), tallest %s DIP" % (
            len(expanded), max((el.get("height") or 0) for el in expanded))
    if kind == "cards_all_named":
        found = cards_on_screen(tree)
        if not found:
            return False, "no card found; not an audit"
        blank = [c for c in found if not (c.get("name") or "").strip()]
        return not blank, ("%d of %d card(s) have an empty name"
                           % (len(blank), len(found)))

    if kind == "card_name_is_caption":
        pairs = caption_pairs(tree)
        if not pairs:
            return False, "no card carries both a name and a caption; not an audit"
        wrong = [(n, c) for n, c in pairs if n != c]
        if wrong:
            return False, ("%d card(s) named from a different property, e.g. "
                           "name=%r caption=%r" % (len(wrong), wrong[0][0], wrong[0][1]))
        return True, "%d card(s) named from their caption" % len(pairs)

    if kind == "card_tile_has_media":
        found = cards_on_screen(tree)
        if not found:
            return False, "no card found; not an audit"
        # A card's height is its media tile plus a caption band. For a square card
        # that ratio is above 1.0; the collapsed form leaves the caption band alone
        # at about 0.3. The threshold is a table value so the number is visible.
        ratios = [(c.get("height") or 0) / c["width"] for c in found if c.get("width")]
        low = [r for r in ratios if r < spec[2]]
        if low:
            return False, ("%d card(s) at or below h/w %.2f, smallest %.2f"
                           % (len(low), spec[2], min(ratios)))
        return True, "%d card(s), smallest h/w %.2f" % (len(ratios), min(ratios))

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
    if spec[0] == "cards_all_named":
        return "every card carries an accessible name"
    if spec[0] == "card_name_is_caption":
        return "each card is named from its caption"
    if spec[0] == "no_type_names":
        return "no CLR type names in row captions"
    if spec[0] == "collapsed_rows_hidden":
        return "collapsed expanders are header-sized"
    if spec[0] == "class_count":
        return "%s count == %d" % (spec[1], spec[2])
    if spec[0] == "class_count_at_least":
        return "%s count >= %d" % (spec[1], spec[2])
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

        # A board may need a second navigation step: neither the settings rail
        # item nor the music tab carries an AutomationId, so both are found by
        # their caption and then invoked.
        #
        # This used to accept only "ListItem" plus an "itm-" slug, because the
        # settings rail is a list. The music library's tabs are RadioButtons, so
        # all three slug prefixes the CLI emits are accepted now. A rail item
        # still resolves, and a tab resolves too.
        # Two ways to name the second step, because the two nav surfaces differ.
        #
        #   open_caption_id     an AutomationId, when the control has one. The music
        #                       tabs do (MusicTabAlbums), and an id cannot be
        #                       confused with a caption that appears twice.
        #   open_caption        a caption, for the settings rail, whose items carry
        #                       no AutomationId. Its slug is on the ListItem line.
        #
        # The slug the CLI prints for a radio button is "rdo-", and the search lists
        # it on the "^ invoke via:" line rather than the element's own line - both
        # learned by reading the output rather than assuming the shape.
        caption = board.get("open_caption")
        caption_id = board.get("open_caption_id")
        if caption_id:
            caption = caption_id
        if caption:
            out, rc, _ = run(["winapp", "ui", "search", caption, "-a", str(pid)], 90)
            item = caption if caption_id else None
            for line in out.splitlines():
                if caption not in line:
                    continue
                for part in line.split():
                    if part.startswith(SLUG_PREFIXES):
                        item = part
                        break
                if item and not caption_id:
                    break
            if item is None:
                print("FAIL: could not find the %r nav item" % caption)
                return 2
            out, rc, err = run(["winapp", "ui", "invoke", item, "-a", str(pid)], 90)
            if rc != 0:
                print("FAIL: could not open %r (%s)" % (caption, err[:200]))
                return 2
            time.sleep(4)

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
