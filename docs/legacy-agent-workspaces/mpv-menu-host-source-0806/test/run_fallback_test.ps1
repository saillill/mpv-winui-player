$ErrorActionPreference = "Stop"
$mpv = "C:\Program Files\mpv-lazy\mpv.exe"
$trash = "C:\Users\z2637\Documents\Codex\2026-08-04\mpv-lazy-hooke007-mpv-playkit-codex\work\trash"
$video = Join-Path $trash "hdr_test\sdr30s.mp4"
$pipe = "mpvpipe"
$mpvLog = Join-Path $PSScriptRoot "fb-mpv.log"
$evidence = Join-Path $PSScriptRoot "fb-evidence.txt"

foreach ($f in @($mpvLog, $evidence)) {
    if ([System.IO.File]::Exists($f)) { [System.IO.File]::Delete($f) }
}
Get-Process -Name mpv,MpvMenuHost -ErrorAction SilentlyContinue | ForEach-Object {
    Stop-Process -Id $_.Id -Force
}

function Send-Ipc {
    param([string]$Line)
    $resp = ""
    try {
        $c = New-Object System.IO.Pipes.NamedPipeClientStream(".", $pipe, [System.IO.Pipes.PipeDirection]::InOut, [System.IO.Pipes.PipeOptions]::Asynchronous)
        $c.Connect(3000)
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Line + [string][char]10)
        $c.Write($bytes, 0, $bytes.Length)
        $c.Flush()
        $buf = New-Object byte[] 4096
        $task = $c.ReadAsync($buf, 0, 4096)
        if ($task.Wait(3000)) {
            $n = $task.Result
            if ($n -gt 0) { $resp = [System.Text.Encoding]::UTF8.GetString($buf, 0, $n) }
        }
        $c.Dispose()
    }
    catch { $resp = "" }
    return $resp
}

$q = [char]34
$argStr = "--no-audio --force-window=yes --log-file=" + $q + $mpvLog + $q + " --script-opts=dyn_menu-host_menu_path=" + $q + "C:\nonexistent\MpvMenuHost.exe" + $q + " " + $q + $video + $q
$mpvProc = Start-Process -FilePath $mpv -ArgumentList $argStr -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2

$resp = Send-Ipc '{"command":["script-message-to","dyn_menu","playlist-menu"],"request_id":1}'
"menu resp: " + $resp.Trim() | Out-File -FilePath $evidence -Encoding UTF8
Start-Sleep -Seconds 2

$hostProc = Get-Process -Name MpvMenuHost -ErrorAction SilentlyContinue
if ($hostProc) {
    "FALLBACK FAIL: MpvMenuHost spawned despite missing path" | Out-File -FilePath $evidence -Append -Encoding UTF8
    $hostProc | ForEach-Object { Stop-Process -Id $_.Id -Force }
}
else {
    "no host spawned (expected)" | Out-File -FilePath $evidence -Append -Encoding UTF8
}

$logText = ""
if (Test-Path $mpvLog) { $logText = Get-Content -LiteralPath $mpvLog -Encoding UTF8 }
$warn = $logText | Where-Object { $_ -match "MpvMenuHost.exe" -or $_ -match "回退原生" }
if ($warn) {
    $warn | Select-Object -Last 2 | ForEach-Object { Out-File -FilePath $evidence -Append -Encoding UTF8 -InputObject ("warn: " + $_) }
    "FALLBACK warn found" | Out-File -FilePath $evidence -Append -Encoding UTF8
}
else {
    "FALLBACK FAIL: no warning in mpv log" | Out-File -FilePath $evidence -Append -Encoding UTF8
}

$alive = -not $mpvProc.HasExited
if ($alive) {
    "mpv still alive (no crash)" | Out-File -FilePath $evidence -Append -Encoding UTF8
    Send-Ipc '{"command":["quit"],"request_id":2}' | Out-Null
    if (-not $mpvProc.WaitForExit(5000)) { Stop-Process -Id $mpvProc.Id -Force }
}
else {
    "FALLBACK FAIL: mpv crashed" | Out-File -FilePath $evidence -Append -Encoding UTF8
}

Write-Output "=== evidence ==="
Get-Content -LiteralPath $evidence -Encoding UTF8
$ok = -not (Get-Content -LiteralPath $evidence -Encoding UTF8 | Where-Object { $_ -match "FAIL" })
if ($ok) { Write-Output "PASS" } else { Write-Output "FAIL"; exit 1 }
