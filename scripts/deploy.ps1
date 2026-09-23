<#
.SYNOPSIS
    Publishes the spo CLI as a single self-contained exe and installs it for the current user.

.DESCRIPTION
    Installs to %LOCALAPPDATA%\Programs\spo\spo.exe. Add that folder to your user PATH once.

    Until the legacy spoticli app was retired (2026-09) the exe was installed as spo2.exe; a
    leftover spo2.exe in the install folder is removed.

.PARAMETER InstallDir
    Where to put the exe. Defaults to %LOCALAPPDATA%\Programs\spo.
#>
[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\spo')
)

$ErrorActionPreference = 'Stop'

$ExeName    = 'spo.exe'

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

$oldExe = Join-Path $InstallDir 'spo2.exe'
if (Test-Path $oldExe) {
    Remove-Item $oldExe -Force
    Write-Host "Removed the old $oldExe" -ForegroundColor Green
}

$command = [System.IO.Path]::GetFileNameWithoutExtension($ExeName)
$resolved = Get-Command $command -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $resolved) {
    Write-Host "$command is not on your PATH yet. Add $InstallDir to your user PATH." -ForegroundColor Yellow
}
elseif ($resolved.Source -ne $ExePath) {
    Write-Host "Heads up: '$command' currently resolves to $($resolved.Source), not $ExePath." -ForegroundColor Yellow
}
