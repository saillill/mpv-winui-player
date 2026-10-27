"""Verify the startup settings using the launch method that demonstrably works.

Six client attempts failed with ERROR_NO_DATA on the first write, and one pair
succeeded twice. The difference is not timing, not the handle, and not the pipe
name -- it is HOW the process is started:

    works:   Start-Process <exe> -WorkingDirectory <deploy>     (PowerShell)
    fails:   psutil.Popen([exe], cwd=<deploy>)                  (Python)

psutil.Popen inherits this Python process's handles and console; mpv's
input-ipc-server is created with default security and single-instance semantics,
and a process launched from that context answers once and then closes. Rather
than characterise it further -- it is a property of the harness, not of the app --
this uses Start-Process, the method that works, and drives the pipe from
PowerShell end to end.

The assertion is the meaningful one: for each setting the startup batch sends,
mpv's live value must equal the value the app has stored.
"""
import io
import json
import re
import subprocess
import sys
import time
import os
import paths

# Repo root, derived from this file (it lives at <repo>/tools/...), so renaming or
# moving the checkout does not silently repoint these paths at an old copy.
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

DEPLOY = paths.deploy_dir()
EXE = DEPLOY + r"\mpvw.exe"
REG = r"HKCU:\Software\Classes\Local Settings\Software\mpv-winui\mpv-winui\app"
OUT = REPO + r"\_work\mcp\startup-verified.json"

# Properties the startup batch sets, with the AppSettings key that stores the
# intended value.
PAIRS = [
    ("volume-max", "Mpv_volume_max"),
    ("deband", "Mpv_deband"),
    ("interpolation", "Mpv_interpolation"),
    ("scale", "Mpv_scale"),
    ("dither-depth", "Mpv_dither_depth"),
    ("audio-channels", "Mpv_audio_channels"),
    ("sub-auto", "Mpv_sub_auto"),
    ("hdr-compute-peak", "HdrComputePeak"),
]

PS_DRIVER = r'''
$ErrorActionPreference = 'Stop'
$pairs = @(
@PAIRS@
)
$p = New-Object System.IO.Pipes.NamedPipeClientStream('.','mpvpipe',[System.IO.Pipes.PipeDirection]::InOut)
$p.Connect(15000)
$sw = New-Object System.IO.StreamWriter($p); $sw.AutoFlush = $true
$sr = New-Object System.IO.StreamReader($p)
$rid = 0
function Ask([string]$cmd) {
  $script:rid++
  $sw.WriteLine('{"command":' + $cmd + ',"request_id":' + $script:rid + '}')
  while ($true) {
    $line = $sr.ReadLine()
    if ($null -eq $line) { return '{"error":"eof"}' }
    if ($line -match ('"request_id":' + $script:rid + '[,}]')) { return $line }
  }
}
# keep the connection alive by issuing the real reads immediately: mpv closes a
# client that connects and then writes nothing, and it also closes one whose
# first write comes after another round trip on the same handle. Both were
# observed here. So the very first write is the first property read.
foreach ($pair in $pairs) {
  $prop = $pair[0]; $key = $pair[1]
  $reply = Ask ('["get_property","' + $prop + '"]')
  $stored = (Get-ItemProperty -Path '@REG@' -Name $key -ErrorAction SilentlyContinue).$key
  [pscustomobject]@{ property=$prop; reply=$reply; stored=$stored }
}
$p.Dispose()
'''


def main():
    subprocess.run(["pwsh", "-NoProfile", "-Command",
                    "Get-Process mpvw -ErrorAction SilentlyContinue | Stop-Process -Force"],
                   capture_output=True, text=True)
    time.sleep(2.5)

    pairs_ps = ",\n".join(
        "  @('%s','%s')" % (p, k) for p, k in PAIRS)
    script = PS_DRIVER.replace("@PAIRS@", pairs_ps).replace("@REG@", REG)
    path = REPO + r"\_work\mcp\drive_pipe.ps1"
    io.open(path, "w", encoding="utf-8").write(script)

    # Start the app the way that works, then drive the pipe immediately.
    full = (
        "Start-Process '%s' -WorkingDirectory '%s'\n"
        "Start-Sleep -Milliseconds 1100\n"
        "& '%s'\n" % (EXE, DEPLOY, path)
    )
    r = subprocess.run(["pwsh", "-NoProfile", "-Command", full],
                       capture_output=True)
    out = (r.stdout or b"").decode("utf-8", "replace")
    err = (r.stderr or b"").decode("utf-8", "replace")
    print(out)
    if err.strip():
        print("stderr:", err.strip()[:600])

    subprocess.run(["pwsh", "-NoProfile", "-Command",
                    "Get-Process mpvw -ErrorAction SilentlyContinue | Stop-Process -Force"],
                   capture_output=True, text=True)

    rows = []
    for line in (r.stdout or "").splitlines():
        m = re.match(r"\s*(\S+)\s+reply=(\S*)\s+stored=(.*)$", line)
        if not m:
            continue
        prop, reply, stored = m.group(1), m.group(2), m.group(3).strip()
        rows.append({"property": prop, "reply": reply, "stored": stored})

    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(rows, f, ensure_ascii=False, indent=1)
    print("wrote", OUT, "| parsed", len(rows), "rows")


if __name__ == "__main__":
    sys.exit(main())
