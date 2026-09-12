<#
.SYNOPSIS
  Runs the Python LP collector for the CoverageManager-Collector scheduled task.

.DESCRIPTION
  Starts collector\venv\Scripts\python.exe -m uvicorn main:app on 127.0.0.1:8100
  and waits for it. The collector attaches to the LP's MT5 Terminal on this
  server (launching it if needed) and pulls the LP credentials from the API
  (/api/settings/accounts), so no secrets are needed here.

  Collector stdout/stderr -> logs\collector-<timestamp>.out.log / .err.log

  ASCII only: Windows PowerShell 5.1 reads BOM-less files as cp1252.
#>
param(
  [string]$Root        = 'C:\CoverageManager',
  [string]$BackendUrl  = 'http://127.0.0.1:5000',
  [string]$BindHost    = '127.0.0.1',
  [int]   $Port        = 8100,
  [int]   $KeepLogDays = 14
)

$ErrorActionPreference = 'Continue'

$collectorDir = Join-Path $Root 'collector'
$python       = Join-Path $collectorDir 'venv\Scripts\python.exe'
$logDir       = Join-Path $Root 'logs'
$runDir       = Join-Path $Root 'run'
foreach ($d in @($logDir, $runDir)) {
  if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
}
$startLog = Join-Path $logDir 'collector-start.log'
$pidFile  = Join-Path $runDir 'collector.pid'

function Write-StartLog([string]$Message) {
  ('{0} {1}' -f (Get-Date -Format 'yyyy-MM-ddTHH:mm:sszzz'), $Message) |
    Out-File -FilePath $startLog -Append -Encoding ascii
}

if (-not (Test-Path $python)) {
  Write-StartLog "ERROR: $python not found. Create the venv: uv venv collector\venv --python 3.12; uv pip install -r collector\requirements.txt"
  exit 2
}

$env:BACKEND_URL = $BackendUrl
$env:PYTHONUNBUFFERED = '1'

Get-ChildItem $logDir -Filter 'collector-*.log' -ErrorAction SilentlyContinue |
  Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$KeepLogDays) } |
  Remove-Item -Force -ErrorAction SilentlyContinue

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$out   = Join-Path $logDir "collector-$stamp.out.log"
$err   = Join-Path $logDir "collector-$stamp.err.log"
$args  = "-m uvicorn main:app --host $BindHost --port $Port"
Write-StartLog "Starting $python $args backend=$BackendUrl"

$proc = Start-Process -FilePath $python -ArgumentList $args -WorkingDirectory $collectorDir -NoNewWindow -PassThru `
          -RedirectStandardOutput $out -RedirectStandardError $err
$null = $proc.Handle
$proc.Id | Out-File -FilePath $pidFile -Encoding ascii -Force
Write-StartLog "Collector pid $($proc.Id)"

$proc.WaitForExit()
$code = $proc.ExitCode
Write-StartLog "Collector exited with code $code"
Remove-Item $pidFile -Force -ErrorAction SilentlyContinue
# Any exit counts as a failure so Task Scheduler's restart-on-failure relaunches it.
if ($code -eq 0) { $code = 1 }
exit $code
