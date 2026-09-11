<#
.SYNOPSIS
  Runs the Coverage Manager API for the CoverageManager-Api scheduled task.

.DESCRIPTION
  Starts publish\api\CoverageManager.Api.exe and waits for it, so the scheduled
  task stays "Running" for the lifetime of the API and Task Scheduler's
  restart-on-failure applies when the process dies.

  Non-secret settings are set below. Secrets (Supabase__Key, LiveBridge__ApiKey)
  are read from the task user's environment: set-secrets.ps1 stores them as
  USER-scope environment variables; this script never writes them anywhere
  and never logs them.

  API stdout/stderr -> logs\api-<timestamp>.out.log / .err.log
  Serilog file sink  -> publish\api\logs\coverage-manager-<date>.log

  ASCII only: Windows PowerShell 5.1 reads BOM-less files as cp1252.
#>
param(
  [string]$Root        = 'C:\CoverageManager',
  [string]$BindUrl     = 'http://127.0.0.1:5000',
  [int]   $KeepLogDays = 14
)

$ErrorActionPreference = 'Continue'

$exe     = Join-Path $Root 'publish\api\CoverageManager.Api.exe'
$workDir = Split-Path $exe -Parent
$logDir  = Join-Path $Root 'logs'
$runDir  = Join-Path $Root 'run'
foreach ($d in @($logDir, $runDir)) {
  if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
}
$startLog = Join-Path $logDir 'api-start.log'
$pidFile  = Join-Path $runDir 'api.pid'

function Write-StartLog([string]$Message) {
  ('{0} {1}' -f (Get-Date -Format 'yyyy-MM-ddTHH:mm:sszzz'), $Message) |
    Out-File -FilePath $startLog -Append -Encoding ascii
}

if (-not (Test-Path $exe)) {
  Write-StartLog "ERROR: $exe not found. Run _deploy\task\build.ps1 -Swap first."
  exit 2
}

# ---- Non-secret runtime settings -------------------------------------------
# Kestrel:Endpoints in appsettings.json overrides ASPNETCORE_URLS, so the
# double-underscore key is the one that actually wins; both are set for clarity.
$env:ASPNETCORE_ENVIRONMENT        = 'Production'
$env:Kestrel__Endpoints__Http__Url = $BindUrl
$env:ASPNETCORE_URLS               = $BindUrl

# ---- Secrets from the user's environment ------------------------------------
# Prefer whatever is already in the process environment (e.g. machine scope),
# otherwise pull USER-scope values. USER scope is available when the task was
# registered with a stored password (register-tasks.ps1), which loads the profile.
$secretNames = @('Supabase__Key', 'LiveBridge__ApiKey')
foreach ($name in $secretNames) {
  if (-not [Environment]::GetEnvironmentVariable($name, 'Process')) {
    $value = [Environment]::GetEnvironmentVariable($name, 'User')
    if ($value) { [Environment]::SetEnvironmentVariable($name, $value, 'Process') }
    $value = $null
  }
}
if (-not [Environment]::GetEnvironmentVariable('Supabase__Key', 'Process')) {
  Write-StartLog 'ERROR: Supabase__Key is not set for this user. Run _deploy\task\set-secrets.ps1 as the task user and register the task with a stored password (register-tasks.ps1), not S4U.'
  exit 3
}

# ---- Prune old per-run logs -------------------------------------------------
Get-ChildItem $logDir -Filter 'api-*.log' -ErrorAction SilentlyContinue |
  Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$KeepLogDays) } |
  Remove-Item -Force -ErrorAction SilentlyContinue

# ---- Launch -----------------------------------------------------------------
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$out   = Join-Path $logDir "api-$stamp.out.log"
$err   = Join-Path $logDir "api-$stamp.err.log"
$providerOverride = [Environment]::GetEnvironmentVariable('MT5__Provider', 'Process')
Write-StartLog ("Starting {0} bind={1} providerEnvOverride={2}" -f $exe, $BindUrl, $(if ($providerOverride) { $providerOverride } else { '(none, appsettings.json)' }))

$proc = Start-Process -FilePath $exe -WorkingDirectory $workDir -NoNewWindow -PassThru `
          -RedirectStandardOutput $out -RedirectStandardError $err
$null = $proc.Handle   # keeps ExitCode readable after the process ends
$proc.Id | Out-File -FilePath $pidFile -Encoding ascii -Force
Write-StartLog "API pid $($proc.Id)"

$proc.WaitForExit()
$code = $proc.ExitCode
Write-StartLog "API exited with code $code"
Remove-Item $pidFile -Force -ErrorAction SilentlyContinue
# The API must never exit on its own in production: report any exit as a
# failure so Task Scheduler's restart-on-failure relaunches it.
if ($code -eq 0) { $code = 1 }
exit $code
