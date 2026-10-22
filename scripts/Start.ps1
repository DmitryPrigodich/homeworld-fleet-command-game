param([string]$Godot = '', [switch]$ChecksOnly)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $projectRoot
try {
    $env:NUGET_PACKAGES = Join-Path $projectRoot '.local\nuget'
    $env:FLEET_COMMAND_DATA_DIR = Join-Path $projectRoot '.local'
    if ($ChecksOnly) {
        & dotnet build 'tests/FleetCommand.Checks/FleetCommand.Checks.csproj' --nologo --disable-build-servers -m:1
        if ($LASTEXITCODE -ne 0) { throw 'Check project build failed.' }
        & dotnet 'tests/FleetCommand.Checks/bin/Debug/net8.0/FleetCommand.Checks.dll' check .
        if ($LASTEXITCODE -ne 0) { throw 'Checks failed.' }
        return
    }
    if (-not $Godot) {
        $found = Get-Command godot -ErrorAction SilentlyContinue
        if ($found) { $Godot = $found.Source }
        elseif (Test-Path -LiteralPath 'tmp/tools/godot') {
            $Godot = (Get-ChildItem -LiteralPath 'tmp/tools/godot' -Recurse -Filter '*console.exe' | Select-Object -First 1).FullName
        }
    }
    if (-not $Godot -or -not (Test-Path -LiteralPath $Godot)) { throw 'Provide a Godot 4.7.2 .NET executable: scripts/Start.ps1 -Godot C:\path\Godot_console.exe' }
    $packages = Join-Path (Split-Path -Parent $Godot) 'GodotSharp/Tools/nupkgs'
    if (Test-Path -LiteralPath $packages) {
        $escaped = [System.Security.SecurityElement]::Escape($packages)
        Set-Content -LiteralPath 'NuGet.Config' -Value ('<configuration><packageSources><clear/><add key="GodotBundled" value="'+$escaped+'" /></packageSources></configuration>') -Encoding utf8
    }
    & dotnet build 'FleetCommand.Game.csproj' --nologo --disable-build-servers -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Game build failed.' }
    & $Godot --path $projectRoot
} finally { Pop-Location }
