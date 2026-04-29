# Coverage Manager — Dealer User Guide

Real-time exposure and P&L dashboard for a forex dealing desk. Shows B-Book client activity alongside LP coverage activity side-by-side.

> **All dates and times on every screen are Asia/Beirut (MT5 server time).** The app converts your picker dates to UTC for storage and back to Beirut for display automatically. You never need to think in UTC.

---

## 1. Starting the app

Three processes need to be running for a full picture:

| # | Component | Command | What it gives you |
|---|---|---|---|
| 1 | Python collector | `cd collector && uvicorn main:app --host 0.0.0.0 --port 8100` | LP (coverage) positions, deals, account data |
| 2 | C# backend | `cd src/CoverageManager.Api && dotnet run` | B-Book live feed + all REST/WebSocket endpoints |
| 3 | Web dashboard | `cd web && npm run dev` | UI at http://localhost:5173 |

The Python collector needs MT5 Terminal open with the LP account (96900) logged in and "Allow algorithmic trading" enabled. The C# backend connects to the MT5 Manager API directly (no terminal needed on the B-Book side).

**Connection health dots** at the top-right of every screen show MT5 / Collector / Centroid / Supabase status. Hover for details. Amber = degraded, red = down, gray = unknown.

---

## 2. Shell — sidebar + top bar (always visible)

### Sidebar (left edge)
Three grouped sections with keyboard shortcuts:

| Group | Tabs | Kbd |
|---|---|---|
| **Real-time** | Exposure · Positions · Compare | `1` / `2` / `3` |
| **P&L** | P&L · Net P&L · Equity P&L | `4` / `5` / `6` |
| **Config** | Mappings · Alerts · Settings | — |

Click the brand logo (top) to collapse the sidebar to a 56-px icon strip when you need more horizontal room for a wide table. The collapsed state persists across reloads.

### Top bar (right side)
- **Metric tiles** — live Client Exposure / Coverage / **Net P&L Today** in always-visible totals across every symbol.
- **Connection dots** (4-dot cluster) — MT5 / Collector / Centroid / Supabase. Hover for detail. Green=ok, amber=degraded, red=down, gray=unknown.
- **Search / ⌘K** — opens the **Command Palette** — fuzzy jump-to-tab + quick actions. Works from anywhere in the app. Arrow keys to nav, Enter to fire, Esc to close.
- **Alert bell** — unacknowledged count badge; click to open alert history.
- **Book icon** — opens this user guide inside the app.
- **Theme toggle** — dark / light. Persisted.
- **Tweaks icon (sliders)** — opens a right-edge drawer for dealer-configurable preferences: accent color (blue/teal/purple), density (compact/cozy/spacious), grid lines, animate-ticks.
- **`?` anywhere** — opens the keyboard shortcut cheat-sheet.

### RiskBanner (below top bar)
Amber/red watchdog. Trips when **either**:
- Total `|net volume|` crosses 50 lots amber / 150 lots red (dealer-adjustable via inline "Thresholds" control), **or**
- Any meaningful-volume symbol (>5 lots) sits below 80% hedged.

Message shows total unhedged lots + worst symbol + its hedge% and net lots so you know where to look first.

---

## 3. Tabs

### 3.1 Exposure
**The main live view.** Two rows per symbol:

- **O (Open)** — live net volume + floating P&L from currently open positions.
- **C (Closed)** — realized P&L from deals closed in the picker range.

Three groups side by side: **Clients (blue)** / **Coverage (teal)** / **Summary**.

Key columns in the Summary group:
- **Net Volume** = `BBookNet − CoverageNet`. Negative means the broker is net short and may want to BUY more on the LP to hedge.
- **To Cover** = same number. Green positive = "BUY more", red negative = "SELL more". No text labels — sign does the work.
- **Hedge %** = `|CoverageNet| / |BBookNet|`. ≥80% green, ≥50% amber, <50% red. Can exceed 100% if the LP side over-hedges.

Date picker (top-right) filters the Closed row. **Beirut-local dates** — midnight Asia/Beirut → UTC with DST. The picker state is shared across Exposure, P&L, Net P&L, Compare Full Table, and Markup.

Preset buttons: Today / Yesterday / Week / MTD / 7D / 30D. Keyboard: `T` / `Y` / `W` / `M`.

Other controls: symbol drag-reorder, sort dropdown, grid-line toggle — all persisted to localStorage.

