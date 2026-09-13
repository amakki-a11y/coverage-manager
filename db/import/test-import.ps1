<#
.SYNOPSIS
  Self-contained integration test for the v1 -> v2 import pipeline.

.DESCRIPTION
  Builds two throwaway local databases (a v1-shaped SOURCE fixture and a fresh v2
  TARGET), applies the v2 migrations to both, seeds the source with representative
  rows (config + archive, including canonical-normalization and admin balance deals),
  runs import.ps1 then verify.ps1, and asserts:
    * verify.ps1 reports zero failures, and
    * aggregate_bbook_settled_pnl on the target returns the hand-computed value, and
    * a deliberate source-only column (deals.legacy_note) is dropped gracefully.
  Throwaway databases are dropped at the end (use -KeepDbs to inspect them).

  Requires the local PostgreSQL superuser password file. Touches ONLY the throwaway
  databases -- never coverage_v2, never anything the live dealer uses.

.EXAMPLE
  .\test-import.ps1 -SuperPasswordFile C:\ProgramData\CoverageManagerV2\secrets\pg_superuser.txt

.NOTES
  Pure ASCII (CLAUDE.md em-dash/cp1252 hazard).
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$SuperPasswordFile,
  [string]$PgHost = '127.0.0.1',
  [int]$Port = 5432,
  [string]$SuperUser = 'postgres',
  [string]$SrcDb = 'cmv2_src_fixture',
  [string]$TgtDb = 'cmv2_tgt_test',
  [switch]$KeepDbs,
  [string]$PsqlPath
)

$ErrorActionPreference = 'Continue'
$here = $PSScriptRoot
$dbDir = Split-Path $here -Parent   # ..\db

function Resolve-Psql {
  param([string]$Explicit)
  if ($Explicit -and (Test-Path $Explicit)) { return $Explicit }
  $onPath = (Get-Command psql -ErrorAction SilentlyContinue)
  if ($onPath) { return $onPath.Source }
  $c = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue | Sort-Object FullName -Descending
  if ($c) { return $c[0].FullName }
  throw "psql not found. Pass -PsqlPath."
}
$psql = Resolve-Psql -Explicit $PsqlPath
$env:PGPASSWORD = (Get-Content $SuperPasswordFile -Raw).Trim()

function PgExec { param([string]$Db,[string]$Sql)
  $out = & $psql @('-v','ON_ERROR_STOP=1','-X','-q','-h',$PgHost,'-p',"$Port",'-U',$SuperUser,'-d',$Db,'-c',$Sql) 2>&1
  return [pscustomobject]@{ ExitCode=$LASTEXITCODE; Output=($out -join "`n") }
}
function PgScalar { param([string]$Db,[string]$Sql)
  $out = & $psql @('-v','ON_ERROR_STOP=1','-X','-q','-t','-A','-h',$PgHost,'-p',"$Port",'-U',$SuperUser,'-d',$Db,'-c',$Sql) 2>&1
  return ($out -join "`n").Trim()
}
function Drop-Db { param([string]$Db)
  $null = PgExec -Db 'postgres' -Sql "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='$Db' AND pid<>pg_backend_pid();"
  $null = PgExec -Db 'postgres' -Sql "DROP DATABASE IF EXISTS $Db;"
}
function ConnStr { param([string]$Db) "host=$PgHost port=$Port dbname=$Db user=$SuperUser" }

$fail = 0
function Assert { param([string]$Name,[bool]$Ok,[string]$Detail)
  if ($Ok) { Write-Host "  [PASS] $Name" } else { Write-Host "  [FAIL] $Name -- $Detail"; $script:fail++ }
}

Write-Host "=== 1. (re)create throwaway databases ==="
Drop-Db $SrcDb; Drop-Db $TgtDb
$r = PgExec -Db 'postgres' -Sql "CREATE DATABASE $SrcDb;"; if ($r.ExitCode){ Write-Host $r.Output; exit 1 }
$r = PgExec -Db 'postgres' -Sql "CREATE DATABASE $TgtDb;"; if ($r.ExitCode){ Write-Host $r.Output; exit 1 }

