# Creates a distributable zip from the Release publish output + the config layer.
param(
    [ValidateSet('Debug', 'Release')] [string]$Configuration = 'Release',
    [ValidateSet('x64', 'ARM64')] [string]$Platform = 'x64',
    [switch]$SkipPublish
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

$publishDir = [System.IO.Path]::GetFullPath("$root\mpv-winui\mpv-winui\bin\win-$Platform\publish") + [System.IO.Path]::DirectorySeparatorChar
if ($SkipPublish) {
    Write-Host "==> Skipping publish, using existing output: $publishDir"
} else {
    Write-Host "==> Publishing mpv-winui [$Configuration|$Platform]"
    # mpv_winrt (C++) must be built with VS MSBuild first - see build.ps1.
    dotnet publish "$root\mpv-winui\mpv-winui\mpv-winui.csproj" `
        -c $Configuration -p:Platform=$Platform `
        -p:GenerateAppxPackageOnBuild=false -p:AppxPackageSigningEnabled=false `
        "-p:PublishDir=$publishDir" -v:m
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$pub = $publishDir.TrimEnd([System.IO.Path]::DirectorySeparatorChar)
if (-not (Test-Path -LiteralPath "$pub\mpv-winui.exe")) {
    Write-Error "Publish output not found: $pub (run build.ps1 + publish first, or omit -SkipPublish)"
}

$staging = Join-Path $root 'dist\staging'
if (Test-Path -LiteralPath $staging) {
    [System.IO.Directory]::Delete($staging, $true)
}
New-Item -ItemType Directory -Path $staging -Force | Out-Null

# Publish output without PDBs (keeps the archive lean).
robocopy $pub $staging /E /XF *.pdb /NFL /NDL /NJH /NJS /NP | Out-Null
# The mpv config layer (mpv-winui-lazy) is included in the publish output by the
# csproj and is auto-deployed to %LOCALAPPDATA%\mpv-winui\mpv on first run
# (ConfigDeployer), so no manual copy is needed here.

New-Item -ItemType Directory -Path "$root\dist" -Force | Out-Null
$zip = "$root\dist\mpv-winui-win-$Platform-$Configuration.zip"
if (Test-Path -LiteralPath $zip) {
    Remove-Item -LiteralPath $zip -Force
}
# Standard zip entries use forward slashes. PowerShell 5.1's Compress-Archive writes
# backslash separators, which breaks non-Windows unzip tools, so create entries explicitly.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipFs = [System.IO.File]::Create($zip)
$archive = New-Object System.IO.Compression.ZipArchive($zipFs, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $staging -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($staging.Length + 1).Replace('\', '/')
        $entry = $archive.CreateEntry($rel, [System.IO.Compression.CompressionLevel]::Optimal)
        $entryStream = $entry.Open()
        try {
            $bytes = [System.IO.File]::ReadAllBytes($_.FullName)
            $entryStream.Write($bytes, 0, $bytes.Length)
        }
        finally {
            $entryStream.Dispose()
        }
    }
}
finally {
    $archive.Dispose()
    $zipFs.Dispose()
}
[System.IO.Directory]::Delete($staging, $true)

Write-Host "Package created: $zip"
Write-Host "Size: $([math]::Round((Get-Item $zip).Length / 1MB, 1)) MB"
# pwsh 7 会把最后一条原生命令（robocopy）的退出码当作脚本退出码，
# 这里显式以 0 结束（robocopy 的 0-7 都算成功）。
exit 0
