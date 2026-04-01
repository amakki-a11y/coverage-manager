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
│   │   ├── Models/                     # Position, SymbolMapping, ExposureSummary, SymbolPnL, ClosedDeal
│   │   └── Engines/                    # PositionManager, ExposureEngine, PriceCache, DealStore
│   ├── CoverageManager.Connector/      # MT5 Manager API connection
│   │   └── MockMT5Connection.cs        # Simulated MT5 for dev/testing
│   ├── CoverageManager.Api/            # ASP.NET Core host
│   │   ├── Controllers/                # Coverage, Exposure, SymbolMapping
│   │   └── Services/                   # SupabaseService, ExposureBroadcastService
│   └── CoverageManager.Tests/          # MSTest unit tests (27 tests)
├── collector/                           # Python FastAPI collector (MT5 Terminal connection)
│   └── main.py                         # FastAPI app with /positions, /deals, /health endpoints
├── web/                                 # React + TypeScript + Vite dashboard
│   └── src/components/                 # ExposureTable, PnLPanel, PositionsTable, etc.
├── CoverageManager.sln
└── CLAUDE.md
```

## Supabase Tables (8)
1. `symbol_mappings` — B-Book ↔ LP symbol mapping + contract sizes
2. `positions` — Open positions snapshot
3. `deals` — Deal history (Phase 2)
4. `exposure_snapshots` — Periodic exposure captures
5. `pl_summary` — P&L summary (Phase 2)
6. `hedge_executions` — Hedge audit trail (Phase 4)
7. `economic_events` — Economic calendar (Phase 3)
8. `risk_thresholds` — Risk limits per symbol (Phase 3)

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
- **WebSocket push:** Throttled broadcast to prevent browser flooding
- **Symbol normalization:** Contract size conversion (e.g., 1500 GOLD lots = 15 XAUUSD B-Book lots)
- **Coverage mirrors client direction:** Clients sell → broker hedges by selling on LP
- **Net Exposure:** `BBookNet - CoverageNet` (not addition, since coverage mirrors direction)
- **Net P&L:** `-ClientPnL + CoveragePnL` (invert client P&L for broker perspective)
- **To Cover:** `BBookNet - CoverageNet` → negative = SELL more, positive = BUY more

## Exposure Table Layout
- **Two rows per symbol:** Open (live positions) + Closed (today's deals)
- **Three sections:** Clients (blue) | Coverage (teal) | Summary
- **Closed row columns:** Buy Volume, Sell Volume, Total Volume, P&L
- **B-Book closed deals:** From `/api/exposure/pnl` (DealStore)
- **Coverage closed deals:** From Python collector `/deals` endpoint (MT5 `history_deals_get`)
- **Symbol mapping:** Coverage symbols (XAUUSD-, US30.c) → canonical → B-Book symbols via `/api/mappings`

## API Endpoints
### C# Backend (port 5000)
- `GET /api/exposure/summary` — Live exposure aggregation (WebSocket also available)
- `GET /api/exposure/pnl` — B-Book realized P&L by symbol (buyVolume, sellVolume, netPnL)
- `GET /api/mappings` — Symbol mapping table (B-Book ↔ Coverage)
- `WS /ws` — Real-time exposure updates

### Python Collector (port 8100)
- `GET /positions` — Current coverage open positions
- `GET /deals?from=YYYY-MM-DD&to=YYYY-MM-DD` — Coverage closed deal history (buyVolume, sellVolume)
- `GET /health` — Connection status + account info

## Phase Status
- [x] Phase 1: Live Exposure View (complete)
- [x] Phase 2: P&L Tracking (closed trades with buy/sell volume split, coverage P&L toggle)
- [ ] Phase 3: Risk Alerts (news events, threshold warnings)
- [ ] Phase 4: Hedge Execution (one-click hedging via LP terminal — mt5.order_send() ready)