Write-Host "=== 2. apply v2 migrations to both ==="
& (Join-Path $dbDir 'apply-migrations.ps1') -ConnString (ConnStr $SrcDb) -MigrationsDir (Join-Path $dbDir 'migrations') | Out-Null
if ($LASTEXITCODE) { Write-Host "migrations failed on source"; exit 1 }
& (Join-Path $dbDir 'apply-migrations.ps1') -ConnString (ConnStr $TgtDb) -MigrationsDir (Join-Path $dbDir 'migrations') | Out-Null
if ($LASTEXITCODE) { Write-Host "migrations failed on target"; exit 1 }

Write-Host "=== 3. seed the SOURCE fixture ==="
# Deliberate source-only column to exercise the intersection/drop path.
$null = PgExec -Db $SrcDb -Sql "ALTER TABLE deals ADD COLUMN legacy_note text;"

$seed = @'
INSERT INTO symbol_mappings (id, canonical_name, bbook_symbol, bbook_contract_size, coverage_symbol, coverage_contract_size, digits, profit_currency, is_active, pip_size) VALUES
  ('11111111-1111-1111-1111-111111111111','XAUUSD','XAUUSD-',100,'XAUUSD.c',100,2,'USD',true,0.01),
  ('22222222-2222-2222-2222-222222222222','US30','US30-10',1,'US30.c',1,1,'USD',true,1);

INSERT INTO trading_accounts (id, source, login, name, group_name, leverage, balance, equity, credit, margin, free_margin, currency, status) VALUES
  ('33333333-3333-3333-3333-333333333333','bbook',5001,'Alice','real\\A',100,10000,10250,0,500,9750,'USD','active'),
  ('44444444-4444-4444-4444-444444444444','bbook',5002,'Bob','real\\B',200,25000,24800,1000,300,24500,'USD','active');

INSERT INTO moved_accounts (login, moved_at, reason) VALUES (5999, '2026-02-01T00:00:00Z','left desk');

INSERT INTO exposure_snapshots (canonical_symbol, snapshot_time, net_volume, bbook_pnl, coverage_pnl, net_pnl) VALUES
  ('XAUUSD','2026-03-27T22:00:00Z', 5.0, -1200.50, 1100.00, 2300.50),
  ('US30',  '2026-03-27T22:00:00Z', 2.0,   300.00,  250.00,  -50.00);

INSERT INTO account_equity_snapshots (login, source, snapshot_time, balance, equity, credit, margin) VALUES
  (5001,'bbook','2026-03-27T22:00:00Z',10000.00,10250.00,0,500.00),
  (5002,'bbook','2026-03-27T22:00:00Z',25000.00,24800.00,1000.00,300.00);

INSERT INTO equity_pnl_client_config (login, source, comm_rebate_pct, ps_pct, ps_contract_start) VALUES
  (5001,'bbook',50.000,10.000,'2026-01-01');

INSERT INTO equity_pnl_spread_rebates (login, source, canonical_symbol, rate_per_lot) VALUES
  (5001,'bbook','XAUUSD',5.0000);

INSERT INTO login_groups (id, name, description) VALUES
  ('55555555-5555-5555-5555-555555555555','VIP-TierA','top clients');
INSERT INTO login_group_members (group_id, login, source, priority) VALUES
  ('55555555-5555-5555-5555-555555555555',5001,'bbook',10);
INSERT INTO equity_pnl_group_config (group_id, comm_rebate_pct, ps_pct) VALUES
  ('55555555-5555-5555-5555-555555555555',40.000,5.000);
INSERT INTO equity_pnl_group_spread_rebates (group_id, canonical_symbol, rate_per_lot) VALUES
  ('55555555-5555-5555-5555-555555555555','US30',3.0000);

INSERT INTO bridge_settings (id, enabled, mode, base_url, client_code, username, password, notes) VALUES
  ('66666666-6666-6666-6666-666666666666', false, 'Stub', 'https://bridge.centroidsol.com','CC','u','p','fixture');

INSERT INTO alert_rules (id, symbol, trigger_type, operator, value, severity, enabled) VALUES
  ('77777777-7777-7777-7777-777777777777','XAUUSD','exposure','gt',100,'warning',true);
