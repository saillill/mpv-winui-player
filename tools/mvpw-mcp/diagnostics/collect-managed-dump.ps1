# Managed crash forensics for mpvw, using the .NET diagnostic toolchain
#
# Why this exists: AGENTS.md's "Reading a crash" section describes a real crash class
# -- 0xc000027b + Microsoft.UI.Xaml.dll, no dialog, no managed stack, no log line --
# and tells you to look at the P7 field of the WER report. That instruction was the
# end of the road, because nothing on this machine could produce a managed stack.
#
# The windbg plugin was installed for this and cannot do it: all 11 of its skills
# target native C/C++ or kernel, and windbg-user-exception-triage says in its own
# description "Not for managed .NET exceptions, WinUI/XAML app errors" -- which is
# exactly this crash class. Measured 2026-10-27.
#
# What actually works is the .NET toolchain, installed and verified the same day:
#   dotnet-dump collect -p <pid>  -> a 1 GB dump of the live app
#   dotnet-dump analyze <dump>    -> SOS loaded (10.0.15.501), 97,051 managed objects,
#                                    12 threads, and a full managed stack including
#                                    ABI.Microsoft.UI.Xaml.IApplicationStaticsMethods.Start
#                                    -> Application.Start -> mpv_winui.Program.Main
#
# Usage:
#   pwsh tools/mvpw-mcp/diagnostics/collect-managed-dump.ps1                 # collect only
#   pwsh tools/mvpw-mcp/diagnostics/collect-managed-dump.ps1 -Analyze        # + stacks
#   pwsh tools/mvpw-mcp/diagnostics/collect-managed-dump.ps1 -DumpPath x.dmp -Analyze
#
# A dump is ~1 GB per run. This script deletes it unless -Keep is passed.

param(
    [string]$DumpPath = (Join-Path $env:TEMP 'mpvw-managed.dmp'),
    [switch]$Analyze,
    [switch]$Keep,
    [string]$App = 'mpvw'
)

$ErrorActionPreference = 'Stop'
$failed = @()

# 1. the process must exist ---------------------------------------------------
$proc = Get-Process -Name $App -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) {
    Write-Host "FAIL: $App is not running. This tool analyses a LIVE process; it does not"
    Write-Host "      launch one, because a dump of a just-started app tells you nothing"
    Write-Host "      about the state that crashed."
    exit 2
}
Write-Host "target : $App (pid $($proc.Id))"

# 2. the toolchain must exist -------------------------------------------------
foreach ($tool in @('dotnet-dump')) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        Write-Host "FAIL: $tool not found. Install with:"
        Write-Host "      dotnet tool install --global $tool"
        exit 2
    }
}

# 3. collect ------------------------------------------------------------------
if (Test-Path -LiteralPath $DumpPath) { Remove-Item -LiteralPath $DumpPath -Force }
Write-Host "dump   : $DumpPath"
$collect = dotnet-dump collect -p $proc.Id -o $DumpPath 2>&1 | Out-String
if (-not (Test-Path -LiteralPath $DumpPath)) {
    Write-Host "FAIL: dotnet-dump collect produced no file"
    Write-Host $collect
    exit 1
}
$sizeMB = [math]::Round((Get-Item -LiteralPath $DumpPath).Length / 1MB, 1)
Write-Host "         $sizeMB MB"

