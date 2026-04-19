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

The MT5 Manager API DLLs are committed to the repo and arrive with the clone — **there is nothing to copy**. They live in `src\CoverageManager.Connector\Libs\`:

| File | How it's wired |
|---|---|
| `MetaQuotes.MT5CommonAPI64.dll` | Managed wrapper — `<Reference>` in `CoverageManager.Connector.csproj` |
| `MetaQuotes.MT5ManagerAPI64.dll` | Managed wrapper — `<Reference>` in `CoverageManager.Connector.csproj` |
| `MT5APIManager64.dll` | Native Manager API — `<None CopyToOutputDirectory="PreserveNewest">` so it lands next to the built assemblies |

`MT5_API_AVAILABLE` is defined unconditionally in the csproj, so `MT5ApiReal.cs` (guarded by `#if MT5_API_AVAILABLE`) always compiles. The `dotnet publish` step in §6 picks all three DLLs up automatically — no manual copy, no dev-machine hand-off.

> **Licensing — flag for review, not an assertion:** these DLLs are proprietary to MetaQuotes. Whether tracking them in this git repository is clean under your MetaQuotes Manager API license is a separate legal question worth checking before making the repo public or distributing it beyond the original licensee. Out of scope for this doc; calling it out so it doesn't get missed.

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
nssm set coverage-api AppEnvironmentExtra "ASPNETCORE_URLS=http://127.0.0.1:5000" "ASPNETCORE_ENVIRONMENT=Production"
nssm set coverage-api Start SERVICE_AUTO_START
nssm set coverage-api AppStdout "C:\apps\coverage-manager\logs\api.log"
nssm set coverage-api AppStderr "C:\apps\coverage-manager\logs\api.err.log"
nssm start coverage-api
```

### 8c. Caddy (TLS reverse proxy)
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

When you have a new commit on `live`:
```powershell
cd C:\apps\coverage-manager
git pull origin live

# Rebuild the C# backend
dotnet publish src\CoverageManager.Api\CoverageManager.Api.csproj -c Release -o C:\apps\coverage-manager\publish\api

# Rebuild the frontend
cd web
npm ci
npm run build
Copy-Item -Recurse -Force dist\* ..\publish\api\wwwroot
cd ..

# Python collector — only if collector/ changed
cd collector
.\venv\Scripts\activate
pip install -r requirements.txt
deactivate
cd ..

# Restart services
nssm restart coverage-api
nssm restart coverage-collector
```

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
