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
  # Rolling retention window in months for tables declaring a WindowColumn.
  # Default comes from the manifest (RetentionMonths). 0 disables windowing.
  [int]$RetentionMonths = -1,
  # Chunked-read tuning for the live source. Override the manifest per run:
  #   -ChunkSpan  key-range width per extract (deal_id span), default from manifest
  #   -PaceMs     pause between chunks, default from manifest
  [int64]$ChunkSpan = 0,
  [int]$PaceMs = -1,
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

# Reads a password file defensively: strips a UTF-8/UTF-16 BOM (an editor-saved file
# carries one, and it becomes an invisible leading character that surfaces only as a
# baffling "password authentication failed") plus surrounding whitespace/newlines.
function Read-PasswordFile {
  param([string]$Path)
  if (-not (Test-Path $Path)) { throw "Password file not found: $Path" }
  $s = Get-Content $Path -Raw
  if ($null -eq $s) { return '' }
  # char codes, not literals: this file must stay pure ASCII (see CLAUDE.md).
  return $s.TrimStart([char]0xFEFF, [char]0xFFFE).Trim()
}

$script:SrcPw = if ($SourcePasswordFile) { Read-PasswordFile $SourcePasswordFile } else { $env:PGPASSWORD }
$script:TgtPw = if ($TargetPasswordFile) { Read-PasswordFile $TargetPasswordFile } else { $env:PGPASSWORD }

