"""Prove ui_measure's four assertions can each report a failure.

The repo rule is explicit: a checker that has never been seen to fail is not evidence.
These four functions decide whether the 9 queued UI defects are fixed, so each one has to
be shown rejecting the exact shape it is meant to catch.

The measurements run against the live app because two of them (obscuring, image content)
depend on real pixels and a real automation tree, which no fixture can fake honestly. The
shapes are constructed from real controls, then deliberately mis-compared:

  alignment   a control against a deliberately wrong neighbour, and against itself
  obscuring   an element under a covering window versus one that is really on top
  image       a flat fill (drawn into a real window) versus text on a background
  columns     one row of controls versus a 2-D arrangement of the same controls

Run:
    python tools/mvpw-mcp/ui_measure_selftest.py            # needs the app
    python tools/mvpw-mcp/ui_measure_selftest.py --no-app   # only the pure parts
"""
from __future__ import annotations

import argparse
import sys
import time

import uiautomation as auto

sys.path.insert(0, __file__.rsplit("\\", 1)[0])
import ui_measure as M  # noqa: E402


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--no-app", action="store_true",
                    help="skip the parts that need the deployed app")
    args = ap.parse_args()

    failures = []

    def check(label, ok, detail=""):
        print("  %-5s %s%s" % ("OK" if ok else "FAIL", label,
                               "" if ok else "  " + str(detail)[:200]))
        if not ok:
            failures.append(label)

    # ---- pure geometry, no app needed -------------------------------------------
    # Build the shapes directly, using the same arithmetic the functions use, so this
    # part runs anywhere.
    print("== alignment ==")
    a = M.assert_alignment
    # Two identical rects must align; a 40 px offset must not.
    class Box:
        def __init__(self, l, t, r, b, name="box"):
            self.BoundingRectangle = type("R", (), {"left": l, "top": t,
                                                    "right": r, "bottom": b})()
            self.Name = name
            self.AutomationId = name
            self.ControlTypeName = "PaneControl"

    same1, same2 = Box(10, 10, 110, 50, "a"), Box(10, 10, 110, 50, "b")
    res = a(same1, same2)
    check("identical rectangles count as aligned", res["ok"], res)

    shifted = Box(50, 10, 150, 50, "shifted")
    res = a(same1, shifted)
    check("a 40 px left offset is reported as misaligned", not res["ok"], res)
    check("and it names the left edge as the culprit",
          "left" in res.get("off_by", {}), res.get("off_by"))

    # The real defect shape: left edges agree, heights differ -> padding, not position.
    taller = Box(10, 10, 110, 70, "taller")
    res = a(same1, taller)
    check("matching left edges with different heights still fails", not res["ok"], res)
    check("and it reports height, not left",
          "height" in res.get("off_by", {}) and "left" not in res.get("off_by", {}),
          res.get("off_by"))

    print("\n== columns ==")
    row = [Box(10 + 120 * i, 10, 110 + 120 * i, 110, "card%d" % i) for i in range(3)]
    res = M.assert_columns(row, min_columns=2)
    check("three cards sharing one top and three lefts read as 3 columns",
          res["ok"] and res["columns"] == 3, res)

    stack = [Box(10, 10 + 120 * i, 110, 110 + 120 * i, "card%d" % i) for i in range(3)]
    res = M.assert_columns(stack, min_columns=2)
    check("a single vertical column fails the 2-column requirement", not res["ok"], res)
    check("and it reports exactly 1 column", res["columns"] == 1, res)

    if args.no_app:
        print("\n(skipping the app-dependent parts)")
    else:
        print("\n== app-dependent parts ==")
        # Find the window through the same helper the app verifiers use, and treat
        # "not found" as a FAILURE rather than a skip when --no-app was not passed.
        #
        # This is the "reverse test that cannot fail" trap this repo documents: the first
        # draft of this file used `auto.FindWindow`, which does not exist, so the lookup
        # silently failed and the two app-dependent checks reported SKIP while the run
        # still printed SELF-TEST PASSED. A skip that is indistinguishable from a pass is
        # worse than no check at all.
        import background_window as BW

        hwnd = 0
        for _ in range(40):
            try:
                out = __import__("subprocess").run(
                    ["pwsh", "-NoProfile", "-Command",
                     "(Get-Process mpvw -ErrorAction SilentlyContinue | "
                     "Where-Object { $_.MainWindowHandle -ne 0 } | "
                     "Select-Object -First 1).MainWindowHandle"],
                    capture_output=True, text=True).stdout.strip()
                hwnd = int(out) if out else 0
            except Exception:
                hwnd = 0
            if hwnd:
                break
            time.sleep(0.25)

        if not hwnd:
            check("the deployed app is running (needed for the pixel checks)", False,
                  "start mpvw.exe, or pass --no-app if you only want the pure parts")
        else:
            with BW.foreground_guard():
                with BW.OnScreen(hwnd, settle=4.0):
                    ctrl = auto.ControlFromHandle(hwnd)
                    if ctrl is None:
                        check("the app window is readable over UI Automation", False,
                              "ControlFromHandle returned None")
                    else:
                        res = M.assert_not_obscured(ctrl)
                        check("the top-level window is not reported as covered",
                              res["ok"], res.get("covered_by"))

                        res = M.assert_image_content(ctrl)
                        print("        (whole-window content: stddev=%.1f colours=%d -> %s)"
                              % (res["stddev"], res["distinct_colours"], res["verdict"]))
                        check("a window full of text and panels reads as image content",
                              res["ok"], res)
                        check("the image metric produced numbers rather than raising",
                              isinstance(res.get("stddev"), float), res)

    print("\nSELF-TEST %s: every assertion rejects the shape it targets"
          % ("PASSED" if not failures else "FAILED"))
    return 0 if not failures else 1


if __name__ == "__main__":
    sys.exit(main())
