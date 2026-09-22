<#
.SYNOPSIS
    Publishes the spo CLI as a single self-contained exe and installs it for the current user.

.DESCRIPTION
    Installs to %LOCALAPPDATA%\Programs\spo\spo2.exe. Add that folder to your user PATH once.

    The exe is installed as spo2.exe while the legacy spoticli app still owns the name spo.exe
    (%APPDATA%\utils\spo.exe). Once the legacy app is retired, set $ExeName back to 'spo.exe'.

.PARAMETER InstallDir
    Where to put the exe. Defaults to %LOCALAPPDATA%\Programs\spo.
#>
[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\spo')
)

$ErrorActionPreference = 'Stop'

# Temporary name while the legacy spoticli build is still installed as spo.exe.
$ExeName    = 'spo2.exe'

$RepoRoot   = Split-Path -Parent $PSScriptRoot
$Project    = Join-Path $RepoRoot 'src\cli\cli.csproj'
$StagingDir = Join-Path $RepoRoot 'artifacts\publish\cli'
$ExePath    = Join-Path $InstallDir $ExeName

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

$command = [System.IO.Path]::GetFileNameWithoutExtension($ExeName)
$resolved = Get-Command $command -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $resolved) {
    Write-Host "$command is not on your PATH yet. Add $InstallDir to your user PATH." -ForegroundColor Yellow
}
elseif ($resolved.Source -ne $ExePath) {
    Write-Host "Heads up: '$command' currently resolves to $($resolved.Source), not $ExePath." -ForegroundColor Yellow
}
