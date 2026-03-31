# Coverage Manager

Real-time exposure management for forex dealing desks.

**B-Book positions** (MT5 Manager API, event-driven) + **LP coverage positions** (MT5 Terminal, Python collector) → unified exposure view with symbol mapping and contract size normalization.

## Tech Stack
- **Backend:** C# .NET 8+ / ASP.NET Core (WebSocket + REST)
- **Coverage Collector:** Python FastAPI + MetaTrader5 library
- **Frontend:** React + TypeScript (Vite)
- **Database:** Supabase (PostgreSQL)
- **Real-time:** WebSocket (< 1ms delivery)

## Status
🚧 Phase 1 — Live Exposure View (in progress)
