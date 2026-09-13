# v2 parallel-run readiness (beside live v1, same server)

**Status:** readiness list, 2026-09-13. Nothing here has been executed. v2 has **not** been
started against the real feed; `:5000` and v1 are untouched. Verified facts are marked
*verified*; everything else is a requirement or a recommendation.

**Principle:** during the parallel run v2 shares **nothing that writes or identifies** with v1
(port, binaries, feed key, resume state, service, deploy path, store) and uses shared services
only **read-only** (the coverage collector, Postgres is v2-only).

---

## 0. Must be resolved BEFORE anyone starts v2 (hazards found)

| # | Hazard | Evidence | Required action |
|---|---|---|---|
| **H1** | **An accidental v2 launch would displace live v1 on the feed.** This Windows account (`makkioo`) has a user-scope env var `LiveBridge__ApiKey` (43 chars). v2's only B-Book provider is the feed, and `appsettings.json` already points at `wss://feed.connecttrader.app:5571/feed/BBcorp-Live`. Any v2 process started under this account -- `dotnet run`, double-clicking the exe, a scheduled task -- inherits that key and dials as `coverage-manager`, which the bridge treats as a replacement of v1's connection. | *verified*: user-scope var present; v1 itself does **not** use it (v1 is a LocalSystem NSSM service with its own key in its NSSM environment, see §2). | **Owner:** remove the stale user-scope `LiveBridge__ApiKey` from `makkioo` (it is not what v1 uses). **Build (recommended):** a default-off dial interlock, e.g. `LiveBridge:Enabled=false` in `appsettings.json`, so no launch can dial unless its service env explicitly arms it. |
| **H2** | **v2 would see no coverage positions.** The collector *pushes* positions to one target, `BACKEND_URL` (v1, `:5000`). `Coverage:PollFallbackEnabled` exists in `appsettings.json` but **nothing reads it** -- dead config. v2 would show every hedge at 0% and the comparison would be meaningless. | *verified*: collector NSSM env has only `BACKEND_URL`; no C# reader of `PollFallbackEnabled`. | **Build (recommended):** a v2-side read-only poller of the collector's existing `GET /positions` (collector untouched). Alternative -- collector pushes to both backends -- changes production collector config; owner decision. |
| **H3** | **The existing deploy script would replace v1 with v2.** `_deploy/deploy.ps1` publishes to `publish\api-staging` and swaps it into `C:\CoverageManager\publish\api` with `nssm stop/start coverage-api` -- **v1's live binaries and service**. Run from the v2 branch it is an accidental cutover. | *verified*: `$publishDir = <repo>\publish\api`; v1 runs from exactly that folder. | **Never use `_deploy/deploy.ps1` for v2.** v2 gets its own folder and service (§3). |

---

## 1. Isolation matrix