if (-not $Analyze) {
    Write-Host "`nDump kept at $DumpPath"
    Write-Host "Analyse it with: dotnet-dump analyze `"$DumpPath`""
    exit 0
}

# 4. analyse ------------------------------------------------------------------
# These four answer the question AGENTS.md poses: what was the app doing, and was
# there a managed exception. `clrstack` is the one that windbg could never give.
$commands = @(
    'eeversion'                       # prove SOS is loaded before trusting anything
    'clrthreads'                      # how many managed threads, which are running
    'clrstack'                        # the managed stack -- the whole point
    'dumpheap -stat -type Exception'  # any live exceptions, by type
    'exit'
)
$rcFile = Join-Path $env:TEMP ("dumprc-{0}.txt" -f [guid]::NewGuid().ToString('N'))
$commands -join "`n" | Out-File -FilePath $rcFile -Encoding ascii

Write-Host "`n--- analysis ---"
# Use dotnet-dump's OWN -c/--command flag, not a shell with piped stdin.
#
# Four failed attempts before this one, all recorded because each looked plausible:
#   1. `... | dotnet-dump ... | Out-String`            -> dropped lines intermittently
#   2. `... | dotnet-dump ... *>&1 | Out-File`         -> clrthreads went to console
#   3. `$commands | & dotnet-dump ... 2>&1 | Out-File` -> same
#   4. `cmd /c 'type rc | dotnet-dump ... > out'`      -> same
# Reason: dotnet-dump's interactive shell binds the console handle directly for
# command output, so no PowerShell stream or cmd redirection ever sees it. `-c` is the
# supported non-interactive path, and with it the output goes through the normal
# process stdout, which PowerShell captures fine.
$outFile = Join-Path $env:TEMP ("dumpout-{0}.txt" -f [guid]::NewGuid().ToString('N'))
$cmdArgs = @()
foreach ($c in $commands) { $cmdArgs += @('-c', $c) }
$null = & dotnet-dump analyze $DumpPath @cmdArgs 2>&1 | Out-File -FilePath $outFile -Encoding utf8
$analysis = if (Test-Path -LiteralPath $outFile) {
    Get-Content -LiteralPath $outFile -Raw
} else { '' }
Remove-Item -LiteralPath $outFile -Force -ErrorAction SilentlyContinue

if (-not $analysis) {
    Write-Host "FAIL: dotnet-dump analyze produced no output at all"
    exit 1
}

# Keep the output short: a full dumpheap -stat is thousands of lines and buries the
# part a human needs. sos version + thread count + the managed stack frame names.
$sos = ([regex]::Match($analysis, 'SOS Version:\s*(\S+(?:\s+\S+)*)')).Groups[1].Value
if ($sos) {
    Write-Host "  PASS  SOS loaded: $sos"
} else {
    Write-Host "  FAIL  SOS did not load -- the rest of this output cannot be trusted"
    $failed += 'SOS not loaded'
}

$threadCount = ([regex]::Match($analysis, 'ThreadCount:\s*(\d+)')).Groups[1].Value
if ($threadCount) {
    Write-Host "  PASS  managed threads: $threadCount"
} else {
    Write-Host "  FAIL  clrthreads produced no thread count"
    $failed += 'no thread count'
}

# Managed frames: lines that look like `address address Namespace.Type.Method(...)`.
$frames = ($analysis -split "`n") |
    Where-Object { $_ -match '^\s*[0-9A-F]{8,}\s+[0-9A-F]{8,}\s+[\w\.\+<>`]+\.' } |
    ForEach-Object { ($_ -split '\s+')[2] } |
    Where-Object { $_ }
if ($frames.Count -gt 0) {
    Write-Host "  PASS  managed stack frames: $($frames.Count)"
    $frames | Select-Object -First 8 | ForEach-Object { Write-Host "          $_" }
} else {
    Write-Host "  FAIL  no managed frames recovered"
    $failed += 'no managed frames'
}

# Exceptions that are actual instances, not NLog's ExceptionLayoutRenderer machinery.
$exLines = ($analysis -split "`n") |
    Where-Object { $_ -match '^\s*[0-9a-f]{10,}\s+\d+\s+[\d,]+\s+System\.\w*Exception' }
if ($exLines) {
    Write-Host "  NOTE  live Exception instances in the heap:"
    $exLines | Select-Object -First 6 | ForEach-Object { Write-Host "          $($_.Trim())" }
} else {
    Write-Host "  PASS  no live System.*Exception instances in the heap"
}

if (-not $Keep) {
    Remove-Item -LiteralPath $DumpPath -Force
    Write-Host "`n(dump deleted; pass -Keep to retain it)"
} else {
    Write-Host "`n(dump kept at $DumpPath)"
}

Write-Host ('=' * 56)
if ($failed.Count) {
    Write-Host "FAILED: $($failed -join '; ')"
    exit 1
}
Write-Host "OK: managed forensics available for $App"
exit 0
