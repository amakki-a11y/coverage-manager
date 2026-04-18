import { useState, useEffect, useCallback, useRef } from 'react';
import { THEME } from '../theme';
import type { PeriodPnLResponse, PeriodPnLRow, PeriodPnLSide } from '../types';

const API_BASE = 'http://localhost:5000';

function todayStr() {
  return new Date().toISOString().slice(0, 10);
}

function fmt(v: number | undefined | null, opts?: { signed?: boolean; dim?: boolean }) {
  if (v === undefined || v === null || !isFinite(v as number)) return '—';
  const signed = opts?.signed ?? true;
  const s = (v as number).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  if (!signed) return s;
  return (v as number) >= 0 ? `+${s}` : s;
}

function pnlColor(v: number | undefined | null): string {
  if (v === undefined || v === null) return THEME.t3;
  if (v > 0) return THEME.green;
  if (v < 0) return THEME.red;
  return THEME.t2;
}

const cellStyle: React.CSSProperties = {
  padding: '7px 8px',
  fontFamily: 'monospace',
  fontSize: 12,
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
  position: 'sticky',
  top: 0,
  background: THEME.bg2,
  borderBottom: `1px solid ${THEME.border}`,
  zIndex: 1,
};

const groupHeader: React.CSSProperties = {
  ...headerStyle,
  fontSize: 11,
  fontWeight: 700,
  letterSpacing: 1,
  textAlign: 'center',
  borderBottom: 'none',
};

// Distinct divider between Clients / Coverage / Edge sections — uses the accent color
// of the section it introduces. Blue (Clients), teal (Coverage), text-2 (Edge).
const CLIENT_DIVIDER   = `2px solid ${THEME.blue}`;
const COVERAGE_DIVIDER = `2px solid ${THEME.teal}`;
const EDGE_DIVIDER     = `2px solid ${THEME.t2}`;

// Slight background tint to visually group the Coverage section and set it apart
// from Clients. Alpha-mixed against the card background so it doesn't overpower.
const COVERAGE_BG = 'rgba(77, 208, 225, 0.045)'; // teal @ 4.5%
const EDGE_BG     = 'rgba(255, 255, 255, 0.03)'; // neutral dim

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
  border: 'none',
  borderRadius: 4,
  padding: '6px 14px',
  fontSize: 13,
  fontWeight: 600,
  cursor: 'pointer',
};

// "—" for columns that are meaningless when there's no live open position
// (Begin/Current/ΔFloat of a symbol you aren't holding right now).
function fmtPos(v: number, hasPos: boolean): string {
  if (!hasPos) return '—';
  return fmt(v);
}

function BeginCell({ side, bg, divider }: { side: PeriodPnLSide; bg?: string; divider?: string }) {
  const base: React.CSSProperties = { ...cellStyle, background: bg, borderLeft: divider };
  if (!side.hasOpenPosition) return <td style={{ ...base, color: THEME.t3 }}>—</td>;
  return (
    <td style={{ ...base, color: pnlColor(side.beginFloating) }}>
      <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4, justifyContent: 'flex-end' }}>
        {fmt(side.beginFloating)}
        {!side.beginFromSnapshot && (
          <span
            title="No snapshot existed before the selected period — Begin treated as 0."
            style={{ width: 7, height: 7, borderRadius: '50%', background: THEME.amber, display: 'inline-block', flexShrink: 0 }}
          />
        )}
      </span>
    </td>
  );
}

function SideCells({ side, bg, divider }: { side: PeriodPnLSide; bg?: string; divider?: string }) {
  const cell: React.CSSProperties = { ...cellStyle, background: bg };
  return (
    <>
      <BeginCell side={side} bg={bg} divider={divider} />
      <td style={{ ...cell, color: side.hasOpenPosition ? pnlColor(side.currentFloating) : THEME.t3 }}>
        {fmtPos(side.currentFloating, side.hasOpenPosition)}
      </td>
      <td style={{ ...cell, color: side.hasOpenPosition ? pnlColor(side.floatingDelta) : THEME.t3, fontWeight: 600 }}>
        {fmtPos(side.floatingDelta, side.hasOpenPosition)}
      </td>
      <td style={{ ...cell, color: pnlColor(side.settled) }}>{fmt(side.settled)}</td>
      <td style={{ ...cell, color: pnlColor(side.net), fontWeight: 700 }}>{fmt(side.net)}</td>
    </>
  );
}

