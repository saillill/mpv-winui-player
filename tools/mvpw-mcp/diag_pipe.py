"""Find out why the mpv IPC pipe answers nothing.

The app stays alive, the window is up, and every request comes back "no reply".
Two possibilities with different owners:

  A. the pipe is not there -- the app never created its input-ipc-server, so
     nothing can connect. That is an app or config problem.
  B. the pipe is there but the server side is not reading. That is an mpv
     internal state, likely because something earlier in this session left it
     wedged (the rejections, or a request it could not answer).

Distinguish by listing the actual named pipes while the app runs, then by
opening the pipe once and writing a single minimal request with a long timeout,
reading whatever comes back raw -- including property-change events, which the
helper filters out and which prove the connection is alive even when no reply
arrives.
"""
import ctypes
import json
import os
import sys
import time
from ctypes import wintypes

import mpvw_tools as T

PIPE = r"\\.\pipe\mpvpipe"


def raw_roundtrip(command, request_id=1, wait=8.0, label=""):
    """Open the pipe, send one request, print every line that comes back."""
    GENERIC_READWRITE = 0xC0000000
    OPEN_EXISTING = 3
    k32 = ctypes.WinDLL("kernel32", use_last_error=True)
    k32.CreateFileW.restype = wintypes.HANDLE
    handle = k32.CreateFileW(PIPE, GENERIC_READWRITE, 0, None, OPEN_EXISTING, 0, None)
    if handle == wintypes.HANDLE(-1).value or handle is None:
        print("  %-22s cannot open the pipe (error %d)" % (
            label, ctypes.get_last_error()))
        return None

    payload = json.dumps({"command": command, "request_id": request_id}).encode() + b"\n"
    written = wintypes.DWORD(0)
    ok = k32.WriteFile(handle, payload, len(payload), ctypes.byref(written), None)
    print("  %-22s wrote=%s bytes=%d" % (label, bool(ok), written.value))
    if not ok:
        print("      write error:", ctypes.get_last_error())
        k32.CloseHandle(handle)
        return None

    buf = ctypes.create_string_buffer(65536)
    read = wintypes.DWORD(0)
    acc = b""
    deadline = time.time() + wait
    replies = []
    while time.time() < deadline:
        if not k32.ReadFile(handle, buf, 65536, ctypes.byref(read), None):
            print("      read failed, error:", ctypes.get_last_error())
            break
        if read.value == 0:
            time.sleep(0.2)
            continue
        acc += buf.raw[: read.value]
        while b"\n" in acc:
            line, acc = acc.split(b"\n", 1)
            line = line.strip()
            if not line:
                continue
            try:
                replies.append(json.loads(line))
            except Exception:
                replies.append({"_raw": line.decode("utf-8", "replace")})
        if any(r.get("request_id") == request_id for r in replies):
            break
    k32.CloseHandle(handle)
    print("      %d message(s) in %.1fs" % (len(replies), wait))
    for r in replies[:8]:
        print("        ", json.dumps(r, ensure_ascii=False)[:140])
    return replies


def main():
    T.close_app()
    T.launch_app()
    for t in (3, 6, 10):
        time.sleep(3)
        print("t+%ds" % t)
        raw_roundtrip(["get_property", "volume"], request_id=t, wait=4, label="get volume")
    T.close_app()


if __name__ == "__main__":
    sys.exit(main())
