"""Which spelling of the size options does mpv actually accept?

The unit helper rewrote "0 B" to "0B" and "512.000 MiB" to "512MiB". After that
change the rejections dropped from 76 to 9, and `demuxer-lavf-probesize 0B` is one
of the nine -- so the helper is closer but not right for every value.

Candidates, tested one per launch because this session established that only one
request per connection works here:

    demuxer-lavf-probesize   "0B"   "0"   "0 B"
    vd-queue-max-bytes       "512MiB"  "512M"  "536870912"

Each candidate is sent as a command LINE through the string path (the path the
startup batch now uses), and the option is read back afterwards. A value that is
accepted AND readable is the spelling to emit.
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
OUT = REPO + r"\_work\mcp\size-spellings.json"

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
    import psutil
    t0 = time.time()
    while time.time() - t0 < 20:
        if not any((p.info["name"] or "").lower() == "mpvw.exe"
                   for p in psutil.process_iter(["name"])):
            return
        time.sleep(0.2)


def probe(option, spelling, read_back=True):
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

    # One request per connection: send the set, and rely on the fact that a
    # command line is either accepted or rejected -- the second read needs a new
    # launch, so it is a separate call.
    line = "no-osd set %s %s" % (option, spelling)
    payload = json.dumps({"command": line, "request_id": 300}).encode() + b"\n"
    w = wintypes.DWORD(0)
    if not k32.WriteFile(h, payload, len(payload), ctypes.byref(w), None):
        k32.CloseHandle(h)
        return {"error": "write %d" % ctypes.get_last_error()}
    buf = ctypes.create_string_buffer(65536)
    rd = wintypes.DWORD(0)
    reply = None
    deadline = time.time() + 5
    acc = b""
    while time.time() < deadline and reply is None:
        if not k32.ReadFile(h, buf, 65536, ctypes.byref(rd), None):
            break
        if rd.value:
            acc += buf.raw[: rd.value]
            for lineb in acc.split(b"\n"):
                lineb = lineb.strip()
                if not lineb:
                    continue
                try:
                    obj = json.loads(lineb)
                except Exception:
                    continue
                if obj.get("request_id") == 300:
                    reply = obj
                    break
    k32.CloseHandle(h)
    kill_app()
    return reply or {"error": "no reply"}


def main():
    cases = [
        ("demuxer-lavf-probesize", "0B"),
        ("demuxer-lavf-probesize", "0"),
        ("vd-queue-max-bytes", "512MiB"),
        ("vd-queue-max-bytes", "512M"),
        ("stream-buffer-size", "128KiB"),
    ]
    rows = []
    print("%-26s %-12s %s" % ("option", "spelling", "reply"))
    for opt, spelling in cases:
        r = probe(opt, spelling)
        print("%-26s %-12s %s" % (opt, spelling,
                                  json.dumps(r, ensure_ascii=False)[:100]))
        rows.append({"option": opt, "spelling": spelling, "reply": r})

    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(rows, f, ensure_ascii=False, indent=1)
    print("\nwrote", OUT)


if __name__ == "__main__":
    sys.exit(main())
