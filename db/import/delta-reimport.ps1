<#
.SYNOPSIS
  ONE command: delta re-import from live v1 into coverage_v2, then verify.

.DESCRIPTION
  Run this immediately before the parallel run, with v1's writers FROZEN. It re-runs the
  full import and then the verifier, so:
    * config-exact tables are re-mirrored (they are live-mutating in v1 -- trading_accounts
      rewrites balance/equity continuously -- so only a frozen source can match exactly);
    * archive tables (deals, bridge_executions, trade_audit_log) pick up everything written
      since the last pass. Loads are ON CONFLICT DO NOTHING, so this is idempotent: running
      it twice, or after an interruption, changes nothing already imported.

  Defaults are the values the 2026-09-13 full import actually ran with, including
  -ChunkSpan 800000 (v1's deal_id spans ~32M IDs for ~2M rows; the manifest's 50k span
  would issue ~639 near-empty round trips at the live database).

  WHY FREEZE FIRST: while v1 writes, src and tgt legitimately differ and verify cannot go
  clean -- measured drift was ~36 bridge rows/min and new deals whenever markets are open.
  A non-clean verify against a LIVE v1 is expected and is not a defect.

.PARAMETER SkipVerify
  Import only. Use when you intend to run verify separately.

.EXAMPLE
  .\delta-reimport.ps1
  .\delta-reimport.ps1 -PaceMs 0        # fastest; only sensible once v1 is frozen

.NOTES
  READ-ONLY on v1 -- this never writes to the source. Pure ASCII (CLAUDE.md cp1252 hazard).
#>
[CmdletBinding()]
param(
  [string]$SourcePasswordFile = 'C:\ProgramData\CoverageManagerV2\secrets\v1_supabase_db.txt',
  [string]$TargetPasswordFile = 'C:\ProgramData\CoverageManagerV2\secrets\pg_app.txt',
  [string]$SourceConn = 'host=db.svhmhcqopkdgccnzgvzp.supabase.co port=5432 dbname=postgres user=postgres sslmode=require connect_timeout=30',
  [string]$TargetConn = 'host=127.0.0.1 port=5432 dbname=coverage_v2 user=coverage_app',
  [int64]$ChunkSpan = 800000,
  [int]$PaceMs = 200,
  [switch]$SkipVerify
)

$ErrorActionPreference = 'Continue'
$here = $PSScriptRoot
$start = Get-Date

Write-Host "=============================================================="
Write-Host " v2 DELTA RE-IMPORT   (run with v1 writers FROZEN)"
Write-Host " started : $($start.ToUniversalTime().ToString('u'))"
Write-Host " source  : v1 Supabase, DIRECT Postgres, READ-ONLY"
Write-Host " target  : $TargetConn"
Write-Host " chunking: span $ChunkSpan, pace ${PaceMs}ms"
Write-Host "=============================================================="
Write-Host ""

foreach ($f in @($SourcePasswordFile, $TargetPasswordFile)) {
  if (-not (Test-Path $f)) { Write-Error "Missing password file: $f"; exit 2 }
}

& (Join-Path $here 'import.ps1') `
    -SourceConn $SourceConn -TargetConn $TargetConn `
    -SourcePasswordFile $SourcePasswordFile -TargetPasswordFile $TargetPasswordFile `
    -ChunkSpan $ChunkSpan -PaceMs $PaceMs
$importExit = $LASTEXITCODE
if ($importExit -ne 0) {
  Write-Host ""
  Write-Host "IMPORT FAILED (exit $importExit). Safe to re-run: loads are insert-missing."
  exit $importExit
}

if ($SkipVerify) {
  Write-Host ""
  Write-Host "Import done; verify skipped (-SkipVerify)."
  exit 0
}

Write-Host ""
Write-Host "-------------------------- VERIFY ----------------------------"
& (Join-Path $here 'verify.ps1') `
    -SourceConn $SourceConn -TargetConn $TargetConn `
    -SourcePasswordFile $SourcePasswordFile -TargetPasswordFile $TargetPasswordFile
$verifyExit = $LASTEXITCODE

$mins = ((Get-Date) - $start).TotalMinutes
Write-Host ""
Write-Host "=============================================================="
Write-Host (" finished: {0}   elapsed {1:N1} min" -f ((Get-Date).ToUniversalTime().ToString('u')), $mins)
if ($verifyExit -eq 0) {
  Write-Host " RESULT  : CLEAN - every table reconciles. Ready for the parallel run."
} else {
  Write-Host " RESULT  : verify reported differences."
  Write-Host "           If v1 writers are genuinely frozen this needs investigation."
  Write-Host "           If v1 is still LIVE this is expected drift, not a defect --"
  Write-Host "           freeze the writers and re-run."
}
Write-Host "=============================================================="
exit $verifyExit
