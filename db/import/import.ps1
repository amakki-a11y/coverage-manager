<#
.SYNOPSIS
  Import v1 (Supabase) data into the v2 local Postgres store. Repeatable.

.DESCRIPTION
  For every table in tables.psd1, in FK-safe order:
    1. Compute the column intersection between source and target (via
       information_schema). Columns only in the source are dropped (logged);
       columns only in the target keep their defaults (logged). Generated columns
       (is_generated <> 'NEVER') are always excluded on the target side.
    2. Extract those columns from the source to a local CSV (psql \copy, client-side
       -- so a remote Supabase source streams to the CM box).
    3. Load the CSV into a TEMP staging table on the target and upsert into the real
       table: config-exact -> ON CONFLICT (keys) DO UPDATE; archive-upsert ->
       ON CONFLICT DO NOTHING. Staging + upsert run in ONE target session so the
       temp table survives.

  Source and target are libpq connection strings (key=value or URI). Passwords come
  from -SourcePasswordFile / -TargetPasswordFile (set into PGPASSWORD per psql call)
  or from a PGPASSWORD already in the environment.

  This tool never drops tables or databases. config-exact tables are REPLACED from
  source (TRUNCATE + load = authoritative mirror), intended for the pre-cutover import
  when the v2 target has no dealer-entered data yet; archive tables are insert-missing.
  It never runs the real import on its own -- Phase 3 runs it against the live Supabase
  source with explicit parameters.

.EXAMPLE
  .\import.ps1 `
    -SourceConn "host=127.0.0.1 port=5432 dbname=cmv2_src_fixture user=postgres" `
    -TargetConn "host=127.0.0.1 port=5432 dbname=coverage_v2 user=postgres" `
    -SourcePasswordFile $pf -TargetPasswordFile $pf

.NOTES
  Pure ASCII (CLAUDE.md em-dash/cp1252 hazard).
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$SourceConn,
  [Parameter(Mandatory=$true)][string]$TargetConn,
  [string]$SourcePasswordFile,
  [string]$TargetPasswordFile,
  [string]$ManifestPath = (Join-Path $PSScriptRoot 'tables.psd1'),
  [string]$WorkDir = (Join-Path $PSScriptRoot '_dumps'),
  [string[]]$Only,
  [string]$PsqlPath,
  [switch]$DryRun
)

$ErrorActionPreference = 'Continue'

function Resolve-Psql {
  param([string]$Explicit)
  if ($Explicit -and (Test-Path $Explicit)) { return $Explicit }
  $onPath = (Get-Command psql -ErrorAction SilentlyContinue)
  if ($onPath) { return $onPath.Source }
  $c = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue | Sort-Object FullName -Descending
  if ($c) { return $c[0].FullName }
  throw "psql not found. Pass -PsqlPath."
}
$script:Psql = Resolve-Psql -Explicit $PsqlPath

$script:SrcPw = if ($SourcePasswordFile) { (Get-Content $SourcePasswordFile -Raw).Trim() } else { $env:PGPASSWORD }
$script:TgtPw = if ($TargetPasswordFile) { (Get-Content $TargetPasswordFile -Raw).Trim() } else { $env:PGPASSWORD }

function Invoke-PsqlConn {
  param([string]$Conn, [string]$Pw, [string]$Sql, [string]$File, [switch]$TuplesOnly)
  $env:PGPASSWORD = $Pw
  $a = @('-v','ON_ERROR_STOP=1','-X','-q','-d',$Conn)
  if ($TuplesOnly) { $a += @('-t','-A') }
  if ($File) { $a += @('-f',$File) } else { $a += @('-c',$Sql) }
  $out = & $script:Psql @a 2>&1
  return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($out -join "`n") }
}

