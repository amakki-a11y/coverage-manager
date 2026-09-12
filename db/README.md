# Coverage Manager v2 — local PostgreSQL store (Phase 0)

Phase 0 of [`docs/V2_PLAN.md`](../docs/V2_PLAN.md): a local PostgreSQL store on the CM
server, the forward-only v2 schema, and a repeatable, tested v1→v2 import. **Nothing
here touches production v1, Caddy, the collector, or the running coverage-api.** No
cutover happens in this phase; the real import is deferred to Phase 3.

## What is installed on this server

- **PostgreSQL 16.15**, Windows service `postgresql-x64-16` (Automatic), installed via
  winget (`PostgreSQL.PostgreSQL.16`) after the Chocolatey community feed was down.
- **Bound to `127.0.0.1:5432` only.** `postgresql.conf` `listen_addresses = '127.0.0.1'`
  (original `'*'` backed up to `postgresql.conf.bak_v2`); `pg_hba.conf` permits only
  `127.0.0.1/32` and `::1/128`. Verified: the only listener on 5432 is `127.0.0.1`.
- **Database `coverage_v2`** owned by login role **`coverage_app`**.
- **Secrets** (superuser + app passwords, the unattended-install option file) live
  **outside the repo** at `C:\ProgramData\CoverageManagerV2\secrets\`, ACL-locked to
  Administrators/SYSTEM. They are never printed and never committed.

## Layout

```
db/
  apply-migrations.ps1   forward-only migration runner (ledger: schema_migrations)
  bootstrap-db.ps1       create the coverage_app role + coverage_v2 database (idempotent)
  migrations/            0001..0090 -- immutable once applied
  import/
    tables.psd1          per-table manifest (mode, conflict keys, verify checks, order)
    import.ps1           v1 -> v2 importer (column-intersection, CSV extract, upsert)
    verify.ps1           row-count / numeric-sum / checksum comparison, source vs target
    test-import.ps1      self-contained integration test (throwaway DBs, seeded fixture)
