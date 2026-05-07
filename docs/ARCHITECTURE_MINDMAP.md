# Coverage Manager — System Architecture Mind Map

> **Generated:** 2026-05-07 from a live scan of the codebase + Supabase project `svhmhcqopkdgccnzgvzp`.
> **Repo:** https://github.com/amakki-a11y/coverage-manager
> **HEAD on `live`:** `9221e14` (docs(claude.md): correct branch-model description)
> **Source of truth:** `CLAUDE.md` at the repo root. Where this file disagrees with CLAUDE.md, treat CLAUDE.md as authoritative and update this file.

This document captures four orthogonal views of the system. Each view is intended to be readable on its own — read whichever matches the question you're holding.

- [View 1 — Data Flow](#view-1--data-flow) — how data moves from MT5/Centroid through the backend out to the dashboard
- [View 2 — Project Structure](#view-2--project-structure) — every file with its line count and one-line purpose
- [View 3 — Persistence Layer](#view-3--persistence-layer) — every Supabase table, who writes to it, on what cadence
- [View 4 — Phase Roadmap](#view-4--phase-roadmap) — what's done, what's blocked, what's next

---

## View 1 — Data Flow

```
                           ┌─────────────────────────────────────────────────────────────┐
                           │                       EXTERNAL SOURCES                       │
                           └─────────────────────────────────────────────────────────────┘
       ┌─────────────────────────┐   ┌──────────────────────────┐   ┌────────────────────────────┐
       │  B-Book MT5 Manager     │   │  Coverage MT5 Terminal   │   │  Centroid CS 360           │
       │  (broker's own book)    │   │  (LP account, fXGROW)    │   │  Bridge / Dropcopy         │
       │  86.104.251.234:443     │   │  194.164.176.137:443     │   │  REST + WebSocket + FIX44  │
       └────────────┬────────────┘   └─────────────┬────────────┘   └──────────────┬─────────────┘
                    │ event-driven                  │ polled @ 100ms                │ REST poll +
                    │ CIMTTickSink /                │ via MetaTrader5 Python lib    │ WS push (Live)
                    │ CIMTDealSink /                │                               │ Stub fallback
                    │ CIMTPositionSink /            │                               │
                    │ CIMTUserSink                  │                               │
                    ▼                                ▼                              ▼
       ┌─────────────────────────┐   ┌──────────────────────────┐   ┌────────────────────────────┐
       │ MT5ManagerConnection.cs │   │ collector/main.py        │   │ RestCentroidBridgeService  │
       │ (758 lines)             │   │ FastAPI :8100, 617 lines │   │ (920 lines, primary)       │
       │ + MT5ApiReal.cs (568)   │   │ /positions /deals        │   │ + StubCentroidBridge (190) │
       │ + IMT5Api.cs (62)       │   │ /deals/raw /health       │   │ + ICentroidBridgeService   │
       │ + RawTypes.cs (83)      │   │                          │   │ → BridgeFeedHost (159)     │
       └────────┬───────────┬────┘   └─────────────┬────────────┘   └──────────────┬─────────────┘
                │           │                       │  HTTP poll                    │
                │           │                       │  from C# backend              │
                ▼           ▼                       ▼                                ▼
   ┌────────────────┐  ┌────────────────┐  ┌─────────────────────┐   ┌────────────────────────────┐
   │ PositionManager│  │ DealStore      │  │ Sent on demand to   │   │ BridgeExecutionWorker (229)│
   │ (142 lines)    │  │ (248 lines)    │  │ /api/coverage/* +   │   │ feeds BridgeExecutionStore │
   │ open positions │  │ closed deals   │  │ /api/compare/trades │   │ (226) → BridgePairingEngine│
   │ event-cache    │  │ +EarliestDeal  │  │ + /api/markup/match │   │ (198) + BridgePipResolver  │
   │ authoritative  │  │ Time CAS       │  │                     │   │ (64) + CoverageDealIndex   │
   └────────┬───────┘  └────────┬───────┘  └─────────────────────┘   └──────────┬─────────────────┘
            │                   │                                                │
            │           ┌───────┴────────┐                                       │
            │           │                │                                       │
            ▼           ▼                ▼                                       │
   ┌────────────────┐  ┌────────────────┐  ┌─────────────────────┐               │
   │ PriceCache     │  │ ExposureEngine │  │ EquityPnLEngine     │               │
   │ (32 lines)     │  │ (194 lines)    │  │ (165 lines)         │               │
   │ bid/ask per    │  │ calibrated-    │  │ + PsHighWaterMark   │               │
   │ canonical sym  │  │ delta floating │  │ Engine (125 lines)  │               │
   │                │  │ P&L            │  │                     │               │
   └────────┬───────┘  └────────┬───────┘  └──────────┬──────────┘               │
            │                   │                      │                          │
            │                   │                      │                          │
            └─────────┬─────────┘                      │                          │
                      │                                 │                          │
                      ▼                                 │                          │
        ┌────────────────────────────┐                  │                          │
        │ AlertEngine (191 lines)    │                  │                          │
        │ evaluates alert_rules vs   │                  │                          │
        │ live exposure → fires      │                  │                          │
        │ alert_events to Supabase   │                  │                          │
        └────────────┬───────────────┘                  │                          │
                     │                                   │                          │
                     ▼                                   ▼                          ▼
        ┌─────────────────────────────────────────────────────────────────────────────────────┐
        │                          ExposureBroadcastService (329 lines)                        │
        │                          ─────────────────────────────────────                        │
        │  3 message types coexist on /ws/exposure:                                            │
        │                                                                                       │
        │   { "type": "exposure_update", … }   throttled 10/s,  full state                     │
        │     fires on position/deal/alert change (MarkDirty)                                   │
        │                                                                                       │
        │   { "type": "price_update", … }      throttled 20/s,  prices + floatingPnls          │
        │     fires on every tick (MarkPriceDirty), idempotent — bursts coalesce               │
        │     → typical: 33,000 ticks/min in → 1,016 frames/min out (97% coalescing)           │
        │                                                                                       │
        │   { "type": "deal_settled", … }      unthrottled,     per-deal SETTLED delta         │
        │     fires the moment OnDealReceived runs in MT5ManagerConnection                      │
        │                                                                                       │
        │  permessage-deflate compression on the socket (RFC 7692, ~78% wire savings)          │
        └─────────────────────────────────────────────────────────────────────────────────────┘
                                                  │
                                                  ▼ WebSocket /ws/exposure
                                                    (also: /ws/bridge for Bridge tab)
        ┌─────────────────────────────────────────────────────────────────────────────────────┐
        │                          REACT FRONTEND (web/, Vite, port 5173)                      │
        │                                                                                       │
        │  Hooks (state plumbing):                                                              │
        │   useExposureSocket (288 lines) — main /ws/exposure consumer + frame-lag watchdog    │
        │   useBridgeSocket (74)          — /ws/bridge consumer                                 │
        │   usePositionsCompare (84)      — REST poll for Compare tab                          │
        │   useConnectionHealth (99)      — pings MT5 / Collector / Centroid / Supabase @ 5s   │
        │   useDateRange (46)             — Beirut-TZ shared picker (localStorage + storage ev)│
        │   useFlashOnChange (44)         — green/red 800ms tint on number change              │
        │   useSymbolDigits (112)         — per-symbol price decimals                          │
        │                                                                                       │
        │  Tabs (visible in dealer build):                                                      │
        │    Real-time:  Exposure · Positions · Compare                                         │
        │    P&L:        P&L · Net P&L · Equity P&L                                            │
        │    Config:     Mappings · Alerts · Settings                                          │
        │    Hidden:     Bridge · Markup (backend still runs; UI hidden in current shell)      │
        └─────────────────────────────────────────────────────────────────────────────────────┘

  PARALLEL PERSIST PATH (background, async — never on the WS hot path):
  ┌────────────────────────────────────────────────────────────────────────────────────────────┐
  │ DealStore           ──30s── DataSyncService (190)         ─────────► Supabase.deals        │
  │ MT5 admin moves     ──15min CashMovementSyncService (156) ─────────► Supabase.deals        │
  │ MT5 accounts        ──15min PositionMgr.SyncAccounts                ► Supabase.trading_…   │
  │ Coverage account    ──??sec CoverageAccountSyncService (122)────────► Supabase.trading_…   │
  │ PositionMgr+PrCache ──60s── ExposureSnapshotService (195) ─────────► Supabase.exposure_…   │
  │ MT5 per-login eq.   ──60s── ExposureSnapshotService                ─► Supabase.account_eq_ │
  │ Mappings cache      ──60s── MappingRefreshService (137) ◄──────────► Supabase.symbol_map_  │
  │ Reconciliation      ──02:05 UTC ReconciliationService (269) ◄──────► Supabase.deals +      │
  │                          (ghost-evicts back into DealStore in mem)   reconciliation_runs   │
  │ Bridge pair updates ──upsert BridgeSupabaseWriter (179)  ──────────► Supabase.bridge_exec  │
  │ Alert fires         ──upsert AlertEngine (191)           ──────────► Supabase.alert_events │
  └────────────────────────────────────────────────────────────────────────────────────────────┘
```

### REST surface (port 5000, ASP.NET Core)

| Controller (file:lines) | Mounted under | Purpose |
|---|---|---|
| `ExposureController.cs` (1,642) | `/api/exposure/*` | Largest — exposure summary, /pnl, /pnl/period, /snapshot, /snapshots, /verify, /diagnostics, /equity-pnl |
| `CompareController.cs` (226) | `/api/compare/*` | `/exposure`, `/trades` for Compare tab |
| `CoverageController.cs` (106) | `/api/coverage/*` | Proxies Python collector |
| `BridgeController.cs` (188) | `/api/bridge/*` | `/executions`, `/live`, `/health` |
| `MarkupController.cs` (369) | `/api/markup/*` | Client-vs-coverage mark-up analysis |
| `AlertsController.cs` (123) | `/api/alerts/*` | Alert rules + events CRUD |
| `SettingsController.cs` (278) | `/api/settings/*` | Account creds, app settings |
| `SymbolMappingController.cs` (84) | `/api/mappings/*` | Symbol mapping CRUD |
| `AccountsController.cs` (92) | `/api/accounts/*` | Trading accounts + audit + deals |
| `EquityPnLConfigController.cs` (78) | `/api/equity-pnl-config/*` | Per-login rebate/PS config |
| `LoginGroupsController.cs` (119) | `/api/login-groups/*` | Phase-2 group config |
| `SnapshotSchedulesController.cs` (79) | `/api/snapshot-schedules/*` | Cron-style snapshot scheduler |
| `ReconciliationController.cs` (57) | `/api/reconciliation/*` | Status + manual run |

**WebSocket endpoints:** `/ws/exposure` (3 message types — see diagram above), `/ws/bridge` (ExecutionPair updates).

---

## View 2 — Project Structure

```
coverage-manager/                                    https://github.com/amakki-a11y/coverage-manager
│
├── src/                                              [C# .NET 8 solution, 15,339 lines]
│   │
│   ├── CoverageManager.Core/                         [pure domain — no I/O, no MT5, no HTTP]
│   │   ├── Engines/                                  [pure logic, easy to unit-test]
│   │   │   ├── PositionManager.cs                  142  in-memory open-position store, event-cache writes
│   │   │   ├── DealStore.cs                        248  closed deal store + EarliestDealTime CAS guard
│   │   │   ├── PriceCache.cs                        32  last bid/ask per canonical symbol
│   │   │   ├── ExposureEngine.cs                   194  calibrated-delta floating P&L + per-symbol agg
│   │   │   ├── EquityPnLEngine.cs                  165  per-login equity decomposition (Begin → Net PL)
│   │   │   ├── PsHighWaterMarkEngine.cs            125  reverse HWM (loss-share) PS engine
│   │   │   ├── BridgePairingEngine.cs              198  CLIENT↔COV_OUT pairing (greedy by |time-diff|)
│   │   │   ├── BridgePipResolver.cs                 64  pip-size lookup (overrides > rules > heuristic)
│   │   │   └── AlertEngine.cs                      191  evaluates alert_rules vs live exposure (Phase 3)
│   │   │
│   │   └── Models/                                   [pure DTOs]
│   │       ├── Position.cs                          18
│   │       ├── SymbolMapping.cs                     60
│   │       ├── ExposureSummary.cs                   32
│   │       ├── SymbolPnL.cs                         18
│   │       ├── ClosedDeal.cs                        37
│   │       ├── DealRecord.cs                        69
│   │       ├── TradeRecord.cs                       14
│   │       ├── TradingAccount.cs                    71
│   │       ├── TradeAuditEntry.cs                   46
│   │       ├── PriceQuote.cs                        10
│   │       ├── SymbolExposure.cs                    30
│   │       ├── ExposureSnapshot.cs                  39
│   │       ├── SnapshotSchedule.cs                  41
│   │       ├── PeriodPnLRow.cs                      60
│   │       ├── ReconciliationRun.cs                 38
│   │       ├── AlertEvent.cs                        40
│   │       ├── RiskThreshold.cs                     36
│   │       ├── AccountSettings.cs                   36
│   │       ├── DailyPnL.cs                          16
│   │       ├── CoveragePositionDto.cs               18
│   │       ├── Bridge/
│   │       │   ├── BridgeDeal.cs                   127  raw FIX/REST deal
│   │       │   ├── BridgeSettings.cs                57
│   │       │   ├── ClientOrderDetail.cs             38
│   │       │   ├── CovFill.cs                       51
│   │       │   └── ExecutionPair.cs                113  paired CLIENT + N COV_OUT legs
│   │       └── EquityPnL/
│   │           ├── EquityPnLRow.cs                  63
│   │           ├── AccountEquitySnapshot.cs         45
│   │           ├── EquityPnLClientConfig.cs         53
│   │           ├── SpreadRebateRate.cs              28
│   │           └── LoginGroup.cs                    95  group + member + group config + group rates
│   │
│   ├── CoverageManager.Connector/                    [native MT5 Manager API binding]
│   │   ├── IMT5Api.cs                                62  interface (Initialize, Connect, sinks, getters)
│   │   ├── MT5ApiReal.cs                            568  native MT5 Manager API impl + sink handlers
│   │   ├── MT5ManagerConnection.cs                  758  B-Book bring-up sequence + sinks + sync logic
│   │   ├── MT5CoverageConnection.cs                 206  legacy/disabled — coverage uses Python collector
│   │   ├── RawTypes.cs                               83  RawDeal/RawPosition/RawTick/RawAccount
│   │   └── Libs/MT5APIManager64.dll                       native MetaQuotes lib (binary)
│   │
│   ├── CoverageManager.Api/                          [ASP.NET Core host — port 5000]
│   │   ├── Program.cs                               412  DI, WS endpoints, startup mapping load
│   │   ├── Controllers/                              [13 controllers — see View 1 table]
│   │   ├── Services/
│   │   │   ├── SupabaseService.cs                  1775  ALL Supabase access (deals, mappings, snapshots, etc.)
│   │   │   ├── ExposureBroadcastService.cs          329  /ws/exposure pump — 3 message types, throttled
│   │   │   ├── DataSyncService.cs                   190  30s tick: DealStore → Supabase deals + audit log
│   │   │   ├── ExposureSnapshotService.cs           195  60s tick: snapshot exposure + per-login equity
│   │   │   ├── ReconciliationService.cs             269  02:05 UTC nightly + manual: backfill/modify/ghost
│   │   │   ├── CashMovementSyncService.cs           156  15min tick: backfill MT5 admin balance/credit
│   │   │   ├── CoverageAccountSyncService.cs        122  syncs LP account state from collector
│   │   │   ├── CoverageDealIndex.cs                 158  in-memory index for coverage deal lookups
│   │   │   ├── MappingRefreshService.cs             137  60s tick: re-fetch mappings + atomic cache swap
│   │   │   ├── BridgeExecutionStore.cs              226  thread-safe state machine for ExecutionPairs
│   │   │   ├── BridgeSupabaseWriter.cs              179  upsert ExecutionPair → bridge_executions
│   │   │   ├── BridgeBroadcastService.cs             66  /ws/bridge pump
│   │   │   ├── BridgeFeedHost.cs                    159  hosts the active Centroid feed (REST/Stub)
│   │   │   ├── BridgeFeedHostAdapter.cs              26  thin adapter wrapping the host
│   │   │   ├── ICentroidBridgeService.cs             57  feed abstraction
│   │   │   ├── RestCentroidBridgeService.cs         920  Centroid CS 360 REST + WebSocket Live mode
│   │   │   └── StubCentroidBridgeService.cs         190  synthetic feed for dev / no-creds running
│   │   └── Workers/
│   │       └── BridgeExecutionWorker.cs             229  subscribes to feed → store → persist + broadcast
│   │
│   └── CoverageManager.Tests/                        [MSTest, 1,051 lines, 7 test files]
│       ├── ExposureEngineTests.cs                   245
│       ├── PositionManagerTests.cs                  128
│       ├── PriceCacheTests.cs                        69
│       ├── SymbolMappingTests.cs                    104
│       ├── BridgePairingTests.cs                    254
│       ├── BridgeEdgeCalculationTests.cs            195
│       └── MT5ReconnectBackoffTests.cs               56
│
├── collector/                                        [Python FastAPI — port 8100]
│   ├── main.py                                      617  /positions /deals /deals/raw /health
│   ├── requirements.txt                                  fastapi, uvicorn, MetaTrader5, …
│   └── venv/                                              local virtualenv (gitignored)
│
├── web/                                              [React + TypeScript + Vite — port 5173, 14,252 lines]
│   └── src/
│       ├── App.tsx                                  223  root component, routing, keyboard shortcuts
│       ├── main.tsx                                  11  Vite entry
│       ├── ThemeContext.tsx                              dark/light theme provider
│       ├── theme.ts                                      DARK_THEME / LIGHT_THEME hex tokens
│       ├── config.ts                                     API/WS base URLs
│       │
│       ├── shell/                                    [Phase 2.12 redesign — Linear/Retool style]
│       │   ├── Sidebar.tsx                          119  3-section nav, kbd 1-6, alert pill, collapse
│       │   ├── Topbar.tsx                           105  3 metric tiles + ConnHealthDots + ⌘K + bell
│       │   ├── CommandPalette.tsx                   142  ⌘K fuzzy jump-to-tab + quick actions
│       │   └── TweaksPanel.tsx                      134  right-edge slide-out (accent/density/grid)
│       │
│       ├── hooks/                                    [state plumbing]
│       │   ├── useExposureSocket.ts                 288  /ws/exposure consumer + frame-lag watchdog
│       │   ├── useBridgeSocket.ts                    74
│       │   ├── usePositionsCompare.ts                84
│       │   ├── useConnectionHealth.ts                99  pings 4 upstreams every 5s
│       │   ├── useDateRange.ts                       46  Beirut TZ, shared via localStorage event
│       │   ├── useFlashOnChange.ts                   44
│       │   └── useSymbolDigits.ts                   112
│       │
│       ├── components/                               [tab components + shared UI]
│       │   ├── ExposureTable.tsx                    747  main exposure grid (open + closed rows)
│       │   ├── PnLPanel.tsx                         406  P&L summary panel (Phase 2)
│       │   ├── PeriodPnLPanel.tsx                   509  Net P&L tab — FloatingΔ + Settled live overlay
│       │   ├── EquityPnLPanel.tsx                   321  per-login equity decomposition table
│       │   ├── EquityPnLPage.tsx                     79  3 sub-tabs wrapper (Table/Groups/Rebates)
│       │   ├── EquityPnLClientConfigCard.tsx        267  per-login rebate/PS config UI
│       │   ├── SpreadRebatesCard.tsx                249  per-(login, symbol) spread rebate UI
│       │   ├── LoginGroupsCard.tsx                  582  Phase-2 group CRUD + member assignment
│       │   ├── PositionsGrid.tsx                    105  raw open positions view
│       │   ├── SettingsPanel.tsx                   1330  largest component — 5 sub-tabs of settings
│       │   ├── SnapshotPickerModal.tsx              364  pick a specific snapshot as Net P&L Begin
│       │   ├── BridgeSettingsCard.tsx               404  Centroid creds + mode (Stub/Live)
│       │   ├── SymbolMappingAdmin.tsx               405  symbol mapping CRUD (B-Book ↔ Coverage)
│       │   ├── DateRangePicker.tsx                  197  shared picker + presets (T/Y/W/M kbd)
│       │   ├── RiskBanner.tsx                       150  top-of-page net-volume / hedge% watchdog
│       │   ├── ConnectionHealthDots.tsx              76  4 dots in topbar (MT5/Coll/Centroid/Supa)
│       │   ├── KeyboardShortcutsOverlay.tsx         149  '?' opens cheat-sheet
│       │   ├── UserGuideOverlay.tsx                 119  in-app user guide
│       │   ├── ConfirmDialog.tsx                    118  standard destructive-action confirm
│       │   ├── ErrorToast.tsx                       143  global throttled/dedupe error toast
│       │   ├── Skeleton.tsx                          96  StaleWrapper + shimmer placeholders
│       │   ├── FlashingCell.tsx                      51  green/red 800ms tint on number change
│       │   ├── HedgeBar.tsx                          43
│       │   ├── SymbolBadge.tsx                       63  2-letter color-coded chip per asset class
│       │   ├── TotalBar.tsx                          69
│       │   ├── AlertBanner.tsx                       46
│       │   ├── AlertHistory.tsx                     207
│       │   └── AlertToast.tsx                       186
│       │
│       ├── pages/
│       │   ├── PositionsCompare/
│       │   │   ├── index.tsx                         62  resizable layout shell
│       │   │   ├── LeftPanel/
│       │   │   │   ├── index.tsx                    199  compact list + expand/collapse
│       │   │   │   ├── SymbolRow.tsx                169
│       │   │   │   └── ExpandedTable.tsx            422  full table matching Exposure layout
│       │   │   └── RightPanel/
│       │   │       ├── index.tsx                     67  empty-state + container
│       │   │       ├── DetailHeader.tsx              58
│       │   │       ├── SummaryCards.tsx              76
│       │   │       ├── PnLRings.tsx                 211  SVG concentric rings (floating ⊕ settled)
│       │   │       ├── CompareTable.tsx             121
│       │   │       ├── PriceChart.tsx               294  canvas timeline + entry/exit markers
│       │   │       ├── VolPnlChart.tsx              208  canvas vol bars + cumulative P&L
│       │   │       ├── LoginsWidget.tsx             166
│       │   │       └── AccountModal.tsx             614
│       │   ├── Bridge/                                    [hidden from dealer nav, backend live]
│       │   │   ├── index.tsx                        155
│       │   │   ├── BridgeFilters.tsx                134
│       │   │   ├── BridgeTable.tsx                  238  CLIENT + N COV_OUT rows with rowSpan
│       │   │   └── pipSize.ts                        29
│       │   └── Markup/
│       │       └── index.tsx                        518  client-vs-coverage VWAP edge analysis
│       │
│       ├── types/
│       │   ├── index.ts                             318  shared TS types (ExposureSummary, PeriodPnL, …)
│       │   ├── compare.ts                            45
│       │   └── bridge.ts                             57
│       │
│       ├── utils/
│       │   └── time.ts                               66  formatBeirut/formatBeirutDate/formatBeirutTime
│       │
│       └── styles/
│           ├── styles.css                           820  CSS custom property palette + base
│           └── styles-extra.css                     243  sidebar/topbar/palette/tweaks classes
│
├── supabase/
│   └── migrations/
│       ├── 20260416_bridge_executions.sql                 + pip_size column on symbol_mappings
│       ├── 20260418_exposure_snapshots.sql                + snapshot_schedules table + seed
│       ├── 20260418_reconciliation_runs.sql
│       └── 20260419_equity_pnl.sql                        + Phase 2 groups (applied inline)
│
├── docs/
│   ├── ARCHITECTURE.md                                    existing prose architecture doc
│   ├── ARCHITECTURE_MINDMAP.md                            ← this file
│   ├── DEPLOYMENT.md                                      production deploy runbook + workarounds
│   ├── SETUP.md                                           local-dev setup
│   ├── USER_GUIDE.md                                      dealer-facing user guide
│   └── centroid/                                          captured Centroid CS 360 API spec
│       ├── README.md
│       ├── dropcopy-fix-4.4.md                            FIX 4.4 Dropcopy spec
│       ├── database-specification.md                      paid Postgres DB replica spec
│       ├── rest-and-websocket.md                          REST + WS spec
│       ├── cs360-rest-openapi.json                        OpenAPI spec
│       └── cs360-realtime-asyncapi.json                   AsyncAPI spec
│
├── _deploy/
│   └── deploy.ps1                                         NSSM service mgmt + npm ci/install fallback
│                                                          + MT5 DLL flatten verification
│
├── design-ref/                                            tracked UI reference mocks (App.jsx, etc.)
├── CoverageManager.sln                                    .NET solution file
├── CLAUDE.md                                              definitive architecture doc — READ FIRST
├── CHANGELOG.md
├── README.md
├── ideas.md
├── .env.example
└── .taskmaster/                                           Task Master setup
```

**Branch model** (from CLAUDE.md): `live` (default, production, branch-protected) · `main` (snapshot) · `dev` (snapshot). New feature branches cut from `live`. Post-merge sync via `gh api PATCH /git/refs/heads/{main,dev}`.

---

## View 3 — Persistence Layer

### All 25 Supabase tables — live row counts as of 2026-05-07

Project: `svhmhcqopkdgccnzgvzp` (eu-central-1, Frankfurt). RLS enabled on every table.

| # | Table | Rows | Domain | Written by |
|---|---|---:|---|---|
| 1 | `symbol_mappings` | **35** | Reference | UI (Mappings tab) + read by `MappingRefreshService` |
| 2 | `positions` | 0 | Transactional | (snapshot table — currently unused, in-memory `PositionManager` is authoritative) |
| 3 | `deals` | **446,672** | Transactional | `DataSyncService` (30s) + `CashMovementSyncService` (15min) + `ReconciliationService` (nightly) |
| 4 | `trading_accounts` | **50** | Reference | `MT5ManagerConnection.SyncAccountsToSupabaseAsync` (15min) + `CoverageAccountSyncService` |
| 5 | `trade_audit_log` | 0 | Audit | `DataSyncService` on detected modifications |
| 6 | `exposure_snapshots` | **501** | Snapshots | `ExposureSnapshotService` (60s tick, fires due cron schedules) |
| 7 | `pl_summary` | 0 | Phase 2 | (unused — superseded by RPC aggregation) |
| 8 | `hedge_executions` | 0 | Phase 4 | (not implemented) |
| 9 | `economic_events` | 0 | Phase 3 | (not implemented) |
| 10 | `risk_thresholds` | 0 | Phase 3 | (not implemented) |
| 11 | `account_settings` | **2** | Reference | UI (Settings tab) |
| 12 | `alert_rules` | **1** | Phase 3 (live) | UI (Alerts tab) via `AlertsController` |
| 13 | `alert_events` | **7,121** | Phase 3 (live) | `AlertEngine` on rule trigger |
| 14 | `moved_accounts` | **6** | Reference | UI (Settings) — logins removed from MT5 but kept in Supabase |
| 15 | `snapshot_schedules` | **3** | Reference | UI (Settings) — Daily/Weekly/Monthly seeded |
| 16 | `bridge_settings` | **1** | Reference | UI (Settings → Bridge) — Centroid creds + mode |
| 17 | `bridge_executions` | **4,364** | Transactional | `BridgeSupabaseWriter` on each pair update |
| 18 | `reconciliation_runs` | **40** | Audit | `ReconciliationService` after each sweep |
| 19 | `account_equity_snapshots` | **1,660** | Snapshots | `ExposureSnapshotService` (60s tick) — per-login equity |
| 20 | `equity_pnl_client_config` | 0 | Reference | UI (Settings → Equity P&L) |
| 21 | `equity_pnl_spread_rebates` | 0 | Reference | UI (Settings → Equity P&L) |
| 22 | `login_groups` | 0 | Reference | UI (Settings → Login Groups) |
| 23 | `login_group_members` | 0 | Reference | UI (Settings → Login Groups) |
| 24 | `equity_pnl_group_config` | 0 | Reference | UI (Settings → Login Groups) |
| 25 | `equity_pnl_group_spread_rebates` | 0 | Reference | UI (Settings → Login Groups) |

**Note:** `deals` is at **446,672** rows today — CLAUDE.md's "280K+" line is stale. The table grows ~10K/week in live trading.

### Background-persist services — what writes where, when, and why

```
┌──────────────────────────────────────────────────────────────────────────────────────┐
│ SERVICE                          INTERVAL          WRITES TO            BEHAVIOR     │
├──────────────────────────────────────────────────────────────────────────────────────┤
│ DataSyncService              ── 30 seconds ────►  deals,             diffs DealStore │
│ (190 lines)                                       trade_audit_log    vs last sync,   │
│                                                                       upserts new +  │
│                                                                       logs modified  │
│                                                                       (500/batch,    │
│                                                                       on_conflict on │
│                                                                       source+deal_id)│
│                                                                                       │
│ CashMovementSyncService      ── 15 minutes ────►  deals              7-day sliding   │
│ (156 lines)                                                          window scan of  │
│                                                                       MT5 admin      │
│                                                                       balance/credit │
│                                                                       deals (action  │
│                                                                       ≥ 2) — these   │
│                                                                       don't fire     │
│                                                                       OnDealAdd      │
│                                                                       reliably       │
│                                                                                       │
│ ExposureSnapshotService      ── 60 seconds ────►  exposure_snapshots  ticks every    │
│ (195 lines)                                       account_equity_…   minute, fires  │
│                                                                       any due cron   │
│                                                                       from snapshot_ │
│                                                                       schedules via  │
│                                                                       Cronos (DST-   │
│                                                                       aware). Also   │
│                                                                       writes per-   │
│                                                                       login equity  │
│                                                                       on every tick.│
│                                                                       Manual button: │
│                                                                       POST /api/    │
│                                                                       exposure/     │
│                                                                       snapshot      │
│                                                                                       │
│ ReconciliationService        ── 02:05 UTC ─────►  deals (backfill +   nightly + on-  │
│ (269 lines)                     nightly + manual  ghost-delete)       demand. Queries│
│                                                   reconciliation_runs MT5 with ±24h │
│                                                                       buffer (TZ    │
│                                                                       workaround),  │
│                                                                       filters both   │
│                                                                       sides to UTC. │
│                                                                       Backfills      │
│                                                                       missing,       │
│                                                                       upserts modi-  │
│                                                                       fied, DELETEs  │
│                                                                       Supa rows that │
│                                                                       vanished from  │
│                                                                       MT5 + EVICTS   │
│                                                                       from in-mem    │
│                                                                       DealStore      │
│                                                                       (or 30s sync   │
│                                                                       resurrects)    │
│                                                                                       │
│ MappingRefreshService        ── 60 seconds ────►  PositionManager     bidirectional: │
│ (137 lines)                                       (atomic cache swap) reads symbol_  │
│                                                                       mappings from  │
│                                                                       Supa, retries  │
│                                                                       3x w/ 0.5/1.5/ │
│                                                                       4.5s exp.     │
│                                                                       backoff; will │
│                                                                       NOT clobber   │
│                                                                       cache w/ []   │
│                                                                       on failure.   │
│                                                                       /api/exposure/│
│                                                                       diagnostics.  │
│                                                                       mappings      │
│                                                                       exposes state.│
│                                                                                       │
│ BridgeSupabaseWriter         ── on pair update  ►  bridge_executions  upsert on      │
│ (179 lines)                                                          unique         │
│                                                                       client_deal_id│
│                                                                       (cov_fills as │
│                                                                       jsonb array)  │
│                                                                                       │
│ AlertEngine                  ── on tick          ►  alert_events      evaluates      │
│ (191 lines)                                                          alert_rules    │
│                                                                       against live  │
│                                                                       exposure;     │
│                                                                       7,121 events  │
│                                                                       fired to date │
└──────────────────────────────────────────────────────────────────────────────────────┘
```

### Write patterns — real-time vs background

| Pattern | Used by | Latency to Supabase | Notes |
|---|---|---|---|
| **WebSocket-only** (no persist) | `price_update` frames | n/a | Ticks live in `PriceCache` only — never written to Supa. Snapshot every 60s. |
| **WS + immediate-async upsert** | `deal_settled` frame + `BridgeSupabaseWriter` | ~50ms WS, ~1s persist | Frontend overlays the WS delta then drops it on the next REST refresh. |
| **WS + batched-async** | exposure changes via `DataSyncService` | ~10/sec WS, 30s persist | The 30s lag was the source of the Equity P&L NetDepW race (documented in CLAUDE.md). |
| **REST-only (read)** | `aggregate_bbook_settled_pnl`, `aggregate_bbook_pnl_full`, `latest_snapshots_before` RPCs | <1s server-side aggregation | Replaced 46-81s client-side GROUP BY scans. |
| **Background-only** | `MappingRefreshService`, `CashMovementSyncService`, `ExposureSnapshotService` | 15s-60min cadences | No WS path — these update reference/snapshot data dealers don't watch tick-by-tick. |

### Three Supabase RPCs (server-side aggregation)

- `aggregate_bbook_settled_pnl(from_ts, to_ts, excluded_logins)` — Net P&L tab settled column. ~46s → sub-second.
- `aggregate_bbook_pnl_full(from_ts, to_ts, excluded_logins)` — Exposure closed row + P&L tab + Compare full table. ~81s → sub-second.
- `latest_snapshots_before(anchor)` — Net P&L Begin anchor. 3.8K row scan → ≤30 rows.

---

## View 4 — Phase Roadmap

Phase entries below combine CLAUDE.md's `Phase Status` block with what the codebase + Supabase data actually show today. Status legend: ✅ DONE · 🟡 IN PROGRESS · 🔵 DESIGNED · 🔴 BLOCKED · ⚪ PLANNED.

### Phase 1 — Live Exposure View ✅ DONE
- Live B-Book + Coverage exposure unified through canonical symbol mapping with contract-size normalization.
- All 5 dealing-desk concepts (Net Exposure, To Cover, Hedge %, Net P&L, Coverage mirrors direction) shipped.
- **Next:** none — feature-complete; iterating via Phase 2.x sub-phases.

### Phase 2 — P&L Tracking ✅ DONE
- Closed-deal P&L by symbol with buy/sell volume split, coverage P&L toggle, date-range filter.
- **Next:** none.

### Phase 2.5 — Data Persistence ✅ DONE
- Trading accounts + 446,672 deals synced to Supabase. Audit trail. Historical backfill on-demand + nightly via Phase 2.9.

### Phase 2.5 — Bridge Execution Analysis ✅ DONE
- Live REST/WS mode wired to Centroid CS 360 (`RestCentroidBridgeService.cs`, 920 lines).
- Resolves real MT5 deal # on both CLIENT and COV via `DealStore` + `CoverageDealIndex`.
- 4,364 ExecutionPairs persisted in `bridge_executions`.
- Stub fallback (`StubCentroidBridgeService.cs`) still works for dev w/o Centroid creds.
- **Next:** Bridge tab is hidden from the dealer sidebar in the current shell — un-hide once acceptance-tested with the full dealer team.

### Phase 2.7 — Positions Compare ✅ DONE
- Side-by-side client vs coverage with PnLRings widget, resizable left panel, drag-reorder, full-table mode.

### Phase 2.8 — Net P&L Tab ✅ DONE
- `FloatingΔ + Settled` period P&L with snapshot scheduler (3 cron schedules seeded: Daily/Weekly/Monthly @ 00:00 Asia/Beirut).
- Sentinel portfolio-anchor rows (`__SEED_TOTAL`, `__PORTFOLIO`).
- 501 snapshots captured to date.

### Phase 2.9 — Deal Reconciliation Sweep (DATA-101) ✅ DONE
- Nightly + manual sweep with ±24h MT5 TZ buffer, ghost-delete + DealStore eviction.
- 40 reconciliation runs to date, all clean (mt5_count == supa_count, 0 backfills, 0 ghosts).
- Beirut TZ alignment across every surface; perf RPCs (81s → 0.5s).

### Phase 2.10 — Equity P&L Phase 1 ✅ DONE
- Per-login balance reconciliation, PS HWM engine, commission + spread rebate config.
- Cent-perfect vs MT5 Summary report on 39/40 logins (1 login residual = TZ-boundary issue, see CLAUDE.md `Operational Learnings`).

### Phase 2.11 — Equity P&L Phase 2 (Login Groups) ✅ DONE (DATA SHIPPED, NOT YET POPULATED)
- Tables shipped (`login_groups`, `login_group_members`, `equity_pnl_group_config`, `equity_pnl_group_spread_rebates`) — all currently 0 rows.
- Priority-based resolution: login → group → default.
- **Next:** dealer-driven population — once they define their groups (e.g. `VIP-TierA`, `IB-Lebanon`), rates can be set per-group instead of per-login.

### Phase 2.12 — UI Shell Redesign ✅ DONE
- Linear/Retool-style sidebar + topbar with metric tiles + ⌘K palette + tweaks drawer.
- Per-asset SymbolBadge + Equity P&L sub-tabs + clickable UNMAPPED → Mappings deep-link.
- Bridge + Markup hidden from dealer nav.

### Phase 2.13 — MT5 Load Reduction ✅ DONE
- Stage 2b: position poll dropped 500ms → 60s using event sinks. -98.7% on `getPositions` (4459/min → 58/min) with zero drift in steady state.
- Account sync widened 5min → 15min. Native API call counters in `/api/exposure/diagnostics`.

### Phase 2.14 — Equity P&L Cleanup ✅ DONE
- DealStore `EarliestDealTime` guard (no phantom NetDepW).
- `CashMovementSyncService` 15min loop for admin balance/credit deals.
- NetDepW/NetCred from deal-sum (matches MT5 Summary 1:1 on 39/40 logins).
- Single "Net Cash Movement" column on dashboard.

### Phase 2.15 — Live Floating P&L ✅ DONE
- `ExposureEngine` consumes `PriceCache`, recomputes per-position floating via calibrated-delta.
- Net P&L tab subscribes to WS, overlays `currentFloating` from every frame; REST poll dropped 2s → 30s (-93% calls).

### Phase 2.16 — Live-Price Fast Path ✅ DONE
- Split `/ws/exposure` into `exposure_update` (full state, 10/s) + `price_update` (light, 20/s).
- 33K ticks/min in → 1,016 frames/min out (97% coalescing).
- Bid-price staleness pill (amber >3s, red >10s).

### Phase 2.17 — Live Floating P&L on Fast Path ✅ DONE
- `ExposureEngine.GetFloatingPnLPerSymbol()` (~10× cheaper than full `CalculateExposure`).
- Floating P&L cells now tick at 20Hz (was ~7Hz, position-event-gated).

### Phase 2.18 — WebSocket permessage-deflate ✅ DONE
- `DangerousEnableCompression = true` on both `/ws/*` accepts.
- ~78% wire savings (110,624 → ~24,769 bytes on a 5-frame sample). Critical for dealers on WAN.

### Phase 2.19 — Live SETTLED Column ✅ DONE
- `deal_settled` WS frame fires unthrottled the moment OnDealReceived runs.
- Frontend `pendingSettled` Map dedupes by dealId, drops on next REST refresh.
- ~50ms latency from deal close to Net P&L cell tick (was 30-60s).

### Phase 2.19a — Picker Exact-Match Anchor ✅ DONE
- `SnapshotPickerModal` lets dealer pick a specific snapshot as Net P&L Begin.
- `GetSnapshotsAtAsync(exact)` replaces `latest_snapshots_before` for picked snapshots — no sentinel pollution.

### Phase 2.20 — WebSocket Frame-Lag Watchdog ✅ DONE
- 1Hz watchdog dispatches `LAGGING:true` after 5s, force-closes WS after 15s.
- Closes the silent-staleness failure mode where ISP/NAT/proxy stops forwarding frames while `readyState === OPEN`.

### Phase 2.21 — Resilient Mapping Cache ✅ DONE
- `GetMappingsAsync` retries 3x w/ 0.5/1.5/4.5s backoff (generic `RetryAsync<T>` helper).
- `MappingRefreshService` 60s tick + atomic cache swap. Empty-result safety: never clobbers populated cache with `[]`.
- `/api/exposure/diagnostics.mappings` exposes `{lastFetchCount, lastFetchAtUtc, lastFetchOk, consecutiveFailures}`.

### Phase 3 — Risk Alerts 🟡 IN PROGRESS
- **Shipped:** `AlertEngine.cs` (191 lines), `AlertsController.cs` (123 lines), `AlertBanner` + `AlertHistory` + `AlertToast` UI components, tables `alert_rules` (1 rule defined) + `alert_events` (**7,121 events fired**).
- **Not yet shipped:**
  - `economic_events` table — 0 rows, no controller, no UI for news-event alerts.
  - `risk_thresholds` table — 0 rows. Per-symbol risk limits surfaced via `RiskBanner` localStorage instead of being persisted server-side.
- **Next actionable:** wire economic-calendar feed → `economic_events` ingest, then add news-event alert rules to `AlertEngine`.

### Phase 3.5 — Bridge Tab Production Rollout 🔵 DESIGNED
- Backend complete (Phase 2.5 Bridge). UI hidden.
- **Next actionable:** un-hide Bridge tab in `Sidebar.tsx`; run dealer-team acceptance test for ≥1 trading week; address feedback.

### Phase 4 — Hedge Execution ⚪ PLANNED
- Goal: one-click hedging via LP terminal. `mt5.order_send()` is ready in the Python collector.
- `hedge_executions` table exists (0 rows), no controller, no UI.
- **Next actionable:** design the hedge-decision UX (which symbols, what size, who approves) before wiring `mt5.order_send`. Risk surface is large; should be gated behind an explicit approval step.

### Cross-cutting open items (not phase-tagged in CLAUDE.md but visible in `Operational Learnings`)

- **Collector silent hangs** 🔴 BLOCKED — Python collector's sync `MetaTrader5` calls block the asyncio loop. Stopgap: Windows Scheduled Task `nssm restart` after 3 consecutive `/health` failures. Planned fix: `await asyncio.wait_for(asyncio.to_thread(fn, ...))`.
- **Equity P&L NetDepW race** 🔵 DESIGNED — read trade-flow from in-memory `DealStore` instead of Supabase to remove the 30s lag. Fallback to Supabase when DealStore is cold.
- **Date-boundary mismatch with MT5 Manager** 🔵 DESIGNED — strategy TBD between (a) switch app to UTC-midnight or (b) add TZ toggle to date pickers.
- **No coverage section in Equity P&L** 🔵 DESIGNED — needs collector `/account` endpoint + sync path.

---

*Generated 2026-05-07. To regenerate, re-run the live scan in CLAUDE.md's `Verification` section + Supabase MCP `list_tables` against project `svhmhcqopkdgccnzgvzp`.*
