$tempRoot = [System.IO.Path]::GetFullPath($env:TEMP)
$mpvRoot = [System.IO.Path]::GetFullPath("C:\Program Files\mpv-lazy")
$targets = @(Get-ChildItem -Path $tempRoot -Filter "mpv-menu-host-items-*.json" -File -ErrorAction SilentlyContinue) + @(Get-ChildItem -Path $mpvRoot -Filter "mpv-menu-host-items.json" -File -Recurse -Depth 2 -ErrorAction SilentlyContinue)
foreach ($t in $targets) {
    $resolved = [System.IO.Path]::GetFullPath($t.FullName)
    if ($resolved.StartsWith($tempRoot) -or $resolved.StartsWith($mpvRoot)) {
        Remove-Item -LiteralPath $resolved -Force
        Write-Output ("deleted " + $resolved)
    }
}
