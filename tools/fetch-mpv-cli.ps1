# Fetches the standalone mpv CLI used by the video-preview thumbnails
# (thumbfast spawns an independent mpv child process).
#
# Source: shinchiro/mpv-winbuild-cmake (GPL-2.0+ build of mpv/FFmpeg).
# Pinned to release 20260808 (mpv v0.41.0-920, FFmpeg N-125994) for reproducibility.
param(
    [Parameter(Mandatory = $true)][string]$PublishDir,
    [string]$Version = '20260808',
    [string]$Commit = 'dd5d17d328'
)

$ErrorActionPreference = 'Stop'

$exe = Join-Path $PublishDir 'mpv.exe'
if (Test-Path -LiteralPath $exe) {
    Write-Host "mpv.exe already present: $exe"
    exit 0
}

$url = "https://github.com/shinchiro/mpv-winbuild-cmake/releases/download/$Version/mpv-x86_64-$Version-git-$Commit.7z"
$archive = Join-Path $env:TEMP "mpv-x86_64-$Version-git-$Commit.7z"
$extract = Join-Path $env:TEMP "mpv-cli-$Version"

if (-not (Test-Path -LiteralPath $archive)) {
    Write-Host "Downloading mpv CLI: $url"
    Invoke-WebRequest -Uri $url -OutFile $archive -UseBasicParsing
}

if (Test-Path -LiteralPath $extract) {
    Remove-Item -LiteralPath $extract -Recurse -Force
}
New-Item -ItemType Directory -Path $extract -Force | Out-Null

$sevenZip = (Get-Command 7z -ErrorAction SilentlyContinue).Source
if (-not $sevenZip) {
    foreach ($candidate in @('C:\Program Files\7-Zip\7z.exe', 'C:\Program Files (x86)\7-Zip\7z.exe')) {
        if (Test-Path -LiteralPath $candidate) {
            $sevenZip = $candidate
            break
        }
    }
}
if (-not $sevenZip) {
    throw "7-Zip is required to extract the mpv CLI archive: $url"
}

& $sevenZip x $archive "-o$extract" -y | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Failed to extract mpv CLI archive: $archive"
}

Copy-Item -LiteralPath (Join-Path $extract 'mpv.exe') -Destination $PublishDir -Force
Copy-Item -LiteralPath (Join-Path $extract 'd3dcompiler_43.dll') -Destination $PublishDir -Force
Write-Host "mpv CLI installed: $exe"
