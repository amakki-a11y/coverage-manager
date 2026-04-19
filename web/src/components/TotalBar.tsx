import { THEME } from '../theme';
import type { ExposureSummary } from '../types';

interface TotalBarProps {
  summaries: ExposureSummary[];
  connected: boolean;
}

export function TotalBar({ summaries, connected }: TotalBarProps) {
  const totalBBookPnL = summaries.reduce((s, e) => s + (e.bBookPnL ?? 0), 0);
  const totalCoveragePnL = summaries.reduce((s, e) => s + (e.coveragePnL ?? 0), 0);
  const totalNetPnL = -totalBBookPnL + totalCoveragePnL;
  const totalUnhedged = summaries.reduce((s, e) => s + Math.abs(e.netVolume ?? 0), 0);

  return (
    <div style={{
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      padding: '12px 24px',
      background: THEME.bg2,
      borderBottom: `1px solid ${THEME.border}`,
    }}>
      <div style={{ display: 'flex', gap: 32, alignItems: 'center' }}>
        <span style={{ color: THEME.t2, fontSize: 14, fontWeight: 600, letterSpacing: 1 }}>
          COVERAGE MANAGER
        </span>
      </div>
      <div style={{ display: 'flex', gap: 32 }}>
        <Metric label="B-Book P&L" value={totalBBookPnL} />
        <Metric label="Coverage P&L" value={totalCoveragePnL} />
        <Metric label="Net P&L" value={totalNetPnL} highlight />
        <div style={{ borderLeft: `1px solid ${THEME.border}`, margin: '0 8px' }} />
        <div>
          <div style={{ color: THEME.t3, fontSize: 10, textTransform: 'uppercase' }}>Unhedged</div>
          <div style={{ color: THEME.amber, fontSize: 16, fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace", fontWeight: 600 }}>
            {totalUnhedged.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
          </div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <div style={{
            width: 8, height: 8, borderRadius: '50%',
            background: connected ? THEME.green : THEME.red,
          }} />
          <span style={{ color: THEME.t3, fontSize: 11 }}>
            {connected ? 'LIVE' : 'DISCONNECTED'}
          </span>
        </div>
      </div>
    </div>
  );
}

function Metric({ label, value, highlight }: { label: string; value: number; highlight?: boolean }) {
  const color = value > 0 ? THEME.green : value < 0 ? THEME.red : THEME.t2;
  return (
    <div>
      <div style={{ color: THEME.t3, fontSize: 10, textTransform: 'uppercase' }}>{label}</div>
      <div style={{
        color: highlight ? color : color,
        fontSize: 16,
        fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace",
        fontWeight: highlight ? 700 : 600,
      }}>
        {value >= 0 ? '+' : ''}{value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
      </div>
    </div>
  );
}
