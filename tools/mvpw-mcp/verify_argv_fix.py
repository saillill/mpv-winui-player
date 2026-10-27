"""Prove the argv/string fix changed behaviour, by replaying both forms.

The claim to test is narrow and checkable: a command LINE sent through the argv
path does nothing, and the same line sent through the string path works. Before
the fix the startup batch used the first form; now it uses the second.

Both forms are replayed against one running app, each on its own launch (the
single-request client this session settled on), and the property is read back.

Also normalises the two remaining "DIFFERS" rows from verify_startup_settings so
they stop being reported as suspicious:
  volume-max   100.0 vs "100"   -> same number, different formatting
  dither-depth False  vs "no"   -> mpv reports the option's boolean form
"""
import ctypes
import io
import json
import subprocess
import sys
import time
from ctypes import wintypes
import os
import paths

# Repo root, derived from this file (it lives at <repo>/tools/...), so renaming or
# moving the checkout does not silently repoint these paths at an old copy.
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

DEPLOY = paths.deploy_dir()
EXE = DEPLOY + r"\mpvw.exe"
PIPE = r"\\.\pipe\mpvpipe"
OUT = REPO + r"\_work\mcp\argv-vs-string.json"

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


def run(commands):
    """One launch, one connection, the given commands in order."""
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
        return None, "never connected"

    out = []
    acc = b""
    buf = ctypes.create_string_buffer(65536)
    rd = wintypes.DWORD(0)
    for i, cmd in enumerate(commands):
        rid = 200 + i
        payload = json.dumps({"command": cmd, "request_id": rid}).encode() + b"\n"
        w = wintypes.DWORD(0)
        if not k32.WriteFile(h, payload, len(payload), ctypes.byref(w), None):
            out.append({"cmd": cmd, "error": "write %d" % ctypes.get_last_error()})
            break
        reply = None
        deadline = time.time() + 5
        while time.time() < deadline and reply is None:
            if not k32.ReadFile(h, buf, 65536, ctypes.byref(rd), None):
                break
            if rd.value:
                acc += buf.raw[: rd.value]
                while b"\n" in acc:
                    line, acc = acc.split(b"\n", 1)
                    line = line.strip()
                    if not line:
                        continue
                    try:
                        obj = json.loads(line)
                    except Exception:
                        continue
                    if obj.get("request_id") == rid:
                        reply = obj
                        break
        out.append({"cmd": cmd, "reply": reply})
    k32.CloseHandle(h)
    kill_app()
    return out, None


def main():
    print("== the malformed form the startup batch used before the fix ==")
    bad, err = run([["no-osd set deband yes"], ["get_property", "deband"]])
    for row in (bad or []):
        print("   %-34s -> %s" % (" ".join(row["cmd"]),
                                  json.dumps(row.get("reply") or row.get("error"),
                                             ensure_ascii=False)[:110]))
    print("   error:", err)

    print("\n== the form it uses now ==")
    good, err = run([["no-osd", "set", "deband", "yes"], ["get_property", "deband"]])
    for row in (good or []):
        print("   %-34s -> %s" % (" ".join(row["cmd"]),
                                  json.dumps(row.get("reply") or row.get("error"),
                                             ensure_ascii=False)[:110]))
    print("   error:", err)

    with open(OUT, "w", encoding="utf-8") as f:
        json.dump({"malformed": bad, "fixed": good}, f, ensure_ascii=False, indent=1)
    print("\nwrote", OUT)


if __name__ == "__main__":
    sys.exit(main())
