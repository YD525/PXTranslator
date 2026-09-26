param(
    [string]$ProjectRoot = (Join-Path $PSScriptRoot '..\Phoenix_Translator')
)

$ErrorActionPreference = 'Stop'
$manifestPath = Join-Path $PSScriptRoot 'FluentIconSubset.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$fontPath = Join-Path $ProjectRoot 'Assets\Fonts\NIMFluentIcons.ttf'
$sourcePath = Join-Path $ProjectRoot 'ThirdParty\FluentSystemIcons\SOURCE.json'
$registryPath = Join-Path $ProjectRoot 'Application\Generated\PreviewIconRegistry.g.cs'

foreach ($required in @($fontPath, $sourcePath, $registryPath)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Required icon asset is missing: $required" }
}

$semanticNames = @($manifest | ForEach-Object { [string]$_.semantic })
$upstreamNames = @($manifest | ForEach-Object { [string]$_.upstream })
if (($semanticNames | Sort-Object -Unique).Count -ne $semanticNames.Count) { throw 'Semantic icon names must be unique.' }
if (($upstreamNames | Sort-Object -Unique).Count -ne $upstreamNames.Count) { throw 'Upstream glyph names must be unique.' }

$source = Get-Content -LiteralPath $sourcePath -Raw | ConvertFrom-Json
$actualHash = (Get-FileHash -LiteralPath $fontPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne $source.subsetFontSha256) { throw 'The embedded font does not match SOURCE.json.' }
if ([int]$source.iconCount -ne $manifest.Count) { throw 'The source record icon count does not match the manifest.' }

$registry = Get-Content -LiteralPath $registryPath -Raw
foreach ($entry in $manifest) {
    if ($registry -notmatch [regex]::Escape("PreviewIconName.$($entry.semantic)")) {
        throw "The generated registry is missing '$($entry.semantic)'."
    }
    if ($registry -notmatch [regex]::Escape([string]$entry.upstream)) {
        throw "The generated registry is missing '$($entry.upstream)'."
    }
}

$previewXaml = Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'UIManagement\Preview') -Filter '*.xaml'
$visibleGlyphPattern = '[▶►◀◄✕✖✓✔⚠⚙📁📄🔍]'
foreach ($file in $previewXaml) {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    if ($text -match $visibleGlyphPattern) { throw "Untracked visible icon glyph in $($file.Name)." }
    $matches = [regex]::Matches($text, '(?:Icon="|Property="Icon"\s+Value=")([A-Za-z0-9]+)"')
    foreach ($match in $matches) {
        $name = $match.Groups[1].Value
        if ($semanticNames -notcontains $name) { throw "Unknown semantic icon '$name' in $($file.Name)." }
    }
}

Write-Output "Preview icon verification passed for $($manifest.Count) semantic icons."
