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
# Usage:  pwsh tools/deploy-test.ps1 [-Verify]

param([switch]$Verify)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$src  = Join-Path $repo 'mpv-winui/mpv-winui/bin/x64/Release/net10.0-windows10.0.26100.0/win-x64'
$dst  = 'C:\Users\z2637\WorkBuddy\2026-09-19-14-59-27\outputs\mpvw-test'

if (-not (Test-Path -LiteralPath (Join-Path $src 'mpvw.dll'))) {
    throw "no mpvw.dll at $src -- build first"
}
if (-not (Test-Path -LiteralPath (Join-Path $dst 'mpvw.exe'))) {
    throw "$dst is not an existing deploy -- refusing to overlay onto nothing"
}

$running = Get-Process -Name mpvw -ErrorAction SilentlyContinue
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
Write-Host ""
Write-Host "copied : $copied file(s)  (skipped $skipped .pdb)"
Write-Host ("dst mpvw.dll: {0:N0} bytes  {1}" -f $dstDll.Length, $dstDll.LastWriteTime)
Write-Host "xbf    : $xbf  (expect 42)"

# The check that makes the exit code mean something: without it this script would
# be as trustworthy as the bash one it replaces.
if ($Verify) {
    Write-Host ""
    & (Join-Path $PSScriptRoot 'compare-deploy.ps1')
}

if ($xbf -ne 42) { throw "xbf count is $xbf, expected 42 -- the deploy is incomplete" }
exit 0
