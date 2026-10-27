"""Are the nine remaining rejected options readable at all?

Nine commands still get rejected on a clean launch:

    openal-direct-channels / openal-num-buffers / openal-num-samples
    cuda-decode-device / player-operation-mode / input-gamepad
    demuxer-lavf-probesize / demuxer-rawvideo-size / demuxer-sbr-probesize

Every spelling of `set` was refused for the size options, including the obvious
`512MiB`, so "wrong spelling" is not the explanation. The remaining question is
whether mpv knows these names at all: an option that is not a property cannot be
read either, and an option that is init-only can be read but not set.

One request per launch, which is the only client shape that works reliably here.
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
import os
import paths

# Repo root, derived from this file (it lives at <repo>/tools/...), so renaming or
# moving the checkout does not silently repoint these paths at an old copy.
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

DEPLOY = paths.deploy_dir()
EXE = DEPLOY + r"\mpvw.exe"
PIPE = r"\\.\pipe\mpvpipe"
OUT = REPO + r"\_work\mcp\init-only.json"

OPTIONS = [
    "openal-direct-channels", "openal-num-buffers", "openal-num-samples",
    "cuda-decode-device", "player-operation-mode", "input-gamepad",
    "demuxer-lavf-probesize", "demuxer-rawvideo-size", "demuxer-sbr-probesize",
    # controls, to show the probe itself works
    "volume", "deband",
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
    print("%-26s %s" % ("option", "get_property"))
    for i, opt in enumerate(OPTIONS):
        r = one(["get_property", opt], 500 + i)
        readable = r.get("error") == "success"
        print("%-26s %-10s %s" % (
            opt, "readable" if readable else "NO",
            json.dumps(r.get("data", r.get("error")))[:60]))
        rows.append({"option": opt, "readable": readable, "reply": r})

    kill_app()
    readable = [r["option"] for r in rows if r["readable"]]
    not_readable = [r["option"] for r in rows if not r["readable"]]
    print("\nreadable as properties : %d  %s" % (len(readable), readable))
    print("not readable           : %d  %s" % (len(not_readable), not_readable))

    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(rows, f, ensure_ascii=False, indent=1)
    print("wrote", OUT)


if __name__ == "__main__":
    sys.exit(main())
