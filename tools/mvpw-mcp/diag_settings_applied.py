"""Verify the settings actually reach mpv now that the startup batch works.

Two rounds of measurement got confused here, and the confusion was the tool's,
not the app's:

  * the pipe answers on the first connection but mpv drops later ones when a
    client opens and closes in a tight loop. The earlier probes opened a fresh
    handle every few hundred milliseconds and then reported "dead pipe".
  * a probe launched the app and immediately hammered ten `get_property` calls
    with no delay, which is the same thing.

So this connects ONCE and does all its reading through that one connection,
with the request ids incremented, the way mpv's IPC is meant to be used. It
compares each setting against the value the app claims to have stored, because
"the command was accepted" is not the same as "the setting took effect".
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

PIPE = r"\\.\pipe\mpvpipe"
DEPLOY = (REPO + r"\mpv-winui\mpv-winui\bin\x64"
          r"\Release\net10.0-windows10.0.26100.0\win-x64")
EXE = DEPLOY + r"\mpvw.exe"
REG = r"HKCU:\Software\Classes\Local Settings\Software\mpv-winui\mpv-winui\app"

k32 = ctypes.WinDLL("kernel32", use_last_error=True)
k32.CreateFileW.restype = wintypes.HANDLE
k32.CreateFileW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD,
                            ctypes.c_void_p, wintypes.DWORD, wintypes.DWORD,
                            wintypes.HANDLE]

# (mpv property, AppSettings key) -- values that the app writes and mpv echoes.
PAIRS = [
    ("deband", "Mpv_deband"),
    ("dither-depth", "Mpv_dither_depth"),
    ("audio-channels", "Mpv_audio_channels"),
    ("interpolation", "Mpv_interpolation"),
    ("scale", "Mpv_scale"),
    ("volume-max", "VolumeMax"),
    ("sub-auto", "Mpv_sub_auto"),
    ("hdr-compute-peak", "HdrComputePeak"),
]


class Conn:
    def __init__(self):
        self.h = k32.CreateFileW(PIPE, 0xC0000000, 0, None, 3, 0, None)
        if self.h == wintypes.HANDLE(-1).value or self.h is None:
            raise RuntimeError("cannot open %s (err %d)" % (PIPE, ctypes.get_last_error()))
        self.rid = 0
        self.acc = b""

    def ask(self, command, wait=4.0):
        self.rid += 1
        payload = json.dumps({"command": command, "request_id": self.rid}).encode() + b"\n"
        w = wintypes.DWORD(0)
        if not k32.WriteFile(self.h, payload, len(payload), ctypes.byref(w), None):
            return {"error": "write failed", "win32": ctypes.get_last_error()}
        buf = ctypes.create_string_buffer(65536)
        rd = wintypes.DWORD(0)
        deadline = time.time() + wait
        while time.time() < deadline:
            if not k32.ReadFile(self.h, buf, 65536, ctypes.byref(rd), None):
                return {"error": "read failed", "win32": ctypes.get_last_error()}
            if rd.value:
                self.acc += buf.raw[: rd.value]
                while b"\n" in self.acc:
                    line, self.acc = self.acc.split(b"\n", 1)
                    line = line.strip()
                    if not line:
                        continue
                    try:
                        obj = json.loads(line)
                    except Exception:
                        continue
                    if obj.get("request_id") == self.rid:
                        return obj
        return {"error": "timeout"}

    def close(self):
        try:
            k32.CloseHandle(self.h)
        except Exception:
            pass


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
    time.sleep(1.5)

    proc = psutil.Popen([EXE], cwd=DEPLOY)
    time.sleep(7)

    c = Conn()
    print("connected; request ids on one connection\n")
    print("%-18s %-14s %-22s %s" % ("mpv property", "mpv value", "app setting", "verdict"))
    rows = []
    for prop, key in PAIRS:
        r = c.ask(["get_property", prop])
        value = r.get("data") if r.get("error") == "success" else None
        stored = reg(key)
        verdict = "same" if str(value).lower() == str(stored).lower() else "MPV DIFFERS"
        if value is None:
            verdict = "unreadable"
        print("%-18s %-14s %-22s %s" % (
            prop, repr(value)[:14], repr(stored)[:22], verdict))
        rows.append({"property": prop, "mpv": value, "setting": stored,
                     "verdict": verdict})

    # The startup batch sends these two from ToCommand, so they are the direct
    # test of the argv/string fix.
    print("\ncommands the app sends at startup, replayed on this connection:")
    for cmd in (["no-osd", "set", "deband", "yes"],
                ["no-osd", "set", "volume-max", "150"]):
        r = c.ask(cmd)
        print("  %-34s -> %s" % (" ".join(cmd), r.get("error")))
    for prop in ("deband", "volume-max"):
        r = c.ask(["get_property", prop])
        print("  %-34s = %r" % (prop, r.get("data")))

    c.close()
    proc.terminate()
    time.sleep(1)
    left = any((p.info["name"] or "").lower() == "mpvw.exe"
               for p in psutil.process_iter(["name"]))
    print("\nprocess left:", left)

    out = REPO + r"\_work\mcp\settings-applied.json"
    with open(out, "w", encoding="utf-8") as f:
        json.dump(rows, f, ensure_ascii=False, indent=1)
    print("wrote", out)


if __name__ == "__main__":
    sys.exit(main())
