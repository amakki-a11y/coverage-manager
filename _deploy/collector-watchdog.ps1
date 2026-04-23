<#
.SYNOPSIS
  Active liveness probe for coverage-collector. Restarts the NSSM service when
  the collector's /health endpoint goes unresponsive for 3 consecutive checks.

.DESCRIPTION
  The MetaTrader5 Python library is a synchronous C binding; any call
  (positions_get, history_deals_get, account_info, initialize, …) blocks the
  thread it runs on. Even with the asyncio.to_thread wrapper added in the
  collector, a sufficiently bad MT5 hang can still leave the process "up" as
  far as NSSM is concerned — NSSM only watches for process crash, not for a
  process that no longer serves HTTP.

  This watchdog fills the gap:
    * Windows Scheduled Task runs every 30 seconds (register with the
      accompanying docs/DEPLOYMENT.md §13 block).
    * Each tick: GET http://127.0.0.1:8100/health with a 3-second timeout.
    * Any non-200 or timeout bumps a failure counter in a state file.
    * 3 consecutive failures (~90 sec) → `nssm restart coverage-collector`
      and reset the counter.
    * Success → reset the counter.
    * Log every transition to C:\apps\coverage-manager\logs\watchdog.log.

  The Scheduled Task runs as .\makkioo (same principal as coverage-collector)
  so it has permission to talk to NSSM without UAC prompts.

.PARAMETER HealthUrl
  Collector /health endpoint. Default http://127.0.0.1:8100/health.

.PARAMETER TimeoutSec
  Per-call HTTP timeout. Default 3 seconds.

.PARAMETER FailuresNeeded
  Consecutive failures required before restarting. Default 3 (~90 s at the
  30 s task cadence).

.PARAMETER StateFile
  Where the failure counter is persisted between runs.

.PARAMETER LogFile
  Append-only watchdog log.

.PARAMETER ServiceName
  NSSM service to restart. Default coverage-collector.

.EXAMPLE
  # Ad-hoc dry-run
  pwsh .\collector-watchdog.ps1 -HealthUrl http://127.0.0.1:8100/health

.NOTES
  Register as a scheduled task per docs/DEPLOYMENT.md §13.
#>
param(
  [string]$HealthUrl       = 'http://127.0.0.1:8100/health',
  [int]   $TimeoutSec      = 3,
  [int]   $FailuresNeeded  = 3,
  [string]$StateFile       = 'C:\apps\coverage-manager\logs\watchdog.state',
  [string]$LogFile         = 'C:\apps\coverage-manager\logs\watchdog.log',
  [string]$ServiceName     = 'coverage-collector'
)

# Lazy-create the logs folder so the task's first run doesn't hard-fail on
# a fresh box.
$logDir = Split-Path $LogFile -Parent
if (-not (Test-Path $logDir)) {
  New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

function Write-WatchLog {
  param([string]$Level, [string]$Message)
  $ts = (Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz')
  "$ts [$Level] $Message" | Out-File -FilePath $LogFile -Append -Encoding utf8
}

# Read the persisted failure counter. Treat any parse error as 0 so a
# corrupt state file can't wedge the watchdog.
function Read-Failures {
  if (Test-Path $StateFile) {
    try { return [int](Get-Content $StateFile -Raw -ErrorAction Stop).Trim() } catch { return 0 }
  }
  return 0
}

function Write-Failures {
  param([int]$Value)
  try { $Value.ToString() | Out-File -FilePath $StateFile -Encoding utf8 -Force }
  catch { Write-WatchLog 'ERROR' "Failed to persist state: $_" }
}

# Probe the collector. Return $true on HTTP 200 with a JSON body containing
# a `status` field that isn't `disconnected` — the collector reports "ok" or
# "stale" when it's usable, "disconnected" only when MT5 is completely down.
# A disconnected collector is STILL responsive (it can answer /health within
# the timeout), so we don't restart for that — we restart only for actual
# non-response (timeout / TCP error / HTTP 5xx).
function Test-CollectorHealth {
  try {
    $response = Invoke-WebRequest -Uri $HealthUrl -TimeoutSec $TimeoutSec -UseBasicParsing -ErrorAction Stop
    if ($response.StatusCode -ne 200) {
      return @{ ok = $false; reason = "http_$($response.StatusCode)" }
    }
    # Parse but don't require specific fields — response being 200 with a
    # body is enough to prove the process is alive and serving.
    $null = $response.Content | ConvertFrom-Json -ErrorAction SilentlyContinue
    return @{ ok = $true; reason = 'ok' }
  }
  catch [System.Net.WebException] {
    # Timeout / TCP reset / refused — the process is stuck or gone.
    return @{ ok = $false; reason = "net_$($_.Exception.Status)" }
  }
  catch {
    return @{ ok = $false; reason = "err_$($_.Exception.GetType().Name)" }
  }
}

$result = Test-CollectorHealth
$failures = Read-Failures

if ($result.ok) {
  if ($failures -gt 0) {
    Write-WatchLog 'INFO' "Recovered after $failures consecutive failure(s)"
  }
  Write-Failures 0
  exit 0
}

$failures++
Write-WatchLog 'WARN' "Health probe failed: $($result.reason) (consecutive=$failures/$FailuresNeeded)"
Write-Failures $failures

if ($failures -lt $FailuresNeeded) {
  exit 0
}

# Hit the threshold — restart the service and zero the counter.
Write-WatchLog 'ERROR' "Restarting $ServiceName after $failures consecutive failures"
try {
  # Prefer nssm restart when the collector is an NSSM-managed service;
  # fall back to sc.exe if nssm isn't on PATH for the task's principal.
  $nssmCmd = Get-Command nssm -ErrorAction SilentlyContinue
  if ($nssmCmd) {
    & $nssmCmd.Path restart $ServiceName 2>&1 | Out-File -FilePath $LogFile -Append -Encoding utf8
  } else {
    Restart-Service -Name $ServiceName -Force -ErrorAction Stop
  }
  Write-WatchLog 'INFO' "Restart command dispatched"
} catch {
  Write-WatchLog 'ERROR' "Restart failed: $_"
}

Write-Failures 0
exit 0
