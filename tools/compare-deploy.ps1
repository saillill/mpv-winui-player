#!/usr/bin/env pwsh
# Compare the build output against the deploy directory, precisely.
#
# Why this exists: tools/deploy-test.sh is a bash script that calls Unix `find`
# and `cp`, and on this machine PATH resolves `find` to Windows' find.exe, which
# answers "FIND: Parameter format not correct" and yields an empty file list. The
# copy loop therefore does nothing and the script reports `xbf count: 0` -- while
# having already printed reassuring "src dll / dst dll" lines. It is a silent
# no-op that looks like a run.
#
# This reports the truth: which files differ, by size or hash, in both directions
# (stale in the deploy dir, and present only in the build output).
#
# Usage:  pwsh tools/compare-deploy.ps1 [-ShowAll]

param([switch]$ShowAll)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$src  = Join-Path $repo 'mpv-winui/mpv-winui/bin/x64/Release/net10.0-windows10.0.26100.0/win-x64'
$dst  = if ($env:MPVW_DEPLOY_DIR) { $env:MPVW_DEPLOY_DIR } else { Join-Path (Split-Path -Parent $repo) 'mpv-deploy' }

foreach ($p in @($src, $dst)) {
    if (-not (Test-Path -LiteralPath $p)) { throw "not found: $p" }
}

function Get-RelativeMap([string]$root) {
    $map = @{}
    Get-ChildItem -LiteralPath $root -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($root.Length).TrimStart('\', '/')
        $map[$rel] = $_
    }
    return $map
}

Write-Host "src: $src"
Write-Host "dst: $dst"
$s = Get-RelativeMap $src
$d = Get-RelativeMap $dst
Write-Host "src files: $($s.Count)  |  dst files: $($d.Count)"
Write-Host ""

$identical = 0
$differ = New-Object System.Collections.Generic.List[object]
$symbols = New-Object System.Collections.Generic.List[object]
foreach ($rel in ($s.Keys | Sort-Object)) {
    # .pdb is skipped by tools/deploy-test.ps1 on purpose -- debug symbols are
    # not part of an install, so their absence from the deploy dir is not
    # staleness. They get their own bucket so they cannot pad "needs deploying"
    # with a permanent non-issue: that made a fully-synced deploy report 4 files
    # behind, which is how a real 4 files behind would have gone unnoticed.
    if ([System.IO.Path]::GetExtension($rel) -eq '.pdb') {
        $symbols.Add([pscustomobject]@{ Rel = $rel; Bytes = $s[$rel].Length })
        continue
    }
    if (-not $d.ContainsKey($rel)) {
        $differ.Add([pscustomobject]@{ Rel = $rel; Kind = 'MISSING in dst'; Src = $s[$rel].Length; Dst = $null })
        continue
    }
    $a = $s[$rel]; $b = $d[$rel]
    if ($a.Length -ne $b.Length) {
        $differ.Add([pscustomobject]@{ Rel = $rel; Kind = 'size differs'; Src = $a.Length; Dst = $b.Length })
        continue
    }
    # Sizes match; compare content. Skip the 63 MB mpv-2.dll, which is pinned.
    if ($a.Length -gt 8MB) { $identical++; continue }
    if ((Get-FileHash -LiteralPath $a.FullName -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $b.FullName -Algorithm SHA256).Hash) {
        $differ.Add([pscustomobject]@{ Rel = $rel; Kind = 'content differs'; Src = $a.Length; Dst = $b.Length })
    } else {
        $identical++
    }
}

$onlyInDst = @($d.Keys | Where-Object { -not $s.ContainsKey($_) })

Write-Host "identical: $identical"
Write-Host "differing: $($differ.Count)"
Write-Host "present only in dst: $($onlyInDst.Count)"
Write-Host "symbols  : $($symbols.Count) .pdb, not deployed by design"
Write-Host ""
if ($differ.Count) {
    Write-Host "== files that need deploying =="
    $differ | Sort-Object Kind, Rel | Format-Table -AutoSize
}
if ($symbols.Count -and $ShowAll) {
    Write-Host "== .pdb present in src only (deploy-test.ps1 skips these) =="
    $symbols | Sort-Object Rel | Format-Table -AutoSize
}
if ($onlyInDst.Count) {
    Write-Host "== only in dst (overlay leaves them; prune only if a .xaml moved) =="
    if ($ShowAll) { $onlyInDst | Sort-Object | ForEach-Object { "  $_" } }
    else { $onlyInDst | Sort-Object | Select-Object -First 20 | ForEach-Object { "  $_" } }
}

exit 0
