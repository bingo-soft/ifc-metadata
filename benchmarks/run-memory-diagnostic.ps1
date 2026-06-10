param(
    [string]$IfcFilePath = "D:\data\ifc\debug\1-2025-68_П_FM (2).ifc",
    [ValidateSet("xbim", "fast-step")]
    [string]$Engine = "fast-step",
    [string]$OutputRoot = "D:\data\ifc\debug\memory-runs",
    [int]$SampleIntervalMilliseconds = 500,
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$ProjectPath = "src\ifc-metadata.csproj",
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

function Format-ByteSize([long]$bytes)
{
    $units = @("B", "KB", "MB", "GB", "TB")
    $value = [double]$bytes
    $unitIndex = 0

    while ($value -ge 1024 -and $unitIndex -lt ($units.Length - 1))
    {
        $value = $value / 1024
        $unitIndex++
    }

    return [string]::Format([System.Globalization.CultureInfo]::InvariantCulture, "{0:0.00} {1}", $value, $units[$unitIndex])
}

function Quote-ProcessArgument([string]$value)
{
    if ($null -eq $value)
    {
        return '""'
    }

    return '"' + $value.Replace('"', '\"') + '"'
}

function Get-OperatingSystemSnapshot()
{
    $os = Get-CimInstance Win32_OperatingSystem
    return [pscustomobject]@{
        TotalVisibleMemoryBytes = [int64]$os.TotalVisibleMemorySize * 1024
        FreePhysicalMemoryBytes = [int64]$os.FreePhysicalMemory * 1024
    }
}

if ($SampleIntervalMilliseconds -le 0)
{
    throw "SampleIntervalMilliseconds must be greater than zero."
}

if (-not (Test-Path -LiteralPath $IfcFilePath))
{
    throw "IFC file not found: $IfcFilePath"
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path -LiteralPath (Join-Path $scriptRoot "..")
$resolvedIfcPath = (Resolve-Path -LiteralPath $IfcFilePath).Path

Push-Location $repoRoot
try
{
    if (-not $SkipBuild)
    {
        Write-Host "Building $ProjectPath ($Configuration/$Platform)..."
        dotnet build $ProjectPath -c $Configuration "-p:Platform=$Platform" --nologo
        if ($LASTEXITCODE -ne 0)
        {
            throw "Build failed with exit code $LASTEXITCODE."
        }
    }

    $exeCandidates = @(
        (Join-Path $repoRoot "src\bin\$Platform\$Configuration\net10.0\win-x64\ifc-metadata-v2.exe"),
        (Join-Path $repoRoot "src\bin\$Platform\$Configuration\net10.0\ifc-metadata-v2.exe"),
        (Join-Path $repoRoot "src\bin\$Configuration\net10.0\win-x64\ifc-metadata-v2.exe"),
        (Join-Path $repoRoot "src\bin\$Configuration\net10.0\ifc-metadata-v2.exe")
    )
    $exePath = $exeCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not (Test-Path -LiteralPath $exePath))
    {
        throw "Executable not found. Checked: $($exeCandidates -join '; ')"
    }

    $stamp = Get-Date -Format "yyyy-MM-dd-HHmmss"
    $outDir = Join-Path $OutputRoot $stamp
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null

    $jsonPath = Join-Path $outDir "memrun-$Engine.json"
    $stdoutPath = Join-Path $outDir "stdout.log"
    $stderrPath = Join-Path $outDir "stderr.log"
    $csvPath = Join-Path $outDir "memory.csv"
    $summaryPath = Join-Path $outDir "summary.txt"

    $csvWriter = [System.IO.StreamWriter]::new($csvPath, $false, [System.Text.UTF8Encoding]::new($false))
    $csvWriter.WriteLine("Timestamp,ElapsedMs,WorkingSetBytes,PeakWorkingSetBytes,PrivateBytes,PagedBytes,VirtualBytes,SystemFreeMemoryBytes")

    $maxWorkingSetBytes = 0L
    $maxPeakWorkingSetBytes = 0L
    $maxPrivateBytes = 0L
    $maxPagedBytes = 0L
    $maxVirtualBytes = 0L
    $minSystemFreeMemoryBytes = [long]::MaxValue
    $sampleCount = 0

    $arguments = @(
        (Quote-ProcessArgument $resolvedIfcPath),
        (Quote-ProcessArgument $jsonPath),
        "--engine",
        $Engine,
        "--verbosity",
        "detailed",
        "--progress",
        "none",
        "--no-write-through"
    )

    $startInfo = @{
        FilePath = $exePath
        ArgumentList = $arguments
        PassThru = $true
        NoNewWindow = $true
        RedirectStandardOutput = $stdoutPath
        RedirectStandardError = $stderrPath
    }

    $initialOs = Get-OperatingSystemSnapshot
    $started = Get-Date
    Write-Host "Running memory diagnostic..."
    Write-Host "IFC: $resolvedIfcPath"
    Write-Host "Engine: $Engine"
    Write-Host "Artifacts: $outDir"

    $process = Start-Process @startInfo

    try
    {
        while (-not $process.HasExited)
        {
            $process.Refresh()
            $os = Get-OperatingSystemSnapshot
            $elapsed = [int64]((Get-Date) - $started).TotalMilliseconds

            $workingSetBytes = [int64]$process.WorkingSet64
            $peakWorkingSetBytes = [int64]$process.PeakWorkingSet64
            $privateBytes = [int64]$process.PrivateMemorySize64
            $pagedBytes = [int64]$process.PagedMemorySize64
            $virtualBytes = [int64]$process.VirtualMemorySize64
            $freeBytes = [int64]$os.FreePhysicalMemoryBytes

            $maxWorkingSetBytes = [Math]::Max($maxWorkingSetBytes, $workingSetBytes)
            $maxPeakWorkingSetBytes = [Math]::Max($maxPeakWorkingSetBytes, $peakWorkingSetBytes)
            $maxPrivateBytes = [Math]::Max($maxPrivateBytes, $privateBytes)
            $maxPagedBytes = [Math]::Max($maxPagedBytes, $pagedBytes)
            $maxVirtualBytes = [Math]::Max($maxVirtualBytes, $virtualBytes)
            $minSystemFreeMemoryBytes = [Math]::Min($minSystemFreeMemoryBytes, $freeBytes)
            $sampleCount++

            $csvWriter.WriteLine(("{0},{1},{2},{3},{4},{5},{6},{7}" -f `
                (Get-Date).ToString("o"),
                $elapsed,
                $workingSetBytes,
                $peakWorkingSetBytes,
                $privateBytes,
                $pagedBytes,
                $virtualBytes,
                $freeBytes))
            $csvWriter.Flush()

            Start-Sleep -Milliseconds $SampleIntervalMilliseconds
        }
    }
    finally
    {
        $csvWriter.Dispose()
    }

    $process.Refresh()
    $ended = Get-Date
    $duration = $ended - $started
    $lastSamples = if (Test-Path -LiteralPath $csvPath) { Get-Content -LiteralPath $csvPath -Tail 20 } else { @() }
    $outputSizeBytes = if (Test-Path -LiteralPath $jsonPath) { (Get-Item -LiteralPath $jsonPath).Length } else { 0L }

    if ($minSystemFreeMemoryBytes -eq [long]::MaxValue)
    {
        $minSystemFreeMemoryBytes = 0L
    }

    @(
        "ExitCode: $($process.ExitCode)"
        "Started: $($started.ToString('o'))"
        "Ended: $($ended.ToString('o'))"
        "Duration: $duration"
        "Engine: $Engine"
        "IFC: $resolvedIfcPath"
        "IFCSizeBytes: $((Get-Item -LiteralPath $resolvedIfcPath).Length)"
        "IFCSize: $(Format-ByteSize ((Get-Item -LiteralPath $resolvedIfcPath).Length))"
        "TotalVisibleMemoryBytes: $($initialOs.TotalVisibleMemoryBytes)"
        "TotalVisibleMemory: $(Format-ByteSize $initialOs.TotalVisibleMemoryBytes)"
        "InitialFreePhysicalMemoryBytes: $($initialOs.FreePhysicalMemoryBytes)"
        "InitialFreePhysicalMemory: $(Format-ByteSize $initialOs.FreePhysicalMemoryBytes)"
        "SampleIntervalMilliseconds: $SampleIntervalMilliseconds"
        "SampleCount: $sampleCount"
        "PeakWorkingSetBytes: $maxWorkingSetBytes"
        "PeakWorkingSet: $(Format-ByteSize $maxWorkingSetBytes)"
        "ProcessPeakWorkingSetBytes: $maxPeakWorkingSetBytes"
        "ProcessPeakWorkingSet: $(Format-ByteSize $maxPeakWorkingSetBytes)"
        "PeakPrivateBytes: $maxPrivateBytes"
        "PeakPrivate: $(Format-ByteSize $maxPrivateBytes)"
        "PeakPagedBytes: $maxPagedBytes"
        "PeakPaged: $(Format-ByteSize $maxPagedBytes)"
        "PeakVirtualBytes: $maxVirtualBytes"
        "PeakVirtual: $(Format-ByteSize $maxVirtualBytes)"
        "MinimumSystemFreeMemoryBytes: $minSystemFreeMemoryBytes"
        "MinimumSystemFreeMemory: $(Format-ByteSize $minSystemFreeMemoryBytes)"
        "OutputJson: $jsonPath"
        "OutputJsonSizeBytes: $outputSizeBytes"
        "OutputJsonSize: $(Format-ByteSize $outputSizeBytes)"
        "Stdout: $stdoutPath"
        "Stderr: $stderrPath"
        "MemoryCsv: $csvPath"
        ""
        "Last 20 memory samples:"
        $lastSamples
    ) | Set-Content -LiteralPath $summaryPath -Encoding UTF8

    Write-Host "Memory diagnostic finished with exit code $($process.ExitCode)."
    Write-Host "Summary: $summaryPath"
    Write-Host "Peak working set: $(Format-ByteSize $maxWorkingSetBytes)"
    Write-Host "Peak private bytes: $(Format-ByteSize $maxPrivateBytes)"

    if ($process.ExitCode -ne 0)
    {
        exit $process.ExitCode
    }
}
finally
{
    Pop-Location
}
