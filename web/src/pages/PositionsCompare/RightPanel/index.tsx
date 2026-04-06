import { THEME } from '../../../theme';
import type { SymbolExposure, TradeRecord } from '../../../types/compare';
import { DetailHeader } from './DetailHeader';
import { SummaryCards } from './SummaryCards';
import { PriceChart } from './PriceChart';
import { VolPnlChart } from './VolPnlChart';
import { CompareTable } from './CompareTable';
import { LoginsWidget } from './LoginsWidget';

interface RightPanelProps {
  selectedSymbol: string | null;
  symbolData: SymbolExposure | undefined;
  trades: TradeRecord[];
}

export function RightPanel({ selectedSymbol, symbolData, trades }: RightPanelProps) {
  if (!selectedSymbol || !symbolData) {
    return (
      <div style={{
        flex: 1,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        color: THEME.t3,
        fontSize: 13,
        fontWeight: 500,
      }}>
        <div style={{ textAlign: 'center' }}>
          <div style={{ fontSize: 32, marginBottom: 8, opacity: 0.3 }}>{'\u2190'}</div>
          <div>Select a symbol to compare</div>
        </div>
      </div>
    );
  }

  const symbolTrades = trades.filter(t => {
    const tSym = t.symbol.replace(/[-.]$/, '').toUpperCase();
    const sSym = selectedSymbol.replace(/[-.]$/, '').toUpperCase();
    return tSym === sSym;
  });

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      <DetailHeader data={symbolData} />
      <SummaryCards data={symbolData} />

      <div style={{ flex: 1, display: 'flex', overflow: 'auto', padding: '0 16px 16px' }}>
        {/* Left: Charts (70%) */}
        <div style={{ flex: 7, display: 'flex', flexDirection: 'column', gap: 8, minWidth: 0 }}>
          <div style={{
            background: THEME.card,
            borderRadius: 6,
            border: `1px solid ${THEME.border}`,
            padding: 8,
          }}>
            <PriceChart trades={symbolTrades} symbol={selectedSymbol} />
          </div>
          <div style={{
            background: THEME.card,
            borderRadius: 6,
            border: `1px solid ${THEME.border}`,
            padding: 8,
            flex: 1,
            minHeight: 180,
          }}>
            <VolPnlChart trades={symbolTrades} symbol={selectedSymbol} />
          </div>
        </div>

        {/* Right: Compare table + Logins (30%) */}
        <div style={{ flex: 3, marginLeft: 8, minWidth: 200, overflow: 'auto' }}>
          <CompareTable data={symbolData} trades={symbolTrades} />
          <LoginsWidget symbol={selectedSymbol} />
        </div>
      </div>
    </div>
  );
}
