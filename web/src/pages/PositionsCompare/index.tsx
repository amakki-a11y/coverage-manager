import { THEME } from '../../theme';
import { usePositionsCompare } from '../../hooks/usePositionsCompare';
import { LeftPanel } from './LeftPanel';
import { RightPanel } from './RightPanel';
import type { PriceQuote } from '../../types';

export function PositionsCompare({ prices = [] }: { prices?: PriceQuote[] }) {
  const {
    symbols,
    trades,
    selectedSymbol,
    setSelectedSymbol,
    isConnected,
    lastUpdated,
  } = usePositionsCompare();

  const symbolData = selectedSymbol
    ? symbols.find(s => s.symbol === selectedSymbol)
    : undefined;

  return (
    <div style={{
      flex: 1,
      display: 'flex',
      position: 'relative',
      overflow: 'hidden',
      background: THEME.bg,
    }}>
      <LeftPanel
        symbols={symbols}
        selectedSymbol={selectedSymbol}
        onSelect={setSelectedSymbol}
        prices={prices}
      />
      <RightPanel
        selectedSymbol={selectedSymbol}
        symbolData={symbolData}
        trades={trades}
      />
      {/* Connection indicator */}
      <div style={{
        position: 'absolute',
        bottom: 8,
        right: 12,
        fontSize: 9,
        color: THEME.t3,
        fontFamily: 'monospace',
        display: 'flex',
        alignItems: 'center',
        gap: 4,
      }}>
        <div style={{
          width: 6,
          height: 6,
          borderRadius: '50%',
          background: isConnected ? THEME.green : THEME.red,
        }} />
        {lastUpdated.toLocaleTimeString()}
      </div>
    </div>
  );
}
