$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$bin = Join-Path $root "bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64"
$mpv = "C:\Program Files\mpv-lazy\mpv.exe"
$trash = "C:\Users\z2637\Documents\Codex\2026-08-04\mpv-lazy-hooke007-mpv-playkit-codex\work\trash"
$v1 = Join-Path $trash "hdr_test\sdr30s.mp4"
$v2 = "C:\Users\z2637\Documents\Codex\2026-08-06\c-users-z2637-documents-codex-2026\work\audio_test_30s.mp4"
$pipe = "mpvpipe"
$mpvLog = Join-Path $PSScriptRoot "m3-mpv.log"
$hostLog = Join-Path $PSScriptRoot "m3-host.log"
$shotPng = Join-Path $PSScriptRoot "m3-host-menu.png"
$evidence = Join-Path $PSScriptRoot "m3-evidence.txt"

foreach ($f in @($mpvLog, $hostLog, $shotPng, $evidence)) {
    if ([System.IO.File]::Exists($f)) { [System.IO.File]::Delete($f) }
}

Get-Process -Name mpv,MpvMenuHost -ErrorAction SilentlyContinue | ForEach-Object {
    Stop-Process -Id $_.Id -Force
    Write-Output ("cleanup stopped " + $_.ProcessName + " pid " + $_.Id)
}

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class M3Win {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

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
    catch {
        $resp = ""
    }
    return $resp
}

# 1. 启动 mpv（真实配置 + 测试钩子：host_test_select=1 让宿主自动点第 2 项）
$q = [char]34
$argStr = "--no-audio --force-window=yes --log-file=" + $q + $mpvLog + $q + " --script-opts=dyn_menu-host_test_select=1,dyn_menu-host_log=" + $q + $hostLog + $q + " " + $q + $v1 + $q + " " + $q + $v2 + $q
$mpvProc = Start-Process -FilePath $mpv -ArgumentList $argStr -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2

# 2. 触发播放列表菜单（dyn_menu 应拉起 WinUI 宿主，宿主自动点击第 2 项）
$resp = Send-Ipc '{"command":["script-message-to","dyn_menu","playlist-menu"],"request_id":1}'
"open-menu resp: " + $resp.Trim() | Out-File -FilePath $evidence -Encoding UTF8

# 宿主窗口只在自动点击前存活（test-select 约 800ms 后自选退出），50ms 紧轮询抓取
$hwnd = [IntPtr]::Zero
for ($i = 0; $i -lt 60 -and $hwnd -eq [IntPtr]::Zero; $i++) {
    $hp = Get-Process -Name MpvMenuHost -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($hp) {
        $hp.Refresh()
        $hwnd = $hp.MainWindowHandle
    }
    if ($hwnd -eq [IntPtr]::Zero) { Start-Sleep -Milliseconds 50 }
}
if ($hwnd -ne [IntPtr]::Zero) {
    $hostProc = Get-Process -Name MpvMenuHost -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($hostProc) {
        $rect = New-Object M3Win+RECT
        [M3Win]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
        $w = $rect.Right - $rect.Left
        $h = $rect.Bottom - $rect.Top
        "host window: " + $w + "x" + $h + " at " + $rect.Left + "," + $rect.Top | Out-File -FilePath $evidence -Append -Encoding UTF8
        $bmp = New-Object System.Drawing.Bitmap($w, $h)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $hdc = $g.GetHdc()
        [M3Win]::PrintWindow($hwnd, $hdc, 2) | Out-Null
        $g.ReleaseHdc($hdc)
        $g.Dispose()
        $bmp.Save($shotPng, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        "host screenshot saved" | Out-File -FilePath $evidence -Append -Encoding UTF8
    }
}
else {
    "HOST WINDOW NOT FOUND" | Out-File -FilePath $evidence -Append -Encoding UTF8
}
$hostProc = Get-Process -Name MpvMenuHost -ErrorAction SilentlyContinue | Select-Object -First 1
if ($hostProc) {
    $hostProc.WaitForExit(5000) | Out-Null
    if (-not $hostProc.HasExited) {
        "host still running after test-select (v4.0 expects clean exit; abnormal if persists)" | Out-File -FilePath $evidence -Append -Encoding UTF8
        Stop-Process -Id $hostProc.Id -Force
    }
    else {
        if ($hostProc.ExitCode -eq 0) {
            "host exited cleanly (code 0)" | Out-File -FilePath $evidence -Append -Encoding UTF8
        }
        else {
            "host exited with code " + $hostProc.ExitCode | Out-File -FilePath $evidence -Append -Encoding UTF8
        }
    }
}
else {
    "NO HOST PROCESS" | Out-File -FilePath $evidence -Append -Encoding UTF8
}

# 3. 验证播放列表已切到第 2 项（playlist-pos = 1）
$posResp = ""
for ($i = 0; $i -lt 6 -and -not $posResp; $i++) {
    $posResp = Send-Ipc '{"command":["get_property","playlist-pos"],"request_id":2}'
    if (-not $posResp) { Start-Sleep -Milliseconds 500 }
}
"playlist-pos resp: " + [string]$posResp | Out-File -FilePath $evidence -Append -Encoding UTF8

# 4. 清理
Send-Ipc '{"command":["quit"],"request_id":3}' | Out-Null
Start-Sleep -Milliseconds 500
if (-not $mpvProc.WaitForExit(5000)) { Stop-Process -Id $mpvProc.Id -Force }

Write-Output "=== evidence ==="
Get-Content -LiteralPath $evidence -Encoding UTF8
Write-Output "=== host log ==="
if (Test-Path -LiteralPath $hostLog) { Get-Content -LiteralPath $hostLog -Encoding UTF8 } else { Write-Output "<none>" }
