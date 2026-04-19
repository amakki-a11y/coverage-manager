"""
Coverage Manager — Python Collector
Reads coverage (LP) positions from MT5 terminal, POSTs to C# backend.
Fetches account credentials from the backend API on startup.
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


def init_mt5(account):
    """Initialize MT5 terminal with coverage account credentials."""
    login = int(account["login"])
    server = account["server"]
    password = account["password"]

    print(f"Connecting to MT5: login={login}, server={server}")

    if not mt5.initialize(login=login, server=server, password=password):
        error = mt5.last_error()
        raise RuntimeError(f"MT5 init failed: {error}")

    info = mt5.terminal_info()
    acc_info = mt5.account_info()
    print(f"MT5 connected: {info.name}")
    print(f"Account: {acc_info.login} on {acc_info.server}, balance={acc_info.balance}")


async def position_loop():
    """Read MT5 positions every 100ms and POST to backend."""
    url = f"{BACKEND_URL}/api/coverage/positions"
    async with httpx.AsyncClient(timeout=2.0) as client:
        while True:
            try:
                positions = mt5.positions_get()
                if positions:
                    # Account login is constant per session, read once per tick (cheap).
                    acc = mt5.account_info()
                    login = int(acc.login) if acc else 0
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
                    await client.post(url, json=data)
                else:
                    await client.post(url, json=[])
            except Exception as e:
                print(f"Error: {e}")
            await asyncio.sleep(POLL_INTERVAL)


@asynccontextmanager
async def lifespan(app: FastAPI):
    # Try to attach to already-running MT5 terminal first
    if mt5.initialize():
        acc = mt5.account_info()
        info = mt5.terminal_info()
        print(f"MT5 attached to running terminal: {info.name}")
        print(f"Account: {acc.login} on {acc.server}, balance={acc.balance}")
    else:
        # Fall back to credentials from backend
        account = await fetch_coverage_account()
        if account is None:
            print("WARNING: No active coverage account found and no MT5 terminal running.")
            print("Add a coverage account via the Settings tab and restart the collector.")
            yield
            return
        init_mt5(account)

    # Start position polling loop
    task = asyncio.create_task(position_loop())
    yield
    task.cancel()
    mt5.shutdown()


app = FastAPI(title="Coverage Manager Collector", lifespan=lifespan)
app.add_middleware(CORSMiddleware, allow_origins=["*"], allow_methods=["*"], allow_headers=["*"])


@app.get("/health")
def health():
    info = mt5.terminal_info()
    acc = mt5.account_info()
    return {
        "status": "ok" if info else "disconnected",
        "terminal": info.name if info else None,
        "login": acc.login if acc else None,
        "server": acc.server if acc else None,
    }


@app.get("/positions")
def get_positions():
    """Alternative: C# can pull instead of Python pushing."""
    positions = mt5.positions_get()
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
def get_deals(
    from_date: str = Query(..., alias="from", description="Start date YYYY-MM-DD"),
    to_date: str = Query(..., alias="to", description="End date YYYY-MM-DD"),
):
    """Get closed deals from coverage account for a date range."""
    dt_from = datetime.strptime(from_date, "%Y-%m-%d").replace(tzinfo=timezone.utc)
    # MT5 history_deals_get 'to' is exclusive, so add 1 day to include the end date
    dt_to = datetime.strptime(to_date, "%Y-%m-%d").replace(tzinfo=timezone.utc) + timedelta(days=1)

    deals = mt5.history_deals_get(dt_from, dt_to)
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


@app.get("/deals/raw")
def get_deals_raw(
    from_date: str = Query(..., alias="from", description="Start date YYYY-MM-DD"),
    to_date: str = Query(..., alias="to", description="End date YYYY-MM-DD"),
):
    """
    Return individual coverage deals (not aggregated) for matching against client deals.
    Used by the /api/markup/match endpoint for time-window matching.
    """
    try:
        dt_from = datetime.strptime(from_date, "%Y-%m-%d").replace(tzinfo=timezone.utc)
        dt_to = datetime.strptime(to_date, "%Y-%m-%d").replace(tzinfo=timezone.utc) + timedelta(days=1)
    except ValueError as e:
        return {"error": f"Invalid date format, expected YYYY-MM-DD: {e}"}, 400

    try:
        deals = mt5.history_deals_get(dt_from, dt_to)
        if deals is None:
            return {"deals": []}

        result = []
        for d in deals:
            if d.type >= 2 or not d.symbol:
                continue  # skip balance/credit
            result.append({
                "ticket": d.ticket,
                "order": d.order,
                "magic": getattr(d, "magic", 0),
                "externalId": getattr(d, "external_id", "") or "",
                "time": datetime.fromtimestamp(d.time_msc / 1000, tz=timezone.utc).isoformat(),
                "timeMsc": d.time_msc,
                "symbol": d.symbol,
                "type": "buy" if d.type == 0 else "sell",
                "entry": d.entry,
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
