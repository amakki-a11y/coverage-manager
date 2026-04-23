"""
Coverage Manager — Python Collector
====================================

The coverage (LP) side of the Coverage Manager pipeline. The C# Manager API
we use for the B-Book side is bound to a single MT5 Manager login and cannot
see accounts that live on the LP's MT5 server, so we run this small Python
service alongside an MT5 Terminal that IS logged into the LP account.

What this process does
----------------------
* Polls ``mt5.positions_get()`` every ``POLL_INTERVAL`` (default 100 ms) and
  POSTs the open-position snapshot to ``/api/coverage/positions`` on the C#
  backend. The backend treats each POST as authoritative — this is a "pull
  me into sync" message, not a delta.
* Exposes FastAPI endpoints the backend/UI call directly:
    * ``GET /health``       — liveness + staleness for the top-bar health dot.
    * ``GET /account``      — LP balance/credit/equity for the Equity P&L tab.
    * ``GET /positions``    — on-demand pull (rarely used; positions are pushed).
    * ``GET /deals``        — aggregated closed-deal P&L per symbol for a window.
    * ``GET /deals/raw``    — individual deals used by Markup + Bridge matching.
* Reconnects MT5 on drop with exponential backoff capped at 10 s — dealers
  routinely close/reopen the MT5 Terminal during the day.

Startup
-------
* Tries to attach to an already-running MT5 Terminal first (simplest path).
* Falls back to pulling creds from ``/api/settings/accounts`` and calling
  ``mt5.initialize(login=..., server=..., password=...)``.
* If neither works, the process stays up and logs a warning so the backend
  health dot shows "disconnected" rather than the collector crashing.

Environment variables
---------------------
* ``BACKEND_URL``             — C# API base URL (default ``http://localhost:5000``).
* ``COLLECTOR_POLL_INTERVAL`` — position poll interval in seconds (default 0.1).

Run locally: ``uvicorn main:app --host 0.0.0.0 --port 8100``.
"""
import os
from fastapi import FastAPI, Query
from fastapi.middleware.cors import CORSMiddleware
import MetaTrader5 as mt5
import httpx
import asyncio
from contextlib import asynccontextmanager
from datetime import datetime, timezone, timedelta

# Config — env-sourced. Keep local dev default so `uvicorn main:app` still works
# without setup, but production / container deploys should export BACKEND_URL.
BACKEND_URL = os.getenv("BACKEND_URL", "http://localhost:5000")
POLL_INTERVAL = float(os.getenv("COLLECTOR_POLL_INTERVAL", "0.1"))  # seconds

# Default per-call timeout. MT5's Python wrapper is a synchronous C binding —
# every call blocks the thread it runs on. Wrapping in asyncio.to_thread moves
# it off the event loop; wait_for adds a hard ceiling so a stuck MT5 server
# can't freeze uvicorn's accept() loop. See `mt5_call` below.
MT5_CALL_TIMEOUT = float(os.getenv("COLLECTOR_MT5_TIMEOUT", "2.0"))
# Longer ceiling for history queries (history_deals_get over large windows
# legitimately takes a few seconds on MT5's side).
MT5_HISTORY_TIMEOUT = float(os.getenv("COLLECTOR_MT5_HISTORY_TIMEOUT", "30.0"))


async def mt5_call(fn, *args, timeout: float = MT5_CALL_TIMEOUT, default=None, **kwargs):
    """Run an MT5 library call off the asyncio event loop, with a hard timeout.

    The MetaTrader5 Python package is a synchronous C wrapper — every call
    (``positions_get``, ``history_deals_get``, ``account_info``, ``initialize``,
    ``shutdown``, ``last_error``, ``terminal_info``, …) blocks the thread it
    runs on. Before this wrapper, every one of those calls ran directly on the
    asyncio event loop; when the MT5 server hiccupped, the call could block
    for seconds-to-minutes, and uvicorn couldn't ``accept()`` new connections
    (the socket shows "Listen" but no response is returned).

    This helper moves each call to a thread via ``asyncio.to_thread`` and
    applies ``wait_for`` so a stuck call returns ``default`` after ``timeout``
    seconds instead of freezing the event loop. Callers should handle the
    ``default`` value (typically ``None``/``False``) as "unavailable" and
    continue gracefully.

    Parameters
    ----------
    fn       : callable  — the ``mt5.<name>`` function reference.
    *args    : forwarded to ``fn``.
    timeout  : seconds; raises ``asyncio.TimeoutError`` internally after this,
               caught and converted to ``default``.
    default  : returned on timeout or if ``fn`` raises.
    **kwargs : forwarded to ``fn``.

    Returns the function's return value, or ``default`` on timeout / error.
    """
    try:
        return await asyncio.wait_for(
            asyncio.to_thread(fn, *args, **kwargs),
            timeout=timeout,
        )
    except asyncio.TimeoutError:
        print(f"[collector] mt5.{getattr(fn, '__name__', '<?>')} timed out after {timeout:.1f}s")
        return default
    except Exception as e:
        print(f"[collector] mt5.{getattr(fn, '__name__', '<?>')} raised: {e}")
        return default


