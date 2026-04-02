import { useState, useEffect, useCallback, useRef } from 'react';
import type { SymbolExposure, TradeRecord } from '../types/compare';

const API_BASE = 'http://localhost:5000';

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

  // Fetch trades when symbol changes
  const fetchTrades = useCallback(async (symbol: string) => {
    try {
      const today = new Date().toISOString().split('T')[0];
      const res = await fetch(`${API_BASE}/api/compare/trades?symbol=${encodeURIComponent(symbol)}&from=${today}`);
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
