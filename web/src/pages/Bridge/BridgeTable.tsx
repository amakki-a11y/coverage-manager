import React from 'react';
import { THEME } from '../../theme';
import type { ExecutionPair } from '../../types/bridge';

interface Props {
  pairs: ExecutionPair[];
  pipThresholdForAnomaly: number;
}

function fmtTimeHms(iso: string): string {
  try {
    // iso is UTC timestamptz. Render UTC HH:MM:SS.mmm to match spec.
    const d = new Date(iso);
    const h = String(d.getUTCHours()).padStart(2, '0');
    const m = String(d.getUTCMinutes()).padStart(2, '0');
    const s = String(d.getUTCSeconds()).padStart(2, '0');
    const ms = String(d.getUTCMilliseconds()).padStart(3, '0');
    return `${h}:${m}:${s}.${ms}`;
  } catch {
    return iso;
  }
}

function timeDiffColor(ms: number): string {
  const a = Math.abs(ms);
  if (a > 2000) return THEME.red;
  if (a > 500) return THEME.amber;
  return THEME.green;
}

function edgeColor(edge: number): string {
  if (edge > 0) return THEME.green;
  if (edge < 0) return THEME.red;
  return THEME.t3;
}

function formatPrice(price: number): string {
  // Preserve reasonable precision — bridge emits 5-digit FX and 2-digit metals/indices.
  // Using toFixed(5) across the board keeps column alignment clean per reference screenshot.
  return price.toFixed(5);
}

function formatSignedFixed(v: number, digits: number): string {
  const s = v >= 0 ? '+' : '';
  return `${s}${v.toFixed(digits)}`;
}

const thStyle: React.CSSProperties = {
  padding: '8px 10px',
  textAlign: 'right',
  fontSize: 11,
  color: THEME.t2,
  fontWeight: 600,
  textTransform: 'uppercase',
  letterSpacing: 0.3,
};

const thStyleLeft: React.CSSProperties = { ...thStyle, textAlign: 'left' };

const tdStyle: React.CSSProperties = {
  padding: '6px 10px',
  fontFamily: 'monospace',
  textAlign: 'right',
  color: THEME.t1,
  fontSize: 12,
};

const tdStyleLeft: React.CSSProperties = { ...tdStyle, textAlign: 'left' };

