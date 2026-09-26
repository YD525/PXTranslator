param(
    [string]$ReportPath
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runner = Join-Path $repositoryRoot 'NIMTranslator.PresetTests\bin\x64\Release\NIMTranslator.PresetTests.exe'

if (-not (Test-Path -LiteralPath $runner)) {
    throw 'Build the Release x64 solution before measuring preview performance.'
}

$processor = Get-CimInstance Win32_Processor | Select-Object -First 1
$computer = Get-CimInstance Win32_ComputerSystem
$revision = git -C $repositoryRoot rev-parse HEAD
$workingTreeDirty = -not [string]::IsNullOrWhiteSpace((git -C $repositoryRoot status --porcelain))
$lines = @(
    "TimestampUtc,$([DateTime]::UtcNow.ToString('o'))"
    "Revision,$revision"
    "WorkingTreeDirty,$workingTreeDirty"
    "Configuration,Release x64"
    "OperatingSystem,$([Environment]::OSVersion.VersionString)"
    "Processor,$($processor.Name.Trim())"
    "LogicalProcessors,$($computer.NumberOfLogicalProcessors)"
    "MemoryBytes,$($computer.TotalPhysicalMemory)"
)

$measurement = & $runner --performance 2>&1
$exitCode = $LASTEXITCODE
$lines += $measurement
$lines | Write-Output

if (-not [string]::IsNullOrWhiteSpace($ReportPath)) {
    $resolvedReportPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ReportPath)
    $reportDirectory = Split-Path -Parent $resolvedReportPath
    if (-not (Test-Path -LiteralPath $reportDirectory)) {
        New-Item -ItemType Directory -Path $reportDirectory | Out-Null
    }

    $lines | Set-Content -LiteralPath $resolvedReportPath -Encoding UTF8
}

exit $exitCode
