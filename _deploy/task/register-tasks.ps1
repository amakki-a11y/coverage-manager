<#
.SYNOPSIS
  Registers the Coverage Manager scheduled tasks (API, watchdog, optional
  collector and Caddy) so they start at boot and run whether or not anyone is
  logged on.

.DESCRIPTION
  Run this from an ELEVATED PowerShell (Run as administrator): boot-triggered
  tasks that run "whether the user is logged on or not" cannot be registered by
  a standard user (Task Scheduler answers "Access is denied"). Pass -User for
  the account that owns C:\CoverageManager and should run the app (default
  .\makkioo). You are prompted once for that account's Windows password; it is
  handed to the Task Scheduler in memory only and never written to disk. A
  stored password is required so the tasks run with the user profile loaded
  (USER-scope secrets from set-secrets.ps1 become visible to the API).

  After registration the app account is granted read/write/execute on each
  task, so day-to-day start/stop/swap (build.ps1 -Swap, watchdog.ps1) works
  from that account without elevation.

  Tasks (all: at startup, restart on failure, no run-time limit):
    CoverageManager-Api        _deploy\task\start-api.ps1
    CoverageManager-Watchdog   _deploy\task\watchdog.ps1  (every minute, 2 probes 30 s apart)
    CoverageManager-Collector  _deploy\task\start-collector.ps1   (-Collector)
    CoverageManager-Caddy      _deploy\task\start-caddy.ps1       (-Caddy)

  Re-running the script replaces existing definitions (-Force).
  Unregister: Unregister-ScheduledTask -TaskName 'CoverageManager-*' -Confirm:$false

  ASCII only: Windows PowerShell 5.1 reads BOM-less files as cp1252.
#>
param(
  [string]$Root = 'C:\CoverageManager',
  [string]$User = '.\makkioo',
  [switch]$Collector,
  [switch]$Caddy,
  [switch]$NoStart
)

$ErrorActionPreference = 'Stop'
$scripts = Join-Path $Root '_deploy\task'
foreach ($s in 'start-api.ps1', 'watchdog.ps1', 'start-collector.ps1', 'start-caddy.ps1') {
  if (-not (Test-Path (Join-Path $scripts $s))) { throw "Missing $scripts\$s" }
}

$isElevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isElevated) {
  Write-Warning 'Not elevated: registering boot-triggered tasks usually fails with "Access is denied". Re-run from an elevated PowerShell if it does.'
}

# Resolve the app account's SID once; used to grant it control of the tasks.
$accountName = $User -replace '^\.\\', "$env:COMPUTERNAME\"
$appSid = (New-Object System.Security.Principal.NTAccount($accountName)).Translate([System.Security.Principal.SecurityIdentifier]).Value

Write-Host "Registering Coverage Manager tasks under $User (root $Root)."
$secure = Read-Host -AsSecureString "Windows password for $User (needed so tasks run when nobody is logged on)"
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try   { $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
if ([string]::IsNullOrEmpty($password)) { throw 'A password is required.' }

function New-TaskAction([string]$Script) {
  $arg = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "{0}" -Root "{1}"' -f (Join-Path $scripts $Script), $Root
  New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $arg -WorkingDirectory $Root
}

function Register-Task([string]$Name, $Action, $Trigger, $Settings, [string]$Description) {
  Register-ScheduledTask -TaskName $Name -Action $Action -Trigger $Trigger -Settings $Settings `
    -User $User -Password $password -RunLevel Limited -Description $Description -Force | Out-Null
  Write-Host "  registered $Name"

  # Let the app account read/run/stop its own tasks (Administrators + SYSTEM keep full control).
  try {
    $svc = New-Object -ComObject 'Schedule.Service'
    $svc.Connect()
    $task = $svc.GetFolder('\').GetTask($Name)
    $task.SetSecurityDescriptor("D:(A;;FA;;;BA)(A;;FA;;;SY)(A;;FRFWFX;;;$appSid)", 0)
    Write-Host "  granted $User read/write/execute on $Name"
  } catch {
    Write-Warning "  could not set task permissions on ${Name}: $($_.Exception.Message). The app account may need an admin to start/stop it."
  }
}

# Long-running services: no time limit, restart every minute on failure.
$serviceSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) `
  -RestartCount 99 -RestartInterval (New-TimeSpan -Minutes 1) -MultipleInstances IgnoreNew `
  -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries

$registered = @()

Register-Task 'CoverageManager-Api' (New-TaskAction 'start-api.ps1') (New-ScheduledTaskTrigger -AtStartup) $serviceSettings `
  'Coverage Manager API (Kestrel on 127.0.0.1:5000). Restarted by CoverageManager-Watchdog when /api/exposure/status stops answering.'
$registered += 'CoverageManager-Api'

# Watchdog: at startup, then every minute for ever (Task Scheduler's minimum
# repetition); each run does two probes 30 s apart and is capped at 2 min.
$wdTrigger = New-ScheduledTaskTrigger -AtStartup
# No -RepetitionDuration: an omitted duration means "repeat indefinitely"
# (Task Scheduler rejects [TimeSpan]::MaxValue as out of range).
$wdTrigger.Repetition = (New-ScheduledTaskTrigger -Once -At (Get-Date) `
  -RepetitionInterval (New-TimeSpan -Minutes 1)).Repetition
$wdSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Minutes 2) `
  -MultipleInstances IgnoreNew -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
Register-Task 'CoverageManager-Watchdog' (New-TaskAction 'watchdog.ps1') $wdTrigger $wdSettings `
  'Probes the Coverage Manager API/collector every 30 s (2 probes per minutely run) and restarts the owning task after 3 consecutive failures.'
$registered += 'CoverageManager-Watchdog'

if ($Collector) {
  Register-Task 'CoverageManager-Collector' (New-TaskAction 'start-collector.ps1') (New-ScheduledTaskTrigger -AtStartup) $serviceSettings `
    'Coverage Manager Python LP collector (uvicorn on 127.0.0.1:8100), attached to the LP MT5 Terminal on this server.'
  $registered += 'CoverageManager-Collector'
}

if ($Caddy) {
  Register-Task 'CoverageManager-Caddy' (New-TaskAction 'start-caddy.ps1') (New-ScheduledTaskTrigger -AtStartup) $serviceSettings `
    'Caddy HTTPS reverse proxy (:80/:443 -> 127.0.0.1:5000) for Coverage Manager.'
  $registered += 'CoverageManager-Caddy'
}

$password = $null

if (-not $NoStart) {
  foreach ($n in $registered) {
    Start-ScheduledTask -TaskName $n
    Write-Host "  started $n"
  }
}

Write-Host ''
Write-Host 'Check:  Get-ScheduledTask -TaskName "CoverageManager-*" | Select TaskName, State'
Write-Host '        Invoke-WebRequest http://127.0.0.1:5000/api/exposure/status -UseBasicParsing'
