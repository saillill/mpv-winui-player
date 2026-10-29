param(
    [Parameter(Mandatory = $true)][string]$Path,
    [Parameter(Mandatory = $true)][ValidateSet('bom', 'nobom')][string]$Bom,
    [Parameter(Mandatory = $true)][ValidateSet('crlf', 'lf')][string]$Eol
)
# Restore the encoding shape the git index has for a file, after a tool that
# normalises both away. Two independent knobs on purpose: this repo has all
# four combinations, and guessing either one shows up as a whole-file rewrite
# in `git diff --stat` rather than as a one-line change.
$full = (Resolve-Path $Path).Path
$text = [System.IO.File]::ReadAllText($full)
$text = $text.TrimStart([char]0xFEFF)
$text = $text -replace "`r`n", "`n"
if ($Eol -eq 'crlf') { $text = $text -replace "`n", "`r`n" }
$enc = New-Object System.Text.UTF8Encoding($Bom -eq 'bom')
[System.IO.File]::WriteAllText($full, $text, $enc)

$b = [System.IO.File]::ReadAllBytes($full)
$hasBom = ($b.Length -ge 3 -and $b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF)
$crlf = 0; $lf = 0
for ($i = 0; $i -lt $b.Length; $i++) {
    if ($b[$i] -eq 10) { if ($i -gt 0 -and $b[$i - 1] -eq 13) { $crlf++ } else { $lf++ } }
}
Write-Host "$Path BOM=$hasBom CRLF=$crlf bareLF=$lf"
