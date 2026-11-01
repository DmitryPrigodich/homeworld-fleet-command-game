param(
    [string]$SourcePdf = 'G:\Games\HW_Games\HWFC\HOMEWORLD-mini euro portrait.pdf',
    [string]$Poppler = 'pdftoppm',
    [string]$DataDirectory = ''
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $DataDirectory) { $DataDirectory = Join-Path $projectRoot '.local' }
$destination = Join-Path $DataDirectory 'card-styles/hwfc-frames'
if (-not (Test-Path -LiteralPath $SourcePdf -PathType Leaf)) { throw 'Source PDF was not found.' }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach ($frame in @(@(2,'order'), @(3,'interrupt'))) {
    & $Poppler -f $frame[0] -l $frame[0] -scale-to 1000 -singlefile -png $SourcePdf (Join-Path $destination $frame[1])
    if ($LASTEXITCODE -ne 0) { throw 'PDF frame rendering failed.' }
}
@{id='hwfc-frames';name='Локальные рамки HWFC';background='#102436';accent='#e2b361';text='#b4d9e7';orderFrame='order.png';interruptFrame='interrupt.png'} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'style.json') -Encoding utf8
Write-Host "Imported private frame pack: $destination"
