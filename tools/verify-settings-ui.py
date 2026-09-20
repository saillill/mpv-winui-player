"""Verify the settings window end to end: category count, and whether the
mpv.conf-backed rows are editable.

What this checks, and why each one is the right assertion
--------------------------------------------------------
1.  Category count, by counting the UIA names in the NavigationView, must be 9.
    The previous build had 10 because "advanced" still existed as a category.

2.  `vo` / `hwdec` / `scale` must NOT be disabled. This is the actual bug the
    user reported ("二级菜单里都显示不出来东西"). The page was never blank --
    all ~104 mpv.conf-backed rows were classified as user-owned and shown
    greyed-out. `IsEnabled` on the row is the signal that distinguishes
    "rendered" from "usable", and a screenshot alone cannot tell them apart
    because a disabled row still paints.

Writes a full UIA dump alongside the verdict so a failure is diagnosable
without re-running.

Usage:
    PY="C:/Users/z2637/.workbuddy/binaries/python/envs/default/Scripts/python.exe"
    "$PY" tools/verify-settings-ui.py
"""

import os
import subprocess
import sys
import time

import psutil
import win32gui
import win32process

DST = r"C:\Users\z2637\WorkBuddy\2026-09-19-14-59-27\outputs\mpvw-test"
EXE = os.path.join(DST, "mpvw.exe")
MEDIA = r"C:\Users\z2637\Videos\1.mp4"
OUT = os.path.join(os.environ["TEMP"], "wb-settings-ui.txt")
WINUI_CLASS = "WinUIDesktopWin32WindowClass"

lines = []


def log(m):
    lines.append(m)
    print(m, flush=True)


def mpvw_pids():
    return [p.info["pid"] for p in psutil.process_iter(["pid", "name"])
            if (p.info["name"] or "").lower() == "mpvw.exe"]


def kill_and_wait(timeout=15.0):
    """Poll until no mpvw.exe remains, so the single-instance key is free.

    A fixed sleep is not enough: the app registers the AppInstance key "main",
    and launching while a previous instance is still tearing down hands the new
    activation to it -- producing a healthy-looking "exited early" verdict.
    """
    deadline = time.time() + timeout
    while time.time() < deadline:
        still = [p for p in psutil.process_iter(["pid", "name"])
                 if (p.info["name"] or "").lower() == "mpvw.exe"]
        if not still:
            return True
        for p in still:
            try:
                p.kill()
            except (psutil.NoSuchProcess, psutil.AccessDenied):
                pass
        time.sleep(0.5)
    return False


def windows_of(pid):
    out = []

    def visit(h, _):
        try:
            if not win32gui.IsWindowVisible(h):
                return
            _, p = win32process.GetWindowThreadProcessId(h)
            if p != pid:
                return
            t = win32gui.GetWindowText(h)
            if t:
                out.append((h, win32gui.GetClassName(h), t, win32gui.GetWindowRect(h)))
        except Exception:
            pass

    win32gui.EnumWindows(visit, None)
    return out


kill_and_wait()
time.sleep(2)

proc = subprocess.Popen([EXE, MEDIA], cwd=DST)
log(f"launched pid={proc.pid}")

main = None
start = time.time()
while time.time() - start < 60:
    for h, cls, title, rect in windows_of(proc.pid):
        if cls == WINUI_CLASS and title:
            main = h
            break
    if main:
        break
    time.sleep(0.5)

if not main:
    log("FAIL: no main window")
    open(OUT, "w", encoding="utf-8").write("\n".join(lines) + "\n")
    sys.exit(1)

log(f"main hwnd={main}")
time.sleep(3)

import uiautomation as auto  # noqa: E402

win = auto.ControlFromHandle(main)
tools = win.MenuItemControl(Name="工具", searchDepth=6)
if not tools.Exists(3, 0.5):
    log("FAIL: 工具 not found")
    open(OUT, "w", encoding="utf-8").write("\n".join(lines) + "\n")
    sys.exit(2)
tools.GetInvokePattern().Invoke()
time.sleep(1.5)

opt = auto.GetRootControl().MenuItemControl(Name="选项", searchDepth=12)
if not opt.Exists(3, 0.5):
    log("FAIL: 选项 not found")
    open(OUT, "w", encoding="utf-8").write("\n".join(lines) + "\n")
    sys.exit(3)

opt.GetInvokePattern().Invoke()

settings = None
for i in range(1, 16):
    time.sleep(1.0)
    if not mpvw_pids():
        log("!! CRASHED opening settings")
        open(OUT, "w", encoding="utf-8").write("\n".join(lines) + "\n")
        sys.exit(1)
    cand = [w for w in windows_of(proc.pid) if w[0] != main]
    if cand:
        settings = cand[0][0]
        log(f"settings window at t=+{i}s: {cand[0][2]}")
        break

if not settings:
    log("FAIL: settings window never appeared")
    open(OUT, "w", encoding="utf-8").write("\n".join(lines) + "\n")
    sys.exit(1)

