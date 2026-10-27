$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root "bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\MpvMenuHost.exe"
$log = Join-Path $env:TEMP ("mpvhost-repro-" + [guid]::NewGuid().ToString("N") + ".log")

$p = Start-Process -FilePath $exe -ArgumentList @("--no-connect", "--pos", "300,300", "--test-select", "0", "--test-delay-ms", "1500", "--log", $log) -PassThru
if (-not $p.WaitForExit(10000)) { Stop-Process -Id $p.Id -Force; Write-Output "TIMEOUT" } else {
    Write-Output ("exit code: " + $p.ExitCode)
    Get-Content -LiteralPath $log -Encoding UTF8 | Select-Object -Last 4 | ForEach-Object { Write-Output $_ }
}
Remove-Item -LiteralPath $log -Force -ErrorAction SilentlyContinue
