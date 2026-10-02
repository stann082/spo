<#
.SYNOPSIS
    Installs the spo top-list monitor as a daily Scheduled Task, plus a half-hourly play log.

.DESCRIPTION
    Publishes the monitor project as a single self-contained executable, copies it to
    %LOCALAPPDATA%\Programs\spo-monitor, and registers a Scheduled Task named
    "spo Top Monitor" that runs it once a day.

    A second task, "spo Play Log", runs the same executable with --plays-only every half hour.
    Spotify keeps no play counts and only shows your last 50 plays, so the counts that
    'spo plays' reports are built by copying that list into history.db before it scrolls away.

    The task runs interactively as the current user. That matters twice over: the monitor
    reads your Spotify login from %APPDATA%\spo\config.json, and Windows only delivers
    toast notifications to an interactive session. A Windows Service would satisfy neither.

    Snapshots are stored in %APPDATA%\spo\history.db and logs in %APPDATA%\spo\logs.

    The script is idempotent. If the task already exists, it offers to reinstall (republish +
    replace binaries), run it now, or uninstall it.

    Elevation is NOT required - everything is scoped to the current user.

.PARAMETER Time
    Time of day to run, as HH:mm. Defaults to 00:00 (midnight).

.PARAMETER PlayLogMinutes
    How often the play log runs, in minutes. Defaults to 30; 50 plays have to fit in between.

.EXAMPLE
    .\install-monitor.ps1
    .\install-monitor.ps1 -Time 03:30
#>
[CmdletBinding()]
param(
    [ValidatePattern('^\d{2}:\d{2}$')]
    [string]$Time = '00:00',

    [ValidateRange(5, 120)]
    [int]$PlayLogMinutes = 30
)

$ErrorActionPreference = 'Stop'

$TaskName   = 'spo Top Monitor'
$PlayTask   = 'spo Play Log'
$TaskPath   = '\spo\'
$InstallDir = Join-Path $env:LOCALAPPDATA 'Programs\spo-monitor'
$RepoRoot   = Split-Path -Parent $PSScriptRoot
$Project    = Join-Path $RepoRoot 'src\monitor\monitor.csproj'
$StagingDir = Join-Path $RepoRoot 'artifacts\publish\monitor'
$ExePath    = Join-Path $InstallDir 'spo-monitor.exe'
$AumidKey   = 'HKCU:\Software\Classes\AppUserModelId\spo.TopMonitor'

function Publish-Monitor {
    Write-Host "Publishing monitor to staging..." -ForegroundColor Cyan
    if (Test-Path $StagingDir) {
        Remove-Item $StagingDir -Recurse -Force
    }
    dotnet publish $Project -c Release -o $StagingDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }
}

function Copy-MonitorFiles {
    Write-Host "Copying files to $InstallDir..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Force $InstallDir | Out-Null
    Copy-Item (Join-Path $StagingDir '*') $InstallDir -Recurse -Force
}

