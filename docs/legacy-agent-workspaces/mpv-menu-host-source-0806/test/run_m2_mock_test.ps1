$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$bin = Join-Path $root "bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64"
$out = Join-Path $PSScriptRoot "mock-received.json"
$hostLog = Join-Path $PSScriptRoot "host-m2.log"
$mockLog = Join-Path $PSScriptRoot "mock-server.log"

if ([System.IO.File]::Exists($out)) { [System.IO.File]::Delete($out) }
if ([System.IO.File]::Exists($hostLog)) { [System.IO.File]::Delete($hostLog) }
if ([System.IO.File]::Exists($mockLog)) { [System.IO.File]::Delete($mockLog) }

$server = Start-Process -FilePath "powershell.exe" `
    -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", `
        (Join-Path $PSScriptRoot "mock_pipe_server.ps1"), `
        "mpv-test-pipe", $out, ">", $mockLog, "2>&1") `
    -PassThru -WindowStyle Hidden
Start-Sleep -Milliseconds 800

$p = Start-Process -FilePath (Join-Path $bin "MpvMenuHost.exe") `
    -ArgumentList @(
        "--items", (Join-Path $PSScriptRoot "sample-menu.json"),
        "--pipe", "mpv-test-pipe",
        "--test-select", "0",
        "--test-delay-ms", "500",
        "--log", $hostLog) `
    -PassThru -WindowStyle Hidden
if (-not $p.WaitForExit(20000)) {
    Stop-Process -Id $p.Id -Force
    Write-Output "HOST TIMEOUT"
}
else {
    Write-Output ("HOST EXIT " + $p.ExitCode)
}

if (-not $server.WaitForExit(10000)) { Stop-Process -Id $server.Id -Force }

Write-Output "=== host log ==="
if (Test-Path -LiteralPath $hostLog) { Get-Content -LiteralPath $hostLog -Encoding UTF8 }
Write-Output "=== received by mock server ==="
if (Test-Path -LiteralPath $out) { Get-Content -LiteralPath $out -Encoding UTF8 } else { Write-Output "<none>" }
