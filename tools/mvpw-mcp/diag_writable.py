"""Which schema options are readable but NOT writable (the ConfigOnly set)?

The nine rejections split into "name absent" and "init-only", and this measures
the second group precisely, because that group is what ConfigOnlyKeys is for.

For each candidate: one launch, one `get_property` (does the name exist?), then a
separate launch with one `set` (is it writable?). Two launches per option, which
is the reliable shape here.

The candidate list is the nine rejections plus the other size options the unit
helper rewrites, because the spelling test showed those are refused too:
  vd-queue-max-bytes, ad-queue-max-bytes, stream-buffer-size,
  demuxer-lavf-buffersize, demuxer-hysteresis-bytes
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
OUT = r"C:\Users\z2637\Documents\DeepSeek\mpv\_work\mcp\writable.json"

CANDIDATES = [
    # the nine rejections
    ("openal-direct-channels", "yes"),
    ("openal-num-buffers", "4"),
    ("openal-num-samples", "8192"),
    ("cuda-decode-device", "auto"),
    ("player-operation-mode", "cplayer"),
    ("input-gamepad", "no"),
    ("demuxer-lavf-probesize", "8192"),
    ("demuxer-rawvideo-size", "0"),
    ("demuxer-sbr-probesize", "128"),
    # other size options the helper rewrites
    ("vd-queue-max-bytes", "512MiB"),
    ("ad-queue-max-bytes", "1MiB"),
    ("stream-buffer-size", "128KiB"),
    ("demuxer-lavf-buffersize", "32KiB"),
    ("demuxer-hysteresis-bytes", "0"),
    # controls that must stay writable
    ("volume", "55"),
    ("deband", "yes"),
    ("scale", "bilinear"),
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
    print("%-26s %-12s %-12s %s" % ("option", "get", "set", "class"))
    for i, (opt, value) in enumerate(CANDIDATES):
        g = one(["get_property", opt], 600 + i)
        exists = g.get("error") == "success"
        # argv form: ["no-osd","set",opt,value]. The one-element form
        # ["no-osd set opt value"] is the malformed shape this whole session was
        # about -- using it here made even `volume` look init-only.
        s = one(["no-osd", "set", opt, value], 700 + i)
        writable = s.get("error") == "success"

        if not exists:
            cls = "name absent"
        elif writable:
            cls = "writable"
        else:
            cls = "init-only"
        print("%-26s %-12s %-12s %s" % (
            opt, "yes" if exists else "no", "yes" if writable else "no", cls))
        rows.append({"option": opt, "value": value, "exists": exists,
                     "writable": writable, "class": cls,
                     "setReply": s.get("error")})

    kill_app()
    by = {}
    for r in rows:
        by.setdefault(r["class"], []).append(r["option"])
    print()
    for cls, opts in sorted(by.items()):
        print("%-14s %d  %s" % (cls, len(opts), opts))

    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(rows, f, ensure_ascii=False, indent=1)
    print("\nwrote", OUT)


if __name__ == "__main__":
    sys.exit(main())
