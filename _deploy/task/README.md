# Coverage Manager — scheduled-task deployment runbook

Runs the API, the Python LP collector and Caddy as **Windows Scheduled Tasks** under the app
user (no NSSM, no administrator needed for build / deploy / restart). Layout:

```
C:\CoverageManager\              git clone (branch: live)
  publish\api\                   live API build (+ wwwroot = React bundle)   <- build.ps1 -Swap
  publish\api-old-<ts>\          previous build(s), rollback target
  collector\venv\                Python 3.12 venv (MetaTrader5, fastapi, uvicorn, httpx)
  Caddyfile                      from _deploy\task\Caddyfile.template (hostname filled in)
  caddy\data, caddy\config       Caddy state + certificates
  logs\                          api-*.out/err.log, collector-*.log, caddy-*.log, watchdog.log, *-start.log
  run\                           pid files, watchdog state, maintenance.flag
```

Tasks (`Get-ScheduledTask -TaskName 'CoverageManager-*'`):

| Task | Action | Health probe |
|---|---|---|
| `CoverageManager-Api` | `start-api.ps1` → `publish\api\CoverageManager.Api.exe` on `http://127.0.0.1:5000` | `GET /api/exposure/status` |
| `CoverageManager-Watchdog` | `watchdog.ps1` every minute (2 probes, 30 s apart) | — |
| `CoverageManager-Collector` | `start-collector.ps1` → uvicorn on `127.0.0.1:8100` | `GET /health` |
| `CoverageManager-Caddy` | `start-caddy.ps1` → Caddy `:80/:443` → `127.0.0.1:5000` | task state only |

All service tasks: trigger *at startup*, no run-time limit, *restart on failure* every minute,
`RunLevel Limited`, stored password (so the user profile and USER-scope secrets are loaded).
The watchdog leaves a task alone for 5 minutes after it starts (cold start loads deals from
Supabase before Kestrel listens) and pauses entirely while `run\maintenance.flag` exists
(written by `build.ps1 -Swap`).

## Secrets

Secrets are **environment variables only**, USER scope, typed by the operator:

```powershell
.\_deploy\task\set-secrets.ps1                        # Supabase__Key (service-role key)
.\_deploy\task\set-secrets.ps1 -Names LiveBridge__ApiKey   # later, when the Live Bridge exists
```

MT5 Manager and LP credentials are not environment variables in this app: they are entered in
the dashboard (Settings → Connections) and stored in the `account_settings` table. The
collector pulls the LP login from the API at `/api/settings/accounts`.

## First install

As the app account (`makkioo`), normal PowerShell:

```powershell
cd C:\CoverageManager
.\_deploy\task\build.ps1 -Swap          # publish\api + wwwroot (no task yet -> plain rename)
.\_deploy\task\set-secrets.ps1          # Supabase__Key
```

From an **elevated** PowerShell (boot-triggered tasks cannot be registered by a standard
user; the script prompts for the app account's password and grants that account control of
the tasks afterwards):

```powershell
C:\CoverageManager\_deploy\task\register-tasks.ps1 -User .\makkioo -Collector -Caddy
```

Before `-Caddy`: copy `_deploy\task\Caddyfile.template` to `C:\CoverageManager\Caddyfile`,
replace `HOSTNAME_PLACEHOLDER` with the DNS name (A record → this server), and make sure
nothing else listens on 80/443. Validate with:

```powershell
caddy validate --config C:\CoverageManager\Caddyfile --adapter caddyfile
```

### Administrator steps (one-off)

```powershell
# inbound TLS + ACME (80 is usually already open; add if missing)
New-NetFirewallRule -DisplayName "Coverage Manager HTTPS (443)" -Direction Inbound -Protocol TCP -LocalPort 443 -Action Allow
New-NetFirewallRule -DisplayName "Coverage Manager HTTP (80)"   -Direction Inbound -Protocol TCP -LocalPort 80  -Action Allow
# if an older NSSM deployment is still running (holds :80 and :8100):
Stop-Service coverage-api;       Set-Service coverage-api       -StartupType Disabled
Stop-Service coverage-collector; Set-Service coverage-collector -StartupType Disabled
```

