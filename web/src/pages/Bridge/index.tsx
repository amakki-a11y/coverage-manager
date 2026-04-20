import { useCallback, useEffect, useMemo, useState } from 'react';
import { THEME } from '../../theme';
import { useBridgeSocket } from '../../hooks/useBridgeSocket';
import type { BridgeExecutionsResponse, BridgeHealth, ExecutionPair } from '../../types/bridge';
import { BridgeFilters, type SideFilter } from './BridgeFilters';
import { BridgeTable } from './BridgeTable';
import { API_BASE } from '../../config';

const todayUtc = () => new Date().toISOString().slice(0, 10);

const ANOMALY_PIP_THRESHOLD = 10;
const MAX_LIVE_ROWS = 500;

function mergePair(prev: ExecutionPair[], incoming: ExecutionPair): ExecutionPair[] {
  const idx = prev.findIndex((p) => p.clientDealId === incoming.clientDealId);
  if (idx >= 0) {
    const next = prev.slice();
    next[idx] = incoming;
    return next;
  }
  // Insert at front (newest first) and cap list size to avoid unbounded growth.
  const next = [incoming, ...prev];
  return next.length > MAX_LIVE_ROWS ? next.slice(0, MAX_LIVE_ROWS) : next;
}

export function BridgePanel() {
  const [fromDate, setFromDate] = useState(todayUtc);
  const [toDate, setToDate] = useState(todayUtc);
  const [symbol, setSymbol] = useState('');
  const [sideFilter, setSideFilter] = useState<SideFilter>('ALL');
  const [anomalyOnly, setAnomalyOnly] = useState(false);
  const [pairs, setPairs] = useState<ExecutionPair[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [health, setHealth] = useState<BridgeHealth | null>(null);

  const fetchPairs = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const qs = new URLSearchParams({ from: fromDate, to: toDate, limit: String(MAX_LIVE_ROWS) });
      if (symbol) qs.set('symbol', symbol);
      const res = await fetch(`${API_BASE}/api/bridge/executions?${qs}`);
      if (!res.ok) {
        const body = await res.json().catch(() => ({ error: `HTTP ${res.status}` }));
        setError(body?.error ?? 'Failed to load');
        return;
      }
      const data = (await res.json()) as BridgeExecutionsResponse;
      setPairs(data.pairs ?? []);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Request failed');
    } finally {
      setLoading(false);
    }
  }, [fromDate, toDate, symbol]);

  const fetchHealth = useCallback(async () => {
    try {
      const res = await fetch(`${API_BASE}/api/bridge/health`);
      if (res.ok) setHealth(await res.json());
    } catch { /* ignore */ }
  }, []);

  // Initial load + whenever filters that hit the server change.
  useEffect(() => {
    fetchPairs();
  }, [fetchPairs]);

  useEffect(() => {
    fetchHealth();
    const id = setInterval(fetchHealth, 5000);
    return () => clearInterval(id);
  }, [fetchHealth]);

  // Live updates via WS — merge into pairs list.
  const handlePair = useCallback((pair: ExecutionPair) => {
    setPairs((prev) => mergePair(prev, pair));
  }, []);
  const { connected } = useBridgeSocket(handlePair);

  // Distinct symbol list for the filter dropdown (based on what we currently have).
  const symbolOptions = useMemo(() => {
    const set = new Set<string>();
    pairs.forEach((p) => set.add(p.symbol));
    return Array.from(set).sort();
  }, [pairs]);

  const filteredPairs = useMemo(() => {
    return pairs.filter((p) => {
      if (symbol && p.symbol !== symbol) return false;
      if (sideFilter !== 'ALL' && p.side !== sideFilter) return false;
      if (anomalyOnly) {
        const noCov = p.covFills.length === 0;
        const bigPips = Math.abs(p.pips) > ANOMALY_PIP_THRESHOLD;
        if (!noCov && !bigPips) return false;
      }
      return true;
    });
  }, [pairs, symbol, sideFilter, anomalyOnly]);

  return (
    <div style={{ padding: 20, overflow: 'auto', flex: 1 }}>
      <div style={{ marginBottom: 16 }}>
        <h2 style={{ margin: '0 0 4px', color: THEME.t1, fontSize: 18, fontWeight: 600 }}>
          Bridge Execution Analysis
        </h2>
        <div style={{ color: THEME.t3, fontSize: 12 }}>
          CLIENT fills paired with COV OUT coverage legs from the Centroid CS 360 Dropcopy feed.
          All times in UTC.
        </div>
      </div>

      <BridgeFilters
        fromDate={fromDate}
        toDate={toDate}
        symbol={symbol}
        symbols={symbolOptions}
        sideFilter={sideFilter}
        anomalyOnly={anomalyOnly}
        loading={loading}
        onFromChange={setFromDate}
        onToChange={setToDate}
        onSymbolChange={setSymbol}
        onSideChange={setSideFilter}
        onAnomalyToggle={setAnomalyOnly}
        onRefresh={fetchPairs}
        connected={connected}
        healthMode={health?.mode ?? 'unknown'}
      />

      {error && (
        <div style={{
          padding: 12,
          background: THEME.badgeRed,
          border: `1px solid ${THEME.red}`,
          borderRadius: 4,
          color: THEME.red,
          fontSize: 12,
          marginBottom: 16,
        }}>
          {error}
        </div>
      )}

      <BridgeTable pairs={filteredPairs} pipThresholdForAnomaly={ANOMALY_PIP_THRESHOLD} />

      <div style={{ marginTop: 12, fontSize: 11, color: THEME.t3, lineHeight: 1.6 }}>
        <strong>Price Edge</strong> = (avg cov price − client price) for SELL, reversed for BUY. Positive = broker gain.{' '}
        <strong>Pips</strong> = Price Edge / pip size. <strong>Time Diff</strong> = cov time − client time (ms). Negative = pre-hedge.{' '}
        Rows with no coverage leg or |pips| &gt; {ANOMALY_PIP_THRESHOLD} are tinted red.
      </div>
    </div>
  );
}
