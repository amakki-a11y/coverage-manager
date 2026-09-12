# Coverage Manager v2 — Design Plan

**Status:** Design only. No code in this document. Nothing here is built yet.
**Author:** planning pass, 2026-09-11.
**Scope:** A ground-up re-platforming of Coverage Manager onto two decisions:

1. **The Live Bridge consumer feed is the ONLY B-Book source.** No MT5 Manager API
   connector ships in v2. The bridge (TheBridge, `docs/product-spec.md` §15,
   `wss://feed.connecttrader.app:5571/feed/<source>`) owns ingestion, reconnect,
   gap-fill/replay, and multi-server fan-in. v2 consumes one authoritative stream.
2. **A LOCAL PostgreSQL instance on the CM server is the store.** No Supabase, no
   PostgREST. The app opens a direct SQL connection (Npgsql) to a Postgres running
   on the same box as the API.

Everything below follows from those two decisions. This plan does **not** change the
LP/coverage collector or the React dashboard's behaviour — both are carried forward.

---

## 1. Why this is a v2 and not a refactor

v1's shape is dominated by two facts that v2 removes:

- **MT5 Manager was the B-Book source.** That forced a strict bring-up order, event
  sink wiring, position/account/deal polling, an auto-backfill-on-startup, an
  account-sync tracker, a cash-movement sync loop, and a nightly reconciliation
  sweep — all to keep an in-memory cache and Supabase in agreement with a live
  Manager connection. The Live Bridge feed already does ingestion, reconnect,
  gap-fill and multi-server, so most of that scaffolding has nothing left to do.
- **Supabase (remote PostgREST) was the store.** That forced paced/throttled/probed
  writes, `on_conflict` batch upserts, pagination, exponential-backoff retries, a
  mapping-cache self-healer, and server-side RPCs to dodge 46–81s client-side
  GROUP BYs. Against local Postgres over a Unix socket / localhost TCP, those
  problems are gone: the app runs plain SQL, `COPY`, and set-based aggregation with
  no network, no rate limit, no statement-timeout-turned-503.

v2 keeps every **domain engine** and the entire **frontend/API contract**, and
rewrites the **edges** (source adapter and storage layer). The dealer sees the same
tabs; the plumbing behind them is roughly half the code.

---

## 2. Target architecture

```
                          ┌──────────────────────────────────────────┐
  Live Bridge feed        │            CM server (single box)         │
  wss://…:5571/feed/<src> │                                          │
  (ingestion, reconnect,  │   ┌────────────┐    ┌──────────────────┐ │
   gap-fill, multi-server)─┼──▶│ FeedClient │───▶│ Domain engines    │ │
                          │   │ (consumer) │    │ Position/Exposure │ │
                          │   └────────────┘    │ Price/Deal/Equity │ │
  Coverage MT5 Terminal   │                     │ PnL/Snapshot      │ │
  → Python collector ─────┼──HTTP POST─────────▶│                   │ │
    (poll 100ms, unchanged)│                     └────────┬─────────┘ │
                          │                              │            │
  Centroid CS 360         │   ┌────────────┐             ▼            │
  FIX 4.4 Dropcopy ───────┼──▶│ Bridge     │      ┌─────────────┐     │
  (coverage exec pairs)   │   │ pairing    │─────▶│ LOCAL        │     │
                          │   └────────────┘      │ PostgreSQL   │     │
                          │                        │ (Npgsql,     │     │
                          │   WebSocket / REST ◀───│  direct SQL) │     │
                          │        │               └─────────────┘     │
                          └────────┼──────────────────────────────────┘
                                   ▼
                          React dashboard (unchanged API contract)
```

Two distinct "bridges" — do not conflate them:

- **Live Bridge feed** = the B-Book source (positions/accounts/deals/ticks). Replaces
  MT5 Manager. New in v2 as the *only* source.
