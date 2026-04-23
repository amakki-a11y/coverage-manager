import { THEME } from '../../../theme';
import type { SymbolExposure } from '../../../types/compare';

interface SummaryCardsProps {
  data: SymbolExposure;
}

function Card({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div style={{
      flex: 1,
      background: THEME.card,
      borderRadius: 6,
      padding: '10px 12px',
      border: `1px solid ${THEME.border}`,
      minWidth: 0,
    }}>
      <div style={{ fontSize: 9, fontWeight: 600, color: THEME.t3, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 6 }}>
        {title}
      </div>
      {children}
    </div>
  );
}

function Row({ label, value, color }: { label: string; value: string; color: string }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 2 }}>
      <span style={{ fontSize: 10, color: THEME.t3 }}>{label}</span>
      <span style={{ fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace", fontSize: 11, fontWeight: 600, color }}>{value}</span>
    </div>
  );
}

export function SummaryCards({ data }: SummaryCardsProps) {
  const entryDelta = data.entryPriceDelta;
  const cliWinRate = data.clientTradeCount > 0 ? (data.clientWins / data.clientTradeCount * 100) : 0;

  return (
    <div style={{ display: 'flex', gap: 8, padding: '8px 16px', flexShrink: 0 }}>
      {/* Avg Entry */}
      <Card title="Avg Entry">
        <Row label="CLI" value={data.clientAvgEntryPrice.toFixed(2)} color={THEME.blue} />
        <Row label="COV" value={data.coverageAvgEntryPrice.toFixed(2)} color={THEME.amber} />
        <div style={{ marginTop: 4, borderTop: `1px solid ${THEME.border}`, paddingTop: 4 }}>
          <Row label={'\u0394'} value={entryDelta.toFixed(2)} color={entryDelta > 0 ? THEME.red : THEME.green} />
        </div>
      </Card>

      {/* Avg Exit */}
      <Card title="Avg Exit">
        <Row label="CLI" value={data.clientAvgExitPrice.toFixed(2)} color={THEME.blue} />
        <Row label="COV" value={data.coverageAvgExitPrice.toFixed(2)} color={THEME.amber} />
      </Card>

      {/* Volume */}
      <Card title="Volume">
        <Row label="BUY" value={`${data.clientBuyVolume.toFixed(2)} / ${data.coverageBuyVolume.toFixed(2)}`} color={THEME.green} />
        <Row label="SELL" value={`${data.clientSellVolume.toFixed(2)} / ${data.coverageSellVolume.toFixed(2)}`} color={THEME.red} />
      </Card>

      {/* P&L */}
      <Card title="P&L">
        <Row label="CLI" value={`${data.clientPnl >= 0 ? '+' : ''}${data.clientPnl.toFixed(2)}`} color={data.clientPnl >= 0 ? THEME.green : THEME.red} />
        <Row label="COV" value={`${data.coveragePnl >= 0 ? '+' : ''}${data.coveragePnl.toFixed(2)}`} color={data.coveragePnl >= 0 ? THEME.green : THEME.red} />
      </Card>

      {/* Net Combined */}
      <Card title="Net Combined">
        <Row label="Net P&L" value={`${data.netPnl >= 0 ? '+' : ''}${data.netPnl.toFixed(2)}`} color={data.netPnl >= 0 ? THEME.green : THEME.red} />
        <Row label="CLI W/L" value={`${data.clientWins}/${data.clientTradeCount - data.clientWins}`} color={THEME.t2} />
        <Row label="CLI Win%" value={`${cliWinRate.toFixed(0)}%`} color={cliWinRate >= 50 ? THEME.green : THEME.red} />
      </Card>
    </div>
  );
}
