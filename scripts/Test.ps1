param([string]$Godot = '', [switch]$CoreOnly)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $projectRoot
$results = [System.Collections.Generic.List[object]]::new()
$outputRoot = Join-Path $projectRoot 'tmp/test-results'
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$env:NUGET_PACKAGES = Join-Path $projectRoot '.local/nuget'
function Step([string]$name, [scriptblock]$action) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    try { & $action; $results.Add(@{name=$name;passed=$true;seconds=$watch.Elapsed.TotalSeconds}); Write-Host "PASS $name" }
    catch { $results.Add(@{name=$name;passed=$false;error=$_.Exception.Message}); throw }
}
function EngineProcess([string]$name, [string[]]$flags) {
    $env:FLEET_COMMAND_DATA_DIR = Join-Path $outputRoot ($name + '-data')
    $arguments = @('--headless','--path',('"'+$projectRoot+'"'),'--') + $flags
    $child = Start-Process -FilePath $Godot -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $outputRoot ($name+'.log')) -RedirectStandardError (Join-Path $outputRoot ($name+'.error.log'))
    $null = $child.Handle # Cache the handle before exit; Windows PowerShell otherwise loses ExitCode.
    return $child
}
function Finish($process, [string]$name, [string]$marker) {
    if (-not $process.WaitForExit(45000)) { $process.Kill(); throw "$name timeout" }
    $process.WaitForExit()
    $log = Get-Content -LiteralPath (Join-Path $outputRoot ($name+'.log')) -Raw
    $errors = Get-Content -LiteralPath (Join-Path $outputRoot ($name+'.error.log')) -Raw
    if ($process.ExitCode -ne 0 -or $log -notmatch $marker -or $errors -match 'ERROR:') { throw "$name failed (exit $($process.ExitCode)); see $outputRoot" }
}
try {
    Step 'Core build and checks' { & ./scripts/Start.ps1 -ChecksOnly; if ($LASTEXITCODE -ne 0) { throw 'Core checks failed' } }
    Step 'Deterministic battle scenarios and save/resume' { & dotnet tests/FleetCommand.Checks/bin/Debug/net8.0/FleetCommand.Checks.dll scenarios .; if ($LASTEXITCODE -ne 0) { throw 'Scenarios failed' } }
    if (-not $CoreOnly) {
        if (-not $Godot) { $Godot = (Get-ChildItem -LiteralPath 'tmp/tools/godot' -Filter '*console.exe' -Recurse | Select-Object -First 1).FullName }
        if (-not $Godot) { throw 'Provide -Godot with the Godot .NET executable path' }
        Step 'Game build' { & dotnet build FleetCommand.Game.csproj --nologo --disable-build-servers -m:1; if ($LASTEXITCODE -ne 0) { throw 'Game build failed' } }
        foreach ($test in @(@('controls','--check-controls','CONTROLS_CHECKS_OK'),@('presentation','--check-presentation','PRESENTATION_CHECKS_OK'),@('actions','--check-actions','ACTION_HIGHLIGHTS_OK'),@('cards','--check-cards','CARD_UI_OK'),@('special-ui','--check-special','SPECIAL_UI_OK'),@('interrupt-ui','--check-interrupt','INTERRUPT_UI_OK'))) {
            Step ('UI '+$test[0]) { $process = EngineProcess $test[0] @($test[1]); Finish $process $test[0] $test[2] }
        }
        foreach ($mode in @('pvp','coop','cards','special','interrupt')) {
            Step ('Network '+$mode) {
                $extra = @(); if ($mode -eq 'cards') { $extra = @('--smoke-cards') }; if ($mode -eq 'coop') { $extra = @('--smoke-coop') }
                if ($mode -eq 'special') { $extra = @('--smoke-cards','--smoke-special') }
                if ($mode -eq 'interrupt') { $extra = @('--smoke-cards','--smoke-interrupt') }
                $server = EngineProcess ($mode+'-host') (@('--smoke-host') + $extra)
                try {
                    $ready = $false
                    for ($attempt=0; $attempt -lt 100; $attempt++) {
                        $serverLog = Join-Path $outputRoot ($mode+'-host.log')
                        if ((Test-Path -LiteralPath $serverLog) -and (Get-Content -LiteralPath $serverLog -Raw) -match 'FLEET_COMMAND_READY') { $ready=$true; break }
                        Start-Sleep -Milliseconds 100
                    }
                    if (-not $ready) { throw 'Host failed to become ready' }
                    $client = EngineProcess ($mode+'-client') (@('--smoke-client') + $extra)
                    Finish $client ($mode+'-client') ($(if ($mode -eq 'interrupt') { 'INTERRUPT_NETWORK_OK' } elseif ($mode -eq 'special') { 'SPECIAL_NETWORK_OK' } elseif ($mode -eq 'cards') { 'CARD_NETWORK_OK' } else { 'NETWORK_SNAPSHOT_OK' }))
                    Finish $server ($mode+'-host') ($(if ($mode -eq 'interrupt') { 'INTERRUPT_NETWORK_OK' } elseif ($mode -eq 'special') { 'SPECIAL_NETWORK_OK' } elseif ($mode -eq 'cards') { 'CARD_NETWORK_OK' } else { 'NETWORK_COMMAND_ACCEPTED' }))
                    if ($mode -eq 'interrupt' -and (Get-Content -LiteralPath (Join-Path $outputRoot 'interrupt-host.log') -Raw) -notmatch 'INTERRUPT_NETWORK_RESUMED') { throw 'Host did not restore the interrupt save' }
                } finally { if (-not $server.HasExited) { $server.Kill() } }
            }
        }
    }
} catch { Write-Error $_ -ErrorAction Continue; $failure = $true }
finally {
    @{passed=(-not $failure); results=@($results.ToArray())} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $outputRoot 'summary.json') -Encoding utf8
    Pop-Location
}
if ($failure) { exit 1 }
Write-Host "ALL TESTS PASSED - $outputRoot/summary.json"
