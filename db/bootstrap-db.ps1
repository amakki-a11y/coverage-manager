<#
.SYNOPSIS
  Create the Coverage Manager v2 application role and database (idempotent).

.DESCRIPTION
  Connects to the local PostgreSQL as the superuser and ensures:
    * a login role (default: coverage_app) with a password read from a file, and
    * a database (default: coverage_v2) owned by that role.
  Safe to re-run: existing role/database are left as-is (the role's password is
  reset to the file's value on every run so the app config and DB stay in sync).

  This does NOT run migrations -- run db/apply-migrations.ps1 afterwards.

.EXAMPLE
  .\bootstrap-db.ps1 `
     -SuperPasswordFile C:\ProgramData\CoverageManagerV2\secrets\pg_superuser.txt `
     -AppPasswordFile   C:\ProgramData\CoverageManagerV2\secrets\pg_app.txt

.NOTES
  Pure ASCII (CLAUDE.md em-dash/cp1252 hazard). Passwords are never echoed.
#>
[CmdletBinding()]
param(
  [string]$PgHost = '127.0.0.1',
  [int]$Port = 5432,
  [string]$SuperUser = 'postgres',
  [Parameter(Mandatory=$true)][string]$SuperPasswordFile,
  [string]$AppUser = 'coverage_app',
  [Parameter(Mandatory=$true)][string]$AppPasswordFile,
  [string]$Database = 'coverage_v2',
  [string]$PsqlPath
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

foreach ($f in @($SuperPasswordFile, $AppPasswordFile)) {
  if (-not (Test-Path $f)) { throw "Password file not found: $f" }
}
$psql   = Resolve-Psql -Explicit $PsqlPath
$appPw  = (Get-Content $AppPasswordFile -Raw).Trim()
$env:PGPASSWORD = (Get-Content $SuperPasswordFile -Raw).Trim()

# App password is generated as [A-Za-z0-9] so it is safe in a single-quoted literal.
if ($appPw -notmatch '^[A-Za-z0-9]+$') {
  throw "App password must be alphanumeric for safe SQL embedding (regenerate it)."
}

function Psql-Postgres {
  param([string]$Sql, [switch]$TuplesOnly)
  $a = @('-v','ON_ERROR_STOP=1','-X','-q','-h',$PgHost,'-p',"$Port",'-U',$SuperUser,'-d','postgres')
  if ($TuplesOnly) { $a += @('-t','-A') }
  $a += @('-c',$Sql)
  $out = & $psql @a 2>&1
  return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($out -join "`n") }
}

Write-Host "psql:      $psql"
Write-Host "superuser: $SuperUser@${PgHost}:$Port"
Write-Host "app role:  $AppUser"
Write-Host "database:  $Database"
Write-Host ""

# --- role ---
$r = Psql-Postgres -Sql "SELECT 1 FROM pg_roles WHERE rolname = '$AppUser';" -TuplesOnly
if ($r.ExitCode -ne 0) { Write-Error "Cannot reach server:`n$($r.Output)"; exit 1 }
if ($r.Output.Trim() -eq '1') {
  Write-Host "Role $AppUser exists -- resetting password to match the secrets file."
  $r = Psql-Postgres -Sql "ALTER ROLE $AppUser WITH LOGIN PASSWORD '$appPw';"
} else {
  Write-Host "Creating role $AppUser."
  $r = Psql-Postgres -Sql "CREATE ROLE $AppUser WITH LOGIN PASSWORD '$appPw';"
}
if ($r.ExitCode -ne 0) { Write-Error "Role step failed:`n$($r.Output)"; exit 1 }

# --- database (CREATE DATABASE cannot run inside a transaction/DO block) ---
$r = Psql-Postgres -Sql "SELECT 1 FROM pg_database WHERE datname = '$Database';" -TuplesOnly
if ($r.Output.Trim() -eq '1') {
  Write-Host "Database $Database exists."
} else {
  Write-Host "Creating database $Database owned by $AppUser."
  $r = Psql-Postgres -Sql "CREATE DATABASE $Database OWNER $AppUser;"
  if ($r.ExitCode -ne 0) { Write-Error "Database create failed:`n$($r.Output)"; exit 1 }
}

# --- privileges ---
$null = Psql-Postgres -Sql "GRANT ALL ON DATABASE $Database TO $AppUser;"
Write-Host "`nBootstrap complete. Next: apply-migrations.ps1 against $Database as $AppUser."
exit 0
