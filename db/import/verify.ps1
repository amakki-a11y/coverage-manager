<#
.SYNOPSIS
  Verify a v1 -> v2 import: row counts, numeric aggregates, and (for config tables)
  a column-level checksum, comparing source and target.

.DESCRIPTION
  Reads the same tables.psd1 manifest as import.ps1 and runs each table's declared
  checks:
    count       : config-exact requires source == target; archive requires
                  target >= source (the live feed may have added rows).
    sum:<col>   : compares round(sum(col),4). config-exact: equal; archive: target>=source.
    checksum    : (config-exact only) md5 over the intersection columns, ordered by the
                  conflict keys, must match exactly -- byte-level fidelity of shared cols.

  Exit code 0 = all checks passed, non-zero = at least one FAIL.

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
  [string[]]$Only,
  [string]$PsqlPath,
  # Must match the value import.ps1 ran with. Default comes from the manifest.
  [int]$RetentionMonths = -1
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

function Q {
  param([string]$Conn, [string]$Pw, [string]$Sql)
  $env:PGPASSWORD = $Pw
  $out = & $script:Psql @('-v','ON_ERROR_STOP=1','-X','-q','-t','-A','-d',$Conn,'-c',$Sql) 2>&1
  return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Value = ($out -join "`n").Trim() }
}
function Cols {
  param([string]$Conn, [string]$Pw, [string]$Table, [switch]$InsertableOnly)
  $filter = if ($InsertableOnly) { "AND is_generated='NEVER'" } else { "" }
  $r = Q -Conn $Conn -Pw $Pw -Sql "SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='$Table' $filter ORDER BY ordinal_position;"
  if ($r.ExitCode -ne 0) { throw "column introspection failed for $Table" }
  return @($r.Value -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}

$manifest = Import-PowerShellDataFile -Path $ManifestPath
$tables = $manifest.Tables | Sort-Object { [int]$_.Order }
if ($Only) { $tables = $tables | Where-Object { $Only -contains $_.Name } }

# Same rolling-retention cutoff import.ps1 uses (V2_PLAN 5.5). Applied to BOTH source
# and target so a windowed import is compared apples-to-apples instead of a windowed
# target being reported as "missing" rows against a full-history source.
if ($RetentionMonths -lt 0) {
  $RetentionMonths = if ($null -ne $manifest.RetentionMonths) { [int]$manifest.RetentionMonths } else { 0 }
}
$cutoffSql = "(date_trunc('day', (now() AT TIME ZONE 'UTC')) - interval '$RetentionMonths months') AT TIME ZONE 'UTC'"
function WindowWhere {
  # Must mirror import.ps1 exactly: retention window AND any RowFilter, applied to BOTH
  # sides. Otherwise a deliberately-filtered import reads as "missing rows" against an
  # unfiltered source.
  param($t)
  $preds = @()
  if ($RetentionMonths -gt 0 -and $t.WindowColumn) { $preds += "`"$($t.WindowColumn)`" >= $cutoffSql" }
  if ($t.RowFilter) { $preds += "($($t.RowFilter))" }
  if ($preds.Count -gt 0) { return " WHERE " + ($preds -join ' AND ') }
  return ''
}
Write-Host ("retention: {0}" -f $(if ($RetentionMonths -gt 0) { "rolling $RetentionMonths months on windowed tables" } else { "DISABLED (full history)" }))

$pass = 0; $fail = 0
$results = @()
function Record { param($Table,$Check,$Ok,$Detail)
  $script:results += [pscustomobject]@{ Table=$Table; Check=$Check; Result=$(if($Ok){'PASS'}else{'FAIL'}); Detail=$Detail }
  if ($Ok) { $script:pass++ } else { $script:fail++ }
}

foreach ($t in $tables) {
  $name = $t.Name
  $archive = ($t.Mode -eq 'archive-upsert')

  $srcExists = (Q -Conn $SourceConn -Pw $script:SrcPw -Sql "SELECT to_regclass('public.$name') IS NOT NULL;").Value
  if ($srcExists -ne 't') { Record $name 'exists' $false "source table missing"; continue }

  $w = WindowWhere $t
  $wLabel = if ($w) { " [windowed]" } else { "" }

  # ONE aggregate scan per side for count + every sum. v1 is a live production database;
  # running a separate full scan per check (5 on deals) would multiply that load for no
  # benefit, so they are combined into a single pipe-delimited row.
  $sumCols = @($t.Checks | Where-Object { $_ -like 'sum:*' } | ForEach-Object { $_.Substring(4) })
  $exprs = @("count(*)::text") + @($sumCols | ForEach-Object { "COALESCE(round(sum(`"$_`")::numeric,4),0)::text" })
  $aggSql = "SELECT " + ($exprs -join " || '|' || ") + " FROM public.`"$name`"$w;"
  $sAgg = (Q -Conn $SourceConn -Pw $script:SrcPw -Sql $aggSql).Value -split '\|'
  $dAgg = (Q -Conn $TargetConn -Pw $script:TgtPw -Sql $aggSql).Value -split '\|'

  $sCount = [int64]$sAgg[0]; $dCount = [int64]$dAgg[0]
  $okCount = if ($archive) { $dCount -ge $sCount } else { $dCount -eq $sCount }
  Record $name 'count' $okCount "src=$sCount tgt=$dCount$(if($archive){' (>=)'})$wLabel"

  for ($i = 0; $i -lt $sumCols.Count; $i++) {
    $sv = [decimal]$sAgg[$i + 1]; $dv = [decimal]$dAgg[$i + 1]
    $okSum = if ($archive) { $dv -ge $sv } else { $dv -eq $sv }
    Record $name "sum:$($sumCols[$i])" $okSum "src=$sv tgt=$dv$wLabel"
  }

  foreach ($check in @($t.Checks)) {
    if ($check -eq 'checksum') {
      if ($archive) { continue }
      $sc = Cols -Conn $SourceConn -Pw $script:SrcPw -Table $name
      $tc = Cols -Conn $TargetConn -Pw $script:TgtPw -Table $name -InsertableOnly
      $cols = @($sc | Where-Object { $tc -contains $_ })
      $concat = ($cols | ForEach-Object { "COALESCE(`"$_`"::text,'~')" }) -join ",'|',"
      $order  = ($t.Keys | ForEach-Object { "`"$_`"" }) -join ', '
      $sql = "SELECT md5(COALESCE(string_agg(concat_ws('|',$concat), E'\n' ORDER BY $order),'')) FROM public.`"$name`"$w;"
      $s = (Q -Conn $SourceConn -Pw $script:SrcPw -Sql $sql).Value
      $d = (Q -Conn $TargetConn -Pw $script:TgtPw -Sql $sql).Value
      Record $name 'checksum' ($s -eq $d) "$($s.Substring(0,[Math]::Min(8,$s.Length)))..vs..$($d.Substring(0,[Math]::Min(8,$d.Length)))"
    }
  }
}

Write-Host ""
Write-Host (($results | Format-Table -AutoSize | Out-String).TrimEnd())
Write-Host ""
Write-Host "VERIFY: $pass passed, $fail failed."
if ($fail -gt 0) { exit 1 }
exit 0
