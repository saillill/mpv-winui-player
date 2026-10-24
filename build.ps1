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

# The mpv development libraries (mpv-2.dll, mpv.lib, headers) are not committed
# and are per-architecture, so both the check and the fetch are platform-scoped:
# a checkout that has x64 libraries is still missing them for ARM64.
$libsDir = "$root\mpv-winui\libs"
$libsOk = (Test-Path -LiteralPath "$libsDir\bin\$Platform\mpv-2.dll") -and (Test-Path -LiteralPath "$libsDir\lib\$Platform\mpv.lib")
if (-not $libsOk) {
    if ($FetchMpvLibs) {
        Write-Host "==> mpv libs missing for $Platform ($libsDir); fetching..."
        & "$root\tools\fetch-mpv-libs.ps1" -Root $root -Platform $Platform
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    } else {
        Write-Error "mpv libs missing for $Platform ($libsDir\bin\$Platform). Run .\tools\fetch-mpv-libs.ps1 -Platform $Platform first, or re-run with -FetchMpvLibs to download automatically."
    }
}

# XAML comments cannot contain "--" (WMC9997) and nothing in this repository's
# editing tools prevents writing it, so the check runs before the build rather
# than after. A failed build followed by a deploy that still reports success is
# the documented symptom of skipping this.
Write-Host "==> Checking XAML"
& python "$root\tools\check-xaml.py"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# A Lua script that shadows one of mpv's built-ins has to keep accepting the
# script-opts mpv ships for it: mpv.conf hands select.lua a key its local copy
# did not declare, so every launch logged "unknown key populate_menu_data" and
# dropped the value. Cheap (reads one DLL and a few files) and the drift it
# catches is invisible in a build that otherwise succeeds.
Write-Host "==> Checking script-opts"
& python "$root\tools\check-script-opts.py"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# An event the app subscribes to has to be raised by the native component. Two
# real defects shipped with this shape: PlayerControl listened for
# PlaybackFailed and MpvPlayerPage for VoConfigured, both renamed by c49b461.
# The IDL kept the old names, so both subscriptions compiled and never fired --
# one of them made the Error visual state unreachable. Static, so it runs here
# rather than after the build.
Write-Host "==> Checking WinRT events"
& python "$root\tools\check-winrt-events.py"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

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
#
# --no-incremental is NOT belt-and-braces; it is the difference between a real
# result and a fabricated one. Measured on 2026-10-24:
#
#   ControlBarAdaptiveLayout.cs changed 340 -> 260 (breaks a tier assertion),
#   but written with the file's ORIGINAL mtime -- which is what `git checkout`,
#   `Copy-Item -Force` and a fresh clone all produce, since git sets mtime to
#   checkout time and links share timestamps:
#
#     dotnet test (incremental)      -> 496/496 PASS    <-- wrong
#     dotnet build --no-incremental  -> 495/496 FAIL    <-- right
#
#   Touching the same broken file to "now" made the incremental run fail too,
#   which pins the cause on timestamps rather than on the build graph.
#
# The linked source files live in ..\mpv-winui\, outside this project directory.
# MSBuild's up-to-date check compares those timestamps against the output, so any
# operation that restores an older mtime makes the suite skip recompiling and then
# report the PREVIOUS build's verdict. A suite that can pass while its subject is
# broken is not evidence, and this one is the only test of the control bar's
# width tiers.
Write-Host "==> Running mpv-conf-test"
dotnet build "$root\mpv-winui\mpv-conf-test\mpv-conf-test.csproj" -c $Configuration --no-incremental -v:minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet test "$root\mpv-winui\mpv-conf-test\mpv-conf-test.csproj" -c $Configuration --no-build -v:minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# mpv-winrt-test drives the real mpv_winrt component against the real mpv
# runtime, so it can only run after the C++ build above. Nothing used to run it:
# not this script, not CI, so its assertions were never evidence of anything.
# It is run here rather than in a CI job precisely because it needs the C++ and
# native binaries this script has just produced.
#
# Same --no-incremental reasoning as above: its sources are the projection
# assembly, whose timestamp moves with every checkout of the C++ side.
Write-Host "==> Running mpv-winrt-test"
dotnet build "$root\mpv-winui\mpv-winrt-test\mpv-winrt-test.csproj" -c $Configuration -p:Platform=$Platform --no-incremental -v:minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet test "$root\mpv-winui\mpv-winrt-test\mpv-winrt-test.csproj" -c $Configuration -p:Platform=$Platform --no-build -v:minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($CheckLocalization) {
    Write-Host "==> Checking localization consistency"
    & python "$root\tools\check-localization.py"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
exit $LASTEXITCODE
