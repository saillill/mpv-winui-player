"""Verify that a video actually plays when the APP opens it.

Why this file exists
--------------------
The old gate reported 8/8 PASS while the app could not play a video through any
user-facing path. The cause was not a wrong assertion: five of fifteen verifiers needed
playback, and all five sent `loadfile` over the IPC pipe themselves. That exercises mpv
and nothing else -- the app's own open path (`PlayLibraryEntry` -> `_mediaPlayer.OpenAsync`
-> `MpvPlayerExtensions.Open` -> `MpvPlayer::Command`) never ran at all, so a total
failure there was invisible.

This verifier therefore never sends loadfile. It launches the app WITH a file argument,
which is the same public entry point a double-click uses, and then asks four independent
questions. Each one fails on its own, so a partial breakage cannot hide behind a passing
aggregate.

  1. did the app's own open path run          (log: "open requested" / "loadfile sent")
  2. did mpv finish loading                   (log: "file-loaded", NOT sent by this script)
  3. did mpv report a non-empty video surface (log: "surface sized WxH", W,H > 0)
  4. is the picture actually moving on screen (pixels, not an opinion)

(4) is the one the old suite could not ask. `time-pos` advancing proves mpv's clock, not
that a frame reached the compositor; a wedged vo can keep the clock running. So the last
step grabs the window four times and requires real frame-to-frame change.

Exit code 0 only when all four hold. Each check prints its own PASS/FAIL so a failure
names the link that broke.

Run:
    python tools/mvpw-mcp/verify_playback.py
    python tools/mvpw-mcp/verify_playback.py --self-test   # prove it can fail
"""
from __future__ import annotations

import argparse
import ctypes
import glob
import os
import re
import subprocess
import sys
import time
from ctypes import wintypes

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

DEPLOY_DIR = r"C:\Users\z2637\Documents\DeepSeek\mpv-deploy"
EXE = os.path.join(DEPLOY_DIR, "mpvw.exe")
LOGDIR = os.path.join(os.environ["LOCALAPPDATA"], "mpv-winui", "mpv-winui", "logs")
CLIP = r"C:\Users\z2637\AppData\Local\Temp\probe-clip.mp4"

user32 = ctypes.WinDLL("user32", use_last_error=True)
user32.FindWindowW.restype = wintypes.HWND
user32.GetWindowRect.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.RECT)]

# The window has to be on screen for ImageGrab to see real pixels, but it must NOT be
# activated: check-no-focus-steal.py rejects SetForegroundWindow in probe code, and it was
# right to -- a verifier that steals focus changes what it is measuring and interrupts
# whoever is using the machine. background_window.OnScreen moves it with SWP_NOACTIVATE
# and restores the exact rectangle, and foreground_guard() fails the run if the foreground
# window changes anyway.
import background_window as BW


# ---------------------------------------------------------------------------
# process control


def _ps(cmd: str) -> str:
    return subprocess.run(["pwsh", "-NoProfile", "-Command", cmd],
                          capture_output=True, text=True).stdout or ""


def _running() -> int:
    out = _ps("(Get-Process mpvw -ErrorAction SilentlyContinue | Measure-Object).Count")
    try:
        return int(out.strip() or 0)
    except ValueError:
        return 0


def close_app(timeout: float = 12.0) -> bool:
    """WM_CLOSE first, force only if the window ignores it.

    Force-killing loses mpv's log buffer, which is the whole measurement here.
    """
    if not _running():
        return True
    _ps("Get-Process mpvw -ErrorAction SilentlyContinue | ForEach-Object { $_.CloseMainWindow() | Out-Null }")
    deadline = time.time() + timeout
    while time.time() < deadline:
        if not _running():
            return True
        time.sleep(0.25)
    _ps("Get-Process mpvw -ErrorAction SilentlyContinue | Stop-Process -Force")
    time.sleep(0.5)
    return not _running()


def current_log() -> str:
    os.makedirs(LOGDIR, exist_ok=True)
    files = sorted(glob.glob(os.path.join(LOGDIR, "mpv-winui.*.log.txt")))
    if not files:
        # The app names the file after the date; create-then-read would race the
        # app, so just fail loudly rather than inventing a path.
        raise SystemExit("no app log in %s -- has the app ever run?" % LOGDIR)
    return files[-1]


# ---------------------------------------------------------------------------
# window pixels


def _find_window() -> int:
    for cls in ("WinUIDesktopWin32WindowClass", "mpvw"):
        hwnd = user32.FindWindowW(cls, None)
        if hwnd:
            return hwnd
    out = _ps("(Get-Process mpvw -ErrorAction SilentlyContinue | "
              "Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1)"
              ".MainWindowHandle")
    try:
        return int(out.strip())
    except ValueError:
        return 0


def _grab(hwnd: int):
    from PIL import ImageGrab

    rc = wintypes.RECT()
    if not user32.GetWindowRect(hwnd, ctypes.byref(rc)):
        return None
    box = (rc.left, rc.top, rc.right, rc.bottom)
    if box[2] - box[0] < 80 or box[3] - box[1] < 80:
        return None
    return ImageGrab.grab(bbox=box, all_screens=True)


