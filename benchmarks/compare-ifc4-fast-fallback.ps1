$ErrorActionPreference = 'Stop'

$exe = "./src/bin/Release/net10.0/ifc-metadata-v2.exe"
$outDir = "benchmarks/results/stage0/ifc4-parity"

New-Item -Path $outDir -ItemType Directory -Force | Out-Null

$files = Get-ChildItem -Path "ifc" -Filter "*.ifc" -File
$pattern = 'FILE_SCHEMA\s*\(\s*\(\s*''([^'']+)'''
$ifc4Files = @()

foreach ($f in $files)
{
    $match = Select-String -Path $f.FullName -Pattern $pattern -List
    if (-not $match)
    {
        continue
    }

    $schema = $match.Matches[0].Groups[1].Value
    if ($schema -eq 'IFC4')
    {
        $ifc4Files += $f
    }
}

if ($ifc4Files.Count -eq 0)
{
    throw 'No IFC4 files found in ifc folder.'
}

$resultRows = New-Object System.Collections.Generic.List[object]

foreach ($file in $ifc4Files)
{
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
    $safeName = ($baseName -replace '[^A-Za-z0-9_-]', '_')

    $fastJson = Join-Path $outDir ("$safeName.fast.json")
    $fallbackJson = Join-Path $outDir ("$safeName.fallback.json")

    & $exe $file.FullName $fastJson --verbosity none --preserve-order true
    if ($LASTEXITCODE -ne 0)
    {
        throw "Fast-path export failed for: $($file.FullName)"
    }

    $env:IFC_FORCE_FALLBACK = '1'
    & $exe $file.FullName $fallbackJson --verbosity none --preserve-order true
    $fallbackExitCode = $LASTEXITCODE
    Remove-Item Env:IFC_FORCE_FALLBACK -ErrorAction SilentlyContinue

    if ($fallbackExitCode -ne 0)
    {
        throw "Fallback export failed for: $($file.FullName)"
    }

    $fastHash = (Get-FileHash -Path $fastJson -Algorithm SHA256).Hash
    $fallbackHash = (Get-FileHash -Path $fallbackJson -Algorithm SHA256).Hash

    $resultRows.Add([pscustomobject]@{
        File = $file.Name
        FastHash = $fastHash
        FallbackHash = $fallbackHash
        Equal = ($fastHash -eq $fallbackHash)
    })
}

$summaryPath = Join-Path $outDir 'summary.md'
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('# IFC4 fast-path parity report')
$lines.Add('')
$lines.Add("Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$lines.Add('')
$lines.Add('| File | Fast SHA256 | Fallback SHA256 | Equal |')
$lines.Add('|---|---|---|---|')

foreach ($row in $resultRows)
{
    $lines.Add("| $($row.File) | $($row.FastHash) | $($row.FallbackHash) | $($row.Equal) |")
}

$lines | Set-Content -Path $summaryPath -Encoding UTF8

$resultRows | Format-Table -AutoSize
Write-Host "Summary: $summaryPath"