async def fetch_coverage_account():
    """Fetch the active coverage account from the C# backend."""
    async with httpx.AsyncClient(timeout=5.0) as client:
        res = await client.get(f"{BACKEND_URL}/api/settings/accounts")
        res.raise_for_status()
        accounts = res.json()
        for acc in accounts:
            if acc.get("account_type") == "coverage" and acc.get("is_active"):
                return acc
    return None


async def init_mt5(account):
    """Initialize the MT5 terminal with coverage account credentials.

    Async so the blocking ``initialize`` call is off-loaded to a thread
    via ``mt5_call``. A stalled MT5 server won't freeze lifespan.
    """
    login = int(account["login"])
    server = account["server"]
    password = account["password"]

    print(f"Connecting to MT5: login={login}, server={server}")

    ok = await mt5_call(mt5.initialize, default=False,
                        login=login, server=server, password=password,
                        timeout=MT5_HISTORY_TIMEOUT)
    if not ok:
        error = await mt5_call(mt5.last_error, default=("?", "unavailable"))
        raise RuntimeError(f"MT5 init failed: {error}")

    info = await mt5_call(mt5.terminal_info)
    acc_info = await mt5_call(mt5.account_info)
    if info and acc_info:
        print(f"MT5 connected: {info.name}")
        print(f"Account: {acc_info.login} on {acc_info.server}, balance={acc_info.balance}")


# Tracks the last time MT5 returned usable data. /health exposes this so the
# backend can detect a stalled collector even when the Python process is alive.
_last_position_update_utc: datetime | None = None

# Cached account login so a brief acc==None mid-session doesn't reset it to 0.
_cached_login: int = 0


async def _reconnect_mt5() -> bool:
    """Attempt to re-init MT5 after a drop. Returns True on success.

    When the user closes the MT5 terminal and reopens it, the Python library's
    internal terminal pointer goes stale. We go straight to credential-based
    re-init so a fresh terminal launch is picked up on the first retry tick.
    All MT5 calls run through ``mt5_call`` so a hung MT5 server can't block
    the asyncio event loop during a reconnect storm.
    """
    # shutdown() is best-effort and timeout-guarded to prevent a stuck MT5
    # instance from holding up reconnection.
    await mt5_call(mt5.shutdown, default=None, timeout=MT5_CALL_TIMEOUT)
    try:
        account = await fetch_coverage_account()
        if account is None:
            # Backend is down / creds not configured — fall back to reusing
            # whatever terminal session Python still remembers. Last-ditch.
            if await mt5_call(mt5.initialize, default=False, timeout=MT5_HISTORY_TIMEOUT):
                return (await mt5_call(mt5.account_info)) is not None
            return False

        login = int(account["login"])
        server = account["server"]
        password = account["password"]

        ok = await mt5_call(
            mt5.initialize, default=False, timeout=MT5_HISTORY_TIMEOUT,
            login=login, server=server, password=password,
        )
        if not ok:
            err = await mt5_call(mt5.last_error, default=("?", "unavailable"))
            print(f"[collector] mt5.initialize failed: {err}")
            return False

        acc = await mt5_call(mt5.account_info)
        if acc is None:
            print("[collector] initialize ok but account_info is None")
            return False
        print(f"[collector] MT5 re-connected: {acc.login} @ {acc.server} bal={acc.balance}")
        return True
    except Exception as e:
        print(f"[collector] MT5 reconnect failed: {e}")
        return False