function Register-MonitorTask {
    Write-Host "Registering scheduled task '$TaskName' for $Time daily..." -ForegroundColor Cyan

    $action = New-ScheduledTaskAction -Execute $ExePath -WorkingDirectory $InstallDir
    $trigger = New-ScheduledTaskTrigger -Daily -At $Time

    # Interactive logon so the toast reaches the desktop and %APPDATA% resolves to this user.
    $principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive -RunLevel Limited

    # StartWhenAvailable makes a missed midnight run (machine asleep or logged off) fire once
    # the user is back, rather than being skipped until tomorrow.
    $settings = New-ScheduledTaskSettingsSet `
        -StartWhenAvailable `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries `
        -ExecutionTimeLimit (New-TimeSpan -Minutes 15) `
        -MultipleInstances IgnoreNew `
        -Hidden

    Register-ScheduledTask `
        -TaskName $TaskName `
        -TaskPath $TaskPath `
        -Action $action `
        -Trigger $trigger `
        -Principal $principal `
        -Settings $settings `
        -Description 'Snapshots your top Spotify artists and tracks daily and reports what moved.' `
        -Force | Out-Null

    Write-Host "Task registered." -ForegroundColor Green
}

function Register-PlayLogTask {
    Write-Host "Registering scheduled task '$PlayTask' for every $PlayLogMinutes minutes..." -ForegroundColor Cyan

    # A console program started by Task Scheduler flashes a window; conhost --headless runs it without one.
    $action = New-ScheduledTaskAction -Execute 'conhost.exe' -Argument "--headless `"$ExePath`" --plays-only" -WorkingDirectory $InstallDir

    # No -RepetitionDuration: the trigger repeats for as long as the task exists.
    $trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).Date -RepetitionInterval (New-TimeSpan -Minutes $PlayLogMinutes)

    $principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive -RunLevel Limited

    $settings = New-ScheduledTaskSettingsSet `
        -StartWhenAvailable `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries `
        -ExecutionTimeLimit (New-TimeSpan -Minutes 5) `
        -MultipleInstances IgnoreNew `
        -Hidden

    Register-ScheduledTask `
        -TaskName $PlayTask `
        -TaskPath $TaskPath `
        -Action $action `
        -Trigger $trigger `
        -Principal $principal `
        -Settings $settings `
        -Description 'Copies your recently played Spotify tracks into the play log that "spo plays" counts.' `
        -Force | Out-Null

    Write-Host "Task registered." -ForegroundColor Green
}

function Unregister-MonitorTask {
    Write-Host "Removing scheduled task '$TaskName'..." -ForegroundColor Cyan
    Unregister-ScheduledTask -TaskName $TaskName -TaskPath $TaskPath -Confirm:$false
    Write-Host "Task removed." -ForegroundColor Green

    if (Get-ScheduledTask -TaskName $PlayTask -TaskPath $TaskPath -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName $PlayTask -TaskPath $TaskPath -Confirm:$false
        Write-Host "Removed the play log task." -ForegroundColor Green
    }

    if (Test-Path $AumidKey) {
        Remove-Item $AumidKey -Recurse -Force
        Write-Host "Removed the toast notification registration." -ForegroundColor Green
    }

    if (Test-Path $InstallDir) {
        $answer = Read-Host "Remove installed files at $InstallDir as well? [y/N]"
        if ($answer -match '^[yY]') {
            Remove-Item $InstallDir -Recurse -Force
            Write-Host "Removed $InstallDir." -ForegroundColor Green
        }
    }

    $dataDir = Join-Path $env:APPDATA 'spo'
    Write-Host "History and logs were left in $dataDir." -ForegroundColor Yellow
}

function Start-MonitorTask {
    Write-Host "Running the monitor now..." -ForegroundColor Cyan
    Start-ScheduledTask -TaskName $TaskName -TaskPath $TaskPath
    Write-Host "Started. Check the log at $env:APPDATA\spo\logs." -ForegroundColor Green
}

function Show-Summary {
    Write-Host ""
    Write-Host "Installed:  $ExePath"          -ForegroundColor Gray
    Write-Host "Schedule:   daily at $Time"     -ForegroundColor Gray
    Write-Host "Play log:   every $PlayLogMinutes minutes (see it with 'spo plays')" -ForegroundColor Gray
    Write-Host "History:    $env:APPDATA\spo\history.db" -ForegroundColor Gray
    Write-Host "Logs:       $env:APPDATA\spo\logs"       -ForegroundColor Gray
    Write-Host ""
    Write-Host "Preview a report without touching the history:" -ForegroundColor Gray
    Write-Host "  & '$ExePath' --dry-run"       -ForegroundColor Gray
    Write-Host ""
}

# --- Main ---

$existing = Get-ScheduledTask -TaskName $TaskName -TaskPath $TaskPath -ErrorAction SilentlyContinue

if ($null -eq $existing) {
    Write-Host "Task '$TaskName' is not installed. Performing fresh install." -ForegroundColor Cyan

    Write-Host ""
    Write-Host "The monitor reads the Spotify login stored by the CLI." -ForegroundColor Yellow
    Write-Host "If you have not logged in as this user, run 'spo login' first." -ForegroundColor Yellow
    Write-Host ""

    Publish-Monitor
    Copy-MonitorFiles
    Register-MonitorTask
    Register-PlayLogTask
    Show-Summary

    $answer = Read-Host "Run it once now to record the baseline? [Y/n]"
    if ($answer -notmatch '^[nN]') {
        Start-MonitorTask
    }
}
else {
    Write-Host "Task '$TaskName' is already installed (state: $($existing.State))." -ForegroundColor Yellow

    $choices = @(
        [System.Management.Automation.Host.ChoiceDescription]::new('&Reinstall', 'Republish the monitor, replace the binaries and re-register both tasks.')
        [System.Management.Automation.Host.ChoiceDescription]::new('Run &now',   'Trigger the installed task immediately.')
        [System.Management.Automation.Host.ChoiceDescription]::new('&Uninstall', 'Remove both tasks and the toast registration.')
        [System.Management.Automation.Host.ChoiceDescription]::new('&Cancel',    'Do nothing and exit.')
    )
    $choice = $Host.UI.PromptForChoice("Task already exists", "What would you like to do?", $choices, 0)

    switch ($choice) {
        0 {
            Publish-Monitor
            Copy-MonitorFiles
            Register-MonitorTask
            Register-PlayLogTask
            Show-Summary
        }
        1 { Start-MonitorTask }
        2 { Unregister-MonitorTask }
        3 { Write-Host "Cancelled." }
    }
}
