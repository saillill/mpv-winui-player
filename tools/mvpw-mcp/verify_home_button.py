"""Does the home page's 打开文件 button sit on the same row as 添加文件夹?

The user's report: 首页 "打开文件" 按钮位置太靠上，应与音乐库 "添加文件夹" 按钮处于相同位置.

One heading slot, three sections. The same control (`VideoAddFolderButton`) carries the
action on Home and on both libraries, and its words change with the section. So the whole
claim reduces to four measurable facts:

  1. the three sections put the button at the SAME top edge
  2. that top edge is on the heading row, not on the band above the library
  3. Home says 打开文件 and the libraries say 添加文件夹
  4. the old band-above-the-library button is gone

Before the fix, measured by UI Automation rectangles:

    home    heading top=479   ShellOpenFileButton top=404   <- 75 px ABOVE the heading
    music   heading top=407   VideoAddFolderButton top=445  <- on the heading row

Home had a different control on a different row, which is what "太靠上" describes.

Run:
    python tools/mvpw-mcp/verify_home_button.py
    python tools/mvpw-mcp/verify_home_button.py --self-test
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

# The section keys are the constants in MpvPlayerPage_Library.cs (lowercase), and MainWindow
# sets "ShellNav_" + section.Key. Getting the case wrong makes every lookup fail, which reads
# exactly like a broken app and is only a broken probe -- measured, first run.
NAV = ("home", "music", "videos")

BUTTON_ID = "VideoAddFolderButton"
HEADING_ID = "LibrarySectionTitle"
# The old home-only control. It must not come back: two open commands on one screen is a
# duplicate this app already removed once.
LEGACY_ID = "ShellOpenFileButton"

SAME_ROW_PX = 3


def _find(aid):
    try:
        c = auto.Control(AutomationId=aid)
        return c if c.Exists(1.5, 0.2) else None
    except Exception:
        return None


def _label(control) -> str:
    try:
        return control.Name or ""
    except Exception:
        return ""


def main() -> int:
    if "--self-test" in sys.argv:
        class Box:
            def __init__(self, l, t, r, b):
                self.BoundingRectangle = type("R", (), {"left": l, "top": t,
                                                        "right": r, "bottom": b})()
                self.Name = self.AutomationId = "probe"
                self.ControlTypeName = "ButtonControl"

        failures = []
        if M.assert_alignment(Box(10, 10, 100, 40), Box(50, 10, 140, 40))["ok"]:
            failures.append("a 40 px offset compared as aligned")
        if not M.assert_alignment(Box(10, 10, 100, 40), Box(10, 10, 100, 40))["ok"]:
            failures.append("identical rectangles compared as misaligned")
        good = not failures
        print("SELF-TEST %s: the row comparison can report a mismatch"
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
        capture_output=True, text=True).stdout.strip()
    try:
        hwnd = int(out) if out else 0
    except ValueError:
        hwnd = 0

    seen = {}
    try:
        ctx = BW.OnScreen(hwnd, settle=4.0) if hwnd else _nullctx()
        with ctx:
            for section in NAV:
                try:
                    T.invoke_control("ShellNav_" + section, "click")
                except Exception as e:
                    print("  FAIL  could not switch to %s: %s" % (section, e))
                    seen[section] = None
                    continue
                time.sleep(1.6)
                btn = _find(BUTTON_ID)
                head = _find(HEADING_ID)
                legacy = _find(LEGACY_ID)
                seen[section] = {
                    "button": M.rect_of(btn),
                    "label": _label(btn),
                    "heading": M.rect_of(head),
                    "legacy": M.rect_of(legacy),
                }
                s = seen[section]
                print("  %-7s button=%-32s label=%-10s heading=%s"
                      % (section, s["button"], s["label"], s["heading"]))
    finally:
        T.close_app()

    good = True

    def expect(label, ok, detail=""):
        nonlocal good
        print("  %-5s %s%s" % ("OK" if ok else "FAIL", label,
                               "" if ok else "  " + str(detail)[:140]))
        good &= ok

    print("\n--- checks ---")
    for section in NAV:
        s = seen.get(section) or {}
        expect("%s shows the heading button" % section, s.get("button") is not None, s)

    have_all = all((seen.get(s) or {}).get("button") for s in NAV)
    if have_all:
        tops = {s: seen[s]["button"][1] for s in NAV}
        spread = max(tops.values()) - min(tops.values())
        print("        tops: %s  (spread %d px)" % (tops, spread))
        expect("all three sections put the button at the same top edge",
               spread <= SAME_ROW_PX)

        heading = seen["home"]["heading"]
        if heading:
            on_row = heading[1] <= tops["home"] <= heading[3]
            print("        home heading=%s button top=%d" % (heading, tops["home"]))
            expect("the home button is on the heading row, not above it", on_row)

    expect("home says 打开文件", "打开" in (seen.get("home") or {}).get("label", ""),
           (seen.get("home") or {}).get("label"))
    for lib in ("music", "videos"):
        expect("%s still says 添加文件夹" % lib,
               "添加" in (seen.get(lib) or {}).get("label", ""),
               (seen.get(lib) or {}).get("label"))

    expect("the old band-above-the-library button is gone",
           all((seen.get(s) or {}).get("legacy") is None for s in NAV))

    print("\nVERDICT: %s -- 首页 打开文件 button placement" % ("PASS" if good else "FAIL"))
    return 0 if good else 1


class _nullctx:
    def __enter__(self):
        return None

    def __exit__(self, *a):
        return False


if __name__ == "__main__":
    sys.exit(main())
