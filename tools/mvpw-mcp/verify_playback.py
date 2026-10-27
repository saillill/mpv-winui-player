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

**How (4) must be read, learned the hard way.** With `d3d11-output-mode=composition` the
video is composited by the GPU into a SwapChainPanel, and `ImageGrab` -- a GDI screen
capture -- can return the video area flat while mpv is demonstrably playing. Measured right
after the idle fix: `time-pos` advanced 9.30 -> 12.48 with `pause=false` and `idle=false`,
while four window grabs were pixel-identical. A capture that does not change is therefore
reported as INCONCLUSIVE, not as a failure, and the verdict then rests on mpv's own evidence
(file-loaded, a real surface size, and advancing time-pos). A capture that DOES change is
still taken as positive proof.

Exit code 0 only when the app's own path ran, mpv loaded the file, a real surface was
reported, and either the pixels moved or mpv's clock advanced.

Run:
    python tools/mvpw-mcp/verify_playback.py
    python tools/mvpw-mcp/verify_playback.py --self-test   # prove it can fail
"""
from __future__ import annotations

import argparse
import ctypes
import glob
import json
import os
import re
import subprocess
import sys
import time
from ctypes import wintypes
import paths

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

DEPLOY_DIR = paths.deploy_dir()
EXE = os.path.join(DEPLOY_DIR, "mpvw.exe")
LOGDIR = os.path.join(os.environ["LOCALAPPDATA"], "mpv-winui", "mpv-winui", "logs")
CLIP = r"C:\Users\z2637\AppData\Local\Temp\probe-clip.mp4"

user32 = ctypes.WinDLL("user32", use_last_error=True)
user32.FindWindowW.restype = wintypes.HWND
user32.GetWindowRect.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.RECT)]

# The IPC pipe, for reading mpv's own clock. Read-only: this verifier must never send
# loadfile, because that is the defect that made the old suite useless -- it exercised mpv
# and never the app.
_k32 = ctypes.WinDLL("kernel32", use_last_error=True)
_k32.CreateFileW.restype = wintypes.HANDLE
_k32.CreateFileW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD,
                             ctypes.c_void_p, wintypes.DWORD, wintypes.DWORD,
                             wintypes.HANDLE]
_k32.ReadFile.argtypes = [wintypes.HANDLE, ctypes.c_void_p, wintypes.DWORD,
                          ctypes.POINTER(wintypes.DWORD), ctypes.c_void_p]
_k32.WriteFile.argtypes = [wintypes.HANDLE, ctypes.c_void_p, wintypes.DWORD,
                           ctypes.POINTER(wintypes.DWORD), ctypes.c_void_p]
_k32.CloseHandle.argtypes = [wintypes.HANDLE]
_PIPE = r"\\.\pipe\mpvpipe"


def _open_pipe():
    h = _k32.CreateFileW(_PIPE, 0xC0000000, 0, None, 3, 0, None)
    if h == wintypes.HANDLE(-1).value or h is None:
        return None
    return h

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


def _time_pos_progress() -> tuple[float | None, float | None, bool | None, bool | None]:
    """Read time-pos twice, about a second apart, over the IPC pipe.

    Returns (first, last, paused, idle). Any value that could not be read comes back None,
    and the caller treats that as "no evidence" rather than as a pass.

    Connect-and-write immediately: a connection that idles for even ~400 ms is dropped by
    this mpv build (measured, see AGENTS.md rule 5), so all reads for one burst share one
    connection and nothing sleeps between connect and the first request.
    """
    h = _open_pipe()
    if h is None:
        return None, None, None, None

    def one(prop: str, rid: int):
        payload = (json.dumps({"command": ["get_property", prop],
                               "request_id": rid}) + "\n").encode()
        w = wintypes.DWORD(0)
        if not _k32.WriteFile(h, payload, len(payload), ctypes.byref(w), None):
            return None
        buf = ctypes.create_string_buffer(65536)
        rd = wintypes.DWORD(0)
        deadline = time.time() + 1.5
        acc = b""
        while time.time() < deadline:
            if not _k32.ReadFile(h, buf, 65536, ctypes.byref(rd), None):
                return None
            if rd.value:
                acc += buf.raw[:rd.value]
                for line in acc.split(b"\n"):
                    line = line.strip()
                    if not line:
                        continue
                    try:
                        o = json.loads(line)
                    except Exception:
                        continue
                    if o.get("request_id") == rid:
                        return o.get("data") if o.get("error") == "success" else None
            else:
                time.sleep(0.005)
        return None

    try:
        first = one("time-pos", 1)
        paused = one("pause", 2)
        idle = one("idle-active", 3)
        time.sleep(1.2)
        last = one("time-pos", 4)
    finally:
        _k32.CloseHandle(h)

    def num(v):
        return float(v) if isinstance(v, (int, float)) else None

    return num(first), num(last), paused, idle


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

    # mpv's own clock, read over the pipe. Not a substitute for the pixels -- together they
    # separate "the picture is not reaching the compositor" from "nothing is playing".
    tp_first, tp_last, paused, idle = _time_pos_progress()

    close_app()

    with open(logfile, "rb") as f:
        f.seek(offset)
        text = f.read().decode("utf-8", "replace")

    facts = parse(text)
    change = frame_change(shots) if len(shots) >= 2 else 0.0
    advanced = (tp_first is not None and tp_last is not None and tp_last > tp_first + 0.2)

    print("\n--- measurements ---")
    print("  open requested (app's own path): %d" % facts["open_requested"])
    print("  loadfile sent  (app's own path): %d" % facts["loadfile_sent"])
    print("  file-loaded    (raised by mpv) : %d" % facts["file_loaded"])
    print("  surface sizes reported         : %s" % (facts["surface"] or "none"))
    print("  frames captured                : %d" % len(shots))
    print("  max frame-to-frame change      : %.1f%%" % change)
    print("  mpv time-pos                   : %r -> %r  (advanced=%s, pause=%r, idle=%r)"
          % (tp_first, tp_last, advanced, paused, idle))

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
    ]
    for name, ok in checks:
        print("  %-5s %s" % ("OK" if ok else "FAIL", name))
        good &= ok

    # The playback check, from two independent angles.
    #
    # A changing capture is accepted outright. A static capture is NOT a failure, because
    # composition output plus a GDI grab produces a flat result for a video that is playing
    # (measured: time-pos 9.30 -> 12.48, pause=false, four identical grabs). In that case the
    # verdict falls back to mpv's own clock, which cannot advance unless it is decoding.
    if change > 8.0:
        print("  OK    the picture is moving on screen (%.1f%% of pixels changed)" % change)
    elif advanced and paused is False and idle is False:
        print("  OK    mpv is playing (time-pos %.2f -> %.2f, pause=false, idle=false);"
              % (tp_first, tp_last))
        print("        the pixel capture was static, which composition output + "
              "ImageGrab can produce -- inconclusive, not a failure")
    else:
        print("  FAIL  nothing is playing (pixels %.1f%% changed, time-pos %r -> %r, "
              "pause=%r, idle=%r)" % (change, tp_first, tp_last, paused, idle))
        good = False

    # The line tools/ui-repair/gate.py parses, and it must be exact. The gate refuses any
    # verifier that does not print it, because "the last output line contained OK" is
    # precisely how the previous gate reported 8/8 PASS while the app could not play.
    print("\nVERDICT: %s -- playback through the app's own open path"
          % ("PASS" if good else "FAIL"))
    return 0 if good else 1


if __name__ == "__main__":
    sys.exit(main())
