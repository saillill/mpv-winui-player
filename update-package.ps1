# Rewrites the Package.appxmanifest version and identity before packaging.
#
# Version resolution order:
#   1. $env:VERSION          — full semantic version "X.Y.Z" or "X.Y.Z.B"
#   2. $env:GITHUB_REF_NAME  — a tag ref like "v1.2.3" (CI tag-triggered runs)
#   3. $env:VERSION_BUILD_NUMBER — legacy: only replaces the 4th component
#   4. none                   — version left as-is
# Identity.Name / DisplayName are always forced to "mpvw".
# Identity.Publisher is replaced only when $env:APPX_PUBLISHER is set (see below).

$manifestPaths = @(
    "mpv-winui/mpv-winui/Package.appxmanifest"
)

foreach ($manifestPath in $manifestPaths) {
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        Write-Warning "Manifest not found: $manifestPath"
        continue
    }

    $appxManifestPath = Convert-Path $manifestPath
    [xml]$manifest = Get-Content -Path $appxManifestPath

    $version = $null
    if ($env:VERSION) {
        $version = $env:VERSION
        Write-Host "Using VERSION env: $version"
    } elseif ($env:GITHUB_REF_NAME -and $env:GITHUB_REF_NAME -match '^v(\d+\.\d+\.\d+(\.\d+)?)$') {
        $version = $Matches[1]
        Write-Host "Using git tag ref: $version"
    }

    if ($version) {
        $versionParts = $version -split '\.'
        if ($versionParts.Count -eq 3) { $versionParts += '0' }
        if ($versionParts.Count -ne 4) {
            Write-Error "Version must be X.Y.Z or X.Y.Z.B: $version"
            exit 1
        }
        foreach ($part in $versionParts) {
            if ($part -notmatch '^\d+$') {
                Write-Error "Version components must be numeric: $version"
                exit 1
            }
        }
        $manifest.Package.Identity.Version = $versionParts -join "."
        Write-Host "package new version ($appxManifestPath): $($manifest.Package.Identity.Version)"
    } elseif ($env:VERSION_BUILD_NUMBER -gt 0) {
        $version = $manifest.Package.Identity.Version
        $versionParts = $version -split '\.'

        if ($versionParts.Length -ne 4) {
            Write-Error "Version format error: $version ($appxManifestPath)"
            exit 1
        }

        $versionParts[3] = $env:VERSION_BUILD_NUMBER
        $manifest.Package.Identity.Version = $versionParts -join "."
        Write-Host "package new version ($appxManifestPath): $($manifest.Package.Identity.Version)"
    }


    $manifest.Package.Identity.Name = "mpvw"
    $manifest.Package.Properties.DisplayName = "mpvw"
    ($manifest.GetElementsByTagName("uap:VisualElements")).SetAttribute("DisplayName", "mpvw")

    # Publisher has to equal the subject of the certificate the package is signed
    # with, or Windows refuses to install it (0x80073CF0). The value in the
    # repository is the development identity, which is what local sideloading
    # wants; a signed CI package needs the CI certificate's subject instead, and
    # that cannot be known from here, so it is taken from the environment rather
    # than guessed. Left alone when unset, so local behaviour is unchanged.
    if ($env:APPX_PUBLISHER) {
        $manifest.Package.Identity.Publisher = $env:APPX_PUBLISHER
        Write-Host "package publisher ($appxManifestPath): $env:APPX_PUBLISHER"
    } else {
        Write-Host "package publisher ($appxManifestPath): left as $($manifest.Package.Identity.Publisher) (set APPX_PUBLISHER to override)"
    }

    $manifest.Save($appxManifestPath)
}
