$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$bin = Join-Path $root "bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64"
$mpv = "C:\Program Files\mpv-lazy\mpv.exe"
$video = "C:\Users\z2637\Documents\Codex\2026-08-04\mpv-lazy-hooke007-mpv-playkit-codex\work\trash\hdr_test\sdr30s.mp4"
$pipe = "mpvpipe"
$mpvLog = Join-Path $PSScriptRoot "m4-mpv.log"
$hostLog = Join-Path $PSScriptRoot "m4-host.log"
$evidence = Join-Path $PSScriptRoot "m4-evidence.txt"

foreach ($f in @($mpvLog, $hostLog, $evidence)) {
    if ([System.IO.File]::Exists($f)) { [System.IO.File]::Delete($f) }
}
Get-Process -Name mpv,MpvMenuHost -ErrorAction SilentlyContinue | ForEach-Object {
    Stop-Process -Id $_.Id -Force
    Write-Output ("cleanup stopped " + $_.ProcessName + " pid " + $_.Id)
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

# 1. 启动 mpv
$q = [char]34
$argStr = "--no-audio --force-window=yes --log-file=" + $q + $mpvLog + $q + " " + $q + $video + $q
$mpvProc = Start-Process -FilePath $mpv -ArgumentList $argStr -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2

# 2. 级联测试（v4.0 单进程）：宿主直接启动，test-select 0 → 进程内子菜单 → 叶子命令
$hostProc = Start-Process -FilePath (Join-Path $bin "MpvMenuHost.exe") -ArgumentList ("--items", (Join-Path $PSScriptRoot "cascade-menu.json"), "--pipe", $pipe, "--test-select", "0", "--test-delay-ms", "600", "--log", $hostLog) -PassThru -WindowStyle Hidden
$hostProc.WaitForExit(8000) | Out-Null
if ($hostProc.HasExited) {
    "parent host exited (code " + $hostProc.ExitCode + ")" | Out-File -FilePath $evidence -Encoding UTF8
} else {
    "parent host did not exit" | Out-File -FilePath $evidence -Encoding UTF8
}
Start-Sleep -Seconds 4

$left = Get-Process -Name MpvMenuHost -ErrorAction SilentlyContinue
if ($left) {
    "single-process cascade: hosts remaining after exit: " + ($left | Measure-Object).Count + " (expect 0)" | Out-File -FilePath $evidence -Append -Encoding UTF8
    $left | ForEach-Object { Stop-Process -Id $_.Id -Force }
}
else {
    "single-process cascade: no host remaining (clean)" | Out-File -FilePath $evidence -Append -Encoding UTF8
}

# 3. 验证 pause 已被叶子命令切换为 true
$pauseResp = ""
for ($i = 0; $i -lt 6 -and -not $pauseResp; $i++) {
    $pauseResp = Send-Ipc '{"command":["get_property","pause"],"request_id":2}'
    if (-not $pauseResp) { Start-Sleep -Milliseconds 500 }
}
"pause resp: " + [string]$pauseResp | Out-File -FilePath $evidence -Append -Encoding UTF8

# 4. 右键完整菜单接入宿主：script-binding dyn_menu/show → 应拉起宿主窗口
$showResp = ""
for ($i = 0; $i -lt 3 -and -not $showResp; $i++) {
    $showResp = Send-Ipc '{"command":["script-binding","dyn_menu/show"],"request_id":3}'
    if (-not $showResp) { Start-Sleep -Seconds 1 }
}
"show resp: " + [string]$showResp | Out-File -FilePath $evidence -Append -Encoding UTF8
$menuHost = $null
for ($i = 0; $i -lt 20 -and -not $menuHost; $i++) {
    Start-Sleep -Milliseconds 250
    $menuHost = Get-Process -Name MpvMenuHost -ErrorAction SilentlyContinue | Select-Object -First 1
}
if ($menuHost) {
    "right-click host launched (pid " + $menuHost.Id + ")" | Out-File -FilePath $evidence -Append -Encoding UTF8
    Stop-Process -Id $menuHost.Id -Force
}
else {
    "right-click host NOT launched" | Out-File -FilePath $evidence -Append -Encoding UTF8
}

# 5. 清理
Send-Ipc '{"command":["quit"],"request_id":4}' | Out-Null
Start-Sleep -Milliseconds 500
if (-not $mpvProc.WaitForExit(5000)) { Stop-Process -Id $mpvProc.Id -Force }

Write-Output "=== evidence ==="
Get-Content -LiteralPath $evidence -Encoding UTF8
Write-Output "=== host log ==="
if (Test-Path -LiteralPath $hostLog) { Get-Content -LiteralPath $hostLog -Encoding UTF8 } else { Write-Output "<none>" }