async def position_loop():
    """Read MT5 positions every POLL_INTERVAL seconds and POST to backend.

    On MT5 error the loop now distinguishes MT5 failures from HTTP failures,
    reconnects MT5 with exponential backoff (1s → 60s cap), and publishes
    `last_position_update_utc` on /health so the C# side can detect stalls.
    """
    global _last_position_update_utc, _cached_login
    url = f"{BACKEND_URL}/api/coverage/positions"
    mt5_backoff_s = 1.0
    # Backoff caps at 10s — the dealer will often close/reopen MT5 Terminal
    # during the day and expects the coverage panel to come back quickly.
    # A 60s ceiling added an unnecessary dead zone after a restart.
    MT5_BACKOFF_MAX = 10.0

    async with httpx.AsyncClient(timeout=2.0) as client:
        while True:
            try:
                # mt5_call returns None both on a real session drop AND on a
                # timeout. Either way we treat the tick as failed and trigger
                # a reconnect — the timeout path is the critical one; without
                # the thread off-load, an unresponsive MT5 server would hang
                # this loop indefinitely and uvicorn would stop serving /health.
                positions = await mt5_call(mt5.positions_get)
                if positions is None:
                    err = await mt5_call(mt5.last_error, default=("?", "unavailable"))
                    print(f"[collector] positions_get returned None; MT5 error={err}. Reconnecting in {mt5_backoff_s:.1f}s…")
                    await asyncio.sleep(mt5_backoff_s)
                    mt5_backoff_s = min(mt5_backoff_s * 2, MT5_BACKOFF_MAX)
                    if await _reconnect_mt5():
                        print("[collector] MT5 reconnected")
                        mt5_backoff_s = 1.0
                    continue

                # Healthy tick — reset backoff + record freshness.
                mt5_backoff_s = 1.0
                _last_position_update_utc = datetime.now(tz=timezone.utc)

                acc = await mt5_call(mt5.account_info)
                if acc is not None:
                    _cached_login = int(acc.login)
                login = _cached_login

                if positions:
                    data = [{
                        "symbol": p.symbol,
                        "direction": "BUY" if p.type == 0 else "SELL",
                        "volume": p.volume,
                        "openPrice": p.price_open,
                        "currentPrice": p.price_current,
                        "profit": p.profit,
                        "swap": p.swap,
                        "ticket": p.ticket,
                        "login": login,
                        # p.time is seconds since Unix epoch (UTC) per MT5 spec.
                        "openTime": datetime.fromtimestamp(p.time, tz=timezone.utc).isoformat(),
                    } for p in positions]
                    try:
                        await client.post(url, json=data)
                    except httpx.HTTPError as http_err:
                        # Backend blip — DON'T reconnect MT5, just skip this tick.
                        print(f"[collector] backend POST failed: {http_err}")
                else:
                    try:
                        await client.post(url, json=[])
                    except httpx.HTTPError as http_err:
                        print(f"[collector] backend POST failed (empty): {http_err}")
            except Exception as e:
                # Anything else (unexpected MT5 behavior, e.g. connection drop mid-call).
                print(f"[collector] position_loop unexpected error: {e}; reconnecting in {mt5_backoff_s:.1f}s")
                await asyncio.sleep(mt5_backoff_s)
                mt5_backoff_s = min(mt5_backoff_s * 2, MT5_BACKOFF_MAX)
                await _reconnect_mt5()

            await asyncio.sleep(POLL_INTERVAL)


