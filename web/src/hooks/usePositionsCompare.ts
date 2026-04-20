import { useState, useEffect, useCallback, useRef } from 'react';
import type { SymbolExposure, TradeRecord } from '../types/compare';
import { API_BASE } from '../config';

/**
 * Drives the Compare tab. Polls two REST endpoints on different cadences:
 *
 * - `/api/compare/exposure` every 500 ms — merged per-symbol snapshot (B-Book
 *   + Coverage volumes, P&L, hedge%). This is a "cheap" read — served from
 *   the same in-memory state the WebSocket uses.
 * - `/api/compare/trades`   every 5 s — merged trade stream (client +
 *   coverage) for the right-panel charts. 2-day rolling window so pre-market
 *   activity shows up on the detail view before today accumulates fills.
 *
 * Exposes: `symbols`, `trades`, `selectedSymbol`, `isConnected`, `lastUpdated`,
 * plus `selectSymbol(s)` and `refresh()`.
 */
export function usePositionsCompare() {
  const [symbols, setSymbols] = useState<SymbolExposure[]>([]);
  const [trades, setTrades] = useState<TradeRecord[]>([]);
  const [selectedSymbol, setSelectedSymbol] = useState<string | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [lastUpdated, setLastUpdated] = useState<Date>(new Date());
  const intervalRef = useRef<number | null>(null);

  // Poll exposure data every 200ms
  const fetchExposure = useCallback(async () => {
    try {
      const res = await fetch(`${API_BASE}/api/compare/exposure`);
      if (res.ok) {
        const data = await res.json();
        setSymbols(data.symbols ?? []);
        setLastUpdated(new Date(data.timestamp));
        setIsConnected(true);
      }
    } catch {
      setIsConnected(false);
    }
  }, []);

  // Fetch trades when symbol changes. 2-day rolling window so the widget shows
  // yesterday's closed activity early-UTC before today accumulates any fills.
  const fetchTrades = useCallback(async (symbol: string) => {
    try {
      const now = new Date();
      const today = now.toISOString().split('T')[0];
      const fromDate = new Date(now.getTime() - 24 * 3600_000).toISOString().split('T')[0];
      const res = await fetch(`${API_BASE}/api/compare/trades?symbol=${encodeURIComponent(symbol)}&from=${fromDate}&to=${today}`);
      if (res.ok) {
        const data = await res.json();
        setTrades(data.trades ?? []);
      }
    } catch {
      setTrades([]);
    }
  }, []);

  useEffect(() => {
    fetchExposure();
    intervalRef.current = window.setInterval(fetchExposure, 500);
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, [fetchExposure]);

  useEffect(() => {
    if (selectedSymbol) {
      fetchTrades(selectedSymbol);
      const id = window.setInterval(() => fetchTrades(selectedSymbol), 5000);
      return () => clearInterval(id);
    } else {
      setTrades([]);
    }
  }, [selectedSymbol, fetchTrades]);

  return {
    symbols,
    trades,
    selectedSymbol,
    setSelectedSymbol,
    isConnected,
    lastUpdated,
  };
}
