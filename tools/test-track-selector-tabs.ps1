# Walk all four track-selector tabs and assert which list is on screen.
#
# This is the runtime half of the Pivot->SelectorBar migration check. The
# static checker (tools/check-no-pivot.py) can only prove the control is gone;
# whether the four pages still swap is a property of the running app, which is
# why PIVOT-MIGRATION-NOTES.md calls it a human-observed step.
#
# What makes this different from "the window is up": it asserts, per tab, BOTH
# that the expected list is present AND that the other three are absent. A
# handler that never fires leaves all four visible, and a handler that shows
# everything would pass a presence-only assertion.
#
# Requires the app to be running with the track selector popup open.
param(
    [Parameter(Mandatory = $true)][string]$PopupHwnd
)

$ErrorActionPreference = 'Stop'

$tabs = @(
    @{ Item = 'SubtitleTab'; List = 'SubtitleListView'; Caption = 'Subtitle' },
    @{ Item = 'SecondSubtitleTab'; List = 'SecondSubListView'; Caption = 'SecondSubtitle' },
    @{ Item = 'VideoTab'; List = 'VideoListView'; Caption = 'Video' },
    @{ Item = 'AudioTab'; List = 'AudioListView'; Caption = 'Audio' }
)
$allLists = $tabs | ForEach-Object { $_.List }

$failures = 0
foreach ($tab in $tabs) {
    & winapp ui invoke $tab.Item -a mpvw -w $PopupHwnd 2>&1 | Out-Null
    Start-Sleep -Milliseconds 800

    # Depth 4 is enough to see the sibling lists under the page grid without
    # expanding every row's contents.
    $tree = & winapp ui inspect -a mpvw -w $PopupHwnd --depth 4 2>&1 | Out-String

    $seen = @()
    foreach ($name in $allLists) {
        if ($tree -match "\b$name\b") { $seen += $name }
    }

    $expected = $tab.List
    $missing = $seen -notcontains $expected
    $extra = $seen | Where-Object { $_ -ne $expected }

    if ($missing) {
        Write-Host "BAD  $($tab.Caption): $expected is not on screen (seen: $($seen -join ', '))"
        $script:failures++
    }
    elseif ($extra) {
        Write-Host "BAD  $($tab.Caption): $expected is shown but so are $($extra -join ', ')"
        $script:failures++
    }
    else {
        Write-Host "ok   $($tab.Caption): only $expected is on screen"
    }
}

Write-Host ""
if ($failures -gt 0) {
    Write-Host "FAIL: $failures tab(s)"
    exit 1
}
Write-Host "PASS: all four tabs swap exactly one page in and the other three out"
exit 0
