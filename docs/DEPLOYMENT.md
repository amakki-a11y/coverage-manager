# Production Deployment — Windows Server

End-to-end setup on a fresh Windows Server (2019 / 2022 / 2025). Result: the C# API, Python collector, MT5 Terminal, and the React dashboard all running as auto-start services behind a TLS reverse proxy.

> Windows-only by necessity — the MT5 Manager API ships as native Windows DLLs and the Python `MetaTrader5` library needs a GUI MT5 Terminal running.

---

## 0. Before you start — gather these

| # | Item | Where from |
|---|---|---|
| 1 | RDP access to the new server | Your VPS provider |
| 2 | Public domain name (optional but recommended) | e.g. `coverage.yourbroker.com` → A record to the VPS IP |
| 3 | Supabase URL + service-role key | Supabase dashboard → Project settings → API |
| 4 | MT5 Manager login + password + server | From your broker's backend team |
| 5 | MT5 Manager API DLLs | MetaQuotes (the 4 files that currently live in `src/CoverageManager.Connector/Libs/`) |
| 6 | LP (Coverage) account login + password + server | Your LP (e.g. fXGROW → 96900 / 194.164.176.137:443) |
| 7 | A Windows user account on the server dedicated to the app (NOT your RDP login) | Create via `lusrmgr.msc` |

---

## 1. Server spec

Minimum: 4 vCPU, 8 GB RAM, 50 GB SSD, Windows Server 2022.
Location: a data center geographically close to your MT5 servers (same region minimum; same DC ideal). For fXGROW-backed desks the BeeksFX / ForexVPS London / NY4 boxes are typical choices.

Enable: Remote Desktop, Windows Updates auto-apply off-hours.

---

## 2. One-time prerequisites (PowerShell as Administrator)

```powershell
# Chocolatey — package manager for everything below
Set-ExecutionPolicy Bypass -Scope Process -Force
iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))

# Runtimes
choco install -y dotnet-8.0-sdk
choco install -y python --version=3.11.9
choco install -y nodejs-lts
choco install -y git

# Service supervisor + reverse proxy
choco install -y nssm
choco install -y caddy

# MT5 Terminal (for the LP account)
choco install -y metatrader5

# Refresh PATH in this shell
refreshenv
```

Verify: `dotnet --version`, `python --version`, `node -v`, `git --version`, `nssm --help`, `caddy version`.

---

## 3. Clone the repo (`live` branch)

```powershell
cd C:\
New-Item -ItemType Directory -Path C:\apps -Force
cd C:\apps
git clone -b live https://github.com/amakki-a11y/coverage-manager.git
cd coverage-manager
```

> Production always runs `live`. `main` is staging, `dev` is integration. Never deploy from `main` or `dev`.

---

## 4. Configure Supabase + accounts

Edit `src\CoverageManager.Api\appsettings.json` — set:
```json
{
  "Supabase": {
    "Url": "https://svhmhcqopkdgccnzgvzp.supabase.co",
    "Key": "<service-role key from Supabase dashboard>"
  }
}
```

The MT5 Manager + LP account credentials go in the **Settings tab in the UI** (stored encrypted in the `account_settings` table). Do NOT hard-code them in `appsettings.json`.

---

## 5. MT5 Manager DLLs

The repo already tracks the three DLLs the C# backend needs under `src\CoverageManager.Connector\Libs\`:

- `MetaQuotes.MT5CommonAPI64.dll` — managed wrapper (common types)
- `MetaQuotes.MT5ManagerAPI64.dll` — managed wrapper (manager API)
- `MT5APIManager64.dll` — native library the wrappers P/Invoke into

**No copy needed** — they come with the clone. On `dotnet publish`, the native `MT5APIManager64.dll` is auto-emitted at the publish root (flattened via a `<Link>` entry in `CoverageManager.Connector.csproj`) so Windows P/Invoke can find it without a manual post-publish step.

---

## 6. Build the apps

```powershell
cd C:\apps\coverage-manager

# Backend — publish to a self-contained folder
dotnet publish src\CoverageManager.Api\CoverageManager.Api.csproj `
  -c Release `
  -o C:\apps\coverage-manager\publish\api

# Frontend — build the React bundle
cd web
npm ci
npm run build
# Output: web\dist  (we'll serve this from the C# backend's wwwroot)
Copy-Item -Recurse -Force dist\* ..\publish\api\wwwroot

# Python collector — install deps
cd ..\collector
python -m venv venv
.\venv\Scripts\activate
pip install -r requirements.txt
deactivate
```

