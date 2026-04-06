import { THEME } from '../../../theme';
import type { SymbolExposure } from '../../../types/compare';

interface SymbolRowProps {
  data: SymbolExposure;
  isSelected: boolean;
  onClick: () => void;
}

function hedgeColor(pct: number): string {
  if (pct >= 80) return THEME.green;
  if (pct >= 50) return THEME.amber;
  return THEME.red;
}

function nc(v: number): string {
  return v >= 0 ? THEME.blue : THEME.red;
}

function fmtNet(v: number): string {
  if (Math.abs(v) < 0.005) return '0.00';
  return `${v > 0 ? '+' : ''}${v.toFixed(2)}`;
}

function fmtPnl(v: number): string {
  const abs = Math.abs(v);
  const sign = v >= 0 ? '+' : '-';
  if (abs >= 1000) return `${sign}$${(abs / 1000).toFixed(1)}K`;
  return `${sign}$${abs.toFixed(0)}`;
}

const numStyle: React.CSSProperties = {
  fontFamily: 'monospace',
  fontSize: 11,
  fontWeight: 600,
  flex: 1,
  textAlign: 'right',
};

export function SymbolRow({ data, isSelected, onClick }: SymbolRowProps) {
  const netDiff = data.clientNetVolume - data.coverageNetVolume;
  const pnlDiff = data.netPnl;

  return (
    <div
      onClick={onClick}
      style={{
        padding: '6px 10px',
        borderBottom: `1px solid ${THEME.border}`,
        cursor: 'pointer',
        background: isSelected ? 'rgba(20,184,166,0.08)' : 'transparent',
        borderLeft: isSelected ? `2px solid ${THEME.teal}` : '2px solid transparent',
        transition: 'background 0.15s',
      }}
      onMouseEnter={e => { if (!isSelected) e.currentTarget.style.background = `rgba(255,255,255,0.02)`; }}
      onMouseLeave={e => { if (!isSelected) e.currentTarget.style.background = 'transparent'; }}
    >
      {/* Top line: Symbol | Hedge% | Net CLI / COV / Δ */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
        <span style={{ fontFamily: 'monospace', fontWeight: 700, color: THEME.t1, fontSize: 13, minWidth: 80 }}>
          {data.symbol}
        </span>
        <span style={{
          fontSize: 10, fontWeight: 700, fontFamily: 'monospace',
          padding: '1px 6px', borderRadius: 3, whiteSpace: 'nowrap',
          background: `${hedgeColor(data.hedgePercent)}22`,
          color: hedgeColor(data.hedgePercent),
        }}>
          {data.hedgePercent.toFixed(0)}%
        </span>
        <div style={{ width: 1, height: 16, background: THEME.border, flexShrink: 0 }} />
        <span style={{ ...numStyle, color: THEME.t1 }}>{fmtNet(data.clientNetVolume)}</span>
        <span style={{ ...numStyle, color: THEME.t1 }}>{fmtNet(data.coverageNetVolume)}</span>
        <span style={{ ...numStyle, color: nc(netDiff), fontWeight: 700 }}>{fmtNet(netDiff)}</span>
      </div>

      {/* Bottom line: P&L CLI / COV / Δ (aligned under net values) */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 2 }}>
        <span style={{ minWidth: 80 }} />
        <span style={{ fontSize: 10, fontFamily: 'monospace', padding: '1px 6px', visibility: 'hidden' }}>00%</span>
        <div style={{ width: 1, height: 1, flexShrink: 0 }} />
        <span style={{ ...numStyle, color: THEME.t1 }}>{fmtPnl(data.clientPnl)}</span>
        <span style={{ ...numStyle, color: THEME.t1 }}>{fmtPnl(data.coveragePnl)}</span>
        <span style={{ ...numStyle, color: nc(pnlDiff), fontWeight: 700 }}>{fmtPnl(pnlDiff)}</span>
      </div>
    </div>
  );
}
