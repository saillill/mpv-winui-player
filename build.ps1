# Builds mpv-winui-player locally:
#   1) mpv_winrt (C++/WinRT) with VS MSBuild (dotnet CLI cannot build C++ projects)
#   2) mpv-winui (C# WinUI 3) with dotnet build
# The C# project references the C++ outputs from mpv-winui\bin\<Platform>\<Configuration>\mpv_winrt\.
param(
    [ValidateSet('Debug', 'Release')] [string]$Configuration = 'Debug',
    [ValidateSet('x64', 'ARM64')] [string]$Platform = 'x64',
    [switch]$CheckLocalization,
    [switch]$FetchMpvLibs
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

# The mpv development libraries (mpv-2.dll, mpv.lib, headers) are not committed;
# a clean clone must fetch them first (CI downloads the same archive).
$libsDir = "$root\mpv-winui\libs"
$libsOk = (Test-Path -LiteralPath "$libsDir\bin\mpv-2.dll") -and (Test-Path -LiteralPath "$libsDir\lib\mpv.lib")
if (-not $libsOk) {
    if ($FetchMpvLibs) {
        Write-Host "==> mpv libs missing ($libsDir); fetching..."
        & "$root\tools\fetch-mpv-libs.ps1" -Root $root
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    } else {
        Write-Error "mpv libs missing ($libsDir). Run .\tools\fetch-mpv-libs.ps1 first, or re-run with -FetchMpvLibs to download automatically."
    }
}

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
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# The mpv.conf test suite.
#
# This project did not compile for a while: MpvConfManager reached into
# ManagedMpvConfig/MpvConfWatcher, which dragged Modules.Settings (and therefore
# AppContext and WinUI) into a net10.0 test project that does not link it. Two
# shipped defects went unnoticed while it was dead — a value that doubled its own
# escaping on every launch until mpv.conf reached 896 MB with 884 newlines, and
# six settings whose own written values could not be read back. Neither was a
# build error, and neither had a test that could run. The coupling is gone now
# (MpvConfManager takes an onBeforeSave callback instead), so run the real suite.
#
# It sits before packaging because a green build is not evidence that a value
# survives a write and a read.
Write-Host "==> Running mpv-conf-test"
dotnet test "$root\mpv-winui\mpv-conf-test\mpv-conf-test.csproj" -c $Configuration -v:minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($CheckLocalization) {
    Write-Host "==> Checking localization consistency"
    & python "$root\tools\check-localization.py"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
exit $LASTEXITCODE