function Get-Columns {
  param([string]$Conn, [string]$Pw, [string]$Table, [switch]$InsertableOnly)
  $filter = if ($InsertableOnly) { "AND is_generated = 'NEVER'" } else { "" }
  $sql = "SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='$Table' $filter ORDER BY ordinal_position;"
  $r = Invoke-PsqlConn -Conn $Conn -Pw $Pw -Sql $sql -TuplesOnly
  if ($r.ExitCode -ne 0) { throw "column introspection failed for $Table`n$($r.Output)" }
  return @($r.Output -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}

if (-not (Test-Path $ManifestPath)) { throw "Manifest not found: $ManifestPath" }
$manifest = Import-PowerShellDataFile -Path $ManifestPath
$tables = $manifest.Tables | Sort-Object { [int]$_.Order }
if ($Only) { $tables = $tables | Where-Object { $Only -contains $_.Name } }

New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null
Write-Host "psql:   $script:Psql"
Write-Host "source: $SourceConn"
Write-Host "target: $TargetConn"
Write-Host "tables: $($tables.Count)   dry-run: $([bool]$DryRun)"
Write-Host ""

$failures = 0
foreach ($t in $tables) {
  $name = $t.Name
  Write-Host "== $name ($($t.Mode)) =="
  try {
    $srcCols = Get-Columns -Conn $SourceConn -Pw $script:SrcPw -Table $name
    if (-not $srcCols) { Write-Host "  source has no such table -- skipping."; continue }
    $tgtCols = Get-Columns -Conn $TargetConn -Pw $script:TgtPw -Table $name -InsertableOnly
    if (-not $tgtCols) { throw "target table $name missing (run migrations first)" }

    $cols = @($srcCols | Where-Object { $tgtCols -contains $_ })
    if (-not $cols) { throw "no common insertable columns for $name" }
    $srcOnly = @($srcCols | Where-Object { $tgtCols -notcontains $_ })
    $tgtOnly = @($tgtCols | Where-Object { $srcCols -notcontains $_ })
    if ($srcOnly) { Write-Host "  drop (source-only): $($srcOnly -join ', ')" }
    if ($tgtOnly) { Write-Host "  default (target-only): $($tgtOnly -join ', ')" }

    $colList = ($cols | ForEach-Object { "`"$_`"" }) -join ', '

    if ($DryRun) { Write-Host "  would import columns: $colList"; continue }

    # 1. extract source -> CSV (client-side copy)
    $csv = Join-Path $WorkDir "$name.csv"
    if (Test-Path $csv) { Remove-Item $csv -Force }
    $copyOut = "\copy (SELECT $colList FROM public.`"$name`") TO '$($csv -replace '\\','/')' WITH (FORMAT csv, HEADER true)"
    $r = Invoke-PsqlConn -Conn $SourceConn -Pw $script:SrcPw -Sql $copyOut
    if ($r.ExitCode -ne 0) { throw "source extract failed:`n$($r.Output)" }

    # 2. load in one target session (temp table must survive the \copy).
    #    config-exact  -> authoritative mirror: TRUNCATE ... CASCADE then INSERT, so
    #                     migration-seeded rows (e.g. snapshot_schedules, which carry
    #                     fresh UUIDs that would otherwise duplicate v1's rows) are
    #                     replaced, giving an exact source==target match. Atomic:
    #                     ON_ERROR_STOP + implicit single statement-file transaction.
    #    archive-upsert -> insert-missing: INSERT ... ON CONFLICT DO NOTHING (never
    #                     clobbers a row the live feed may already have written).
    $csvFwd = $csv -replace '\\','/'
    if ($t.Mode -eq 'config-exact') {
      $mutate = "TRUNCATE TABLE public.`"$name`" RESTART IDENTITY CASCADE;`nINSERT INTO public.`"$name`" ($colList) SELECT $colList FROM _stg;"
    } else {
      $mutate = "INSERT INTO public.`"$name`" ($colList) SELECT $colList FROM _stg ON CONFLICT DO NOTHING;"
    }
    $script = @"
BEGIN;
CREATE TEMP TABLE _stg AS SELECT $colList FROM public."$name" WITH NO DATA;
\copy _stg ($colList) FROM '$csvFwd' WITH (FORMAT csv, HEADER true)
$mutate
COMMIT;
"@
    $scriptFile = Join-Path $WorkDir "$name.load.sql"
    Set-Content -Path $scriptFile -Value $script -Encoding ascii
    $r = Invoke-PsqlConn -Conn $TargetConn -Pw $script:TgtPw -File $scriptFile
    if ($r.ExitCode -ne 0) { throw "target load failed:`n$($r.Output)" }
    Write-Host "  loaded."
  }
  catch {
    Write-Host "  FAILED: $($_.Exception.Message)"
    $failures++
  }
}

Write-Host ""
if ($failures -gt 0) { Write-Host "$failures table(s) failed."; exit 1 }
Write-Host "Import pass complete. Run verify.ps1 next."
exit 0