export function BridgeTable({ pairs, pipThresholdForAnomaly }: Props) {
  return (
    <div style={{
      background: THEME.card,
      borderRadius: 8,
      border: `1px solid ${THEME.border}`,
      overflow: 'auto',
    }}>
      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
        <thead>
          <tr style={{ background: THEME.bg3, borderBottom: `1px solid ${THEME.border}` }}>
            <th style={thStyleLeft}>Symbol</th>
            <th style={thStyleLeft}>Source</th>
            <th style={thStyleLeft}>Login</th>
            <th style={thStyleLeft}>Deal #</th>
            <th style={thStyle}>Side</th>
            <th style={thStyle}>Volume</th>
            <th style={thStyle}>Price</th>
            <th style={thStyle}>Time</th>
            <th style={thStyle}>Time Diff</th>
            <th style={thStyle}>Price Edge</th>
            <th style={thStyle}>Pips</th>
          </tr>
        </thead>
        <tbody>
          {pairs.map((p) => {
            const isAnomaly =
              p.covFills.length === 0 ||
              Math.abs(p.pips) > pipThresholdForAnomaly;
            const bgTint = isAnomaly ? 'rgba(255, 82, 82, 0.06)' : 'transparent';
            const spanRows = 1 + p.covFills.length;

            return (
              <React.Fragment key={p.clientDealId}>
                {/* CLIENT row */}
                <tr style={{
                  background: bgTint,
                  borderTop: `2px solid ${THEME.border}`,
                }}>
                  <td
                    rowSpan={spanRows}
                    style={{
                      ...tdStyleLeft,
                      fontWeight: 700,
                      verticalAlign: 'top',
                      borderRight: `1px solid ${THEME.border}`,
                    }}
                  >
                    {p.symbol}
                  </td>
                  <td style={{ ...tdStyleLeft, color: THEME.blue, fontWeight: 700, fontSize: 11 }}>CLIENT</td>
                  <td style={{ ...tdStyleLeft, fontSize: 11, color: THEME.t2 }}>
                    {p.clientMtLogin ?? '\u2014'}
                  </td>
                  <td
                    style={{ ...tdStyleLeft, fontSize: 11, color: THEME.t2 }}
                    title={[
                      `cen_ord_id: ${p.cenOrdId}`,
                      p.clientMtTicket ? `MT5 ticket: ${p.clientMtTicket}` : null,
                      p.clientMtDealId ? `MT5 deal: ${p.clientMtDealId}` : null,
                    ].filter(Boolean).join(' \u00b7 ')}
                  >
                    {p.clientMtDealId ?? p.clientMtTicket ?? p.cenOrdId}
                  </td>
                  <td style={{ ...tdStyle, color: p.side === 'BUY' ? THEME.green : THEME.red, fontWeight: 600 }}>
                    {p.side}
                  </td>
                  <td style={tdStyle}>{p.clientVolume.toFixed(2)}</td>
                  <td style={{ ...tdStyle, fontWeight: 600 }}>{formatPrice(p.clientPrice)}</td>
                  <td style={{ ...tdStyle, fontSize: 11 }}>{fmtTimeHms(p.clientTimeUtc)}</td>
                  <td style={tdStyle}></td>
                  <td
                    rowSpan={spanRows}
                    style={{
                      ...tdStyle,
                      color: edgeColor(p.priceEdge),
                      fontWeight: 700,
                      verticalAlign: 'top',
                      borderLeft: `1px solid ${THEME.border}`,
                    }}
                  >
                    {p.covFills.length === 0 ? '—' : formatSignedFixed(p.priceEdge, 5)}
                  </td>
                  <td
                    rowSpan={spanRows}
                    style={{
                      ...tdStyle,
                      color: edgeColor(p.priceEdge),
                      fontWeight: 700,
                      verticalAlign: 'top',
                    }}
                  >
                    {p.covFills.length === 0 ? '—' : formatSignedFixed(p.pips, 1)}
                  </td>
                </tr>
                {/* COV OUT rows — one per coverage fill */}
                {p.covFills.length === 0 ? (
                  <tr style={{ background: bgTint }}>
                    <td style={{ ...tdStyleLeft, color: THEME.amber, fontWeight: 700, fontSize: 11 }}>COV OUT</td>
                    <td colSpan={7} style={{ ...tdStyleLeft, color: THEME.t3, fontStyle: 'italic' }}>
                      No coverage legs within pairing window
                    </td>
                  </tr>
                ) : (
                  p.covFills.map((c) => {
                    const covDealNo = c.mtDealId ?? c.makerOrderId ?? c.mtTicket ?? '\u2014';
                    const covTitle = [
                      c.mtDealId ? `MT5 deal: ${c.mtDealId}` : null,
                      c.makerOrderId ? `LP order: ${c.makerOrderId}` : null,
                      c.mtTicket ? `MT5 ticket: ${c.mtTicket}` : null,
                      c.lpName ? `LP: ${c.lpName}` : null,
                    ].filter(Boolean).join(' \u00b7 ');
                    return (
                      <tr key={c.dealId} style={{ background: bgTint }}>
                        <td style={{ ...tdStyleLeft, color: THEME.teal, fontWeight: 700, fontSize: 11 }}>COV OUT</td>
                        <td style={{ ...tdStyleLeft, fontSize: 11, color: THEME.t3 }} title={c.lpName ? `LP: ${c.lpName}` : undefined}>
                          {c.lpName ?? '\u2014'}
                        </td>
                        <td style={{ ...tdStyleLeft, fontSize: 11, color: THEME.t2 }} title={covTitle}>
                          {covDealNo}
                        </td>
                        <td style={{ ...tdStyle, color: p.side === 'BUY' ? THEME.green : THEME.red, fontWeight: 600 }}>
                          {p.side}
                        </td>
                        <td style={tdStyle}>{c.volume.toFixed(2)}</td>
                        <td style={{ ...tdStyle, fontWeight: 600 }}>{formatPrice(c.price)}</td>
                        <td style={{ ...tdStyle, fontSize: 11 }}>{fmtTimeHms(c.timeUtc)}</td>
                        <td style={{ ...tdStyle, fontSize: 11, color: timeDiffColor(c.timeDiffMs) }}>
                          {c.timeDiffMs >= 0 ? '+' : ''}{c.timeDiffMs}ms
                        </td>
                      </tr>
                    );
                  })
                )}
              </React.Fragment>
            );
          })}
          {pairs.length === 0 && (
            <tr>
              <td colSpan={11} style={{ padding: 32, textAlign: 'center', color: THEME.t3 }}>
                No execution pairs for the selected filter
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
