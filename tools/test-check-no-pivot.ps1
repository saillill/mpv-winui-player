# Injection test for tools/check-no-pivot.py.
#
# Rule 1 in AGENTS.md: a checker must be seen failing on a REAL violation, not a
# synthetic one. Each case below reintroduces the exact shape the migration
# removed, runs the checker, and requires exit 1 plus the expected message.
# Case 0 is the control: on the migrated tree the checker must pass.
#
# Every case restores the file in a finally block, and the script verifies the
# restore by re-running the control at the end. A test that leaves the tree
# dirty would poison every later run in this repo.

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo

$failures = 0

function Invoke-Checker {
    $out = & python tools\check-no-pivot.py 2>&1 | Out-String
    return @{ Code = $LASTEXITCODE; Out = $out }
}

# Read a file as its exact bytes, plus an LF view of the text for anchors.
# The injections below are written with LF; the repo files are CRLF. Comparing
# LF against CRLF is why five anchors "were not found" on the first run of this
# script, so the normalisation is explicit instead of incidental.
function Read-Source {
    param([string]$Path)
    $full = (Resolve-Path $Path).Path
    $bytes = [System.IO.File]::ReadAllBytes($full)
    $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    $raw = [System.IO.File]::ReadAllText($full).TrimStart([char]0xFEFF)
    $usesCrlf = $raw.Contains("`r`n")
    return @{
        Full   = $full
        Bytes  = $bytes
        Bom    = $hasBom
        Crlf   = $usesCrlf
        LfText = ($raw -replace "`r`n", "`n")
    }
}

function Write-Source {
    param($Source, [string]$LfText)
    $text = if ($Source.Crlf) { $LfText -replace "`n", "`r`n" } else { $LfText }
    [System.IO.File]::WriteAllText($Source.Full, $text, (New-Object System.Text.UTF8Encoding($Source.Bom)))
}

function Restore-Source {
    param($Source)
    [System.IO.File]::WriteAllBytes($Source.Full, $Source.Bytes)
}

function Test-Case {
    param([string]$Name, [string]$Path, [string]$Find, [string]$Replace, [string]$Expect)

    $src = Read-Source $Path
    $lf = $src.LfText
    if (-not $lf.Contains($Find)) {
        Write-Host "BAD  $Name : injection anchor not found in $Path"
        $script:failures++
        return
    }
    try {
        Write-Source $src $lf.Replace($Find, $Replace)
        $r = Invoke-Checker
    }
    finally {
        Restore-Source $src
    }

    if ($r.Code -ne 1) {
        Write-Host "BAD  $Name : checker exit $($r.Code), expected 1"
        Write-Host $r.Out
        $script:failures++
        return
    }
    if (-not $r.Out.Contains($Expect)) {
        Write-Host "BAD  $Name : exit 1 but message missing '$Expect'"
        Write-Host $r.Out
        $script:failures++
        return
    }
    Write-Host "ok   $Name (exit 1, reported '$Expect')"
}

# 0. Control: the migrated tree passes.
$control = Invoke-Checker
if ($control.Code -ne 0) {
    Write-Host "BAD  control: migrated tree should pass, got exit $($control.Code)"
    Write-Host $control.Out
    $failures++
}
else {
    Write-Host "ok   control: migrated tree passes"
}

$trackXaml = 'src\mpvw-app\Domains\Playback\PlayerTrackSelectorControl.xaml'
$panelCs = 'src\mpvw-app\Domains\Playback\PlayerControl.ControlPanel.cs'

# 1. XAML Pivot, tag name followed by newline (the shape that beat the old
#    regexes, which required a delimiter after the name on the same line).
Test-Case -Name 'xaml: <Pivot newline' -Path $trackXaml `
    -Find '            <SelectorBar
                x:Name="TrackSelectorBar"' `
    -Replace '            <Pivot
                x:Name="TrackSelectorBar"' `
    -Expect '<Pivot'

