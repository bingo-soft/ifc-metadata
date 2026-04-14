param(
    [string]$IfcFilePath = "ifc/01_26_Slavyanka_4.ifc",
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'

function Convert-ToInvariantNumber([string]$numberText)
{
    $normalized = $numberText.Trim()

    $lastComma = $normalized.LastIndexOf(',')
    $lastDot = $normalized.LastIndexOf('.')

    if ($lastComma -ge 0 -and $lastDot -ge 0)
    {
        $decimalSeparator = if ($lastComma -gt $lastDot) { ',' } else { '.' }
        $thousandSeparator = if ($decimalSeparator -eq ',') { '.' } else { ',' }

        $normalized = $normalized.Replace([string]$thousandSeparator, '')
        if ($decimalSeparator -eq ',')
        {
            $normalized = $normalized.Replace(',', '.')
        }

        return [double]::Parse($normalized, [System.Globalization.CultureInfo]::InvariantCulture)
    }

    if ($lastComma -ge 0)
    {
        $commaCount = ($normalized.Length - $normalized.Replace(',', '').Length)
        if ($commaCount -gt 1)
        {
            $normalized = $normalized.Replace(',', '')
        }
        else
        {
            $split = $normalized.Split(',')
            $fractionLength = $split[1].Length
            if ($fractionLength -eq 3 -and $split[0].Length -gt 3)
            {
                $normalized = $normalized.Replace(',', '')
            }
            else
            {
                $normalized = $normalized.Replace(',', '.')
            }
        }
    }
    elseif ($lastDot -ge 0)
    {
        $dotCount = ($normalized.Length - $normalized.Replace('.', '').Length)
        if ($dotCount -gt 1)
        {
            $lastIndex = $normalized.LastIndexOf('.')
            $intPart = $normalized.Substring(0, $lastIndex).Replace('.', '')
            $fraction = $normalized.Substring($lastIndex + 1)
            $normalized = "$intPart.$fraction"
        }
    }

    return [double]::Parse($normalized, [System.Globalization.CultureInfo]::InvariantCulture)
}

function Convert-ToMicroseconds([string]$value)
{
    if ([string]::IsNullOrWhiteSpace($value))
    {
        return $null
    }

    $trimmed = $value.Trim().Trim('"').Replace([char]0xA0, ' ').Replace([char]0x202F, ' ')
    $numberMatch = [regex]::Match($trimmed, '[0-9][0-9\.,]*')
    if (-not $numberMatch.Success)
    {
        throw "Unable to parse benchmark time value: '$value'"
    }

    $number = Convert-ToInvariantNumber $numberMatch.Value
    $unitMatch = [regex]::Match($trimmed, '(μs|µs|us|ms|s)\s*$')
    $unit = if ($unitMatch.Success) { $unitMatch.Groups[1].Value } else { '' }

    switch ($unit)
    {
        's' { return $number * 1000000 }
        'ms' { return $number * 1000 }
        'us' { return $number }
        'μs' { return $number }
        'µs' { return $number }
        '' { return $number }
        default { throw "Unsupported time unit in benchmark value: '$value'" }
    }
}

function Convert-ToKilobytes([string]$value)
{
    if ([string]::IsNullOrWhiteSpace($value))
    {
        return $null
    }

    $trimmed = $value.Trim().Trim('"').Replace([char]0xA0, ' ').Replace([char]0x202F, ' ')
    $numberMatch = [regex]::Match($trimmed, '[0-9][0-9\.,]*')
    if (-not $numberMatch.Success)
    {
        throw "Unable to parse benchmark memory value: '$value'"
    }

    $number = Convert-ToInvariantNumber $numberMatch.Value
    $unitMatch = [regex]::Match($trimmed, '(KB|MB|GB)\s*$')
    $unit = if ($unitMatch.Success) { $unitMatch.Groups[1].Value } else { '' }

    switch ($unit)
    {
        'GB' { return $number * 1048576 }
        'MB' { return $number * 1024 }
        'KB' { return $number }
        '' { return $number }
        default { throw "Unsupported memory unit in benchmark value: '$value'" }
    }
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

function Get-BenchmarkCaseName($row)
{
    $benchmarkParameterColumns = @('PreserveOrder', 'Job')
    $parameterPairs = @()

    foreach ($columnName in $benchmarkParameterColumns)
    {
        $property = $row.PSObject.Properties[$columnName]
        if ($null -eq $property)
        {
            continue
        }

        if ([string]::IsNullOrWhiteSpace([string]$property.Value))
        {
            continue
        }

        $parameterPairs += "$($property.Name)=$($property.Value)"
    }

    if ($parameterPairs.Count -eq 0)
    {
        return $row.Method
    }

    return "$($row.Method) [$($parameterPairs -join ', ')]"
}

function Get-PropertyValueOrDefault($row, [string]$propertyName, [string]$defaultValue)
{
    $property = $row.PSObject.Properties[$propertyName]
    if ($null -eq $property)
    {
        return $defaultValue
    }

    $valueText = [string]$property.Value
    if ([string]::IsNullOrWhiteSpace($valueText))
    {
        return $defaultValue
    }

    return $valueText
}

function Get-BenchmarkCaseKey([string]$methodName, [string]$preserveOrderValue, [string]$jobName)
{
    return "$methodName|PreserveOrder=$preserveOrderValue|Job=$jobName"
}

function Parse-PreserveOrderValue([string]$preserveOrderText)
{
    if ([string]::IsNullOrWhiteSpace($preserveOrderText))
    {
        return $null
    }

    $parsed = $false
    if ([bool]::TryParse($preserveOrderText, [ref]$parsed))
    {
        return $parsed
    }

    return $null
}

function Parse-BenchmarkCsv([string]$csvPath)
{
    $rows = Import-Csv -Path $csvPath -Delimiter ';'
    $result = @{}

    foreach ($row in $rows)
    {
        $methodName = [string]$row.Method
        $preserveOrderValue = Get-PropertyValueOrDefault $row 'PreserveOrder' 'n/a'
        $jobName = Get-PropertyValueOrDefault $row 'Job' 'DefaultJob'
        $caseName = Get-BenchmarkCaseName $row
        $caseKey = Get-BenchmarkCaseKey $methodName $preserveOrderValue $jobName

        $meanUs = Convert-ToMicroseconds $row.Mean
        $allocatedKb = Convert-ToKilobytes $row.Allocated

        $result[$caseKey] = [pscustomobject]@{
            CaseKey = $caseKey
            CaseName = $caseName
            MethodName = $methodName
            PreserveOrderText = $preserveOrderValue
            PreserveOrder = Parse-PreserveOrderValue $preserveOrderValue
            Job = $jobName
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

function Find-EndToEndRow($data, [bool]$preserveOrder, [string]$jobName)
{
    foreach ($key in $data.Keys)
    {
        $row = $data[$key]
        if ($row.MethodName -ne 'EndToEnd_Extract_And_Serialize')
        {
            continue
        }

        if ($row.PreserveOrder -ne $preserveOrder)
        {
            continue
        }

        if ($row.Job -ne $jobName)
        {
            continue
        }

        return $row
    }

    return $null
}

function Get-CaseRows($data, [bool]$preserveOrder)
{
    $rows = @()

    foreach ($key in $data.Keys)
    {
        $row = $data[$key]
        if ($row.MethodName -ne 'EndToEnd_Extract_And_Serialize')
        {
            continue
        }

        if ($row.PreserveOrder -ne $preserveOrder)
        {
            continue
        }

        $rows += $row
    }

    return $rows
}

function Get-CaseRowByJob($rows, [string]$jobName)
{
    foreach ($row in $rows)
    {
        if ($row.Job -eq $jobName)
        {
            return $row
        }
    }

    return $null
}

function Format-ResultPair($row)
{
    if ($null -eq $row)
    {
        return 'n/a'
    }

    return "Mean: $($row.MeanFormatted); Allocated: $($row.AllocatedFormatted)"
}

function Format-DeltaPair($currentRow, $previousRow)
{
    if ($null -eq $currentRow -or $null -eq $previousRow)
    {
        return 'Mean: n/a; Allocated: n/a'
    }

    $meanDelta = Format-Delta $currentRow.MeanUs $previousRow.MeanUs
    $allocatedDelta = Format-Delta $currentRow.AllocatedKb $previousRow.AllocatedKb
    return "Mean: $meanDelta; Allocated: $allocatedDelta"
}

if (-not (Test-Path -Path $IfcFilePath))
{
    throw "IFC file not found: $IfcFilePath"
}

$resolvedIfcPath = (Resolve-Path $IfcFilePath).Path
$env:IFC_BENCHMARK_FILE = $resolvedIfcPath
$artifactDir = Join-Path (Get-Location) "BenchmarkDotNet.Artifacts/results"

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

if (Test-Path $previousCsv)
{
    $comparisonSourcePath = $previousCsv
    $comparisonSourceLabel = "previous local run"
}
else
{
    $gitPrevious = git show HEAD~1:benchmarks/results/latest/IfcFilePipelineBenchmark-report.csv 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace(($gitPrevious -join "")))
    {
        $gitPrevious | Set-Content -Path $previousFromGitPath -Encoding UTF8
        $comparisonSourcePath = $previousFromGitPath
        $comparisonSourceLabel = "previous commit (HEAD~1)"
    }
}

$comparisonFileLatest = Join-Path $latestDir "IfcFilePipelineBenchmark-comparison.md"
$comparisonFileStamped = Join-Path $resultsDir "$stamp-IfcFilePipelineBenchmark-comparison.md"
$gcComparisonFileLatest = Join-Path $latestDir "IfcFilePipelineBenchmark-gc-comparison.md"
$gcComparisonFileStamped = Join-Path $resultsDir "$stamp-IfcFilePipelineBenchmark-gc-comparison.md"

$currentData = Parse-BenchmarkCsv $latestCsv
$previousData = $null

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

    foreach ($caseKey in ($currentData.Keys | Sort-Object))
    {
        $currentRow = $currentData[$caseKey]
        $previousRow = $previousData[$caseKey]

        if ($null -eq $previousRow)
        {
            $lines.Add("| $($currentRow.CaseName) | n/a | $($currentRow.MeanFormatted) | n/a | n/a | $($currentRow.AllocatedFormatted) | n/a |")
            continue
        }

        $deltaMean = Format-Delta $currentRow.MeanUs $previousRow.MeanUs
        $deltaAllocated = Format-Delta $currentRow.AllocatedKb $previousRow.AllocatedKb
        $lines.Add("| $($currentRow.CaseName) | $($previousRow.MeanFormatted) | $($currentRow.MeanFormatted) | $deltaMean | $($previousRow.AllocatedFormatted) | $($currentRow.AllocatedFormatted) | $deltaAllocated |")
    }
}
else
{
    $lines.Add("Comparison source is not available (no previous commit report and no previous local snapshot).")
    $lines.Add("")
    $lines.Add("| Method | Current Mean | Current Allocated |")
    $lines.Add("|---|---:|---:|")

    foreach ($caseKey in ($currentData.Keys | Sort-Object))
    {
        $row = $currentData[$caseKey]
        $lines.Add("| $($row.CaseName) | $($row.MeanFormatted) | $($row.AllocatedFormatted) |")
    }
}

$summaryJobName = 'WS_Concurrent'
$currentOrdered = Find-EndToEndRow $currentData $true $summaryJobName
$currentNoOrder = Find-EndToEndRow $currentData $false $summaryJobName
$previousOrdered = if ($null -ne $previousData) { Find-EndToEndRow $previousData $true $summaryJobName } else { $null }
$previousNoOrder = if ($null -ne $previousData) { Find-EndToEndRow $previousData $false $summaryJobName } else { $null }

$lines.Add("")
$lines.Add("## Final report order/no-order summary")
$lines.Add("")
$lines.Add("Summary job: $summaryJobName")
$lines.Add("")
$lines.Add("| Metric | Value |")
$lines.Add("|---|---|")
$lines.Add("| Current ordered result | $(Format-ResultPair $currentOrdered) |")
$lines.Add("| Current no-order result | $(Format-ResultPair $currentNoOrder) |")
$lines.Add("| Current no-order vs ordered difference | $(Format-DeltaPair $currentNoOrder $currentOrdered) |")
$lines.Add("| Previous ordered result | $(Format-ResultPair $previousOrdered) |")
$lines.Add("| Previous no-order result | $(Format-ResultPair $previousNoOrder) |")
$lines.Add("| Previous no-order vs ordered difference | $(Format-DeltaPair $previousNoOrder $previousOrdered) |")
$lines.Add("| Ordered current vs previous difference | $(Format-DeltaPair $currentOrdered $previousOrdered) |")
$lines.Add("| No-order current vs previous difference | $(Format-DeltaPair $currentNoOrder $previousNoOrder) |")

$lines | Set-Content -Path $comparisonFileLatest -Encoding UTF8
$lines | Set-Content -Path $comparisonFileStamped -Encoding UTF8

$gcModes = @('WS_Concurrent', 'WS_NonConcurrent', 'Server_Concurrent')
$gcLines = New-Object System.Collections.Generic.List[string]
$gcLines.Add("# GC mode comparison report")
$gcLines.Add("")
$gcLines.Add("Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$gcLines.Add("IFC file: $resolvedIfcPath")
$gcLines.Add("")

foreach ($preserveOrder in @($false, $true))
{
    $sectionRows = Get-CaseRows $currentData $preserveOrder
    $baselineRow = Get-CaseRowByJob $sectionRows 'WS_Concurrent'

    $gcLines.Add("## PreserveOrder=$preserveOrder")
    $gcLines.Add("")
    $gcLines.Add("| GC Mode | Mean | Allocated | Delta Mean vs WS_Concurrent | Delta Allocated vs WS_Concurrent |")
    $gcLines.Add("|---|---:|---:|---:|---:|")

    foreach ($gcMode in $gcModes)
    {
        $row = Get-CaseRowByJob $sectionRows $gcMode
        if ($null -eq $row)
        {
            $gcLines.Add("| $gcMode | n/a | n/a | n/a | n/a |")
            continue
        }

        $deltaMean = if ($null -eq $baselineRow) { 'n/a' } else { Format-Delta $row.MeanUs $baselineRow.MeanUs }
        $deltaAllocated = if ($null -eq $baselineRow) { 'n/a' } else { Format-Delta $row.AllocatedKb $baselineRow.AllocatedKb }
        $gcLines.Add("| $gcMode | $($row.MeanFormatted) | $($row.AllocatedFormatted) | $deltaMean | $deltaAllocated |")
    }

    $bestMean = $sectionRows | Sort-Object -Property MeanUs | Select-Object -First 1
    $bestAllocated = $sectionRows | Sort-Object -Property AllocatedKb | Select-Object -First 1

    $gcLines.Add("")
    $gcLines.Add("Best Mean: $(if ($null -eq $bestMean) { 'n/a' } else { "$($bestMean.Job) ($($bestMean.MeanFormatted))" })")
    $gcLines.Add("Best Allocated: $(if ($null -eq $bestAllocated) { 'n/a' } else { "$($bestAllocated.Job) ($($bestAllocated.AllocatedFormatted))" })")
    $gcLines.Add("")
}

$gcLines | Set-Content -Path $gcComparisonFileLatest -Encoding UTF8
$gcLines | Set-Content -Path $gcComparisonFileStamped -Encoding UTF8

if (Test-Path $previousFromGitPath)
{
    Remove-Item $previousFromGitPath -Force
}

Write-Host "Saved benchmark snapshots to: $resultsDir"
Write-Host "Saved latest comparison report: $comparisonFileLatest"
Write-Host "Saved stamped comparison report: $comparisonFileStamped"
Write-Host "Saved latest GC comparison report: $gcComparisonFileLatest"
Write-Host "Saved stamped GC comparison report: $gcComparisonFileStamped"

if (Test-Path "BenchmarkDotNet.Artifacts")
{
    Remove-Item -Recurse -Force "BenchmarkDotNet.Artifacts"
}
