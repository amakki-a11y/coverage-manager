# Coverage Manager

## Project Description
Real-time exposure management system for forex dealing desks. Shows B-Book client positions alongside LP coverage positions with symbol mapping and contract size normalization.

## Architecture
```
B-Book MT5 Server → C# Manager API (event-driven) → C# Backend → WebSocket → React Dashboard
Coverage MT5 Terminal → Python Collector (poll 100ms) → HTTP POST → C# Backend ↗
                                                          ↕                       ↓
                                                   /deals endpoint          Supabase (async persist)
                                                   (closed deal history)
```

## GitHub Repository
https://github.com/amakki-a11y/coverage-manager

## Supabase
- **Project:** coverage-manager
- **Region:** eu-central-1 (Frankfurt)
- **URL:** https://svhmhcqopkdgccnzgvzp.supabase.co
- **Project ID:** svhmhcqopkdgccnzgvzp

## Tech Stack
- **Backend:** C# .NET 8 / ASP.NET Core (WebSocket + REST)
- **Coverage Collector:** Python FastAPI + MetaTrader5 library
- **Frontend:** React + TypeScript (Vite)
- **Database:** Supabase (PostgreSQL)
- **Real-time:** Raw WebSocket (throttled 10 updates/sec)

## Solution Structure
```
coverage-manager/
├── src/
│   ├── CoverageManager.Core/           # Domain models + engines
│   │   ├── Models/                     # Position, SymbolMapping, ExposureSummary, SymbolPnL, ClosedDeal, TradingAccount, DealRecord, TradeAuditEntry, PriceQuote, SymbolExposure, TradeRecord
│   │   └── Engines/                    # PositionManager, ExposureEngine, PriceCache, DealStore
│   ├── CoverageManager.Connector/      # MT5 Manager API connection
│   │   ├── IMT5Api.cs                  # Interface (Initialize, Connect, OnTick, OnDealAdd, GetPositions, GetUserLogins, GetUserAccount, RequestDeals)
│   │   ├── MT5ApiReal.cs               # Real MT5 Manager API implementation (#if MT5_API_AVAILABLE)
│   │   ├── MT5ManagerConnection.cs     # B-Book connection service (positions, deals, account sync)
│   │   ├── MT5CoverageConnection.cs    # Coverage connection (disabled — uses Python collector)
│   │   ├── RawTypes.cs                 # RawDeal, RawPosition, RawTick, RawAccount
│   │   └── Libs/                       # MetaQuotes native DLLs
│   ├── CoverageManager.Api/            # ASP.NET Core host
│   │   ├── Controllers/                # Coverage, Exposure, Compare, SymbolMapping, Accounts, Settings
│   │   └── Services/                   # SupabaseService, ExposureBroadcastService, DataSyncService
│   └── CoverageManager.Tests/          # MSTest unit tests (27 tests)
│       ├── ExposureEngineTests.cs
│       ├── PositionManagerTests.cs
│       ├── PriceCacheTests.cs
│       ├── SymbolMappingTests.cs
│       └── UnitTest1.cs
├── collector/                           # Python FastAPI collector (MT5 Terminal connection)
│   └── main.py                         # FastAPI app with /positions, /deals, /health endpoints
├── web/                                 # React + TypeScript + Vite dashboard
│   └── src/
│       ├── ThemeContext.tsx             # Dark/Light theme context provider
│       ├── theme.ts                    # Theme definitions
│       ├── types/index.ts              # Shared TypeScript types
│       ├── types/compare.ts            # SymbolExposure, TradeRecord types for Compare tab
│       ├── hooks/useExposureSocket.ts  # WebSocket hook for real-time exposure data
│       ├── hooks/usePositionsCompare.ts # Polling hook for Compare tab (500ms exposure, 5s trades)
│       ├── components/
│       │   ├── ExposureTable.tsx        # Main exposure grid (open + closed rows, date picker, sort, drag)
│       │   ├── PnLPanel.tsx            # P&L summary panel
│       │   ├── PositionsGrid.tsx       # Raw positions view (with open time column)
│       │   ├── TotalBar.tsx            # Footer totals bar (locale-formatted numbers)
│       │   ├── SettingsPanel.tsx       # Account settings UI
│       │   └── SymbolMappingAdmin.tsx  # Symbol mapping management
│       └── pages/
│           └── PositionsCompare/        # Compare tab — side-by-side client vs coverage analysis
│               ├── index.tsx            # Tab entry, layout shell (left panel + right panel)
│               ├── LeftPanel/
│               │   ├── index.tsx        # Compact list + expand/collapse + resizable drag handle
│               │   ├── SymbolRow.tsx    # Single row: symbol, hedge%, net CLI/COV/Δ, P&L CLI/COV/Δ
│               │   └── ExpandedTable.tsx # Full-width table matching Exposure layout (O/C rows, date picker, bid prices)
│               └── RightPanel/
│                   ├── index.tsx        # Empty state + detail view container
│                   ├── DetailHeader.tsx # Symbol name, vol, P&L, hedge pill
│                   ├── SummaryCards.tsx # 5 metric cards (Avg Entry, Avg Exit, Volume, P&L, Net Combined)
│                   ├── PriceChart.tsx   # Canvas price timeline chart (entry/exit markers, sparkline)
│                   ├── VolPnlChart.tsx  # Canvas volume bars + cumulative P&L lines
│                   └── CompareTable.tsx # Comparison metrics table (Trades, Volume, Win Rate, P&L)
├── CoverageManager.sln
└── CLAUDE.md
```

