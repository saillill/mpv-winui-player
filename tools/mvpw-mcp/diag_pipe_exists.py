"""Is the mpv IPC pipe there at all, and who holds it?

Every helper probe now reports "no reply", but that helper opens
`\\.\pipe\mpvpipe` by name and writes. Two very different situations look the
same from there:

  A. the pipe does not exist -> CreateFileW would fail with ERROR_FILE_NOT_FOUND
     (2). It does NOT fail, so the pipe exists and accepts the write.
  B. the pipe exists, the write succeeds, the read returns ERROR_BROKEN_PIPE
     (109): the server closed its end, or a second writer took it.

Since A is excluded by the error code, the useful next question is whether mpv
created *a* pipe and what its state is. `Get-ChildItem \\.\pipe\` in PowerShell
enumerates them without opening anything.
"""
import io
import json
import os
import subprocess
import sys
import time

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

import mpvw_tools as T

PS = r"""
$ErrorActionPreference='SilentlyContinue'
Get-ChildItem '\\.\pipe\' | Where-Object { $_.Name -like '*mpv*' } |
  Select-Object -ExpandProperty Name
"""


def pipes():
    r = subprocess.run(["pwsh", "-NoProfile", "-Command", PS],
                       capture_output=True, text=True)
    names = [x.strip() for x in (r.stdout or "").splitlines() if x.strip()]
    if r.stderr.strip():
        print("    ps stderr:", r.stderr.strip()[:160])
    return names


def main():
    print("pipes with 'mpv' in the name, app NOT running:")
    for n in pipes():
        print("   ", n)

    T.close_app()
    T.launch_app()
    print("\napp launched; sampling for 20s")
    seen = None
    for i in range(10):
        time.sleep(2)
        names = pipes()
        alive = T._pid() is not None
        print("  t+%2ds alive=%-5s pipes=%s" % (2 * (i + 1), alive, names))
        if names:
            seen = names
        if not alive:
            break

    if seen:
        print("\nthe pipe exists, so CreateFileW succeeds; the failure is on the")
        print("server side of the connection, not the name.")
    else:
        print("\nno pipe was ever created: input-ipc-server did not take effect.")
    T.close_app()


if __name__ == "__main__":
    sys.exit(main())