- **Centroid Bridge** = the coverage execution dropcopy (FIX 4.4) that powers the
  Bridge Execution tab. Unrelated to the Live Bridge feed; carried forward unchanged.

---

## 3. Module inventory — keep / drop / rewrite

### 3.1 DROP (the feed or local Postgres makes them dead weight)

| v1 component | Why it dies in v2 |
|---|---|
| `MT5ApiReal.cs`, `MT5ManagerConnection.cs`, `MT5CoverageConnection.cs` | No Manager API. The feed is the source. |
| `IMT5Api`, `IMT5ApiFactory`, `MT5ApiFactory`, `MT5ApiProviders` | The provider abstraction existed to swap Manager↔LiveBridge. v2 has one source; the factory collapses. |
| `Connector/Libs/` MetaQuotes native DLLs + the `publish\api\` DLL-copy workaround | No native P/Invoke. Removes the whole "native DLL in Libs\" deploy hazard. |
| Bring-up order steps 3–5 (GetUserLogins → SnapshotPositions → BackfillDeals) and the "subscribe last" race guard | The feed's `hello → snapshot/replay → snapshot_end` handshake *is* the bring-up. State arrives before live records by contract. |
| `AccountSyncTracker` + 15-min account bulk sync | Accounts arrive on the feed as account frames. No roster poll. |
| `CashMovementSyncService` | Admin balance/credit deals are pushed by the feed (already true under LiveBridge in v1). No 7-day sliding backfill. |
| Auto-backfill-on-startup from Manager (gap detection) | Postgres holds everything since cutover; the feed replays its retained window on reconnect. Gap-fill is the bridge's job. |
| `DealReconciler`, `IMT5DealHistory`, `DealHistoryWindow`, provider-window clamp | These existed to reconcile a *second* authority (Manager) and to gate ghost-deletion against the feed's 48h horizon. v2 has no second authority; Postgres is durable. (See §5.4 for the reduced self-check that replaces it.) |
| `ReconciliationService` (nightly Manager-vs-Supabase sweep) + `reconciliation_runs` as a cross-source audit | No cross-source reconciliation. Optionally kept as a much smaller feed↔db integrity check. |
| `SupabaseService` (PostgREST client) + `RetryAsync` HTTPS backoff | Replaced by `PostgresService` (Npgsql). No remote HTTP. |
| `MappingRefreshService` | Existed to heal Supabase TLS-reset cold starts. Local Postgres doesn't TLS-reset; mapping cache reloads via a simple query + optional `LISTEN/NOTIFY`. |
| `DataSyncService` paced/throttled/probed writer (500 rows/req, 20 req/tick, 250ms apart, HardCap, consecutiveFailures probe) | The pacing machinery is entirely a Supabase-rate-limit workaround. Local Postgres takes batched `INSERT … ON CONFLICT` / `COPY` synchronously. A bounded write queue stays; the elaborate pacing does not. |
| `aggregate_bbook_settled_pnl`, `aggregate_bbook_pnl_full`, `latest_snapshots_before`, `GetSnapshotsAtAsync` **as Supabase RPCs** | Re-expressed as local SQL (functions or parameterized queries). The 46s/81s roundtrips they replaced never existed locally. |
| `account_settings` table + Settings "Connections" MT5 Manager credential UI | No Manager to authenticate. Feed creds live in config/env (`LiveBridge:Url` + `LiveBridge__ApiKey`). |

### 3.2 KEEP unchanged (pure domain / presentation)

- **Core engines:** `PositionManager`, `ExposureEngine` (incl. `PriceCache` calibrated-delta floating P&L), `PriceCache`, `DealStore`.
- **Equity P&L:** `EquityPnLEngine`, `PsHighWaterMarkEngine`.
- **Coverage/Centroid Bridge:** `BridgePairingEngine`, `BridgePipResolver`, `BridgeExecutionStore`, `BridgeBroadcastService`, `BridgeExecutionWorker`, `StubCentroidBridgeService` — the coverage dropcopy path is independent of the B-Book source.
- **Broadcast layer:** `ExposureBroadcastService` two-channel design (`exposure_update` + `price_update`), `deal_settled` frames, coalescing, `/ws/exposure`, `/ws/bridge`, `permessage-deflate`.
- **Scheduler:** `ExposureSnapshotService` + `Cronos` cron dispatch (snapshot + account-equity snapshot writes).
- **The entire `web/` React app.** It talks to the same REST + WS contract; it does not know or care that the source and store changed.
- **The Python coverage collector (`collector/main.py`).** Unchanged. Still polls the MT5 terminal every 100ms and serves `/positions`, `/deals`, `/deals/raw`, `/health`.

### 3.3 KEEP but re-point the storage layer

Every service that today calls `SupabaseService` gets a new `PostgresService`
(Npgsql, Dapper-or-raw) with the same method surface. Same DTOs, same call sites.

- `ExposureController`, `EquityPnLConfigController`, `LoginGroupsController`, `SnapshotSchedulesController`, `ReconciliationController` (if the self-check survives), `BridgeController`, plus the `Accounts`, `Compare`, `SymbolMapping`, `Settings` controllers.
- `ExposureSnapshotService`, `BridgeSupabaseWriter` → `BridgePostgresWriter`.

### 3.4 KEEP and simplify (the feed adapter)

`LiveBridgeApi` and its `LiveBridge/` internals (`FeedWire`, `FeedMapping`,
`FeedBook`, `FeedSequenceStore`, `FeedSocket`, `GroupMask`, `LiveBridgeOptions`,
`IMT5ApiDiagnostics`) are the v2 foundation and are **kept**. What changes:

- It no longer implements `IMT5Api` to satisfy a swappable factory — it is *the*
  source. The `IMT5Api` interface can be retired or slimmed to just what the engines
  consume (`OnTick/OnDealAdd/OnPosition*/OnUserUpdate`, `GetPositions`,
  `GetUserAccount`, `GetUserLogins`, `GetTickLast`).
- `RequestDeals` no longer needs the 48h-retention-and-`DealHistoryWindow` dance to
  answer historical queries: historical deals are read from **Postgres**, not from
  the feed's in-process retention. The feed streams *new* deals into Postgres; the
  app reads history back from Postgres. `FeedBook` keeps only the live working set.
- Durable resume sequences (`FeedSequenceStore`, `LiveBridge:StatePath`) stay
  exactly as designed — they are the reconnect/replay contract.

---

## 4. Local PostgreSQL — deployment shape

- **Where:** same server as the API (`C:\CoverageManager`). Postgres 16+ Windows
  service, or containerized. Listens on localhost only.
- **Connection:** Npgsql connection string in config/env (not in git). Connection
  pooling on. All access is server-side SQL — no PostgREST, no row-level security,
  no anon/publishable keys.
- **Durability (this is the new single point of failure Supabase used to own):**
  - `pg_dump` nightly to a separate disk/volume + off-box copy (retain N days).
  - WAL archiving on, so point-in-time recovery is possible.
  - **Recommended:** a streaming read-replica (second Postgres, on another box or the
    old v1 host) for warm standby. Decision flagged in §9.
- **Migrations:** plain SQL migration files under `supabase/migrations/` (folder can
  be renamed `db/migrations/`), applied by a lightweight runner at deploy time. No
  Supabase CLI.
- **Schema-change discipline:** same rule as v1 — inspect current tables before
  altering; migrations are forward-only and reviewed.

---

## 5. Schema — local Postgres, mapped from v1's Supabase tables

v1 has 25 Supabase tables. Below is the v2 disposition. Types are Postgres-native
(`bigint`, `numeric`, `timestamptz`, `jsonb`, generated columns) — the same engine,
so DDL is a near-copy minus PostgREST/RLS artifacts.

### 5.1 Carried over 1:1 (data + config)

| v1 Supabase table | v2 Postgres table | Notes |
|---|---|---|
| `symbol_mappings` | `symbol_mappings` | B-Book↔LP mapping, contract sizes, `pip_size`. Seed from v1 export. **Must add feed-suffix variant rows** (`UT100-20`, `US30-10`, `DE40U6-`, `ES500.U6` → canonical) — the cutover gap noted in v1 Operational Learnings. |
| `deals` | `deals` | 280K+ historical rows migrated as archive so period P&L over old ranges works. Dedup unique `(source, deal_id)`. Keeps `direction/fee/entry/order_id/action`. New rows written by the feed writer. |
| `positions` | `positions` | Open-position snapshot; now sourced from feed position frames. |
| `trading_accounts` | `trading_accounts` | Unique `(source, login)`. Populated from feed **account frames**, not a Manager roster poll. |
| `trade_audit_log` | `trade_audit_log` | Deal-modification audit; written from the in-memory changed copy (as v1). |
| `exposure_snapshots` | `exposure_snapshots` | Net P&L "Begin" anchor. Unique `(canonical_symbol, snapshot_time)`, `trigger_type`, `label`. **Migrate — Net P&L history breaks without it.** |
| `account_equity_snapshots` | `account_equity_snapshots` | Equity P&L "Begin Equity". Includes the 2026-03-28 Segregated seed rows. **Migrate.** |
| `equity_pnl_client_config` | `equity_pnl_client_config` | Per-login rebate/PS config + engine HWM state. **Migrate — dealer-entered.** |
| `equity_pnl_spread_rebates` | `equity_pnl_spread_rebates` | Per-(login, symbol) rate. **Migrate.** |
| `login_groups` | `login_groups` | Phase-2 groups. **Migrate.** |
| `login_group_members` | `login_group_members` | Priority-based membership. **Migrate.** |
| `equity_pnl_group_config` | `equity_pnl_group_config` | Per-group config. **Migrate.** |
| `equity_pnl_group_spread_rebates` | `equity_pnl_group_spread_rebates` | Per-group rate. **Migrate.** |
| `snapshot_schedules` | `snapshot_schedules` | Cron cadences (`Asia/Beirut`). **Migrate.** |
| `bridge_settings` | `bridge_settings` | Centroid creds + mode. **Migrate.** |
| `bridge_executions` | `bridge_executions` | Paired CLIENT↔COV_OUT, `cov_fills` jsonb, generated `coverage_ratio`. **Migrate + keep writing.** |
| `moved_accounts` | `moved_accounts` | Logins removed from source but kept for history. Still meaningful (feed sources can drop logins). |
| `alert_rules`, `alert_events` | same | Alert config + fired events. **Migrate rules; events are append-only history.** |

### 5.2 Dropped

| v1 table | Reason |
|---|---|
| `account_settings` | No MT5 Manager to authenticate. Feed creds move to config/env. |
| `reconciliation_runs` | No cross-source sweep. Keep only if the reduced feed↔db self-check (§5.4) is built, and then repurpose its columns. |
| `pl_summary`, `hedge_executions`, `economic_events`, `risk_thresholds` | Phase 2/3/4 stubs, unused today. Recreate when those phases actually land, not migrated speculatively. |

### 5.3 v1 Supabase RPCs → v2 local SQL

- `aggregate_bbook_settled_pnl(from,to,excluded)` → local SQL function, identical
  canonical-key normalization (strip `.c`/`.m`/trailing `-`, uppercase), sums
  `profit+swap` on OUT deals + `commission+fee` on all trade deals.
- `aggregate_bbook_pnl_full(from,to,excluded)` → local SQL function, per-symbol full
  aggregation for `/api/exposure/pnl`, P&L tab, Compare Full Table.
- `latest_snapshots_before(anchor)` → local `DISTINCT ON (canonical_symbol)` query.
- `GetSnapshotsAtAsync(exact)` → local `WHERE snapshot_time = anchor` query.

These stay *functions/queries* (not client-side loops) for the same reason as v1 —
set-based aggregation belongs in the database — but locally they are sub-millisecond
and need no "avoid the 81s roundtrip" justification.

### 5.4 New / reduced: feed↔db integrity self-check (optional)

Because the feed is the only authority and Postgres persists it, there is nothing to
reconcile *against*. The one residual risk is the app dropping a record between
"received from feed" and "committed to Postgres" (crash mid-write). A small
**self-check** can replace the whole reconciliation sweep:

- On a schedule, compare `FeedBook`'s live working set (positions, latest deals in
  retention) against Postgres for the retained window only.
- Divergence → re-write from `FeedBook`; log to a slimmed `reconciliation_runs`
  (trigger/window/counts/notes). Never deletes on the basis of a second source
  (there isn't one). This is a self-heal, not a cross-source audit.

Decision flagged in §9: build the self-check, or trust synchronous commit + WAL.

### 5.5 Data retention — rolling 12 months of closed deals (DECIDED 2026-09-12)

**The v2 local Postgres keeps a rolling 12 months of closed deals. Nothing older is
stored locally.** This resolves the former open decision "historical archive depth"
(§9.4).

- **Why 12 months:** the Exposure tab's "closed trades" section looks back at most
  12 months. Local storage is sized to the dealer-facing look-back, not to the full
  history of the books.
- **Scope — one policy, two enforcement points:**
  1. **Import scope (Phase 3):** the v1 → v2 import pulls only the trailing 12 months
     of `deals`. It does *not* copy the full 280K+ row archive.
  2. **Prune policy (ongoing):** `deals` rows older than the 12-month window are
     pruned from local Postgres on a rolling basis.
  Both are configured in one place — `RetentionMonths` in
  [`db/import/tables.psd1`](../db/import/tables.psd1) — so import and prune cannot
  drift apart.
- **Deeper look-back is DEFERRED — do not build it now.** When a dealer eventually
  needs older than 12 months, it will be served by an **on-demand, READ-ONLY request
  to the accounting system through a narrow, purpose-built API** — never a direct
  database key into the books, and never a bulk copy back into v2. No phase in this
  plan builds that fetch; it is explicitly out of scope until separately commissioned.
- **Consequence to accept:** once v1 is frozen and its Supabase project is retired
  (§8.3), deals older than the window exist only in the accounting system. Keep the
  final v1 export from §8.3 as the bridge until the read-only API exists.
- **Not covered by this decision:** `trade_audit_log`, `bridge_executions` and
  `alert_events` are separate history tables. This decision speaks only to closed
  deals; their retention needs its own call (flagged in §9.7) and until then they
  import in full.

---

## 6. Multi-server (feed fan-in)

The feed URL is `/feed/<source>`. "Multi-server" means the bridge aggregates several
MT5 servers; the consumer may subscribe to one source or several. v2 design:

- **`deals.source` / `trading_accounts.source` / `positions.source`** already carry a
  source tag in v1 — keep it as the per-feed discriminator.
- One `FeedClient` instance per subscribed `<source>`, each with its own durable
  resume sequences (`StatePath` per source), all writing into the one Postgres,
  tagged by source. `GroupMask` filters logins per source as today.
- Exposure aggregation is canonical-symbol-first and source-agnostic; adding a source
  adds rows, it does not change the math.

Decision flagged in §9: is v2 single-source (`BBcorp-Live`) at launch with multi
designed-in, or multi-source day one?

---

## 7. LP / coverage collector — unchanged

No change. The Python FastAPI collector keeps polling the coverage MT5 terminal at
100ms and POSTing to the backend; `/positions`, `/deals`, `/deals/raw`, `/health`
stay as-is. Coverage settled P&L stays REST-driven (the collector has no event hook
into the C# backend — same as v1). The known collector-hang stopgap (health-probe +
`nssm restart` scheduled task) carries forward until the `asyncio.wait_for` timeout
wrapper lands (still a good idea, still not v2-specific).

---

## 8. Migration, cutover, and keeping v1 alive

### 8.1 Keep v1 running on Supabase during the build

- v1 uses the **MT5 Manager** (40 visible logins) + Supabase. That workload is small
  (thousands of deals, not the feed's ~20k/hr) — Supabase can carry it.
- **Restore and right-size the Supabase project.** Per the 2026-09 incident memo, the
  project was paused / unreachable. To run v1 as the live dealer tool through the v2
  build: unpause/restore it and, if it was struggling, bump to a **larger compute
  add-on**. This is *only* to keep v1 stable during the parallel period — v2 does not
  touch Supabase at all.
- v1 gets **no new features** during this window; it is frozen except for keep-alive.

### 8.2 Stand up v2 alongside (parallel run)

1. **Postgres up** on the CM server; apply v2 schema migrations.
2. **One-time config + history import** from v1 Supabase → local Postgres:
   - *Config (mandatory, exact):* `symbol_mappings`, all `equity_pnl_*` config,
     `login_groups(+members)`, `snapshot_schedules`, `bridge_settings`,
     `account_equity_snapshots` (incl. 2026-03-28 seed), `exposure_snapshots`,
     `alert_rules`, `moved_accounts`.
   - *History (archive):* `deals` — **trailing 12 months only** per the retention
     decision in §5.5 (NOT the full 280K+ archive); plus `bridge_executions`,
     `trade_audit_log`, `alert_events` in full (see §9.7). Export via Supabase →
     CSV/`COPY` import. Verify row counts **against the same 12-month window on both
     sides** so the comparison is apples-to-apples.
3. **Point v2 at the feed** (`LiveBridge:Url` + `LiveBridge__ApiKey`), single source
   to start. v2 streams new deals/positions/accounts/ticks into Postgres from the
   moment it connects; historical ranges are served from the imported archive.
4. **Verify to the penny.** Run v1 and v2 side by side and diff:
   - Live exposure per canonical symbol (net volume, hedge %).
   - Settled P&L for a fixed picker range across all four surfaces (Exposure closed
     row, P&L tab, Compare Full Table, Net P&L `bBook.settled`) — the v1 invariant
     was "identical to the penny"; hold v2 to the same bar.
   - Equity P&L per login vs MT5 Summary (v1 matched 39/40 penny-perfect).
   - Address the **symbol-mapping suffix gap** here: feed client symbols carry
     group suffixes (`UT100-20`) that need mapping rows or hedge reads 0%.

### 8.3 Cutover

- When v2 matches v1 within tolerance across a full trading day, switch dealers to v2
  (DNS / reverse-proxy target, or the `dealing.connecttrader.app` Caddy upstream).
- **Freeze v1 as a read-only archive:** stop v1's writers (or set its store
  read-only), keep the Supabase project reachable read-only for historical lookups
  for an agreed retention window, then export a final snapshot and downsize/pause the
  Supabase compute to stop the meter.
- **Rollback:** if v2 misbehaves within the first window, flip the proxy back to v1
  (still warm on Supabase). Because v2 only *reads* the feed and *writes* its own
  Postgres, rolling back has no data-loss coupling to v1.

---

## 9. Open decisions (need a call before/at build time)

1. **Postgres durability tier:** nightly `pg_dump` + WAL only, or add a streaming
   read-replica? (Supabase gave managed durability for free; local Postgres is now a
   single point of failure on the CM box.)
2. **Multi-source at launch** (`BBcorp-Live` only, multi designed-in) vs multi-source
   day one — drives whether `FeedClient` is instanced per source from v1.
3. **Feed↔db self-check** (§5.4): build the slimmed self-heal, or rely on synchronous
   commit + WAL and delete the reconciliation concept entirely?
4. ~~**Historical archive depth:** import all 280K+ deals, or only config + a rolling
   window, with the deep archive left queryable on the frozen v1?~~
   **RESOLVED 2026-09-12 — rolling 12 months.** Local Postgres stores a rolling
   12 months of closed deals; import scope and prune policy both follow that window.
   Deeper look-back will come from an on-demand, read-only, narrow API into the
   accounting system — **deferred, not built now**. Full policy in §5.5.
5. **Silent admin balance/credit moves:** v1 accepted losing these when it switched
   NetDepW/NetCred to deal-sum. Confirm the feed's admin-deal frames close that gap,
   or decide it stays out of scope.
6. **Retire vs slim `IMT5Api`:** delete the interface outright, or keep a minimal
   source-facing interface for testability (the `FakeFeedServer` test harness already
   exercises the feed contract and should carry over).
7. **Retention for the other history tables** (surfaced by §5.5): the 12-month
   decision covers closed `deals` only. Do `trade_audit_log`, `bridge_executions` and
   `alert_events` follow the same 12-month window, or keep full history (they are far
   smaller)? Until a call is made they import in full and are never pruned.

---

## 10. Phased build order

Each phase is independently shippable and leaves a working system.

- **Phase 0 — Postgres + migration tooling.**
  Stand up local Postgres, author v2 schema migrations (§5), build the v1→Postgres
  import/verify tooling. Deliverable: empty-but-correct schema + a repeatable import
  that reproduces v1 config and history with verified row counts.

- **Phase 1 — Storage layer swap (behind the same interfaces).**
  Introduce `PostgresService` mirroring `SupabaseService`'s surface; re-point every
  controller/service/engine writer. Re-express the four RPCs as local SQL. Ship with
  the store swapped but the **source still v1's** path if needed for A/B — or straight
  onto the feed if confident. Deliverable: the app runs on local Postgres.

- **Phase 2 — Source consolidation (feed only) + ingestion simplification.**
  Make `LiveBridgeApi` the sole source; delete the Manager connector, factory, native
  DLLs, bring-up dance, account/cash sync, auto-backfill, and the reconciliation
  sweep. Replace `DataSyncService` pacing with a bounded synchronous batch writer.
  `RequestDeals` reads history from Postgres. Deliverable: single-source, single-store
  v2 with ~half of v1's edge code removed.

- **Phase 3 — Parallel run + verification harness.**
  Run v2 next to v1; build the penny-diff checks (§8.2). Fix the symbol-suffix
  mapping gap. Deliverable: a signed-off verification report across exposure, all four
  settled-P&L surfaces, and Equity P&L per login.

- **Phase 4 — Cutover + v1 archive.**
  Flip dealers to v2, freeze v1 read-only, export final v1 snapshot, downsize Supabase
  compute. Deliverable: v2 in production, v1 as cold archive, rollback path documented.

- **Phase 5 — Cleanup.**
  Delete dead code paths, retire/slim `IMT5Api`, decide the self-check (§5.4/§9),
  finalize durability tier (§9), update `CLAUDE.md` and `docs/ARCHITECTURE.md` to the
  v2 shape. Deliverable: no v1 scaffolding left in the tree.

---

## 11. What explicitly does NOT change

- The React dashboard and every tab (Exposure, Positions, Compare, P&L, Net P&L,
  Equity P&L, Mappings, Alerts, Settings) and their behaviour.
- The REST + WebSocket API contract (`/api/exposure/*`, `/api/equity-pnl*`,
  `/api/compare/*`, `/api/bridge/*`, `/ws/exposure`, `/ws/bridge`), including the
  two-channel price/exposure split, `deal_settled` frames, and `permessage-deflate`.
- The Python coverage collector.
- The Centroid Bridge (coverage dropcopy) pairing/edge engines and Bridge tab.
- All domain math: net-exposure, to-cover, net-P&L inversion, calibrated-delta
  floating P&L, PS high-water-mark, Asia/Beirut date model.

The dealer should not be able to tell v2 from v1 by looking — only by the fact that it
no longer depends on the MT5 Manager or on Supabase.
