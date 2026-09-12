<#
.SYNOPSIS
  Liveness watchdog for the Coverage Manager scheduled tasks.

.DESCRIPTION
  Triggered every minute by the CoverageManager-Watchdog scheduled task (Task
  Scheduler's minimum repetition). Each run performs -Passes probes spaced
  -PassIntervalSec apart (default 2 passes, 30 s apart), so the effective
  cadence is 30 s and three consecutive failures restart a task after ~90 s.

  For each managed task (API, Collector, Caddy):
    * task not registered or disabled       -> skipped
    * task not running                      -> started
    * started less than -GraceMinutes ago   -> left alone (cold start can take a
      while: the API loads deals from Supabase before it listens)
    * has a health URL: probe it with -TimeoutSec; -FailuresNeeded consecutive
      failures -> Stop-ScheduledTask, kill the leftover process from the pid
      file (only if its path is under -Root), Start-ScheduledTask
  A maintenance flag (run\maintenance.flag, written by build.ps1 -Swap) pauses
  all actions for up to 10 minutes so a deploy is not fought by the watchdog.

  State: run\watchdog-<task>.state   Log: logs\watchdog.log (rotated at 5 MB)

  ASCII only: Windows PowerShell 5.1 reads BOM-less files as cp1252.
#>
param(
  [string]$Root            = 'C:\CoverageManager',
  [int]   $TimeoutSec      = 5,
  [int]   $FailuresNeeded  = 3,
  [int]   $GraceMinutes    = 5,
  [int]   $Passes          = 2,
  [int]   $PassIntervalSec = 30
)

$ErrorActionPreference = 'Continue'

$logDir  = Join-Path $Root 'logs'
$runDir  = Join-Path $Root 'run'
foreach ($d in @($logDir, $runDir)) {
  if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
}
$logFile = Join-Path $logDir 'watchdog.log'
$flag    = Join-Path $runDir 'maintenance.flag'

$targets = @(
  @{ Task = 'CoverageManager-Api';       Url = 'http://127.0.0.1:5000/api/exposure/status'; PidFile = 'api.pid' },
  @{ Task = 'CoverageManager-Collector'; Url = 'http://127.0.0.1:8100/health';              PidFile = 'collector.pid' },
  @{ Task = 'CoverageManager-Caddy';     Url = '';                                          PidFile = 'caddy.pid' }
)

function Write-WatchLog([string]$Level, [string]$Message) {
  if ((Test-Path $logFile) -and ((Get-Item $logFile).Length -gt 5MB)) {
    Move-Item -Force $logFile ($logFile + '.1') -ErrorAction SilentlyContinue
  }
  ('{0} [{1}] {2}' -f (Get-Date -Format 'yyyy-MM-ddTHH:mm:sszzz'), $Level, $Message) |
    Out-File -FilePath $logFile -Append -Encoding ascii
}

function Get-Failures([string]$stateFile) {
  if (Test-Path $stateFile) {
    try { return [int](Get-Content $stateFile -Raw -ErrorAction Stop).Trim() } catch { return 0 }
  }
  return 0
}

function Set-Failures([string]$stateFile, [int]$value) {
  try { $value.ToString() | Out-File -FilePath $stateFile -Encoding ascii -Force }
  catch { Write-WatchLog 'ERROR' "Cannot persist state ${stateFile}: $_" }
}

function Test-Health([string]$url) {
  try {
    $r = Invoke-WebRequest -Uri $url -TimeoutSec $TimeoutSec -UseBasicParsing -ErrorAction Stop
    if ($r.StatusCode -eq 200) { return @{ ok = $true; reason = 'ok' } }
    return @{ ok = $false; reason = "http_$($r.StatusCode)" }
  } catch [System.Net.WebException] {
    return @{ ok = $false; reason = "net_$($_.Exception.Status)" }
  } catch {
    return @{ ok = $false; reason = "err_$($_.Exception.GetType().Name)" }
  }
}

function Stop-TaskProcess([string]$pidFile) {
  $path = Join-Path $runDir $pidFile
  if (-not (Test-Path $path)) { return }
  try {
    $procId = [int](Get-Content $path -Raw).Trim()
    $p = Get-Process -Id $procId -ErrorAction SilentlyContinue
    if ($p -and $p.Path -and $p.Path.StartsWith($Root, [StringComparison]::OrdinalIgnoreCase)) {
      Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
      Write-WatchLog 'INFO' "Killed leftover pid $procId ($($p.Path))"
    }
  } catch { }
  Remove-Item $path -Force -ErrorAction SilentlyContinue
}

function Test-MaintenancePause {
  if (-not (Test-Path $flag)) { return $false }
  $age = (Get-Date) - (Get-Item $flag).LastWriteTime
  if ($age.TotalMinutes -lt 10) { return $true }
  Write-WatchLog 'WARN' 'Stale maintenance flag (>10 min) ignored and removed'
  Remove-Item $flag -Force -ErrorAction SilentlyContinue
  return $false
}

function Invoke-WatchPass {
  if (Test-MaintenancePause) { return }

  foreach ($t in $targets) {
    $name      = $t.Task
    $stateFile = Join-Path $runDir "watchdog-$name.state"

    $task = Get-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue
    if (-not $task -or $task.State -eq 'Disabled') { continue }

    if ($task.State -ne 'Running') {
      Write-WatchLog 'WARN' "$name is $($task.State) - starting it"
      Stop-TaskProcess $t.PidFile
      try { Start-ScheduledTask -TaskName $name -ErrorAction Stop }
      catch { Write-WatchLog 'ERROR' "Start-ScheduledTask $name failed: $($_.Exception.Message.Trim())" }
      Set-Failures $stateFile 0
      continue
    }

    if (-not $t.Url) { continue }

    $info = Get-ScheduledTaskInfo -TaskName $name -ErrorAction SilentlyContinue
    if ($info -and $info.LastRunTime -and (((Get-Date) - $info.LastRunTime).TotalMinutes -lt $GraceMinutes)) {
      continue   # still inside the startup grace window
    }

    $result   = Test-Health $t.Url
    $failures = Get-Failures $stateFile

    if ($result.ok) {
      if ($failures -gt 0) { Write-WatchLog 'INFO' "$name recovered after $failures failure(s)" }
      Set-Failures $stateFile 0
      continue
    }

    $failures++
    Write-WatchLog 'WARN' "$name health failed: $($result.reason) (consecutive=$failures/$FailuresNeeded)"
    Set-Failures $stateFile $failures
    if ($failures -lt $FailuresNeeded) { continue }

    Write-WatchLog 'ERROR' "Restarting $name after $failures consecutive failures"
    $stopped = $false
    try {
      Stop-ScheduledTask -TaskName $name -ErrorAction Stop
      $stopped = $true
    } catch {
      # No control over the task (registered by an admin without granting us
      # rights): kill the process instead. start-*.ps1 then exits non-zero and
      # Task Scheduler's restart-on-failure relaunches it within a minute.
      Write-WatchLog 'WARN' "Stop-ScheduledTask $name failed ($($_.Exception.Message.Trim())) - killing the process; Task Scheduler restarts it on failure"
    }
    Start-Sleep -Seconds 2
    Stop-TaskProcess $t.PidFile
    if ($stopped) {
      try {
        Start-ScheduledTask -TaskName $name -ErrorAction Stop
        Write-WatchLog 'INFO' "$name restart dispatched"
      } catch {
        Write-WatchLog 'ERROR' "Start-ScheduledTask $name failed: $($_.Exception.Message.Trim())"
      }
    }
    Set-Failures $stateFile 0
  }
}

for ($pass = 1; $pass -le $Passes; $pass++) {
  Invoke-WatchPass
  if ($pass -lt $Passes) { Start-Sleep -Seconds $PassIntervalSec }
}

exit 0
