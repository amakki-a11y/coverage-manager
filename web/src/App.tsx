import { useState, useEffect, useCallback } from 'react';
import { ThemeProvider, useTheme } from './ThemeContext';
import { useExposureSocket } from './hooks/useExposureSocket';
import { TotalBar } from './components/TotalBar';
import { ExposureTable } from './components/ExposureTable';
import { PositionsGrid } from './components/PositionsGrid';
import { SymbolMappingAdmin } from './components/SymbolMappingAdmin';
import { SettingsPanel } from './components/SettingsPanel';
import { PnLPanel } from './components/PnLPanel';
import { PeriodPnLPanel } from './components/PeriodPnLPanel';
import { EquityPnLPanel } from './components/EquityPnLPanel';
import { PositionsCompare } from './pages/PositionsCompare';
import { MarkupPanel } from './pages/Markup';
import { BridgePanel } from './pages/Bridge';
import { AlertToast } from './components/AlertToast';
import { AlertBanner } from './components/AlertBanner';
import { AlertHistory } from './components/AlertHistory';
import { ConnectionHealthDots } from './components/ConnectionHealthDots';
import { RiskBanner } from './components/RiskBanner';
import { ErrorToastProvider } from './components/ErrorToast';
import { StaleWrapper } from './components/Skeleton';
import { KeyboardShortcutsOverlay } from './components/KeyboardShortcutsOverlay';
import { UserGuideOverlay } from './components/UserGuideOverlay';
import type { Position } from './types';

/**
 * Root shell. Owns the top bar (TotalBar + RiskBanner + connection dots +
 * alert bell + theme toggle), the tab strip, and the currently-active panel.
 *
 * Tab registration lives here: add a new entry to the `Tab` union, import the
 * panel component, add a `<button>` in the tab strip, and render it inside the
 * `<StaleWrapper>`. Every tab is lazy only in the sense that it receives an
 * empty / default state until it mounts — the WebSocket feed is always on
 * regardless of which tab is visible so returning to a tab is instant.
 *
 * Providers: `ThemeProvider` (dark/light + localStorage) wraps
 * `ErrorToastProvider` (global throttled error toast).
 */
type Tab = 'exposure' | 'positions' | 'pnl' | 'netpnl' | 'equitypnl' | 'compare' | 'markup' | 'bridge' | 'mappings' | 'settings';

