"""The video library's folder view: a cover per folder, and its path in large type.

Two queued defects about one surface:

    视频库: 应显示大路径 + 分栏文件夹图片样式，而非把视频全部展开
    视频库: 不需要右侧的字母排序栏        (covered by verify_letter_rail.py)

Measured on the deployed build before the work:

    folder view automation tree: 0 ImageControl elements
    header: 'Hades' h=33 and 'C:\\...\\Hades' h=33 on the SAME row

After:

    folder cover   stddev 49.5, 2213 distinct colours, 112x63 at 1.75x scale
    folder name    h=33
    folder path    h=47, its own row

Two checks, because they can fail independently: a cover that renders as a flat fill is no
better than no cover, and a path that is present but tiny is the original complaint.

Run:
    python tools/mvpw-mcp/verify_video_folder_cover.py
    python tools/mvpw-mcp/verify_video_folder_cover.py --self-test
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

# The cover is declared 64x36 in XAML. This machine reports UIA rectangles at 1.75x, so it lands
# near 112x63. The band is deliberately loose enough for other DPI settings and tight enough to
# exclude a video card (which is several hundred px wide).
COVER_MIN_W, COVER_MAX_W = 80, 180
COVER_MIN_H, COVER_MAX_H = 30, 120

MIN_STDDEV = 12.0

# The path must be TALLER than the name above it -- promoting it was the point. 6 px is about a
# font-size step at these sizes and cannot be a rounding difference.
MIN_PATH_DOMINANCE_PX = 6


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


def _walk(predicate) -> list:
    out = []
    stack = [auto.GetRootControl()]
    while stack:
        node = stack.pop()
        try:
            children = node.GetChildren()
        except Exception:
            continue
        try:
            if predicate(node):
                out.append(node)
        except Exception:
            pass
        stack.extend(children)
    return out


def _is_folder_name(node, paths) -> bool:
    """A text run that sits directly above the first path and is short enough to be a name.

    Identified by position rather than by value: the header is a name over a path, and what the
    name says depends on which folders this machine has.
    """
    if not paths:
        return False
    text = (node.Name or "").strip()
    if not text or text[:2] in ("C:", "D:", "E:"):
        return False

    name_rect = M.rect_of(node)
    path_rect = M.rect_of(paths[0])
    if name_rect is None or path_rect is None:
        return False

    # A real text row, not a container that happens to wrap one. Measured: a 244 px tall node
    # passed the position test because it spanned the whole header, and then "the path is larger
    # than the name" compared 47 against 244 and failed for the wrong reason.
    name_height = name_rect[3] - name_rect[1]
    if not (8 <= name_height <= 60):
        return False

    # Above the path, horizontally overlapping it, and on the immediately preceding line.
    return (name_rect[3] <= path_rect[1] + 4
            and path_rect[1] - name_rect[3] < 40
            and name_rect[0] < path_rect[2]
            and name_rect[2] > path_rect[0])


def main() -> int:
    if "--self-test" in sys.argv:
        # The size band and the dominance rule, against real measured numbers.
        failures = []

        if not (COVER_MIN_W <= 112 <= COVER_MAX_W and COVER_MIN_H <= 63 <= COVER_MAX_H):
            failures.append("the measured 112x63 cover falls outside the band")
        # A video card is several hundred px wide and must not be mistaken for a cover.
        if COVER_MIN_W <= 443 <= COVER_MAX_W:
            failures.append("a 443 px video card would pass as a folder cover")
        # A 20 px glyph is what the header used to show and must not count either.
        if COVER_MIN_W <= 20 <= COVER_MAX_W:
            failures.append("the old 20 px glyph would pass as a folder cover")

        # Path dominance: 47 vs 33 is promoted; 33 vs 33 (the old same-row layout) is not.
        if not (47 - 33 >= MIN_PATH_DOMINANCE_PX):
            failures.append("47 vs 33 was not detected as a promoted path")
        if 33 - 33 >= MIN_PATH_DOMINANCE_PX:
            failures.append("33 vs 33 was treated as promoted")

        good = not failures
        print("SELF-TEST %s: the cover band and the path rule reject the old layout"
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
    covers: list = []
    heights = {"path": 0, "name": 0}
    paths: list = []
    names: list = []
    try:
        ctx = BW.OnScreen(hwnd, settle=4.0) if hwnd else _nullctx()
        with ctx:
            T.invoke_control("ShellNav_videos", "click")
            time.sleep(2.0)
            T.invoke_control("VideoTabFolders", "click")
            # Thumbnails are fetched asynchronously after the group is realised.
            time.sleep(5.0)

            def _is_cover(node) -> bool:
                # Written as one straight line on purpose. The first version called rect_of
                # four times inside a chained expression, one of which compared `[2] is not
                # None`, and the element that this predicate selected then measured as
                # "not on screen" -- the predicate was returning a different wrapper than the
                # one it had sized. One call, one result.
                if node.ControlTypeName != "ImageControl":
                    return False
                rect = M.rect_of(node)
                if rect is None:
                    return False
                # On screen only. ItemsRepeater parks recycled elements at large negative
                # coordinates; measuring one screenshots empty desktop, which reads as a flat
                # fill and blames the product for the probe's placement.
                if rect[1] < 0 or rect[3] > 4000 or rect[0] < 0:
                    return False
                w = rect[2] - rect[0]
                h = rect[3] - rect[1]
                return COVER_MIN_W <= w <= COVER_MAX_W and COVER_MIN_H <= h <= COVER_MAX_H

            covers = _walk(_is_cover)

            # A path is a text run that looks like one, not merely any text.
            def _on_screen(node):
                r = M.rect_of(node)
                return r is not None and 0 <= r[1] and r[3] <= 4000

            paths = [n for n in _walk(lambda n: n.ControlTypeName == "TextControl"
                                      and (n.Name or "")[:2] in ("C:", "D:", "E:"))
                     if _on_screen(n)]
            paths.sort(key=lambda n: M.rect_of(n)[1])
            # Heights captured HERE, while the wrappers are alive. Reading BoundingRectangle
            # after the walk raised COMError "event cannot call any subscriber" -- the wrapper
            # had gone stale, which is the same failure that made the music-card verifier report
            # "not on screen" for an element it had just measured.
            if paths:
                r0 = M.rect_of(paths[0])
                heights["path"] = r0[3] - r0[1]
            # Any folder name, not the literal "Hades". Hard-coding this machine's folder made
            # the check fail on a tree with a different one, which says nothing about the
            # product. The name is the text on the row ABOVE the path, which is what the header
            # renders, whatever it happens to be called.
            names = [n for n in _walk(lambda n: n.ControlTypeName == "TextControl"
                                      and _is_folder_name(n, paths))
                     if _on_screen(n)]
            names.sort(key=lambda n: M.rect_of(n)[1])
            if names:
                r1 = M.rect_of(names[0])
                heights["name"] = r1[3] - r1[1]

            print("  folder covers found : %d" % len(covers))
            # Measure once here and keep the RESULT, not the control. A uiautomation wrapper
            # goes stale fast -- the identical element that measured stddev 49.5 inside this
            # loop reported "not on screen" when it was measured again after the loop, because
            # by then the wrapper no longer resolved. Caching numbers instead of elements is the
            # fix, and it is also what makes the failure readable.
            cover_results = []
            for c in covers[:3]:
                rect = M.rect_of(c)
                res = M.assert_image_content(c, min_stddev=MIN_STDDEV)
                cover_results.append((rect, res))
                print("    %-30s stddev=%-6.1f colours=%-5d"
                      % (rect, res.get("stddev", -1),
                         res.get("distinct_colours", -1)))
    finally:
        T.close_app()

    good = True

    def expect(label, ok, detail=""):
        nonlocal good
        print("  %-5s %s%s" % ("OK" if ok else "FAIL", label,
                               "" if ok else "  " + str(detail)[:170]))
        good &= ok

    print("\n--- checks ---")
    expect("the folder view renders at least one folder cover",
           len(covers) >= 1, len(covers))

    if cover_results:
        rect, res = cover_results[0]
        expect("the folder cover has image content, not a flat fill",
               res.get("ok") is True, {"rect": rect, "measured": res})

    expect("the folder path is rendered", len(paths) >= 1, len(paths))

    # Say so when the comparison cannot run, instead of quietly dropping the check. A skipped
    # assertion that looks like a passing one is how a verifier stops being evidence.
    expect("a folder name was found to compare the path against", bool(names),
           {"paths": len(paths), "names": len(names)})

    if heights["path"] and heights["name"]:
        expect("the path is larger than the folder name above it",
               heights["path"] - heights["name"] >= MIN_PATH_DOMINANCE_PX,
               heights)

    print("\nVERDICT: %s -- the folder view's cover and large path"
          % ("PASS" if good else "FAIL"))
    return 0 if good else 1


class _nullctx:
    def __enter__(self):
        return None

    def __exit__(self, *a):
        return False


if __name__ == "__main__":
    sys.exit(main())
