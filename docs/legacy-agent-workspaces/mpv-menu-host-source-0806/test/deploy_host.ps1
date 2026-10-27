param(
    [string]$PublishDir = "C:\Users\z2637\Documents\Codex\2026-08-06\c-users-z2637-documents-codex-2026\work\mpv-menu-host\publish\win-x64",
    [string]$Repo = "C:\Users\z2637\Documents\Codex\2026-08-04\mpv-lazy-hooke007-mpv-playkit-codex\work\mpv-config",
    [string]$Install = "C:\Program Files\mpv-lazy\portable_config"
)

$ErrorActionPreference = "Stop"

function Copy-HostDir {
    param([string]$DestRoot)
    $dest = Join-Path $DestRoot "tools\winui-menu-host"
    if (-not (Test-Path -LiteralPath $dest)) {
        New-Item -ItemType Directory -Path $dest | Out-Null
    }
    Get-ChildItem -LiteralPath $PublishDir -Recurse -File | Where-Object {
        $_.Extension -notin @(".pdb", ".log") -and $_.Name -ne "crash.log"
    } | ForEach-Object {
        $rel = $_.FullName.Substring($PublishDir.Length + 1)
        $target = Join-Path $dest $rel
        $tdir = Split-Path -Parent $target
        if (-not (Test-Path -LiteralPath $tdir)) {
            New-Item -ItemType Directory -Path $tdir -Force | Out-Null
        }
        Copy-Item -LiteralPath $_.FullName -Destination $target -Force
    }
    $size = [math]::Round((Get-ChildItem -LiteralPath $dest -Recurse -File | Measure-Object Length -Sum).Sum / 1MB, 1)
    Write-Output ("deployed to " + $dest + " (" + $size + " MB)")
}

Copy-HostDir -DestRoot $Repo
Copy-HostDir -DestRoot $Install