function Invoke-PsqlConn {
  # -Chatty omits psql's -q so command tags (notably "COPY n") reach stdout; the chunked
  # reader parses that to count rows. With -q they are suppressed and every count reads 0.
  param([string]$Conn, [string]$Pw, [string]$Sql, [string]$File, [switch]$TuplesOnly, [switch]$Chatty)
  $env:PGPASSWORD = $Pw
  $a = @('-v','ON_ERROR_STOP=1','-X','-d',$Conn)
  if (-not $Chatty) { $a += '-q' }
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

# Retention window (V2_PLAN 5.5). UTC-day-stable so import and a later verify on the
# same UTC day compute an identical cutoff on both servers regardless of session TZ.
if ($RetentionMonths -lt 0) {
  $RetentionMonths = if ($null -ne $manifest.RetentionMonths) { [int]$manifest.RetentionMonths } else { 0 }
}
$cutoffSql = "date_trunc('day', (now() AT TIME ZONE 'UTC')) AT TIME ZONE 'UTC' - interval '$RetentionMonths months'"

New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null
Write-Host "psql:   $script:Psql"
Write-Host "source: $SourceConn"
Write-Host "target: $TargetConn"
Write-Host "tables: $($tables.Count)   dry-run: $([bool]$DryRun)"
Write-Host ("retention: {0}" -f $(if ($RetentionMonths -gt 0) { "rolling $RetentionMonths months on windowed tables" } else { "DISABLED (full history)" }))
Write-Host ""

# Loads one CSV into the target in a single psql session (the TEMP staging table has to
# survive the \copy, so extract and load cannot be separate sessions).
#   config-exact   -> authoritative mirror: TRUNCATE ... CASCADE then INSERT, so
#                     migration-seeded rows (e.g. snapshot_schedules, whose fresh UUIDs
#                     would otherwise duplicate v1's) are replaced and source == target.
#   archive-upsert -> insert-missing: ON CONFLICT DO NOTHING, so re-running a chunk (or a
#                     whole interrupted import) never clobbers rows and is idempotent.
function Load-Csv {
  param([string]$Table, [string]$ColList, [string]$CsvPath, [string]$Mode)
  $csvFwd = $CsvPath -replace '\\','/'
  if ($Mode -eq 'config-exact') {
    $mutate = "TRUNCATE TABLE public.`"$Table`" RESTART IDENTITY CASCADE;`nINSERT INTO public.`"$Table`" ($ColList) SELECT $ColList FROM _stg;"
  } else {
    $mutate = "INSERT INTO public.`"$Table`" ($ColList) SELECT $ColList FROM _stg ON CONFLICT DO NOTHING;"
  }
  $sql = @"
BEGIN;
CREATE TEMP TABLE _stg AS SELECT $ColList FROM public."$Table" WITH NO DATA;
\copy _stg ($ColList) FROM '$csvFwd' WITH (FORMAT csv, HEADER true)
$mutate
COMMIT;
"@
  $file = Join-Path $script:WorkDirResolved "$Table.load.sql"
  Set-Content -Path $file -Value $sql -Encoding ascii
  $res = Invoke-PsqlConn -Conn $script:TargetConnResolved -Pw $script:TgtPw -File $file
  if ($res.ExitCode -ne 0) { throw "target load failed:`n$($res.Output)" }
}

$script:WorkDirResolved    = $WorkDir
$script:TargetConnResolved = $TargetConn
$script:Summary = @()
$runStart = Get-Date

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

    # Predicates: the rolling-retention window (tables declaring a WindowColumn) and an
    # optional RowFilter that excludes rows we deliberately do not carry. Both are applied
    # to the source extract here and to BOTH sides in verify.ps1, so counts stay comparable.
    $preds = @()
    if ($RetentionMonths -gt 0 -and $t.WindowColumn) {
      if ($srcCols -notcontains $t.WindowColumn) { throw "WindowColumn '$($t.WindowColumn)' not present on source table $name" }
      $preds += "`"$($t.WindowColumn)`" >= $cutoffSql"
      Write-Host "  window: $($t.WindowColumn) >= now() - $RetentionMonths months (rolling retention)"
    }
    if ($t.RowFilter) {
      $preds += "($($t.RowFilter))"
      Write-Host "  row filter: $($t.RowFilter)"
    }
    $where = if ($preds.Count -gt 0) { " WHERE " + ($preds -join ' AND ') } else { '' }

    if ($DryRun) { Write-Host "  would import columns: $colList$where"; continue }

    $tableStart = Get-Date
    $rows = 0

    if ($t.ChunkColumn -and $t.Mode -eq 'archive-upsert') {
      # ---- chunked + paced read (large history tables) -------------------------
      # Never issue one giant SELECT against the live v1 database: walk the table in
      # key ranges over ChunkColumn (deal_id), extracting and loading one range at a
      # time with a pause between ranges. Idempotent (ON CONFLICT DO NOTHING), so an
      # interrupted run is resumed simply by running it again.
      if ($srcCols -notcontains $t.ChunkColumn) { throw "ChunkColumn '$($t.ChunkColumn)' not on source table $name" }
      $span  = if ($ChunkSpan -gt 0) { $ChunkSpan } elseif ($t.ChunkSpan) { [int64]$t.ChunkSpan } else { 50000 }
      $pace  = if ($PaceMs -ge 0) { $PaceMs } elseif ($null -ne $t.PaceMs) { [int]$t.PaceMs } else { 200 }
      $ck    = "`"$($t.ChunkColumn)`""

      $b = Invoke-PsqlConn -Conn $SourceConn -Pw $script:SrcPw -TuplesOnly `
            -Sql "SELECT COALESCE(min($ck),0)::text || '|' || COALESCE(max($ck),-1)::text FROM public.`"$name`"$where;"
      if ($b.ExitCode -ne 0) { throw "bounds probe failed:`n$($b.Output)" }
      $parts = ($b.Output.Trim() -split '\|')
      $lo = [int64]$parts[0]; $hi = [int64]$parts[1]
      if ($hi -lt $lo) { Write-Host "  no rows in window."; continue }
      $totalChunks = [math]::Ceiling(($hi - $lo + 1) / $span)
      Write-Host ("  chunked read: {0} in [{1}..{2}], span {3}, pace {4}ms -> {5} chunk(s)" -f $t.ChunkColumn, $lo, $hi, $span, $pace, $totalChunks)

      $csv = Join-Path $WorkDir "$name.chunk.csv"
      $n = 0
      for ($start = $lo; $start -le $hi; $start += $span) {
        $end = $start + $span
        $n++
        if (Test-Path $csv) { Remove-Item $csv -Force }
        $pred = if ($where) { "$where AND $ck >= $start AND $ck < $end" } else { " WHERE $ck >= $start AND $ck < $end" }
        $r = Invoke-PsqlConn -Conn $SourceConn -Pw $script:SrcPw -Chatty `
              -Sql "\copy (SELECT $colList FROM public.`"$name`"$pred) TO '$($csv -replace '\\','/')' WITH (FORMAT csv, HEADER true)"
        if ($r.ExitCode -ne 0) { throw "chunk $n extract failed:`n$($r.Output)" }
        # -1 = count unknown (unexpected psql output); load anyway rather than skip data.
        $got = -1
        if ($r.Output -match 'COPY\s+(\d+)') { $got = [int64]$matches[1] }
        if ($got -ne 0) {
          Load-Csv -Table $name -ColList $colList -CsvPath $csv -Mode $t.Mode
          if ($got -gt 0) { $rows += $got }
        }
        if ($n % 10 -eq 0 -or $start + $span -gt $hi) {
          $el = ((Get-Date) - $tableStart).TotalSeconds
          Write-Host ("    chunk {0}/{1}  rows so far {2:N0}  {3:N0}s  ({4:N0} rows/s)" -f $n, $totalChunks, $rows, $el, $(if($el -gt 0){$rows/$el}else{0}))
        }
        if ($pace -gt 0 -and $start + $span -le $hi) { Start-Sleep -Milliseconds $pace }
      }
    }
    else {
      # ---- single-shot (small config / history tables) -------------------------
      $csv = Join-Path $WorkDir "$name.csv"
      if (Test-Path $csv) { Remove-Item $csv -Force }
      $r = Invoke-PsqlConn -Conn $SourceConn -Pw $script:SrcPw -Chatty `
            -Sql "\copy (SELECT $colList FROM public.`"$name`"$where) TO '$($csv -replace '\\','/')' WITH (FORMAT csv, HEADER true)"
      if ($r.ExitCode -ne 0) { throw "source extract failed:`n$($r.Output)" }
      if ($r.Output -match 'COPY\s+(\d+)') { $rows = [int64]$matches[1] }
      Load-Csv -Table $name -ColList $colList -CsvPath $csv -Mode $t.Mode
    }

    $secs = ((Get-Date) - $tableStart).TotalSeconds
    Write-Host ("  loaded {0:N0} row(s) in {1:N1}s" -f $rows, $secs)
    $script:Summary += [pscustomobject]@{ Table = $name; Rows = $rows; Seconds = [math]::Round($secs,1) }
  }
  catch {
    Write-Host "  FAILED: $($_.Exception.Message)"
    $failures++
  }
}

Write-Host ""
Write-Host (($script:Summary | Format-Table -AutoSize | Out-String).TrimEnd())
Write-Host ""
Write-Host ("TOTAL: {0:N0} rows in {1:N1}s" -f ($script:Summary | Measure-Object Rows -Sum).Sum, ((Get-Date) - $runStart).TotalSeconds)
if ($failures -gt 0) { Write-Host "$failures table(s) failed."; exit 1 }
Write-Host "Import pass complete. Run verify.ps1 next."
exit 0
