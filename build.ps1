# Builds mpv-winui-player locally:
#   1) mpv_winrt (C++/WinRT) with VS MSBuild (dotnet CLI cannot build C++ projects)
#   2) mpv-winui (C# WinUI 3) with dotnet build
# The C# project references the C++ outputs from mpv-winui\bin\<Platform>\<Configuration>\mpv_winrt\.
param(
    [ValidateSet('Debug', 'Release')] [string]$Configuration = 'Debug',
    [ValidateSet('x64', 'ARM64')] [string]$Platform = 'x64'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

# Use the VS Build Tools MSBuild when available; otherwise fall back to PATH.
$msbuild = 'C:\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path -LiteralPath $msbuild)) {
    $msbuild = 'MSBuild.exe'
}

# packages.config restore (needed for a clean clone; no-op when packages already exist).
if (Get-Command nuget -ErrorAction SilentlyContinue) {
    Write-Host "==> Restoring NuGet packages (mpv-winrt)"
    nuget restore "$root\mpv-winui\mpv-winrt\mpv-winrt.vcxproj" -SolutionDirectory "$root\mpv-winui" | Out-Null
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} elseif (-not (Test-Path -LiteralPath "$root\mpv-winui\packages")) {
    Write-Error "nuget.exe not found and mpv-winui\packages is missing. Install nuget.exe or restore packages first."
}

Write-Host "==> Building mpv_winrt (C++/WinRT) [$Configuration|$Platform]"
& $msbuild "$root\mpv-winui\mpv-winrt\mpv-winrt.vcxproj" /t:Build "/p:Configuration=$Configuration" "/p:Platform=$Platform" /p:VisualStudioVersion=17.0 /m /v:m /nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "==> Building mpv-winui (C# WinUI 3) [$Configuration|$Platform]"
dotnet build "$root\mpv-winui\mpv-winui\mpv-winui.csproj" -c $Configuration -p:Platform=$Platform -p:GenerateAppxPackageOnBuild=false -p:AppxPackageSigningEnabled=false -v:m
exit $LASTEXITCODE
