param(
    [Parameter(Mandatory = $true)]
    [string[]]$Dataset,

    [int]$MeasuredRuns = 5,
    [int]$WarmupRuns = 1,
    [string]$Configuration = "Release",
    [string]$ProjectPath = "src/ifc-metadata.csproj",
    [string]$ResultsRoot = "benchmarks/results/stage0",
    [bool]$PreserveOrder = $true,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'

function Parse-DatasetSpec([string]$spec)
{
    $parts = $spec.Split('|')
    if ($parts.Length -ne 2)
    {
        throw "Dataset format must be: <label>|<ifcPath>. Got: $spec"
    }

    $label = $parts[0].Trim()
    $ifcPath = $parts[1].Trim()

    if ([string]::IsNullOrWhiteSpace($label) -or [string]::IsNullOrWhiteSpace($ifcPath))
    {
        throw "Dataset format must be: <label>|<ifcPath>. Got: $spec"
    }

    if (-not (Test-Path -Path $ifcPath))
    {
        throw "IFC file not found for dataset '$label': $ifcPath"
    }

    return [pscustomobject]@{
        Label = $label
        IfcPath = (Resolve-Path $ifcPath).Path
    }
}

function Convert-ToDoubleInvariant([string]$value)
{
    $normalized = $value.Trim().Replace([char]0xA0, ' ').Replace([char]0x202F, ' ')

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

function Convert-SizeTextToBytes([string]$text)
{
    if ([string]::IsNullOrWhiteSpace($text))
    {
        return $null
    }

    $trimmed = $text.Trim()
    $match = [regex]::Match($trimmed, '^([+-]?[0-9][0-9\.,]*)\s*(B|KB|MB|GB|TB)$', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $match.Success)
    {
        throw "Cannot parse size value: '$text'"
    }

    $number = Convert-ToDoubleInvariant $match.Groups[1].Value
    $unit = $match.Groups[2].Value.ToUpperInvariant()

    $multiplier = switch ($unit)
    {
        'B'  { 1 }
        'KB' { 1024 }
        'MB' { 1024 * 1024 }
        'GB' { 1024 * 1024 * 1024 }
        'TB' { 1024 * 1024 * 1024 * 1024 }
        default { throw "Unsupported unit: $unit" }
    }

    return [long][Math]::Round($number * $multiplier)
}

function Get-LineValue([string[]]$lines, [string]$prefix)
{
    $line = $lines | Where-Object { $_ -like "$prefix*" } | Select-Object -Last 1
    if ($null -eq $line)
    {
        return $null
    }

    return $line.Substring($prefix.Length).Trim()
}

function Get-Median([double[]]$values)
{
    if ($null -eq $values -or $values.Count -eq 0)
    {
        return $null
    }

    $sorted = $values | Sort-Object
    $count = $sorted.Count

    if ($count % 2 -eq 1)
    {
        return [double]$sorted[[int]($count / 2)]
    }

    $left = [double]$sorted[($count / 2) - 1]
    $right = [double]$sorted[$count / 2]
    return ($left + $right) / 2.0
}

function Get-P95([double[]]$values)
{
    if ($null -eq $values -or $values.Count -eq 0)
    {
        return $null
    }

    $sorted = $values | Sort-Object
    $count = $sorted.Count
    $index = [int][Math]::Ceiling(0.95 * $count) - 1
    if ($index -lt 0) { $index = 0 }
    if ($index -ge $count) { $index = $count - 1 }

    return [double]$sorted[$index]
}

function Invoke-ExportRun(
    [string]$projectPath,
    [string]$configuration,
    [string]$ifcPath,
    [string]$jsonPath,
    [string]$logPath,
    [bool]$preserveOrder)
{
    $output = & dotnet run --project $projectPath -c $configuration -- $ifcPath $jsonPath --verbosity detailed --preserve-order $preserveOrder 2>&1
    $exitCode = $LASTEXITCODE

    $output | Set-Content -Path $logPath -Encoding UTF8

    if ($exitCode -ne 0)
    {
        throw "Export run failed with exit code $exitCode. See log: $logPath"
    }

    $lines = @($output | ForEach-Object { [string]$_ })

    $schema = Get-LineValue $lines 'Schema:'
    $metaObjectsText = Get-LineValue $lines 'MetaObjects:'
    $elapsedText = Get-LineValue $lines 'Elapsed:'
    $managedDeltaText = Get-LineValue $lines 'Managed memory delta:'
    $workingSetText = Get-LineValue $lines 'Working set:'
    $peakWorkingSetText = Get-LineValue $lines 'Peak working set:'

    if ([string]::IsNullOrWhiteSpace($elapsedText))
    {
        throw "Cannot parse 'Elapsed' from log: $logPath"
    }

    $elapsedMatch = [regex]::Match($elapsedText, '^([0-9][0-9\.,]*)\s*ms$')
    if (-not $elapsedMatch.Success)
    {
        throw "Cannot parse elapsed milliseconds from '$elapsedText' in log: $logPath"
    }

    $elapsedMs = Convert-ToDoubleInvariant $elapsedMatch.Groups[1].Value
    $metaObjects = if ([string]::IsNullOrWhiteSpace($metaObjectsText)) { 0 } else { [int64]$metaObjectsText }

    if (-not (Test-Path -Path $jsonPath))
    {
        throw "JSON output not found: $jsonPath"
    }

    $jsonSizeBytes = (Get-Item $jsonPath).Length
    $managedDeltaBytes = Convert-SizeTextToBytes $managedDeltaText
    $workingSetBytes = Convert-SizeTextToBytes $workingSetText
    $peakWorkingSetBytes = Convert-SizeTextToBytes $peakWorkingSetText

    return [pscustomobject]@{
        Schema = $schema
        MetaObjects = $metaObjects
        ElapsedMs = $elapsedMs
        JsonSizeBytes = $jsonSizeBytes
        ManagedDeltaBytes = $managedDeltaBytes
        WorkingSetBytes = $workingSetBytes
        PeakWorkingSetBytes = $peakWorkingSetBytes
    }
}

if ($MeasuredRuns -le 0)
{
    throw "MeasuredRuns must be > 0"
}

if ($WarmupRuns -lt 0)
{
    throw "WarmupRuns must be >= 0"
}

$datasets = @($Dataset | ForEach-Object { Parse-DatasetSpec $_ })

if (-not $NoBuild)
{
    dotnet build ifc-metadata.slnx -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0)
    {
        throw "Build failed with exit code $LASTEXITCODE"
    }
}

$stamp = Get-Date -Format 'yyyy-MM-dd-HHmmss'
$root = Join-Path (Get-Location) $ResultsRoot
$runDir = Join-Path $root $stamp
$logsDir = Join-Path $runDir 'logs'
$rawDir = Join-Path $runDir 'raw'
$goldenDir = Join-Path $runDir 'golden'

New-Item -Path $runDir -ItemType Directory -Force | Out-Null
New-Item -Path $logsDir -ItemType Directory -Force | Out-Null
New-Item -Path $rawDir -ItemType Directory -Force | Out-Null
New-Item -Path $goldenDir -ItemType Directory -Force | Out-Null

$allRows = New-Object System.Collections.Generic.List[object]
$summaryRows = New-Object System.Collections.Generic.List[object]

foreach ($ds in $datasets)
{
    Write-Host "Running dataset: $($ds.Label) -> $($ds.IfcPath)"

    for ($w = 1; $w -le $WarmupRuns; $w++)
    {
        $warmupJson = Join-Path $runDir "$($ds.Label)-warmup-$w.json"
        $warmupLog = Join-Path $logsDir "$($ds.Label)-warmup-$w.log"
        [void](Invoke-ExportRun -projectPath $ProjectPath -configuration $Configuration -ifcPath $ds.IfcPath -jsonPath $warmupJson -logPath $warmupLog -preserveOrder $PreserveOrder)
        Remove-Item -Path $warmupJson -Force
    }

    $rows = New-Object System.Collections.Generic.List[object]
    $schemaValue = ''
    $metaObjectsValue = 0
    $goldenPath = $null

    for ($i = 1; $i -le $MeasuredRuns; $i++)
    {
        $jsonPath = Join-Path $runDir "$($ds.Label)-run-$i.json"
        $logPath = Join-Path $logsDir "$($ds.Label)-run-$i.log"

        $result = Invoke-ExportRun -projectPath $ProjectPath -configuration $Configuration -ifcPath $ds.IfcPath -jsonPath $jsonPath -logPath $logPath -preserveOrder $PreserveOrder

        if ([string]::IsNullOrWhiteSpace($schemaValue))
        {
            $schemaValue = $result.Schema
            $metaObjectsValue = $result.MetaObjects
        }

        $row = [pscustomobject]@{
            Label = $ds.Label
            Run = $i
            Schema = $result.Schema
            MetaObjects = $result.MetaObjects
            ElapsedMs = $result.ElapsedMs
            JsonSizeBytes = $result.JsonSizeBytes
            ManagedDeltaBytes = $result.ManagedDeltaBytes
            WorkingSetBytes = $result.WorkingSetBytes
            PeakWorkingSetBytes = $result.PeakWorkingSetBytes
            LogPath = $logPath
        }

        $rows.Add($row)
        $allRows.Add($row)

        if ($i -eq 1)
        {
            $goldenPath = Join-Path $goldenDir "$($ds.Label).json"
            Copy-Item -Path $jsonPath -Destination $goldenPath -Force
        }

        Remove-Item -Path $jsonPath -Force
    }

    $elapsedValues = @($rows | ForEach-Object { [double]$_.ElapsedMs })
    $managedValues = @($rows | ForEach-Object { [double]$_.ManagedDeltaBytes })
    $workingSetValues = @($rows | ForEach-Object { [double]$_.WorkingSetBytes })
    $peakWorkingSetValues = @($rows | ForEach-Object { [double]$_.PeakWorkingSetBytes })
    $jsonSizeValues = @($rows | ForEach-Object { [double]$_.JsonSizeBytes })

    $goldenHash = (Get-FileHash -Path $goldenPath -Algorithm SHA256).Hash

    $summaryRows.Add([pscustomobject]@{
        Label = $ds.Label
        Schema = $schemaValue
        MetaObjects = $metaObjectsValue
        ElapsedMedianMs = [Math]::Round((Get-Median $elapsedValues), 3)
        ElapsedP95Ms = [Math]::Round((Get-P95 $elapsedValues), 3)
        JsonSizeBytes = [long](Get-Median $jsonSizeValues)
        ManagedDeltaMedianBytes = [long](Get-Median $managedValues)
        WorkingSetMedianBytes = [long](Get-Median $workingSetValues)
        PeakWorkingSetMedianBytes = [long](Get-Median $peakWorkingSetValues)
        GoldenJsonPath = $goldenPath
        GoldenJsonSha256 = $goldenHash
    })
}

$rawCsvPath = Join-Path $rawDir 'raw-metrics.csv'
$allRows | Export-Csv -Path $rawCsvPath -Delimiter ';' -NoTypeInformation -Encoding UTF8

$summaryCsvPath = Join-Path $runDir 'summary.csv'
$summaryRows | Export-Csv -Path $summaryCsvPath -Delimiter ';' -NoTypeInformation -Encoding UTF8

$summaryMdPath = Join-Path $runDir 'summary.md'
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('# Stage 0 baseline summary')
$lines.Add('')
$lines.Add("Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$lines.Add("Configuration: $Configuration")
$lines.Add("Project: $ProjectPath")
$lines.Add("WarmupRuns: $WarmupRuns")
$lines.Add("MeasuredRuns: $MeasuredRuns")
$lines.Add("PreserveOrder: $PreserveOrder")
$lines.Add('')
$lines.Add('| Label | Schema | MetaObjects | Elapsed median (ms) | Elapsed p95 (ms) | Output size (bytes) | Managed delta median (bytes) | Working set median (bytes) | Peak working set median (bytes) | Golden JSON SHA256 |')
$lines.Add('|---|---|---:|---:|---:|---:|---:|---:|---:|---|')

foreach ($row in $summaryRows)
{
    $lines.Add("| $($row.Label) | $($row.Schema) | $($row.MetaObjects) | $($row.ElapsedMedianMs) | $($row.ElapsedP95Ms) | $($row.JsonSizeBytes) | $($row.ManagedDeltaMedianBytes) | $($row.WorkingSetMedianBytes) | $($row.PeakWorkingSetMedianBytes) | $($row.GoldenJsonSha256) |")
}

$lines.Add('')
$lines.Add("Raw CSV: $rawCsvPath")
$lines.Add("Summary CSV: $summaryCsvPath")
$lines.Add("Golden dir: $goldenDir")

$lines | Set-Content -Path $summaryMdPath -Encoding UTF8

Write-Host "Stage 0 baseline completed."
Write-Host "Summary: $summaryMdPath"
Write-Host "Raw data: $rawCsvPath"
