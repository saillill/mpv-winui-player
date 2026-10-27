# v4.0 single-process regression: cascade opens an in-process submenu window,
# so exactly ONE MpvMenuHost process must exist; killing it leaves zero.
$ErrorActionPreference = 'Stop'

$exe = 'C:\Program Files\mpv-lazy\portable_config\tools\winui-menu-host\MpvMenuHost.exe'
$cascade = Join-Path (Split-Path -Parent $PSScriptRoot) 'test\cascade-menu.json'
Get-Process -Name MpvMenuHost -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

# test-select 0 opens the in-process submenu; large delay keeps the leaf from
# auto-selecting during the assertion window
$p = Start-Process -FilePath $exe -ArgumentList @(
    '--items', $cascade,
    '--no-connect',
    '--pos', '300,300',
    '--test-select', '0',
    '--test-delay-ms', '3000') -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2

$hosts = Get-Process -Name MpvMenuHost -ErrorAction SilentlyContinue
$single = ($hosts | Measure-Object).Count -eq 1
Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500
$left = Get-Process -Name MpvMenuHost -ErrorAction SilentlyContinue
$clean = ($left | Measure-Object).Count -eq 0

Write-Output ("single process after in-process cascade: " + $single)
Write-Output ("zero processes after kill: " + $clean)

if (-not ($single -and $clean)) {
    $left | ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }
    exit 1
}
exit 0
