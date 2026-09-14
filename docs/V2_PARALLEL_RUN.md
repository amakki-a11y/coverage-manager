# v2 parallel-run readiness (beside live v1, same server)

**Status:** readiness list, 2026-09-13. H1, H2 and H3 are **resolved** (see §0). The
runbook (§4) has not been executed: v2 has **not** been started against the real feed;
`:5000` and v1 are untouched. Verified facts are marked
*verified*; everything else is a requirement or a recommendation.

**Principle:** during the parallel run v2 shares **nothing that writes or identifies** with v1
(port, binaries, feed key, resume state, service, deploy path, store) and uses shared services
only **read-only** (the coverage collector, Postgres is v2-only).

---

## 0. Must be resolved BEFORE anyone starts v2 (hazards found)

| # | Status (2026-09-13) |
|---|---|
| **H1** | **RESOLVED.** (1) The stale user-scope `LiveBridge__ApiKey` was removed from `makkioo`. Before removal it was confirmed to be an exact copy of v1's service key. Afterwards it was absent from `HKCU\Environment`, v1's NSSM key was intact and `coverage-api` was Running. Processes already running at removal time keep the value in memory until they restart; new logons and processes do not get it. (2) **Connect-to-feed switch** `LiveBridge:Enabled`, default **false** in code and in `appsettings.json` (`98db5fa`). While off, `LiveBridgeApi.Connect` refuses before dialing and `MT5ManagerConnection` idles with one warning; `status`/`diagnostics` show `feedDialEnabled`. Only v2's service environment turns it on (§3). |
| **H2** | **RESOLVED.** `CollectorPositionsPoller` (`f8bb7ca`) reads the collector's `GET /positions` every 1 s (`Coverage:PollEnabled=true`; collector untouched). An empty answer is applied only when `/health` is `ok` and fresh on two consecutive polls, because the collector also returns `[]` when MT5 fails. HTTP errors keep the last snapshot. `login` comes from `/health`. `openTime` is not in the payload (known gap: polled coverage rows have no open time). Counters: `diagnostics.coveragePoll`. The dead `PollFallbackEnabled` key is gone. |
| **H3** | **RESOLVED.** `_deploy/deploy.ps1` exits 2 before doing anything on a v2 checkout (`34dd531`). v2 has its own `_deploy/deploy-v2.ps1`: it stages into `C:\CoverageManagerV2\app-staging` and only prints the swap for `coverage-api-v2`. It refuses any folder inside a v1 `publish` folder and the service name `coverage-api`. `-CheckOnly` runs the guards only. |

Hazards as found:

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
| Feed consumer | key for `coverage-manager` (NSSM env) | **new** key for `coverage-manager-v2` (key file, §3) | the bridge replaces a same-name connection (H1) |
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
9. Key handover **out of band** -- never in chat or tickets; it goes straight into v2's key
   file (§3), never into an environment variable.

---

## 3. v2 service configuration (NSSM `AppEnvironmentExtra`)

Set **explicitly on the service**, so nothing inherited from any account can leak in:

```
ASPNETCORE_ENVIRONMENT=Production
Kestrel__Endpoints__Http__Url=http://127.0.0.1:5100
MT5__Provider=LiveBridge
LiveBridge__Enabled=true
LiveBridge__Url=wss://feed.connecttrader.app:5571/feed/BBcorp-Live
LiveBridge__ApiKeyFile=C:\ProgramData\CoverageManagerV2\secrets\livebridge_v2_key.txt
LiveBridge__StatePath=C:\CoverageManagerV2\state\livebridge-state.json
Postgres__PasswordFile=C:\ProgramData\CoverageManagerV2\secrets\pg_app.txt
Supabase__ReadOnly=true
Supabase__Key=
Centroid__Enabled=false
Retention__Months=12
Coverage__PollEnabled=true
```

`LiveBridge__Enabled=true` is the connect-to-feed switch (H1). It is **off** everywhere else,
so only this service, holding the `coverage-manager-v2` key, can dial.

**The feed key is never in the service environment.** It lives in
`C:\ProgramData\CoverageManagerV2\secrets\livebridge_v2_key.txt` (`LiveBridge:ApiKeyFile`, also the
`appsettings.json` default): one line, the key only, ASCII, ACL = `SYSTEM` + `Administrators`
plus **Read for the account the service runs as** (inheritance removed; nobody else). The adapter
reads it at every connect, never logs it, and refuses to start a session if `LiveBridge__ApiKey`
is ALSO set, so an inherited key can never win.

Write it with `_deploy\set-feed-key-v2.ps1` from an **elevated** Windows PowerShell, **after**
installing the service (§4). The script reads the service's actual account (`Win32_Service.StartName`)
and grants that account Read -- nothing broader (Everyone / Users / Authenticated Users /
Interactive / Guests are refused). The key is typed at a masked prompt: not on the command line,
not in PSReadLine history, not on the clipboard, never printed.

