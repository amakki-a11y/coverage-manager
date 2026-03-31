"""
Coverage Manager — Python Collector
Reads positions from MT5 terminal, POSTs to C# backend.
Phase 4 adds order execution endpoint.
"""
from fastapi import FastAPI
import MetaTrader5 as mt5
import httpx
import asyncio
from contextlib import asynccontextmanager

# Config
API_URL = "http://YOUR_VPS_IP:5000/api/coverage/positions"
POLL_INTERVAL = 0.1  # 100ms


async def position_loop():
    """Read MT5 positions every 100ms and POST to backend."""
    async with httpx.AsyncClient(timeout=2.0) as client:
        while True:
            try:
                positions = mt5.positions_get()
                if positions:
                    data = [{
                        "symbol": p.symbol,
                        "direction": "BUY" if p.type == 0 else "SELL",
                        "volume": p.volume,
                        "openPrice": p.price_open,
                        "currentPrice": p.price_current,
                        "profit": p.profit,
                        "swap": p.swap,
                        "ticket": p.ticket
                    } for p in positions]
                    await client.post(API_URL, json=data)
                else:
                    await client.post(API_URL, json=[])
            except Exception as e:
                print(f"Error: {e}")
            await asyncio.sleep(POLL_INTERVAL)


@asynccontextmanager
async def lifespan(app: FastAPI):
    # Initialize MT5
    if not mt5.initialize():
        raise RuntimeError(f"MT5 init failed: {mt5.last_error()}")
    print(f"MT5 connected: {mt5.terminal_info().name}")

    # Start position polling loop
    task = asyncio.create_task(position_loop())
    yield
    task.cancel()
    mt5.shutdown()


app = FastAPI(title="Coverage Manager Collector", lifespan=lifespan)


@app.get("/health")
def health():
    info = mt5.terminal_info()
    return {"status": "ok", "terminal": info.name if info else "disconnected"}


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