## Supabase Tables (18)
1. `symbol_mappings` — B-Book ↔ LP symbol mapping + contract sizes (also holds `pip_size` override for Bridge)
2. `positions` — Open positions snapshot
3. `deals` — Deal history with dedup on (source, deal_id), includes direction/fee/entry/**order_id**. 280K+ deals persisted.
4. `trading_accounts` — Mirror of all MT5 accounts (B-Book + Coverage), unique on (source, login). Auto-synced every 5min.
5. `trade_audit_log` — Tracks deal modifications (price, volume, profit changes) with old/new values
6. `exposure_snapshots` — Periodic exposure captures (floating P&L per symbol). Unique on `(canonical_symbol, snapshot_time)`, carries `trigger_type` (scheduled/manual/daily/weekly/monthly) and `label`. Feeds the Net P&L tab's "Begin" anchor.
7. `pl_summary` — P&L summary (Phase 2)
8. `hedge_executions` — Hedge audit trail (Phase 4)
9. `economic_events` — Economic calendar (Phase 3)
10. `risk_thresholds` — Risk limits per symbol (Phase 3)
11. `account_settings` — MT5 Manager and Coverage account credentials
12. `alert_rules` — Configurable alert thresholds (trigger type, operator, value, severity)
13. `alert_events` — Fired alert notifications (symbol, severity, threshold vs actual value, acknowledged)
14. `moved_accounts` — Logins removed from MT5 Manager, deals kept in Supabase but excluded from dashboard
15. `snapshot_schedules` — Dealer-configurable cadences for `exposure_snapshots` captures. Columns: `cadence` (daily/weekly/monthly/custom), `cron_expr`, `tz` (default `Asia/Beirut`), `enabled`, `last_run_at`, `next_run_at`. Seeded with Daily/Weekly/Monthly Lebanon-time rows.
16. `bridge_settings` — Centroid Bridge credentials + mode (Stub/Live/Replay) + enabled flag.
17. `bridge_executions` — Paired CLIENT ↔ COV_OUT rows from the Centroid Dropcopy feed; includes `client_mt_login/ticket/deal_id`.
18. `reconciliation_runs` — Audit log for the nightly deal-reconciliation sweep. Columns: `trigger_type` (scheduled/manual), `window_from/to`, `mt5_deal_count`, `supabase_deal_count`, `backfilled`, `ghost_deleted`, `modified`, `error`, `notes`.
19. `account_equity_snapshots` — Per-login point-in-time equity snapshot (balance, equity, credit, margin). Seeded from the Segregated HTML on 2026-03-28 and re-captured by `ExposureSnapshotService` on every scheduled tick. Backs the Equity P&L tab's **Begin Equity** column.
20. `equity_pnl_client_config` — Per-login rebate / PS configuration (`comm_rebate_pct`, `ps_pct`, `ps_contract_start`) plus engine-managed PS HWM state (`ps_cum_pl`, `ps_low_water_mark`, `ps_last_processed_month`).
21. `equity_pnl_spread_rebates` — Per-(login, canonical_symbol) spread rebate rate in USD per lot.
22. `login_groups` — Named groups for Phase-2 per-group rebate/PS config (e.g. `VIP-TierA`, `IB-Lebanon`).
23. `login_group_members` — Many-to-many (login ↔ group) with a `priority` field; highest priority wins when a login belongs to multiple groups.
24. `equity_pnl_group_config` — Per-group rebate / PS configuration. Login-specific rows in `equity_pnl_client_config` override this when both exist.
25. `equity_pnl_group_spread_rebates` — Per-group per-symbol spread rebate rate. Login-level rate (from `equity_pnl_spread_rebates`) overrides group rate for same symbol.

### Supabase functions
- `aggregate_bbook_settled_pnl(from_ts, to_ts, excluded_logins)` — server-side SQL aggregation used by the Net P&L tab. Normalizes canonical keys (strips `.c` / `.m` / trailing `-`, uppercases) and sums `profit + swap` on OUT deals + `commission + fee` on all trade deals. Replaces a 46s client-side "pull 200K rows and GROUP BY" roundtrip with a sub-second RPC.
- `aggregate_bbook_pnl_full(from_ts, to_ts, excluded_logins)` — full per-symbol aggregation for `/api/exposure/pnl` (Exposure tab's closed row, P&L tab, Compare Full Table). Returns deal count + profit/commission/swap/fee + total/buy/sell volume + net. Replaced an 81-second page-by-page GROUP BY over 121K rows with a sub-second RPC.
- `latest_snapshots_before(anchor)` — `DISTINCT ON (canonical_symbol)` scan over `exposure_snapshots` for the Net P&L tab's Begin anchor. Previously pulled the full 3.8K-row table every call; now returns ≤ 30 rows.

## MT5 Bring-up Order (non-negotiable)
On every connect / reconnect, [`MT5ManagerConnection.ExecuteAsync`](src/CoverageManager.Connector/MT5ManagerConnection.cs) follows a strict order:

1. `Initialize()` + `Connect()` to MT5 Manager.
2. `SelectedAddAll()` to enrol every symbol for tick streaming.
3. `GetUserLogins()` → assign `_logins` before any per-login query.
4. `SnapshotPositions(_logins)` → fill `PositionManager` with B-Book positions.
5. `BackfillDeals(_logins, from, UtcNow)` → fill `DealStore` with historical closes.
6. `SyncAccountsToSupabaseAsync(_logins)`.
7. **Then** subscribe to all four event sinks: `OnTick` / `SubscribeTicks()`, `OnDealAdd` / `SubscribeDeals()`, `OnPosition*` / `SubscribePositions()`, `OnUserUpdate` / `SubscribeUsers()`.

Subscribing to the sinks BEFORE steps 3–5 opens a race: the first live callback fires against an empty cache, producing WebSocket flickers with stale exposure and duplicate Supa upserts. The order is enforced by the call sequence and a prominent comment block at the call site — change it at your peril.

## Event-Driven MT5 (Stage 2b)
Position + user state are pushed by MT5 via `CIMTPositionSink` / `CIMTUserSink` rather than polled. The 500 ms position-snapshot poll is dropped to **60 s** and acts only as a reconciliation safety net — it logs `drift` (symmetric difference between event-cache state and MT5 snapshot) but normally finds zero. Event-cache writes are authoritative.

- **`PositionSinkHandler`** ([`MT5ApiReal.cs`](src/CoverageManager.Connector/MT5ApiReal.cs)) → `OnPositionAdd/Update/Delete` → `MT5ManagerConnection` upserts/removes the per-position entry in `PositionManager`.
- **`UserSinkHandler`** → `OnUserUpdate` → mirrors balance/credit changes for diagnostics. (Equity / margin still come from the periodic `GetUserAccount` poll because `CIMTUserSink` doesn't carry them.)
- **`AccountSyncIntervalMinutes = 15`** (was 5). Balance/credit arrive via events; the bulk sync only refreshes roster fields (group, leverage, comment) + equity/margin.
- **A/B verified empirically:** 500 ms poll = 4459 `getPositions` calls/min (40 logins) vs 60 s poll = 58 calls/min — **−98.7%** with zero drift in steady state.
- **Diagnostics endpoint:** `GET /api/exposure/diagnostics` — returns `{ stage, pollIntervalMs, snapshotCount, drift.{totalDriftPositions,pollsWithDrift}, positionEvents.{add,update,delete}, userEvents.update, tickEvents.{total,perMinute,lastAt}, apiCalls.{getPositions,getUserAccount,getUserLogins,requestDeals,tickLast}.{total,perMinute}, broadcasts.{fullState,priceOnly}.{total,perMinute} + priceOnly.coalescedTicks }`. Watch `drift.pollsWithDrift` after any change. `tickEvents.lastAt` getting older than ~5s during market hours = MT5 isn't delivering ticks (server-side, not the app). `broadcasts.priceOnly.coalescedTicks` is the SUCCESS metric — high values mean bursts are collapsing into single sends.

## Data Sync Architecture
- **DataSyncService** (background): Syncs deals to Supabase every 30s with change detection
- **On startup:** Loads deals from Supabase into in-memory DealStore (survives restarts)
- **Auto-backfill on startup:** Queries last deal time from Supabase, detects gap if app was down, backfills missed deals from MT5 Manager API automatically (no manual backfill needed)
- **Account sync:** MT5ManagerConnection syncs all trading accounts to Supabase on connect + every **15 minutes** (was 5 — see Event-Driven MT5 above)
- **Login refresh:** Periodically re-fetches `GetUserLogins()` to detect new accounts (every 15 min)
- **Cash-movement sync:** [`CashMovementSyncService`](src/CoverageManager.Api/Services/CashMovementSyncService.cs) — background timer, 15 min cadence, 7-day sliding window. Backfills MT5 admin balance/credit deals (`action ≥ 2`) into Supabase because `CIMTDealSink.OnDealAdd` doesn't reliably fire for admin transfers and `ReconciliationService.QueryDeals` filters to `action ≤ 1`. Without this loop, admin transfers stay invisible to the Equity P&L tab until a dealer manually clicks Settings → "Backfill Cash Movements".
- **Change detection:** Compares incoming deals vs stored, logs modifications to `trade_audit_log`
- **Batch upserts:** 500 records per batch for accounts and deals, with `on_conflict` for dedup
- **Supabase pagination:** GetDealsAsync pages through results (1000 rows/page) to handle large date ranges
- **DealStore.EarliestDealTime** — tracks the earliest UTC deal time held in memory (Interlocked CAS in `AddDeal` / `AddDeals`). The Equity P&L NetDepW override path (`ExposureController.cs:1050`) now consults this before letting the in-memory DealStore overwrite the Supabase trade-flow sum: an in-memory slice that doesn't cover the requested window would otherwise produce phantom NetDepW values for historical date ranges.

## Deal Volume Calculation
- **Volume includes both IN + OUT deals** to match MT5 Manager totals
- **P&L only from OUT deals** (Entry=1,2,3) — only closing deals carry profit/loss
- **Balance/credit deals are KEPT in DealStore** (Action >= 2) so the Equity P&L tab can source NetDepW + NetCred from `deals` directly. Trade-only consumers (`DealStore.GetPnLBySymbol`, `GetPnLByDay`) filter to `Action < 2` themselves.
- **Commission/Fee from ALL deals** (both IN + OUT may carry commission)
- **Symbol matching:** Canonical symbols (e.g., "US30") mapped from raw MT5 symbols ("US30-") by stripping trailing `-`/`.`

## Running Locally

### Backend
```bash
cd src/CoverageManager.Api
dotnet run
# Runs on http://localhost:5000
```

### Frontend
```bash
cd web
npm run dev
# Runs on http://localhost:5173
```

### Python Collector
```bash
cd collector
pip install -r requirements.txt
uvicorn main:app --host 0.0.0.0 --port 8100
# Connects to fXGROW coverage account (login 96900, server 194.164.176.137:443)
# Requires MT5 Terminal running with "Allow algorithmic trading" enabled
```

### Tests
```bash
dotnet test CoverageManager.sln
```

## Key Design Decisions
- **Event-driven B-Book:** MT5 Manager API deal callbacks, no polling
- **Polling Coverage:** Python collector polls MT5 terminal every 100ms, POSTs to backend
- **In-memory state:** ConcurrentDictionary for thread-safe position store
- **WebSocket push:** Two channels on the same `/ws/exposure` socket:
  - `exposure_update` (~10/sec, throttled): full state — exposure summaries, prices, P&L, alerts. Fires when something position/deal/alert-related changes (`MarkDirty`).
  - `price_update` (~20/sec, throttled): lightweight bid/ask snapshot. Fires on every MT5 tick (`MarkPriceDirty`). Bypasses the heavy `ExposureEngine.CalculateExposure()` recompute so the bid price under each symbol stays fresh even when the position book is large.
  - **Why two channels:** routing every tick through the full-state broadcast made bid prices wait for the next position event (3-5s on quiet symbols). Splitting them lets prices update at full tick cadence (~50ms) without recomputing every position's floating P&L on every tick.
  - **Coalescing:** `MarkPriceDirty` is idempotent within a frame — bursts of ticks (typical mid-active session = 500/sec) collapse into one ~50ms WS send. The `broadcasts.priceOnly.coalescedTicks` counter on `/api/exposure/diagnostics` reports the saved sends.
  - **Staleness UI:** the bid price shown under each symbol in the Exposure table dims to grey + shows an amber dot when the last tick is >3s old, and dims to red + 50% opacity when >10s old. Lets the dealer distinguish "price genuinely frozen because MT5 isn't sending ticks for this symbol right now" from "we have a fresh price". Source: [`web/src/components/ExposureTable.tsx`](web/src/components/ExposureTable.tsx) bid-price block.
- **Symbol normalization:** Contract size conversion (e.g., 1500 GOLD lots = 15 XAUUSD B-Book lots)
- **Coverage mirrors client direction:** Clients sell → broker hedges by selling on LP
- **Net Exposure:** `BBookNet - CoverageNet` (not addition, since coverage mirrors direction)
- **Net P&L:** `-ClientPnL + CoveragePnL` (invert client P&L for broker perspective)
- **To Cover:** `BBookNet - CoverageNet` → negative = SELL more, positive = BUY more
- **Deal callbacks trigger WebSocket:** `OnDealReceived` calls `_onUpdate()` so closed row updates in real-time
- **Supabase as source of truth for closed deals:** PnL endpoint queries Supabase directly (not in-memory DealStore) for date-filtered historical data
- **Deal verification:** `/api/exposure/verify` compares MT5 Manager deals vs Supabase per symbol (batched 1K logins). `fix=true` upserts missing deals. UI in Settings tab with risk warning.

## Exposure Table Layout
- **Two rows per symbol:** Open (live positions) + Closed (date-filtered deals, always shown)
- **Three sections:** Clients (blue) | Coverage (teal) | Summary
- **Closed row columns:** Buy Volume, Sell Volume, Total Volume, P&L
- **B-Book closed deals:** From `/api/exposure/pnl?from=&to=` (Supabase query with pagination)
- **Coverage closed deals:** From Python collector `/deals` endpoint (MT5 `history_deals_get`)
- **Date range picker:** Filters both B-Book and Coverage closed deals
- **Symbol mapping:** Coverage symbols (XAUUSD-, US30.c) → canonical → B-Book symbols via `/api/mappings`
- **To Cover column:** `BBookNet - CoverageNet` — green for buy (positive), red with `-` prefix for sell (negative), no BUY/SELL text
- **Grid toggle:** Optional vertical + horizontal grid lines (persisted in localStorage)
- **Bold symbol dividers:** Horizontal borders between symbol groups are 2px bold
- **Bid price:** Shown under each symbol name with direction colors (green up, red down)
- **Uniform font size:** All data cells use 12px
- **Sort & reorder:** Sort dropdown + drag-and-drop symbol reordering (persisted in localStorage)
- **Open/Closed labels:** "O" and "C" shorthand

## Positions Compare Tab
- **Split layout:** Resizable left panel (drag handle) + right detail panel
- **Left panel compact:** Each row shows Symbol | Hedge% | Net (CLI/COV/Δ) | P&L (CLI/COV/Δ)
- **Left panel expanded (Full Table):** Full table matching Exposure layout with Open/Closed rows, bid prices, date picker (shared with other tabs via `useDateRange`), To Cover, Hedge%, and a TOTAL footer row summing OPEN + CLOSED across CLIENTS/COVERAGE/SUMMARY.
- **Right panel detail:** Shows when a symbol is selected — DetailHeader, 5 SummaryCards, `PnLRings` widget, CompareTable.
- **PnLRings widget:** SVG concentric-rings view (inner = floating, outer = settled) with broker-edge math (`coverage − client`). Replaces the older Price + VolPnl canvas charts. Source: [`web/src/pages/PositionsCompare/RightPanel/PnLRings.tsx`](web/src/pages/PositionsCompare/RightPanel/PnLRings.tsx).
- **Drag reorder:** Symbols can be drag-reordered in compact mode (persisted to localStorage)
- **Panel resize:** Left panel width adjustable by dragging right edge (persisted to localStorage)
- **Hedge % colors:** green >= 80%, amber >= 50%, red < 50%
- **Entry Δ colors:** positive (client paid more) = red, negative = green
- **Data source:** Polls `/api/compare/exposure` every 500ms, `/api/compare/trades` every 5s (2-day rolling window so closed/settled values appear before today accumulates fills).

## Net P&L Tab (Period P&L)
Separate tab (kept alongside the original P&L tab) that computes full broker period P&L:
```
FloatingΔ = CurrentFloating − BeginFloating
Net       = FloatingΔ + Settled
Edge Net  = Coverage.Net − Clients.Net   (positive = broker profited)
```
### Mechanics
- **Begin anchor**: midnight Asia/Beirut of `fromDate` converted to UTC (21:00 DST / 22:00 standard). DST spring-forward handled with a 1-hour retry.
- **Begin source**: latest `exposure_snapshots` row per canonical symbol with `snapshot_time <= anchor`. If none exists, `beginFromSnapshot=false` and Begin treated as 0 (per decision; amber dot shown in UI).
- **Current floating**: live, calibrated-delta from tick prices via `ExposureEngine.CalculateExposure()` (which now takes a `PriceCache`). Per-position formula: `livePnL = (livePrice − OpenPrice) × (Position.Profit / (Position.CurrentPrice − OpenPrice))` — calibration coefficient derived from MT5's own numbers so it stays correct even when `SymbolMapping.BBookContractSize` is mis-configured (which is common for index/futures CFDs in our `symbol_mappings`). Falls back to `Position.Profit` when calibration can't be derived (zero profit, zero open delta, or PriceCache miss).
- **Live overlay (UI)**: `PeriodPnLPanel` subscribes to `useExposureSocket()` and overlays `currentFloating` from each WS frame onto the cached REST snapshot. Recomputes `floatingDelta`, `net`, `edge.{floating,net}` per row + propagates the delta to TOTAL. Begin + Settled stay REST-driven (snapshots / deal sums change rarely; REST poll slowed back to 30 s).
- **Settled**: `aggregate_bbook_settled_pnl` RPC for B-Book (Lebanon-time boundary) + Python collector `/deals` for Coverage.
- **Date pickers**: interpreted as Asia/Beirut throughout so ranges match MT5 terminal views.
- **Sentinel snapshots**: rows with `canonical_symbol` starting `__`, or containing `SEED_TOTAL` / `PORTFOLIO`, feed the TOTAL only (no per-symbol row). Lets a dealer seed portfolio-level Begin without per-symbol values.
- **"No position"**: rows whose live summary has 0 volume on a side render `—` for Begin/Current/ΔFloat on that side; Settled/Net still show real realized P&L.
- **Loading badge**: date-change fetches show a ≥450ms amber pill next to the date pickers; the 30 s silent refresh polls don't flash the badge.
- **Tab-revisit cache**: a module-level `periodCache` keyed by `(from,to)` survives component unmount. Revisiting the tab renders the previous result immediately (no "Loading…" flash); a background refresh updates it silently. First visit for a new range still shows the badge.

### Scheduler
- Three seeded schedules (Daily/Weekly/Monthly at 00:00 Asia/Beirut). Manage in Settings tab → "Snapshot Schedules".
- `ExposureSnapshotService` (BackgroundService) ticks every 60s, fires due schedules via `Cronos` (DST-aware cron parser), upserts one row per symbol into `exposure_snapshots`.
- "Capture Snapshot Now" button on the Net P&L toolbar posts `/api/exposure/snapshot`.
- "Snapshot History" section in Settings lists recent captures grouped by `snapshot_time` with expandable per-symbol breakdown.

### File inventory
- `supabase/migrations/20260418_exposure_snapshots.sql` — unique constraint + trigger_type + label + `snapshot_schedules` table + seed rows.
- `supabase/migrations/aggregate_bbook_settled_pnl_fn` (applied inline) — SQL aggregation function.
- `src/CoverageManager.Core/Models/{ExposureSnapshot,SnapshotSchedule,PeriodPnLRow}.cs` — DTOs.
- `src/CoverageManager.Api/Services/ExposureSnapshotService.cs` — scheduler dispatcher + `RunNowAsync`.
- `src/CoverageManager.Api/Services/SupabaseService.cs` — `UpsertExposureSnapshotsAsync`, `GetNearestSnapshotsBeforeAsync`, `AggregateBBookSettledPnlAsync`, schedule CRUD.
- `src/CoverageManager.Api/Controllers/ExposureController.cs` — `GetPeriodPnL`, `CaptureSnapshotNow`, `ListSnapshots`.
- `src/CoverageManager.Api/Controllers/SnapshotSchedulesController.cs` — scheduler CRUD + run-now.
- `web/src/components/PeriodPnLPanel.tsx` — tab component.
- `web/src/components/SettingsPanel.tsx` — adds `SnapshotSchedulesCard` + `SnapshotHistoryCard`.
- `web/src/types/index.ts` — `PeriodPnL*`, `SnapshotSchedule`, `ExposureSnapshot` types.

### NuGet
- `Cronos 0.10.0` — cron + DST-aware timezone handling for schedule dispatch.

## Equity P&L Tab
Per-login decomposition of the account's equity move across a date range.
Separate from Net P&L — this is the "accountant's" view (balance-reconciled)
rather than the "dealer's" view (exposure-driven).

### Columns
`Login, Begin Eq, Net Cash Movement, Comm Reb, Spread Reb, Adj, PS, Supposed Eq, Current Eq, PL, Net PL`

The previously-separate **Net Dep/W** and **Net Cred** columns are collapsed into a single **Net Cash Movement** column on the dashboard. Per-login (NetDepW + NetCred) matches MT5 Summary's (In/Out + Credit) penny-perfect on 39 of 40 logins; the per-column split differs because MT5 splits balance/credit transfers across the two columns using internal ledger semantics that aren't exposed via `RequestDeals`. The combined view sidesteps the categorization mismatch. Cell tooltip shows the underlying split for diagnostics. Backend API still returns `netDepositWithdraw` + `netCredit` separately.

### Math
```
Supposed Eq      = Begin + NetDep + NetCred
NetDepW          = Σ profit of action=2 (BALANCE) deals in window   // matches MT5 Summary's Deposit
NetCred          = Σ profit of action=3 (CREDIT)  deals in window   // matches MT5 Summary's Credit
PL               = CurrentEq − Supposed Eq
NetPL (client)   = PL − CommReb − SpreadReb − Adj − PS
NetPL (coverage) = PL
Broker Edge      = −Σ(Clients.NetPL) + Σ(Coverage.NetPL)
```

### Critical data-path rules (MT5 quirks we had to work around)
- **MT5 `RequestDeals` does NOT surface admin balance/credit transfers** — the dealer can move value between Balance and Credit buckets via MT5 Manager's admin tools and no deal row is emitted. Earlier we computed Net Dep/W and Net Credit via **balance reconciliation** (current − begin − trade flow) to capture those silent moves, but the dealer team treats MT5 Manager as the source of truth — so the panel now uses **deal-sum** (`Σ action=2` for NetDepW, `Σ action=3` for NetCred) which matches MT5 Summary 1:1. Trade-off: silent admin Edit-Account moves are no longer reflected anywhere; if that becomes a problem, surface it in a separate column rather than mixing it into NetDepW/NetCred.
- **Trade-flow filter** in `SumTradeBalanceFlowPerLoginAsync` uses `action not in (2,3)` — everything except BALANCE and CREDIT deals contributes to trade flow, including broker-specific action codes (we observed action=19 in this dataset). If we filtered `action<2` we'd leak small deal types into the implicit deposit calculation.
- **Every writer that builds `DealRecord` MUST use `Action = (int)d.Action`** (preserve MT5's real action code) rather than deriving from `Direction`. The latter was the default before Equity P&L existed — when balance/credit deals started flowing, it clobbered `action=3` to `action=1` (SELL) on every sync. All 5 sites fixed: [DataSyncService.cs:167](src/CoverageManager.Api/Services/DataSyncService.cs:167), [ReconciliationService.cs:174](src/CoverageManager.Api/Services/ReconciliationService.cs:174), [ReconciliationService.cs:205](src/CoverageManager.Api/Services/ReconciliationService.cs:205), [ExposureController.cs:303](src/CoverageManager.Api/Controllers/ExposureController.cs:303), [ExposureController.cs:484](src/CoverageManager.Api/Controllers/ExposureController.cs:484).
- **MT5 Manager's `GetUserLogins('*')` scope is permission-limited** — HR sub-accounts (e.g. `5222, 5231, 5237, …`) in a different group are invisible to the Manager login we use. They don't appear in `trading_accounts`, and `UserGet(5237)` returns `MT_RET_ERR_NOT_FOUND`. The Segregated report also respects this scope. The Equity P&L tab therefore shows only the 40 logins the Manager can see. Fix would require a second Manager connection with broader permissions.
- **No coverage section today.** Python collector has no `/account` endpoint; `trading_accounts` never gets coverage rows populated. To add coverage P&L we'd need a collector endpoint that returns the LP account's balance/credit/equity and a sync path in `MT5ManagerConnection` or `DataSyncService` that calls it.

### Phase 2 — per-group rebate/PS config
Instead of setting rates per-login for every account, dealer creates named groups (e.g. `VIP-TierA`, `IB-Lebanon`) and assigns logins to them. The endpoint resolves effective rate as:
```
login-specific override → group config (highest priority) → 0 (default)
```

**Tables:** `login_groups` · `login_group_members(priority breaks ties)` · `equity_pnl_group_config` · `equity_pnl_group_spread_rebates`.

**Perf note:** trade deals are only fetched for **rebate-eligible logins** — a login is eligible if it has a direct config, a direct spread-rate row, or belongs to a group that has config / spread rates. Keeps the endpoint sub-second even on full-period queries.

### File inventory
- `supabase/migrations/20260419_equity_pnl.sql` + `20260419_equity_pnl_phase2_groups` (applied inline) — tables and triggers.
- `src/CoverageManager.Core/Models/EquityPnL/*.cs` — DTOs: `EquityPnLRow`, `AccountEquitySnapshot`, `EquityPnLClientConfig`, `SpreadRebateRate`, `LoginGroup`, `LoginGroupMember`, `EquityPnLGroupConfig`, `GroupSpreadRebateRate`.
- `src/CoverageManager.Core/Engines/EquityPnLEngine.cs` — deal classification + client/coverage Net PL convention.
- `src/CoverageManager.Core/Engines/PsHighWaterMarkEngine.cs` — reverse HWM (loss-share) PS engine with idempotent month-walk.
- `src/CoverageManager.Api/Services/SupabaseService.cs` — adds ~15 accessors for snapshots, config, rebates, groups, group members, group config, group rates, plus `SumTradeBalanceFlowPerLoginAsync`, `GetTradeDealsForLoginsAsync`, `GetNonTradeDealsAsync`.
- `src/CoverageManager.Api/Services/ExposureSnapshotService.cs` — extended to also write `account_equity_snapshots` on every scheduled tick.
- `src/CoverageManager.Api/Controllers/ExposureController.cs` — `GET /api/equity-pnl`, `POST /api/equity-pnl/backfill-cash-movements`, `GET /api/equity-pnl/account-live`.
- `src/CoverageManager.Api/Controllers/EquityPnLConfigController.cs` — per-login CRUD.
- `src/CoverageManager.Api/Controllers/LoginGroupsController.cs` — Phase 2 groups CRUD.
- `src/CoverageManager.Connector/{MT5ApiReal,MT5ManagerConnection}.cs` — adds `Credit` to `RawAccount`/`TradingAccount` syncs so Net Credit reconciliation can compute `Current − Begin`.
- `web/src/components/EquityPnLPanel.tsx` — the 12-column tab with sticky header, date-range picker, loading badge, UNMAPPED badges.
- `web/src/components/EquityPnLClientConfigCard.tsx` + `SpreadRebatesCard.tsx` + `LoginGroupsCard.tsx` — Settings UI under the Equity P&L sub-tab.

### API endpoints
- `GET  /api/equity-pnl?from=&to=` — returns `{clientRows, coverageRows, clientsTotal, coverageTotal, brokerEdge}`
- `POST /api/equity-pnl/backfill-cash-movements?from=&to=` — one-shot MT5 scan to persist balance/credit/correction deals that ingestion may have missed (300ms/login pacing)
- `GET  /api/equity-pnl/account-live?login=N` — diagnostic: read an account's live MT5 balance/credit/equity
- `GET|PUT /api/equity-pnl-config` — per-login rebate/PS config
- `GET|PUT|DELETE /api/equity-pnl-config/spread-rebates` — per-login per-symbol spread rebate
- `GET|POST|DELETE /api/login-groups[/{id}]` — group CRUD
- `GET|POST|DELETE /api/login-groups/{id}/members` — group membership
- `GET|PUT /api/login-groups/config` — per-group rebate/PS config
- `GET|PUT|DELETE /api/login-groups/{id}/spread-rebates` — per-group spread-rebate rates

## Date + Timezone Model (important)
All dealer-facing dates and times are interpreted and displayed in **Asia/Beirut** (MT5 server TZ). Stored data stays UTC (Postgres standard) — conversion happens at query + render time.

- **Every date picker** (Exposure, P&L, Net P&L, Compare Full Table) is interpreted as Beirut midnight and converted to UTC for the Supa query (DST-aware, spring-forward retries +1h).
- **Shared date state**: [`web/src/hooks/useDateRange.ts`](web/src/hooks/useDateRange.ts) backs the picker on every tab with a single localStorage-keyed range. Switching tabs keeps the range; a change in one tab updates the others instantly via the `storage` event.
- **Every timestamp rendered in the UI** (position open time, snapshot history, reconciliation runs, alert times, Bridge exec times, Markup match times) uses the [`formatBeirut` / `formatBeirutDate` / `formatBeirutTime`](web/src/utils/time.ts) helpers — not browser-local (which on the dealer's Windows machine is PDT).
- **All four settled-P&L surfaces** (Exposure closed row, P&L tab, Compare Full Table closed row, Net P&L `bBook.settled`) return identical sums for the same picker range — verified to the penny (e.g. 03-29 → 04-18 = −$481,278.05 on all four).

## Deal Reconciliation Sweep (DATA-101)
Nightly (02:05 UTC) + manual "Run Now" in Settings. Reconciles Supabase `deals` vs MT5 Manager's authoritative set for a rolling 14-day window.

### What it does
1. **Backfill**: MT5 deals missing from Supa → upsert.
2. **Modifications**: Deals in both where fields differ → log to `trade_audit_log` + upsert.
3. **Ghost deletion**: Supa rows absent from MT5 → DELETE (+ evict from in-memory `DealStore` so DataSyncService's 30s tick doesn't resurrect them).

### Timezone workaround
MT5's `DealRequest` interprets the request window as **server-local time**, not UTC — which shifted result sets several hours relative to Supa's UTC window and produced thousands of false-positive "ghosts" on both edges. The sweep now queries MT5 with a **±24h buffer** then filters both sides to UTC `deal_time ∈ [fromUtc, toUtc)`. Apples-to-apples regardless of server TZ. Verified empirically: clean data gives `mt5_deal_count == supabase_deal_count` with zero backfills and zero ghosts on repeat runs.

### Files
- `supabase/migrations/20260418_reconciliation_runs.sql` — audit table.
- `src/CoverageManager.Core/Models/ReconciliationRun.cs` — DTO.
- `src/CoverageManager.Core/Engines/DealStore.cs` — `RemoveDeals(ids)` for ghost eviction.
- `src/CoverageManager.Api/Services/ReconciliationService.cs` — `BackgroundService` + `RunNowAsync`. Seeds `lastNightlyRun` from Supa so restarts after 02:05 don't re-fire.
- `src/CoverageManager.Api/Controllers/ReconciliationController.cs` — `GET /status`, `POST /run`.
- `web/src/components/SettingsPanel.tsx` — `ReconciliationCard` (history + Run Now button, Beirut times).

## UI Features
- **Dark/Light theme toggle:** ThemeContext with mutable THEME object (Object.assign pattern)
- **Theme persistence:** Saved to localStorage, applied on load
- **Theme-reactive styles:** All theme-dependent styles computed inside components (not module-level) to update on theme toggle
- **P&L Panel:** Shows client perspective (positive = clients profited), NOT inverted for broker view
- **Coverage P&L:** Respects date range picker
- **TotalBar numbers:** Locale-formatted with thousand separators for readability
- **Positions grid:** Open time column, single volume column (removed redundant normalized column)
- **Case-insensitive symbol lookup:** Price and closed deal lookups use toUpperCase() fallback (handles MT5 mixed-case like Ut100- vs UT100-)
- **Hedge ratio uncapped:** HedgeRatio can exceed 100% when coverage exceeds client exposure
- **Color semantics:** Documented in [`theme.ts`](web/src/theme.ts) at the top. green=positive, red=negative, amber=warning, blue=informational, teal=accent/secondary (used for Coverage group). `t1`/`t2`/`t3` are the ONLY foreground greys — never use a semantic color in place of them. Audit completed; removed legacy `#FF8A80` red-ish coverage accent in favor of `THEME.teal`.

### Dealer-facing shell components
- **RiskBanner** ([`web/src/components/RiskBanner.tsx`](web/src/components/RiskBanner.tsx)) — top-of-page watchdog. Amber/red banner when total `|netVolume|` crosses dealer-set thresholds (default 50 / 150 lots) OR when any meaningful-volume symbol sits below 80% hedged. Inline "Thresholds" control, values persisted to localStorage.
- **ConnectionHealthDots** ([`web/src/components/ConnectionHealthDots.tsx`](web/src/components/ConnectionHealthDots.tsx) + [`useConnectionHealth.ts`](web/src/hooks/useConnectionHealth.ts)) — 4 dots (MT5 / Collector / Centroid / Supabase) in the top bar. Polls each upstream every 5s (MT5 via `/api/exposure/status`, Collector via `/health`, Centroid via `/api/bridge/health`, Supabase via `/api/mappings` canary). Green/amber/red/gray tooltips.
- **DateRangePicker** ([`web/src/components/DateRangePicker.tsx`](web/src/components/DateRangePicker.tsx)) — shared `from`/`to` inputs backed by `useDateRange` + preset buttons (Today / Yesterday / Week / MTD / 7D / 30D) + a teal "BEIRUT" TZ pill. Keyboard shortcuts `T`/`Y`/`W`/`M` fire when focus is outside inputs. Mounted in ExposureTable; other tabs read the same `useDateRange` state so changes propagate.
- **KeyboardShortcutsOverlay** ([`web/src/components/KeyboardShortcutsOverlay.tsx`](web/src/components/KeyboardShortcutsOverlay.tsx)) — `?` anywhere opens a cheat-sheet of all app-wide shortcuts; Esc closes.
- **ConfirmDialog** ([`web/src/components/ConfirmDialog.tsx`](web/src/components/ConfirmDialog.tsx)) — standard modal used before destructive actions (delete mapping / account / schedule, deal backfill). Enter/Escape shortcuts; overlays use `THEME.shadowOverlay`.
- **ErrorToastProvider** ([`web/src/components/ErrorToast.tsx`](web/src/components/ErrorToast.tsx)) — global, throttled, dedupe-within-3s error toast. Components opt in via `const { showError } = useErrorToast()` instead of empty `catch { /* ignore */ }`.
- **StaleWrapper + Skeleton** ([`web/src/components/Skeleton.tsx`](web/src/components/Skeleton.tsx)) — when the exposure WebSocket drops, the main content dims + gets a diagonal amber hatch so the dealer can see the data is stale. `Skeleton` / `SkeletonRows` render shimmer placeholders for in-flight tables.
- **FlashingCell + useFlashOnChange** ([`web/src/components/FlashingCell.tsx`](web/src/components/FlashingCell.tsx) + [`web/src/hooks/useFlashOnChange.ts`](web/src/hooks/useFlashOnChange.ts)) — table cells that tint green/red for 800ms when a monitored number ticks up/down. Wired into the OPEN-row B-Book / Coverage / Net P&L cells of the Exposure table.
- **HedgeBar** ([`web/src/components/HedgeBar.tsx`](web/src/components/HedgeBar.tsx)) — thin horizontal bar under the hedge % cell (green ≥80%, amber ≥50%, red <50%). Width saturates at 100% so over-hedges stay visually full.
- **UNMAPPED badge** — amber pill rendered next to the symbol name in ExposureTable when the canonical symbol is missing from `symbol_mappings`. **Click the badge** to jump to the Mappings tab; the canonical is stashed in `localStorage.mappings.focusSymbol` and picked up by `SymbolMappingAdmin` on mount to pre-fill the new-mapping form.
- **SymbolBadge** ([`web/src/components/SymbolBadge.tsx`](web/src/components/SymbolBadge.tsx)) — 2-letter color-coded chip beside each symbol in the Exposure table. Classifier: metals=amber (XAU/XAG/XPD/XPT), indices=blue (US30/NAS/UK100/DAX/NIK/HSI/…), energy=teal (USOIL/WTI/BRENT/NG), crypto=purple (BTC/ETH/LTC/…), FX=grey.
- **Settings sub-tabs** — the Settings page is organized into 5 sub-tabs (**Connections · Equity P&L · Snapshots · Data Integrity · Reference**) instead of one long scroll. Sub-tab selection persisted in `localStorage`.

### Shell (Phase 2.12 redesign)
- **Sidebar** ([`web/src/shell/Sidebar.tsx`](web/src/shell/Sidebar.tsx)) — left nav with three labelled sections (Real-time / P&L / Config) + numbered kbd shortcuts 1-6 + alert pill + collapse toggle persisted to `localStorage.sidebar.collapsed`. Nine tabs active: Exposure / Positions / Compare · P&L / Net P&L / Equity P&L · Mappings / Alerts / Settings. Bridge and Markup tabs hidden from the dealer shell in the current build — underlying components + routes + WebSocket feed untouched.
- **Topbar** ([`web/src/shell/Topbar.tsx`](web/src/shell/Topbar.tsx)) — three metric tiles (Client Exposure / Coverage / Net P&L Today) fed from live `ExposureSummary[]`, plus 4-dot `ConnectionHealthDots`, ⌘K search stub, alert bell, guide button, theme toggle, tweaks drawer icon. No "Active Clients" / "Open Positions" tiles — not backend-computed.
- **CommandPalette** ([`web/src/shell/CommandPalette.tsx`](web/src/shell/CommandPalette.tsx)) — Cmd/Ctrl-K fuzzy jump-to-tab + quick actions (open guide, open alert history, toggle theme). Arrow keys navigate, Enter fires, Esc closes. Mounted at App root so it can fire from any focus context.
- **TweaksPanel** ([`web/src/shell/TweaksPanel.tsx`](web/src/shell/TweaksPanel.tsx)) — right-edge slide-out with Accent (blue/teal/purple), Density (compact/cozy/spacious), Grid-lines and Animate-ticks toggles. State persisted to `localStorage.tweaks`; accent/density mirrored to `<html data-accent>` / `<html data-density>` so CSS rules in `styles-extra.css` can respond without JS.
- **EquityPnLPage** ([`web/src/components/EquityPnLPage.tsx`](web/src/components/EquityPnLPage.tsx)) — wraps `EquityPnLPanel` with three sub-tabs: **Table** (per-login breakdown) · **Login Groups** (re-uses `LoginGroupsCard`) · **Spread Rebates** (re-uses `SpreadRebatesCard`). Sub-tab selection persisted to `localStorage.equitypnl.sub`. Settings → Equity P&L sub-tab still works for discoverability.
- **Design tokens** — `web/src/styles/styles.css` holds the CSS custom property palette (`--bg`, `--t1`, `--green`, `--accent`, …); `styles-extra.css` adds sidebar/topbar/palette classes. `DARK_THEME` / `LIGHT_THEME` hex values in `theme.ts` are kept in sync with the CSS so JS-inline-styled legacy components render in the same palette.
- **Keyboard shortcuts** wired at App root: `1-6` = tabs in sidebar order, `Cmd/Ctrl-K` = command palette, `?` = shortcut overlay, `T/Y/W/M` = date-range presets (when focus is outside an input). Full cheat-sheet in `docs/USER_GUIDE.md §4`.
- **Design reference files** live in `./design-ref/` (gitignored-worthy but currently tracked): original `App.jsx`, `Shell.jsx`, `NewTabs.jsx`, `MoreTabs.jsx`, `ExposureTable.jsx`, `HedgeModal.jsx`, `Tabs.jsx`, `bundle.jsx`, `styles.css`, `styles-extra.css`, `icons.jsx`, and screenshots. Kept for provenance; not imported by the build.

## API Endpoints
### C# Backend (port 5000)
- `GET /api/exposure/summary` — Live exposure aggregation (WebSocket also available)
- `GET /api/exposure/pnl?from=&to=` — B-Book realized P&L by symbol. Dates interpreted as Asia/Beirut; runs on `aggregate_bbook_pnl_full` RPC (sub-second on 121K-row / 21-day windows — was 81s pre-RPC).
- `GET /api/exposure/report?from=&to=` — Manager-style summary report (by symbol, login, day)
- `GET /api/exposure/positions` — All open positions
- `GET /api/exposure/status` — MT5 connection status
- `GET /api/exposure/deals` — All closed deals from in-memory DealStore
- `POST /api/exposure/pnl/reload?from=&to=` — Reload deals from MT5 Manager for date range
- `GET /api/mappings` — Symbol mapping table (B-Book ↔ Coverage)
- `GET /api/accounts` — List trading accounts from Supabase
- `GET /api/accounts/audit` — Query trade audit log
- `GET /api/accounts/deals?source=&from=&to=` — Query historical deals from Supabase
- `POST /api/accounts/backfill-deals?from=&to=` — Backfill deals from MT5 to Supabase
- `GET /api/exposure/verify?from=&to=&fix=false` — Compare MT5 Manager deals vs Supabase (batched 1K logins). `fix=true` upserts missing deals.
- `GET /api/markup/match?from=&to=` — Aggregates client vs coverage deals per canonical symbol; returns broker mark-up (Cov P&L − Client P&L) and VWAP price edge.
- `GET /api/compare/exposure` — Full snapshot of symbol exposures for Compare tab
- `GET /api/compare/trades?symbol=&from=&to=` — Trade history for the Compare tab. Merges B-Book (client) deals from Supabase with coverage deals from the Python collector, tagged `side: 'client' | 'coverage'`. Dates in Asia/Beirut.
- `GET /api/reconciliation/status?limit=N` — Recent deal-reconciliation runs (for the Settings `ReconciliationCard`).
- `POST /api/reconciliation/run` — Manually trigger a sweep. Body `{ fromUtc?, toUtc? }`, defaults to last 14 days.
- `GET /api/bridge/executions?from=&to=&symbol=&limit=` — Bridge ExecutionPairs (persisted first, live store fallback). UTC date range.
- `GET /api/bridge/live?limit=` — In-memory ExecutionPairs currently tracked by BridgeExecutionStore.
- `GET /api/bridge/health` — Centroid feed state (mode, connection, messages received, pairs in memory).
- `GET /api/exposure/pnl/period?from=&to=` — Net P&L with floating decomposition per symbol. Dates interpreted as Asia/Beirut local (DST-aware). Uses `aggregate_bbook_settled_pnl` RPC for fast settlement sum. Returns per-symbol `{bBook, coverage, edge}` each with `beginFloating`, `currentFloating`, `floatingDelta`, `settled`, `net`, `beginFromSnapshot`, `hasOpenPosition`. `edge.net = coverage.net − bbook.net` (broker P&L convention).
- `POST /api/exposure/snapshot` — Immediate manual snapshot capture. Body `{ "label": "..." }`.
- `GET /api/exposure/snapshots?from=&to=&symbol=` — Raw snapshot history for diagnostics UI.
- `GET /api/snapshot-schedules` (+ POST/PUT/DELETE + POST `{id}/run-now`) — Scheduler CRUD for `snapshot_schedules`.
- `WS /ws/exposure` — Real-time push for the dashboard. Two message types coexist on the same socket:
  - `{"type":"exposure_update","data":{exposure, prices, pnl, alerts, alertCount, timestamp}}` — full state. Throttled to 10/sec. Fires when something position/deal/alert-related changes.
  - `{"type":"price_update","data":{prices, floatingPnls, timestamp}}` — lightweight price-only frame. Throttled to 20/sec. Fires on every MT5 tick (idempotent — bursts coalesce into the next 50 ms slot). `floatingPnls: [{canonicalSymbol, bBook, coverage}]` (Phase 2.17) lets the frontend overlay live floating P&L onto `exposureSummaries` so the Exposure open-row P&L cells, Net P&L tab "Current Floating", and Topbar "Net P&L Today" tile tick at the same 20 Hz cadence as the bid prices — without paying the heavy per-position exposure recompute cost.
- `WS /ws/bridge` — Real-time ExecutionPair updates from the Centroid Dropcopy feed (Phase 2.5 Bridge tab).

### Python Collector (port 8100)
- `GET /positions` — Current coverage open positions
- `GET /deals?from=YYYY-MM-DD&to=YYYY-MM-DD` — Coverage closed deal history (aggregated)
- `GET /deals/raw?from=YYYY-MM-DD&to=YYYY-MM-DD` — Individual coverage deals with ticket/order/**external_id**/magic/price/time (used by Markup + Bridge tabs)
- `GET /health` — Connection status + account info

## Phase 2.5 — Bridge Execution Analysis Tab
CLIENT fills paired with COV OUT coverage legs from the Centroid CS 360 Dropcopy feed (FIX 4.4, see `/docs/centroid/`).

### Centroid facts
- CS 360 exposes **no REST API**. Post-trade data comes via **FIX 4.4 Dropcopy** Execution Reports, or via a paid Postgres DB replica.
- The Bridge tab consumes the Dropcopy feed via a single persistent FIX session.
- Connecting requires IP whitelisting plus credentials issued by Centroid support.
- All times UTC, μs precision. Correlation between CLIENT and COV OUT legs is by FIX tag 37 (OrderID) = "Cen Ord ID".

### Runtime modes (`Centroid:Mode` in `appsettings.json`)
- **Stub** (default today) — synthetic `BridgeDeal` stream so UI/pairing can run without real creds.
- **Live** — QuickFIX/n-backed session. NOT implemented until the dependency is approved and creds arrive.
- **Replay** — file-based playback for integration tests. NOT implemented yet.

### File inventory
- `docs/centroid/README.md`, `docs/centroid/dropcopy-fix-4.4.md`, `docs/centroid/database-specification.md` — captured API reference.
- `supabase/migrations/20260416_bridge_executions.sql` — schema + indexes + `pip_size` column on `symbol_mappings`.
- `src/CoverageManager.Core/Models/Bridge/{BridgeDeal,CovFill,ExecutionPair}.cs` — pure domain types.
- `src/CoverageManager.Core/Engines/BridgePairingEngine.cs` — pure reconciliation (time window, canonical symbol, greedy by |time_diff|, volume-weighted VWAP, signed edge).
- `src/CoverageManager.Core/Engines/BridgePipResolver.cs` — pluggable pip-size lookup (overrides > symbol-name rules > price heuristic).
- `src/CoverageManager.Api/Services/ICentroidBridgeService.cs` + `StubCentroidBridgeService.cs` — feed abstraction with a synthetic generator.
- `src/CoverageManager.Api/Services/BridgeExecutionStore.cs` — thread-safe in-memory state machine; handles pre-hedge (negative time diff) and late-arriving CLIENT fills.
- `src/CoverageManager.Api/Services/BridgeSupabaseWriter.cs` — UPSERT into `bridge_executions` on pair update.
- `src/CoverageManager.Api/Services/BridgeBroadcastService.cs` — `/ws/bridge` WebSocket hub.
- `src/CoverageManager.Api/Workers/BridgeExecutionWorker.cs` — subscribes to feed, normalizes symbol + CLIENT/COV_OUT, feeds store, persists + broadcasts.
- `src/CoverageManager.Api/Controllers/BridgeController.cs` — `/api/bridge/executions`, `/live`, `/health`.
- `src/CoverageManager.Tests/BridgePairingTests.cs`, `BridgeEdgeCalculationTests.cs` — 21 passing unit tests covering split, partial, over, pre-hedge, out-of-window, symbol-mapping, side mismatch, greedy-by-|diff|, unclassified, per-asset pip conversion.
- `web/src/types/bridge.ts`, `web/src/hooks/useBridgeSocket.ts`, `web/src/pages/Bridge/{index,BridgeFilters,BridgeTable,pipSize}.tsx|ts` — React tab, filters, stacked CLIENT/COV OUT table.

### UI
- Filters: UTC date range, symbol dropdown, side (ALL/BUY/SELL), anomaly-only toggle (no coverage leg OR `|pips| > 10`).
- Table: one CLIENT row + N COV OUT rows with shared SYMBOL, PRICE EDGE, PIPS cells via `rowSpan`. Thin border only between deal groups.
- Colors: CLIENT=blue, COV OUT=teal, BUY=green, SELL=red; time-diff green ≤500ms / amber ≤2000ms / red beyond; edge red when negative.
- Live updates: `/ws/bridge` merges pairs by `clientDealId` and keeps the list capped at 500.

### Supabase
`bridge_executions`:
- Unique on `client_deal_id`; indexed on `(symbol, client_time desc)` and `client_time desc`.
- `coverage_ratio` is a stored generated column.
- `cov_fills` is a jsonb array of `{dealId, volume, price, time, timeDiffMs, lpName?}`.
- Extra column `pip_size` added to `symbol_mappings` for per-symbol pip overrides.

## Operational Learnings (from first production deploy, 2026-04-20)

Notes discovered in production that future contributors should internalize. Full deploy runbook with workarounds is in [`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md); this section documents the *why*.

