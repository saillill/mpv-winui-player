# Fetches the mpv development libraries (mpv-2.dll, mpv.lib, headers) used to
# build the mpv_winrt C++/WinRT component.
#
# Source: ikas-mc/mpv-windows-builder (the same release the CI workflow pins).
# Pinned to 20260731 for reproducibility; the archive extracts the whole
# mpv-winui\libs layout (bin\, include\, lib\, share\).
param(
    [string]$Root,
    [string]$Version = '20260731'
)

$ErrorActionPreference = 'Stop'

if (-not $Root) {
    # <root>\tools\fetch-mpv-libs.ps1 -> parent tools -> parent root
    $Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
}
$libsDir = Join-Path $Root 'mpv-winui\libs'
$dll = Join-Path $libsDir 'bin\mpv-2.dll'
$lib = Join-Path $libsDir 'lib\mpv.lib'

if ((Test-Path -LiteralPath $dll) -and (Test-Path -LiteralPath $lib)) {
    Write-Host "mpv libs already present: $libsDir"
    exit 0
}

$url = "https://github.com/ikas-mc/mpv-windows-builder/releases/download/$Version/mpv-2.dll.zip"
$archive = Join-Path $env:TEMP "mpv-libs-$Version.zip"

if (-not (Test-Path -LiteralPath $archive)) {
    Write-Host "Downloading mpv libs: $url"
    Invoke-WebRequest -Uri $url -OutFile $archive -UseBasicParsing
}

if (-not (Test-Path -LiteralPath $libsDir)) {
    New-Item -ItemType Directory -Path $libsDir -Force | Out-Null
}

# The archive's internal layout is already bin\include\lib\share, i.e. the
# mpv-winui\libs layout the vcxproj expects (matches the CI workflow).
# $ErrorActionPreference = 'Stop' makes Expand-Archive throw on failure.
Expand-Archive -Path $archive -DestinationPath $libsDir -Force

Write-Host "mpv libs installed: $libsDir"
