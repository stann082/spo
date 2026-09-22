<#
.SYNOPSIS
    Publishes the spo CLI as a single self-contained exe and installs it for the current user.

.DESCRIPTION
    Installs to %LOCALAPPDATA%\Programs\spo\spo.exe. Add that folder to your user PATH once.

    The legacy spoticli deploy script also installs an exe named spo.exe, into %APPDATA%\utils.
    If that folder comes first on your PATH, typing `spo` still runs the legacy app - the script
    checks for this and tells you which one wins.

.PARAMETER InstallDir
    Where to put spo.exe. Defaults to %LOCALAPPDATA%\Programs\spo.
#>
[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\spo')
)

$ErrorActionPreference = 'Stop'

$RepoRoot   = Split-Path -Parent $PSScriptRoot
$Project    = Join-Path $RepoRoot 'src\cli\cli.csproj'
$StagingDir = Join-Path $RepoRoot 'artifacts\publish\cli'
$ExePath    = Join-Path $InstallDir 'spo.exe'

Write-Host "Publishing spo..." -ForegroundColor Cyan
if (Test-Path $StagingDir) {
    Remove-Item $StagingDir -Recurse -Force
}
dotnet publish $Project -c Release -o $StagingDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

New-Item -ItemType Directory -Force $InstallDir | Out-Null
Copy-Item (Join-Path $StagingDir 'spo.exe') $ExePath -Force
Write-Host "Installed $ExePath" -ForegroundColor Green

$resolved = Get-Command spo -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $resolved) {
    Write-Host "spo is not on your PATH yet. Add $InstallDir to your user PATH." -ForegroundColor Yellow
}
elseif ($resolved.Source -ne $ExePath) {
    Write-Host "Heads up: 'spo' currently resolves to $($resolved.Source)." -ForegroundColor Yellow
    Write-Host "That is probably the legacy spoticli build. Rename or remove it, or put $InstallDir earlier on PATH." -ForegroundColor Yellow
}
