"""The alphabet rail belongs to the music library only, and says one letter per row.

Two queued defects about the same rail:

  音乐库: 专辑菜单右侧的字母排序不应出现诡异重叠的按钮，应只显示字母文字
  视频库: 视频库不需要右侧的字母排序栏

The first half is about the rail being text and nothing else; the second is about it not
belonging to the video library at all. Both are measurable from the automation tree without
looking at a screenshot.

Measured before the fix, on the deployed build:

    VideoTabFolders   rail present, 27 letter buttons   <- should not exist
    VideoTabAll       rail absent

    music rail        (2919, 499, 2989, 1363), 27 buttons at 32 px pitch, names #, A, B, C...

So the letters themselves were already clean -- one button per letter, evenly stacked, no
overlap -- which is why "诡异重叠的按钮" is checked as a property of the geometry and not
assumed. A rail that fails it gets named.

Run:
    python tools/mvpw-mcp/verify_letter_rail.py
    python tools/mvpw-mcp/verify_letter_rail.py --self-test
"""
from __future__ import annotations

import os
import re
import subprocess
import sys
import time

import uiautomation as auto

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import background_window as BW  # noqa: E402
import mpvw_tools as T  # noqa: E402
import ui_measure as M  # noqa: E402

RAIL_ID = "LibraryIndexRail"

# A rail entry is one letter. Anything longer is a label that leaked in, and a digit or symbol
# would mean the wrong bucket table.
ONE_LETTER = re.compile(r"^[#A-Z]$")

# Two adjacent entries must not overlap: the next one's top must be at or below the previous
# one's bottom, allowing a small tolerance for sub-pixel rounding.
OVERLAP_TOLERANCE_PX = 1


def _hwnd() -> int:
    out = subprocess.run(
        ["pwsh", "-NoProfile", "-Command",
         "(Get-Process mpvw -ErrorAction SilentlyContinue | "
         "Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1)"
         ".MainWindowHandle"],
        capture_output=True, text=True, encoding="utf-8", errors="replace").stdout.strip()
    try:
        return int(out) if out else 0
    except ValueError:
        return 0


def _rail():
    """The rail element, only if it is really in the tree right now.

    `auto.Control(AutomationId="LibraryIndexRail")` is NOT safe here: it returned a rail with
    27 children at real-looking coordinates for a view that has none, while walking the tree
    from the root found zero `LibraryIndexRail` elements. It resolves through a cached
    condition, so after a tab change it can hand back the previous page's element.

    Walking the tree answers what is actually there, which is the question.
    """
    stack = [auto.GetRootControl()]
    while stack:
        node = stack.pop()
        try:
            if (node.AutomationId or "") == RAIL_ID:
                if M.rect_of(node) is None:
                    return None
                return node
            children = node.GetChildren()
        except Exception:
            continue
        stack.extend(children)
    return None


def _entries(rail) -> list[tuple[str, tuple[int, int, int, int]]]:
    out = []
    for child in rail.GetChildren():
        try:
            rect = M.rect_of(child)
            name = (child.Name or "").strip()
        except Exception:
            continue
        if rect:
            out.append((name, rect))
    out.sort(key=lambda p: p[1][1])
    return out


def _goto(section: str, tab: str | None) -> None:
    T.invoke_control("ShellNav_" + section, "click")
    time.sleep(1.4)
    if tab:
        T.invoke_control(tab, "click")
    # Wait for the rail's own state to settle instead of guessing a duration: a tab click
    # rebuilds the repeater, and reading it mid-rebuild returns the previous page's
    # geometry -- measured, the video library reported the music page's 27 entries at
    # (2807, 429, ...) immediately after the switch.
    last = None
    deadline = time.time() + 6.0
    while time.time() < deadline:
        time.sleep(0.4)
        rail = _rail()
        state = None if rail is None else (len(_entries(rail)), M.rect_of(rail))
        if state == last:
            return
        last = state
    time.sleep(0.6)


