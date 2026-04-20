import { useEffect, useState, useCallback } from 'react';
import { THEME } from '../theme';
import { useDateRange } from '../hooks/useDateRange';
import { DateRangePicker } from './DateRangePicker';
import type { EquityPnLResponse, EquityPnLRow } from '../types';
import { API_BASE } from '../config';

/**
 * Equity P&L tab — per-login decomposition of the equity move across a range.
 *
 * <pre>
 * Supposed Eq   = Begin + NetDepW + NetCred
 * PL            = CurrentEq - Supposed Eq
 * NetPL (client) = PL - CommReb - SpreadReb - Adj - PS   (broker outlays stripped)
 * NetPL (cov)    = PL + CommReb + SpreadReb + Adj + PS   (broker income included)
 * Broker Edge    = -Σ(clients.NetPL) + Σ(coverage.NetPL)
 * </pre>
 */

const API = `${API_BASE}/api/equity-pnl`;

function fmt(n: number): string {
  if (!Number.isFinite(n)) return '\u2014';
  const sign = n < 0 ? '\u2212' : '';
  return `${sign}${Math.abs(n).toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;
}

/** Signed number with explicit + for positives, U+2212 for negatives. */
function fmtSigned(n: number): string {
  if (!Number.isFinite(n)) return '\u2014';
  if (n === 0) return '0.00';
  const sign = n < 0 ? '\u2212' : '+';
  return `${sign}${Math.abs(n).toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;
}

function pc(n: number): string {
  if (n > 0) return THEME.green;
  if (n < 0) return THEME.red;
  return THEME.t3;
}

const cell: React.CSSProperties = {
  padding: '5px 8px',
  fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace",
  fontSize: 11,
  textAlign: 'right',
  whiteSpace: 'nowrap',
};

const hdr: React.CSSProperties = {
  padding: '6px 8px',
  fontSize: 9,
  fontWeight: 700,
  textTransform: 'uppercase',
  letterSpacing: 0.4,
  color: THEME.t3,
  background: THEME.bg2,
  position: 'sticky',
  top: 0,
  // Sticky `<th>` inside a `border-collapse: collapse` table is easily
  // overpainted by body `<tr>`s once they scroll past — body cells share the
  // same stacking context and render on top of the sticky header. Bumping the
  // z-index above the default (auto/0) keeps the header visually on top.
  zIndex: 2,
  textAlign: 'right',
  whiteSpace: 'nowrap',
  // The real separator: a bottom-edge shadow instead of a border. Collapsed
  // table borders disappear from sticky cells after they detach from the
  // first visible row; a box-shadow is painted by the header itself, so it
  // always sits below the frozen row.
  boxShadow: `inset 0 -1px 0 ${THEME.border}`,
};

const hdrLeft: React.CSSProperties = { ...hdr, textAlign: 'left' };
const cellLeft: React.CSSProperties = { ...cell, textAlign: 'left' };

interface RowProps {
  row: EquityPnLRow;
  bold?: boolean;
}

function DataRow({ row, bold }: RowProps) {
  const labelColor = row.source === 'coverage' ? THEME.teal : THEME.blue;
  const weight = bold ? 700 : 500;
  return (
    <tr style={{ background: bold ? THEME.bg3 : 'transparent', borderBottom: `1px solid ${THEME.border}` }}>
      <td style={{ ...cellLeft, color: labelColor, fontWeight: 600 }}>{row.login || ''}</td>
      <td style={{ ...cellLeft, color: THEME.t1, fontWeight: weight, fontFamily: 'inherit' }}>
        {row.name || (bold ? 'TOTAL' : '—')}
        {!row.beginFromSnapshot && row.login > 0 && (
          <span
            title="No equity snapshot exists before the range start — Begin treated as 0."
            style={{ marginLeft: 6, color: THEME.amber, fontSize: 10 }}
          >
            {'\u25CF'}
          </span>
        )}
      </td>
      <td style={{ ...cell, color: THEME.t1, fontWeight: weight }}>{fmt(row.beginEquity)}</td>
      <td style={{ ...cell, color: pc(row.netDepositWithdraw), fontWeight: weight }}>
        {fmtSigned(row.netDepositWithdraw)}
      </td>
      <td style={{ ...cell, color: pc(row.netCredit), fontWeight: weight }}>
        {fmtSigned(row.netCredit)}
      </td>
      <td style={{ ...cell, color: row.commRebate === 0 ? THEME.t3 : THEME.teal, fontWeight: weight }}>
        {row.commRebate === 0 ? '0.00' : `+${fmt(row.commRebate)}`}
      </td>
      <td style={{ ...cell, color: row.spreadRebate === 0 ? THEME.t3 : THEME.teal, fontWeight: weight }}>
        {row.spreadRebate === 0 ? '0.00' : `+${fmt(row.spreadRebate)}`}
      </td>
      <td style={{ ...cell, color: pc(row.adjustment), fontWeight: weight }}>
        {fmtSigned(row.adjustment)}
      </td>
      <td style={{ ...cell, color: row.profitShare === 0 ? THEME.t3 : THEME.teal, fontWeight: weight }}>
        {row.profitShare === 0 ? '0.00' : `+${fmt(row.profitShare)}`}
      </td>
      <td style={{ ...cell, color: THEME.t2, fontWeight: weight }}>{fmt(row.supposedEquity)}</td>
      <td style={{ ...cell, color: THEME.t1, fontWeight: weight }}>
        {fmt(row.currentEquity)}
        {row.currentIsLive && (
          <span
            title="Live equity from MT5 (range ends today)."
            style={{ marginLeft: 4, color: THEME.green, fontSize: 9 }}
          >
            {'\u25CF'}
          </span>
        )}
      </td>
      <td style={{ ...cell, color: pc(row.pl), fontWeight: weight + 100 > 800 ? 800 : weight + 100 }}>
        {fmtSigned(row.pl)}
      </td>
      <td style={{ ...cell, color: pc(row.netPl), fontWeight: 700 }}>{fmtSigned(row.netPl)}</td>
    </tr>
  );
}

function SectionHeader({ label, color }: { label: string; color: string }) {
  return (
    <tr style={{ background: THEME.bg2, borderTop: `2px solid ${THEME.border}` }}>
      <td
        colSpan={13}
        style={{
          padding: '6px 10px',
          color,
          fontSize: 10,
          fontWeight: 700,
          letterSpacing: 0.5,
          textTransform: 'uppercase',
        }}
      >
        {label}
      </td>
    </tr>
  );
}

export function EquityPnLPanel() {
  const [from, to] = useDateRange();
  const [data, setData] = useState<EquityPnLResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchData = useCallback(async (ctl?: AbortController) => {
    setLoading(true);
    setError(null);
    try {
      const r = await fetch(`${API}?from=${from}&to=${to}`, { signal: ctl?.signal });
      if (!r.ok) {
        setError(`HTTP ${r.status}`);
        return;
      }
      setData(await r.json());
    } catch (e) {
      if ((e as DOMException)?.name !== 'AbortError') {
        setError(e instanceof Error ? e.message : 'Request failed');
      }
    } finally {
      setLoading(false);
    }
  }, [from, to]);

  useEffect(() => {
    const ctl = new AbortController();
    fetchData(ctl);
    // Silent refresh every 30s — the table pulls live MT5 deal history on each
    // call so we don't want to hammer the endpoint.
    const i = setInterval(() => fetchData(), 30_000);
    return () => { ctl.abort(); clearInterval(i); };
  }, [fetchData]);

  return (
    // `minHeight: 0` lets the inner `overflow: auto` div actually constrain
    // its height — without it the flex column defaults to `min-height: auto`
    // and grows to fit its content, defeating the scroll region and the
    // sticky thead that depends on it.
    <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0 }}>
      <div style={{
        display: 'flex',
        alignItems: 'center',
        gap: 12,
        padding: '10px 16px',
        background: THEME.bg2,
        borderBottom: `1px solid ${THEME.border}`,
      }}>
        <DateRangePicker />
        {loading && (
          <span style={{
            padding: '2px 8px',
            borderRadius: 3,
            background: THEME.badgeAmber,
            color: THEME.amber,
            fontSize: 10,
            fontWeight: 700,
          }}>LOADING…</span>
        )}
        {error && (
          <span style={{ color: THEME.red, fontSize: 12 }}>{error}</span>
        )}
        <div style={{ marginLeft: 'auto', fontSize: 11, color: THEME.t3 }}>
          {data && `${data.clientRows.length} clients · ${data.coverageRows.length} coverage`}
        </div>
      </div>

      <div style={{ flex: 1, overflow: 'auto' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr>
              <th style={hdrLeft}>Login</th>
              <th style={hdrLeft}>Name</th>
              <th style={hdr}>Begin Eq</th>
              <th style={hdr}>Net Dep/W</th>
              <th style={hdr}>Net Cred</th>
              <th style={hdr}>Comm Reb</th>
              <th style={hdr}>Spread Reb</th>
              <th style={hdr}>Adj</th>
              <th style={hdr}>PS</th>
              <th style={hdr}>Supposed Eq</th>
              <th style={hdr}>Current Eq</th>
              <th style={hdr}>PL</th>
              <th style={hdr}>Net PL</th>
            </tr>
          </thead>
          <tbody>
            {data && data.clientRows.length > 0 && (
              <SectionHeader label="Clients (B-Book)" color={THEME.blue} />
            )}
            {data?.clientRows.map(r => <DataRow key={`c-${r.login}`} row={r} />)}
            {data && data.clientRows.length > 0 && data.clientsTotal && (
              <DataRow row={data.clientsTotal} bold />
            )}

            {/* Coverage section — always render the header so the dealer
                knows where the LP numbers would appear, even when no coverage
                accounts are currently synced to trading_accounts. The empty
                state below explains why. */}
            {data && <SectionHeader label="Coverage (LP)" color={THEME.teal} />}
            {data?.coverageRows.map(r => <DataRow key={`cov-${r.login}`} row={r} />)}
            {data && data.coverageRows.length > 0 && data.coverageTotal && (
              <DataRow row={data.coverageTotal} bold />
            )}
            {data && data.coverageRows.length === 0 && (
              <tr>
                <td colSpan={13} style={{
                  padding: '14px 16px',
                  color: THEME.t3,
                  fontSize: 12,
                  fontFamily: 'inherit',
                  background: THEME.rowAlt,
                  borderBottom: `1px solid ${THEME.border}`,
                  lineHeight: 1.5,
                }}>
                  <span style={{ color: THEME.amber, marginRight: 6 }}>{'\u26A0'}</span>
                  <strong>No coverage accounts synced.</strong>{' '}
                  Coverage equity isn't populated via the MT5 Manager API (it lives on the LP server). To see LP
                  rows here, the Python collector needs an <code style={{ color: THEME.t2 }}>/account</code>{' '}
                  endpoint that returns balance/credit/equity for <code style={{ color: THEME.t2 }}>96900</code>{' '}
                  and a sync path that writes them into <code style={{ color: THEME.t2 }}>trading_accounts</code>.
                </td>
              </tr>
            )}

            {data && (
              <tr style={{ background: THEME.bg3, borderTop: `3px solid ${THEME.border}` }}>
                <td colSpan={11} style={{ ...cellLeft, color: THEME.t1, fontWeight: 700, fontSize: 12 }}>
                  BROKER EDGE
                  <span style={{ marginLeft: 8, color: THEME.t3, fontSize: 10, fontWeight: 400 }}>
                    (−Clients.NetPL + Coverage.NetPL)
                  </span>
                </td>
                <td style={{ ...cell, fontWeight: 700 }}></td>
                <td style={{ ...cell, color: pc(data.brokerEdge), fontSize: 13, fontWeight: 800 }}>
                  {fmtSigned(data.brokerEdge)}
                </td>
              </tr>
            )}

            {data && data.clientRows.length === 0 && data.coverageRows.length === 0 && (
              <tr>
                <td colSpan={13} style={{ padding: 32, textAlign: 'center', color: THEME.t3 }}>
                  No accounts in this range — check that <code>trading_accounts</code> is synced.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