**Symbol badges** — 2-letter colored chip beside each symbol for quick asset-class recognition: amber for metals (XA), blue for indices (US30/NAS/UK100), teal for energy, purple for crypto, grey for FX.

**UNMAPPED badge** — a symbol on the exposure table that isn't in `symbol_mappings`. **Click the badge** → jumps to the Mappings tab with the new-mapping form pre-filled so you can save the row in one click.

**Bid price under each symbol** — refreshes ~20 times per second on active symbols (lightweight `price_update` WebSocket frame, separate from the heavier full-state push so prices don't queue behind exposure recomputes). Color flashes green/red on each tick to signal direction. **Staleness pill:** if the price hasn't ticked for >3 s the value dims to grey and a small amber dot appears beside it; >10 s it dims to red with 50 % opacity. Hover the price for "Price last updated Ns ago — MT5 may not be delivering ticks for this symbol". This separates "MT5 isn't sending ticks for this symbol right now" (server-side, common on quiet symbols outside major sessions) from "the app is stuck" (which would also turn the diagonal-hatch StaleWrapper on the whole screen — see §7).

### 3.2 Positions
Raw open positions across both sides. One row per position. Use for audit / forensic work — e.g. "why is this symbol showing −50 lots?"

### 3.3 P&L
The **original** settled-only P&L tab. Closed-deal profit per symbol for the picker range. B-Book and optionally Coverage P&L. No concept of floating — use Net P&L for a fuller picture.

### 3.4 Net P&L
**The period P&L tab.** For a date range it decomposes broker P&L as:
```
FloatingΔ = CurrentFloating − BeginFloating
Net       = FloatingΔ + Settled
Edge Net  = Coverage.Net − Clients.Net     (positive → broker profited)
```

- **Begin** comes from the latest `exposure_snapshots` row at or before the picker's **from** date (00:00 Asia/Beirut).
- **Current** is live `ExposureEngine` output — date-independent.
- **Settled** is the same aggregation the P&L tab uses, scoped to the picker range.
- **Amber dot** on Begin means no snapshot exists for that symbol before the range → Begin treated as 0.
- **"—" on Begin/Current/ΔFloat** means that side has zero live volume (no open position); Settled/Net still show real realized P&L.

Toolbar actions:
- **Capture Snapshot Now** — immediately writes a row per symbol to `exposure_snapshots` with `trigger_type='manual'`. Optional label.
- The result refreshes automatically every 10s.

Loading behavior: date-change fetches show a ≥450ms amber pill; interval polls refresh silently. Revisiting the tab uses a cached result, then refreshes in the background.

### 3.5 Equity P&L
**The accountant's view — per-login balance reconciliation.** Separate from Net P&L (which is exposure-driven).

Columns: `Login · Begin Eq · Net Dep/W · Net Cred · Comm Reb · Spread Reb · Adj · PS · Supposed Eq · Current Eq · PL · Net PL`

Math:
```
Supposed Eq      = Begin + NetDep + NetCred
NetDepW          = (CurrentBalance − BeginBalance) − Σ(trade-flow)
NetCred          = CurrentCredit − BeginCredit
PL               = CurrentEq − Supposed Eq
NetPL (client)   = PL − CommReb − SpreadReb − Adj − PS
NetPL (coverage) = PL + CommReb + SpreadReb + Adj + PS      (sign flipped — income for broker)
Broker Edge      = −Σ(Clients.NetPL) + Σ(Coverage.NetPL)
```

Sections:
1. **Clients (B-Book)** — one row per MT5 login visible to the Manager API.
2. **Coverage (LP)** — one row per LP account synced via the collector (currently 96900).
3. **Broker Edge total** at the very bottom.

**UNMAPPED** badges flag symbols that don't map back to any canonical; these silently fall back to raw prices and should be added to `symbol_mappings`. (Exposure tab's UNMAPPED badges are clickable — see §3.1.)

**Sub-tabs** at the top of the Equity P&L tab:
- **Table** — the 12-column per-login breakdown (default view).
- **Login Groups** — create/edit named groups (VIP-TierA, IB-Lebanon, Retail, …) with per-group commission rebate % and profit-share % that apply to every member login unless the login has its own override.
- **Spread Rebates** — per-(login, symbol) or per-(group, symbol) USD/lot rate. Login-level overrides group.

