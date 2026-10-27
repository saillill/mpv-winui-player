param(
    [string]$Exe = "",
    [string]$Items = "",
    [string]$Theme = "",
    [int]$PosX = 200,
    [int]$PosY = 200
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not $Exe) {
    $Exe = Join-Path $root "bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\MpvMenuHost.exe"
}
$log = Join-Path $env:TEMP ("mpvhost-verify-" + [guid]::NewGuid().ToString("N") + ".log")

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class DpiVerifyWin {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
}
"@
# PerMonitorV2, so GetWindowRect matches physical pixels from DWM (no virtualization offset)
[DpiVerifyWin]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class VerifyWin {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int sz);
    public struct RECT { public int L, T, R, B; }
}
"@

$argsList = @("--no-connect", ("--pos" + " " + $PosX + "," + $PosY), "--log", $log)
if ($Items) { $argsList = @("--items", $Items) + $argsList }
if ($Theme) { $argsList = @("--theme", $Theme) + $argsList }
$proc = Start-Process -FilePath $Exe -ArgumentList $argsList -PassThru

# 等待 pass2 最终布局（临时尺寸首帧约 50ms 内完成）
Start-Sleep -Milliseconds 400
$script:hwnd = [IntPtr]::Zero
for ($i = 0; $i -lt 40 -and $script:hwnd -eq [IntPtr]::Zero; $i++) {
    Start-Sleep -Milliseconds 50
    $null = [VerifyWin]::EnumWindows({ param($h, $l)
        $pid2 = [uint32]0
        [VerifyWin]::GetWindowThreadProcessId($h, [ref]$pid2) | Out-Null
        if ($pid2 -eq $proc.Id -and [VerifyWin]::IsWindowVisible($h)) { $script:hwnd = $h; return $false }
        return $true
    }, [IntPtr]::Zero)
}

$failures = @()
if ($script:hwnd -eq [IntPtr]::Zero) {
    $failures += "no window found"
} else {
    $wr = New-Object VerifyWin+RECT
    $fr = New-Object VerifyWin+RECT
    [VerifyWin]::GetWindowRect($script:hwnd, [ref]$wr) | Out-Null
    [VerifyWin]::DwmGetWindowAttribute($script:hwnd, 9, [ref]$fr, [Runtime.InteropServices.Marshal]::SizeOf($fr)) | Out-Null
    $w = $wr.R - $wr.L
    $h = $wr.B - $wr.T
    $dL = $fr.L - $wr.L
    $dT = $fr.T - $wr.T
    $dR = $wr.R - $fr.R
    $dB = $wr.B - $fr.B
    Write-Output ("window: " + $w + "x" + $h + " at " + $wr.L + "," + $wr.T + " frame-diff=" + $dL + "," + $dT + "," + $dR + "," + $dB)
    if ($dL -ne 0 -or $dT -ne 0 -or $dR -ne 0 -or $dB -ne 0) {
        $failures += ("DWM frame diff non-zero: " + $dL + "," + $dT + "," + $dR + "," + $dB)
    }
    $style = [VerifyWin]::GetWindowLong($script:hwnd, -16)
    if (($style -band 0x00C00000) -ne 0) { $failures += "WS_CAPTION still set" }
    if (($style -band 0x00040000) -ne 0) { $failures += "WS_THICKFRAME still set" }
    if ($w -lt 300) { $failures += ("width below 300: " + $w) }

    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
    $win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
    if (-not $win) {
        $failures += "UIA window missing"
    } else {
        $all = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
        $count = 0
        foreach ($el in $all) { if ($el.Current.Name) { $count++ } }
        if ($count -lt 5) { $failures += ("UIA text nodes too few: " + $count) }
        Write-Output ("UIA text nodes: " + $count)
    }
}

Start-Sleep -Milliseconds 300
if (Test-Path $log) {
    $logText = Get-Content -LiteralPath $log -Encoding UTF8
    if (-not ($logText -match "ACRYLIC controller ok|ACRYLIC unsupported")) {
        $failures += "acrylic init not logged"
    }
    if ($Theme) {
        $expectDark = ($Theme -eq "dark")
        if (-not ($logText -match ("THEME dark=" + $expectDark.ToString()))) {
            $failures += "theme override not applied (expected dark=" + $expectDark + ")"
        }
        $expectTint = if ($expectDark) { "tint=0.55" } else { "tint=0.45" }
        if (-not ($logText -match $expectTint)) {
            $failures += "acrylic tint mismatch (expected " + $expectTint + ")"
        }
    }
    Write-Output ("host log: " + (($logText | Select-Object -Last 3) -join " | "))
} else {
    $failures += "host log missing"
}

Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $log -Force -ErrorAction SilentlyContinue

if ($failures.Count -gt 0) {
    Write-Output "FAIL:"
    $failures | ForEach-Object { Write-Output ("  - " + $_) }
    exit 1
}
Write-Output "PASS"
exit 0
