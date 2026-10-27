# Official-tooling check: the parts of verify_exe.py that `winapp ui` replaces
#
# Why this file exists: the user's instruction was "prefer the official WinUI
# plugin/tools to perform checks, avoid depending on self-written programs". The
# previous round verified the built exe with `_rebuild-20261026/verify_exe.py`, ~200
# lines of hand-rolled UIA walking over `uiautomation`. Measured 2026-10-27: `winapp
# ui` covers the reading half of that script natively, in three commands.
#
# This is the official replacement, kept in the repo so the next round uses it instead
# of rewriting the Python. It is deliberately NOT a Python wrapper around winapp --
# that would be the same mistake with an extra layer.
#
# What still needs a process check, and why it is not in here: `winapp ui wait-for
# --gone` waits for an ELEMENT to disappear, not for a process to exit. There is no
# winapp verb for "the process is gone", so that single assertion stays a one-liner:
#
#     Get-Process -Name mpvw -ErrorAction SilentlyContinue   # must be empty
#
# Usage:
#   pwsh tools/mvpw-mcp/verify-official.ps1                    # against a running mpvw
#   pwsh tools/mvpw-mcp/verify-official.ps1 -CloseRunning      # close it first
#
# Exit 0 = every assertion passed. Any failure prints the assertion and the raw output.

param(
    [switch]$CloseRunning,
    [string]$App = 'mpvw'
)

$ErrorActionPreference = 'Stop'
$failed = @()

function Assert-Equal {
    param([string]$What, [string]$Actual, [string]$Expected)
    if ($Actual -eq $Expected) {
        Write-Host ("  PASS  {0} = {1}" -f $What, $Actual)
    } else {
        # No {n!r} anywhere in this file. That is Python's repr syntax, and PowerShell's
        # -f operator throws on it ("Input string was not in a correct format"), which
        # turns a clean assertion failure into a crash -- so the FAIL path itself was
        # broken. Found on 2026-10-27 by injecting a wrong expected value and watching
        # the failure branch raise instead of printing.
        Write-Host ("  FAIL  {0}: got '{1}', expected '{2}'" -f $What, $Actual, $Expected)
        $script:failed += $What
    }
}

function Assert-Match {
    param([string]$What, [string]$Text, [string]$Pattern)
    if ($Text -match $Pattern) {
        Write-Host ("  PASS  {0}" -f $What)
    } else {
        Write-Host ("  FAIL  {0}: pattern '{1}' not found" -f $What, $Pattern)
        $script:failed += $What
    }
}

# --- preconditions -----------------------------------------------------------

if ($CloseRunning) {
    $running = Get-Process -Name $App -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host "closing $App ($($running.Count) process(es)) with WM_CLOSE..."
        $running | ForEach-Object { $_.CloseMainWindow() | Out-Null }
        $deadline = (Get-Date).AddSeconds(10)
        while ((Get-Date) -lt $deadline) {
            Start-Sleep -Milliseconds 250
            if (-not (Get-Process -Name $App -ErrorAction SilentlyContinue)) { break }
        }
    }
}

if (-not (Get-Process -Name $App -ErrorAction SilentlyContinue)) {
    # Launching is allowed by AGENTS.md; leaving it running is not. This script does not
    # launch on its own so a caller controls the lifetime.
    Write-Host "FAIL: $App is not running. Start it, or re-run with the app already up."
    Write-Host "      (this script does not launch it, so it can be used as a gate stage)"
    exit 2
}

Write-Host "`n=== 1. window title ==="
$title = (& winapp ui get-value ShellTitleText -a $App 2>&1 | Out-String).Trim()
Assert-Equal 'ShellTitleText' $title 'MPV'

Write-Host "`n=== 2. the four navigation items, one inspect ==="
# Measured: a single `inspect PaneRoot -d 6` returns all four with their Chinese names.
# This is the assertion that used to need the NAV dictionary plus a hand-written tree
# walk. Grid produces no UIA node, which is why the root is PaneRoot and not the pane.
$tree = (& winapp ui inspect PaneRoot -a $App -d 6 2>&1 | Out-String)
Assert-Match 'nav home'    $tree 'ShellNav_home\s+\S+\s+"主页"'
Assert-Match 'nav music'   $tree 'ShellNav_music\s+\S+\s+"音乐库"'
Assert-Match 'nav videos'  $tree 'ShellNav_videos\s+\S+\s+"视频库"'
Assert-Match 'nav queue'   $tree 'ShellNav_queue\s+\S+\s+"播放队列"'

Write-Host "`n=== 3. the transport bar ==="
foreach ($pair in @(
        @{ Id = 'PlayPauseButton';   Name = '播放' },
        @{ Id = 'VolumeMuteButton';  Name = '静音' },
        @{ Id = 'FullScreenButton';  Name = '全屏' })) {
    $out = (& winapp ui get-property $pair.Id -a $App -p Name 2>&1 | Out-String)
    $got = ($out -split "`n" | Where-Object { $_ -match '^\s*Name:\s*(.+)$' } |
            Select-Object -First 1) -replace '^\s*Name:\s*', ''
    Assert-Equal $pair.Id ($got -replace '\s+$', '') $pair.Name
}

Write-Host "`n=== 4. exit-code contract (so this can be a gate stage) ==="
& winapp ui wait-for ShellNav_home -a $App -t 2000 *> $null
if ($LASTEXITCODE -eq 0) {
    Write-Host "  PASS  wait-for present element exits 0"
} else {
    Write-Host "  FAIL  wait-for present element exited $LASTEXITCODE"
    $failed += 'wait-for exit code (present)'
}
& winapp ui wait-for NoSuchElement__official_check -a $App -t 800 *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Host "  PASS  wait-for absent element exits non-zero"
} else {
    Write-Host "  FAIL  wait-for absent element exited 0 -- the instrument cannot fail"
    $failed += 'wait-for exit code (absent)'
}

Write-Host "`n" + ('=' * 52)
if ($failed.Count -eq 0) {
    Write-Host "OK: official-tooling assertions passed ($App)"
    Write-Host "NOTE: this does NOT check that the process exits; do that separately:"
    Write-Host "      Get-Process -Name $App -ErrorAction SilentlyContinue   # must be empty"
    exit 0
}
Write-Host "FAILED: $($failed.Count) assertion(s): $($failed -join '; ')"
exit 1
