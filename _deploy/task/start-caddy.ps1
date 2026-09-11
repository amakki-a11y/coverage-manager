<#
.SYNOPSIS
  Runs Caddy (HTTPS reverse proxy) for the CoverageManager-Caddy scheduled task.

.DESCRIPTION
  Validates and then runs <Root>\Caddyfile. Certificates and Caddy's own state
  live under <Root>\caddy\ (XDG_* variables) so they survive user-profile
  changes and are easy to back up. Caddy binds :80 and :443, so nothing else
  may hold those ports on this server.

  ASCII only: Windows PowerShell 5.1 reads BOM-less files as cp1252.
#>
param(
  [string]$Root     = 'C:\CoverageManager',
  [string]$CaddyExe = ''
)

$ErrorActionPreference = 'Continue'

$logDir = Join-Path $Root 'logs'
$runDir = Join-Path $Root 'run'
foreach ($d in @($logDir, $runDir, (Join-Path $Root 'caddy\data'), (Join-Path $Root 'caddy\config'))) {
  if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
}
$startLog  = Join-Path $logDir 'caddy-start.log'
$pidFile   = Join-Path $runDir 'caddy.pid'
$caddyfile = Join-Path $Root 'Caddyfile'

function Write-StartLog([string]$Message) {
  ('{0} {1}' -f (Get-Date -Format 'yyyy-MM-ddTHH:mm:sszzz'), $Message) |
    Out-File -FilePath $startLog -Append -Encoding ascii
}

if (-not $CaddyExe) {
  $cmd = Get-Command caddy.exe -ErrorAction SilentlyContinue
  if ($cmd) { $CaddyExe = $cmd.Source }
}
if (-not $CaddyExe) {
  $pkg = Get-ChildItem (Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages') -Directory -Filter 'CaddyServer.Caddy*' -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($pkg) { $CaddyExe = Join-Path $pkg.FullName 'caddy.exe' }
}
if (-not $CaddyExe -or -not (Test-Path $CaddyExe)) {
  Write-StartLog 'ERROR: caddy.exe not found. Install with: winget install --id CaddyServer.Caddy --scope user'
  exit 2
}
if (-not (Test-Path $caddyfile)) {
  Write-StartLog "ERROR: $caddyfile not found. Copy _deploy\task\Caddyfile.template and set the hostname."
  exit 2
}

$env:XDG_DATA_HOME   = Join-Path $Root 'caddy\data'
$env:XDG_CONFIG_HOME = Join-Path $Root 'caddy\config'

$validate = & $CaddyExe validate --config $caddyfile --adapter caddyfile 2>&1
if ($LASTEXITCODE -ne 0) {
  Write-StartLog "ERROR: Caddyfile validation failed: $validate"
  exit 4
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$out   = Join-Path $logDir "caddy-$stamp.out.log"
$err   = Join-Path $logDir "caddy-$stamp.err.log"
Write-StartLog "Starting $CaddyExe run --config $caddyfile"

$proc = Start-Process -FilePath $CaddyExe -ArgumentList "run --config `"$caddyfile`" --adapter caddyfile" `
          -WorkingDirectory $Root -NoNewWindow -PassThru -RedirectStandardOutput $out -RedirectStandardError $err
$null = $proc.Handle
$proc.Id | Out-File -FilePath $pidFile -Encoding ascii -Force
Write-StartLog "Caddy pid $($proc.Id)"

$proc.WaitForExit()
$code = $proc.ExitCode
Write-StartLog "Caddy exited with code $code"
Remove-Item $pidFile -Force -ErrorAction SilentlyContinue
# Any exit counts as a failure so Task Scheduler's restart-on-failure relaunches it.
if ($code -eq 0) { $code = 1 }
exit $code
