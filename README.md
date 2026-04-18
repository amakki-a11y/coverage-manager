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

## Dev Secrets

Secrets (Supabase key, MT5 manager creds, Centroid creds) are **never** committed. `appsettings.json` ships with empty values; the runtime reads the real ones from:

1. **Dev box (Windows):** `dotnet user-secrets` scoped to the API project.
   ```powershell
   cd src/CoverageManager.Api
   dotnet user-secrets init
   dotnet user-secrets set "Supabase:Key"    "eyJ..."
   dotnet user-secrets set "MT5:Server"      "86.104.251.234:443"
   dotnet user-secrets set "MT5:Login"       "1065"
   dotnet user-secrets set "MT5:Password"    "..."
   ```
2. **Production:** environment variables with `__` as the section separator, e.g. `Supabase__Key=eyJ...`, `MT5__Password=...`.

If either is missing, `SupabaseService` will throw `ArgumentException: Supabase:Key not configured` on startup.