Same tables used to live under Settings → Equity P&L sub-tab; they now sit alongside the data they configure.

### 3.6 Compare
Side-by-side client vs coverage per symbol. Left panel is a compact list (Symbol · Hedge% · Net CLI/COV/Δ · P&L CLI/COV/Δ), draggable. Drag the right edge of the left panel to resize. Click a symbol → right panel opens with:
- **DetailHeader** — symbol, vol, P&L, hedge pill
- **5 SummaryCards** — avg entry, avg exit, volume, P&L, net combined
- **PnLRings** — concentric rings (inner floating, outer settled) with broker-edge math
- **CompareTable** — trades, volume, win rate, P&L per side

"Full Table" toggle expands the left panel into a full Exposure-style layout (Open/Closed rows, date picker, To Cover, Hedge %, TOTAL footer).

### 3.7 Markup (hidden in current build)
Per-symbol broker markup analysis — client P&L vs coverage P&L gives the broker's edge in dollar terms, and VWAP(client entry) − VWAP(coverage entry) gives the price edge in pips. **Removed from the sidebar for the current dealer persona**; the underlying route (`/api/markup/match`) and component are still in the codebase. Restore in three lines of `Sidebar.tsx` + one line of `App.tsx` if you want it back.

### 3.8 Bridge (hidden in current build)
Post-trade execution analysis from the **Centroid CS 360 Dropcopy feed** (FIX 4.4). Each CLIENT fill is paired with its COV OUT coverage legs via FIX OrderID (tag 37). Table shows one CLIENT row + N COV OUT rows per pair, with shared Symbol / Price Edge / Pips cells. Requires Centroid FIX Dropcopy creds + IP whitelisting. **Removed from the sidebar for the current dealer persona**; API endpoints and WebSocket feed still run. Restore the same way as Markup.

### 3.9 Mappings
Manage `symbol_mappings`: B-Book symbol ↔ canonical ↔ Coverage symbol + contract sizes + optional `pip_size` override. Any symbol flagged **UNMAPPED** on other tabs should end up here.

### 3.10 Settings
Organized into 5 sub-tabs:

#### Connections
- MT5 Manager account credentials (B-Book side).
- Coverage account credentials (Python collector reads these on startup).

#### Equity P&L
- **Client Config card** — per-login Commission Rebate %, Profit Share %, PS contract start date.
- **Spread Rebates card** — per-(login, symbol) USD/lot rate.
- **Login Groups card** — create named groups (e.g. `VIP-TierA`, `IB-Lebanon`), add logins with a priority, set group-level rebate/PS config and group spread rates.

Rate resolution at request time: `login-specific override → highest-priority group → 0`.

#### Snapshots
- **Snapshot Schedules** — three seeded rows (Daily / Weekly / Monthly, all 00:00 Asia/Beirut). Enable/disable, edit cron, delete, **Run Now**.
- **Snapshot History** — recent captures grouped by `snapshot_time`, expandable per-symbol.

#### Data Integrity
- **Deal Verification** — compares MT5 Manager deals vs Supabase for a date range. `Fix=true` upserts the missing ones. Risk-warning confirm dialog.
- **Reconciliation** — audit log of the nightly sweep + Run Now.

#### Reference
- Moved Accounts — hide logins from the dashboard without losing their historical deals.
- Alert Rules — configure thresholds (by symbol, side, operator, severity).

---

## 4. Keyboard shortcuts

Every shortcut fires when focus is outside an input (except ⌘K which works everywhere).

| Key | Action |
|---|---|
| `1` | Exposure |
| `2` | Positions |
| `3` | Compare |
| `4` | P&L |
| `5` | Net P&L |
| `6` | Equity P&L |
| `⌘K` / `Ctrl+K` | Open Command Palette |
| `?` | Keyboard shortcut cheat-sheet |
| `Esc` | Close any modal / overlay |
| `T` | Today (date picker) |
| `Y` | Yesterday |
| `W` | This week |
| `M` | Month-to-date |

---

## 5. Daily dealer workflow

**Morning (before London open):**
1. Check topbar metric tiles + RiskBanner. Any red means immediate attention.
2. Net P&L → Today preset. Scan Begin → Current movement per symbol.
3. Exposure tab → verify hedge % on top-traffic symbols (XAUUSD, US30, GOLD, etc).