# 2. XAML Pivot with an xmlns prefix.
Test-Case -Name 'xaml: <controls:Pivot' -Path $trackXaml `
    -Find '            <SelectorBar
                x:Name="TrackSelectorBar"' `
    -Replace '            <controls:Pivot
                x:Name="TrackSelectorBar"' `
    -Expect '<controls:Pivot'

# 3. PivotItem in markup.
Test-Case -Name 'xaml: <PivotItem' -Path $trackXaml `
    -Find '                <SelectorBarItem x:Name="SubtitleTab" IsSelected="True">' `
    -Replace '                <PivotItem x:Name="SubtitleTab" IsSelected="True">' `
    -Expect '<PivotItem'

# 4. Runtime Pivot with an object initializer and NO parenthesis: the exact
#    shape three earlier "proofs" missed, because they searched for 'new Pivot('.
Test-Case -Name 'cs: new Pivot {' -Path $panelCs `
    -Find '            var bar = new SelectorBar
            {' `
    -Replace '            var bar = new Pivot
            {' `
    -Expect 'new Pivot'

# 5. Runtime PivotItem with an object initializer.
Test-Case -Name 'cs: new PivotItem {' -Path $panelCs `
    -Find '                var item = new SelectorBarItem
                {' `
    -Replace '                var item = new PivotItem
                {' `
    -Expect 'new PivotItem'

# 6. The vacuity guard: rename the control so no SelectorBar is found in the
#    XAML it was migrated to. A pattern that silently stops matching looks
#    exactly like this, and the replacement assertion must catch it.
Test-Case -Name 'xaml: replacement missing' -Path $trackXaml `
    -Find '            <SelectorBar
                x:Name="TrackSelectorBar"' `
    -Replace '            <Border
                x:Name="TrackSelectorBar"' `
    -Expect 'no SelectorBar found'

# 7. The Child trap. WinUI 2.3.9's SelectorBarItem template presents only Icon
#    and Text, so Child renders nothing and raises nothing.
Test-Case -Name 'xaml: SelectorBarItem.Child' -Path $trackXaml `
    -Find '                    <SelectorBarItem.Icon>' `
    -Replace '                    <SelectorBarItem.Child><TextBlock /></SelectorBarItem.Child>
                    <SelectorBarItem.Icon>' `
    -Expect 'SelectorBarItem.Child'

# 8. Prose must NOT trip the checker: a comment naming the old control is
#    history, not a regression. If this ever fails, the checker is reading
#    comments as code.
$full = (Resolve-Path $panelCs).Path
$src = Read-Source $panelCs
$lf = $src.LfText
try {
    Write-Source $src $lf.Replace(
            '            var bar = new SelectorBar',
            '            // new Pivot was here; the SelectorBar replaced it.
            var bar = new SelectorBar')
    $r = Invoke-Checker
}
finally {
    Restore-Source $src
}
if ($r.Code -ne 0) {
    Write-Host "BAD  prose: a comment mentioning 'new Pivot' failed the checker"
    Write-Host $r.Out
    $failures++
}
else {
    Write-Host "ok   prose: comments naming the old control are ignored"
}

# Final control: every file above was restored byte-for-byte and the tree is
# still green.
$final = Invoke-Checker
if ($final.Code -ne 0) {
    Write-Host "BAD  restore: tree is dirty after the injection run"
    Write-Host $final.Out
    $failures++
}
else {
    Write-Host "ok   restore: tree is clean and green after every case"
}

$dirty = (& git status --porcelain -- 'src/' | Out-String).Trim()
if ($dirty.Length -gt 0) {
    # The migration itself is uncommitted, so files are expected to be
    # modified; what must not appear is a change to a file this test only
    # touched in memory.
    Write-Host "note: uncommitted changes present (expected for the migration):"
    Write-Host $dirty
}

Write-Host ""
if ($failures -gt 0) {
    Write-Host "FAIL: $failures case(s)"
    exit 1
}
Write-Host "PASS: checker fails on every real violation and passes on the migrated tree"
exit 0