@asynccontextmanager
async def lifespan(app: FastAPI):
    """FastAPI lifespan hook — startup + shutdown of the MT5 attachment and
    position-polling loop.

    Fails OPEN on every upstream failure mode: if MT5 won't attach, the
    backend is unreachable, or credentials are missing, we log a warning
    and yield without starting the position loop. The service stays up so
    the backend's health proxy still returns a real response (`status: "disconnected"`)
    instead of the process going into an NSSM restart loop.

    Once the underlying issue is fixed (MT5 Terminal restarted, backend
    reachable, creds configured), the next `/health` hit or a service
    restart picks up the happy path.
    """
    task = None
    try:
        # Try to attach to an already-running MT5 terminal first (happy path
        # on the production box where makkioo's auto-login launches MT5 on boot).
        # All MT5 calls go through mt5_call so a stalled terminal can't freeze
        # startup — a 30s timeout wins over an indefinite lifespan block.
        if await mt5_call(mt5.initialize, default=False, timeout=MT5_HISTORY_TIMEOUT):
            acc = await mt5_call(mt5.account_info)
            info = await mt5_call(mt5.terminal_info)
            if acc and info:
                print(f"MT5 attached to running terminal: {info.name}")
                print(f"Account: {acc.login} on {acc.server}, balance={acc.balance}")
            else:
                print("[collector] mt5.initialize returned ok but account/terminal info missing")
        else:
            # Fall back to credentials from the C# backend. Any failure here
            # (backend down, httpx timeout, empty creds, MT5 init reject) is
            # non-fatal — we just skip starting the poll loop.
            try:
                account = await fetch_coverage_account()
            except Exception as e:
                print(f"[collector] WARNING: could not reach backend for coverage credentials: {e}")
                print("[collector] Collector will stay up; /health will report 'disconnected'.")
                yield
                return

            if account is None:
                print("[collector] WARNING: no active coverage account configured.")
                print("[collector] Add a coverage account via the Settings tab and restart the collector.")
                yield
                return

            try:
                await init_mt5(account)
            except Exception as e:
                print(f"[collector] WARNING: MT5 init with fetched creds failed: {e}")
                print("[collector] Collector will stay up; /health will report 'disconnected'.")
                yield
                return

        # Start position polling loop only if we have a working MT5 attachment.
        task = asyncio.create_task(position_loop())
        yield
    finally:
        if task is not None:
            task.cancel()
        # Timeout-guarded shutdown so a stuck MT5 can't block FastAPI teardown.
        await mt5_call(mt5.shutdown, default=None, timeout=MT5_CALL_TIMEOUT)


app = FastAPI(title="Coverage Manager Collector", lifespan=lifespan)
app.add_middleware(CORSMiddleware, allow_origins=["*"], allow_methods=["*"], allow_headers=["*"])


@app.get("/health")
async def health():
    """Liveness probe for the top-bar 'Collector' dot.

    All MT5 calls here are timeout-guarded so a hung MT5 server can't stall
    /health. If ``terminal_info`` or ``account_info`` doesn't return in
    ``MT5_CALL_TIMEOUT`` seconds we treat MT5 as disconnected for the probe
    and let the watchdog (if configured) decide whether to restart.

    Returns
    -------
    dict
        ``status``                    — ``ok`` (healthy) / ``stale`` (MT5 up but positions
                                        haven't refreshed in 10 s) / ``disconnected`` (MT5 down).
        ``terminal``                  — MT5 terminal name or ``None``.
        ``login`` / ``server``        — cached LP login + server.
        ``last_position_update_utc``  — ISO timestamp of the last successful positions poll.
        ``stale``                     — bool; true when the position poll has gone quiet.
    """
    info = await mt5_call(mt5.terminal_info)
    acc = await mt5_call(mt5.account_info)
    now = datetime.now(tz=timezone.utc)
    last = _last_position_update_utc
    # Consider the collector stale if we haven't successfully read positions in 10s.
    stale = last is None or (now - last).total_seconds() > 10
    status = "stale" if (info and stale) else ("ok" if info else "disconnected")
    return {
        "status": status,
        "terminal": info.name if info else None,
        "login": acc.login if acc else _cached_login if _cached_login else None,
        "server": acc.server if acc else None,
        "last_position_update_utc": last.isoformat() if last else None,
        "stale": stale,
    }


@app.get("/account")
async def get_account():
    """Return the currently-connected LP account's balance / credit / equity.

    Used by the C# API's coverage-sync loop to populate `trading_accounts` for
    the LP login (normally 96900). Without this endpoint the Equity P&L tab
    has no way to show the Coverage row — MT5 Manager API (our B-Book side)
    can't see LP accounts since they live on a different MT5 server.
    """
    acc = await mt5_call(mt5.account_info)
    if acc is None:
        return {"error": "MT5 not connected"}, 503
    return {
        "login": acc.login,
        "name": acc.name,
        "server": acc.server,
        "balance": float(acc.balance),
        "credit": float(acc.credit),
        "equity": float(acc.equity),
        "margin": float(acc.margin),
        "free_margin": float(acc.margin_free),
        "leverage": int(acc.leverage),
        "currency": acc.currency,
    }


