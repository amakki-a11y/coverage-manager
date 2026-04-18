# Centroid CS 360 Bridge — Integration Notes

Captured from https://bridge.centroidsol.com/docs/361234633 on 2026-04-16.

## What Centroid actually exposes

There is **no REST API**. The bridge provides four integration surfaces:

| Surface | Protocol | Use |
|---|---|---|
| [Centroid Database Specification](./database-specification.md) | Postgres replica | Historical queries (TradesDB + ConfigDB). Provisioned on request by support@centroidsol.com. |
| [Centroid Dropcopy API](./dropcopy-fix-4.4.md) | **FIX 4.4** | Post-trade feed — every fill as an Execution Report. **This is what Coverage Manager uses for the Bridge tab.** |
| Centroid Maker FIX API | FIX 4.4 | For liquidity providers making quotes. Not used. |
| Centroid Taker FIX API | FIX 4.4 | For placing orders into LPs. Not used — the bridge does this itself. |

## Why Dropcopy

Dropcopy is a **receive-only** feed. The bridge pushes one Execution Report per fill over a persistent FIX 4.4 session. Every client fill AND every LP hedge leg flows through this single stream — we distinguish them by the Group (tag 90002) and/or Maker Name (tag 90015).

## Prerequisites to connect

Confirmed from the spec:
1. IP whitelist — our backend server's IP must be pre-approved by Centroid
2. Credentials issued by Centroid support:
   - `SenderCompID` (ours)
   - `TargetCompID` (theirs, `CENTROID_SOL` in the docs examples)
   - Username (`553`)
   - Password (`554`)
   - Host + port of the FIX server
3. Session times (Centroid tells us when the session is open)
4. Giveup rule must be configured on Centroid's side
5. Timestamp precision is 3+ digits (ms or μs) — always UTC

## Implementation plan

- Use **QuickFIX/n** (`QuickFIXn.Core` on NuGet, MIT licensed) as the FIX engine on the C# backend.
- One `CentroidBridgeService` IHostedService holds the FIX session, parses Execution Reports, and pushes normalized `BridgeDeal` records into a `BridgePairingEngine`.
- Until credentials + IP whitelisting are in place, the service runs in `Stub` mode emitting synthetic deals so the UI and pairing logic can be built end-to-end.

## Modes supported by `CentroidBridgeService`

| Mode | Purpose |
|---|---|
| `Live` | Real FIX 4.4 session via QuickFIX/n. Needs approved credentials + whitelist. |
| `Replay` | Reads a captured `.log` of FIX messages — used in unit/integration tests. |
| `Stub` | Generates synthetic Execution Reports — lets the frontend be developed without Centroid access. |

Set via `appsettings.json` → `Centroid:Mode`.

## See also

- `dropcopy-fix-4.4.md` — full tag reference, session flow, example messages
- `database-specification.md` — notes on the Postgres replica option
