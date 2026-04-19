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
- **UI/UX polish batch** — shell-wide dealer experience improvements
  - `RiskBanner` — top-of-page watchdog for portfolio unhedged lots + worst-hedged symbol, with inline dealer-configurable amber/red thresholds (persisted to localStorage).
  - `ConnectionHealthDots` — per-source health indicators (MT5 / Collector / Centroid / Supabase) in the top bar, polling each upstream every 5 s.
  - `DateRangePicker` — shared picker backed by `useDateRange`, with Today / Yesterday / Week / MTD / 7D / 30D presets, a teal **BEIRUT** timezone pill, and `T`/`Y`/`W`/`M` keyboard shortcuts.
  - `KeyboardShortcutsOverlay` — `?` anywhere opens a cheat-sheet modal listing all registered shortcuts.
  - `ConfirmDialog` — replaces native `confirm()` for destructive actions (delete mapping / account / schedule, deal backfill) with Enter/Escape shortcuts.
  - `ErrorToastProvider` + `useErrorToast()` — global, 3-second-dedupe error toast stack at bottom-right.
  - `StaleWrapper` — desaturates and hatches the main content when the exposure WebSocket drops.
  - `Skeleton` / `SkeletonRows` — shimmering placeholders for in-flight table loads.
  - `FlashingCell` + `useFlashOnChange` — 800 ms green/red tint on numeric cells when they tick up/down. Wired into the OPEN-row B-Book / Coverage / Net P&L cells in Exposure.
  - `HedgeBar` — thin progress bar under the per-symbol hedge % cell (green ≥ 80%, amber ≥ 50%, red < 50%).
  - `UNMAPPED` amber badge in Exposure for any canonical symbol missing from `symbol_mappings`.

### Changed
- `web/src/theme.ts` — documented color semantics at the top of the file (green/red/amber/blue/teal meanings). Audit pass: replaced legacy `#FF8A80` red-ish coverage accent with `THEME.teal` across `SettingsPanel` and `SymbolMappingAdmin`.
- `web/src/components/PnLPanel.tsx` — `formatDate` now uses `Intl.DateTimeFormat` with `timeZone: 'Asia/Beirut'` so daily rows no longer render as the wrong weekday for dealers in non-Beirut browser timezones.
- `web/src/components/AlertToast.tsx` — audio beep ref is now a typed `(() => void) | null`, dropped all `as any` shims; Web Audio context is created lazily on first play (survives browsers that block auto-start).
- `web/src/components/TotalBar.tsx` — removed `highlight ? color : color` tautological ternary.
- `web/src/pages/PositionsCompare/RightPanel/CompareTable.tsx` — Net P&L footer uses explicit sign prefix (`+` / `−` via U+2212 minus) on the absolute value for typographic cleanliness.
- `src/CoverageManager.Api/Program.cs` — collapsed duplicated `AddHttpClient<T>()` + `AddSingleton<T>(factory)` pairs down to a single `AddHttpClient()` registration plus per-service factory singletons. `CoverageDealIndex` retains the typed-client registration because its constructor takes `HttpClient` directly.

### Removed
- `src/CoverageManager.Tests/UnitTest1.cs` — MSTest template leftover.

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
