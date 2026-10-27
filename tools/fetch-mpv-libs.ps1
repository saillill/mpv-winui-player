# Fetches the per-architecture native libraries mpv-winui-player builds against
# and ships:
#
#   mpv-2.dll + mpv.lib + headers   the mpv engine (linked, then loaded at runtime)
#   MediaInfo.dll + mediainfo.lib   media information (loaded at runtime)
#
# Installed layout (see NativeLibraryLayout in mpv-winrt\mpv-winrt.vcxproj and the
# Content items plus CheckNativeRuntimeLibraries in mpv-winui.csproj):
#
#   libs\include            headers, shared by every architecture
#   libs\lib\<Platform>\    import libraries
#   libs\bin\<Platform>\    runtime DLLs
#   libs\share\             mpv data files
#
# x64 is downloaded from the two upstream builders the CI workflow pins, and both
# archives are verified against the sha256 published on their release page before
# anything is extracted. These binaries end up inside a signed MSI and a release
# zip; an unverified download is the one step in that chain where a substituted
# asset would go unnoticed.
#
# ARM64 has no equivalent upstream: both builders publish x64 only, so there is
# nothing to download here and the script says so rather than pretending. Supply
# an aarch64 libmpv (and MediaInfo, if one exists) from a source you trust, drop
# the files at the paths above, and the rest of the pipeline builds unchanged.
param(
    [string]$Root,
    [ValidateSet('x64', 'ARM64')][string]$Platform = 'x64',
    [string]$MpvVersion = '20260731',
    [string]$MediaInfoVersion = '0.1.0',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

if (-not $Root) {
    # <root>\tools\fetch-mpv-libs.ps1 -> parent tools -> parent root
    $Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
}

$libsDir    = Join-Path $Root 'src\libs'
$binDir     = Join-Path $libsDir "bin\$Platform"
$libDir     = Join-Path $libsDir "lib\$Platform"
$includeDir = Join-Path $libsDir 'include'
$shareDir   = Join-Path $libsDir 'share'

$mpvDll = Join-Path $binDir 'mpv-2.dll'
$mpvLib = Join-Path $libDir 'mpv.lib'

# Copy the sha256 exactly as published on each release page. If a version pin
# above is changed, these must be changed in the same edit; the script refuses to
# use a download that does not match, so a forgotten hash fails loudly rather
# than silently accepting new bytes.
$MpvSha256       = 'fdcbdefc006a23a7d771e05ac74b4241441dc6367168544d1e7a924b1e9c42ac'
$MediaInfoSha256 = '6d26e9fb9687bdcaf78d8541e7bf0ef64489deaf93598ca798625ebbb3eefd97'

$MpvUrl       = "https://github.com/ikas-mc/mpv-windows-builder/releases/download/$MpvVersion/mpv-2.dll.zip"
$MediaInfoUrl = "https://github.com/ikas-mc/MediaInfoLib-builder/releases/download/$MediaInfoVersion/mediainfo.dll.zip"

function Assert-Sha256 {
    param([string]$Path, [string]$Expected, [string]$What)
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
    $want = $Expected.ToUpperInvariant()
    if ($actual -ne $want) {
        throw "$What failed sha256 verification.`n  expected: $want`n  actual:   $actual`nThe download was rejected and not extracted. Delete it and retry, or check whether the upstream release asset was replaced."
    }
    Write-Host "    sha256 verified: $(Split-Path -Leaf $Path)"
}

function Get-VerifiedArchive {
    param([string]$Url, [string]$Sha256, [string]$Label)
    $archive = Join-Path $env:TEMP "$Label-$(Split-Path -Leaf $Url)"
    if ($Force -or -not (Test-Path -LiteralPath $archive)) {
        Write-Host "==> Downloading $Label"
        Write-Host "    $Url"
        Invoke-WebRequest -Uri $Url -OutFile $archive -UseBasicParsing
    } else {
        Write-Host "==> Reusing cached $Label"
    }
    Assert-Sha256 -Path $archive -Expected $Sha256 -What $Label
    return $archive
}

# Places a payload file where the build looks for it. Searches rather than
# assumes: the two upstream archives do not agree on whether their payload sits
# at the archive root or under bin\ / lib\.
function Install-PayloadFile {
    param([string]$ExtractedRoot, [string]$FileName, [string]$DestinationDir)
    $found = @(Get-ChildItem -LiteralPath $ExtractedRoot -Recurse -File -Filter $FileName)
    if ($found.Count -eq 0) {
        return $false
    }
    if (-not (Test-Path -LiteralPath $DestinationDir)) {
        New-Item -ItemType Directory -Path $DestinationDir -Force | Out-Null
    }
    foreach ($hit in $found) {
        Copy-Item -LiteralPath $hit.FullName -Destination (Join-Path $DestinationDir $FileName) -Force
    }
    Write-Host "    installed $FileName -> $DestinationDir"
    return $true
}

if ($Platform -ne 'x64') {
    Write-Host ""
    Write-Host "No download source for $Platform." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  The upstream builders this project pins publish x64 binaries only, so"
    Write-Host "  there is nothing to fetch for $Platform. To build this configuration,"
    Write-Host "  supply an $Platform build of the mpv development libraries from a source"
    Write-Host "  you trust and place them here:"
    Write-Host ""
    Write-Host "    $mpvLib"
    Write-Host "    $mpvDll"
    Write-Host "    $(Join-Path $binDir 'MediaInfo.dll')"
    Write-Host ""
    Write-Host "  The headers in $includeDir are architecture independent and are"
    Write-Host "  reused as-is. The build refuses to produce a package without these files"
    Write-Host "  (CheckNativeLibraries in the C++ project, CheckNativeRuntimeLibraries in"
    Write-Host "  the app project), so an incomplete $Platform package cannot be produced"
    Write-Host "  by accident."
    Write-Host ""
    exit 1
}

if (-not $Force -and (Test-Path -LiteralPath $mpvDll) -and (Test-Path -LiteralPath $mpvLib)) {
    Write-Host "mpv libs already present for $Platform`: $binDir"
    exit 0
}

$mpvArchive = Get-VerifiedArchive -Url $MpvUrl -Sha256 $MpvSha256 -Label 'mpv-libs'
$miArchive  = Get-VerifiedArchive -Url $MediaInfoUrl -Sha256 $MediaInfoSha256 -Label 'mediainfo-libs'

$temp = Join-Path $env:TEMP "mpv-fetch-$Platform-$(Get-Random)"
New-Item -ItemType Directory -Path $temp -Force | Out-Null
try {
    Write-Host "==> Extracting"
    Expand-Archive -LiteralPath $mpvArchive -DestinationPath (Join-Path $temp 'mpv') -Force
    Expand-Archive -LiteralPath $miArchive  -DestinationPath (Join-Path $temp 'mediainfo') -Force

    $mpvRoot = Join-Path $temp 'mpv'
    $miRoot  = Join-Path $temp 'mediainfo'

    $okDll = Install-PayloadFile -ExtractedRoot $mpvRoot -FileName 'mpv-2.dll' -DestinationDir $binDir
    $okLib = Install-PayloadFile -ExtractedRoot $mpvRoot -FileName 'mpv.lib'   -DestinationDir $libDir
    if (-not ($okDll -and $okLib)) {
        throw "The mpv archive did not contain both mpv-2.dll and mpv.lib. Contents may have changed upstream; inspect $mpvRoot and adjust Install-PayloadFile."
    }

    Install-PayloadFile -ExtractedRoot $miRoot -FileName 'MediaInfo.dll' -DestinationDir $binDir | Out-Null
    Install-PayloadFile -ExtractedRoot $miRoot -FileName 'mediainfo.lib' -DestinationDir $libDir | Out-Null

    # Headers and mpv data files are shared across architectures: install them
    # only if this is the first architecture fetched.
    foreach ($pair in @(@('include', $includeDir), @('share', $shareDir))) {
        $src = Join-Path $mpvRoot $pair[0]
        if ((Test-Path -LiteralPath $src) -and -not (Test-Path -LiteralPath $pair[1])) {
            Move-Item -LiteralPath $src -Destination $pair[1] -Force
            Write-Host "    installed $($pair[0]) -> $($pair[1])"
        }
    }
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "mpv libs installed for $Platform`: $binDir"
