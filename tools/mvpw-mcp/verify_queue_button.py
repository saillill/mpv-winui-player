"""The play queue carries its own open-file action, in the top right.

Reported: 播放队列也应有右上角"添加文件"按钮.

Measured on the deployed build before the change:

    QueueClearButton   x=936   both against the LEFT gutter
    QueueAddToButton   x=1087  nothing on the right

After:

    QueueClearButton    x=892
    QueueAddToButton    x=1043
    QueueOpenFileButton x=2890  <- at the right edge, same row

The check is alignment, not existence: a button that merely exists somewhere would satisfy
"there is a button" while missing the whole point of the report. It has to share a row with the
other queue actions AND sit near the window's right edge, which is where every other section
puts its action (the library's 添加文件夹 measures x=2844 in a 1400-wide window).

Run:
    python tools/mvpw-mcp/verify_queue_button.py
    python tools/mvpw-mcp/verify_queue_button.py --self-test
"""
from __future__ import annotations

import os
import subprocess
import sys
import time

import uiautomation as auto

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import background_window as BW  # noqa: E402
import mpvw_tools as T  # noqa: E402
import ui_measure as M  # noqa: E402

QUEUE_ACTIONS = ("QueueClearButton", "QueueAddToButton", "QueueOpenFileButton")

# How close to the window's right edge counts as "the top right". 200 px covers the page
# gutter plus the button's own padding without being so loose that a centred button passes.
RIGHT_EDGE_INSET_PX = 200

# Two controls share a row when their vertical centres are within half a button height.
SAME_ROW_PX = 30


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


def _find(aid: str):
    """Walk the tree for one control, by the id the page sets on it."""
    stack = [auto.GetRootControl()]
    while stack:
        node = stack.pop()
        try:
            if (node.AutomationId or "") == aid:
                return node
            stack.extend(node.GetChildren())
        except Exception:
            continue
    return None


def main() -> int:
    if "--self-test" in sys.argv:
        # The alignment rules must reject the layout that was reported.
        failures = []
        window = (0, 0, 3000, 2000)

        # A left-gutter button must NOT read as "top right".
        left = (936, 485, 1073, 543)
        if M.is_near_right_edge(left, window, RIGHT_EDGE_INSET_PX):
            failures.append("a button at x=936 in a 3000-wide window passed as right-aligned")

        # The measured right-aligned button must pass.
        right = (2890, 485, 3076, 543)
        if not M.is_near_right_edge(right, window, RIGHT_EDGE_INSET_PX):
            failures.append("the measured right-aligned button failed the edge test")

        # Row comparison: 485 and 485 are the same row, 485 and 600 are not.
        if not M.same_row(left, right, SAME_ROW_PX):
            failures.append("two buttons at y=485 were not detected as sharing a row")
        if M.same_row(left, (936, 600, 1073, 658), SAME_ROW_PX):
            failures.append("buttons 115 px apart vertically were treated as one row")

        good = not failures
        print("SELF-TEST %s: the alignment rules reject a left-gutter button"
              % ("PASSED" if good else "FAILED"))
        for f in failures:
            print("  " + f)
        return 0 if good else 1

    T.close_app()
    if not T.launch_app().get("pid"):
        print("FAIL: the app did not launch")
        print("VERDICT: FAIL -- nothing measured")
        return 1

    hwnd = _hwnd()
    rects: dict[str, tuple[int, int, int, int] | None] = {}
    window = None
    try:
        ctx = BW.OnScreen(hwnd, settle=4.0) if hwnd else _nullctx()
        with ctx:
            window = BW.get_rect(hwnd) if hwnd else None
            T.invoke_control("ShellNav_queue", "click")
            time.sleep(3.0)
            for aid in QUEUE_ACTIONS:
                node = _find(aid)
                rects[aid] = M.rect_of(node) if node is not None else None
                print("  %-20s %s" % (aid, rects[aid]))
    finally:
        T.close_app()

    good = True

    def expect(label, ok, detail=""):
        nonlocal good
        print("  %-5s %s%s" % ("OK" if ok else "FAIL", label,
                               "" if ok else "  " + str(detail)[:150]))
        good &= ok

    print("\n--- checks ---")
    for aid in QUEUE_ACTIONS:
        expect("%s is present in the queue" % aid, rects.get(aid) is not None, rects.get(aid))

    open_rect = rects.get("QueueOpenFileButton")
    clear_rect = rects.get("QueueClearButton")

    expect("the open-file action sits near the window's right edge",
           open_rect is not None and window is not None
           and M.is_near_right_edge(open_rect, window, RIGHT_EDGE_INSET_PX),
           {"open": open_rect, "window": window})

    expect("the open-file action shares a row with the other queue actions",
           open_rect is not None and clear_rect is not None
           and M.same_row(open_rect, clear_rect, SAME_ROW_PX),
           {"open": open_rect, "clear": clear_rect})

    expect("the open-file action is to the RIGHT of them, not merely present",
           open_rect is not None and clear_rect is not None
           and open_rect[0] > clear_rect[2],
           {"open_x": open_rect[0] if open_rect else None,
            "clear_right": clear_rect[2] if clear_rect else None})

    print("\nVERDICT: %s -- the play queue's top-right open-file button"
          % ("PASS" if good else "FAIL"))
    return 0 if good else 1


class _nullctx:
    def __enter__(self):
        return None

    def __exit__(self, *a):
        return False


if __name__ == "__main__":
    sys.exit(main())
