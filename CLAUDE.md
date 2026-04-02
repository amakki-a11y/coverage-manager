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
│   │   ├── Models/                     # Position, SymbolMapping, ExposureSummary, SymbolPnL, ClosedDeal, TradingAccount, DealRecord, TradeAuditEntry
│   │   └── Engines/                    # PositionManager, ExposureEngine, PriceCache, DealStore
│   ├── CoverageManager.Connector/      # MT5 Manager API connection
│   │   ├── IMT5Api.cs                  # Interface (Initialize, Connect, OnTick, OnDealAdd, GetPositions, GetUserLogins, GetUserAccount, RequestDeals)
│   │   ├── MT5ApiReal.cs               # Real MT5 Manager API implementation (#if MT5_API_AVAILABLE)
│   │   ├── MT5ManagerConnection.cs     # B-Book connection service (positions, deals, account sync)
│   │   ├── MT5CoverageConnection.cs    # Coverage connection (disabled — uses Python collector)
│   │   ├── RawTypes.cs                 # RawDeal, RawPosition, RawTick, RawAccount
│   │   └── Libs/                       # MetaQuotes native DLLs
│   ├── CoverageManager.Api/            # ASP.NET Core host
│   │   ├── Controllers/                # Coverage, Exposure, SymbolMapping, Accounts
│   │   └── Services/                   # SupabaseService, ExposureBroadcastService, DataSyncService
│   └── CoverageManager.Tests/          # MSTest unit tests (27 tests)
├── collector/                           # Python FastAPI collector (MT5 Terminal connection)
│   └── main.py                         # FastAPI app with /positions, /deals, /health endpoints
├── web/                                 # React + TypeScript + Vite dashboard
│   └── src/components/                 # ExposureTable, PnLPanel, PositionsTable, etc.
├── CoverageManager.sln
└── CLAUDE.md
```

## Supabase Tables (11)
1. `symbol_mappings` — B-Book ↔ LP symbol mapping + contract sizes
2. `positions` — Open positions snapshot
3. `deals` — Deal history with dedup on (source, deal_id), includes direction/fee/entry. 192K+ deals persisted.
4. `trading_accounts` — Mirror of all MT5 accounts (B-Book + Coverage), unique on (source, login). Auto-synced every 5min.
5. `trade_audit_log` — Tracks deal modifications (price, volume, profit changes) with old/new values
6. `exposure_snapshots` — Periodic exposure captures
7. `pl_summary` — P&L summary (Phase 2)
8. `hedge_executions` — Hedge audit trail (Phase 4)
9. `economic_events` — Economic calendar (Phase 3)
10. `risk_thresholds` — Risk limits per symbol (Phase 3)
11. `account_settings` — MT5 Manager and Coverage account credentials

## Data Sync Architecture
- **DataSyncService** (background): Syncs deals to Supabase every 30s with change detection
- **On startup:** Loads today's deals from Supabase into in-memory DealStore (survives restarts)
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

## UI Features
- **Dark/Light theme toggle:** ThemeContext with mutable THEME object (Object.assign pattern)
- **Theme persistence:** Saved to localStorage, applied on load
- **Theme-reactive styles:** All theme-dependent styles computed inside components (not module-level) to update on theme toggle
- **P&L Panel:** Shows client perspective (positive = clients profited), NOT inverted for broker view
- **Coverage P&L:** Respects date range picker

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
- `WS /ws` — Real-time exposure + prices + P&L updates

### Python Collector (port 8100)
- `GET /positions` — Current coverage open positions
- `GET /deals?from=YYYY-MM-DD&to=YYYY-MM-DD` — Coverage closed deal history (buyVolume, sellVolume)
- `GET /health` — Connection status + account info

## Phase Status
- [x] Phase 1: Live Exposure View (complete)
- [x] Phase 2: P&L Tracking (closed trades with buy/sell volume split, coverage P&L toggle, date range filtering)
- [x] Phase 2.5: Data Persistence (trading accounts + deals synced to Supabase, audit trail, historical backfill)
- [ ] Phase 3: Risk Alerts (news events, threshold warnings)
- [ ] Phase 4: Hedge Execution (one-click hedging via LP terminal — mt5.order_send() ready)