def frame_change(shots) -> float:
    """Largest percentage of pixels that differ between consecutive frames."""
    best = 0.0
    for i in range(1, len(shots)):
        a = shots[i - 1].convert("RGB").resize((64, 36))
        b = shots[i].convert("RGB").resize((64, 36))
        pa, pb = list(a.getdata()), list(b.getdata())
        changed = sum(1 for x, y in zip(pa, pb)
                      if abs(x[0] - y[0]) + abs(x[1] - y[1]) + abs(x[2] - y[2]) > 24)
        best = max(best, 100.0 * changed / len(pa))
    return best


# ---------------------------------------------------------------------------
# the checks


def parse(log_text: str) -> dict:
    """Pull the four facts out of one launch's log slice."""
    out = {
        "open_requested": len(re.findall(r"play-chain: open requested", log_text)),
        "loadfile_sent": len(re.findall(r"play-chain: loadfile sent", log_text)),
        "file_loaded": len(re.findall(r"play-chain: file-loaded", log_text)),
        "surface": [],
    }
    for m in re.finditer(r"play-chain: surface sized (\d+)x(\d+)", log_text):
        out["surface"].append((int(m.group(1)), int(m.group(2))))
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--self-test", action="store_true",
                    help="prove this verifier fails when playback is broken")
    ap.add_argument("--clip", default=CLIP)
    ap.add_argument("--settle", type=float, default=9.0,
                    help="seconds to let the app reach its steady state")
    args = ap.parse_args()

    if not os.path.exists(args.clip):
        print("FAIL: clip missing: %s" % args.clip)
        return 2
    if not os.path.exists(EXE):
        print("FAIL: %s not found -- run tools/deploy-test.ps1 -Deploy" % EXE)
        return 2

    close_app()

    if args.self_test:
        # The self-test is the point of the exercise: a verifier that has never been
        # seen to fail has an unknown blind spot. This launches the app with NO file,
        # so the app's open path must not run and every check must report FAIL.
        print("SELF-TEST: launching without a file; every check must FAIL.\n")
        launch_args = [EXE]
    else:
        launch_args = [EXE, args.clip]

    logfile = current_log()
    offset = os.path.getsize(logfile)

    print("launching: %s" % " ".join(os.path.basename(a) for a in launch_args))
    proc = subprocess.Popen(launch_args, cwd=DEPLOY_DIR)

    shots = []
    hwnd = 0
    deadline = time.time() + args.settle
    while time.time() < deadline:
        time.sleep(0.5)
        if not hwnd:
            hwnd = _find_window()

    if hwnd:
        # OnScreen parks it visibly without activating it, and restores the rectangle on
        # exit. foreground_guard() is the assertion that this stayed true.
        with BW.foreground_guard():
            with BW.OnScreen(hwnd, settle=4.0):
                for _ in range(4):
                    img = _grab(hwnd)
                    if img is not None:
                        shots.append(img)
                    time.sleep(1.1)

    close_app()

    with open(logfile, "rb") as f:
        f.seek(offset)
        text = f.read().decode("utf-8", "replace")

    facts = parse(text)
    change = frame_change(shots) if len(shots) >= 2 else 0.0

    print("\n--- measurements ---")
    print("  open requested (app's own path): %d" % facts["open_requested"])
    print("  loadfile sent  (app's own path): %d" % facts["loadfile_sent"])
    print("  file-loaded    (raised by mpv) : %d" % facts["file_loaded"])
    print("  surface sizes reported         : %s" % (facts["surface"] or "none"))
    print("  frames captured                : %d" % len(shots))
    print("  max frame-to-frame change      : %.1f%%" % change)

    print("\n--- checks ---")
    if args.self_test:
        # Every predicate here is the NEGATIVE of the real one: with no file argument
        # the app must do nothing at all. Kept separate from the normal path on purpose
        # -- an earlier version routed both through one helper that inverted the
        # predicate, so the self-test inverted it twice and reported FAILED while the
        # verifier was actually working. Two code paths is the honest shape.
        good = True
        passed_self = []
        passed_self.append(("- the app's open path did NOT run", facts["open_requested"] == 0))
        passed_self.append(("- mpv did NOT report file-loaded", facts["file_loaded"] == 0))
        passed_self.append(("- the picture did NOT move", change <= 8.0))
        for name, ok in passed_self:
            print("  %-5s %s" % ("OK" if ok else "FAIL", name))
            good &= ok
        print("\nSELF-TEST %s: the verifier detects a broken playback path."
              % ("PASSED" if good else "FAILED"))
        return 0 if good else 1

    good = True
    checks = [
        ("the app's own open path ran (PlayLibraryEntry -> OpenAsync)",
         facts["open_requested"] >= 1 and facts["loadfile_sent"] >= 1),
        ("mpv finished loading the file (file-loaded raised)", facts["file_loaded"] >= 1),
        ("mpv reported a non-empty video surface",
         any(w > 0 and h > 0 for w, h in facts["surface"])),
        ("the picture is actually moving on screen (> 8% pixels change)", change > 8.0),
    ]
    for name, ok in checks:
        print("  %-5s %s" % ("OK" if ok else "FAIL", name))
        good &= ok

    # The line tools/ui-repair/gate.py parses, and it must be exact. The gate refuses any
    # verifier that does not print it, because "the last output line contained OK" is
    # precisely how the previous gate reported 8/8 PASS while the app could not play.
    print("\nVERDICT: %s -- playback through the app's own open path"
          % ("PASS" if good else "FAIL"))
    return 0 if good else 1


if __name__ == "__main__":
    sys.exit(main())