### Collector hangs silently
The Python collector uses the synchronous `MetaTrader5` C wrapper on the asyncio event loop. Any `mt5.*` call can block the loop indefinitely under MT5 server hiccups — port stays in "Listen" state but `accept()` never fires. NSSM doesn't detect this (process is alive, just deaf). Dealers see the collector health-dot go red and stay red until someone manually `nssm restart`s.

**Planned fix:** wrap every `mt5.*` call as `await asyncio.wait_for(asyncio.to_thread(fn, ...), timeout=2.0)`. Event loop survives; on timeout we log + return sentinel + keep serving.

**Stopgap until then:** a Windows Scheduled Task probing `/health` every 30s with a 3-sec timeout, triggering `nssm restart` after 3 consecutive failures. Bounds outage to ~90s.

### Equity P&L NetDepW race
`NetDepW = (CurrentBalance − BeginBalance) − Σ(trade-flow)`:
- `CurrentBalance` is live from MT5 Manager (sub-second).
- `Σ(trade-flow)` is from Supabase `deals` via `SumTradeBalanceFlowPerLoginAsync` — lagged 30s by `DataSyncService`.

When a trade closes, `CurrentBalance` jumps immediately but `trade-flow` doesn't catch up for ~30s. The calculation then appears to show a phantom "deposit" equal to the deal's profit, which disappears when Supabase catches up. For high-turnover accounts (13+ open positions, closes every few seconds) this creates constant visible flicker that dealers read as "a deposit was added and then removed".

