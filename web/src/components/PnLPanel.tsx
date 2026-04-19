import { useState, useEffect, useCallback } from 'react';
import { THEME } from '../theme';
import { useDateRange } from '../hooks/useDateRange';

/**
 * Legacy "P&L" tab — settled-only P&L per canonical symbol for the picker range.
 *
 * Uses:
 *   - `GET /api/exposure/pnl?from=&to=` for B-Book (sub-second `aggregate_bbook_pnl_full` RPC).
 *   - `GET http://localhost:8100/deals?from=&to=` for optional Coverage P&L toggle.
 *
 * Presents client perspective (positive P&L = clients profited) — not inverted
 * for broker view. For the dealer-oriented broker edge view see `PeriodPnLPanel`
 * (Net P&L tab), which adds floating + delta decomposition.
 */
interface SymbolPnL {
  symbol: string;
  dealCount: number;
  totalProfit: number;
  totalCommission: number;
  totalSwap: number;
  totalFee: number;
  netPnL: number;
  totalVolume: number;
}

interface DailyPnL {
  date: string;
  dealCount: number;
  totalProfit: number;
  totalCommission: number;
  totalSwap: number;
  totalFee: number;
  netPnL: number;
  symbols: SymbolPnL[];
}

interface PnLData {
  totalDeals: number;
  symbols: SymbolPnL[];
  daily?: DailyPnL[];
}

interface CoverageSymbolPnL {
  symbol: string;
  dealCount: number;
  totalProfit: number;
  totalCommission: number;
  totalSwap: number;
  totalFee: number;
  netPnL: number;
  totalVolume: number;
}

interface SymbolMappingEntry {
  canonical_name: string;
  bbook_symbol: string;
  coverage_symbol: string;
}

const cellStyle: React.CSSProperties = {
  padding: '8px 12px',
  fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace",
  fontSize: 13,
  textAlign: 'right',
  whiteSpace: 'nowrap',
};

const headerStyle: React.CSSProperties = {
  ...cellStyle,
  color: THEME.t3,
  fontSize: 10,
  textTransform: 'uppercase',
  letterSpacing: 0.5,
  fontWeight: 600,
  fontFamily: 'inherit',
  borderBottom: `1px solid ${THEME.border}`,
  position: 'sticky',
  top: 0,
  background: THEME.bg2,
};

const inputStyle: React.CSSProperties = {
  background: THEME.bg3,
  border: `1px solid ${THEME.border}`,
  borderRadius: 4,
  color: THEME.t1,
  padding: '6px 10px',
  fontSize: 13,
  fontFamily: 'inherit',
  outline: 'none',
};

const btnStyle: React.CSSProperties = {
  background: THEME.blue,
  color: '#fff',
  border: 'none',
  borderRadius: 4,
  padding: '6px 16px',
  fontSize: 13,
  fontWeight: 600,
  cursor: 'pointer',
};

