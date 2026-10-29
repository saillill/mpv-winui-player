# Prove tools/test-control-panel-tabs.ps1 can fail, on the exact defect the
# second migration risked.
#
# The realistic failure is not "the tabs do nothing" -- that would be obvious.
# It is "the height stayed on the wrong element": the Pivot owned
# Height = PanelHeight - PanelChromeHeight, and the migration moved that onto a
# page Grid. If the assignment in OpenControlPanel keeps pointing at something
# that no longer sizes the pages, each tab sizes the panel from its own content
# and the panel jumps as you switch. That is the defect injected here.
#
# Two independent injections, because the two assertions guard different things:
#   1. drop the height assignment      -> panel height becomes content-driven
#   2. never swap page Visibility      -> every page stays visible at once
#
# Restores the source, rebuilds, redeploys and re-runs the green case at the
# end, so "restored" is demonstrated rather than claimed.
param(
    [string]$DeployDir = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'mpv-deploy')
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo

$source = (Resolve-Path 'src/mpvw-app/Domains/Playback/PlayerControl.ControlPanel.cs').Path
$bytes = [System.IO.File]::ReadAllBytes($source)
$original = [System.IO.File]::ReadAllText($source)

function Stop-App {
    Get-Process -Name mpvw -ErrorAction SilentlyContinue | ForEach-Object {
        $null = $_.CloseMainWindow()
        if (-not $_.WaitForExit(8000)) { $_.Kill(); $_.WaitForExit(5000) }
    }
}

function Write-Source {
    param([string]$Text)
    [System.IO.File]::WriteAllText($source, ($Text -replace "`n", "`r`n"),
        (New-Object System.Text.UTF8Encoding($false)))
}

function Rebuild-AndRun {
    & dotnet build src\mpvw-app\mpv-winui.csproj -c Release -p:Platform=x64 `
        -p:GenerateAppxPackageOnBuild=false -p:AppxPackageSigningEnabled=false -v:q 2>&1 |
        Select-Object -Last 2
    if ($LASTEXITCODE -ne 0) { throw 'the injected build failed, so it cannot test behaviour' }

    Stop-App
    $env:MPVW_DEPLOY_DIR = $DeployDir
    $null = & pwsh -NoProfile -File tools\deploy-test.ps1 2>&1

    $out = & pwsh -NoProfile -File tools\test-control-panel-tabs.ps1 2>&1 | Out-String
    return @{ Code = $LASTEXITCODE; Out = $out }
}

$failures = 0
$lf = $original -replace "`r`n", "`n"

try {
    # --- Injection 1: the height never reaches the page grid -----------------
    # BOTH places must go, and that was measured, not guessed. On the first run
    # of this script only the OpenControlPanel assignment was removed and the
    # panel height stayed at 897px for all three tabs -- because
    # EnsureControlPanel's own initializer already sets the same 420 on the
    # grid, and on a normal-height window the assignment recomputes exactly
    # 420. Removing one of the two is therefore a no-op at this window size,
    # and a check that "fails" on a no-op would be proving nothing. The real
    # defect is the height never being taken over at all.
    $heightAnchor = '            _panelPageHost.Height = Math.Max(200, panelHeight - PanelChromeHeight);'
    $initAnchor = '                Height = PanelHeight - PanelChromeHeight,'
    if (-not $lf.Contains($heightAnchor)) { throw 'height assignment anchor not found' }
    if (-not $lf.Contains($initAnchor)) { throw 'height initializer anchor not found' }
    Write-Source $lf.Replace($heightAnchor,
        '            // injected: height assignment removed').Replace(
        $initAnchor,
        '                // injected: initializer height removed')
    Write-Host '== injection 1: the 420px handover is gone =='
    $r1 = Rebuild-AndRun
    if ($r1.Code -ne 1) {
        Write-Host "BAD  injection 1: exit $($r1.Code), expected 1"
        Write-Host $r1.Out
        $failures++
    }
    else {
        Write-Host 'ok   injection 1: the panel test fails without the height handover'
        ($r1.Out -split "`n" | Where-Object { $_ -match '^(BAD|ok) ' }) | ForEach-Object { Write-Host "       $_" }
    }

    # --- Injection 2: pages never swap --------------------------------------
    # Kept as one line so it still compiles: a behaviour defect, not a syntax
    # error, is the only shape a runtime check can catch.
    $swapAnchor = '            SubtitleListView.Visibility = selected == SubtitleTab ? Visibility.Visible : Visibility.Collapsed;'
    $tracker = (Resolve-Path 'src/mpvw-app/Domains/Playback/PlayerTrackSelectorControl.xaml.cs').Path
    $trackerBytes = [System.IO.File]::ReadAllBytes($tracker)
    $swapAnchor = '            SubtitleListView.Visibility = selected == SubtitleTab ? Visibility.Visible : Visibility.Collapsed;'
    $panelSwap = '                for (var i = 0; i < items.Count; i++)'
    if (-not $lf.Contains($panelSwap)) { throw 'page-swap anchor not found' }
    $mutated2 = $lf.Replace($panelSwap,
        '                if (items.Count > 0) { selected = items[0]; }
                for (var i = 0; i < items.Count; i++)')
    # Force the loop to always show page 0 by pointing `selected` at it.
    Write-Source $mutated2
    Write-Host ''
    Write-Host '== injection 2: every tab keeps showing the first page =='
    $r2 = Rebuild-AndRun
    if ($r2.Code -ne 1) {
        Write-Host "BAD  injection 2: exit $($r2.Code), expected 1"
        Write-Host $r2.Out
        $failures++
    }
    else {
        Write-Host 'ok   injection 2: the panel test fails when tabs stop swapping pages'
        ($r2.Out -split "`n" | Where-Object { $_ -match '^(BAD|ok) ' }) | ForEach-Object { Write-Host "       $_" }
    }
}
finally {
    Stop-App
    [System.IO.File]::WriteAllBytes($source, $bytes)
    Write-Host ''
    Write-Host '== restored the source, rebuilding =='
    & dotnet build src\mpvw-app\mpv-winui.csproj -c Release -p:Platform=x64 `
        -p:GenerateAppxPackageOnBuild=false -p:AppxPackageSigningEnabled=false -v:q 2>&1 |
        Select-Object -Last 2
}

# Green case on the restored build.
Stop-App
$env:MPVW_DEPLOY_DIR = $DeployDir
$null = & pwsh -NoProfile -File tools\deploy-test.ps1 2>&1
$green = & pwsh -NoProfile -File tools\test-control-panel-tabs.ps1 2>&1 | Out-String
$greenCode = $LASTEXITCODE
Stop-App

if ($greenCode -ne 0) {
    Write-Host "BAD  restored build: panel test exit $greenCode, expected 0"
    Write-Host $green
    $failures++
}
else {
    Write-Host 'ok   restored build: panel test passes again'
}

Write-Host ''
if ($failures -gt 0) {
    Write-Host "FAIL: $failures case(s)"
    exit 1
}
Write-Host 'PASS: the panel test fails on both real defects and passes on the migrated build'
exit 0
