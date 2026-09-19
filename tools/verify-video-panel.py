"""Click the 视频 category in the settings window and report the state of the
output-chain rows (vo / hwdec / scale) plus any second-level cards.

Why a separate script: the page opens on 桌面, so the video rows are not in the
tree until the category is selected. Selecting it also exercises the exact path
the user reported as broken.

Reports, for the video panel:
  * every visible row label
  * any second-level (collapsed section) cards
  * IsEnabled per row -- a disabled row still paints, so a screenshot cannot
    distinguish "shows a value" from "is editable"

Usage:
    PY="C:/Users/z2637/.workbuddy/binaries/python/envs/default/Scripts/python.exe"
    "$PY" tools/verify-video-panel.py
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
TREE = os.path.join(os.environ["TEMP"], "wb-video-tree.txt")
OUT = os.path.join(os.environ["TEMP"], "wb-video-panel.txt")
WINUI_CLASS = "WinUIDesktopWin32WindowClass"

lines = []


def log(m):
    lines.append(m)
    print(m, flush=True)


def kill_and_wait(timeout=15.0):
    deadline = time.time() + timeout
    while time.time() < deadline:
        still = [p for p in psutil.process_iter(["pid", "name"])
                 if (p.info["name"] or "").lower() == "mpvw.exe"]
        if not still:
            return
        for p in still:
            try:
                p.kill()
            except (psutil.NoSuchProcess, psutil.AccessDenied):
                pass
        time.sleep(0.5)


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
                out.append((h, win32gui.GetClassName(h), t))
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
    for h, cls, title in windows_of(proc.pid):
        if cls == WINUI_CLASS and title:
            main = h
            break
    if main:
        break
    time.sleep(0.5)
if not main:
    log("FAIL: no main window")
    sys.exit(1)
time.sleep(3)

import uiautomation as auto  # noqa: E402

win = auto.ControlFromHandle(main)
win.MenuItemControl(Name="工具", searchDepth=6).GetInvokePattern().Invoke()
time.sleep(1.5)
auto.GetRootControl().MenuItemControl(Name="选项", searchDepth=12).GetInvokePattern().Invoke()

settings = None
for _ in range(16):
    time.sleep(1.0)
    if not [p for p in psutil.process_iter(["pid", "name"])
            if (p.info["name"] or "").lower() == "mpvw.exe"]:
        log("!! CRASHED")
        sys.exit(1)
    cand = [w for w in windows_of(proc.pid) if w[0] != main]
    if cand:
        settings = cand[0][0]
        break
if not settings:
    log("FAIL: no settings window")
    sys.exit(1)
time.sleep(4)

sroot = auto.ControlFromHandle(settings)


def walk(root, maxdepth=14):
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


# --- find and click the 视频 category ---------------------------------------
video = None
for e, d in walk(sroot):
    try:
        if e.ControlTypeName == "ListItemControl" and (e.Name or "") == "视频":
            r = e.BoundingRectangle
            # Only the sidebar instance has a real rect; the content-side
            # duplicates collapse to 0,0,0,0.
            if r.width() > 1 and r.left < sroot.BoundingRectangle.left + 600:
                video = e
                break
    except Exception:
        pass

if video is None:
    log("FAIL: 视频 category node not found")
    sys.exit(2)

log(f"clicking 视频 at {video.BoundingRectangle}")
video.Click(simulateMove=False)
time.sleep(3)

# --- dump the now-visible panel ---------------------------------------------
rows = []
cards = []
for e, d in walk(sroot):
    try:
        ct = e.ControlTypeName or ""
        nm = (e.Name or "").strip()
        r = e.BoundingRectangle
        if r.width() <= 1 or r.height() <= 1:
            continue
        entry = f"{ct} name=[{nm}] enabled={e.IsEnabled}"
        if ct == "ListItemControl" and nm and not nm.startswith("mpv_winui."):
            rows.append(entry)
        elif nm and ct in ("TextControl", "ButtonControl", "ExpanderControl"):
            cards.append(entry)
    except Exception:
        pass

with open(TREE, "w", encoding="utf-8") as fh:
    fh.write("\n".join(rows + ["", "--- text/buttons ---"] + cards))

log(f"visible row ListItems: {len(rows)}")
for r in rows[:60]:
    log("  " + r)

log("")
disabled = [r for r in rows if "enabled=False" in r]
log(f"disabled rows: {len(disabled)}")
for r in disabled[:20]:
    log("  DISABLED " + r)

log("")
HOT = ("vo", "hwdec", "scale", "d3d11", "video-sync", "interpolation")
found = {k: [r for r in rows if f"name=[{k}]" in r or f"name=[{k} " in r] for k in HOT}
for k in HOT:
    log(f"{k!r}: {len(found[k])} -> {found[k][:2]}")

log("")
open(OUT, "w", encoding="utf-8").write("\n".join(lines) + "\n")
log(f"report -> {OUT}")
log(f"tree  -> {TREE}")
kill_and_wait()
