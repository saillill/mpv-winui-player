"""Probe: can this harness observe the letter rail's hover wave at all?

**A probe, not a verifier: no VERDICT line.** Right now the honest answer is "no", and this file
records why, because the reason is a property of the harness and not of the feature.

What was built. `IndexRailLetter_PointerEntered` scales the hovered letter to 1.9x and dims its
neighbours along a cosine falloff, so the rail reads as a wave rather than as a list of static
letters. Reported as 悬浮放大并带波浪式渐变.

What could not be measured, and each attempt in order:

1. **UIA bounding rectangles.** Compare each letter's rect before and after hovering. Measured
   growth=0, then found the reason: `RenderTransform` is applied at render time and UI Automation
   reports the LAYOUT rect. A letter scaled to 1.9x still reports its 32 px slot. The instrument
   is blind to this change by construction.
2. **Rendered ink rows, whole rail.** Count rows containing non-background pixels. Measured 912
   at rest and 912 hovering -- saturated, because 27 letters always cover the column.
3. **Rendered inked pixels, local box.** Same saturation in the count itself: 1872 both times.
4. **Whether the handler runs at all.** Added a log line to `IndexRailLetter_PointerEntered` and
   drove the cursor with `SetCursorPos`. The line never appeared. That is the finding that
   matters: the pointer events are not being delivered, so nothing downstream can be observed.

Why pointer events are not delivered here. Every MCP probe in this repo runs under
`background_window.foreground_guard()`, which asserts the harness never takes the foreground --
that rule exists because stealing focus changes what is being measured. A window that is not
foreground does not receive hover input. So `SetCursorPos` moves the cursor over a window the
system is not routing pointer input to, `PointerEntered` never fires, and no amount of pixel
comparison can see a wave that was never triggered.

Options, and what each costs:

* **Foreground the app for this one check.** Directly contradicts `foreground_guard`, and the
  guard exists because a probe that takes focus measures a different window state. Would need
  the guard relaxed for this file only, with a reason, and someone to decide that is acceptable.
* **`SendInput` with mouse move while the window is foreground**, same objection.
* **Trust the code and stop here.** Defensible for a cosmetic hover effect, but it is exactly the
  "looks right, must work" reasoning that let a dead playback path pass 8/8 for weeks.
* **Add a UI-automation-visible signal.** If the wave also set an AutomationProperty (say
  HelpText to the current peak letter), a probe could assert it without needing hover input at
  all. That means changing the product to suit the test, which is worth doing only if this
  effect is important enough to keep a regression test for.

Recommendation, in one line: run this probe on a machine where the app can be foregrounded, by
hand, and look at the rail. The effect is either visible or it is not, and a human answers that
in two seconds -- which is faster than any of the four options above.

Run:
    python tools/mvpw-mcp/probe_rail_hover_wave.py
"""
from __future__ import annotations

import ctypes
import os
import subprocess
import sys
import time

import uiautomation as auto

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import background_window as BW  # noqa: E402
import mpvw_tools as T  # noqa: E402
import ui_measure as M  # noqa: E402

RAIL_ID = "LibraryIndexRail"


def _hwnd() -> int:
    for _ in range(20):
        time.sleep(0.5)
        out = subprocess.run(
            ["pwsh", "-NoProfile", "-Command",
             "(Get-Process mpvw -ErrorAction SilentlyContinue | "
             "Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1)"
             ".MainWindowHandle"],
            capture_output=True, text=True, encoding="utf-8", errors="replace").stdout.strip()
        if out:
            try:
                return int(out)
            except ValueError:
                return 0
    return 0


def _rail():
    stack = [auto.GetRootControl()]
    while stack:
        node = stack.pop()
        try:
            if (node.AutomationId or "") == RAIL_ID:
                return node
            stack.extend(node.GetChildren())
        except Exception:
            continue
    return None


def _ink(box) -> int | None:
    """Inked pixels inside a screen box, against that box's own most common value."""
    try:
        from collections import Counter

        from PIL import ImageGrab
    except Exception:
        return None
    try:
        img = ImageGrab.grab(bbox=box, all_screens=True).convert("L")
    except Exception:
        return None
    w, h = img.size
    if w <= 0 or h <= 0:
        return None
    px = img.load()
    counts = Counter(px[x, y] for y in range(0, h, 2) for x in range(0, w, 2))
    if not counts:
        return None
    background = counts.most_common(1)[0][0]
    return sum(1 for y in range(h) for x in range(w)
               if abs(px[x, y] - background) > 18)


def main() -> int:
    T.close_app()
    if not T.launch_app().get("pid"):
        print("could not launch the app")
        return 1

    hwnd = _hwnd()
    try:
        ctx = BW.OnScreen(hwnd, settle=4.0) if hwnd else _nullctx()
        with ctx:
            T.invoke_control("ShellNav_music", "click")
            time.sleep(1.4)
            T.invoke_control("MusicTabAlbums", "click")
            time.sleep(2.6)

            rail = _rail()
            if rail is None:
                print("the rail is not present, so there is nothing to hover")
                return 1

            letters = []
            for child in rail.GetChildren():
                rect = M.rect_of(child)
                if rect:
                    letters.append(((child.Name or "").strip(), rect))
            letters.sort(key=lambda p: p[1][1])
            print("rail entries: %d" % len(letters))
            if len(letters) < 4:
                print("too few letters to hover one")
                return 1

            name, rect = letters[3]
            box = (min(r[2] for _, r in letters) - 40,
                   rect[1] - 2 * (rect[3] - rect[1]),
                   max(r[2] for _, r in letters) + 40,
                   rect[3] + 2 * (rect[3] - rect[1]))

            at_rest = _ink(box)
            ctypes.windll.user32.SetCursorPos((rect[0] + rect[2]) // 2,
                                             (rect[1] + rect[3]) // 3)
            time.sleep(1.2)
            hovering = _ink(box)

            print("hovered letter      : %r at %s" % (name, rect))
            print("inked pixels at rest: %r" % at_rest)
            print("inked pixels hovered: %r" % hovering)
            if at_rest is None or hovering is None:
                print("\nCould not capture the rail, so this run says nothing either way.")
            elif hovering > at_rest:
                print("\nThe renderal changed while the cursor was over the rail. The wave is "
                      "visible on this machine.")
            else:
                print("\nNo change. This is the expected result under background_window's "
                      "guard: a window that is not foreground gets no hover input, so "
                      "PointerEntered never fires. See this file's docstring -- it does NOT "
                      "mean the wave is broken.")
    finally:
        T.close_app()

    return 0


class _nullctx:
    def __enter__(self):
        return None

    def __exit__(self, *a):
        return False


if __name__ == "__main__":
    sys.exit(main())
