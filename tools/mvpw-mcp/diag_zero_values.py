"""Is `0B` a legal value, or is the zero special?

diag_writable.py showed `demuxer-lavf-probesize` IS writable when given `8192`
(argv form). The startup batch sends `0B`, which was rejected. So the question is
narrow: does this option accept a value with a unit at all, and does it accept
zero?

Cases, one launch each:
    demuxer-lavf-probesize  0B    0    8192   8KiB
    demuxer-hysteresis-bytes  0    0B

If zero-with-unit fails but zero alone works, the unit helper must not add a
suffix to a zero value for these options.
"""
import ctypes
import io
import json
import subprocess
import sys
import time
from ctypes import wintypes

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

import psutil

DEPLOY = r"C:\Users\z2637\Documents\DeepSeek\mpv-deploy"
EXE = DEPLOY + r"\mpvw.exe"
PIPE = r"\\.\pipe\mpvpipe"
OUT = r"C:\Users\z2637\Documents\DeepSeek\mpv\_work\mcp\zero-values.json"

CASES = [
    ("demuxer-lavf-probesize", "0B"),
    ("demuxer-lavf-probesize", "0"),
    ("demuxer-lavf-probesize", "8192"),
    ("demuxer-lavf-probesize", "8KiB"),
    ("demuxer-hysteresis-bytes", "0"),
    ("demuxer-hysteresis-bytes", "0B"),
    ("vd-queue-max-bytes", "512MiB"),
    ("stream-buffer-size", "0B"),
    ("stream-buffer-size", "0"),
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
        if not any((p.info["name"] or "").lower() == "mpvw.exe"
                   for p in psutil.process_iter(["name"])):
            return
        time.sleep(0.2)


def one(command, rid):
    kill_app()
    subprocess.run(["pwsh", "-NoProfile", "-Command",
                    "Start-Process '%s' -WorkingDirectory '%s'" % (EXE, DEPLOY)],
                   capture_output=True)
    time.sleep(1.1)
    h = None
    t0 = time.time()
    while time.time() - t0 < 12:
        h = k32.CreateFileW(PIPE, 0xC0000000, 0, None, 3, 0, None)
        if not (h == wintypes.HANDLE(-1).value or h is None):
            break
        h = None
        time.sleep(0.15)
    if h is None:
        return {"error": "never connected"}
    payload = json.dumps({"command": command, "request_id": rid}).encode() + b"\n"
    w = wintypes.DWORD(0)
    if not k32.WriteFile(h, payload, len(payload), ctypes.byref(w), None):
        k32.CloseHandle(h)
        return {"error": "write %d" % ctypes.get_last_error()}
    buf = ctypes.create_string_buffer(65536)
    rd = wintypes.DWORD(0)
    acc = b""
    deadline = time.time() + 5
    while time.time() < deadline:
        if not k32.ReadFile(h, buf, 65536, ctypes.byref(rd), None):
            k32.CloseHandle(h)
            return {"error": "read %d" % ctypes.get_last_error()}
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
                if obj.get("request_id") == rid:
                    k32.CloseHandle(h)
                    return obj
    k32.CloseHandle(h)
    return {"error": "no reply"}


def main():
    rows = []
    print("%-26s %-10s %s" % ("option", "value", "set result"))
    for i, (opt, val) in enumerate(CASES):
        r = one(["no-osd", "set", opt, val], 800 + i)
        print("%-26s %-10s %s" % (opt, val, r.get("error")))
        rows.append({"option": opt, "value": val, "result": r.get("error")})
    kill_app()

    print("\nzero WITH a unit:")
    for r in rows:
        if r["value"].endswith("B") and r["value"][:-1].strip("0.") == "":
            print("   %-26s %-8s %s" % (r["option"], r["value"], r["result"]))
    print("zero WITHOUT a unit:")
    for r in rows:
        if r["value"] == "0":
            print("   %-26s %-8s %s" % (r["option"], r["value"], r["result"]))

    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(rows, f, ensure_ascii=False, indent=1)
    print("\nwrote", OUT)


if __name__ == "__main__":
    sys.exit(main())
