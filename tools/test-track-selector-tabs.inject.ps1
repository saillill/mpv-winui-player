# Prove tools/test-track-selector-tabs.ps1 can fail.
#
# AGENTS.md rule 1, applied to a runtime check this time: a checker that has
# never been seen red has unknown blind spots. The green run above proves very
# little on its own -- a `inspect | Select-String` that never matches anything
# also prints nothing and exits 0 if the assertion is written carelessly.
#
# The injection is the realistic defect, not a synthetic one: the
# SelectionChanged handler returns before it swaps any page, which is exactly
# what happens if the handler is wired to the wrong element or the comparison
# stops matching. With that in place all four lists stay collapsed except the
# first, so the three "only X is on screen" assertions must fail.
#
# Restores the source, rebuilds, redeploys, and re-runs the green case, so the
# final state is proven, not assumed.
param(
    [string]$DeployDir = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'mpv-deploy')
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo

$source = Join-Path $repo 'src/mpvw-app/Domains/Playback/PlayerTrackSelectorControl.xaml.cs'
$source = (Resolve-Path $source).Path
$bytes = [System.IO.File]::ReadAllBytes($source)
$original = [System.IO.File]::ReadAllText($source)

function Stop-App {
    $procs = Get-Process -Name mpvw -ErrorAction SilentlyContinue
    foreach ($p in $procs) {
        $null = $p.CloseMainWindow()
        if (-not $p.WaitForExit(8000)) { $p.Kill(); $p.WaitForExit(5000) }
    }
}

function Start-AppAndOpenSelector {
    $exe = Join-Path $DeployDir 'mpvw.exe'
    $null = Start-Process -FilePath $exe -PassThru

    # The button's UIA slug is regenerated per process, and a truncated slug
    # (`btn-trackselectionb`) does not resolve: invoke then fails, no popup
    # opens, and the failure looks like a layout bug. Read the full slug out of
    # the search output instead of guessing it.
    $slug = $null
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 500
        $hit = & winapp ui search 'trackselection' -a mpvw 2>&1 | Out-String
        $m = [regex]::Match($hit, '(btn-trackselection\S*)')
        if ($m.Success) { $slug = $m.Groups[1].Value; break }
    }
    if (-not $slug) { throw 'the track-selector button never showed up in the UIA tree' }

    $invoke = & winapp ui invoke $slug -a mpvw 2>&1 | Out-String
    if ($invoke -notmatch 'Invoked') { throw "invoking $slug failed: $invoke" }

    # The popup HWND changes every open; look it up rather than remembering it.
    for ($i = 0; $i -lt 25; $i++) {
        Start-Sleep -Milliseconds 400
        $w = & winapp ui list-windows -a mpvw 2>&1 | Out-String
        # Skip the "Auto-selected ... " chatter: only the listing lines have
        # an HWND followed by a popup/window kind.
        $m = [regex]::Match($w, 'HWND (\d+): "[^"]*" \(popup')
        if ($m.Success) { return $m.Groups[1].Value }
    }
    throw 'the track selector popup never appeared'
}

function Invoke-TabTest {
    param([string]$Hwnd)
    $out = & pwsh -NoProfile -File tools\test-track-selector-tabs.ps1 -PopupHwnd $Hwnd 2>&1 | Out-String
    return @{ Code = $LASTEXITCODE; Out = $out }
}

$failures = 0
$injected = $false
try {
    # LF-normalised anchor: the file is CRLF and a here-string is LF.
    $lf = $original -replace "`r`n", "`n"
    $anchor = '            var selected = sender.SelectedItem;'
    if (-not $lf.Contains($anchor)) { throw "injection anchor not found in $source" }

    # Return before the assignments. Kept as one line so the file still
    # compiles: this is a behaviour defect, not a syntax error, which is the
    # only shape that can reach a runtime check.
    $mutated = $lf.Replace($anchor, '            if (sender.SelectedItem is not null) { return; }
            var selected = sender.SelectedItem;')
    [System.IO.File]::WriteAllText($source, ($mutated -replace "`n", "`r`n"), (New-Object System.Text.UTF8Encoding($true)))
    $injected = $true

    Write-Host '== rebuilding with the handler disabled =='
    & dotnet build src\mpvw-app\mpv-winui.csproj -c Release -p:Platform=x64 `
        -p:GenerateAppxPackageOnBuild=false -p:AppxPackageSigningEnabled=false -v:q 2>&1 |
        Select-Object -Last 3
    if ($LASTEXITCODE -ne 0) { throw 'the injected build failed, so it cannot test behaviour' }

    Stop-App
    $env:MPVW_DEPLOY_DIR = $DeployDir
    $null = & pwsh -NoProfile -File tools\deploy-test.ps1 2>&1
    $hwnd = Start-AppAndOpenSelector
    $red = Invoke-TabTest -Hwnd $hwnd

    if ($red.Code -ne 1) {
        Write-Host "BAD  injected defect: tab test exit $($red.Code), expected 1"
        Write-Host $red.Out
        $failures++
    }
    else {
        $badLines = ($red.Out -split "`n" | Where-Object { $_ -match '^BAD ' }).Count
        Write-Host "ok   injected defect: tab test fails ($badLines BAD line(s))"
    }
}
finally {
    Stop-App
    if ($injected) {
        [System.IO.File]::WriteAllBytes($source, $bytes)
        Write-Host '== restored the source, rebuilding =='
        & dotnet build src\mpvw-app\mpv-winui.csproj -c Release -p:Platform=x64 `
            -p:GenerateAppxPackageOnBuild=false -p:AppxPackageSigningEnabled=false -v:q 2>&1 |
            Select-Object -Last 3
    }
}

# Green case again on the restored build, so "restored" is demonstrated.
$env:MPVW_DEPLOY_DIR = $DeployDir
$null = & pwsh -NoProfile -File tools\deploy-test.ps1 2>&1
$hwnd = Start-AppAndOpenSelector
$green = Invoke-TabTest -Hwnd $hwnd
Stop-App

if ($green.Code -ne 0) {
    Write-Host "BAD  restored build: tab test exit $($green.Code), expected 0"
    Write-Host $green.Out
    $failures++
}
else {
    Write-Host 'ok   restored build: tab test passes again'
}

$dirty = (& git status --porcelain -- 'src/mpvw-app/Domains/Playback/PlayerTrackSelectorControl.xaml.cs' | Out-String).Trim()
if ($dirty -match '^ M') { Write-Host "note: source still differs from the index (the migration itself): $dirty" }

Write-Host ''
if ($failures -gt 0) {
    Write-Host "FAIL: $failures case(s)"
    exit 1
}
Write-Host 'PASS: the runtime tab check fails on a real defect and passes on the migrated build'
exit 0
