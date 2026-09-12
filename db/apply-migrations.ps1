<#
.SYNOPSIS
  Forward-only migration runner for the Coverage Manager v2 local PostgreSQL store.

.DESCRIPTION
  Applies every *.sql file in the migrations directory, in lexical (numeric) order,
  that has not already been applied to the target database. Each migration runs
  inside a single transaction (psql --single-transaction), and on success a row is
  recorded in schema_migrations (filename + sha256 + applied_at). Re-running is a
  no-op for already-applied files. Migrations are forward-only: this script never
  rolls back or "downs" a migration.

  A migration whose recorded sha256 no longer matches the file on disk is reported
  as DRIFT and the run aborts -- migrations are immutable once applied; author a new
  file instead of editing a shipped one.

  Connection details come from -ConnString, or from the individual -PgHost/-Port/
  -Db/-User params, or from the CMV2_PG_CONN environment variable. The password is
  read from PGPASSWORD (preferred) or -PasswordFile (a path to a file containing
  only the password). Passwords are never echoed and never passed on the command
  line.

.EXAMPLE
  $env:PGPASSWORD = (Get-Content C:\ProgramData\CoverageManagerV2\secrets\pg_app.txt -Raw).Trim()
  .\apply-migrations.ps1 -Db coverage_v2 -User coverage_app

.NOTES
  This file MUST stay pure ASCII (see CLAUDE.md: em-dash/cp1252 PowerShell hazard).
#>
[CmdletBinding()]
param(
  [string]$ConnString,
  [string]$PgHost = '127.0.0.1',
  [int]$Port = 5432,
  [string]$Database = 'coverage_v2',
  [string]$User = 'coverage_app',
  [string]$PasswordFile,
  [string]$MigrationsDir = (Join-Path $PSScriptRoot 'migrations'),
  [string]$PsqlPath,
  [switch]$DryRun
)

$ErrorActionPreference = 'Continue'  # rely on explicit exit-code checks (see CLAUDE.md)

function Resolve-Psql {
  param([string]$Explicit)
  if ($Explicit -and (Test-Path $Explicit)) { return $Explicit }
  $onPath = (Get-Command psql -ErrorAction SilentlyContinue)
  if ($onPath) { return $onPath.Source }
  $candidates = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue |
                Sort-Object FullName -Descending
  if ($candidates) { return $candidates[0].FullName }
  throw "psql not found. Pass -PsqlPath or add psql to PATH."
}

function Invoke-Psql {
  param([string]$Sql, [switch]$SingleTx, [switch]$TuplesOnly)
  $args = @('-v','ON_ERROR_STOP=1','-X','-q')
  if ($SingleTx)   { $args += '--single-transaction' }
  if ($TuplesOnly) { $args += @('-t','-A') }
  if ($script:ConnString) { $args += @('-d', $script:ConnString) }
  else { $args += @('-h', $PgHost, '-p', "$Port", '-d', $Database, '-U', $User) }
  $args += @('-c', $Sql)
  $out = & $script:Psql @args 2>&1
  return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($out -join "`n") }
}

function Invoke-PsqlFile {
  param([string]$Path)
  $args = @('-v','ON_ERROR_STOP=1','-X','-q','--single-transaction')
  if ($script:ConnString) { $args += @('-d', $script:ConnString) }
  else { $args += @('-h', $PgHost, '-p', "$Port", '-d', $Database, '-U', $User) }
  $args += @('-f', $Path)
  $out = & $script:Psql @args 2>&1
  return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($out -join "`n") }
}

# --- password wiring (never on the command line) ---
if ($PasswordFile) {
  if (-not (Test-Path $PasswordFile)) { throw "PasswordFile not found: $PasswordFile" }
  $env:PGPASSWORD = (Get-Content $PasswordFile -Raw).Trim()
}
if (-not $ConnString -and $env:CMV2_PG_CONN) { $ConnString = $env:CMV2_PG_CONN }
$script:ConnString = $ConnString
$script:Psql = Resolve-Psql -Explicit $PsqlPath

if (-not (Test-Path $MigrationsDir)) { throw "Migrations dir not found: $MigrationsDir" }

Write-Host "psql:       $script:Psql"
Write-Host "target:     $(if($ConnString){'(conn string)'}else{"$User@${PgHost}:$Port/$Database"})"
Write-Host "migrations: $MigrationsDir"
Write-Host ("dry-run:    {0}" -f [bool]$DryRun)
Write-Host ""

# --- ensure ledger table ---
$ledger = @"
CREATE TABLE IF NOT EXISTS schema_migrations (
  filename   text PRIMARY KEY,
  sha256     text NOT NULL,
  applied_at timestamptz NOT NULL DEFAULT now()
);
"@
$r = Invoke-Psql -Sql $ledger -SingleTx
if ($r.ExitCode -ne 0) { Write-Error "Failed to ensure schema_migrations:`n$($r.Output)"; exit 1 }

# --- read applied set ---
$r = Invoke-Psql -Sql "SELECT filename || '|' || sha256 FROM schema_migrations;" -TuplesOnly
if ($r.ExitCode -ne 0) { Write-Error "Failed to read schema_migrations:`n$($r.Output)"; exit 1 }
$applied = @{}
foreach ($line in ($r.Output -split "`n")) {
  $line = $line.Trim()
  if ($line) { $parts = $line -split '\|', 2; $applied[$parts[0]] = $parts[1] }
}

$files = Get-ChildItem -Path $MigrationsDir -Filter '*.sql' | Sort-Object Name
if (-not $files) { Write-Host "No migration files found."; exit 0 }

$pending = @()
foreach ($f in $files) {
  $sha = (Get-FileHash -Algorithm SHA256 -Path $f.FullName).Hash.ToLower()
  if ($applied.ContainsKey($f.Name)) {
    if ($applied[$f.Name] -ne $sha) {
      Write-Error "DRIFT: $($f.Name) was applied with a different checksum than the file on disk. Migrations are immutable; add a new migration instead of editing this one."
      exit 2
    }
    Write-Host ("  ok   {0}" -f $f.Name)
  } else {
    $pending += [pscustomobject]@{ File = $f; Sha = $sha }
    Write-Host ("  PEND {0}" -f $f.Name)
  }
}

if (-not $pending) { Write-Host "`nNothing to apply. Schema is up to date."; exit 0 }
if ($DryRun) { Write-Host "`nDry run: $($pending.Count) migration(s) would be applied."; exit 0 }

Write-Host ""
foreach ($p in $pending) {
  Write-Host ("Applying {0} ..." -f $p.File.Name)
  $res = Invoke-PsqlFile -Path $p.File.FullName
  if ($res.ExitCode -ne 0) {
    Write-Error "FAILED $($p.File.Name) (transaction rolled back):`n$($res.Output)"
    exit 1
  }
  $esc = $p.File.Name.Replace("'","''")
  $rec = Invoke-Psql -Sql "INSERT INTO schema_migrations(filename, sha256) VALUES ('$esc','$($p.Sha)');" -SingleTx
  if ($rec.ExitCode -ne 0) { Write-Error "Applied $($p.File.Name) but failed to record it:`n$($rec.Output)"; exit 1 }
  Write-Host ("  done {0}" -f $p.File.Name)
}
Write-Host "`nApplied $($pending.Count) migration(s)."
exit 0