@app.get("/positions")
async def get_positions():
    """Return open coverage positions on demand (pull instead of push).

    Not used in steady state — ``position_loop`` pushes to the backend every
    100 ms. Kept for ad-hoc diagnostics and as a fallback if the push path
    is ever disabled. Timeout-guarded so a stuck MT5 can't hang this endpoint.
    """
    positions = await mt5_call(mt5.positions_get)
    if not positions:
        return []
    return [{
        "symbol": p.symbol,
        "direction": "BUY" if p.type == 0 else "SELL",
        "volume": p.volume,
        "openPrice": p.price_open,
        "currentPrice": p.price_current,
        "profit": p.profit,
        "swap": p.swap,
        "ticket": p.ticket
    } for p in positions]


@app.get("/deals")
async def get_deals(
    from_date: str = Query(..., alias="from", description="Start date YYYY-MM-DD"),
    to_date: str = Query(..., alias="to", description="End date YYYY-MM-DD"),
):
    """Aggregated closed-deal P&L per coverage symbol for a UTC date range.

    Parameters
    ----------
    from_date, to_date : str (``YYYY-MM-DD``, via query aliases ``from`` / ``to``)
        Inclusive date range. Dates are interpreted as UTC midnight; ``to`` is
        exclusive internally (we add 1 day) so "today..today" = full 24 h.

    Returns
    -------
    dict
        ``totalDeals`` — count of OUT (closing) deals.
        ``symbols``    — per-symbol aggregate with ``buyVolume``, ``sellVolume``,
                         ``totalVolume`` (includes IN+OUT to match MT5 totals),
                         ``totalProfit`` / ``totalCommission`` / ``totalSwap`` /
                         ``totalFee`` (OUT-only for P&L math), and ``netPnL``.
        ``debug``      — raw sums across all deals (balance/credit excluded)
                         for spot-checking against the MT5 terminal History tab.

    Volume vs P&L split: MT5 emits two deals per close (IN + OUT). Volume
    summed naively would double-count, so per-CLAUDE.md conventions volume
    uses both sides and P&L only the OUT side.
    """
    dt_from = datetime.strptime(from_date, "%Y-%m-%d").replace(tzinfo=timezone.utc)
    # MT5 history_deals_get 'to' is exclusive, so add 1 day to include the end date
    dt_to = datetime.strptime(to_date, "%Y-%m-%d").replace(tzinfo=timezone.utc) + timedelta(days=1)

    # Long timeout — large windows (weeks of deals) legitimately take several
    # seconds on MT5's side. Still bounded so a truly stuck query doesn't
    # block the handler forever.
    deals = await mt5_call(mt5.history_deals_get, dt_from, dt_to, timeout=MT5_HISTORY_TIMEOUT)
    if deals is None:
        return {"deals": [], "symbols": []}

    # All trade deals (exclude balance/credit which have type >= 2 / no symbol)
    trade_deals = [d for d in deals if d.symbol and d.type < 2]

    # Build per-symbol summary: volumes from ALL deals (IN+OUT) to match MT5 totals,
    # P&L only from OUT deals (entry 1, 2, 3)
    symbol_map: dict = {}
    for d in trade_deals:
        s = symbol_map.setdefault(d.symbol, {
            "symbol": d.symbol,
            "dealCount": 0,
            "totalProfit": 0.0,
            "totalCommission": 0.0,
            "totalSwap": 0.0,
            "totalFee": 0.0,
            "totalVolume": 0.0,
            "buyVolume": 0.0,
            "sellVolume": 0.0,
        })
        s["dealCount"] += 1
        s["totalVolume"] += d.volume
        s["totalCommission"] += d.commission
        s["totalFee"] += d.fee
        if d.type == 0:  # BUY
            s["buyVolume"] += d.volume
        else:  # SELL
            s["sellVolume"] += d.volume
        # Profit and swap only from OUT deals
        if d.entry in (1, 2, 3):
            s["totalProfit"] += d.profit
            s["totalSwap"] += d.swap

    symbols = list(symbol_map.values())
    for s in symbols:
        s["netPnL"] = s["totalProfit"] + s["totalCommission"] + s["totalSwap"] + s["totalFee"]

    # Keep separate count of OUT-only deals for backwards compat
    closed = [d for d in trade_deals if d.entry in (1, 2, 3)]

    # Also compute raw totals for all deals (for debugging/comparison with MT5 History tab)
    all_with_symbol = [d for d in deals if d.symbol]
    all_profit = sum(d.profit for d in all_with_symbol)
    all_commission = sum(d.commission for d in all_with_symbol)
    all_swap = sum(d.swap for d in all_with_symbol)
    all_fee = sum(d.fee for d in all_with_symbol)

    return {
        "totalDeals": len(closed),
        "symbols": sorted(symbols, key=lambda x: abs(x["netPnL"]), reverse=True),
        "debug": {
            "allDealsCount": len(deals),
            "closedDealsCount": len(closed),
            "allProfit": round(all_profit, 2),
            "allCommission": round(all_commission, 2),
            "allSwap": round(all_swap, 2),
            "allFee": round(all_fee, 2),
            "allNet": round(all_profit + all_commission + all_swap + all_fee, 2),
            "closedProfit": round(sum(d.profit for d in closed), 2),
            "closedNet": round(sum(s["netPnL"] for s in symbols), 2),
        }
    }


