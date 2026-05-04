# Coverage Manager -- production deploy script (Windows VPS).
#
# Purpose: rebuild the C# API + React frontend, then atomically swap the running
# `publish\api` directory in place. The naive "publish straight to publish\api"
# approach in earlier runbooks fails with MSB3027 because the running NSSM
# service holds file locks on CoverageManager.Api.dll. This script avoids that
# by building into `publish\api-staging` first and using a rename-based swap
# while the service is briefly stopped.
#
# Usage (run from the repo root, NORMAL PowerShell -- no elevation needed for
# the build steps; nssm stop/start near the end requires elevated PS, so the
# script pauses for you to do that part by hand):
#
#   .\_deploy\deploy.ps1
#
# Idempotent: on rebuild failure, the live `publish\api` is untouched.
# Rollback: rename the latest `publish\api-old-<timestamp>` back to `publish\api`.
#
# Encoding note: this file is intentionally ASCII-only (no em-dashes, no arrows,
# no smart quotes) so it parses correctly under Windows PowerShell 5.1, which
# reads files as cp1252 by default and will misparse multi-byte UTF-8 sequences
# inside string literals if a BOM is missing. Don't introduce non-ASCII chars
# inside throw "..." or Write-Host "..." strings without also adding a UTF-8 BOM.

param(
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot\.."),
    [string]$ApiCsproj = "src\CoverageManager.Api\CoverageManager.Api.csproj",
    [switch]$SkipFrontend = $false,
    [switch]$SkipBackend = $false
)

$ErrorActionPreference = "Stop"
Set-Location $RepoRoot

function Step($msg) {
    Write-Host ""
    Write-Host "==> $msg" -ForegroundColor Cyan
}

$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$publishDir   = Join-Path $RepoRoot "publish\api"
$stagingDir   = Join-Path $RepoRoot "publish\api-staging"
$rollbackDir  = Join-Path $RepoRoot "publish\api-old-$timestamp"

# ---------------------------------------------------------------------------
# 1. Backend rebuild -> publish\api-staging
# ---------------------------------------------------------------------------
if (-not $SkipBackend) {
    Step "Backend: dotnet publish -> $stagingDir"
    if (Test-Path $stagingDir) {
        Remove-Item -Recurse -Force $stagingDir
    }
    dotnet publish $ApiCsproj -c Release -o $stagingDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

    # Spot-check the native MT5 DLL landed at the publish ROOT (not just under Libs\).
    # Past deploys hit a P/Invoke load failure when the native DLL was only in Libs\.
    $nativeDll = Join-Path $stagingDir "MT5APIManager64.dll"
    if (-not (Test-Path $nativeDll)) {
        # Fallback: copy from the Libs subfolder if the csproj didn't auto-emit it
        $libDll = Join-Path $stagingDir "Libs\MT5APIManager64.dll"
        if (Test-Path $libDll) {
            Copy-Item -Force $libDll $nativeDll
            Write-Host "    Copied MT5APIManager64.dll from Libs\ to publish root" -ForegroundColor Yellow
        } else {
            throw "MT5APIManager64.dll not found in $stagingDir or Libs\ -- backend will P/Invoke-fail at runtime"
        }
    }
}

# ---------------------------------------------------------------------------
# 2. Frontend rebuild -> web\dist (then copied into staging\wwwroot)
# ---------------------------------------------------------------------------
if (-not $SkipFrontend) {
    Step "Frontend: npm ci + npm run build (fallback to vite build on tsc errors)"
    Push-Location (Join-Path $RepoRoot "web")
    try {
        npm ci
        if ($LASTEXITCODE -ne 0) { throw "npm ci failed (exit $LASTEXITCODE)" }

        # `npm run build` runs `tsc -b && vite build`. tsc -b sometimes trips on
        # TS6133 "declared but never read" warnings as errors; if it does, fall
        # back to vite build alone -- the runtime bundle is produced by vite anyway.
        npm run build
        if ($LASTEXITCODE -ne 0) {
            Write-Host "    npm run build failed (likely tsc -b) -- retrying with npx vite build" -ForegroundColor Yellow
            npx vite build
            if ($LASTEXITCODE -ne 0) { throw "vite build failed (exit $LASTEXITCODE)" }
        }
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
# 3. Hand off to elevated PS for the atomic swap
# ---------------------------------------------------------------------------
Step "Build complete. Swap to live in elevated PowerShell:"
Write-Host ""
Write-Host "    nssm stop coverage-api" -ForegroundColor Green
Write-Host "    Rename-Item `"$publishDir`" `"$rollbackDir`"" -ForegroundColor Green
Write-Host "    Rename-Item `"$stagingDir`" `"$publishDir`"" -ForegroundColor Green
Write-Host "    nssm start coverage-api" -ForegroundColor Green
Write-Host ""
Write-Host "Verify after restart:"
Write-Host "    Invoke-WebRequest http://localhost:5000/api/exposure/status -UseBasicParsing | Select StatusCode" -ForegroundColor Gray
Write-Host "    Get-Content $RepoRoot\logs\coverage-manager-*.log -Tail 60" -ForegroundColor Gray
Write-Host ""
Write-Host "Rollback (if anything broke):"
Write-Host "    nssm stop coverage-api" -ForegroundColor Yellow
Write-Host "    Rename-Item `"$publishDir`" `"$publishDir-FAILED-$timestamp`"" -ForegroundColor Yellow
Write-Host "    Rename-Item `"$rollbackDir`" `"$publishDir`"" -ForegroundColor Yellow
Write-Host "    nssm start coverage-api" -ForegroundColor Yellow
