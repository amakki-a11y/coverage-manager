import { THEME } from '../theme';
import type { ExposureSummary } from '../types';

interface ExposureTableProps {
  summaries: ExposureSummary[];
}

const cellStyle: React.CSSProperties = {
  padding: '6px 10px',
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

export function ExposureTable({ summaries }: ExposureTableProps) {
  // Totals
  const totals = summaries.reduce(
    (acc, s) => ({
      bbookBuy: acc.bbookBuy + s.bbookBuyVolume,
      bbookSell: acc.bbookSell + s.bbookSellVolume,
      bbookNet: acc.bbookNet + s.bbookNetVolume,
      bbookPnL: acc.bbookPnL + s.bbookPnL,
      covBuy: acc.covBuy + s.coverageBuyVolume,
      covSell: acc.covSell + s.coverageSellVolume,
      covNet: acc.covNet + s.coverageNetVolume,
      covPnL: acc.covPnL + s.coveragePnL,
      netVol: acc.netVol + s.netVolume,
      netPnL: acc.netPnL + s.netPnL,
    }),
    { bbookBuy: 0, bbookSell: 0, bbookNet: 0, bbookPnL: 0, covBuy: 0, covSell: 0, covNet: 0, covPnL: 0, netVol: 0, netPnL: 0 }
  );

  return (
    <div style={{ overflow: 'auto', flex: 1 }}>
      <table style={{ width: '100%', borderCollapse: 'collapse', tableLayout: 'fixed' }}>
        <thead>
          <tr>
            <th style={{ ...headerStyle, textAlign: 'left', width: 100 }}>Symbol</th>
            <th style={headerStyle}>BB Buy</th>
            <th style={headerStyle}>BB Sell</th>
            <th style={headerStyle}>BB Net</th>
            <th style={headerStyle}>BB P&L</th>
            <th style={{ ...headerStyle, borderLeft: `1px solid ${THEME.border}` }}>Cov Buy</th>
            <th style={headerStyle}>Cov Sell</th>
            <th style={headerStyle}>Cov Net</th>
            <th style={headerStyle}>Cov P&L</th>
            <th style={{ ...headerStyle, borderLeft: `1px solid ${THEME.border}` }}>Net Exp</th>
            <th style={headerStyle}>Net P&L</th>
            <th style={headerStyle}>Hedge %</th>
          </tr>
        </thead>
        <tbody>
          {summaries.map((s) => {
            const hedgeBg = s.hedgeRatio < 20 ? 'rgba(255,82,82,0.12)'
              : s.hedgeRatio < 50 ? 'rgba(255,186,66,0.08)'
              : 'transparent';
            return (
              <tr key={s.canonicalSymbol} style={{ background: hedgeBg, borderBottom: `1px solid ${THEME.border}` }}>
                <td style={{ ...cellStyle, textAlign: 'left', color: THEME.t1, fontWeight: 600, fontFamily: 'inherit' }}>
                  {s.canonicalSymbol}
                </td>
                <td style={{ ...cellStyle, color: THEME.green }}>{fmt(s.bbookBuyVolume)}</td>
                <td style={{ ...cellStyle, color: THEME.red }}>{fmt(s.bbookSellVolume)}</td>
                <td style={{ ...cellStyle, color: pnlColor(s.bbookNetVolume) }}>{fmt(s.bbookNetVolume)}</td>
                <td style={{ ...cellStyle, color: pnlColor(s.bbookPnL) }}>{fmtPnl(s.bbookPnL)}</td>
                <td style={{ ...cellStyle, color: THEME.green, borderLeft: `1px solid ${THEME.border}` }}>{fmt(s.coverageBuyVolume)}</td>
                <td style={{ ...cellStyle, color: THEME.red }}>{fmt(s.coverageSellVolume)}</td>
                <td style={{ ...cellStyle, color: pnlColor(s.coverageNetVolume) }}>{fmt(s.coverageNetVolume)}</td>
                <td style={{ ...cellStyle, color: pnlColor(s.coveragePnL) }}>{fmtPnl(s.coveragePnL)}</td>
                <td style={{ ...cellStyle, borderLeft: `1px solid ${THEME.border}`, color: pnlColor(s.netVolume), fontWeight: 700 }}>
                  {fmt(s.netVolume)}
                </td>
                <td style={{ ...cellStyle, color: pnlColor(s.netPnL), fontWeight: 700 }}>{fmtPnl(s.netPnL)}</td>
                <td style={{ ...cellStyle, color: hedgeColor(s.hedgeRatio) }}>{s.hedgeRatio.toFixed(0)}%</td>
              </tr>
            );
          })}
        </tbody>
        <tfoot>
          <tr style={{ borderTop: `2px solid ${THEME.border}`, background: THEME.bg3 }}>
            <td style={{ ...cellStyle, textAlign: 'left', color: THEME.t2, fontWeight: 700, fontFamily: 'inherit' }}>TOTAL</td>
            <td style={{ ...cellStyle, color: THEME.green }}>{fmt(totals.bbookBuy)}</td>
            <td style={{ ...cellStyle, color: THEME.red }}>{fmt(totals.bbookSell)}</td>
            <td style={{ ...cellStyle, color: pnlColor(totals.bbookNet) }}>{fmt(totals.bbookNet)}</td>
            <td style={{ ...cellStyle, color: pnlColor(totals.bbookPnL) }}>{fmtPnl(totals.bbookPnL)}</td>
            <td style={{ ...cellStyle, color: THEME.green, borderLeft: `1px solid ${THEME.border}` }}>{fmt(totals.covBuy)}</td>
            <td style={{ ...cellStyle, color: THEME.red }}>{fmt(totals.covSell)}</td>
            <td style={{ ...cellStyle, color: pnlColor(totals.covNet) }}>{fmt(totals.covNet)}</td>
            <td style={{ ...cellStyle, color: pnlColor(totals.covPnL) }}>{fmtPnl(totals.covPnL)}</td>
            <td style={{ ...cellStyle, borderLeft: `1px solid ${THEME.border}`, color: pnlColor(totals.netVol), fontWeight: 700 }}>{fmt(totals.netVol)}</td>
            <td style={{ ...cellStyle, color: pnlColor(totals.netPnL), fontWeight: 700 }}>{fmtPnl(totals.netPnL)}</td>
            <td style={cellStyle} />
          </tr>
        </tfoot>
      </table>
    </div>
  );
}

function fmt(v: number): string {
  return v.toFixed(2);
}

function fmtPnl(v: number): string {
  return (v >= 0 ? '+' : '') + v.toFixed(2);
}

function pnlColor(v: number): string {
  return v > 0 ? THEME.green : v < 0 ? THEME.red : THEME.t2;
}

function hedgeColor(ratio: number): string {
  if (ratio >= 80) return THEME.green;
  if (ratio >= 50) return THEME.amber;
  return THEME.red;
}
