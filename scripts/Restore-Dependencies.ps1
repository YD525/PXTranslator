$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.IO.Compression.FileSystem

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$manifestPath = Join-Path $repositoryRoot "dependencies.json"
$dependencyRoot = Join-Path $repositoryRoot "dependencies"
$temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$temporaryDirectory = Join-Path $temporaryRoot ("NIMTranslator-dependencies-" + [System.Guid]::NewGuid().ToString("N"))
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json

function Assert-DependencyMetadata {
    param([Parameter(Mandatory = $true)] $Dependency)

    if ([System.IO.Path]::GetFileName($Dependency.name) -ne $Dependency.name) {
        throw "The dependency name is invalid: $($Dependency.name)"
    }

    if ($Dependency.repository -notmatch "^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$") {
        throw "The dependency repository name is invalid: $($Dependency.name)"
    }

    if ($Dependency.tag -notmatch "^v[0-9]+\.[0-9]+\.[0-9]+(?:[-.][A-Za-z0-9.-]+)?$") {
        throw "The dependency tag is invalid: $($Dependency.name)"
    }

    foreach ($fileName in @($Dependency.asset, $Dependency.checksumAsset)) {
        if ([System.IO.Path]::GetFileName($fileName) -ne $fileName) {
            throw "The dependency asset name is invalid: $fileName"
        }
    }
}

function Get-VerifiedAsset {
    param(
        [Parameter(Mandatory = $true)] $Dependency,
        [Parameter(Mandatory = $true)] [string] $WorkingDirectory)

    Assert-DependencyMetadata -Dependency $Dependency

    $releaseBaseUri = "https://github.com/$($Dependency.repository)/releases/download/$($Dependency.tag)"
    $assetPath = Join-Path $WorkingDirectory $Dependency.asset
    $checksumPath = Join-Path $WorkingDirectory $Dependency.checksumAsset

    Invoke-WebRequest -Uri "$releaseBaseUri/$($Dependency.asset)" -OutFile $assetPath
    Invoke-WebRequest -Uri "$releaseBaseUri/$($Dependency.checksumAsset)" -OutFile $checksumPath

    $checksumLine = (Get-Content -LiteralPath $checksumPath -Raw).Trim()
    $checksumMatch = [System.Text.RegularExpressions.Regex]::Match(
        $checksumLine,
        "^(?<Hash>[A-Fa-f0-9]{64})\s+[*]?(?<FileName>.+)$")

    if (-not $checksumMatch.Success -or $checksumMatch.Groups["FileName"].Value -ne $Dependency.asset) {
        throw "The dependency checksum file has an invalid format: $($Dependency.name)"
    }

    $expectedHash = $checksumMatch.Groups["Hash"].Value
    $actualHash = (Get-FileHash -LiteralPath $assetPath -Algorithm SHA256).Hash
    if (-not $actualHash.Equals($expectedHash, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "The dependency checksum does not match: $($Dependency.name)"
    }

    return $assetPath
}

if (-not $temporaryDirectory.StartsWith($temporaryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The temporary directory is outside the system temporary directory."
}

New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
New-Item -ItemType Directory -Path $dependencyRoot -Force | Out-Null

try {
    foreach ($dependency in $manifest.files) {
        $workingDirectory = Join-Path $temporaryDirectory $dependency.name
        New-Item -ItemType Directory -Path $workingDirectory | Out-Null
        $assetPath = Get-VerifiedAsset -Dependency $dependency -WorkingDirectory $workingDirectory
        Copy-Item -LiteralPath $assetPath -Destination (Join-Path $dependencyRoot $dependency.asset) -Force
    }

    foreach ($dependency in $manifest.archives) {
        if ([System.IO.Path]::GetFileName($dependency.destination) -ne $dependency.destination) {
            throw "The dependency destination is invalid: $($dependency.name)"
        }

        $workingDirectory = Join-Path $temporaryDirectory $dependency.name
        New-Item -ItemType Directory -Path $workingDirectory | Out-Null
        $assetPath = Get-VerifiedAsset -Dependency $dependency -WorkingDirectory $workingDirectory
        $destination = [System.IO.Path]::GetFullPath((Join-Path $dependencyRoot $dependency.destination))
        $destinationPrefix = $destination + [System.IO.Path]::DirectorySeparatorChar

        if (-not $destinationPrefix.StartsWith(
            [System.IO.Path]::GetFullPath($dependencyRoot) + [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "The dependency destination is outside the dependency directory."
        }

        $archive = [System.IO.Compression.ZipFile]::OpenRead($assetPath)
        try {
            foreach ($entry in $archive.Entries) {
                $entryPath = [System.IO.Path]::GetFullPath((Join-Path $destination $entry.FullName))
                if (-not $entryPath.StartsWith($destinationPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                    throw "The dependency archive contains an unsafe path: $($entry.FullName)"
                }
            }
        }
        finally {
            $archive.Dispose()
        }

        if (Test-Path -LiteralPath $destination) {
            Remove-Item -LiteralPath $destination -Recurse -Force
        }

        New-Item -ItemType Directory -Path $destination | Out-Null
        Expand-Archive -LiteralPath $assetPath -DestinationPath $destination
    }
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}