---

## 7. MT5 Terminal — log in once, keep it logged in

1. Launch **MT5 Terminal** (installed by Chocolatey).
2. File → Login to Trade Account → enter the LP account (login 96900, password, server).
3. Tools → Options → Expert Advisors → tick **"Allow algorithmic trading"**.
4. File → Keep logged in (if your build supports it) — otherwise the auto-login step below handles it.

Leave the terminal open. The Python collector will attach to it.

---

## 8. Register services with NSSM

Three Windows Services — all auto-start on boot, restart on crash.

### 8a. Python collector
```powershell
cd C:\apps\coverage-manager
nssm install coverage-collector "C:\apps\coverage-manager\collector\venv\Scripts\python.exe"
nssm set coverage-collector AppParameters "-m uvicorn main:app --host 127.0.0.1 --port 8100"
nssm set coverage-collector AppDirectory "C:\apps\coverage-manager\collector"
nssm set coverage-collector Start SERVICE_AUTO_START
nssm set coverage-collector AppStdout "C:\apps\coverage-manager\logs\collector.log"
nssm set coverage-collector AppStderr "C:\apps\coverage-manager\logs\collector.err.log"
nssm start coverage-collector
```

### 8b. C# backend
```powershell
nssm install coverage-api "C:\apps\coverage-manager\publish\api\CoverageManager.Api.exe"
nssm set coverage-api AppDirectory "C:\apps\coverage-manager\publish\api"

# Binding note: `ASPNETCORE_URLS` is overridden by `Kestrel:Endpoints` in
# appsettings.json on modern .NET, so the env var no longer wins on its own.
# Use the `Kestrel__Endpoints__Http__Url` override (double-underscore = nested
# JSON key in env-var form) to bind reliably on any port. For a public IP-only
# deployment, bind port 80 directly and skip Caddy — see §8c for alternatives.
nssm set coverage-api AppEnvironmentExtra `
  "ASPNETCORE_ENVIRONMENT=Production" `
  "Kestrel__Endpoints__Http__Url=http://127.0.0.1:5000" `
  "Supabase__Key=<paste service-role JWT here>"

nssm set coverage-api Start SERVICE_AUTO_START
nssm set coverage-api AppStdout "C:\apps\coverage-manager\logs\api.log"
nssm set coverage-api AppStderr "C:\apps\coverage-manager\logs\api.err.log"
# Optional: rotate logs at 10 MB to stop them eating the C: drive.
nssm set coverage-api AppRotateFiles 1
nssm set coverage-api AppRotateBytes 10485760
nssm start coverage-api
```

**VPS provider firewalls:** most hosts (GoDaddy, Hetzner Cloud, OVH, AWS Security Groups, DigitalOcean) run a cloud firewall in front of Windows Firewall. Even with the Windows rule added in §9, non-standard ports (5000, 8100, …) often get silently dropped. **Prefer port 80 (HTTP) or 443 (HTTPS)** — they're open by default almost everywhere. If you bind the API directly to port 80, change `Kestrel__Endpoints__Http__Url` above to `http://0.0.0.0:80` and skip the Caddy reverse proxy.

### 8c. Caddy (TLS reverse proxy) — only when using HTTPS with a domain

**Skip this whole section if you're on IP-only / plain HTTP.** For an
IP-only deployment, bind the API directly to port 80 via the
`Kestrel__Endpoints__Http__Url` override in §8b and go straight to §9
(firewall). Caddy is only needed when you have a DNS name and want TLS.