**Mid-session:**
1. Exposure tab open, RiskBanner visible. Flashing cells flag P&L ticks.
2. Compare tab for any symbol whose hedge pill goes amber/red.
3. If the Collector dot goes red: MT5 Terminal has disconnected — restart the terminal. The collector auto-reconnects once MT5 is up.

**End of day (Beirut midnight):**
1. Daily snapshot fires automatically at 00:00 Asia/Beirut → tomorrow's Begin anchor.
2. Review Net P&L → Yesterday preset for a full-day settled + floating breakdown.
3. Equity P&L → same range to compare to MT5 Summary Report (should be cent-perfect).

**Weekly:**
1. Reconciliation card → confirm nightly sweeps ran with 0 backfills and 0 ghosts.
2. Deal Verification → spot-check a busy symbol over the last 7 days.

---

## 6. Terminology cheat-sheet

| Term | Meaning |
|---|---|
| **B-Book / Client** | Orders executed on the broker's internal MT5 server — broker is counterparty. |
| **Coverage / LP** | Hedge legs sent to the liquidity provider's MT5. When clients sell, broker sells on LP. |
| **Floating P&L** | Unrealized P&L on currently-open positions. |
| **Settled P&L** | Realized P&L from closed deals in a time window. |
| **Net Volume** | `BBookNet − CoverageNet`. How far the broker is from fully hedged. |
| **To Cover** | Same number, presented as a hedging instruction. Sign matters. |
| **Hedge %** | Fraction of B-Book net volume mirrored on LP. |
| **Canonical symbol** | Normalized symbol name (e.g. `XAUUSD`) that maps both broker and LP raw symbols (`XAUUSD.c`, `GOLD`). |
| **Broker Edge** | `Coverage P&L − Client P&L`. Positive = broker profited from the spread/mark-up. |
| **Begin Anchor** | UTC instant corresponding to 00:00 Asia/Beirut of the picker's `from` date. |
| **IN deal / OUT deal** | MT5 deal entry type: IN opens/increases, OUT closes/decreases. Only OUT carries profit. |

---

## 7. Common issues

**"Coverage section empty on Equity P&L"**
- Collector disconnected. Check health dots. Restart MT5 Terminal and leave the collector running — it polls `/account` every 5 min and the row reappears on the next tick.

**"Amber dot on Begin in Net P&L"**
- No snapshot exists before the selected range for that symbol. Either it's a new symbol, or snapshots weren't running yet. Click **Capture Snapshot Now** to anchor today, or ignore — missing Begin is treated as 0 by design.

**"MT5 dot is red"**
- Manager API lost connection. Check the backend log — usually transient; it auto-reconnects within 30s and re-runs the bring-up sequence (SelectedAddAll → GetUserLogins → SnapshotPositions → BackfillDeals → subscribe ticks/deals).

**"UNMAPPED badge next to a symbol"**
- That symbol isn't in `symbol_mappings`. P&L still computes (falls back to a raw string compare) but contract-size conversion won't be applied to the coverage side. Add a row in the Mappings tab.

**"HR sub-accounts (5222/5231/5237/…) missing from Equity P&L"**
- These accounts live in a group the B-Book Manager login can't see (Manager API scope limitation). Requires a Manager credential with wider permissions. Not fixable from the UI.

**"Deal counts don't match MT5 Summary Report"**
- Run the Reconciliation sweep (Settings → Data Integrity). It uses a ±24h buffer to work around MT5's timezone quirks. Repeat runs should show 0 backfills and 0 ghosts on clean data.

**"Data looks stale / diagonal hatch overlay"**
- Exposure WebSocket dropped. Check the backend. The StaleWrapper dims the whole screen automatically.

**"One symbol's bid price has a small amber dot / is dimmed"**
- That price hasn't received a tick from MT5 in >3 s. Different from the screen-wide stale overlay above — the WebSocket is healthy, MT5 just isn't delivering ticks for that specific symbol right now. Common on illiquid symbols outside major sessions. If it persists across all symbols, check `/api/exposure/diagnostics` → `tickEvents.lastAt` (older than 5 s during market hours = MT5 server-side issue, not the app).

---

## 8. Support

- **Source & issues**: https://github.com/amakki-a11y/coverage-manager
- **Architecture docs**: `docs/ARCHITECTURE.md`
- **Setup**: `docs/SETUP.md`
- **Centroid FIX reference**: `docs/centroid/`
- **Project conventions + file inventory**: `CLAUDE.md` in the repo root.
