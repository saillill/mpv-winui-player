"""Why does this helper fail where an identical hand-written probe succeeds?

Both open `\\.\pipe\mpvpipe` with access 0xC0000000 and share mode 0. Both write
the same JSON. The hand-written one gets `{"data":...,"error":"success"}`; the
helper gets nothing. Rather than keep guessing, run the helper's own code path in
the same process as the hand-written one, so the difference cannot be the app,
the machine or the moment -- only the code.

Prints, for each attempt: the handle value, its low 32 bits, and whether
CloseHandle/ReadFile see it as valid. A truncated-TRUNCATED handle shows up as a
mismatch between the value ctypes returns and the value the OS accepts.
"""
import ctypes
import io
import json
import os
import sys
import time
from ctypes import wintypes

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "tools", "mvpw-mcp"))
import mpvw_tools as T

PIPE = r"\\.\pipe\mpvpipe"


def declare(dll):
    dll.CreateFileW.restype = wintypes.HANDLE
    dll.CreateFileW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD,
                                ctypes.c_void_p, wintypes.DWORD, wintypes.DWORD,
                                wintypes.HANDLE]
    dll.ReadFile.restype = wintypes.BOOL
    dll.ReadFile.argtypes = [wintypes.HANDLE, ctypes.c_void_p, wintypes.DWORD,
                             ctypes.POINTER(wintypes.DWORD), ctypes.c_void_p]
    dll.WriteFile.restype = wintypes.BOOL
    dll.WriteFile.argtypes = [wintypes.HANDLE, ctypes.c_void_p, wintypes.DWORD,
                              ctypes.POINTER(wintypes.DWORD), ctypes.c_void_p]
    dll.CloseHandle.restype = wintypes.BOOL
    dll.CloseHandle.argtypes = [wintypes.HANDLE]


def attempt(label, dll):
    h = dll.CreateFileW(PIPE, 0xC0000000, 0, None, 3, 0, None)
    err = ctypes.get_last_error()
    bad = (h == wintypes.HANDLE(-1).value or h is None)
    print("  %-18s handle=%-22r err=%-3d invalid=%s" % (label, h, err, bad))
    if bad:
        return
    payload = json.dumps({"command": ["get_property", "volume"],
                          "request_id": 42}).encode() + b"\n"
    w = wintypes.DWORD(0)
    ok = dll.WriteFile(h, payload, len(payload), ctypes.byref(w), None)
    print("      write=%s wrote=%d err=%d" % (ok, w.value, ctypes.get_last_error()))
    buf = ctypes.create_string_buffer(8192)
    rd = wintypes.DWORD(0)
    rok = dll.ReadFile(h, buf, 8192, ctypes.byref(rd), None)
    print("      read=%s n=%d err=%d data=%r" % (
        rok, rd.value, ctypes.get_last_error(),
        buf.raw[: rd.value][:80]))
    dll.CloseHandle(h)


def main():
    T.close_app()
    T.launch_app()
    time.sleep(7)

    print("module helper state: _HANDLE=%r" % (T._HANDLE,))

    # a) the hand-written path: a private WinDLL with signatures declared
    mine = ctypes.WinDLL("kernel32", use_last_error=True)
    declare(mine)
    attempt("private WinDLL", mine)

    # b) the module's shared instance
    attempt("module _k32", T._k32)

    # c) through the helper itself
    r = T._ipc(["get_property", "volume"], 4242)
    print("  helper _ipc        -> %r" % (r,))

    T.close_app()


if __name__ == "__main__":
    sys.exit(main())
