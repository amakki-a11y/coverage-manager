# Coverage Manager v2 -- build + stage script (separate from v1's deploy.ps1).
#
# docs/V2_PARALLEL_RUN.md, H3. v2 NEVER goes near v1's publish\api folder or the
# coverage-api service. This script builds the v2 API + React bundle into
#   C:\CoverageManagerV2\app-staging
# and then PRINTS (does not run) the elevated steps to swap it into
#   C:\CoverageManagerV2\app        (service coverage-api-v2, 127.0.0.1:5100).
# It never stops, starts or installs a service and never touches :5000.
#
# Usage (repo root, normal PowerShell):
#   .\_deploy\deploy-v2.ps1              # build + stage, print swap steps
#   .\_deploy\deploy-v2.ps1 -CheckOnly   # run the guards only, build nothing
#
# Guards (all refuse with exit 2 before anything is built):
#   - the checkout must be v2 (v2 marker files present);
#   - the app / staging folders must not be, or be inside, v1's publish folder
#     (<repo>\publish) or C:\CoverageManager\publish;
#   - the service name must not be coverage-api (v1).
#
# Encoding: ASCII only (Windows PowerShell 5.1 reads BOM-less files as cp1252).

param(
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot\.."),
    [string]$ApiCsproj = "src\CoverageManager.Api\CoverageManager.Api.csproj",
    [string]$AppDir = "C:\CoverageManagerV2\app",
    [string]$ServiceName = "coverage-api-v2",
    [switch]$SkipFrontend = $false,
    [switch]$SkipBackend = $false,
    [switch]$CheckOnly = $false
)

$ErrorActionPreference = "Continue"

function Step($msg) {
    Write-Host ""
    Write-Host "==> $msg" -ForegroundColor Cyan
}

function Refuse($msg) {
    Write-Host ""
    Write-Host "REFUSED: $msg" -ForegroundColor Red
    exit 2
}

function Normalize($p) {
    return ([System.IO.Path]::GetFullPath($p)).TrimEnd('\').ToLowerInvariant()
}

function IsSameOrInside($path, $root) {
    $a = Normalize $path
    $b = Normalize $root
    return ($a -eq $b) -or $a.StartsWith($b + '\')
}

# ---------------------------------------------------------------------------
# 0. Guards
# ---------------------------------------------------------------------------
$timestamp  = Get-Date -Format "yyyyMMddHHmmss"
$stagingDir = "$AppDir-staging"
$rollbackDir = "$AppDir-old-$timestamp"

$v2Marker = Join-Path $RepoRoot "src\CoverageManager.Api\Services\PostgresService.cs"
if (-not (Test-Path $v2Marker)) {
    Refuse "not a v2 checkout (missing $v2Marker). For v1 use _deploy\deploy.ps1 from the live branch."
}

$v1Roots = @((Join-Path $RepoRoot "publish"), "C:\CoverageManager\publish")
foreach ($target in @($AppDir, $stagingDir, $rollbackDir)) {
    foreach ($root in $v1Roots) {
        if (IsSameOrInside $target $root) {
            Refuse "target $target is inside v1's publish folder $root (v1 runs from publish\api)."
        }
    }
}

if ($ServiceName.Trim().ToLowerInvariant() -eq "coverage-api") {
    Refuse "service name coverage-api is live v1. v2 is $([char]39)coverage-api-v2$([char]39)."
}

Step "Guards passed: v2 checkout; app $AppDir; staging $stagingDir; service $ServiceName"
if ($CheckOnly) {
    Write-Host "    -CheckOnly: nothing built." -ForegroundColor Gray
    exit 0
}

Set-Location $RepoRoot

# ---------------------------------------------------------------------------
# 1. Backend -> staging
# ---------------------------------------------------------------------------
if (-not $SkipBackend) {
    Step "Backend: dotnet publish -> $stagingDir"
    if (Test-Path $stagingDir) { Remove-Item -Recurse -Force $stagingDir }
    dotnet publish $ApiCsproj -c Release -o $stagingDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }
}

# ---------------------------------------------------------------------------
# 2. Frontend -> staging\wwwroot (tsc must pass; no vite-only fallback for v2)
# ---------------------------------------------------------------------------
if (-not $SkipFrontend) {
    Step "Frontend: npm ci + npm run build"
    Push-Location (Join-Path $RepoRoot "web")
    try {
        npm ci --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) {
            Write-Host "    npm ci failed (likely platform-specific lockfile mismatch) -- retrying with npm install" -ForegroundColor Yellow
            npm install --no-audit --no-fund
            if ($LASTEXITCODE -ne 0) { throw "npm install failed (exit $LASTEXITCODE)" }
        }
        npm run build
        if ($LASTEXITCODE -ne 0) { throw "npm run build failed (exit $LASTEXITCODE)" }
    } finally {
        Pop-Location
    }

    Step "Frontend: copy dist\* into $stagingDir\wwwroot"
    $wwwroot = Join-Path $stagingDir "wwwroot"
    if (Test-Path $wwwroot) { Remove-Item -Recurse -Force $wwwroot }
    New-Item -ItemType Directory -Force -Path $wwwroot | Out-Null
    Copy-Item -Recurse -Force "$RepoRoot\web\dist\*" $wwwroot
}

# ---------------------------------------------------------------------------
# 3. Print the elevated swap (not executed here)
# ---------------------------------------------------------------------------
Step "Staged. Swap in elevated PowerShell (v2 only; v1 coverage-api is not involved):"
Write-Host ""
Write-Host "    nssm stop $ServiceName" -ForegroundColor Green
Write-Host "    if (Test-Path `"$AppDir`") { Rename-Item `"$AppDir`" `"$rollbackDir`" }" -ForegroundColor Green
Write-Host "    Rename-Item `"$stagingDir`" `"$AppDir`"" -ForegroundColor Green
Write-Host "    nssm start $ServiceName" -ForegroundColor Green
Write-Host ""
Write-Host "First install and the service environment: docs\V2_PARALLEL_RUN.md sections 3 and 4." -ForegroundColor Gray
Write-Host "Verify: Invoke-WebRequest http://127.0.0.1:5100/api/exposure/status -UseBasicParsing" -ForegroundColor Gray
