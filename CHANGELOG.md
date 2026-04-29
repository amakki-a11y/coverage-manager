# Changelog

All notable changes documented here.
Format: [Conventional Changelog](https://conventionalcommits.org)

## [Unreleased]
### Added
- **Live-price fast path (Phase 2.16)** — split `/ws/exposure` into two message types so bid prices update at full MT5 tick cadence without paying the per-position exposure recompute on every frame.
  - New `MarkPriceDirty()` on `ExposureBroadcastService` is idempotent so tick bursts (typical session = ~500/sec) coalesce into one ~50 ms WS frame.
  - Separate 50 ms timer ships a lightweight `{type:"price_update",data:{prices,timestamp}}` message at ~20 Hz; the existing `{type:"exposure_update",...}` keeps its 100 ms / position-event-gated cadence.
  - `OnTickReceived` in `MT5ManagerConnection` routes through the fast path; `MarkDirty` (full state) still fires on position/deal/alert events.
  - `/api/exposure/diagnostics` adds `tickEvents.{total,perMinute,lastAt}` (was hidden — `_tickCount` was incremented but never exposed) plus `broadcasts.{fullState,priceOnly}.{total,perMinute}` and `broadcasts.priceOnly.coalescedTicks` (success metric — high values mean the coalescing is paying off).
  - Exposure table's bid-price-under-symbol cell renders a staleness pill: amber dot + grey text at >3 s since last tick, red text + 50 % opacity at >10 s. Hover tooltip shows the actual age. Lets the dealer distinguish "MT5 isn't sending ticks for this symbol" from "we have a fresh price".
  - `useExposureSocket` reducer gains a `PRICE_UPDATE` action that updates only `state.prices`, so the rest of the table doesn't re-render on every tick.
  - Verified live: 33 K ticks/min in → 1 016 price-only frames/min out (97 % coalescing) → bid prices refresh ~20 Hz on active symbols on the dealer UI.
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
- **Equity P&L tab — Phase 1** (per-login balance-reconciled view)
  - Columns: `Login, Begin Eq, Net Dep/W, Net Cred, Comm Reb, Spread Reb, Adj, PS, Supposed Eq, Current Eq, PL, Net PL`.
  - `Net Dep/W = (CurrentBalance − BeginBalance) − Σ(trade-flow)` — captures MT5 admin balance transfers that `RequestDeals` doesn't surface. Reconciles to the cent vs. MT5 Summary Report's Deposit column.
  - `Net Cred = Current Credit − Begin Credit` — `credit` field added to `trading_accounts` + `RawAccount` + wired through `IMTUser.Credit()` so the Manager sync picks it up every 5 min.
  - `PsHighWaterMarkEngine` — reverse HWM (loss-share) engine with idempotent month-walk; pays % of new drawdown below the running low-water mark.
  - New tables `account_equity_snapshots`, `equity_pnl_client_config`, `equity_pnl_spread_rebates`; scheduler tick writes per-login equity on every capture.
  - `POST /api/equity-pnl/backfill-cash-movements` — one-shot historical backfill of balance/credit/correction deals with 300 ms-per-login pacing.
  - `GET /api/equity-pnl/account-live?login=N` — diagnostic bypass for the 5-min sync.
- **Equity P&L Phase 2 — login groups with priority-based resolution**
  - New tables `login_groups`, `login_group_members(priority)`, `equity_pnl_group_config`, `equity_pnl_group_spread_rebates`.
  - Endpoint resolves effective rate per login as `login-specific → group (highest priority) → 0 (default)`. Login-level spread rate overrides group rate for the same symbol.
  - Trade-deal fetch scoped to **rebate-eligible logins only** so the endpoint stays sub-second instead of paging 120 k trade rows.
  - `LoginGroupsCard` Settings UI — left: groups list with inline add/delete; right: detail with `CONFIG / MEMBERS / SPREAD REBATES` sub-tabs.
- **Settings sub-tabs** — the Settings page is organized into 5 sub-tabs (`Connections / Equity P&L / Snapshots / Data Integrity / Reference`) instead of one long scroll. Sub-tab selection persisted in localStorage.

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
- **Critical — 5 writers derived `DealRecord.Action` from `Direction` instead of using MT5's real action code.** Harmless while only trade deals (action 0/1) flowed, but when Equity P&L ingestion started pulling balance/credit/correction deals, every `action=3` CREDIT deal got re-classified to `action=1` SELL on the next sync → phantom trade P&L drift. Fixed in `DataSyncService.cs:167`, `ReconciliationService.cs:174`, `ReconciliationService.cs:205`, `ExposureController.cs:303`, `ExposureController.cs:484`. All now use `Action = (int)d.Action`.
- **`SumTradeBalanceFlowPerLoginAsync`** — Supabase PostgREST caps `limit` at 1000 rows server-side regardless of the value requested. The pagination was terminating early after the first page (1000 rows < 5000 pageSize), silently dropping ~80 % of the trade-flow data. Switched to `pageSize=1000` + `if (page.Count == 0) break;`.
- **`SumTradeBalanceFlowPerLoginAsync` filter** — was `action < 2` (which excluded MT5's broker-specific action codes like `19` — observed in this dataset, 7 deals totalling up to $29k) so they leaked into the implicit NetDepW. Now uses `action not in (2,3)` so every non-BALANCE non-CREDIT deal contributes to trade flow.
- **`GetAccountEquitySnapshotsBeforeAsync`** — HTTP `Range: 0-9999` header was invalid format (`.NET HttpClient` rejects PostgREST's bare `"0-N"` syntax), silently throwing `FormatException` and making every Begin Equity come back as 0. Swapped to PostgREST's `limit=10000` query parameter.
- **Python collector reconnect** — `_reconnect_mt5` tried `mt5.initialize()` with no args first (reuse existing session) which silently fails when the user closes and reopens the MT5 terminal (Python library's internal pointer goes stale). Now goes straight to credential-based `mt5.initialize(login=, server=, password=)` so a fresh terminal launch is picked up on the first retry tick. Backoff cap lowered from 60 s → 10 s so the coverage panel recovers within 10 s of MT5 being reopened.
- **`EquityPnLClientConfigCard`** — was crashing on mount because it treated `/api/accounts` as an array when the endpoint returns `{accounts: [...], count: N}`. Caused the Settings page to flash/flicker (React unmounts then remounts after a render-time throw). Now unwraps `body.accounts`.
- **Equity P&L panel sticky header** — `position: sticky; top: 0` on `<th>` wasn't holding under a `border-collapse: collapse` table when body rows scrolled past. Added `z-index: 2` + `box-shadow: inset 0 -1px 0` for the bottom-edge separator, and `minHeight: 0` on the flex column root so the inner scroll container actually constrains instead of growing to fit content.

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