function AppContent() {
  const { theme, mode, toggleTheme } = useTheme();
  const [tab, setTab] = useState<Tab>('exposure');
  const { exposureSummaries, prices, connected, newAlerts, alertCount } = useExposureSocket();
  const [positions, setPositions] = useState<Position[]>([]);
  const [showAlertHistory, setShowAlertHistory] = useState(false);
  const [showUserGuide, setShowUserGuide] = useState(false);
  const [soundEnabled, setSoundEnabled] = useState(() => {
    const saved = localStorage.getItem('alertSound');
    return saved !== 'false';
  });

  const acknowledgeAlert = useCallback(async (id: string) => {
    try {
      await fetch(`http://localhost:5000/api/alerts/${id}/acknowledge`, { method: 'POST' });
    } catch { /* ignore */ }
  }, []);

  const tabStyle = (active: boolean): React.CSSProperties => ({
    padding: '8px 20px',
    background: active ? theme.bg3 : 'transparent',
    color: active ? theme.t1 : theme.t3,
    border: 'none',
    borderBottom: active ? `2px solid ${theme.blue}` : '2px solid transparent',
    cursor: 'pointer',
    fontSize: 13,
    fontWeight: 600,
    transition: 'all 0.15s',
  });

  // Fetch positions for the positions tab.
  // 5s cadence (was 1s — overkill for a view that doesn't need sub-second refresh),
  // in-flight guard to prevent overlap, pause when tab hidden via visibilitychange.
  useEffect(() => {
    if (tab !== 'positions') return;
    let cancelled = false;
    let inFlight = false;
    const fetchPositions = async () => {
      if (inFlight || document.hidden) return;
      inFlight = true;
      try {
        const res = await fetch('http://localhost:5000/api/exposure/positions');
        if (!cancelled && res.ok) setPositions(await res.json());
      } catch { /* ignore */ }
      inFlight = false;
    };
    fetchPositions();
    const interval = setInterval(fetchPositions, 5000);
    const onVisible = () => { if (!document.hidden) fetchPositions(); };
    document.addEventListener('visibilitychange', onVisible);
    return () => {
      cancelled = true;
      clearInterval(interval);
      document.removeEventListener('visibilitychange', onVisible);
    };
  }, [tab]);

  return (
    <div style={{
      display: 'flex',
      flexDirection: 'column',
      height: '100vh',
      background: theme.bg,
      color: theme.t1,
      fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif',
    }}>
      <TotalBar summaries={exposureSummaries} connected={connected} />
      <AlertBanner alertCount={alertCount} onShowHistory={() => setShowAlertHistory(true)} />
      <RiskBanner summaries={exposureSummaries} />

      <div style={{
        display: 'flex',
        borderBottom: `1px solid ${theme.border}`,
        background: theme.bg2,
        alignItems: 'center',
      }}>
        <button style={tabStyle(tab === 'exposure')} onClick={() => setTab('exposure')}>Exposure</button>
        <button style={tabStyle(tab === 'positions')} onClick={() => setTab('positions')}>Positions</button>
        <button style={tabStyle(tab === 'pnl')} onClick={() => setTab('pnl')}>P&L</button>
        <button style={tabStyle(tab === 'netpnl')} onClick={() => setTab('netpnl')}>Net P&L</button>
        <button style={tabStyle(tab === 'equitypnl')} onClick={() => setTab('equitypnl')}>Equity P&L</button>
        <button style={tabStyle(tab === 'compare')} onClick={() => setTab('compare')}>Compare</button>
        <button style={tabStyle(tab === 'markup')} onClick={() => setTab('markup')}>Markup</button>
        <button style={tabStyle(tab === 'bridge')} onClick={() => setTab('bridge')}>Bridge</button>
        <button style={tabStyle(tab === 'mappings')} onClick={() => setTab('mappings')}>Mappings</button>
        <button style={tabStyle(tab === 'settings')} onClick={() => setTab('settings')}>Settings</button>

        <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center' }}>
          <ConnectionHealthDots />
        </div>

        {/* Alert bell + badge */}
        <button
          onClick={() => setShowAlertHistory(true)}
          style={{
            marginLeft: 6,
            position: 'relative',
            background: 'transparent',
            border: `1px solid ${alertCount > 0 ? theme.amber : theme.border}`,
            borderRadius: 6,
            padding: '4px 10px',
            cursor: 'pointer',
            fontSize: 14,
            color: alertCount > 0 ? theme.amber : theme.t2,
          }}
          title={`${alertCount} unacknowledged alerts`}
        >
          {'\uD83D\uDD14'}
          {alertCount > 0 && (
            <span style={{
              position: 'absolute',
              top: -6,
              right: -6,
              background: theme.red,
              color: '#fff',
              fontSize: 9,
              fontWeight: 700,
              borderRadius: '50%',
              width: 16,
              height: 16,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}>
              {alertCount > 9 ? '9+' : alertCount}
            </span>
          )}
        </button>

        {/* Sound toggle */}
        <button
          onClick={() => {
            const next = !soundEnabled;
            setSoundEnabled(next);
            localStorage.setItem('alertSound', String(next));
          }}
          style={{
            background: 'transparent',
            border: `1px solid ${theme.border}`,
            borderRadius: 6,
            padding: '4px 10px',
            cursor: 'pointer',
            fontSize: 14,
            color: soundEnabled ? theme.t2 : theme.t3,
            marginLeft: 6,
          }}
          title={soundEnabled ? 'Mute alert sounds' : 'Enable alert sounds'}
        >
          {soundEnabled ? '\uD83D\uDD0A' : '\uD83D\uDD07'}
        </button>

        <button
          onClick={() => setShowUserGuide(true)}
          style={{
            marginLeft: 6,
            background: 'transparent',
            border: `1px solid ${theme.border}`,
            borderRadius: 6,
            padding: '4px 10px',
            cursor: 'pointer',
            fontSize: 14,
            color: theme.t2,
          }}
          title="Open user guide"
        >
          {'\uD83D\uDCD6'}
        </button>

        <button
          onClick={toggleTheme}
          style={{
            marginLeft: 6,
            marginRight: 12,
            background: 'transparent',
            border: `1px solid ${theme.border}`,
            borderRadius: 6,
            padding: '4px 10px',
            cursor: 'pointer',
            fontSize: 14,
            color: theme.t2,
          }}
          title={`Switch to ${mode === 'dark' ? 'light' : 'dark'} mode`}
        >
          {mode === 'dark' ? '\u2600' : '\u263E'}
        </button>
      </div>

      <StaleWrapper isStale={!connected} style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>
        <div style={{ flex: 1, overflow: 'auto', display: 'flex', flexDirection: 'column' }}>
          {tab === 'exposure' && <ExposureTable summaries={exposureSummaries} prices={prices} />}
          {tab === 'positions' && <PositionsGrid positions={positions} />}
          {tab === 'pnl' && <PnLPanel />}
          {tab === 'netpnl' && <PeriodPnLPanel />}
          {tab === 'equitypnl' && <EquityPnLPanel />}
          {tab === 'compare' && <PositionsCompare prices={prices} />}
          {tab === 'markup' && <MarkupPanel />}
          {tab === 'bridge' && <BridgePanel />}
          {tab === 'mappings' && <SymbolMappingAdmin />}
          {tab === 'settings' && <SettingsPanel />}
        </div>
      </StaleWrapper>

      <AlertToast alerts={newAlerts} soundEnabled={soundEnabled} onAcknowledge={acknowledgeAlert} />
      {showAlertHistory && (
        <AlertHistory onClose={() => setShowAlertHistory(false)} onAcknowledge={acknowledgeAlert} />
      )}
      <KeyboardShortcutsOverlay />
      <UserGuideOverlay open={showUserGuide} onClose={() => setShowUserGuide(false)} />
    </div>
  );
}

function App() {
  return (
    <ThemeProvider>
      <ErrorToastProvider>
        <AppContent />
      </ErrorToastProvider>
    </ThemeProvider>
  );
}

export default App;