**Planned fix:** read trade-flow from the in-memory `DealStore` (event-driven, no lag) instead of Supabase. Keep Supabase path as fallback when DealStore is cold.

### Date-boundary mismatch with MT5 Manager reports
Every date picker in the app interprets user input as **Asia/Beirut midnight**. MT5 Manager's Summary / Segregated reports appear to use a different boundary (UTC midnight or broker-local midnight — exact rule not empirically nailed down). Cross-checks between the app and Manager reports show per-login residuals on any account that had activity in the ~2-hour window between boundaries on the `from` date.

One observed case: login 5250 had a $10,000 credit deal at `2026-03-27 22:55 UTC` = `2026-03-28 00:55 Beirut`. App's `from=2026-03-28` includes it (Beirut window starts 22:00 UTC). Manager's `from=2026.03.28` excludes it. Delta: $10k NetCredit. Resolved by adjusting the snapshot seed for this specific login as a one-off fix until a proper TZ-alignment change lands.

**Planned fix (strategy TBD):** either switch the app to UTC-midnight (cleanest; matches Manager + financial industry convention) OR add a TZ toggle to the date picker. First requires empirical investigation to pin down Manager's exact boundary rule.

### Admin balance/credit transfers are invisible at the deal level
MT5 Manager admin ops that move value between Balance and Credit buckets produce **no deal row**. Balance reconciliation (`current − begin − trade-flow`) picks these up automatically because `current` reflects them. But summing `deals` table rows for action=2/action=3 will miss them. Keep this in mind when investigating "why does App.NetCredit disagree with the deal-sum?" — an honest discrepancy usually means admin transfers happened in the window.