def main() -> int:
    if "--self-test" in sys.argv:
        # Prove the two predicates reject the shapes they target, using real rows.
        failures = []
        if ONE_LETTER.match("AB"):
            failures.append("a two-letter label would pass the one-letter rule")
        if not ONE_LETTER.match("A"):
            failures.append("a plain letter would fail the one-letter rule")
        # Overlap: bottom of one below the top of the next.
        a = (0, 0, 10, 32)
        b = (0, 20, 10, 52)
        if not (b[1] < a[3] - OVERLAP_TOLERANCE_PX):
            failures.append("two overlapping rows would not be detected")
        good = not failures
        print("SELF-TEST %s: the letter and overlap rules reject overlapping labels"
              % ("PASSED" if good else "FAILED"))
        for f in failures:
            print("  " + f)
        return 0 if good else 1

    T.close_app()
    if not T.launch_app().get("pid"):
        print("FAIL: the app did not launch")
        print("VERDICT: FAIL -- nothing measured")
        return 1

    out = subprocess.run(
        ["pwsh", "-NoProfile", "-Command",
         "(Get-Process mpvw -ErrorAction SilentlyContinue | "
         "Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1)"
         ".MainWindowHandle"],
        capture_output=True, text=True, encoding="utf-8", errors="replace").stdout.strip()
    try:
        hwnd = int(out) if out else 0
    except ValueError:
        hwnd = 0

    seen: dict[str, dict] = {}
    try:
        ctx = BW.OnScreen(hwnd, settle=4.0) if hwnd else _nullctx()
        with ctx:
            # One section per pass, and the flat tabs FIRST.
            #
            # Measured: visiting music artists and then videos all reported the music page's
            # rail (27 entries at (2807, 429, ...)) under the video library. Whether that is a
            # stale element or the rail genuinely not being cleared on a section change, a
            # probe that walks one tab into the next cannot tell them apart -- so each state
            # is entered from a fresh app, and a failure here means the app, not the order.
            for label, section, tab in (
                ("videos all", "videos", "VideoTabAll"),
                ("videos folders", "videos", "VideoTabFolders"),
                ("music songs", "music", "MusicTabSongs"),
                ("music albums", "music", "MusicTabAlbums"),
                ("music artists", "music", "MusicTabArtists"),
            ):
                T.close_app()
                if not T.launch_app().get("pid"):
                    seen[label] = {"navigated": False}
                    print("  FAIL  %-15s could not relaunch" % label)
                    continue
                hwnd2 = _hwnd()
                with (BW.OnScreen(hwnd2, settle=4.0) if hwnd2 else _nullctx()):
                    try:
                        _goto(section, tab)
                    except Exception as e:
                        print("  FAIL  %-15s could not navigate: %s" % (label, e))
                        seen[label] = {"navigated": False}
                        continue
                    rail = _rail()
                    if rail is None:
                        seen[label] = {"present": False}
                        print("  %-15s rail absent" % label)
                        continue
                    entries = _entries(rail)
                    seen[label] = {"present": True, "entries": entries}
                    print("  %-15s rail present, %d entries, rect=%s"
                          % (label, len(entries), M.rect_of(rail)))
    finally:
        T.close_app()

    good = True

    def expect(label, ok, detail=""):
        nonlocal good
        print("  %-5s %s%s" % ("OK" if ok else "FAIL", label,
                               "" if ok else "  " + str(detail)[:150]))
        good &= ok

    print("\n--- checks ---")

    # The video library has no alphabet, so it must have no rail, in either view.
    for label in ("videos all", "videos folders"):
        expect("%s has no alphabet rail" % label,
               (seen.get(label) or {}).get("present") is False, seen.get(label))

    # The music library's grouped tabs do have one, and the flat Songs tab does not.
    for label in ("music albums", "music artists"):
        expect("%s has an alphabet rail" % label,
               (seen.get(label) or {}).get("present") is True, seen.get(label))
    expect("music songs has no alphabet rail (it is not grouped)",
           (seen.get("music songs") or {}).get("present") is False,
           seen.get("music songs"))

    # The letters themselves: one each, no overlap.
    for label in ("music albums", "music artists"):
        s = seen.get(label) or {}
        entries = s.get("entries") or []
        if not entries:
            expect("%s's rail carries letters" % label, False, s)
            continue
        expect("%s's rail carries letters" % label, len(entries) >= 20, len(entries))

        bad = [n for n, _ in entries if not ONE_LETTER.match(n)]
        expect("%s: every entry is a single letter, not a label" % label, not bad, bad[:6])

        overlaps = []
        for i in range(1, len(entries)):
            if entries[i][1][1] < entries[i - 1][1][3] - OVERLAP_TOLERANCE_PX:
                overlaps.append((entries[i - 1][0], entries[i][0],
                                 entries[i - 1][1], entries[i][1]))
        expect("%s: no two letters overlap" % label, not overlaps, overlaps[:3])

    print("\nVERDICT: %s -- alphabet rail scope and contents"
          % ("PASS" if good else "FAIL"))
    return 0 if good else 1


class _nullctx:
    def __enter__(self):
        return None

    def __exit__(self, *a):
        return False


if __name__ == "__main__":
    sys.exit(main())
