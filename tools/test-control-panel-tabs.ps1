# Walk the three quick-control tabs and assert (a) each tab puts ITS page on
# screen and the other two pages away, and (b) the panel's height does not move
# when tabs change.
#
# (b) is the entire risk of migrating the second Pivot. The Pivot WAS the 420px
# container: it carried Height = PanelHeight - PanelChromeHeight and scrolled
# each page inside that box. A SelectorBar is only the strip, so the height
# moved onto a page Grid. Wrong handover = the panel grows and shrinks as you
# switch tabs: visible on screen, invisible to every static checker here.
#
# (a) uses text that only one page builds (measured on this build, see the
# per-tab markers). Asserting "some controls exist" would pass on a panel where
# nothing swaps at all, because the strip and its captions are always present.
#
# Operational facts this script has to respect, both learned by watching it
# fail:
#   * the popup is light-dismiss, so it closes between commands that touch the
#     desktop. Every step re-opens it and re-reads the HWND instead of caching.
#   * UIA slugs are per-process. Tabs are addressed by their visible caption,
#     resolved fresh each time.
#   * the tab ListItems sit 6 levels down, under PART_ScrollPresenter, so the
#     tree has to be read at depth 8 or they come back as "+more".
param(
    [string]$DeployDir = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'mpv-deploy')
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo

# Markers measured on the migrated build: the equalizer band row exists only on
# the audio page, the picture adjustments only on video, the caption card only
# on subtitles. Each list also names a marker the OTHER pages must not show.
$tabs = @(
    @{ Caption = '声音'; Own = @('均衡器预设', '音频输出设备'); Foreign = @('字幕字体', '对比度') },
    @{ Caption = '画面'; Own = @('对比度', '饱和度'); Foreign = @('均衡器预设', '字幕字体') },
    @{ Caption = '字幕'; Own = @('字幕字体', '字幕加粗'); Foreign = @('均衡器预设', '对比度') }
)

function Stop-App {
    Get-Process -Name mpvw -ErrorAction SilentlyContinue | ForEach-Object {
        $null = $_.CloseMainWindow()
        if (-not $_.WaitForExit(8000)) { $_.Kill(); $_.WaitForExit(5000) }
    }
}

function Get-Popup {
    $w = & winapp ui list-windows -a mpvw 2>&1 | Out-String
    $m = [regex]::Match($w, 'HWND (\d+): "[^"]*" \(popup, (\d+)x(\d+)')
    if (-not $m.Success) { return $null }
    return @{ Hwnd = $m.Groups[1].Value; W = [int]$m.Groups[2].Value; H = [int]$m.Groups[3].Value }
}

function Get-TabSlug {
    param([string]$Hwnd, [string]$Caption)
    $tree = & winapp ui inspect -a mpvw -w $Hwnd --depth 8 2>&1 | Out-String
    $m = [regex]::Match($tree, "(?m)^\s*(\S+) ListItem `"$([regex]::Escape($Caption))`"")
    if ($m.Success) { return $m.Groups[1].Value }
    return $null
}

function Open-Panel {
    # Dismiss whatever is open, then toggle the panel open.
    if (Get-Popup) {
        $null = & winapp ui send-keys 'escape' -a mpvw 2>&1
        Start-Sleep -Milliseconds 700
    }

    $btn = $null
    for ($i = 0; $i -lt 30; $i++) {
        $hit = & winapp ui search 'ControlPanelButton' -a mpvw 2>&1 | Out-String
        $m = [regex]::Match($hit, '(?m)^\s*(\S+) Button')
        if ($m.Success) { $btn = $m.Groups[1].Value; break }
        Start-Sleep -Milliseconds 400
    }
    if (-not $btn) { throw 'the control-panel button is not in the UIA tree' }

    $null = & winapp ui invoke $btn -a mpvw 2>&1
    for ($i = 0; $i -lt 25; $i++) {
        Start-Sleep -Milliseconds 400
        $p = Get-Popup
        if ($p) { return $p }
    }
    throw 'the control panel never opened'
}

Stop-App
$env:MPVW_DEPLOY_DIR = $DeployDir
$null = Start-Process -FilePath (Join-Path $DeployDir 'mpvw.exe') -PassThru
Start-Sleep -Seconds 6

$failures = 0
$heights = @()
$rows = @()

foreach ($tab in $tabs) {
    $popup = Open-Panel
    $slug = Get-TabSlug -Hwnd $popup.Hwnd -Caption $tab.Caption

    if (-not $slug) {
        Write-Host "BAD  $($tab.Caption): no tab matched"
        $failures++
        continue
    }

    $null = & winapp ui invoke $slug -a mpvw -w $popup.Hwnd 2>&1
    Start-Sleep -Milliseconds 1000

    # Re-read the window size AFTER the switch: a height handover bug shows up
    # here, as a different popup height per tab.
    $after = Get-Popup
    if (-not $after) {
        Write-Host "BAD  $($tab.Caption): the panel closed when the tab was clicked"
        $failures++
        continue
    }

    $tree = & winapp ui inspect -a mpvw -w $after.Hwnd --depth 8 2>&1 | Out-String

    $missing = $tab.Own | Where-Object { $tree -notmatch [regex]::Escape($_) }
    $leaked = $tab.Foreign | Where-Object { $tree -match [regex]::Escape($_) }

    if ($missing) {
        Write-Host "BAD  $($tab.Caption): its own page is not on screen (missing: $($missing -join ', '))"
        $failures++
    }
    elseif ($leaked) {
        Write-Host "BAD  $($tab.Caption): another page is still visible ($($leaked -join ', '))"
        $failures++
    }
    else {
        Write-Host "ok   $($tab.Caption): only its own page is on screen"
    }

    $heights += $after.H
    $rows += "     $($tab.Caption) -> popup $($after.W)x$($after.H)px"
}

Write-Host '== panel size per tab =='
$rows | ForEach-Object { Write-Host $_ }

Stop-App

if ($heights.Count -ge 2) {
    $min = ($heights | Measure-Object -Minimum).Minimum
    $max = ($heights | Measure-Object -Maximum).Maximum
    # 4px of slack out of ~897: a page that needs a vertical scrollbar can
    # shave a pixel or two, and the acrylic border can round differently.
    # A genuine height-handover bug moves this by hundreds, so the tolerance is
    # nowhere near the size of the failure being tested for.
    $spread = $max - $min
    if ($spread -gt 4) {
        Write-Host "BAD  panel height moves by ${spread}px across tabs (min $min, max $max): the 420px handover to the page grid is wrong"
        $failures++
    }
    else {
        Write-Host "ok   panel height stable across $($heights.Count) tabs (spread ${spread}px, min $min, max $max)"
    }
}
elseif ($heights.Count -lt 2) {
    Write-Host "BAD  only $($heights.Count) tab(s) produced a height; the comparison is meaningless"
    $failures++
}

Write-Host ''
if ($failures -gt 0) {
    Write-Host "FAIL: $failures finding(s)"
    exit 1
}
Write-Host 'PASS: three tabs each show their own page and the panel height does not move'
exit 0