### `dotnet publish` puts MT5 native DLL in a `Libs\` subfolder
`CoverageManager.Connector.csproj` references `MT5APIManager64.dll` as:
```xml
<None Include="Libs\MT5APIManager64.dll">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```
This copies to `publish\api\Libs\MT5APIManager64.dll`. But Windows P/Invoke only searches the .exe's directory for native DLLs by default — **you must also copy the native DLL to `publish\api\` (root)**. Without this, the service fails at runtime with a native-lib load error the moment it touches the Manager API.

**Fix candidate:** add `<Link>MT5APIManager64.dll</Link>` to the csproj `<None>` entry to flatten the copy path. Until then, every deploy must manually copy.

### `ASPNETCORE_URLS` is silently overridden by Kestrel:Endpoints config
When `appsettings.json` has a `"Kestrel": { "Endpoints": { "Http": { "Url": ... } } }` section (which this repo does), Kestrel picks that URL and ignores `ASPNETCORE_URLS`. Startup log prints a WARNING ("Overriding address(es) 'http://...'. Binding to endpoints defined via IConfiguration...") but the service still binds the Kestrel-config URL, not the env var.

**The correct override** via env var is `Kestrel__Endpoints__Http__Url=http://0.0.0.0:<port>` — the double-underscore notation .NET uses to represent nested keys.