export function PnLPanel() {
  const [data, setData] = useState<PnLData | null>(null);
  // Shared date range — persisted across tab switches via localStorage.
  const [fromDate, toDate, setFromDate, setToDate] = useDateRange();
  const [loading, setLoading] = useState(false);
  const [showCoverage, setShowCoverage] = useState(false);
  const [coverageData, setCoverageData] = useState<Record<string, CoverageSymbolPnL>>({});

  // Auto-fetch on mount + every 15s. Polls the CURRENT date range (prior version
  // polled no-params = "today" which silently overwrote the user's selection every
  // 3s). A multi-day range takes several seconds server-side, so the 3s cadence
  // caused overlapping in-flight requests that visibly flashed the table. A
  // `inFlight` ref guards against re-entry.
  useEffect(() => {
    let cancelled = false;
    let inFlight = false;
    const fetchPnL = async () => {
      if (inFlight) return;
      inFlight = true;
      try {
        const res = await fetch(`http://localhost:5000/api/exposure/pnl?from=${fromDate}&to=${toDate}`);
        if (!cancelled && res.ok) setData(await res.json());
      } catch { /* ignore */ }
      inFlight = false;
    };
    fetchPnL();
    const interval = setInterval(fetchPnL, 15000);
    return () => { cancelled = true; clearInterval(interval); };
  }, [fromDate, toDate]);

  const fetchCoverageDeals = useCallback(async () => {
    try {
      // Fetch mappings and coverage deals in parallel
      const [mapRes, dealRes] = await Promise.all([
        fetch('http://localhost:5000/api/mappings'),
        fetch(`http://localhost:8100/deals?from=${fromDate}&to=${toDate}`),
      ]);
      if (!mapRes.ok || !dealRes.ok) return;

      const mappings: SymbolMappingEntry[] = await mapRes.json();
      const cov = await dealRes.json();

      // Build coverage_symbol → canonical lookup
      const covToCanonical: Record<string, string> = {};
      const bbookToCanonical: Record<string, string> = {};
      for (const m of mappings) {
        if (m.coverage_symbol) covToCanonical[m.coverage_symbol] = m.canonical_name;
        if (m.bbook_symbol) bbookToCanonical[m.bbook_symbol] = m.canonical_name;
      }

      // Helper: find canonical name for a coverage deal symbol
      const findCanonical = (sym: string): string => {
        if (covToCanonical[sym]) return covToCanonical[sym];
        // Try stripping common suffixes: XAUUSD- → XAUUSD, US30.c → US30
        const stripped = sym.replace(/[-.].*$/, '');
        if (covToCanonical[stripped]) return covToCanonical[stripped];
        return sym; // fallback: use as-is
      };

      // Helper: find B-Book P&L symbol for a canonical name
      // B-Book deals use symbols like "XAUUSD-" — find best match
      const bbookSymbols = (data?.symbols ?? []).map(s => s.symbol);
      const findBBookSymbol = (canonical: string): string => {
        // Check if any B-Book P&L symbol starts with the canonical name
        const match = bbookSymbols.find(s => s === canonical || s.startsWith(canonical));
        return match || canonical;
      };

      // Map coverage deals → B-Book symbol names via canonical
      const result: Record<string, CoverageSymbolPnL> = {};
      for (const s of (cov.symbols ?? []) as CoverageSymbolPnL[]) {
        const canonical = findCanonical(s.symbol);
        const bbSymbol = findBBookSymbol(canonical);
        if (result[bbSymbol]) {
          result[bbSymbol].dealCount += s.dealCount;
          result[bbSymbol].netPnL += s.netPnL;
          result[bbSymbol].totalProfit += s.totalProfit;
          result[bbSymbol].totalVolume += s.totalVolume;
        } else {
          result[bbSymbol] = { ...s, symbol: bbSymbol };
        }
      }
      setCoverageData(result);
    } catch { /* ignore */ }
  }, [fromDate, toDate, data]);

  const loadRange = useCallback(async () => {
    setLoading(true);
    try {
      const res = await fetch(
        `http://localhost:5000/api/exposure/pnl/reload?from=${fromDate}&to=${toDate}`,
        { method: 'POST' }
      );
      if (res.ok) setData(await res.json());
      if (showCoverage) await fetchCoverageDeals();
    } catch { /* ignore */ }
    setLoading(false);
  }, [fromDate, toDate, showCoverage, fetchCoverageDeals]);

  // Fetch coverage deals when toggle turns on
  useEffect(() => {
    if (showCoverage) fetchCoverageDeals();
  }, [showCoverage, fetchCoverageDeals]);

  // Show the layout (date pickers, reload button) even before the first fetch resolves
  // so the user can re-pick a range without waiting on a multi-second query.
  const safeData = data ?? { totalDeals: 0, symbols: [] as SymbolPnL[], daily: [] as DailyPnL[] };

  const grandTotal = safeData.symbols.reduce(
    (acc, s) => ({
      deals: acc.deals + s.dealCount,
      profit: acc.profit + s.totalProfit,
      commission: acc.commission + s.totalCommission,
      swap: acc.swap + s.totalSwap,
      net: acc.net + s.netPnL,
      volume: acc.volume + s.totalVolume,
    }),
    { deals: 0, profit: 0, commission: 0, swap: 0, net: 0, volume: 0 }
  );

  return (
    <div style={{ display: 'flex', flexDirection: 'column', flex: 1 }}>
      {/* Date picker bar */}
      <div style={{
        display: 'flex',
        gap: 12,
        padding: '10px 16px',
        background: THEME.bg2,
        borderBottom: `1px solid ${THEME.border}`,
        alignItems: 'center',
      }}>
        <span style={{ color: THEME.t3, fontSize: 11, textTransform: 'uppercase', fontWeight: 600 }}>
          From
        </span>
        <input
          type="date"
          value={fromDate}
          onChange={(e) => setFromDate(e.target.value)}
          style={inputStyle}
        />
        <span style={{ color: THEME.t3, fontSize: 11, textTransform: 'uppercase', fontWeight: 600 }}>
          To
        </span>
        <input
          type="date"
          value={toDate}
          onChange={(e) => setToDate(e.target.value)}
          style={inputStyle}
        />
        <button onClick={loadRange} disabled={loading} style={{
          ...btnStyle,
          opacity: loading ? 0.6 : 1,
        }}>
          {loading ? 'Loading...' : 'Load'}
        </button>

        <label style={{
          display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer',
          color: showCoverage ? THEME.blue : THEME.t3, fontSize: 12, fontWeight: 600,
        }}>
          <input
            type="checkbox"
            checked={showCoverage}
            onChange={(e) => setShowCoverage(e.target.checked)}
            style={{ accentColor: THEME.blue }}
          />
          Coverage
        </label>

        <div style={{ marginLeft: 'auto', display: 'flex', gap: 16, alignItems: 'center' }}>
          <span style={{
            fontSize: 18,
            fontWeight: 700,
            fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace",
            color: grandTotal.net >= 0 ? THEME.green : THEME.red,
          }}>
            {fmtPnl(grandTotal.net)}
          </span>
          <span style={{ color: THEME.t3, fontSize: 12 }}>
            {safeData.totalDeals} deals | {safeData.symbols.length} symbols | {(safeData.daily ?? []).length} days
            {!data && ' · loading…'}
          </span>
        </div>
      </div>

      <div style={{ overflow: 'auto', flex: 1 }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', tableLayout: 'fixed' }}>
          <thead>
            <tr>
              <th style={{ ...headerStyle, textAlign: 'left', width: 140 }}>Symbol</th>
              <th style={headerStyle}>Volume</th>
              <th style={headerStyle}>Profit</th>
              <th style={headerStyle}>Commission</th>
              <th style={headerStyle}>Swap</th>
              <th style={{ ...headerStyle, fontWeight: 700 }}>Net P&L</th>
              {showCoverage && <>
                <th style={{ ...headerStyle, borderLeft: `1px solid ${THEME.border}` }}>Cov P&L</th>
                <th style={{ ...headerStyle, fontWeight: 700 }}>Broker Net</th>
              </>}
            </tr>
          </thead>
          <tbody>
            {safeData.symbols.map((s) => (
              <tr key={s.symbol} style={{ borderBottom: `1px solid ${THEME.border}` }}>
                <td style={{ ...cellStyle, textAlign: 'left', color: THEME.t1, fontWeight: 600, fontFamily: 'inherit' }}>
                  {s.symbol}
                </td>
                <td style={{ ...cellStyle, color: THEME.t2 }}>{s.totalVolume.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
                <td style={{ ...cellStyle, color: pnlColor(s.totalProfit) }}>{fmtPnl(s.totalProfit)}</td>
                <td style={{ ...cellStyle, color: s.totalCommission < 0 ? THEME.red : THEME.t2 }}>
                  {fmtPnl(s.totalCommission)}
                </td>
                <td style={{ ...cellStyle, color: pnlColor(s.totalSwap) }}>{fmtPnl(s.totalSwap)}</td>
                <td style={{ ...cellStyle, color: pnlColor(s.netPnL), fontWeight: 700 }}>{fmtPnl(s.netPnL)}</td>
                {showCoverage && (() => {
                  const cov = coverageData[s.symbol];
                  const covPnL = cov?.netPnL ?? 0;
                  // Broker net = −(client P&L) + coverage P&L (CLAUDE.md P&L framework).
                  // When clients lose, the broker books a gain on the B-Book side; the
                  // LP coverage leg nets against that. Adding the two raw numbers gives
                  // the combined-loss-on-the-book which isn't a useful metric.
                  const brokerNet = -s.netPnL + covPnL;
                  return <>
                    <td style={{ ...cellStyle, borderLeft: `1px solid ${THEME.border}`, color: covPnL !== 0 ? pnlColor(covPnL) : THEME.t3 }}>
                      {covPnL !== 0 ? fmtPnl(covPnL) : '—'}
                    </td>
                    <td style={{ ...cellStyle, color: covPnL !== 0 ? pnlColor(brokerNet) : pnlColor(-s.netPnL), fontWeight: 600 }}>
                      {covPnL !== 0 ? fmtPnl(brokerNet) : fmtPnl(-s.netPnL)}
                    </td>
                  </>;
                })()}
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr style={{ borderTop: `2px solid ${THEME.border}`, background: THEME.bg3 }}>
              <td style={{ ...cellStyle, textAlign: 'left', color: THEME.t2, fontWeight: 700, fontFamily: 'inherit' }}>
                TOTAL
              </td>
              <td style={{ ...cellStyle, color: THEME.t2 }}>{grandTotal.volume.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
              <td style={{ ...cellStyle, color: pnlColor(grandTotal.profit) }}>{fmtPnl(grandTotal.profit)}</td>
              <td style={{ ...cellStyle, color: grandTotal.commission < 0 ? THEME.red : THEME.t2 }}>
                {fmtPnl(grandTotal.commission)}
              </td>
              <td style={{ ...cellStyle, color: pnlColor(grandTotal.swap) }}>{fmtPnl(grandTotal.swap)}</td>
              <td style={{ ...cellStyle, color: pnlColor(grandTotal.net), fontWeight: 700 }}>{fmtPnl(grandTotal.net)}</td>
              {showCoverage && (() => {
                const totalCovPnL = Object.values(coverageData).reduce((a, c) => a + c.netPnL, 0);
                // Broker net = −(client P&L) + coverage P&L per CLAUDE.md.
                const brokerNet = -grandTotal.net + totalCovPnL;
                return <>
                  <td style={{ ...cellStyle, color: pnlColor(totalCovPnL), borderLeft: `1px solid ${THEME.border}` }}>{fmtPnl(totalCovPnL)}</td>
                  <td style={{ ...cellStyle, color: pnlColor(brokerNet), fontWeight: 700 }}>{fmtPnl(brokerNet)}</td>
                </>;
              })()}
            </tr>
          </tfoot>
        </table>
      </div>
    </div>
  );
}

function fmtPnl(v: number): string {
  // Match the Exposure / Compare / Net P&L tabs: signed, 2 decimals, thousand separators.
  const sign = v >= 0 ? '+' : '';
  return sign + v.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function pnlColor(v: number): string {
  return v > 0 ? THEME.green : v < 0 ? THEME.red : THEME.t2;
}
