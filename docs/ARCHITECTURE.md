# Architecture

## System Overview
Coverage Manager is a real-time forex exposure management system that aggregates positions from two MT5 sources (B-Book clients and LP coverage), normalizes them via symbol mapping, and presents a unified dashboard for dealing desk operators.

## Data Flow
```
B-Book MT5 Server → C# Manager API (event-driven)  → C# Backend → WebSocket → React Dashboard
                       ├── OnPositionAdd/Update/Delete  ─→ PositionManager  ─┐
                       ├── OnDealAdd                    ─→ DealStore        ─┤
                       ├── OnUserUpdate                 ─→ trading_accounts ─┤
                       └── OnTick (~500/sec mid-session)─→ PriceCache       ─┤
                                                                             ↓
                                                          ExposureBroadcastService
                                                          ├── exposure_update (10 Hz, dirty-gated)
                                                          └── price_update    (20 Hz, dirty-gated)

Coverage MT5 Terminal → Python Collector (poll 100ms) → HTTP POST → C# Backend ↗
                                                          ↕                       ↓
                                                   /deals endpoint          Supabase (async persist)
```

**Why two WebSocket message types?** Routing every MT5 tick through the heavy
broadcast (which recomputes exposure across every position) made bid prices
under each symbol wait for the next position event — 3-5 s of staleness on
quiet symbols. Splitting the socket into a lightweight `price_update` (just
prices + timestamp) and a full `exposure_update` (everything else) lets the
"price under symbol" UI cell update at full tick cadence without paying the
per-position recompute cost. Both share the same `/ws/exposure` socket.

## Directory Structure
```
src/
├── CoverageManager.Core/       — Domain models (Position, ExposureSummary, DealRecord, etc.)
│   ├── Models/                 — Data models and engines
│   └── Engines/                — PositionManager, ExposureEngine, PriceCache, DealStore
├── CoverageManager.Connector/  — MT5 Manager API connection layer
├── CoverageManager.Api/        — ASP.NET Core host (REST + WebSocket)
│   ├── Controllers/            — Coverage, Exposure, Compare, SymbolMapping, Accounts, Settings
│   └── Services/               — SupabaseService, ExposureBroadcastService, DataSyncService
└── CoverageManager.Tests/      — MSTest unit tests (27 tests)

collector/                       — Python FastAPI (MT5 Terminal connection for coverage)
web/src/                         — React + TypeScript + Vite dashboard
├── components/                  — ExposureTable, PnLPanel, PositionsGrid, TotalBar, etc.
├── hooks/                       — useExposureSocket, usePositionsCompare
└── pages/PositionsCompare/      — Compare tab (LeftPanel, RightPanel, charts, modals)
```

## Key Design Decisions
- **Event-driven B-Book**: MT5 Manager API deal callbacks, no polling — instant updates
- **Polling Coverage**: Python collector polls MT5 terminal every 100ms, POSTs to backend
- **In-memory state**: ConcurrentDictionary for thread-safe position store, Supabase for persistence
- **WebSocket push**: Two channels on the same `/ws/exposure` socket — `exposure_update` at 10 Hz (full state, gated by position/deal events) and `price_update` at 20 Hz (price-only, gated by MT5 ticks, idempotent so bursts coalesce). Tracked via `/api/exposure/diagnostics` (`broadcasts.{fullState,priceOnly}.{total,perMinute}` + `priceOnly.coalescedTicks`).
- **Symbol normalization**: Contract size conversion (e.g., 1500 GOLD lots = 15 XAUUSD B-Book lots)
- **Net Exposure**: `BBookNet - CoverageNet` (not addition, since coverage mirrors client direction)
- **Net P&L**: `-ClientPnL + CoveragePnL` (invert client P&L for broker perspective)

## External Services
- **Supabase (PostgreSQL)**: Persistent storage for deals, accounts, mappings, audit trail (11 tables)
- **MT5 Manager API**: B-Book client positions and deals (event-driven via C# SDK)
- **MT5 Terminal**: Coverage account positions and deals (polled via Python MetaTrader5 library)
