# Changelog

All notable changes documented here.
Format: [Conventional Changelog](https://conventionalcommits.org)

## [Unreleased]
### Added
- Account modal with equity curve, open positions, and deal history
- Logins widget showing accounts with positions per symbol
- Equity curve chart (cumulative realized P&L) in Compare tab and Account modal
- Closed rows toggle checkbox in Compare expanded table
- Color scheme update: blue (buy/positive) and red (sell/negative) for net and P&L
- **Bridge tab** (Phase 2.5) — CLIENT/COV OUT execution pairing from Centroid CS 360 Dropcopy FIX 4.4
  - QuickFIX/n live mode + synthetic Stub mode behind `Centroid:Mode`
  - `bridge_executions` Supabase table with UPSERT on `client_deal_id` and generated `coverage_ratio`
  - `pip_size` column added to `symbol_mappings`, seeded for all 22 active mappings
  - `/api/bridge/{executions,live,health}` REST + `/ws/bridge` WebSocket push
  - 21 new unit tests for pairing and edge/pip calculations

### Changed
### Fixed

## [0.2.7] — 2026-04-03
### Added
- Positions Compare tab with side-by-side client vs coverage analysis
- Resizable left panel with drag handle
- Drag-and-drop symbol reorder (persisted to localStorage)
- Canvas charts: PriceChart (entry/exit markers), VolPnlChart (hourly volume bars)
- CompareTable with metrics comparison (trades, volume, win rate, P&L)

## [0.2.5] — 2026-04-02
### Added
- Trading accounts synced to Supabase (auto-sync every 5 min)
- Deal persistence with change detection and audit trail
- Historical deal backfill from MT5 Manager
- Account deals API with Supabase pagination

## [0.2.0] — 2026-04-01
### Added
- P&L tracking with closed deals (buy/sell volume split)
- Coverage P&L integration via Python collector
- Date range filtering for closed deals
- Supabase as source of truth for historical P&L

## [0.1.0] — 2026-03-31
### Added
- Live exposure view with real-time WebSocket updates
- MT5 Manager API connector (event-driven)
- Python collector for coverage positions (100ms polling)
- Symbol mapping with contract size normalization
- React dashboard with ExposureTable, PnLPanel, TotalBar
- Dark/light theme toggle
- Supabase schema (11 tables)