# Let the page finish building its tree before enumerating.
time.sleep(4)

sroot = auto.ControlFromHandle(settings)


def visible(e):
    r = e.BoundingRectangle
    return r.width() > 1 and r.height() > 1


def dump(e, depth=0, maxdepth=9, acc=None):
    if acc is None:
        acc = []
    if depth > maxdepth:
        return acc
    for c in e.GetChildren():
        try:
            nm = c.Name or ""
            ct = c.ControlTypeName
            r = c.BoundingRectangle
            acc.append(("  " * depth) + f"{ct} name=[{nm}] "
                       f"rect=({r.left},{r.top},{r.right},{r.bottom}) "
                       f"enabled={c.IsEnabled}")
            dump(c, depth + 1, maxdepth, acc)
        except Exception:
            pass
    return acc


def walk(root, maxdepth=14):
    """Depth-first (element, depth) generator, tolerant of transient UIA errors."""
    stack = [(root, 0)]
    while stack:
        e, d = stack.pop()
        if d > maxdepth:
            continue
        try:
            kids = e.GetChildren()
        except Exception:
            continue
        for c in kids:
            yield c, d
            stack.append((c, d + 1))


tree = dump(sroot)
with open(os.path.join(os.environ["TEMP"], "wb-settings-tree.txt"),
          "w", encoding="utf-8") as fh:
    fh.write("\n".join(tree))
log(f"tree lines: {len(tree)}")

failures = []

# --- Check 1: category count ------------------------------------------------
# Categories are ListItemControl nodes in the sidebar. Two traps here:
#
#   * Do NOT look for a control whose type name contains "Navigation" -- this
#     page builds its own sidebar and the tree shows plain ListItemControl.
#   * The content pane renders second-level section cards that are ALSO
#     ListItemControl, and they overlap the sidebar's x-range. They are told
#     apart by indent: sidebar icons start at the pane edge, cards are inset.
EXPECTED_CATEGORIES = {
    "桌面", "播放", "视频", "音频", "字幕", "窗口", "网络", "快捷键", "OSD",
}

sroot_rect = sroot.BoundingRectangle
sidebar_right = sroot_rect.left + 600
sidebar_left = sroot_rect.left

# Sidebar rows are the ones whose left edge sits closest to the pane edge; the
# content cards are pushed right by their card padding. Take the minimum left
# across all candidates as the sidebar's own inset, then keep rows within a
# small tolerance of it.
candidates = []
for e, _d in walk(sroot):
    try:
        if (e.ControlTypeName or "") != "ListItemControl":
            continue
        nm = (e.Name or "").strip()
        if not nm or nm.startswith("mpv_winui."):
            continue
        r = e.BoundingRectangle
        if r.width() <= 1 or r.height() <= 1:
            continue
        if not (sidebar_left <= r.left < sidebar_right):
            continue
        candidates.append((r.left, nm))
    except Exception:
        pass

nav_names = []
if candidates:
    inset = min(c[0] for c in candidates)
    for left, nm in candidates:
        if left - inset <= 12 and nm not in nav_names:
            nav_names.append(nm)

log(f"sidebar categories: {nav_names}")
log(f"category count: {len(nav_names)} (expect 9)")

if set(nav_names) != EXPECTED_CATEGORIES:
    failures.append(f"category set mismatch: got {sorted(nav_names)}, "
                    f"expected {sorted(EXPECTED_CATEGORIES)}")

for gone in ("高级", "Advanced"):
    if gone in nav_names:
        failures.append(f"category {gone!r} still present -- removal did not take effect")

# --- Check 2: mpv.conf-backed rows must be enabled ---------------------------
# The rows are labelled with localised names ("硬解模式" for hwdec, "画面" for the
# scale/vo chain), not raw mpv keys, so matching on the raw key finds nothing.
# The durable assertion is simpler and stronger: no row on the page may be
# disabled. A disabled row still paints, so a screenshot cannot tell "shows a
# value" from "is editable" -- IsEnabled is the only signal that separates them.
disabled = []
for e, _d in walk(sroot):
    try:
        if (e.ControlTypeName or "") != "ListItemControl":
            continue
        nm = (e.Name or "").strip()
        if not nm or nm.startswith("mpv_winui."):
            continue
        r = e.BoundingRectangle
        if r.width() <= 1 or r.height() <= 1:
            continue
        if not e.IsEnabled:
            disabled.append(nm)
    except Exception:
        pass

log(f"disabled option rows: {len(disabled)} -> {disabled[:15]}")
if disabled:
    failures.append(f"{len(disabled)} option row(s) are DISABLED, which is the "
                    f"reported bug: {disabled[:15]}")

log("")
if failures:
    for f in failures:
        log(f"FAILED: {f}")
else:
    log("ALL CHECKS PASSED")

open(OUT, "w", encoding="utf-8").write("\n".join(lines) + "\n")
log(f"report -> {OUT}")

kill_and_wait()
sys.exit(1 if failures else 0)