INSERT INTO alert_events (id, threshold_id, trigger_type, symbol, severity, message, threshold_value, actual_value, triggered_at, acknowledged) VALUES
  ('88888888-8888-8888-8888-888888888888','77777777-7777-7777-7777-777777777777','exposure','XAUUSD','warning','over',100,120,'2026-04-01T09:00:00Z',false);

-- deals: times are now()-relative (import windows on the REAL clock; absolute dates would age out
-- of the 12-month window and fail this test on correct code). Settled window = now-400d .. now+1d.
-- Expected XAUUSD settled = 135:
--   OUT (canonical XAUUSD):        profit 100 + swap -2 + comm -5 + fee -1 = 92
--   IN  (canonical XAUUSD):        entry=0 -> profit/swap excluded; comm -5 + fee 0 = -5
--   OUT (canonical XAUUSD.c -> normalizes to XAUUSD): 50 + 0 + comm -2 + fee 0 = 48
--   BALANCE (action=2):            excluded entirely
--   => 92 - 5 + 48 = 135
INSERT INTO deals (source, deal_id, login, symbol, canonical_symbol, direction, action, entry, volume, price, profit, commission, swap, fee, order_id, position_id, deal_time, legacy_note) VALUES
  ('bbook',1001,5001,'XAUUSD-','XAUUSD','SELL',1,1,1,2400.5, 100,-5,-2,-1, 9001,8001,now() - interval '11 days','n1'),
  ('bbook',1002,5001,'XAUUSD-','XAUUSD','BUY', 0,0,1,2399.0,   0,-5, 0, 0, 9001,8001,now() - interval '12 days','n2'),
  ('bbook',1003,5002,'XAUUSD.c','XAUUSD.c','SELL',1,1,1,2401.0,50,-2, 0, 0, 9002,8002,now() - interval '10 days','n3'),
  ('bbook',1004,5001,'','', 'BALANCE',2,0,0,0, 1000, 0, 0, 0, NULL, NULL,now() - interval '9 days','deposit'),
  -- Out of the rolling 12-month retention window (V2_PLAN 5.5): must NOT be imported.
  ('bbook',1005,5001,'XAUUSD-','XAUUSD','SELL',1,1,1,2000.0, 999,-9,-9,-9, 9005,8005,'2019-01-01T10:00:00Z','ancient');

INSERT INTO trade_audit_log (source, deal_id, login, symbol, field_changed, old_value, new_value, changed_by, change_type, detected_at) VALUES
  ('bbook',1001,5001,'XAUUSD','profit','90','100','recon','modified','2026-04-02T11:00:00Z');

-- bridge_executions: one REAL pair (has client_mt_deal_id) and one synthetic Stub pair (does not).
-- The RowFilter must carry the real one and drop the stub one.
INSERT INTO bridge_executions (client_deal_id, cen_ord_id, symbol, side, client_volume, client_price, client_time, client_mt_deal_id, cov_volume, cov_fills, avg_cov_price, price_edge, pips) VALUES
  ('EXID-1','ORD-1','XAUUSD','SELL',2,2400.5,'2026-04-02T10:00:00Z',284872,2,'[{"dealId":"d1","volume":2,"price":2400.4,"time":"2026-04-02T10:00:00Z","timeDiffMs":120}]'::jsonb,2400.4,0.1,10),
  ('ord-1789000000000','c-ord-1789000000000','XAUUSD','BUY',1,2400.5,'2026-04-02T11:00:00Z',NULL,1,'[]'::jsonb,2400.4,0.1,10);