```powershell
.\_deploy\set-feed-key-v2.ps1            # writes the key; ACL = SYSTEM F, Administrators F, <service account> R
.\_deploy\set-feed-key-v2.ps1 -AclOnly   # re-apply the ACL after changing the service account; key untouched
```

For a LocalSystem service the ACL is just `NT AUTHORITY\SYSTEM:(F)` + `BUILTIN\Administrators:(F)`;
for any other account a third line `<account>:(R)` appears. Re-running without `-AclOnly` replaces
the key (rotation; picked up at the next fresh session).

**Prove the service can read it before arming the switch.** With `LiveBridge__Enabled` still unset
(false), start the service and check `/api/exposure/diagnostics.feedKey`: `readable` = true and
`runningAs` = the service account (`NT AUTHORITY\SYSTEM` for LocalSystem). This is a read-only
preflight -- nothing dials. The startup log has the same line (`Live Bridge key: readable ...`).
Only then add `LiveBridge__Enabled=true`.

`Postgres__PasswordFile` is readable by LocalSystem (*verified*: the secrets folder ACL grants
SYSTEM full control). Postgres itself listens on `127.0.0.1:5432` only.

---

## 4. Runbook (for the owner/guard; not executed)

**Pre-flight** -- all must hold:
- H1, H2, H3 resolved; the v2 key is in hand.
- `127.0.0.1:5100` free; v1 `coverage-api` Running; bridge Health shows `coverage-manager` connected.
- `coverage_v2` migrations current (`db\apply-migrations.ps1 -Database coverage_v2 -User coverage_app` reports nothing to apply).

**Build** (from the `v2-local-postgres` checkout; never `_deploy\deploy.ps1`, which refuses a v2 checkout):
```powershell
.\_deploy\deploy-v2.ps1 -CheckOnly      # guards only
.\_deploy\deploy-v2.ps1                 # stages C:\CoverageManagerV2\app-staging, prints the swap
New-Item -ItemType Directory -Force C:\CoverageManagerV2\state | Out-Null
# first install only: Rename-Item C:\CoverageManagerV2\app-staging C:\CoverageManagerV2\app
```

**Install** (elevated):
```powershell
nssm install coverage-api-v2 C:\CoverageManagerV2\app\CoverageManager.Api.exe
nssm set coverage-api-v2 AppDirectory C:\CoverageManagerV2\app
nssm set coverage-api-v2 ObjectName LocalSystem
nssm set coverage-api-v2 Start SERVICE_DEMAND_START          # manual start during the parallel run
nssm set coverage-api-v2 AppEnvironmentExtra <the §3 lines WITHOUT LiveBridge__Enabled=true>
```

**Key + read proof (switch still off)** (elevated):
```powershell
.\_deploy\set-feed-key-v2.ps1            # reads the account from the installed service; masked prompt
nssm start coverage-api-v2
# diagnostics.feedKey.readable = true, runningAs = NT AUTHORITY\SYSTEM; liveBridge not connected; nothing dialed
nssm stop coverage-api-v2
nssm set coverage-api-v2 AppEnvironmentExtra <the full §3 lines, now WITH LiveBridge__Enabled=true>
```

**Close the history gap:** the import cut is 2026-09-13 00:41 UTC. Run
`db\import\delta-reimport.ps1` right after v2's first connect, and again later if the bridge's
first-connect deal replay (§2.5) starts after the cut. **v1 is NOT frozen for this** (owner,
2026-09-13; `V2_PLAN.md` §9a). The import is idempotent and v2's live feed keeps the store
correct, so `verify` **will** show drift on `deals` / `trading_accounts` / snapshots while v1
keeps writing. That drift is expected, not a failure (see `db/README.md`). Freezing v1's writers
applies **only at cutover**, where the final delta import must verify with zero drift.

**Start and verify:**
```powershell
nssm start coverage-api-v2
```
Then, all read-only:
- `http://127.0.0.1:5100/api/exposure/status` -> `mt5Provider` = `LiveBridge`, connected.
- `http://127.0.0.1:5100/api/exposure/diagnostics` -> `liveBridge.state` = `live`, `source` = `BBcorp-Live`, `dealSync.pending` near 0, `coveragePoll.stale` = false with `lastAppliedCount` equal to the collector's open LP positions.
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

- **Hedge % differs from v1 by design** (`V2_PLAN.md` §9.8, resolved). v2 counts a wrong-way
  hedge as 0% and flags it WRONG-WAY; v1 still shows `|coverage/client|`. When diffing hedge %
  between v1 and v2, expect differences exactly on symbols whose coverage net is opposite the
  client net. Net volume / To Cover are computed identically.
- `CompareController`, `CoverageController` and `MarkupController` hardcode the collector URL
  (`localhost:8100` / `127.0.0.1:8100`) rather than reading `Coverage:CollectorUrl`. Correct on
  this box; wrong anywhere else.
