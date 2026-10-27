$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root "bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\MpvMenuHost.exe"

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class InputWin {
    [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
}
"@

$VK_DOWN = 0x28
$VK_UP = 0x26
$VK_RETURN = 0x0D
$VK_ESCAPE = 0x1B
$KEYEVENTF_KEYUP = 0x0002
$MOUSEEVENTF_LEFTDOWN = 0x0002
$MOUSEEVENTF_LEFTUP = 0x0004

function Send-Key {
    param([byte]$Vk)
    [InputWin]::keybd_event($Vk, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [InputWin]::keybd_event($Vk, 0, $KEYEVENTF_KEYUP, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
}

function Start-Host {
    $log = Join-Path $env:TEMP ("mpvhost-input-" + [guid]::NewGuid().ToString("N") + ".log")
    $p = Start-Process -FilePath $exe -ArgumentList @("--no-connect", "--pos", "300,300", "--log", $log) -PassThru
    Start-Sleep -Milliseconds 900
    return @($p, $log)
}

function Show-Log($Label, $Log, $P) {
    Write-Output ("--- " + $Label + " tail ---")
    if (Test-Path $Log) {
        Get-Content -LiteralPath $Log -Encoding UTF8 | Select-Object -Last 8 | ForEach-Object { Write-Output $_ }
    } else {
        Write-Output "<no host log>"
    }
    $crash = Join-Path (Split-Path -Parent $exe) "crash.log"
    if (Test-Path $crash) {
        Write-Output "--- crash.log ---"
        Get-Content -LiteralPath $crash -Encoding UTF8 | Select-Object -Last 8 | ForEach-Object { Write-Output $_ }
    } else {
        Write-Output "<no crash.log>"
    }
    Write-Output ("exit code: " + $P.ExitCode)
}

$failures = @()

# Case 1: DOWN DOWN ENTER -> select item idx=1 (前进 5 秒), host exits 0
$r = Start-Host
$p = $r[0]; $log = $r[1]
Send-Key -Vk $VK_DOWN
Send-Key -Vk $VK_DOWN
Send-Key -Vk $VK_RETURN
if (-not $p.WaitForExit(5000)) {
    Stop-Process -Id $p.Id -Force
    $failures += "kbd nav: host did not exit"
} elseif ($p.ExitCode -ne 0) {
    $failures += "kbd nav: exit code " + $p.ExitCode
} else {
    $txt = Get-Content -LiteralPath $log -Encoding UTF8 -ErrorAction SilentlyContinue
    if (-not ($txt -match "SELECT idx=1")) { $failures += "kbd nav: SELECT idx=1 not logged" }
}
if ($failures.Count -gt 0 -and ($failures[-1] -match "kbd nav")) { Show-Log "kbd nav" $log $p }
Remove-Item -LiteralPath $log -Force -ErrorAction SilentlyContinue

# Case 2: ESC closes without select
$r = Start-Host
$p = $r[0]; $log = $r[1]
Send-Key -Vk $VK_ESCAPE
if (-not $p.WaitForExit(5000)) {
    Stop-Process -Id $p.Id -Force
    $failures += "esc: host did not exit"
} elseif ($p.ExitCode -ne 0) {
    $failures += "esc: exit code " + $p.ExitCode
} else {
    $txt = Get-Content -LiteralPath $log -Encoding UTF8 -ErrorAction SilentlyContinue
    if ($txt -match "SELECT ") { $failures += "esc: unexpected SELECT" }
}
if ($failures.Count -gt 0 -and ($failures[-1] -match "^esc")) { Show-Log "esc" $log $p }
Remove-Item -LiteralPath $log -Force -ErrorAction SilentlyContinue

# Case 3: click outside window closes
$r = Start-Host
$p = $r[0]; $log = $r[1]
[InputWin]::SetCursorPos(50, 50) | Out-Null
Start-Sleep -Milliseconds 100
[InputWin]::mouse_event($MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
[InputWin]::mouse_event($MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
if (-not $p.WaitForExit(5000)) {
    Stop-Process -Id $p.Id -Force
    $failures += "click-outside: host did not exit"
} elseif ($p.ExitCode -ne 0) {
    $failures += "click-outside: exit code " + $p.ExitCode
} else {
    $txt = Get-Content -LiteralPath $log -Encoding UTF8 -ErrorAction SilentlyContinue
    if ($txt -match "SELECT ") { $failures += "click-outside: unexpected SELECT" }
}
if ($failures.Count -gt 0 -and ($failures[-1] -match "^click-outside")) { Show-Log "click-outside" $log $p }
Remove-Item -LiteralPath $log -Force -ErrorAction SilentlyContinue

if ($failures.Count -gt 0) {
    Write-Output "FAIL:"
    $failures | ForEach-Object { Write-Output ("  - " + $_) }
    exit 1
}
Write-Output "PASS (kbd nav / esc / click-outside)"
exit 0
