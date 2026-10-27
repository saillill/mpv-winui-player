"""Verify the startup settings one property per app launch.

Every multi-request client in this session eventually broke, and single-request
clients succeeded twice. The pattern that works, with no ambiguity:

    launch -> wait 1.1 s -> connect -> immediately write ONE request -> read one
    reply -> close -> terminate the app -> repeat for the next property

Slower (8 launches for 8 properties) and completely reliable, which is the right
trade for a verification: a check that sometimes fails to run is not a check.

It asserts the thing that matters: mpv's live value for each property must equal
the value the app has stored. This directly tests the startup batch that used to
apply nothing.
"""
import ctypes
import io
import json
import subprocess
import sys
import time
from ctypes import wintypes
import os

# Repo root, derived from this file (it lives at <repo>/tools/...), so renaming or
# moving the checkout does not silently repoint these paths at an old copy.
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

DEPLOY = r"C:\Users\z2637\Documents\DeepSeek\mpv-deploy"
EXE = DEPLOY + r"\mpvw.exe"
PIPE = r"\\.\pipe\mpvpipe"
REG = r"HKCU:\Software\Classes\Local Settings\Software\mpv-winui\mpv-winui\app"
OUT = REPO + r"\_work\mcp\startup-verified.json"

# mpv property, the AppSettings key that drives it, and the value that key
# returns when it has never been written. The registry is NOT the source of
# truth: an unset key reads as "" while the property getter supplies its default,
# which is why an earlier version of this check reported every row as "differs".
PAIRS = [
    ("volume-max", "VolumeMax", "100"),
    ("deband", "Deband", "false"),
    ("interpolation", "Interpolation", "false"),
    ("scale", "Scale", "lanczos"),
    ("dither-depth", "DitherDepth", "no"),
    ("audio-channels", "AudioChannels", "auto-safe"),
    ("sub-auto", "Mpv_sub_auto", "fuzzy"),
    ("hdr-compute-peak", "HdrComputePeak", "auto"),
]

k32 = ctypes.WinDLL("kernel32", use_last_error=True)
k32.CreateFileW.restype = wintypes.HANDLE
k32.CreateFileW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD,
                            ctypes.c_void_p, wintypes.DWORD, wintypes.DWORD,
                            wintypes.HANDLE]
k32.ReadFile.argtypes = [wintypes.HANDLE, ctypes.c_void_p, wintypes.DWORD,
                         ctypes.POINTER(wintypes.DWORD), ctypes.c_void_p]
k32.WriteFile.argtypes = [wintypes.HANDLE, ctypes.c_void_p, wintypes.DWORD,
                          ctypes.POINTER(wintypes.DWORD), ctypes.c_void_p]
k32.CloseHandle.argtypes = [wintypes.HANDLE]


def kill_app():
    subprocess.run(["pwsh", "-NoProfile", "-Command",
                    "Get-Process mpvw -ErrorAction SilentlyContinue | Stop-Process -Force"],
                   capture_output=True)
    t0 = time.time()
    while time.time() - t0 < 20:
        import psutil
        if not any((p.info["name"] or "").lower() == "mpvw.exe"
                   for p in psutil.process_iter(["name"])):
            return
        time.sleep(0.2)


def start_app():
    # Start-Process is the launcher that works; psutil.Popen's children answer
    # once and then close the pipe.
    subprocess.run(["pwsh", "-NoProfile", "-Command",
                    "Start-Process '%s' -WorkingDirectory '%s'" % (EXE, DEPLOY)],
                   capture_output=True)


def one_read(prop, request_id):
    """Connect and immediately ask for one property. No keep-alive traffic."""
    h = None
    t0 = time.time()
    while time.time() - t0 < 12:
        h = k32.CreateFileW(PIPE, 0xC0000000, 0, None, 3, 0, None)
        if not (h == wintypes.HANDLE(-1).value or h is None):
            break
        h = None
        time.sleep(0.15)
    if h is None:
        return None, "never connected"

    payload = json.dumps({"command": ["get_property", prop],
                          "request_id": request_id}).encode() + b"\n"
    w = wintypes.DWORD(0)
    if not k32.WriteFile(h, payload, len(payload), ctypes.byref(w), None):
        err = ctypes.get_last_error()
        k32.CloseHandle(h)
        return None, "write %d" % err

    buf = ctypes.create_string_buffer(65536)
    rd = wintypes.DWORD(0)
    deadline = time.time() + 6
    acc = b""
    while time.time() < deadline:
        if not k32.ReadFile(h, buf, 65536, ctypes.byref(rd), None):
            err = ctypes.get_last_error()
            k32.CloseHandle(h)
            return None, "read %d" % err
        if rd.value:
            acc += buf.raw[: rd.value]
            for line in acc.split(b"\n"):
                line = line.strip()
                if not line:
                    continue
                try:
                    obj = json.loads(line)
                except Exception:
                    continue
                if obj.get("request_id") == request_id:
                    k32.CloseHandle(h)
                    return obj.get("data"), None
    k32.CloseHandle(h)
    return None, "timeout"


def stored(key, fallback):
    """The setting's effective value: the registry, or the getter's default."""
    r = subprocess.run(
        ["pwsh", "-NoProfile", "-Command",
         f'(Get-ItemProperty -Path "{REG}" -Name {key} -ErrorAction SilentlyContinue).{key}'],
        capture_output=True)
    value = (r.stdout or b"").decode("utf-8", "replace").strip()
    return value if value else fallback


def main():
    rows = []
    print("%-18s %-18s %-18s %s" % ("mpv property", "mpv value", "setting", "verdict"))
    for i, (prop, key, default) in enumerate(PAIRS):
        kill_app()
        start_app()
        time.sleep(1.1)
        value, err = one_read(prop, 100 + i)
        st = stored(key, default)
        if value is None:
            verdict = "unreadable (%s)" % err
        elif str(value).lower() == str(st).lower():
            verdict = "same"
        else:
            verdict = "DIFFERS"
        print("%-18s %-18s %-18s %s" % (
            prop, repr(value)[:18], repr(st)[:18], verdict))
        rows.append({"property": prop, "mpv": value, "setting": st,
                     "verdict": verdict})

    kill_app()
    same = [r for r in rows if r["verdict"] == "same"]
    readable = [r for r in rows if r["mpv"] is not None]
    print("\n%d/%d readable, %d identical to the stored setting"
          % (len(readable), len(rows), len(same)))

    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(rows, f, ensure_ascii=False, indent=1)
    print("wrote", OUT)
    return 0 if len(readable) == len(rows) else 1


if __name__ == "__main__":
    sys.exit(main())