# MT5 DealType → lowercase string. Names mirror MT5's official spec so the C#
# side can cross-check balance/credit movements symmetrically with bbook.
_DEAL_TYPE_NAMES = {
    0: "buy",
    1: "sell",
    2: "balance",
    3: "credit",
    4: "charge",
    5: "correction",
    6: "bonus",
    7: "commission",
    8: "commission_daily",
    9: "commission_monthly",
    10: "commission_agent_daily",
    11: "commission_agent_monthly",
    12: "interest",
    13: "buy_canceled",
    14: "sell_canceled",
    15: "dividend",
    16: "dividend_franked",
    17: "tax",
}


@app.get("/deals/raw")
async def get_deals_raw(
    from_date: str = Query(..., alias="from", description="Start date YYYY-MM-DD"),
    to_date: str = Query(..., alias="to", description="End date YYYY-MM-DD"),
):
    """Return individual (un-aggregated) coverage deals for a UTC date range.

    Consumed by:
      * ``/api/markup/match`` — time-window matching (±500 ms) between client
        OUT deals and coverage OUT deals to build the Markup tab.
      * The Compare tab's coverage trade stream.
      * The Bridge tab's coverage-side enrichment path.
      * ``/api/equity-pnl`` — cross-checks coverage-side balance/credit
        movements symmetrically with bbook (needs balance/credit types).

    Each deal carries the MT5 ticket, order id, magic, ``externalId`` (used by
    the Bridge to resolve Centroid's ``maker_order_id`` back to a real MT5
    deal number), volume/price/profit/commission/fee/swap, and ``timeMsc``
    (millisecond-precision Unix timestamp).

    Non-trade types (balance, credit, charge, correction, bonus, commission,
    interest, dividend, tax, …) are now INCLUDED in the response with a
    lower-cased ``type`` tag — previously only ``buy``/``sell`` trade deals
    were emitted. Consumers that only want trade deals should filter on
    ``type in {"buy","sell"}`` or ``entry > 0``.
    """
    try:
        dt_from = datetime.strptime(from_date, "%Y-%m-%d").replace(tzinfo=timezone.utc)
        dt_to = datetime.strptime(to_date, "%Y-%m-%d").replace(tzinfo=timezone.utc) + timedelta(days=1)
    except ValueError as e:
        return {"error": f"Invalid date format, expected YYYY-MM-DD: {e}"}, 400

    try:
        deals = await mt5_call(mt5.history_deals_get, dt_from, dt_to, timeout=MT5_HISTORY_TIMEOUT)
        if deals is None:
            return {"deals": []}

        result = []
        for d in deals:
            type_name = _DEAL_TYPE_NAMES.get(int(d.type), f"type_{int(d.type)}")
            result.append({
                "ticket": d.ticket,
                "order": d.order,
                "magic": getattr(d, "magic", 0),
                "externalId": getattr(d, "external_id", "") or "",
                "time": datetime.fromtimestamp(d.time_msc / 1000, tz=timezone.utc).isoformat(),
                "timeMsc": d.time_msc,
                # `symbol` is empty on balance/credit/correction — preserve as-is
                # so the consumer can identify non-trade rows.
                "symbol": d.symbol or "",
                "type": type_name,
                "typeCode": int(d.type),
                "entry": int(d.entry),
                "volume": d.volume,
                "price": d.price,
                "profit": d.profit,
                "commission": d.commission,
                "fee": d.fee,
                "swap": d.swap,
                "positionId": d.position_id,
                "comment": d.comment or "",
            })

        return {"deals": result, "count": len(result)}
    except Exception as e:
        return {"error": f"Failed to fetch raw deals: {e}"}, 500
