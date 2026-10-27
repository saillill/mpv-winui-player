"""Why does the standalone matrix succeed and the helper's own path fail?

diag_ipc_matrix.py: connecting 0.6-1.2 s after launch succeeds, connecting at
4.0 s fails. launch_app() now sleeps 1.1 s and connects -- the same timing that
works -- and yet `_ipc` immediately reports write 232.

So the difference is in the helper's code, not the timing. This runs both in one
process, on one app instance, in the order that exposes it:

  1. _open_pipe(), then write directly through kernel32 -- bypassing _ipc
  2. _ipc on the same handle
  3. a second _open_pipe and a direct write

If (1) works and (2) does not, the fault is inside _ipc. If both fail, the
fault is in _open_pipe. Printing the handle each time rules out the handle being
replaced under us.
"""
import ctypes
import io
import json
import subprocess
import os
import sys
import time
from ctypes import wintypes

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "tools", "mvpw-mcp"))
import mpvw_tools as T

DEPLOY = r"C:\Users\z2637\Documents\DeepSeek\mpv-deploy"


def kill_all():
    subprocess.run(["pwsh", "-NoProfile", "-Command",
                    "Get-Process mpvw -ErrorAction SilentlyContinue | Stop-Process -Force"],
                   capture_output=True)
    import psutil
    t0 = time.time()
    while time.time() - t0 < 25:
        if not any((p.info["name"] or "").lower() == "mpvw.exe"
                   for p in psutil.process_iter(["name"])):
            return
        time.sleep(0.2)


def direct(handle, command, rid=1):
    payload = json.dumps({"command": command, "request_id": rid}).encode() + b"\n"
    w = wintypes.DWORD(0)
    ok = T._k32.WriteFile(handle, payload, len(payload), ctypes.byref(w), None)
    if not ok:
        return {"stage": "write", "error": ctypes.get_last_error()}
    buf = ctypes.create_string_buffer(8192)
    rd = wintypes.DWORD(0)
    if not T._k32.ReadFile(handle, buf, 8192, ctypes.byref(rd), None):
        return {"stage": "read", "error": ctypes.get_last_error()}
    return {"stage": "ok", "data": buf.raw[: rd.value][:100]}


def main():
    kill_all()
    subprocess.run(["pwsh", "-NoProfile", "-Command",
                    "Start-Process '%s' -WorkingDirectory '%s'" % (T.EXE, DEPLOY)],
                   capture_output=True)
    time.sleep(1.1)

    print("1) _open_pipe + direct WriteFile/ReadFile")
    try:
        T._open_pipe()
        print("   handle:", T._HANDLE)
        print("   direct:", direct(T._HANDLE, ["get_property", "volume"]))
    except Exception as e:
        print("   failed:", e)

    print("2) _ipc on the same handle")
    print("   _ipc:", T._ipc(["get_property", "volume"], 2))

    print("3) fresh connection + direct write")
    T._close_pipe()
    time.sleep(0.4)
    try:
        T._open_pipe()
        print("   handle:", T._HANDLE)
        print("   direct:", direct(T._HANDLE, ["get_property", "deband"]))
    except Exception as e:
        print("   failed:", e)

    print("4) _ipc again")
    print("   _ipc:", T._ipc(["get_property", "deband"], 3))

    kill_all()


if __name__ == "__main__":
    sys.exit(main())
