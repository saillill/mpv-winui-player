# Builds the MSI installer for mpv-winui-player (WiX v5).
#
# Usage:
#   .\installer\build-installer.ps1                    # publish + package
#   .\installer\build-installer.ps1 -SkipPublish       # use existing publish output
#   .\installer\build-installer.ps1 -Version 1.2.0
#
# Output: dist\mpv-winui-setup-<Platform>-<Version>.msi
param(
    [ValidateSet('Debug', 'Release')] [string]$Configuration = 'Release',
    [ValidateSet('x64', 'ARM64')] [string]$Platform = 'x64',
    [string]$Version = '1.0.0',
    [switch]$SkipPublish
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

if (-not (Get-Command wix -ErrorAction SilentlyContinue)) {
    Write-Error "WiX v5 not found. Install it with: dotnet tool install --global wix"
}

$publishDir = [System.IO.Path]::GetFullPath("$root\mpv-winui\mpv-winui\bin\win-$Platform\publish") + [System.IO.Path]::DirectorySeparatorChar

if (-not $SkipPublish) {
    # build.ps1 compiles mpv_winrt (C++) with VS MSBuild first, then builds the
    # C# app; the publish below then reuses those binaries (the C++ project
    # cannot be built from the dotnet CLI alone).
    Write-Host "==> Building mpv_winrt + app [$Configuration|$Platform]"
    & "$root\build.ps1" -Configuration $Configuration -Platform $Platform
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host "==> Publishing mpv-winui [$Configuration|$Platform]"
    dotnet publish "$root\mpv-winui\mpv-winui\mpv-winui.csproj" `
        -c $Configuration -p:Platform=$Platform `
        -p:GenerateAppxPackageOnBuild=false -p:AppxPackageSigningEnabled=false `
        "-p:PublishDir=$publishDir" -v:m
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    # Standalone mpv.exe is required by thumbfast (video preview thumbnails).
    & "$root\tools\fetch-mpv-cli.ps1" -PublishDir $publishDir
}

$pub = $publishDir.TrimEnd([System.IO.Path]::DirectorySeparatorChar)
if (-not (Test-Path -LiteralPath "$pub\mpv-winui.exe")) {
    Write-Error "Publish output not found: $pub (run without -SkipPublish first)"
}

Write-Host "==> Building MSI (wix build, harvesting $pub)"
$out = "$root\dist\mpv-winui-setup-$Platform-$Version.msi"
New-Item -ItemType Directory -Path "$root\dist" -Force | Out-Null

& wix build "$root\installer\product.wxs" `
    -d Version=$Version `
    -d UpgradeCode="E9A3F2B7-1C4D-4E5F-8A6B-9C0D1E2F3A4B" `
    -d SourceDir=$pub `
    -d AppIcon="$root\mpv-winui\mpv-winui\App.ico" `
    -arch $Platform `
    -o $out
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "MSI created: $out"
Write-Host "Size: $([math]::Round((Get-Item $out).Length / 1MB, 1)) MB"
exit 0