Create `C:\apps\coverage-manager\Caddyfile`:
```
coverage.yourbroker.com {
    encode gzip
    reverse_proxy /ws*  http://127.0.0.1:5000
    reverse_proxy /api/* http://127.0.0.1:5000
    reverse_proxy *      http://127.0.0.1:5000
    log {
        output file C:/apps/coverage-manager/logs/caddy.access.log
    }
}
```
*(Replace `coverage.yourbroker.com` with your real DNS name. Caddy will auto-provision Let's Encrypt TLS as long as port 80/443 are open and the A record resolves to this server.)*

```powershell
nssm install coverage-caddy "C:\ProgramData\chocolatey\lib\caddy\tools\caddy.exe"
nssm set coverage-caddy AppParameters "run --config C:\apps\coverage-manager\Caddyfile"
nssm set coverage-caddy AppDirectory "C:\apps\coverage-manager"
nssm set coverage-caddy Start SERVICE_AUTO_START
nssm start coverage-caddy
```

**Internal-only deployment (no domain)?** Skip Caddy and just open port 5000 to your office/VPN subnet with a firewall rule (step 9).

---

## 9. Firewall

```powershell
# Allow HTTPS in from anywhere (Caddy handles it)
New-NetFirewallRule -DisplayName "Coverage Manager HTTPS" -Direction Inbound -Protocol TCP -LocalPort 443 -Action Allow

# Allow HTTP for Let's Encrypt ACME challenge
New-NetFirewallRule -DisplayName "Coverage Manager HTTP (ACME)" -Direction Inbound -Protocol TCP -LocalPort 80 -Action Allow

# Block direct access to 5000 and 8100 — everything must go through Caddy
New-NetFirewallRule -DisplayName "Block Coverage API direct" -Direction Inbound -Protocol TCP -LocalPort 5000 -Action Block
New-NetFirewallRule -DisplayName "Block Collector direct"  -Direction Inbound -Protocol TCP -LocalPort 8100 -Action Block
```

For a VPN-only deployment, reverse the last two rules and skip 80/443.

---

## 10. MT5 Terminal — survive RDP disconnects

MT5 Terminal needs an **interactive Windows session** to stay running. Without auto-login, RDP disconnect kills it.

1. `lusrmgr.msc` → create a user `mt5ops`, local admin, password never expires.
2. `netplwiz` → set `mt5ops` as the auto-login user on boot.
3. Log in as `mt5ops` once, place MT5 Terminal in `shell:startup` (`%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup`).
4. Reboot. Terminal comes up with the LP account logged in automatically.
5. **Disconnect RDP via `tscon` to keep the session alive**:
   ```powershell
   # From your admin RDP, find mt5ops's session:
   query session
   # Transfer the console to mt5ops so GUI apps don't suspend:
   tscon <session-id> /dest:console
   ```

Alternatively: run MT5 Terminal inside an AutoLogon + RDPWrap setup if your provider allows it.

---

## 11. Verify

On the server:
```powershell
Get-Service coverage-collector, coverage-api, coverage-caddy
# All three should show Status: Running

# Backend health
curl http://127.0.0.1:5000/api/exposure/status
# → { "connected": true, ... }

# Collector health
curl http://127.0.0.1:8100/health
# → { "status": "ok", "login": 96900, ... }
```

From a browser: `https://coverage.yourbroker.com` → dashboard loads, all 4 health dots green.

In the UI:
1. Settings → Connections → add MT5 Manager + Coverage account credentials.
2. Settings → Snapshots → "Run Now" on the Daily schedule, verify a row lands in `exposure_snapshots`.
3. Net P&L tab → Today preset → numbers appear.

---

## 12. Updating later

When `origin/live` has new commits, update via the **staging-then-swap** pattern.
The naive in-place `dotnet publish -o publish\api` fails with **MSB3027** because
the running NSSM service holds file locks on `CoverageManager.Api.dll`. The
[`_deploy\deploy.ps1`](../_deploy/deploy.ps1) script handles this by building
into `publish\api-staging`, then handing off the atomic swap to elevated PS.

### Standard update (backend + frontend, no collector change)

```powershell
cd C:\apps\coverage-manager
git pull origin live

# Builds the C# API into publish\api-staging and the frontend into
# publish\api-staging\wwwroot. Skips touching the running publish\api.
# Native MT5APIManager64.dll is verified at the publish root (P/Invoke
# requires it there, not just under Libs\).
.\_deploy\deploy.ps1
```

The script ends by printing the swap commands. Open an **elevated PowerShell**
(right-click → "Run as administrator") and paste them — NSSM service control
needs admin even though the build did not:

```powershell
nssm stop coverage-api
Rename-Item C:\apps\coverage-manager\publish\api "api-old-<timestamp>"
Rename-Item C:\apps\coverage-manager\publish\api-staging api
nssm start coverage-api
```

The `api-old-<timestamp>` directory is your rollback target — keep the most
recent one or two, prune the older ones once you're confident in the new build.

### Verify after restart

```powershell
Invoke-WebRequest http://localhost:5000/api/exposure/status -UseBasicParsing | Select-Object StatusCode
Get-Content C:\apps\coverage-manager\logs\coverage-manager-*.log -Tail 60
```

Expect HTTP 200 and a startup banner with no unhandled-exception lines.

### Rollback

If verification fails, restore the previous build (in elevated PS):

```powershell
nssm stop coverage-api
Rename-Item C:\apps\coverage-manager\publish\api "api-FAILED-<timestamp>"
Rename-Item C:\apps\coverage-manager\publish\api-old-<timestamp> api
nssm start coverage-api
```

### Collector-only updates

Only run these when `collector/` changed (Python deps, MT5 timeouts, etc.).
The MetaTrader5 library loads at import time, so a restart is mandatory:

```powershell
cd C:\apps\coverage-manager\collector
.\venv\Scripts\activate
pip install -r requirements.txt
deactivate
nssm restart coverage-collector   # elevated PS
```

Backend-only updates do NOT need to restart the collector.

---

## 13. Collector watchdog (recommended)

NSSM detects process crash but not a process that's up-but-unresponsive. A
rare MT5 hang can leave `coverage-collector` answering to NSSM (`sc query`
reports RUNNING) while /health times out indefinitely — the dealer sees the
Collector dot go red and nothing recovers.

The watchdog in [`_deploy/collector-watchdog.ps1`](_deploy/collector-watchdog.ps1)
probes `/health` on a 30-second cadence; three consecutive failures (~90 s)
triggers `nssm restart coverage-collector`.

Register as a Windows Scheduled Task running under the same local user
(`makkioo`) that runs the collector service:

```powershell
# Run once on the server. `-AtStartup` + a 30-second `RepetitionInterval`
# keeps the watchdog active every boot; `MultipleInstances Ignore` prevents
# overlapping runs if a restart takes longer than 30 seconds.
$action   = New-ScheduledTaskAction -Execute 'powershell.exe' `
              -Argument '-NoProfile -ExecutionPolicy Bypass -File "C:\apps\coverage-manager\_deploy\collector-watchdog.ps1"'
$trigger  = New-ScheduledTaskTrigger -AtStartup
$trigger.Repetition = (New-ScheduledTaskTrigger -Once -At (Get-Date) `
              -RepetitionInterval (New-TimeSpan -Seconds 30) `
              -RepetitionDuration ([TimeSpan]::MaxValue)).Repetition
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries `
              -DontStopIfGoingOnBatteries -StartWhenAvailable `
              -MultipleInstances Ignore -ExecutionTimeLimit (New-TimeSpan -Minutes 2)
$principal = New-ScheduledTaskPrincipal -UserId '.\makkioo' -LogonType ServiceAccount -RunLevel Highest

Register-ScheduledTask -TaskName 'CoverageCollectorWatchdog' `
  -Action $action -Trigger $trigger -Settings $settings -Principal $principal `
  -Description 'Restart coverage-collector when /health stops responding.'

# Kick it off immediately (otherwise first fire is at next reboot).
Start-ScheduledTask -TaskName 'CoverageCollectorWatchdog'
```

Health probe output appends to `C:\apps\coverage-manager\logs\watchdog.log`;
the failure counter persists in `watchdog.state` next to the log.

To unregister later: `Unregister-ScheduledTask -TaskName 'CoverageCollectorWatchdog' -Confirm:$false`.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| Collector dot red on first boot | MT5 Terminal not running / not auto-logged-in | Step 10 |
| `coverage-api` service won't start | Missing MetaQuotes DLLs | Step 5 |
| Dashboard blank over HTTPS | Caddy failed to provision TLS | Check DNS A record + ports 80/443 open |
| MT5 dot red intermittently | Manager credentials changed | Settings → Connections → update |
| `exposure_snapshots` never grows | Scheduler service not ticking | `nssm restart coverage-api`, check `logs/api.log` |

Full conventions + architecture: `CLAUDE.md` at the repo root.
