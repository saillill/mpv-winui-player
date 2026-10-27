$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$longMenu = Join-Path $PSScriptRoot "long-menu.json"

function Invoke-Test {
    param([string]$Label, [string[]]$ScriptArgs)
    $exe = "powershell.exe"
    $fileArgs = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File") + $ScriptArgs
    $out = & $exe @fileArgs 2>&1 | ForEach-Object { $_.ToString() } | Select-Object -Last 4
    $code = $LASTEXITCODE
    $out | ForEach-Object { Write-Host $_ }
    if ($code -eq 0) {
        Write-Host ("PASS  " + $Label)
    } else {
        Write-Host ("FAIL  " + $Label + " (exit " + $code + ")")
    }
    Write-Host ""
    return $code
}

$results = @()
$results += Invoke-Test "verify sample (system theme)" @((Join-Path $PSScriptRoot "verify_host_window.ps1"))
$results += Invoke-Test "verify sample dark" @((Join-Path $PSScriptRoot "verify_host_window.ps1"), "-Theme", "dark")
$results += Invoke-Test "verify sample light" @((Join-Path $PSScriptRoot "verify_host_window.ps1"), "-Theme", "light")
$results += Invoke-Test "verify long menu" @((Join-Path $PSScriptRoot "verify_host_window.ps1"), "-Items", $longMenu, "-PosX", "400", "-PosY", "120")
$results += Invoke-Test "M2 mock" @((Join-Path $PSScriptRoot "run_m2_mock_test.ps1"))
$results += Invoke-Test "M2 real mpv" @((Join-Path $PSScriptRoot "run_m2_mpv_test.ps1"))
$results += Invoke-Test "M3 e2e" @((Join-Path $PSScriptRoot "run_m3_e2e.ps1"))
$results += Invoke-Test "M4 cascade" @((Join-Path $PSScriptRoot "run_m4_cascade_test.ps1"))
$results += Invoke-Test "single-process invariant" @((Join-Path $PSScriptRoot "run_orphan_exit_test.ps1"))
$results += Invoke-Test "fallback (missing host)" @((Join-Path $PSScriptRoot "run_fallback_test.ps1"))

$failed = ($results | Where-Object { $_ -ne 0 }).Count
Write-Output ("== summary: " + $results.Count + " tests, " + $failed + " failed ==")
if ($failed -gt 0) { exit 1 }
exit 0