Also open 443 (and 80) in the VPS provider's firewall.

## Verify

```powershell
Get-ScheduledTask -TaskName 'CoverageManager-*' | Select TaskName, State
Invoke-WebRequest http://127.0.0.1:5000/api/exposure/status -UseBasicParsing | Select StatusCode, Content
Invoke-WebRequest http://127.0.0.1:8100/health -UseBasicParsing | Select StatusCode
Get-Content C:\CoverageManager\logs\watchdog.log -Tail 20
Get-Content C:\CoverageManager\publish\api\logs\coverage-manager-*.log -Tail 40
```

Browser: `https://<hostname>` → dashboard, health dots green. The API status JSON includes
`mt5Provider` (`Manager` or `LiveBridge`).

## Update

```powershell
cd C:\CoverageManager
git pull origin live
.\_deploy\task\build.ps1 -Swap      # build to staging, stop task, swap, start task
```

Rollback (printed by the script): stop the task, rename `publish\api` away, rename the latest
`publish\api-old-<ts>` back to `publish\api`, start the task.

Collector-only change: `uv pip install --python collector\venv\Scripts\python.exe -r collector\requirements.txt`
then `Stop-ScheduledTask CoverageManager-Collector; Start-ScheduledTask CoverageManager-Collector`.

## Switching the B-Book feed to the Live Bridge

The adapter (`src\CoverageManager.Connector\LiveBridgeApi.cs`) implements the consumer contract of
TheBridge `docs/product-spec.md` section 15, proposal 14. `appsettings.json` already points
`LiveBridge:Url` at `wss://feed.connecttrader.app:5571/feed/BBcorp-Live`; production stays on
`MT5:Provider = Manager` until the owner plans the switch.

1. The bridge console must list this server's address (37.148.206.228) as the consumer's allowed
   address and hand you the generated key. Store it as the task user:
   `.\_deploy\task\set-secrets.ps1 -Names LiveBridge__ApiKey`
2. Select the provider per machine without a rebuild: `set-secrets.ps1 -Names MT5__Provider` and
   enter `LiveBridge` (it is not a secret, but the same USER-scope mechanism reaches the task), or set
   `"Provider": "LiveBridge"` in `appsettings.json` and `build.ps1 -Swap`.
3. Restart the API task. The startup log shows `MT5 API provider: LiveBridge`, then
   `Live Bridge: hello ... mode snapshot` and `Live Bridge: snapshot complete ...`. With this provider the
   bring-up does not read the manager row from `account_settings` at all (the feed needs no MT5
   credentials), so it also comes up while Supabase is unreachable.
4. Verify: `/api/exposure/status.mt5Provider` = `LiveBridge`, `/api/exposure/diagnostics.liveBridge`
   shows `state: live`, `sourceConnected: true`, applied counts growing, and the resume sequences.
5. The resume sequences live in `%LOCALAPPDATA%\CoverageManager\livebridge-state.json` for the task
   user (`LiveBridge:StatePath` to move them); they survive a `build.ps1 -Swap`. Delete the file to
   force a fresh snapshot on the next start.
6. Back to the Manager API: remove the `MT5__Provider` variable (or set it to `Manager`) and restart.

`start-api.ps1` passes `LiveBridge__ApiKey` and `MT5__Provider` from the user environment to the API.

## Notes

- `.ps1` files here are ASCII-only on purpose: Windows PowerShell 5.1 reads BOM-less files as
  cp1252 and breaks on UTF-8 punctuation. Audit:
  `(Get-ChildItem _deploy\task\*.ps1 | % { [IO.File]::ReadAllBytes($_.FullName) | ? { $_ -gt 127 } }).Count` → 0.
- `npm ci` can fail on Windows with "lock file out of sync" (platform-specific `@emnapi/*`
  entries); `build.ps1` falls back to `npm install`. Do not commit the regenerated lockfile from
  the server.
- Task Scheduler stops the whole process tree on `Stop-ScheduledTask`; the scripts also keep a
  pid file and the watchdog kills the pid only if its executable lives under `C:\CoverageManager`.