'@
$seedFile = Join-Path $env:TEMP "cmv2_seed.sql"
Set-Content -Path $seedFile -Value $seed -Encoding ascii
$r = & $psql @('-v','ON_ERROR_STOP=1','-X','-q','-h',$PgHost,'-p',"$Port",'-U',$SuperUser,'-d',$SrcDb,'-f',$seedFile) 2>&1
if ($LASTEXITCODE) { Write-Host "seed failed:`n$($r -join "`n")"; exit 1 }

Write-Host "=== 4. run import (source -> target) ==="
& (Join-Path $here 'import.ps1') -SourceConn (ConnStr $SrcDb) -TargetConn (ConnStr $TgtDb) `
    -SourcePasswordFile $SuperPasswordFile -TargetPasswordFile $SuperPasswordFile | Write-Host
$importExit = $LASTEXITCODE

Write-Host "=== 5. verify (source vs target) ==="
& (Join-Path $here 'verify.ps1') -SourceConn (ConnStr $SrcDb) -TargetConn (ConnStr $TgtDb) `
    -SourcePasswordFile $SuperPasswordFile -TargetPasswordFile $SuperPasswordFile | Write-Host
$verifyExit = $LASTEXITCODE

Write-Host "=== 6. assertions ==="
Assert 'import.ps1 exit 0' ($importExit -eq 0) "exit=$importExit"
Assert 'verify.ps1 exit 0' ($verifyExit -eq 0) "exit=$verifyExit"

$settled = PgScalar -Db $TgtDb -Sql "SELECT COALESCE(net_pnl,0) FROM aggregate_bbook_settled_pnl(now() - interval '400 days', now() + interval '1 day') WHERE canonical_key='XAUUSD';"
Assert 'settled(XAUUSD)=135 (canonical .c merged, IN excl profit, BALANCE excluded)' ([decimal]$settled -eq [decimal]135) "got $settled"

$full = PgScalar -Db $TgtDb -Sql "SELECT total_volume||'/'||buy_volume||'/'||sell_volume FROM aggregate_bbook_pnl_full(now() - interval '400 days', now() + interval '1 day') WHERE symbol='XAUUSD';"
Assert 'pnl_full(XAUUSD) volume total/buy/sell = 3/1/2' ($full -eq '3/1/2') "got $full"

$hasLegacy = PgScalar -Db $TgtDb -Sql "SELECT count(*) FROM information_schema.columns WHERE table_name='deals' AND column_name='legacy_note';"
Assert 'source-only column deals.legacy_note dropped on import' ($hasLegacy -eq '0') "count=$hasLegacy"

$dealCount = PgScalar -Db $TgtDb -Sql "SELECT count(*) FROM deals;"
Assert 'in-window deals imported (archive), aged row excluded' ($dealCount -eq '4') "count=$dealCount"

# Retention (V2_PLAN 5.5): the 2019 deal is outside the rolling 12-month window.
$srcTotal = PgScalar -Db $SrcDb -Sql "SELECT count(*) FROM deals;"
$aged     = PgScalar -Db $TgtDb -Sql "SELECT count(*) FROM deals WHERE deal_id = 1005;"
Assert 'source really held the aged row (fixture sanity)' ($srcTotal -eq '5') "src count=$srcTotal"
Assert 'rolling 12-month window excluded the aged deal from the target' ($aged -eq '0') "deal_id 1005 rows=$aged"

$bridgeReal = PgScalar -Db $TgtDb -Sql "SELECT count(*) FROM bridge_executions WHERE client_deal_id = 'EXID-1';"
$bridgeStub = PgScalar -Db $TgtDb -Sql "SELECT count(*) FROM bridge_executions WHERE client_mt_deal_id IS NULL;"
Assert 'bridge_executions: the real pair (client_mt_deal_id set) is imported' ($bridgeReal -eq '1') "count=$bridgeReal"
Assert 'bridge_executions: the synthetic stub pair is filtered out' ($bridgeStub -eq '0') "count=$bridgeStub"

$schedCount = PgScalar -Db $TgtDb -Sql "SELECT count(*) FROM snapshot_schedules;"
Assert 'snapshot_schedules mirrored to 3 (seed replaced, no dup)' ($schedCount -eq '3') "count=$schedCount"

if (-not $KeepDbs) { Write-Host "=== 7. cleanup ==="; Drop-Db $SrcDb; Drop-Db $TgtDb }
else { Write-Host "=== 7. keeping $SrcDb / $TgtDb (per -KeepDbs) ===" }

Write-Host ""
if ($fail -gt 0) { Write-Host "TEST RESULT: FAIL ($fail assertion(s) failed)"; exit 1 }
Write-Host "TEST RESULT: PASS"
exit 0
