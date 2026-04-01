import { useState, useEffect } from 'react';
import { THEME } from './theme';
import { useExposureSocket } from './hooks/useExposureSocket';
import { TotalBar } from './components/TotalBar';
import { ExposureTable } from './components/ExposureTable';
import { PositionsGrid } from './components/PositionsGrid';
import { SymbolMappingAdmin } from './components/SymbolMappingAdmin';
import { SettingsPanel } from './components/SettingsPanel';
import { PnLPanel } from './components/PnLPanel';
import type { Position } from './types';

type Tab = 'exposure' | 'positions' | 'pnl' | 'mappings' | 'settings';

const tabStyle = (active: boolean): React.CSSProperties => ({
  padding: '8px 20px',
  background: active ? THEME.bg3 : 'transparent',
  color: active ? THEME.t1 : THEME.t3,
  border: 'none',
  borderBottom: active ? `2px solid ${THEME.blue}` : '2px solid transparent',
  cursor: 'pointer',
  fontSize: 13,
  fontWeight: 600,
  transition: 'all 0.15s',
});

function App() {
  const [tab, setTab] = useState<Tab>('exposure');
  const { exposureSummaries, connected } = useExposureSocket();
  const [positions, setPositions] = useState<Position[]>([]);

  // Fetch positions for the positions tab
  useEffect(() => {
    if (tab !== 'positions') return;
    const fetchPositions = async () => {
      try {
        const res = await fetch('http://localhost:5000/api/exposure/positions');
        if (res.ok) setPositions(await res.json());
      } catch { /* ignore */ }
    };
    fetchPositions();
    const interval = setInterval(fetchPositions, 1000);
    return () => clearInterval(interval);
  }, [tab]);

  return (
    <div style={{
      display: 'flex',
      flexDirection: 'column',
      height: '100vh',
      background: THEME.bg,
      color: THEME.t1,
      fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif',
    }}>
      <TotalBar summaries={exposureSummaries} connected={connected} />

      <div style={{
        display: 'flex',
        borderBottom: `1px solid ${THEME.border}`,
        background: THEME.bg2,
      }}>
        <button style={tabStyle(tab === 'exposure')} onClick={() => setTab('exposure')}>Exposure</button>
        <button style={tabStyle(tab === 'positions')} onClick={() => setTab('positions')}>Positions</button>
        <button style={tabStyle(tab === 'pnl')} onClick={() => setTab('pnl')}>P&L</button>
        <button style={tabStyle(tab === 'mappings')} onClick={() => setTab('mappings')}>Mappings</button>
        <button style={tabStyle(tab === 'settings')} onClick={() => setTab('settings')}>Settings</button>
      </div>

      <div style={{ flex: 1, overflow: 'auto', display: 'flex', flexDirection: 'column' }}>
        {tab === 'exposure' && <ExposureTable summaries={exposureSummaries} />}
        {tab === 'positions' && <PositionsGrid positions={positions} />}
        {tab === 'pnl' && <PnLPanel />}
        {tab === 'mappings' && <SymbolMappingAdmin />}
        {tab === 'settings' && <SettingsPanel />}
      </div>
    </div>
  );
}

export default App;
