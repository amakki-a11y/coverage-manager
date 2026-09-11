<#
.SYNOPSIS
  Builds the Coverage Manager API + React bundle into publish\api-staging and,
  with -Swap, puts it live for the CoverageManager-Api scheduled task.

.DESCRIPTION
  Scheduled-task edition of _deploy\deploy.ps1. No elevation needed.
    1. dotnet publish (Release)            -> publish\api-staging
    2. verify MT5APIManager64.dll sits at the publish root (P/Invoke needs it there)
    3. npm ci (fallback npm install) + npm run build (fallback npx vite build)
    4. copy web\dist\*                     -> publish\api-staging\wwwroot
    5. -Swap: write run\maintenance.flag (pauses the watchdog), stop
       CoverageManager-Api, rename publish\api -> publish\api-old-<ts>,
       staging -> api, start the task again, prune old builds (-KeepOld).

  Rollback (if the new build misbehaves):
    Stop-ScheduledTask CoverageManager-Api
    Rename-Item C:\CoverageManager\publish\api C:\CoverageManager\publish\api-FAILED-<ts>
    Rename-Item C:\CoverageManager\publish\api-old-<ts> C:\CoverageManager\publish\api
    Start-ScheduledTask CoverageManager-Api

  ASCII only: Windows PowerShell 5.1 reads BOM-less files as cp1252.
#>
param(
  [string]$Root = 'C:\CoverageManager',
  [switch]$SkipFrontend,
  [switch]$SkipBackend,
  [switch]$Swap,
  [int]$KeepOld = 2
)

# Native commands write warnings to stderr (vite chunk-size notices); with
# ErrorActionPreference=Stop those would kill the script, so rely on $LASTEXITCODE.
$ErrorActionPreference = 'Continue'
Set-Location $Root

function Step([string]$Message) { Write-Host ''; Write-Host "==> $Message" -ForegroundColor Cyan }

$ts         = Get-Date -Format 'yyyyMMdd-HHmmss'
$publishDir = Join-Path $Root 'publish\api'
$stagingDir = Join-Path $Root 'publish\api-staging'
$oldDir     = Join-Path $Root "publish\api-old-$ts"
$runDir     = Join-Path $Root 'run'
$flag       = Join-Path $runDir 'maintenance.flag'

if (-not $SkipBackend) {
  Step "Backend: dotnet publish -> $stagingDir"
  if (Test-Path $stagingDir) { Remove-Item -Recurse -Force $stagingDir }
  dotnet publish (Join-Path $Root 'src\CoverageManager.Api\CoverageManager.Api.csproj') -c Release -o $stagingDir
  if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

  $native = Join-Path $stagingDir 'MT5APIManager64.dll'
  if (-not (Test-Path $native)) {
    $lib = Join-Path $stagingDir 'Libs\MT5APIManager64.dll'
    if (Test-Path $lib) {
      Copy-Item -Force $lib $native
      Write-Host '    copied MT5APIManager64.dll from Libs\ to the publish root' -ForegroundColor Yellow
    } else {
      throw "MT5APIManager64.dll not found in $stagingDir - the Manager API would fail at runtime"
    }
  }
} elseif (-not (Test-Path $stagingDir)) {
  throw "-SkipBackend given but $stagingDir does not exist"
}

if (-not $SkipFrontend) {
  Step 'Frontend: npm ci (fallback npm install) + npm run build (fallback npx vite build)'
  Push-Location (Join-Path $Root 'web')
  try {
    npm ci --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) {
      Write-Host '    npm ci failed (platform lockfile drift) - retrying with npm install' -ForegroundColor Yellow
      npm install --no-audit --no-fund
      if ($LASTEXITCODE -ne 0) { throw "npm install failed (exit $LASTEXITCODE)" }
    }
    npm run build
    if ($LASTEXITCODE -ne 0) {
      Write-Host '    npm run build failed (tsc -b) - retrying with npx vite build' -ForegroundColor Yellow
      npx vite build
      if ($LASTEXITCODE -ne 0) { throw "vite build failed (exit $LASTEXITCODE)" }
    }
  } finally { Pop-Location }

  Step "Frontend: copy web\dist -> $stagingDir\wwwroot"
  $www = Join-Path $stagingDir 'wwwroot'
  if (Test-Path $www) { Remove-Item -Recurse -Force $www }
  New-Item -ItemType Directory -Force -Path $www | Out-Null
  Copy-Item -Recurse -Force (Join-Path $Root 'web\dist\*') $www
}

