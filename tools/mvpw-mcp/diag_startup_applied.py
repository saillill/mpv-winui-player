"""Final measurement of whether the startup settings reach mpv.

The pattern that works -- established by experiment, not by preference:

  * connect about 1 s after the process starts, BEFORE the window is up
  * hold that single connection for every request
  * never reopen

Connecting later (after `wait_for_window`, i.e. 3-7 s in) fails with
ERROR_NO_DATA (232) on the very first write, consistently. mpv's
input-ipc-server is a single-instance pipe: once a client has connected and gone
away, the next connection is refused for the life of the process. The window
appearing is slower than that first connect, which is why every probe that went
through launch_app() saw a dead pipe while a hand-written early connect worked.

This is a property of the harness, not of the app. The app's own behaviour is
measured here by connecting early and then comparing mpv's live values against
the values the app has stored.
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

# Repo root, derived from this file (it lives at <repo>/tools/...), so renaming or
# moving the checkout does not silently repoint these paths at an old copy.
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

DEPLOY = (REPO + r"\mpv-winui\mpv-winui\bin\x64"
          r"\Release\net10.0-windows10.0.26100.0\win-x64")
EXE = DEPLOY + r"\mpvw.exe"
PIPE = r"\\.\pipe\mpvpipe"
REG = r"HKCU:\Software\Classes\Local Settings\Software\mpv-winui\mpv-winui\app"
OUT = REPO + r"\_work\mcp\startup-applied.json"

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

# mpv property -> AppSettings key, chosen so the stored value differs from mpv's
# default for at least a few of them.
PAIRS = [
    ("volume-max", "Mpv_volume_max"),
    ("deband", "Mpv_deband"),
    ("interpolation", "Mpv_interpolation"),
    ("dither-depth", "Mpv_dither_depth"),
    ("audio-channels", "Mpv_audio_channels"),
    ("sub-auto", "Mpv_sub_auto"),
    ("scale", "Mpv_scale"),
    ("hdr-compute-peak", "HdrComputePeak"),
    ("d3d11-flip", "Mpv_d3d11_flip"),
]


class Conn:
    def __init__(self):
        self.h = None
        self.rid = 0
        self.buf = b""

    def connect(self, deadline_s=30):
        t0 = time.time()
        while time.time() - t0 < deadline_s:
            h = k32.CreateFileW(PIPE, 0xC0000000, 0, None, 3, 0, None)
            if not (h == wintypes.HANDLE(-1).value or h is None):
                self.h = h
                return round(time.time() - t0, 2)
            time.sleep(0.2)
        return None

    def ask(self, command, wait=5.0):
        if self.h is None:
            return {"error": "not connected"}
        self.rid += 1
        payload = json.dumps({"command": command,
                              "request_id": self.rid}).encode() + b"\n"
        w = wintypes.DWORD(0)
        if not k32.WriteFile(self.h, payload, len(payload), ctypes.byref(w), None):
            return {"error": "write failed", "win32": ctypes.get_last_error()}
        buf = ctypes.create_string_buffer(65536)
        rd = wintypes.DWORD(0)
        deadline = time.time() + wait
        while time.time() < deadline:
            while b"\n" in self.buf:
                line, self.buf = self.buf.split(b"\n", 1)
                line = line.strip()
                if not line:
                    continue
                try:
                    obj = json.loads(line)
                except Exception:
                    continue
                if obj.get("request_id") == self.rid:
                    return obj
            if not k32.ReadFile(self.h, buf, 65536, ctypes.byref(rd), None):
                return {"error": "read failed", "win32": ctypes.get_last_error()}
            if rd.value:
                self.buf += buf.raw[: rd.value]
            else:
                time.sleep(0.02)
        return {"error": "timeout"}

    def close(self):
        if self.h is not None:
            k32.CloseHandle(self.h)
            self.h = None


def reg(key):
    r = subprocess.run(
        ["pwsh", "-NoProfile", "-Command",
         f'(Get-ItemProperty -Path "{REG}" -Name {key} -ErrorAction SilentlyContinue).{key}'],
        capture_output=True, text=True)
    return (r.stdout or "").strip()


def main():
    for p in psutil.process_iter(["name", "pid"]):
        if (p.info["name"] or "").lower() == "mpvw.exe":
            try:
                psutil.Process(p.info["pid"]).terminate()
            except Exception:
                pass
    time.sleep(2)

    proc = psutil.Popen([EXE], cwd=DEPLOY)
    conn = Conn()
    when = conn.connect()
    print("connected %.2fs after launch" % when if when is not None
          else "never connected")
    if when is None:
        proc.terminate()
        sys.exit("no connection")

    # Do NOT idle on this connection. mpv drops a client that connects and then
    # stays silent -- every later write fails with ERROR_NO_DATA (232). Keep
    # asking while the app finishes its startup batch, so the connection is
    # demonstrably alive at the moment the settings are read.
    for _ in range(40):
        conn.ask(["get_property", "volume"])
        time.sleep(0.2)

    print("\n%-18s %-16s %-18s %s" % ("mpv property", "mpv value", "app setting", "verdict"))
    rows = []
    for prop, key in PAIRS:
        r = conn.ask(["get_property", prop])
        value = r.get("data") if r.get("error") == "success" else None
        stored = reg(key)
        if value is None:
            verdict = "unreadable (%s)" % r.get("error")
        elif str(value).lower() == str(stored).lower():
            verdict = "same"
        else:
            verdict = "differs"
        print("%-18s %-16s %-18s %s" % (
            prop, repr(value)[:16], repr(stored)[:18], verdict))
        rows.append({"property": prop, "mpv": value, "setting": stored,
                     "verdict": verdict})

    print("\nreplaying the startup command form on the same connection:")
    for cmd in (["no-osd", "set", "volume-max", "150"],
                ["get_property", "volume-max"],
                ["no-osd", "set", "deband", "no"],
                ["get_property", "deband"]):
        r = conn.ask(cmd)
        print("  %-38s -> %s" % (" ".join(cmd), r.get("data", r.get("error"))))

    conn.close()
    proc.terminate()
    time.sleep(1)
    left = any((p.info["name"] or "").lower() == "mpvw.exe"
               for p in psutil.process_iter(["name"]))
    print("\nprocess left:", left)

    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(rows, f, ensure_ascii=False, indent=1)
    print("wrote", OUT)


if __name__ == "__main__":
    sys.exit(main())
