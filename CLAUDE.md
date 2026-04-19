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
7. **Then** `OnTick += …` / `SubscribeTicks()`, `OnDealAdd += …` / `SubscribeDeals()`.

Subscribing to the sinks BEFORE steps 3–5 opens a race: the first live callback fires against an empty cache, producing WebSocket flickers with stale exposure and duplicate Supa upserts. The order is enforced by the call sequence and a prominent comment block at the call site — change it at your peril.

## Data Sync Architecture
- **DataSyncService** (background): Syncs deals to Supabase every 30s with change detection
- **On startup:** Loads deals from Supabase into in-memory DealStore (survives restarts)
- **Auto-backfill on startup:** Queries last deal time from Supabase, detects gap if app was down, backfills missed deals from MT5 Manager API automatically (no manual backfill needed)
- **Account sync:** MT5ManagerConnection syncs all trading accounts to Supabase on connect + every 5 minutes
- **Login refresh:** Periodically re-fetches `GetUserLogins()` to detect new accounts (every 5 min)
- **Change detection:** Compares incoming deals vs stored, logs modifications to `trade_audit_log`
- **Batch upserts:** 500 records per batch for accounts and deals, with `on_conflict` for dedup
- **Supabase pagination:** GetDealsAsync pages through results (1000 rows/page) to handle large date ranges

## Deal Volume Calculation
- **Volume includes both IN + OUT deals** to match MT5 Manager totals
- **P&L only from OUT deals** (Entry=1,2,3) — only closing deals carry profit/loss
- **Balance/credit deals filtered** (Action >= 2) — excluded from DealStore entirely
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
- **WebSocket push:** Throttled broadcast to prevent browser flooding; includes exposure, prices, and P&L data
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
- **Current floating**: live `ExposureEngine.CalculateExposure()`, date-independent.
- **Settled**: `aggregate_bbook_settled_pnl` RPC for B-Book (Lebanon-time boundary) + Python collector `/deals` for Coverage.
- **Date pickers**: interpreted as Asia/Beirut throughout so ranges match MT5 terminal views.
- **Sentinel snapshots**: rows with `canonical_symbol` starting `__`, or containing `SEED_TOTAL` / `PORTFOLIO`, feed the TOTAL only (no per-symbol row). Lets a dealer seed portfolio-level Begin without per-symbol values.
- **"No position"**: rows whose live summary has 0 volume on a side render `—` for Begin/Current/ΔFloat on that side; Settled/Net still show real realized P&L.
- **Loading badge**: date-change fetches show a ≥450ms amber pill next to the date pickers; interval polls (every 10s) refresh silently.
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
- **UNMAPPED badge** — amber pill rendered next to the symbol name in ExposureTable when the canonical symbol is missing from `symbol_mappings`. Surfaces the silent fallback path that would otherwise hide broken contract-size conversions.

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
- `WS /ws` — Real-time exposure + prices + P&L updates
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

## Phase Status
- [x] Phase 1: Live Exposure View (complete)
- [x] Phase 2: P&L Tracking (closed trades with buy/sell volume split, coverage P&L toggle, date range filtering)
- [x] Phase 2.5: Data Persistence (trading accounts + deals synced to Supabase, audit trail, historical backfill)
- [x] Phase 2.7: Positions Compare (side-by-side client vs coverage analysis with charts, resizable panels, drag reorder)
- [x] Phase 2.5: Bridge Execution Analysis (Live REST/WS mode wired to Centroid CS 360; resolves real MT5 deal # on both CLIENT and COV via DealStore + CoverageDealIndex)
- [x] Phase 2.8: Net P&L Tab — `FloatingΔ + Settled` period P&L with snapshot scheduler, Lebanon-time date picker, sentinel portfolio-anchor rows
- [x] Phase 2.9: Deal Reconciliation Sweep (DATA-101) — nightly backfill + modifications + ghost-delete with ±24h MT5 TZ buffer, `ReconciliationCard` UI, shared date range across tabs, Beirut-TZ alignment across every surface, compare-tab PnLRings widget, P&L/Exposure perf RPC (81s → 0.5s)
- [ ] Phase 3: Risk Alerts (news events, threshold warnings)
- [ ] Phase 4: Hedge Execution (one-click hedging via LP terminal — mt5.order_send() ready)
