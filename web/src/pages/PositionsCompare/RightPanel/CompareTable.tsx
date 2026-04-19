import { THEME } from '../../../theme';
import type { SymbolExposure } from '../../../types/compare';
import type { TradeRecord } from '../../../types/compare';

interface CompareTableProps {
  data: SymbolExposure;
  trades: TradeRecord[];
}

function pc(v: number): string {
  return v >= 0 ? THEME.green : THEME.red;
}

export function CompareTable({ data, trades }: CompareTableProps) {
  const cliTrades = trades.filter(t => t.side === 'client');
  const covTrades = trades.filter(t => t.side === 'coverage');
  const cliWinRate = data.clientTradeCount > 0 ? (data.clientWins / data.clientTradeCount * 100) : 0;
  const covWinRate = data.coverageTradeCount > 0 ? (data.coverageWins / data.coverageTradeCount * 100) : 0;

  const rows: { metric: string; cli: string; cov: string; delta: string; deltaColor: string }[] = [
    {
      metric: 'Trades',
      cli: `${cliTrades.length}`,
      cov: `${covTrades.length}`,
      delta: `${cliTrades.length - covTrades.length}`,
      deltaColor: THEME.t2,
    },
    {
      metric: 'Volume',
      cli: (data.clientBuyVolume + data.clientSellVolume).toFixed(2),
      cov: (data.coverageBuyVolume + data.coverageSellVolume).toFixed(2),
      delta: ((data.clientBuyVolume + data.clientSellVolume) - (data.coverageBuyVolume + data.coverageSellVolume)).toFixed(2),
      deltaColor: THEME.t2,
    },
    {
      metric: 'Avg Entry',
      cli: data.clientAvgEntryPrice.toFixed(2),
      cov: data.coverageAvgEntryPrice.toFixed(2),
      delta: data.entryPriceDelta.toFixed(2),
      deltaColor: data.entryPriceDelta > 0 ? THEME.red : THEME.green,
    },
    {
      metric: 'Avg Exit',
      cli: data.clientAvgExitPrice.toFixed(2),
      cov: data.coverageAvgExitPrice.toFixed(2),
      delta: data.exitPriceDelta.toFixed(2),
      deltaColor: data.exitPriceDelta > 0 ? THEME.red : THEME.green,
    },
    {
      metric: 'Win Rate',
      cli: `${cliWinRate.toFixed(0)}%`,
      cov: `${covWinRate.toFixed(0)}%`,
      delta: `${(cliWinRate - covWinRate).toFixed(0)}%`,
      deltaColor: THEME.t2,
    },
    {
      metric: 'P&L',
      cli: `${data.clientPnl >= 0 ? '+' : ''}${data.clientPnl.toFixed(2)}`,
      cov: `${data.coveragePnl >= 0 ? '+' : ''}${data.coveragePnl.toFixed(2)}`,
      delta: `${data.netPnl >= 0 ? '+' : ''}${data.netPnl.toFixed(2)}`,
      deltaColor: pc(data.netPnl),
    },
  ];

  const hdr: React.CSSProperties = {
    padding: '5px 8px',
    fontSize: 9,
    fontWeight: 600,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    color: THEME.t3,
    textAlign: 'right',
    borderBottom: `1px solid ${THEME.border}`,
  };

  const cell: React.CSSProperties = {
    padding: '5px 8px',
    fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace",
    fontSize: 11,
    textAlign: 'right',
    borderBottom: `1px solid ${THEME.border}`,
  };

  return (
    <div style={{
      background: THEME.card,
      borderRadius: 6,
      border: `1px solid ${THEME.border}`,
      overflow: 'hidden',
    }}>
      <table style={{ width: '100%', borderCollapse: 'collapse' }}>
        <thead>
          <tr>
            <th style={{ ...hdr, textAlign: 'left' }}>Metric</th>
            <th style={{ ...hdr, color: THEME.blue }}>CLI</th>
            <th style={{ ...hdr, color: THEME.amber }}>COV</th>
            <th style={hdr}>&Delta;</th>
          </tr>
        </thead>
        <tbody>
          {rows.map(r => (
            <tr key={r.metric}>
              <td style={{ ...cell, textAlign: 'left', color: THEME.t2, fontFamily: 'inherit', fontWeight: 600, fontSize: 10 }}>{r.metric}</td>
              <td style={{ ...cell, color: THEME.t1 }}>{r.cli}</td>
              <td style={{ ...cell, color: THEME.t1 }}>{r.cov}</td>
              <td style={{ ...cell, color: r.deltaColor, fontWeight: 600 }}>{r.delta}</td>
            </tr>
          ))}
          {/* Net P&L footer */}
          <tr style={{ background: THEME.rowAlt }}>
            <td style={{ ...cell, textAlign: 'left', fontWeight: 700, fontSize: 10, color: THEME.t1, fontFamily: 'inherit', borderBottom: 'none' }}>Net P&L</td>
            <td colSpan={2} style={{ ...cell, textAlign: 'center', fontWeight: 700, color: pc(data.netPnl), borderBottom: 'none' }}>
              {data.netPnl >= 0 ? '+' : '\u2212'}{Math.abs(data.netPnl).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
            </td>
            <td style={{ ...cell, borderBottom: 'none' }}></td>
          </tr>
        </tbody>
      </table>
    </div>
  );
}
