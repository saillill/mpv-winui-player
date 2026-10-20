#!/usr/bin/env pwsh
# Deploy the current build into the session's test directory, natively.
#
# Why this exists alongside deploy-test.sh: that one is bash and calls Unix
# `find`/`cp`. On this machine PATH resolves `find` to Windows' find.exe, which
# prints "FIND: Parameter format not correct" and writes nothing -- so the copy
# loop reads an empty file list, copies nothing, exits 0, and still prints a
# reassuring "src dll / dst dll" pair. Measured 2026-09-21: destination dll
# timestamp unchanged after a "successful" run.
#
# Two invariants carried over from that script, because both cost real debugging
# time when they were learned:
#
#   1. .xbf must travel WITH the dll. The compiled XAML is a separate file, and a
#      dll/xbf mismatch surfaces as InvalidCastException inside Connect(), which
#      looks exactly like a genuine XAML bug.
#   2. Never delete-and-recreate the destination. The install needs the WinAppSDK
#      runtime DLLs already sitting there; wiping the directory leaves a
#      half-copied tree that cannot start.
#
# So: mkdir -p, then overlay. .pdb is skipped -- debug symbols are not part of an
# install and their absence is not a stale file.
#
# Usage:  pwsh tools/deploy-test.ps1 [-Verify] [-CloseRunning]

param([switch]$Verify, [switch]$CloseRunning)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$src  = Join-Path $repo 'mpv-winui/mpv-winui/bin/x64/Release/net10.0-windows10.0.26100.0/win-x64'
$dst  = 'C:\Users\z2637\Documents\DeepSeek\mpv-deploy'

if (-not (Test-Path -LiteralPath (Join-Path $src 'mpvw.dll'))) {
    throw "no mpvw.dll at $src -- build first"
}
if (-not (Test-Path -LiteralPath (Join-Path $dst 'mpvw.exe'))) {
    throw "$dst is not an existing deploy -- refusing to overlay onto nothing"
}

$running = Get-Process -Name mpvw -ErrorAction SilentlyContinue
if ($running -and $CloseRunning) {
    # Closing is allowed; launching is not. See "Do not launch the app" in
    # AGENTS.md: the rule forbids putting mpvw.exe into the process list, and
    # the project's owner has explicitly authorised taking it out again.
    #
    # Ask first, force second. CloseMainWindow posts WM_CLOSE, which lets WinUI
    # and mpv shut down the way the user's own close button does -- the config
    # writer, the log flush and mpv's own state save all run. Stop-Process does
    # not, so it is only the fallback for a window that will not answer.
    Write-Host "closing mpvw.exe ($($running.Count) process(es))..."
    foreach ($proc in $running) {
        $proc.CloseMainWindow() | Out-Null
    }
    $deadline = (Get-Date).AddSeconds(10)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 250
        if (-not (Get-Process -Name mpvw -ErrorAction SilentlyContinue)) { break }
    }
    $left = Get-Process -Name mpvw -ErrorAction SilentlyContinue
    if ($left) {
        Write-Host "  did not close in 10s -- forcing"
        $left | Stop-Process -Force
        Start-Sleep -Milliseconds 500
    }
    $running = Get-Process -Name mpvw -ErrorAction SilentlyContinue
}
if ($running) {
    throw "mpvw.exe is running ($($running.Count) process(es)) -- the deploy would be overwritten mid-run"
}

Write-Host "src: $src"
Write-Host "dst: $dst"
Write-Host ("src mpvw.dll: {0:N0} bytes  {1}" -f (Get-Item "$src/mpvw.dll").Length, (Get-Item "$src/mpvw.dll").LastWriteTime)

$copied = 0
$skipped = 0
Get-ChildItem -LiteralPath $src -Recurse -File | ForEach-Object {
    if ($_.Extension -eq '.pdb') { $skipped++; return }
    $rel = $_.FullName.Substring($src.Length).TrimStart('\', '/')
    $target = Join-Path $dst $rel
    $dir = Split-Path $target -Parent
    if (-not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
    }
    Copy-Item -LiteralPath $_.FullName -Destination $target -Force
    $copied++
}

$dstDll = Get-Item "$dst/mpvw.dll"
$xbf = @(Get-ChildItem -LiteralPath $dst -Recurse -File -Filter *.xbf).Count
# Compared against the build output rather than a fixed number. The count used
# to be a literal, so adding one XAML file failed the deploy with "expected 42"
# until someone edited the constant -- a check that reports a stale constant
# instead of a real gap. Source-vs-destination is also the stronger claim: it
# fails when the deploy is incomplete, which is what this is for.
$srcXbf = @(Get-ChildItem -LiteralPath $src -Recurse -File -Filter *.xbf).Count
Write-Host ""
Write-Host "copied : $copied file(s)  (skipped $skipped .pdb)"
Write-Host ("dst mpvw.dll: {0:N0} bytes  {1}" -f $dstDll.Length, $dstDll.LastWriteTime)
Write-Host "xbf    : $xbf  (built $srcXbf)"

# The check that makes the exit code mean something: without it this script would
# be as trustworthy as the bash one it replaces.
if ($Verify) {
    Write-Host ""
    & (Join-Path $PSScriptRoot 'compare-deploy.ps1')
}

if ($xbf -ne $srcXbf) { throw "xbf count is $xbf but the build produced $srcXbf -- the deploy is incomplete" }
exit 0
