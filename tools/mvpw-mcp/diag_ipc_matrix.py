"""One controlled comparison: does the launch method decide whether IPC works?

Everything else in this session's IPC trouble has been a variation on one
question, and it keeps being answered by accident. So ask it directly, in a
single process, with the same client code and the same app binary:

    A. subprocess Start-Process,  sleep 1.1, connect, write, read
    B. subprocess Start-Process,  sleep 1.1, connect, write, read   (repeat)
    C. psutil.Popen,              sleep 1.1, connect, write, read
    D. subprocess Start-Process,  sleep 4.0, connect, write, read

If A/B pass and C/D fail, the answer is "how it was launched and when", and the
helper must do A. If they all fail now, then something changed in this machine's
state during the session -- in which case nothing in the repo is at fault and the
honest thing is to say so rather than keep patching the client.

Each case starts from a fully dead process. One connection, one write, one read.
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


def kill_all():
    subprocess.run(["pwsh", "-NoProfile", "-Command",
                    "Get-Process mpvw -ErrorAction SilentlyContinue | Stop-Process -Force"],
                   capture_output=True)
    t0 = time.time()
    while time.time() - t0 < 25:
        if not any((p.info["name"] or "").lower() == "mpvw.exe"
                   for p in psutil.process_iter(["name"])):
            return True
        time.sleep(0.2)
    return False


def attempt(label, launcher, delay):
    kill_all()
    launcher()
    time.sleep(delay)

    h = None
    t0 = time.time()
    while time.time() - t0 < 10:
        h = k32.CreateFileW(PIPE, 0xC0000000, 0, None, 3, 0, None)
        if not (h == wintypes.HANDLE(-1).value or h is None):
            break
        h = None
        time.sleep(0.1)
    if h is None:
        print("  %-28s no connection" % label)
        return "no connection"

    payload = json.dumps({"command": ["get_property", "volume"],
                          "request_id": 1}).encode() + b"\n"
    w = wintypes.DWORD(0)
    if not k32.WriteFile(h, payload, len(payload), ctypes.byref(w), None):
        err = ctypes.get_last_error()
        k32.CloseHandle(h)
        print("  %-28s write failed (%d)" % (label, err))
        return "write %d" % err

    buf = ctypes.create_string_buffer(8192)
    rd = wintypes.DWORD(0)
    acc = b""
    deadline = time.time() + 6
    while time.time() < deadline:
        if not k32.ReadFile(h, buf, 8192, ctypes.byref(rd), None):
            err = ctypes.get_last_error()
            k32.CloseHandle(h)
            print("  %-28s read failed (%d)" % (label, err))
            return "read %d" % err
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
                if obj.get("request_id") == 1:
                    k32.CloseHandle(h)
                    print("  %-28s OK  %s" % (label, obj.get("data")))
                    return "ok"
    k32.CloseHandle(h)
    print("  %-28s no reply" % label)
    return "no reply"


def start_ps():
    subprocess.run(["pwsh", "-NoProfile", "-Command",
                    "Start-Process '%s' -WorkingDirectory '%s'" % (EXE, DEPLOY)],
                   capture_output=True)


def start_psutil():
    psutil.Popen([EXE], cwd=DEPLOY)


def main():
    results = {}
    for label, launcher, delay in (
            ("A Start-Process, 1.1s", start_ps, 1.1),
            ("B Start-Process, 1.1s", start_ps, 1.1),
            ("C psutil.Popen, 1.1s", start_psutil, 1.1),
            ("D Start-Process, 4.0s", start_ps, 4.0),
            ("E Start-Process, 0.6s", start_ps, 0.6),
    ):
        results[label] = attempt(label, launcher, delay)

    kill_all()
    print("\nsummary:", json.dumps(results, indent=1))


if __name__ == "__main__":
    sys.exit(main())
