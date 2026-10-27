# Real right-click E2E: real mpv + deployed config -> dyn_menu.lua -> MpvMenuHost
# (v4.0: cold-start single host, no prewarm). Moves the mouse (SetCursorPos):
# do NOT run while the user is operating the machine.
$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class RcE2eWin {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
}
"@
[RcE2eWin]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null

$mpv = 'C:\Program Files\mpv-lazy\mpv.exe'
$video = 'C:\Users\z2637\Documents\Codex\2026-08-06\c-users-z2637-documents-codex-2026\work\audio_test_30s.mp4'
$pipe = 'mpvrc-e2e-' + $PID
$targetX = 900; $targetY = 700
$shot = Join-Path $PSScriptRoot 'rc-e2e-menu.png'
$evidence = Join-Path $PSScriptRoot 'rc-e2e-evidence.txt'

Get-Process -Name mpv,MpvMenuHost -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

function Send-Ipc([string]$line) {
    $resp = ''
    try {
        $c = New-Object System.IO.Pipes.NamedPipeClientStream('.', $pipe, [System.IO.Pipes.PipeDirection]::InOut, [System.IO.Pipes.PipeOptions]::Asynchronous)
        $c.Connect(3000)
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($line + [string][char]10)
        $c.Write($bytes, 0, $bytes.Length)
        $c.Flush()
        $buf = New-Object byte[] 4096
        $task = $c.ReadAsync($buf, 0, 4096)
        if ($task.Wait(3000)) {
            $n = $task.Result
            if ($n -gt 0) { $resp = [System.Text.Encoding]::UTF8.GetString($buf, 0, $n) }
        }
        $c.Dispose()
    } catch { $resp = '' }
    return $resp
}

[RcE2eWin]::SetCursorPos($targetX, $targetY) | Out-Null
$pt = New-Object RcE2eWin+POINT
[RcE2eWin]::GetCursorPos([ref]$pt) | Out-Null

$sw = [System.Diagnostics.Stopwatch]::StartNew()
$q = [char]34
$argStr = '--force-window=yes --input-ipc-server=' + $pipe + ' ' + $q + $video + $q
$mpvProc = Start-Process -FilePath $mpv -ArgumentList $argStr -PassThru -WindowStyle Hidden

$lines = @()
$lines += "mpv pid=$($mpvProc.Id) cursor=($($pt.X),$($pt.Y))"

# v4.0：无预热，直接测冷启动（script-binding -> 宿主可见）
Start-Sleep -Milliseconds 800
$swRestart = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Send-Ipc '{"command":["script-binding","dyn_menu/show"],"request_id":1}'
$visMs = -1
$visHwnd = [IntPtr]::Zero
for ($i = 0; $i -lt 150; $i++) {
    $h = Get-Process -Name MpvMenuHost -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if ($h -ne $null) {
        $visMs = $swRestart.ElapsedMilliseconds
        $visHwnd = $h.MainWindowHandle
        break
    }
    Start-Sleep -Milliseconds 20
}
$lines += "right-click resp=$resp cold-to-visible-ms=$visMs"

if ($visHwnd -ne [IntPtr]::Zero) {
    $r = New-Object RcE2eWin+RECT
    [RcE2eWin]::GetWindowRect($visHwnd, [ref]$r) | Out-Null
    $dpi = [RcE2eWin]::GetDpiForWindow($visHwnd)
    $inside = $pt.X -ge $r.Left -and $pt.X -lt $r.Right -and $pt.Y -ge $r.Top -and $pt.Y -lt $r.Bottom
    $lines += "host rect=($($r.Left),$($r.Top)) $($r.Right-$r.Left)x$($r.Bottom-$r.Top) dpi=$dpi cursorInside=$inside"

    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object System.Drawing.Bitmap ($r.Right - $r.Left), ($r.Bottom - $r.Top)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    [RcE2eWin]::PrintWindow($visHwnd, $hdc, 2) | Out-Null
    $g.ReleaseHdc($hdc)
    $g.Dispose()
    $bmp.Save($shot, [System.Drawing.Imaging.ImageFormat]::Png)
    $sum = 0.0; $sumSq = 0.0; $n = 0; $dark = 0
    for ($i = 0; $i -lt $bmp.Width; $i += 2) {
        for ($j = 0; $j -lt $bmp.Height; $j += 2) {
            $c = $bmp.GetPixel($i, $j)
            $lum = 0.2126 * $c.R + 0.7152 * $c.G + 0.0722 * $c.B
            $sum += $lum; $sumSq += $lum * $lum; $n++
            if ($lum -lt 120) { $dark++ }
        }
    }
    $bmp.Dispose()
    $mean = $sum / $n
    $std = [Math]::Sqrt([Math]::Max(0.0, $sumSq / $n - $mean * $mean))
    $lines += ("shot mean=" + [math]::Round($mean, 1) + " std=" + [math]::Round($std, 1) + " darkPx=$dark rendered=" + ($std -gt 8 -and $dark -gt 50))
} else {
    $lines += 'FAIL: menu not visible within 3s'
}

$lines | Set-Content -LiteralPath $evidence -Encoding UTF8
$lines
Get-Process -Name mpv,MpvMenuHost -ErrorAction SilentlyContinue | Stop-Process -Force

if ($lines -match 'FAIL') { exit 1 }
