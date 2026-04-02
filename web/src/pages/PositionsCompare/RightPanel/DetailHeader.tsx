import { THEME } from '../../../theme';
import type { SymbolExposure } from '../../../types/compare';

interface DetailHeaderProps {
  data: SymbolExposure;
}

function hedgeColor(pct: number): string {
  if (pct >= 80) return THEME.green;
  if (pct >= 50) return THEME.amber;
  return THEME.red;
}

export function DetailHeader({ data }: DetailHeaderProps) {
  return (
    <div style={{
      height: 38,
      display: 'flex',
      alignItems: 'center',
      padding: '0 16px',
      gap: 16,
      borderBottom: `1px solid ${THEME.border}`,
      background: THEME.bg2,
      flexShrink: 0,
    }}>
      <span style={{ fontFamily: 'monospace', fontWeight: 700, fontSize: 14, color: THEME.teal }}>
        {data.symbol}
      </span>
      <span style={{ fontSize: 11, color: THEME.t3 }}>
        CLI <span style={{ fontFamily: 'monospace', color: THEME.blue, fontWeight: 600 }}>
          {(data.clientBuyVolume + data.clientSellVolume).toFixed(2)}
        </span>
      </span>
      <span style={{ fontSize: 11, color: THEME.t3 }}>
        COV <span style={{ fontFamily: 'monospace', color: THEME.amber, fontWeight: 600 }}>
          {(data.coverageBuyVolume + data.coverageSellVolume).toFixed(2)}
        </span>
      </span>
      <span style={{
        fontFamily: 'monospace', fontSize: 12, fontWeight: 700,
        color: data.netPnl >= 0 ? THEME.green : THEME.red,
      }}>
        {data.netPnl >= 0 ? '+' : ''}{data.netPnl.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
      </span>
      <span style={{
        fontSize: 10,
        fontWeight: 700,
        fontFamily: 'monospace',
        padding: '2px 8px',
        borderRadius: 4,
        background: `${hedgeColor(data.hedgePercent)}22`,
        color: hedgeColor(data.hedgePercent),
      }}>
        {data.hedgePercent.toFixed(0)}%
      </span>
    </div>
  );
}
