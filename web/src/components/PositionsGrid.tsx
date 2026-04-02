import { THEME } from '../theme';
import type { Position } from '../types';

interface PositionsGridProps {
  positions: Position[];
}

const cellStyle: React.CSSProperties = {
  padding: '5px 10px',
  fontFamily: 'monospace',
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

export function PositionsGrid({ positions }: PositionsGridProps) {
  const sorted = [...positions].sort((a, b) => {
    if (a.canonicalSymbol !== b.canonicalSymbol) return a.canonicalSymbol.localeCompare(b.canonicalSymbol);
    return a.source.localeCompare(b.source);
  });

  return (
    <div style={{ overflow: 'auto', flex: 1 }}>
      <table style={{ width: '100%', borderCollapse: 'collapse' }}>
        <thead>
          <tr>
            <th style={{ ...headerStyle, textAlign: 'left' }}>Source</th>
            <th style={{ ...headerStyle, textAlign: 'left' }}>Login</th>
            <th style={{ ...headerStyle, textAlign: 'left' }}>Symbol</th>
            <th style={{ ...headerStyle, textAlign: 'left' }}>Dir</th>
            <th style={headerStyle}>Volume</th>
            <th style={headerStyle}>Open Price</th>
            <th style={headerStyle}>Current</th>
            <th style={headerStyle}>P&L</th>
            <th style={headerStyle}>Swap</th>
            <th style={headerStyle}>Open Time</th>
          </tr>
        </thead>
        <tbody>
          {sorted.map((p, i) => (
            <tr key={`${p.source}-${p.login}-${p.symbol}-${i}`} style={{ borderBottom: `1px solid ${THEME.border}` }}>
              <td style={{ ...cellStyle, textAlign: 'left' }}>
                <span style={{
                  display: 'inline-block',
                  padding: '2px 8px',
                  borderRadius: 3,
                  fontSize: 10,
                  fontWeight: 600,
                  fontFamily: 'inherit',
                  background: p.source === 'bbook' ? 'rgba(91,158,255,0.15)' : 'rgba(255,82,82,0.15)',
                  color: p.source === 'bbook' ? THEME.blue : '#FF8A80',
                }}>
                  {p.source === 'bbook' ? 'B-BOOK' : 'COVERAGE'}
                </span>
              </td>
              <td style={{ ...cellStyle, textAlign: 'left', color: THEME.t2 }}>{p.login || '—'}</td>
              <td style={{ ...cellStyle, textAlign: 'left', color: THEME.t1 }}>{p.symbol}</td>
              <td style={{
                ...cellStyle, textAlign: 'left',
                color: p.direction === 'BUY' ? THEME.green : THEME.red,
                fontWeight: 600,
              }}>
                {p.direction}
              </td>
              <td style={{ ...cellStyle, color: THEME.t1 }}>{p.volumeLots.toFixed(2)}</td>
              <td style={{ ...cellStyle, color: THEME.t2 }}>{p.openPrice.toFixed(5)}</td>
              <td style={{ ...cellStyle, color: THEME.t1 }}>{p.currentPrice.toFixed(5)}</td>
              <td style={{ ...cellStyle, color: p.profit >= 0 ? THEME.green : THEME.red, fontWeight: 600 }}>
                {p.profit >= 0 ? '+' : ''}{p.profit.toFixed(2)}
              </td>
              <td style={{ ...cellStyle, color: THEME.t3 }}>{p.swap.toFixed(2)}</td>
              <td style={{ ...cellStyle, color: THEME.t2, fontSize: 11 }}>
                {p.openTime ? new Date(p.openTime).toLocaleString('en-GB', { day: '2-digit', month: '2-digit', year: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' }) : '—'}
              </td>
            </tr>
          ))}
          {sorted.length === 0 && (
            <tr>
              <td colSpan={10} style={{ ...cellStyle, textAlign: 'center', color: THEME.t3, padding: 40 }}>
                No positions — waiting for data...
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
