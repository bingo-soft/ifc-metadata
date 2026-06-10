$ErrorActionPreference = 'Stop'

$files = Get-ChildItem -Path "ifc" -Filter "*.ifc" -File
$pattern = 'FILE_SCHEMA\s*\(\s*\(\s*''([^'']+)'''

foreach ($f in $files)
{
    $schema = 'UNKNOWN'
    $match = Select-String -Path $f.FullName -Pattern $pattern -List
    if ($match)
    {
        $schema = $match.Matches[0].Groups[1].Value
    }

    Write-Output ($f.Name + ';' + $schema)
}
