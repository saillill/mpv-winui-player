# Sync mpv-winui-lazy config project into the app config dir
# (%LOCALAPPDATA%\mpv-winui\mpv, unpackaged mode).
param()
$ErrorActionPreference = 'Stop'
$src = $PSScriptRoot
$Target = Join-Path $env:LOCALAPPDATA 'mpv-winui\mpv'
New-Item -ItemType Directory -Path $Target -Force | Out-Null
# /MIR keeps the target identical to the source (removes stale files such as the
# old tools\ menu host); runtime artifacts are excluded and left untouched.
robocopy $src $Target /MIR /XD _cache cache /XF saved-props.json recent.json *.log *.bak* /NFL /NDL /NJH /NJS /NP | Out-Null
$code = $LASTEXITCODE
if ($code -le 7) { Write-Output "config deployed: $src -> $Target"; exit 0 }
exit $code
