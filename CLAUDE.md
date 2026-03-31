# Coverage Manager

## Project Description
Real-time exposure management system for forex dealing desks. Shows B-Book client positions alongside LP coverage positions with symbol mapping and contract size normalization.

## Architecture
```
B-Book MT5 Server → C# Manager API (event-driven) → C# Backend → WebSocket → React Dashboard
Coverage MT5 Terminal → Python Collector (poll 100ms) → HTTP POST → C# Backend ↗
                                                                          ↓
                                                                   Supabase (async persist)
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
│   │   ├── Models/                     # Position, SymbolMapping, ExposureSummary, etc.
│   │   └── Engines/                    # PositionManager, ExposureEngine, PriceCache
│   ├── CoverageManager.Connector/      # MT5 Manager API connection
│   │   └── MockMT5Connection.cs        # Simulated MT5 for dev/testing
│   ├── CoverageManager.Api/            # ASP.NET Core host
│   │   ├── Controllers/                # Coverage, Exposure, SymbolMapping
│   │   └── Services/                   # SupabaseService, ExposureBroadcastService
│   └── CoverageManager.Tests/          # MSTest unit tests (27 tests)
├── collector/                           # Python FastAPI collector
├── web/                                 # React + TypeScript + Vite dashboard
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

## Phase Status
- [x] Phase 1: Live Exposure View (complete)
- [ ] Phase 2: P&L Tracking (closed trades, daily/monthly summaries)
- [ ] Phase 3: Risk Alerts (news events, threshold warnings)
- [ ] Phase 4: Hedge Execution (one-click hedging via LP terminal)
