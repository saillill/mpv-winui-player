param(
    [Parameter(Mandatory = $true)][int]$TargetPid,
    [Parameter(Mandatory = $true)][string]$OutPng
)

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32Cap {
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

$script:hwnd = [IntPtr]::Zero
$targetPid = [uint32]$TargetPid
$cb = [Win32Cap+EnumProc]{
    param($h, $l)
    $winPid = [uint32]0
    [Win32Cap]::GetWindowThreadProcessId($h, [ref]$winPid) | Out-Null
    if ($winPid -eq $targetPid) {
        $script:hwnd = $h
        return $false
    }
    return $true
}
[Win32Cap]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null

if ($script:hwnd -eq [IntPtr]::Zero) {
    Write-Output "NO WINDOW FOUND"
    exit 1
}

$rect = New-Object Win32Cap+RECT
[Win32Cap]::GetWindowRect($script:hwnd, [ref]$rect) | Out-Null
$w = $rect.Right - $rect.Left
$h = $rect.Bottom - $rect.Top
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
[Win32Cap]::PrintWindow($script:hwnd, $hdc, 2) | Out-Null
$g.ReleaseHdc($hdc)
$g.Dispose()
$bmp.Save($OutPng, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output ("CAPTURED " + $w + "x" + $h + " -> " + $OutPng)
