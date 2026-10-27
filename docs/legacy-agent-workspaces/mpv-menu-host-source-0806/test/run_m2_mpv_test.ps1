$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$bin = Join-Path $root "bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64"
$hostLog = Join-Path $PSScriptRoot "host-m2-mpv.log"
$mpv = "C:\Program Files\mpv-lazy\mpv.exe"
$video = "C:\Users\z2637\Documents\Codex\2026-08-04\mpv-lazy-hooke007-mpv-playkit-codex\work\trash\hdr_test\sdr30s.mp4"
$pipe = "mpv-e2e-test"
$mpvLog = Join-Path $PSScriptRoot "mpv-e2e.log"

if ([System.IO.File]::Exists($hostLog)) { [System.IO.File]::Delete($hostLog) }

$mpvProc = Start-Process -FilePath $mpv `
    -ArgumentList @("--no-audio", "--geometry=320x180", "--force-window=yes",
        "--input-ipc-server=\\.\pipe\$pipe", "--log-file=$mpvLog", $video) `
    -PassThru -WindowStyle Hidden
Start-Sleep -Milliseconds 1500

$hostProc = Start-Process -FilePath (Join-Path $bin "MpvMenuHost.exe") `
    -ArgumentList @(
        "--items", (Join-Path $PSScriptRoot "e2e-menu.json"),
        "--pipe", $pipe,
        "--test-select", "0",
        "--test-delay-ms", "500",
        "--log", $hostLog) `
    -PassThru -WindowStyle Hidden
if (-not $hostProc.WaitForExit(20000)) { Stop-Process -Id $hostProc.Id -Force; Write-Output "HOST TIMEOUT" }
else { Write-Output ("HOST EXIT " + $hostProc.ExitCode) }

# 通过 IPC 查询 pause 状态，验证宿主命令已生效
Write-Output ("DBG pipe=[" + $pipe + "]")
try {
    $client = New-Object System.IO.Pipes.NamedPipeClientStream(".", $pipe, [System.IO.Pipes.PipeDirection]::InOut, [System.IO.Pipes.PipeOptions]::Asynchronous)
}
catch {
    Write-Output ("DBG ctor error: " + $_.Exception.Message)
}
if ($null -eq $client) { Write-Output "DBG client is null after New-Object" }
else { Write-Output ("DBG client type: " + $client.GetType().FullName) }
$client.Connect(3000)
$resp = ""
$qbytes = [System.Text.Encoding]::UTF8.GetBytes('{"command":["get_property","pause"],"request_id":2}' + "`n")
$client.Write($qbytes, 0, $qbytes.Length)
$client.Flush()
$buf = New-Object byte[] 4096
try {
    $task = $client.ReadAsync($buf, 0, 4096)
    if ($task.Wait(3000)) { $n = $task.Result } else { $n = -1 }
    if ($n -gt 0) { $resp = [System.Text.Encoding]::UTF8.GetString($buf, 0, $n) }
}
catch {
    Write-Output ("DBG query read error: " + $_.Exception.Message)
}
$qbytes2 = [System.Text.Encoding]::UTF8.GetBytes('{"command":["quit"],"request_id":3}' + "`n")
$client.Write($qbytes2, 0, $qbytes2.Length)
$client.Flush()
Start-Sleep -Milliseconds 300
try { $client.Dispose() } catch { }

# 若 IPC 查询仍无响应，从 mpv 日志兜底断言
$pauseLine = ""
if (-not $resp) {
    if (Test-Path -LiteralPath $mpvLog) {
        $pauseLine = (Get-Content -LiteralPath $mpvLog -Encoding UTF8 | Select-String -Pattern "pause" | Select-Object -Last 1).Line
    }
}

Write-Output "=== host log ==="
if (Test-Path -LiteralPath $hostLog) { Get-Content -LiteralPath $hostLog -Encoding UTF8 }
Write-Output "=== mpv pause state response ==="
Write-Output ($resp.Trim())
if ($pauseLine) {
    Write-Output "=== mpv log pause line ==="
    Write-Output $pauseLine
}

if (-not $mpvProc.WaitForExit(5000)) { Stop-Process -Id $mpvProc.Id -Force }
