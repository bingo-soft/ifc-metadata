param(
    [string]$IfcDirectory = "ifc",
    [string]$Configuration = "Release",
    [string]$TestProject = "tests/ifc-metadata.Tests.csproj",
    [string]$OutputReportPath = "benchmarks/results/latest/fast-step-parity-report.md",
    [string]$Filter = "FullyQualifiedName~FastStepParityTests.Xbim_And_FastStep_Engines_ProduceEquivalentOutput_WhenIfcFileIsConfigured",
    [int]$MaxFiles = 0
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $IfcDirectory)) {
    throw "IFC directory not found: $IfcDirectory"
}

$ifcFiles = Get-ChildItem -Path $IfcDirectory -Filter *.ifc | Sort-Object Name
if ($ifcFiles.Count -eq 0) {
    throw "No .ifc files found in: $IfcDirectory"
}

if ($MaxFiles -gt 0) {
    $ifcFiles = $ifcFiles | Select-Object -First $MaxFiles
}

Write-Host "Building test project..."
dotnet test $TestProject -c $Configuration --filter "FullyQualifiedName~IfcExportEngineParserTests" | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Initial test build failed."
}

$results = New-Object System.Collections.Generic.List[object]

foreach ($file in $ifcFiles) {
    Write-Host "=== $($file.FullName) ==="

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $env:IFC_BENCHMARK_FILE = $file.FullName

    dotnet test $TestProject -c $Configuration --no-build --no-restore --filter $Filter
    $exitCode = $LASTEXITCODE

    $sw.Stop()

    $results.Add([PSCustomObject]@{
        File    = $file.Name
        Status  = if ($exitCode -eq 0) { "PASS" } else { "FAIL" }
        Seconds = [math]::Round($sw.Elapsed.TotalSeconds, 2)
    })
}

$env:IFC_BENCHMARK_FILE = $null

$reportDirectory = Split-Path -Path $OutputReportPath -Parent
if (-not [string]::IsNullOrWhiteSpace($reportDirectory) -and -not (Test-Path $reportDirectory)) {
    New-Item -Path $reportDirectory -ItemType Directory -Force | Out-Null
}

$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$passed = ($results | Where-Object Status -eq "PASS").Count
$failed = ($results | Where-Object Status -eq "FAIL").Count

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# Fast-step parity report")
$lines.Add("")
$lines.Add("- Generated: $timestamp")
$lines.Add("- IFC directory: $IfcDirectory")
$lines.Add("- Test filter: $Filter")
$lines.Add("- Max files: $MaxFiles")
$lines.Add("- Passed: $passed")
$lines.Add("- Failed: $failed")
$lines.Add("")
$lines.Add("| File | Status | Duration (s) |")
$lines.Add("|---|---:|---:|")

foreach ($row in $results) {
    $lines.Add("| $($row.File) | $($row.Status) | $($row.Seconds) |")
}

$lines.Add("")

if ($failed -gt 0) {
    $lines.Add("## Failed files")
    foreach ($row in $results | Where-Object Status -eq "FAIL") {
        $lines.Add("- $($row.File)")
    }
}

Set-Content -Path $OutputReportPath -Value $lines -Encoding UTF8
Write-Host "Report saved to: $OutputReportPath"

if ($failed -gt 0) {
    exit 1
}
