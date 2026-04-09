param(
    [string]$IfcFilePath = "ifc/01_26_Slavyanka_4.ifc",
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'

function Convert-ToMicroseconds([string]$value)
{
    if ([string]::IsNullOrWhiteSpace($value))
    {
        return $null
    }

    $normalized = $value -replace 'μs', '' -replace 'us', '' -replace '\s', '' -replace ',', ''
    return [double]::Parse($normalized, [System.Globalization.CultureInfo]::InvariantCulture)
}

function Convert-ToKilobytes([string]$value)
{
    if ([string]::IsNullOrWhiteSpace($value))
    {
        return $null
    }

    $normalized = $value -replace 'KB', '' -replace '\s', '' -replace ',', ''
    return [double]::Parse($normalized, [System.Globalization.CultureInfo]::InvariantCulture)
}

function Format-TimeByMagnitude([double]$microseconds)
{
    if ($null -eq $microseconds)
    {
        return "n/a"
    }

    if ($microseconds -ge 1000000)
    {
        $value = $microseconds / 1000000
        return [string]::Format([System.Globalization.CultureInfo]::InvariantCulture, '{0:0.###} s', $value)
    }

    if ($microseconds -ge 1000)
    {
        $value = $microseconds / 1000
        return [string]::Format([System.Globalization.CultureInfo]::InvariantCulture, '{0:0.###} ms', $value)
    }

    return [string]::Format([System.Globalization.CultureInfo]::InvariantCulture, '{0:0.###} μs', $microseconds)
}

function Format-SizeByMagnitude([double]$kilobytes)
{
    if ($null -eq $kilobytes)
    {
        return "n/a"
    }

    if ($kilobytes -ge 1048576)
    {
        $value = $kilobytes / 1048576
        return [string]::Format([System.Globalization.CultureInfo]::InvariantCulture, '{0:0.###} GB', $value)
    }

    if ($kilobytes -ge 1024)
    {
        $value = $kilobytes / 1024
        return [string]::Format([System.Globalization.CultureInfo]::InvariantCulture, '{0:0.###} MB', $value)
    }

    return [string]::Format([System.Globalization.CultureInfo]::InvariantCulture, '{0:0.###} KB', $kilobytes)
}

function Parse-BenchmarkCsv([string]$csvPath)
{
    $rows = Import-Csv -Path $csvPath -Delimiter ';'
    $result = @{}

    foreach ($row in $rows)
    {
        $meanUs = Convert-ToMicroseconds $row.Mean
        $allocatedKb = Convert-ToKilobytes $row.Allocated

        $result[$row.Method] = [pscustomobject]@{
            Method = $row.Method
            MeanRaw = $row.Mean
            MeanUs = $meanUs
            MeanFormatted = Format-TimeByMagnitude $meanUs
            AllocatedRaw = $row.Allocated
            AllocatedKb = $allocatedKb
            AllocatedFormatted = Format-SizeByMagnitude $allocatedKb
        }
    }

    return $result
}

function Format-Delta([double]$current, [double]$previous)
{
    if ($null -eq $current -or $null -eq $previous -or $previous -eq 0)
    {
        return "n/a"
    }

    $delta = (($current - $previous) / $previous) * 100
    return [string]::Format([System.Globalization.CultureInfo]::InvariantCulture, '{0:+0.00;-0.00;0.00}%', $delta)
}

if (-not (Test-Path -Path $IfcFilePath))
{
    throw "IFC file not found: $IfcFilePath"
}

$resolvedIfcPath = (Resolve-Path $IfcFilePath).Path
$env:IFC_BENCHMARK_FILE = $resolvedIfcPath

if (-not $SkipTests)
{
    Write-Host "Running tests..."
    dotnet test ifc-metadata.slnx -c Release --nologo
    if ($LASTEXITCODE -ne 0)
    {
        throw "Test run failed with exit code $LASTEXITCODE"
    }
}

Write-Host "Running benchmarks for: $resolvedIfcPath"
dotnet run --project benchmarks/ifc-metadata.Benchmarks.csproj -c Release
if ($LASTEXITCODE -ne 0)
{
    throw "Benchmark run failed with exit code $LASTEXITCODE"
}

$resultsDir = Join-Path (Get-Location) "benchmarks/results"
$latestDir = Join-Path $resultsDir "latest"
$previousDir = Join-Path $resultsDir "previous"
$artifactDir = Join-Path (Get-Location) "BenchmarkDotNet.Artifacts/results"

New-Item -Path $resultsDir -ItemType Directory -Force | Out-Null
New-Item -Path $latestDir -ItemType Directory -Force | Out-Null
New-Item -Path $previousDir -ItemType Directory -Force | Out-Null

$stamp = Get-Date -Format "yyyy-MM-dd-HHmmss"
$currentCsvArtifact = Join-Path $artifactDir "IfcFilePipelineBenchmark-report.csv"
$currentMdArtifact = Join-Path $artifactDir "IfcFilePipelineBenchmark-report-github.md"

if (-not (Test-Path $currentCsvArtifact))
{
    throw "Benchmark CSV artifact was not found: $currentCsvArtifact"
}

if (-not (Test-Path $currentMdArtifact))
{
    throw "Benchmark markdown artifact was not found: $currentMdArtifact"
}

$latestCsv = Join-Path $latestDir "IfcFilePipelineBenchmark-report.csv"
$latestMd = Join-Path $latestDir "IfcFilePipelineBenchmark-report-github.md"
$previousCsv = Join-Path $previousDir "IfcFilePipelineBenchmark-report.csv"
$previousMd = Join-Path $previousDir "IfcFilePipelineBenchmark-report-github.md"

if (Test-Path $latestCsv)
{
    Copy-Item $latestCsv $previousCsv -Force
}

if (Test-Path $latestMd)
{
    Copy-Item $latestMd $previousMd -Force
}

Copy-Item $currentCsvArtifact $latestCsv -Force
Copy-Item $currentMdArtifact $latestMd -Force

Copy-Item $currentCsvArtifact (Join-Path $resultsDir "$stamp-IfcFilePipelineBenchmark-report.csv") -Force
Copy-Item $currentMdArtifact (Join-Path $resultsDir "$stamp-IfcFilePipelineBenchmark-report-github.md") -Force

$comparisonSourcePath = $null
$comparisonSourceLabel = $null
$previousFromGitPath = Join-Path $env:TEMP "ifc-prev-commit-benchmark-$stamp.csv"

$gitPrevious = git show HEAD~1:benchmarks/results/latest/IfcFilePipelineBenchmark-report.csv 2>$null
if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace(($gitPrevious -join "")))
{
    $gitPrevious | Set-Content -Path $previousFromGitPath -Encoding UTF8
    $comparisonSourcePath = $previousFromGitPath
    $comparisonSourceLabel = "previous commit (HEAD~1)"
}
elseif (Test-Path $previousCsv)
{
    $comparisonSourcePath = $previousCsv
    $comparisonSourceLabel = "previous local run"
}