```

All `.ps1` files are **pure ASCII** (see `CLAUDE.md`: the em-dash/cp1252 PowerShell
hazard). `_dumps/` (CSV extract scratch) is gitignored.

## Schema (migrations)

Authored from the 4 tracked `supabase/migrations/*.sql` plus the inline-created tables
reconstructed from the C# models and `SupabaseService` column mapping. Constraints
reproduce v1's real conflict keys exactly, so a verbatim v1 row can never be rejected
by a stricter v2 constraint.

| migration | tables |
|---|---|
| `0001_bootstrap.sql`         | shared `touch_updated_at()` trigger fn |
| `0002_reference.sql`         | `symbol_mappings`, `trading_accounts`, `moved_accounts` |
| `0003_deals_positions.sql`   | `deals`, `positions` (v2-new), `trade_audit_log` |
| `0004_exposure_snapshots.sql`| `exposure_snapshots`, `snapshot_schedules` (+seed) |
| `0005_equity_pnl.sql`        | `account_equity_snapshots`, `equity_pnl_client_config`, `equity_pnl_spread_rebates`, `login_groups`, `login_group_members`, `equity_pnl_group_config`, `equity_pnl_group_spread_rebates` |
| `0006_bridge.sql`            | `bridge_settings`, `bridge_executions` (Centroid dropcopy) |
| `0007_alerts.sql`            | `alert_rules`, `alert_events` |
| `0090_functions.sql`         | `cm_canonical_key`, `aggregate_bbook_settled_pnl`, `aggregate_bbook_pnl_full`, `latest_snapshots_before` (v1 Supabase RPCs re-expressed as local SQL, §5.3) |

**Dropped vs v1** (per plan §5.2): `account_settings` (no MT5 Manager in v2),
`reconciliation_runs` (deferred to the §5.4 feed↔db self-check decision), and the unused
stubs `pl_summary` / `hedge_executions` / `economic_events` / `risk_thresholds`.

**`positions` is v2-new:** v1 never persisted positions to Supabase (in-memory only),
so the import does **not** migrate it — v2 live-populates it from the feed writer.

Migrations are **immutable once applied**: the runner records a sha256 per file and
aborts on drift. To change the schema, add a new numbered migration.

## Import model (`import/tables.psd1`)

- **config-exact** (dealer/config data): authoritative mirror — `TRUNCATE ... CASCADE`
  then load, so source == target exactly (row count + checksum). Intended for the
  pre-cutover import when the target has no dealer-entered data yet.
- **archive-upsert** (`deals`, `trade_audit_log`, `bridge_executions`): insert-missing
  only (`ON CONFLICT DO NOTHING`) so a re-run and the live feed never clobber rows;
  verify allows `target >= source`.

The importer computes the source∩target column intersection (dropping source-only and
generated columns, defaulting target-only) so it tolerates schema drift between v1 and
v2. Source and target are libpq connection strings; passwords come from files, never the
command line — so the same tool moves a **local fixture** now and a **remote Supabase**
source in Phase 3.

## Retention — rolling 12 months of closed deals (decided 2026-09-12)

The v2 local Postgres keeps a **rolling 12 months of closed deals**; nothing older is
stored locally. The Exposure tab's "closed trades" section looks back at most 12
months, so local storage is sized to the dealer-facing look-back rather than the full
history of the books. Full policy: [`docs/V2_PLAN.md` §5.5](../docs/V2_PLAN.md).

Configured in **one place** — `RetentionMonths = 12` in
[`import/tables.psd1`](import/tables.psd1) — which drives both enforcement points so
they cannot drift:

| Enforcement point | Where | Status |
|---|---|---|
| **Import scope** | tables declaring `WindowColumn` (today: `deals.deal_time`) are filtered on extract; `verify.ps1` applies the *same* predicate to **both** sides | implemented |
| **Prune policy** | same cutoff removes aged-out rows locally | policy recorded below; the automated pruner is a **Phase 2 runtime job, not built yet** |

Cutoff is UTC-day-stable, so an import and a later verify on the same UTC day agree
regardless of either server's session timezone:

```sql
date_trunc('day', (now() AT TIME ZONE 'UTC')) AT TIME ZONE 'UTC' - interval '12 months'
```

Prune statement (run deliberately; not yet scheduled):

```sql
DELETE FROM deals
 WHERE deal_time < date_trunc('day', (now() AT TIME ZONE 'UTC')) AT TIME ZONE 'UTC'
                   - interval '12 months';
```

Pass `-RetentionMonths 0` to `import.ps1` / `verify.ps1` to disable windowing and move
full history (both must be given the same value).

**Deeper look-back is deferred.** Older than the window will be served by a future
**on-demand, read-only** request to the accounting system through a narrow,
purpose-built API — never a direct database key into the books, never a bulk copy back
into v2. **Do not build that fetch now.**

**Not covered:** `trade_audit_log`, `bridge_executions` and `alert_events` are separate
history tables — they import in full and are never pruned until a separate call is made
(`docs/V2_PLAN.md` §9.7).

## Usage

```powershell
$sf = 'C:\ProgramData\CoverageManagerV2\secrets\pg_superuser.txt'
$af = 'C:\ProgramData\CoverageManagerV2\secrets\pg_app.txt'

# One-time: create role + database
.\bootstrap-db.ps1 -SuperPasswordFile $sf -AppPasswordFile $af

# Apply / update schema (idempotent, forward-only)
$env:PGPASSWORD = (Get-Content $af -Raw).Trim()
.\apply-migrations.ps1 -Database coverage_v2 -User coverage_app

# Prove the import pipeline end-to-end (builds + drops throwaway DBs)
.\import\test-import.ps1 -SuperPasswordFile $sf
```

### Phase 3 (NOT NOW): the real import

When Phase 3 begins, point the importer at the live Supabase Postgres:

```powershell
.\import\import.ps1 -SourceConn "<supabase libpq conn>" -TargetConn "host=127.0.0.1 dbname=coverage_v2 user=coverage_app" -SourcePasswordFile <src> -TargetPasswordFile $af
.\import\verify.ps1 -SourceConn "<supabase libpq conn>" -TargetConn "host=127.0.0.1 dbname=coverage_v2 user=coverage_app" -SourcePasswordFile <src> -TargetPasswordFile $af
```

## Verified in Phase 0

- Service up, `psql` 16.15, listener = `127.0.0.1:5432` only.
- All 8 migrations apply on a fresh DB and are idempotent (second run applies nothing).
- `coverage_v2` has all 19 domain tables + the `schema_migrations` ledger; only
  `snapshot_schedules` is seeded (3 rows), everything else empty.
- `test-import.ps1`: **30/30 verify checks pass, 7/7 assertions pass** — config mirror,
  archive load, source-only + generated-column drop, canonical `.c` merge, and the
  settled/full aggregation functions all correct.