### VPS provider firewalls ≠ Windows firewall
Providers (GoDaddy, Hetzner, AWS, OVH, DigitalOcean, …) have a firewall layer **above** the Windows firewall you configure via `netsh`. By default most only allow RDP/SSH. Binding to non-standard ports (e.g. :5000) may pass Windows firewall but still get blocked at the provider's perimeter. Bind to :80 or :443 for zero-config external reachability.

**Diagnostic:** if `Test-NetConnection <public-IP> -Port <port>` from the server itself succeeds but external clients get "connection refused" or timeout, the provider firewall is the culprit.

## Phase Status
- [x] Phase 1: Live Exposure View (complete)
- [x] Phase 2: P&L Tracking (closed trades with buy/sell volume split, coverage P&L toggle, date range filtering)
- [x] Phase 2.5: Data Persistence (trading accounts + deals synced to Supabase, audit trail, historical backfill)
- [x] Phase 2.7: Positions Compare (side-by-side client vs coverage analysis with charts, resizable panels, drag reorder)
- [x] Phase 2.5: Bridge Execution Analysis (Live REST/WS mode wired to Centroid CS 360; resolves real MT5 deal # on both CLIENT and COV via DealStore + CoverageDealIndex)
- [x] Phase 2.8: Net P&L Tab — `FloatingΔ + Settled` period P&L with snapshot scheduler, Lebanon-time date picker, sentinel portfolio-anchor rows
- [x] Phase 2.9: Deal Reconciliation Sweep (DATA-101) — nightly backfill + modifications + ghost-delete with ±24h MT5 TZ buffer, `ReconciliationCard` UI, shared date range across tabs, Beirut-TZ alignment across every surface, compare-tab PnLRings widget, P&L/Exposure perf RPC (81s → 0.5s)
- [x] Phase 2.10: Equity P&L tab (Phase 1) — per-login balance reconciliation, PS high-water-mark engine, commission + spread rebate config, cent-perfect vs MT5 Summary report
- [x] Phase 2.11: Equity P&L Phase 2 — login groups with priority-based resolution (login → group → default), per-group config + spread rebate, scoped trade-deal fetch for perf
- [x] Phase 2.12: UI shell redesign — Linear/Retool-style sidebar + topbar with metric tiles + Command Palette (⌘K) + Tweaks drawer + per-asset SymbolBadge + Equity P&L sub-tabs + clickable UNMAPPED → Mappings deep-link. Bridge + Markup hidden from dealer nav (backend still runs). Global CSS tokens in `styles.css` + JS THEME object synced.
- [x] Phase 2.13: MT5 load reduction — Stage 1 shadow event subscriptions, Stage 2a events authoritative for `PositionManager` + drift detection, Stage 2b position poll dropped 500ms → 60s. Adds native API call counters to `/api/exposure/diagnostics`. Verified -98.7% on `getPositions` (4459/min → 58/min) with zero drift in steady state. Account-sync widened 5 min → 15 min on top.
- [x] Phase 2.14: Equity P&L cleanup — (a) DealStore `EarliestDealTime` guard fixes phantom NetDepW for historical windows where the in-memory store doesn't cover the full range. (b) `CashMovementSyncService` runs every 15 min to backfill admin balance/credit deals MT5 doesn't push via `OnDealAdd`. (c) NetDepW / NetCred switched from balance-reconciliation to deal-sum so the panel matches MT5 Manager Summary 1:1 (loses silent admin-move tracking — accepted trade-off). (d) Net Dep/W + Net Cred columns collapsed into a single "Net Cash Movement" column on the dashboard; per-login combined sum matches MT5 Summary's (In/Out + Credit) penny-perfect on 39/40 logins. (e) Equity P&L Begin Eq sourced from the Segregated 2026-03-28 report — `account_equity_snapshots` rows at `2026-03-27 22:00 UTC` (= picker's anchor for `from=2026-03-28`) overwritten with the report's per-login values.
- [x] Phase 2.15: Live floating P&L — `ExposureEngine` now consumes a `PriceCache` and recomputes per-position floating P&L from live ticks via the calibrated-delta approach (`livePnL = (livePrice − OpenPrice) × (MT5.Profit / (MT5.CurrentPrice − OpenPrice))`). Net P&L tab subscribes to `useExposureSocket()` and overlays `currentFloating` from every WS frame onto the cached REST snapshot — REST poll slowed back from 2s to 30s (only Begin + Settled need it). Result: Net P&L tab ticks every WS frame (~10/sec) like the Exposure tab, with 93% fewer REST calls than the prior 2s-poll iteration.
- [x] Phase 2.16: Live-price fast path — split the `/ws/exposure` socket into two message types so bid prices stop riding the heavy full-state broadcast. (a) New `MarkPriceDirty()` on `ExposureBroadcastService` is idempotent — bursts of MT5 ticks (typical session = 500/sec) coalesce into one frame. (b) Separate 50 ms timer ships a lightweight `price_update` WS message at ~20 Hz; the full `exposure_update` keeps its 100 ms / position-event-gated cadence. (c) `OnTickReceived` routes through the fast path; the heavy broadcast still fires on position/deal events. (d) `/api/exposure/diagnostics` adds `tickEvents.{total,perMinute,lastAt}` (was hidden — `_tickCount` was incremented but never exposed) plus `broadcasts.{fullState,priceOnly}` counters with `coalescedTicks` as the success metric. (e) Exposure table's bid-price-under-symbol cell renders a staleness pill — amber dot at >3 s, red + 50% opacity at >10 s — so the dealer can distinguish "price genuinely frozen because MT5 isn't sending ticks" from "we have a fresh price". Verified live: 33 K ticks/min in → 1 016 price-only frames/min out (97 % coalescing), bid prices update ~20 Hz on active symbols.
- [x] Phase 2.17: Live floating P&L on the fast path — Phase 2.16 only fixed the bid-price ticker; the floating-P&L cells (Exposure open-row B-Book/Coverage/Net P&L, Net P&L tab "Current Floating", Topbar "Net P&L Today" tile) still rode the slower full-state broadcast (~7 Hz, position-event-gated), so prices ticked while P&L cells froze. Adds `ExposureEngine.GetFloatingPnLPerSymbol()` (~10× cheaper than `CalculateExposure` — no canonical grouping, no weighted avg, no hedge ratio), included in every `price_update` frame as `floatingPnls: [{canonicalSymbol, bBook, coverage}]`. Frontend `useExposureSocket` reducer's `PRICE_UPDATE` action overlays these onto `state.exposureSummaries` and recomputes `netPnL = −bBook + coverage` so all three surfaces (Exposure open row, Net P&L Current, Topbar tile) tick at full 20 Hz. Volumes / avg prices / hedge ratio stay frozen between `exposure_update` frames — they only change when positions change, not when prices tick.
- [ ] Phase 3: Risk Alerts (news events, threshold warnings)
- [ ] Phase 4: Hedge Execution (one-click hedging via LP terminal — mt5.order_send() ready)