export function PeriodPnLPanel() {
  const [fromDate, setFromDate] = useState(todayStr());
  const [toDate, setToDate] = useState(todayStr());
  const [data, setData] = useState<PeriodPnLResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [capturing, setCapturing] = useState(false);
  const [err, setErr] = useState<string | null>(null);
  const loadSeq = useRef(0);

  // Silent refresh (used by interval polling) does NOT toggle the loading badge
  // so it doesn't flash every 10s. Only date-changed fetches show the badge.
  const fetchPeriod = useCallback(async (showLoading: boolean) => {
    const ticket = ++loadSeq.current;
    const shownAt = Date.now();
    if (showLoading) { setLoading(true); setErr(null); }
    try {
      const res = await fetch(`${API_BASE}/api/exposure/pnl/period?from=${fromDate}&to=${toDate}`);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const json: PeriodPnLResponse = await res.json();
      if (ticket === loadSeq.current) setData(json);
    } catch (e) {
      if (ticket === loadSeq.current && showLoading) setErr(e instanceof Error ? e.message : 'fetch failed');
    } finally {
      if (showLoading) {
        // Keep the loading badge visible ≥ 450ms to avoid flash.
        const remaining = Math.max(0, 450 - (Date.now() - shownAt));
        setTimeout(() => { if (ticket === loadSeq.current) setLoading(false); }, remaining);
      }
    }
  }, [fromDate, toDate]);

  useEffect(() => {
    fetchPeriod(true); // date change: show loading badge
    const interval = setInterval(() => fetchPeriod(false), 10_000); // silent refresh for "current" values
    return () => clearInterval(interval);
  }, [fetchPeriod]);

  const captureNow = async () => {
    setCapturing(true);
    try {
      await fetch(`${API_BASE}/api/exposure/snapshot`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ label: 'from-panel' }),
      });
      await fetchPeriod(true);
    } catch { /* ignore */ }
    setCapturing(false);
  };

  const rows = data?.rows ?? [];
  const totals = data?.totals;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', flex: 1 }}>
      {/* Toolbar */}
      <div style={{
        display: 'flex', gap: 12, padding: '10px 16px',
        background: THEME.bg2, borderBottom: `1px solid ${THEME.border}`,
        alignItems: 'center',
      }}>
        <span style={{ color: THEME.t3, fontSize: 11, textTransform: 'uppercase', fontWeight: 600 }}>From</span>
        <input type="date" value={fromDate} onChange={e => setFromDate(e.target.value)} style={inputStyle} />
        <span style={{ color: THEME.t3, fontSize: 11, textTransform: 'uppercase', fontWeight: 600 }}>To</span>
        <input type="date" value={toDate} onChange={e => setToDate(e.target.value)} style={inputStyle} />

        <button
          onClick={captureNow}
          disabled={capturing}
          style={{ ...btnStyle, background: THEME.blue, color: '#fff', opacity: capturing ? 0.5 : 1 }}
          title="Capture a snapshot right now so today's Begin anchor exists."
        >
          {capturing ? 'Capturing…' : 'Capture Snapshot Now'}
        </button>

        {loading && (
          <span
            title="Fetching period P&L"
            style={{
              display: 'inline-flex', alignItems: 'center', gap: 6,
              padding: '3px 10px', borderRadius: 10, fontSize: 11, fontWeight: 600,
              color: THEME.amber, background: 'rgba(255, 167, 38, 0.12)',
              border: `1px solid ${THEME.amber}`,
            }}
          >
            <span style={{
              width: 8, height: 8, borderRadius: '50%',
              border: `2px solid ${THEME.amber}`, borderTopColor: 'transparent',
              animation: 'cm-spin 0.8s linear infinite',
            }} />
            Loading…
          </span>
        )}

        {data && (
          <span style={{ color: THEME.t3, fontSize: 11, marginLeft: 'auto' }} title={`Begin anchor (UTC): ${data.beginAnchorUtc}`}>
            Begin anchor: <code style={{ color: THEME.t2 }}>{data.beginAnchorUtc.replace('T', ' ').replace(/\.\d+Z?$/, 'Z')}</code>
          </span>
        )}
      </div>

      <style>{`@keyframes cm-spin { to { transform: rotate(360deg); } }`}</style>

      {err && <div style={{ padding: 16, color: THEME.red }}>Error: {err}</div>}

      <div style={{ overflow: 'auto', flex: 1 }}>
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr>
              <th style={{ ...groupHeader, width: 120, background: THEME.bg2, borderBottom: `1px solid ${THEME.border}` }} rowSpan={2}>Symbol</th>
              <th style={{ ...groupHeader, color: THEME.blue, borderLeft: CLIENT_DIVIDER }} colSpan={5}>CLIENTS</th>
              <th style={{ ...groupHeader, color: THEME.teal, borderLeft: COVERAGE_DIVIDER, background: COVERAGE_BG }} colSpan={5}>COVERAGE</th>
              <th style={{ ...groupHeader, color: THEME.t2, borderLeft: EDGE_DIVIDER, background: EDGE_BG }} rowSpan={2}>Edge&nbsp;Net</th>
            </tr>
            <tr>
              <th style={{ ...headerStyle, top: 28, borderLeft: CLIENT_DIVIDER }}>Begin</th>
              <th style={{ ...headerStyle, top: 28 }}>Current</th>
              <th style={{ ...headerStyle, top: 28 }}>Δ Float</th>
              <th style={{ ...headerStyle, top: 28 }}>Settled</th>
              <th style={{ ...headerStyle, top: 28 }}>Net</th>
              <th style={{ ...headerStyle, top: 28, borderLeft: COVERAGE_DIVIDER, background: COVERAGE_BG }}>Begin</th>
              <th style={{ ...headerStyle, top: 28, background: COVERAGE_BG }}>Current</th>
              <th style={{ ...headerStyle, top: 28, background: COVERAGE_BG }}>Δ Float</th>
              <th style={{ ...headerStyle, top: 28, background: COVERAGE_BG }}>Settled</th>
              <th style={{ ...headerStyle, top: 28, background: COVERAGE_BG }}>Net</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r: PeriodPnLRow) => (
              <tr key={r.canonicalSymbol} style={{ borderBottom: `1px solid ${THEME.border}` }}>
                <td style={{ ...cellStyle, textAlign: 'left', color: THEME.t1, fontWeight: 600, fontFamily: 'inherit' }}>
                  {r.canonicalSymbol}
                </td>
                <SideCells side={r.bBook} divider={CLIENT_DIVIDER} />
                <SideCells side={r.coverage} bg={COVERAGE_BG} divider={COVERAGE_DIVIDER} />
                <td style={{
                  ...cellStyle, borderLeft: EDGE_DIVIDER, background: EDGE_BG,
                  color: pnlColor(r.edge.net), fontWeight: 700,
                }}>{fmt(r.edge.net)}</td>
              </tr>
            ))}
            {rows.length === 0 && (
              <tr>
                <td colSpan={12} style={{ padding: 36, textAlign: 'center', color: THEME.t3 }}>
                  No rows for this period.
                </td>
              </tr>
            )}
          </tbody>
          {totals && (
            <tfoot>
              <tr style={{ borderTop: `2px solid ${THEME.border}`, background: THEME.bg3 }}>
                <td style={{ ...cellStyle, textAlign: 'left', color: THEME.t2, fontWeight: 700, fontFamily: 'inherit' }}>TOTAL</td>
                <SideCells side={totals.bBook} divider={CLIENT_DIVIDER} />
                <SideCells side={totals.coverage} bg={COVERAGE_BG} divider={COVERAGE_DIVIDER} />
                <td style={{
                  ...cellStyle, borderLeft: EDGE_DIVIDER, background: EDGE_BG,
                  color: pnlColor(totals.edge.net), fontWeight: 700,
                }}>{fmt(totals.edge.net)}</td>
              </tr>
            </tfoot>
          )}
        </table>
      </div>
    </div>
  );
}