Step "Staging build ready: $stagingDir"

if (-not $Swap) {
  Write-Host 'Put it live with:  .\_deploy\task\build.ps1 -SkipBackend -SkipFrontend -Swap' -ForegroundColor Green
  exit 0
}

Step 'Swap: staging -> publish\api'
if (-not (Test-Path $runDir)) { New-Item -ItemType Directory -Force -Path $runDir | Out-Null }
(Get-Date).ToString('o') | Out-File -FilePath $flag -Encoding ascii -Force   # pause the watchdog
try {
  $task = Get-ScheduledTask -TaskName 'CoverageManager-Api' -ErrorAction SilentlyContinue
  $taskStopped = $false
  if ($task -and $task.State -eq 'Running') {
    Write-Host '    stopping CoverageManager-Api'
    try {
      Stop-ScheduledTask -TaskName 'CoverageManager-Api' -ErrorAction Stop
      $taskStopped = $true
    } catch {
      # No rights on the task: kill the process below; Task Scheduler's
      # restart-on-failure relaunches the API from the swapped folder within a minute.
      Write-Host "    Stop-ScheduledTask denied ($($_.Exception.Message.Trim())) - killing the API process instead; Task Scheduler restarts it within ~1 min" -ForegroundColor Yellow
    }
    Start-Sleep -Seconds 3
  }
  # Kill any API process still running from THIS deployment (path check keeps
  # other installs on the box untouched).
  Get-Process -Name 'CoverageManager.Api' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($publishDir, [StringComparison]::OrdinalIgnoreCase) } |
    Stop-Process -Force -ErrorAction SilentlyContinue
  Start-Sleep -Seconds 1

  if (Test-Path $publishDir) { Rename-Item $publishDir $oldDir }
  Rename-Item $stagingDir $publishDir
  Write-Host "    live: $publishDir  (previous: $oldDir)"

  Get-ChildItem (Join-Path $Root 'publish') -Directory -Filter 'api-old-*' -ErrorAction SilentlyContinue |
    Sort-Object Name -Descending | Select-Object -Skip $KeepOld |
    ForEach-Object { Write-Host "    pruning $($_.Name)"; Remove-Item -Recurse -Force $_.FullName -ErrorAction SilentlyContinue }

  if (-not $task) {
    Write-Host '    CoverageManager-Api task not registered yet - run register-tasks.ps1 (elevated)' -ForegroundColor Yellow
  } elseif ($taskStopped -or $task.State -ne 'Running') {
    Write-Host '    starting CoverageManager-Api'
    try { Start-ScheduledTask -TaskName 'CoverageManager-Api' -ErrorAction Stop }
    catch { Write-Host "    Start-ScheduledTask failed ($($_.Exception.Message.Trim())) - the watchdog or Task Scheduler restart-on-failure will start it" -ForegroundColor Yellow }
  } else {
    Write-Host '    API process killed; Task Scheduler restart-on-failure will relaunch it from the new folder'
  }
} finally {
  Remove-Item $flag -Force -ErrorAction SilentlyContinue
}

Write-Host ''
Write-Host 'Verify:   Invoke-WebRequest http://127.0.0.1:5000/api/exposure/status -UseBasicParsing | Select StatusCode' -ForegroundColor Gray
Write-Host "Rollback: Stop-ScheduledTask CoverageManager-Api; Rename-Item '$publishDir' '$publishDir-FAILED-$ts'; Rename-Item '$oldDir' '$publishDir'; Start-ScheduledTask CoverageManager-Api" -ForegroundColor Yellow
