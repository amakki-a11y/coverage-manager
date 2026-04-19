# Coverage Manager UI/UX Review — 2026-04-17

Full review covering every surface (Exposure, Positions, P&L, Net P&L, Compare,
Markup, Bridge, Mappings, Settings, Global chrome). Scored against a dealer-grade
terminal rubric (Bloomberg / TradingView / IBKR / Centroid / TIP).

**See Task Master entries 65–79 for individual work items derived from this review.**

## Overall Assessment

Coverage Manager is functional and surfacing real Centroid data end-to-end, but
it is not yet a dealer-grade terminal. Information architecture is sound; cell-
level craft is missing:

- Typography is generic system sans + OS monospace
- `tabular-nums` is absent everywhere
- Minus sign is ASCII hyphen, not U+2212
- Price decimals drift between tabs
- Real-time updates repaint whole rows instead of flashing cells
- Status dot aggregates 4 independent data sources, hiding drops
- No at-a-glance risk signal on the Exposure tab

Biggest UX debt: the missing "is my book OK?" summary. Second: connection-
health granularity.

## Severity Breakdown

- CRITICAL: 7 issues (C1–C7) — cause misread data / missed state / wrong call
- HIGH: 13 issues (H1–H13) — sprint-scoped design-system violations
- MEDIUM: 13 issues (M1–M13) — consistency drift, polish
- LOW: 10 issues (L1–L10) — micro-interactions

## Per-Screen Scores (out of 10)

| Screen | Density | Legibility | Decision support | Real-time | Consistency |
|---|---|---|---|---|---|
| Exposure view | 7 | 5 | 5 | 6 | 6 |
| Positions Compare | 7 | 6 | 7 | 6 | 5 |
| P&L Panel | 6 | 5 | 4 | 5 | 4 |
| Net P&L Panel | 7 | 6 | 7 | 5 | 6 |
| Bridge tab | 8 | 6 | 5 | 8 | 4 |
| Markup tab | 6 | 5 | 6 | 4 | 5 |
| Symbol Mapping Admin | 7 | 6 | 6 | — | 7 |
| Settings | 7 | 6 | 5 | — | 6 |
| Global chrome | 8 | 5 | 3 | 5 | 4 |

## Design System Compliance (key failures)

- ❌ JetBrains Mono / DM Sans not loaded
- ❌ `tabular-nums` not applied anywhere
- ❌ U+2212 minus not used (all ASCII hyphens)
- ❌ Inline hex colors in several components
- ❌ No focus-visible styles globally
- ⚠️ SymbolRow uses blue for positive (breaks green-positive convention)
- ⚠️ `THEME.t3` on `THEME.bg2` contrast = 4.2:1 (just below WCAG AA)

## Top 5 Highest-Impact UI Improvements

1. Unified typographic stack (JetBrains Mono + DM Sans + tabular-nums + U+2212)
2. Per-source connection health dots + last-update timestamps
3. Risk banner on Exposure tab for threshold breaches
4. Hedge-% bar visualization on every symbol row
5. Cell-level flash-on-change across Exposure / Compare / Bridge

## Proposed Token Additions to theme.ts

```ts
export const MONO = '"JetBrains Mono", ui-monospace, "SFMono-Regular", Menlo, Consolas, monospace';
export const SANS = '"DM Sans", -apple-system, "Segoe UI", Roboto, sans-serif';
export const NUM  = { fontFamily: MONO, fontVariantNumeric: 'tabular-nums' } as const;
```

## Top-Bar Refactor Sketch (C1)

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ CVRG  │  B-BOOK  −$104,199  │  COVERAGE  −$10,797  │  NET  +$93,402         │
│       │  UNHEDGED  29.21                                                     │
│       │  MT5●  COLLECTOR●  CENTROID●  DB●   updated 02:13:05                 │
└──────────────────────────────────────────────────────────────────────────────┘
```

Each dot: hover-label "MT5 Manager — connected 2h 14m". Grey when stale. Red
when down for >10s.

## Phase Readiness Snapshot

- Phase 2 (P&L + Compare): ⚠️ Acceptable, not strong — density OK, closed/open
  weighting uniform, realized/unrealized labeled equally
- Phase 2.5 (Bridge): ⚠️ Functional — SIDE enum bug, coverage-ratio unencoded
- Phase 3 (AI advisor): Not started — see sketch in Task 78
- Phase 3.5 (Manual confirmation): Not started — Task 78
- Phase 4 (Autonomous): Kill switch UI must be persistent top-bar

## Missing UI Pieces

- Per-data-source connection health
- Unmapped-symbol warning
- Risk banner (threshold breaches)
- Hedge-% bar
- Cell-level flash-on-change
- Keyboard shortcut overlay (`?`)
- Per-column number formatter
- Skeleton loading rows
- Global error toast
- Destructive-action confirmations
- Last-updated timestamp per tab
- Stale-data tint + reconnecting chip

---

_Source: UI/UX review run 2026-04-17. Full tables of each severity tier and
per-component evidence live in this session's final turn; the tasks below
reference that detail._