$comparisonFileLatest = Join-Path $latestDir "IfcFilePipelineBenchmark-comparison.md"
$comparisonFileStamped = Join-Path $resultsDir "$stamp-IfcFilePipelineBenchmark-comparison.md"

$currentData = Parse-BenchmarkCsv $latestCsv

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# Benchmark comparison report")
$lines.Add("")
$lines.Add("Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$lines.Add("IFC file: $resolvedIfcPath")
$lines.Add("")

if ($comparisonSourcePath)
{
    $previousData = Parse-BenchmarkCsv $comparisonSourcePath
    $lines.Add("Comparison source: $comparisonSourceLabel")
    $lines.Add("")
    $lines.Add("| Method | Previous Mean | Current Mean | Delta Mean | Previous Allocated | Current Allocated | Delta Allocated |")
    $lines.Add("|---|---:|---:|---:|---:|---:|---:|")

    foreach ($method in $currentData.Keys)
    {
        $currentRow = $currentData[$method]
        $previousRow = $previousData[$method]

        if ($null -eq $previousRow)
        {
            $lines.Add("| $method | n/a | $($currentRow.MeanFormatted) | n/a | n/a | $($currentRow.AllocatedFormatted) | n/a |")
            continue
        }

        $deltaMean = Format-Delta $currentRow.MeanUs $previousRow.MeanUs
        $deltaAllocated = Format-Delta $currentRow.AllocatedKb $previousRow.AllocatedKb
        $lines.Add("| $method | $($previousRow.MeanFormatted) | $($currentRow.MeanFormatted) | $deltaMean | $($previousRow.AllocatedFormatted) | $($currentRow.AllocatedFormatted) | $deltaAllocated |")
    }
}
else
{
    $lines.Add("Comparison source is not available (no previous commit report and no previous local snapshot).")
    $lines.Add("")
    $lines.Add("| Method | Current Mean | Current Allocated |")
    $lines.Add("|---|---:|---:|")

    foreach ($method in $currentData.Keys)
    {
        $row = $currentData[$method]
        $lines.Add("| $method | $($row.MeanFormatted) | $($row.AllocatedFormatted) |")
    }
}

$lines | Set-Content -Path $comparisonFileLatest -Encoding UTF8
$lines | Set-Content -Path $comparisonFileStamped -Encoding UTF8

if (Test-Path $previousFromGitPath)
{
    Remove-Item $previousFromGitPath -Force
}

Write-Host "Saved benchmark snapshots to: $resultsDir"
Write-Host "Saved latest comparison report: $comparisonFileLatest"
Write-Host "Saved stamped comparison report: $comparisonFileStamped"

if (Test-Path "BenchmarkDotNet.Artifacts")
{
    Remove-Item -Recurse -Force "BenchmarkDotNet.Artifacts"
}
