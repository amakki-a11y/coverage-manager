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

## Supabase Tables (14)
1. `symbol_mappings` — B-Book ↔ LP symbol mapping + contract sizes
2. `positions` — Open positions snapshot
3. `deals` — Deal history with dedup on (source, deal_id), includes direction/fee/entry. 202K+ deals persisted.
4. `trading_accounts` — Mirror of all MT5 accounts (B-Book + Coverage), unique on (source, login). Auto-synced every 5min.
5. `trade_audit_log` — Tracks deal modifications (price, volume, profit changes) with old/new values
6. `exposure_snapshots` — Periodic exposure captures
7. `pl_summary` — P&L summary (Phase 2)
8. `hedge_executions` — Hedge audit trail (Phase 4)
9. `economic_events` — Economic calendar (Phase 3)
10. `risk_thresholds` — Risk limits per symbol (Phase 3)
11. `account_settings` — MT5 Manager and Coverage account credentials
12. `alert_rules` — Configurable alert thresholds (trigger type, operator, value, severity)
13. `alert_events` — Fired alert notifications (symbol, severity, threshold vs actual value, acknowledged)
14. `moved_accounts` — Logins removed from MT5 Manager, deals kept in Supabase but excluded from dashboard

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
- **Left panel expanded:** Full table matching Exposure layout with Open/Closed rows, bid prices, date picker, To Cover, Hedge%
- **Right panel detail:** Shows when a symbol is selected — DetailHeader, 5 SummaryCards, PriceChart (Canvas), VolPnlChart (Canvas), CompareTable
- **Canvas charts:** PriceChart (entry/exit markers, teal sparkline), VolPnlChart (hourly volume bars, cumulative P&L lines)
- **Charts debounced:** Redraw on symbol change + ResizeObserver, 200ms minimum interval
- **Drag reorder:** Symbols can be drag-reordered in compact mode (persisted to localStorage)
- **Panel resize:** Left panel width adjustable by dragging right edge (persisted to localStorage)
- **Hedge % colors:** green >= 80%, amber >= 50%, red < 50%
- **Entry Δ colors:** positive (client paid more) = red, negative = green
- **Data source:** Polls `/api/compare/exposure` every 500ms, `/api/compare/trades` every 5s

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

## API Endpoints
### C# Backend (port 5000)
- `GET /api/exposure/summary` — Live exposure aggregation (WebSocket also available)
- `GET /api/exposure/pnl?from=&to=` — B-Book realized P&L by symbol (queries Supabase with pagination)
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
- `GET /api/compare/trades?symbol=&from=` — Trade history for Compare charts
- `WS /ws` — Real-time exposure + prices + P&L updates

### Python Collector (port 8100)
- `GET /positions` — Current coverage open positions
- `GET /deals?from=YYYY-MM-DD&to=YYYY-MM-DD` — Coverage closed deal history (aggregated)
- `GET /deals/raw?from=YYYY-MM-DD&to=YYYY-MM-DD` — Individual coverage deals with ticket/price/time (used by Markup tab)
- `GET /health` — Connection status + account info

## Phase Status
- [x] Phase 1: Live Exposure View (complete)
- [x] Phase 2: P&L Tracking (closed trades with buy/sell volume split, coverage P&L toggle, date range filtering)
- [x] Phase 2.5: Data Persistence (trading accounts + deals synced to Supabase, audit trail, historical backfill)
- [x] Phase 2.7: Positions Compare (side-by-side client vs coverage analysis with charts, resizable panels, drag reorder)
- [ ] Phase 3: Risk Alerts (news events, threshold warnings)
- [ ] Phase 4: Hedge Execution (one-click hedging via LP terminal — mt5.order_send() ready)