| Resource | v1 (live) *verified* | v2 (parallel run) | Why it must differ |
|---|---|---|---|
| Service | NSSM `coverage-api`, LocalSystem | NSSM `coverage-api-v2`, LocalSystem | independent stop/start |
| Binaries + workdir | `C:\CoverageManager\publish\api` | `C:\CoverageManagerV2\app` | v1's folder is inside the repo tree and is the deploy-swap target (H3) |
| Listen | `127.0.0.1:5000` | `127.0.0.1:5100` (*verified free*) | port clash |
| Public route | Caddy `dealing.connecttrader.app` + `http://37.148.206.228` -> `:5000` | **none** (RDP -> `127.0.0.1:5100`) | changing Caddy touches production; a v2 site is an owner decision |
| Feed consumer | key for `coverage-manager` (NSSM env) | **new** key for `coverage-manager-v2` (NSSM env only) | the bridge replaces a same-name connection (H1) |
| Feed resume state | explicit `LiveBridge__StatePath` in v1's NSSM env | `C:\CoverageManagerV2\state\livebridge-state.json` | the default path is per-Windows-account, not per-app; sharing it corrupts resume sequences |
| Logs | `publish\api\logs\` (relative to workdir) | `C:\CoverageManagerV2\app\logs\` | relative path follows the workdir |
| Store | Supabase `svhmhcqopkdgccnzgvzp` | local Postgres `coverage_v2` | -- ; v2 sets `Supabase__ReadOnly=true` as a backstop (no v2 code writes Supabase) |
| Coverage collector | receives v1's positions push | **reads** `GET /positions` (H2) | collector stays single-target, untouched |
| Centroid dropcopy | per v1 settings | `Centroid__Enabled=false` (default) | no second FIX session |
| Background jobs | v1 sweeps, sync to Supabase | self-check, retention pruner, snapshots, sync -- all to `coverage_v2` only | nothing v2 runs writes to v1 |

The React build is safe to share code-wise: a production bundle uses **same-origin** API and
WebSocket URLs (`web/src/config.ts`), so the copy in v2's `wwwroot` talks only to v2.

---

## 2. What the bridge must provide (for guard to take to the bridge)

1. **A second consumer key, named `coverage-manager-v2`**, on source **`BBcorp-Live`**, with the
   same four streams as `coverage-manager` (positions, deals, accounts, ticks) and the same
   console symbol selection. v2 identifies itself **only** by this bearer key -- there is no
   consumer-name header on the wire (*verified*, `FeedSocket.ConnectAsync`) -- so the name must
   be bound to the key on the bridge side.
2. **Confirmation that two consumers may be connected to the same source concurrently**, and
   that replace-on-connect applies **only to the same consumer name/key**, never across names.
3. **Address allowlist** for the key: this server, `37.148.206.228` (already allowed for v1).
   Confirm no one-connection-per-IP rule.
4. **Independent revocation:** the v2 key can be disabled without touching `coverage-manager`.
5. **Replay depth for a brand-new consumer** (null resume sequences): how far back the deals
   replay reaches on first connect. This decides the size of the gap the delta re-import must
   cover (§4).
6. **Health-page visibility** of both consumers, with lag/acks, so displacement or lag is
   observable during the run.
7. **Capacity headroom** for a second full fan-out (~20,000 deals/hour plus ticks for 26,000+
   accounts).
8. **Certificate:** confirm the feed certificate is unchanged (v2 can pin it with
   `LiveBridge__CertificateThumbprint` if the bridge provides the thumbprint).
9. Key handover **out of band** -- never in chat or tickets; it goes straight into v2's NSSM
   environment.

---

## 3. v2 service configuration (NSSM `AppEnvironmentExtra`)

Set **explicitly on the service**, so nothing inherited from any account can leak in:

```
ASPNETCORE_ENVIRONMENT=Production
Kestrel__Endpoints__Http__Url=http://127.0.0.1:5100
MT5__Provider=LiveBridge
LiveBridge__Url=wss://feed.connecttrader.app:5571/feed/BBcorp-Live
LiveBridge__ApiKey=<coverage-manager-v2 key from the bridge -- out of band>
LiveBridge__StatePath=C:\CoverageManagerV2\state\livebridge-state.json
Postgres__PasswordFile=C:\ProgramData\CoverageManagerV2\secrets\pg_app.txt
Supabase__ReadOnly=true
Supabase__Key=
Centroid__Enabled=false
Retention__Months=12
```

`Postgres__PasswordFile` is readable by LocalSystem (*verified*: the secrets folder ACL grants
SYSTEM full control). Postgres itself listens on `127.0.0.1:5432` only.

---

## 4. Runbook (for the owner/guard; not executed)

**Pre-flight** -- all must hold:
- H1, H2, H3 resolved; the v2 key is in hand.
- `127.0.0.1:5100` free; v1 `coverage-api` Running; bridge Health shows `coverage-manager` connected.
- `coverage_v2` migrations current (`db\apply-migrations.ps1 -Database coverage_v2 -User coverage_app` reports nothing to apply).

**Build** (from the `v2-local-postgres` checkout; never into `publish\`):
```powershell
dotnet publish src\CoverageManager.Api\CoverageManager.Api.csproj -c Release -o C:\CoverageManagerV2\app
cd web; npm ci; npm run build; cd ..
New-Item -ItemType Directory -Force C:\CoverageManagerV2\app\wwwroot, C:\CoverageManagerV2\state | Out-Null
Copy-Item -Recurse -Force web\dist\* C:\CoverageManagerV2\app\wwwroot
```

**Install** (elevated):
```powershell
nssm install coverage-api-v2 C:\CoverageManagerV2\app\CoverageManager.Api.exe
nssm set coverage-api-v2 AppDirectory C:\CoverageManagerV2\app
nssm set coverage-api-v2 ObjectName LocalSystem
nssm set coverage-api-v2 Start SERVICE_DEMAND_START          # manual start during the parallel run
nssm set coverage-api-v2 AppEnvironmentExtra <the §3 lines>
```

**Close the history gap:** the import cut is 2026-09-13 00:41 UTC. If the bridge's first-connect
deal replay (§2.5) starts later than that, run `db\import\delta-reimport.ps1` right after v2's
first connect. v1 stays live during a parallel run, so verify **will** show drift on
`deals` / `trading_accounts` (expected; see `db/README.md`). *Open question for the owner:* the
plan said "delta re-import with v1 writers frozen", which conflicts with v1 staying the live
dealer tool; freezing is only needed for a clean verify, not for a correct v2 store.

**Start and verify:**
```powershell
nssm start coverage-api-v2
```
Then, all read-only:
- `http://127.0.0.1:5100/api/exposure/status` -> `mt5Provider` = `LiveBridge`, connected.
- `http://127.0.0.1:5100/api/exposure/diagnostics` -> `liveBridge.state` = `live`, `source` = `BBcorp-Live`, `dealSync.pending` near 0.
- Bridge Health: **both** `coverage-manager` and `coverage-manager-v2` connected.
- v1 unaffected: its `/api/exposure/status` on `:5000` still connected, and `coverage-manager` shows no reconnect or replace.

**Abort** -- stop v2 immediately if `coverage-manager` disconnects, reconnects or is replaced, or
v1's status shows the feed down:
```powershell
nssm stop coverage-api-v2
```
Stopping v2 cannot affect v1: separate service, folder, port, key and state file.

**Tear down:** `nssm stop coverage-api-v2`, then `nssm remove coverage-api-v2 confirm`; ask the
bridge to revoke the `coverage-manager-v2` key. `coverage_v2` is kept.

---

## 5. Not blocking the parallel run, but worth knowing

- The 2 failing `ExposureEngineTests` are **stale tests** (see `V2_PLAN.md` §9.8), and the
  engine's `HedgeRatio` is direction-blind: a wrong-way hedge reads as covered. That affects v1
  and v2 identically, so it does not distort a v1-versus-v2 comparison.
- `CompareController`, `CoverageController` and `MarkupController` hardcode the collector URL
  (`localhost:8100` / `127.0.0.1:8100`) rather than reading `Coverage:CollectorUrl`. Correct on
  this box; wrong anywhere else.
