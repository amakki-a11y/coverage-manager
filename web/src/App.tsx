import { useState, useEffect, useCallback } from 'react';
import { ThemeProvider, useTheme } from './ThemeContext';
import { useExposureSocket } from './hooks/useExposureSocket';
import { ExposureTable } from './components/ExposureTable';
import { PositionsGrid } from './components/PositionsGrid';
import { SymbolMappingAdmin } from './components/SymbolMappingAdmin';
import { SettingsPanel } from './components/SettingsPanel';
import { PnLPanel } from './components/PnLPanel';
import { PeriodPnLPanel } from './components/PeriodPnLPanel';
import { EquityPnLPage } from './components/EquityPnLPage';
import { PositionsCompare } from './pages/PositionsCompare';
import { AlertToast } from './components/AlertToast';
import { AlertBanner } from './components/AlertBanner';
import { AlertHistory } from './components/AlertHistory';
import { RiskBanner } from './components/RiskBanner';
import { ErrorToastProvider } from './components/ErrorToast';
import { StaleWrapper } from './components/Skeleton';
import { KeyboardShortcutsOverlay } from './components/KeyboardShortcutsOverlay';
import { UserGuideOverlay } from './components/UserGuideOverlay';
import { Sidebar, type SidebarTab } from './shell/Sidebar';
import { Topbar } from './shell/Topbar';
import { CommandPalette } from './shell/CommandPalette';
import { TweaksPanel, DEFAULT_TWEAKS, type Tweaks } from './shell/TweaksPanel';
import { API_BASE } from './config';
import type { Position } from './types';

/**
 * Root shell. Hosts the redesigned left sidebar, topbar, and the currently-
 * active tab panel. The sidebar + topbar are in `./shell/` and use global
 * CSS classes declared in `./styles/styles.css` + `./styles/styles-extra.css`.
 * Existing tab content components still render inline-styled via the
 * `THEME` object — they work unchanged inside the new shell.
 *
 * Keyboard shortcuts: digits 1-8 select the sidebar tabs in order (when
 * focus is outside an input). See `KeyboardShortcutsOverlay` for the
 * full list shown with `?`.
 */
function AppContent() {
  const { mode, toggleTheme } = useTheme();
  const [tab, setTab] = useState<SidebarTab>('exposure');
  const [collapsed, setCollapsed] = useState<boolean>(() => localStorage.getItem('sidebar.collapsed') === 'true');
  const { exposureSummaries, prices, connected, newAlerts, alertCount } = useExposureSocket();
  const [positions, setPositions] = useState<Position[]>([]);
  const [showAlertHistory, setShowAlertHistory] = useState(false);
  const [showUserGuide, setShowUserGuide] = useState(false);
  const [showPalette, setShowPalette] = useState(false);
  const [showTweaks, setShowTweaks] = useState(false);
  const [tweaks, setTweaks] = useState<Tweaks>(() => {
    try {
      const raw = localStorage.getItem('tweaks');
      if (raw) return { ...DEFAULT_TWEAKS, ...JSON.parse(raw) };
    } catch { /* ignore */ }
    return DEFAULT_TWEAKS;
  });
  const [soundEnabled, setSoundEnabled] = useState(() => {
    const saved = localStorage.getItem('alertSound');
    return saved !== 'false';
  });

  // Persist sidebar collapsed state so a reload keeps the dealer's chosen width.
  useEffect(() => {
    localStorage.setItem('sidebar.collapsed', String(collapsed));
  }, [collapsed]);

  // Persist tweaks + mirror accent/density onto <html> data attrs so
  // CSS rules (in styles-extra.css) can react without touching JS.
  useEffect(() => {
    localStorage.setItem('tweaks', JSON.stringify(tweaks));
    document.documentElement.setAttribute('data-accent', tweaks.accent);
    document.documentElement.setAttribute('data-density', tweaks.density);
  }, [tweaks]);

  const acknowledgeAlert = useCallback(async (id: string) => {
    try {
      await fetch(`${API_BASE}/api/alerts/${id}/acknowledge`, { method: 'POST' });
    } catch { /* ignore */ }
  }, []);

  // Keyboard shortcuts:
  //   1-8         → tabs in sidebar order (only when focus is outside inputs)
  //   Cmd/Ctrl-K  → Command Palette (works regardless of focus)
  useEffect(() => {
    const TAB_KEYS: Record<string, SidebarTab> = {
      '1': 'exposure', '2': 'positions', '3': 'compare',
      '4': 'pnl',      '5': 'netpnl',    '6': 'equitypnl',
    };
    const onKey = (e: KeyboardEvent) => {
      // Cmd+K / Ctrl+K opens the palette from anywhere
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setShowPalette(o => !o);
        return;
      }
      if (e.metaKey || e.ctrlKey || e.altKey) return;
      const el = e.target as HTMLElement | null;
      if (el && (el.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(el.tagName))) return;
      const t = TAB_KEYS[e.key];
      if (t) {
        e.preventDefault();
        setTab(t);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  // Fetch positions for the positions tab.
  // 5s cadence, in-flight guard, pause when tab hidden via visibilitychange.
  useEffect(() => {
    if (tab !== 'positions') return;
    let cancelled = false;
    let inFlight = false;
    const fetchPositions = async () => {
      if (inFlight || document.hidden) return;
      inFlight = true;
      try {
        const res = await fetch(`${API_BASE}/api/exposure/positions`);
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
    <div className={`app ${collapsed ? 'collapsed' : ''}`}>
      <Sidebar
        tab={tab}
        setTab={setTab}
        collapsed={collapsed}
        setCollapsed={setCollapsed}
        alertCount={alertCount}
      />

      <Topbar
        summaries={exposureSummaries}
        mode={mode}
        alertCount={alertCount}
        onToggleTheme={toggleTheme}
        onOpenPalette={() => setShowPalette(true)}
        onOpenAlerts={() => setShowAlertHistory(true)}
        onOpenGuide={() => setShowUserGuide(true)}
        onOpenTweaks={() => setShowTweaks(true)}
      />

      <div className="main">
        <AlertBanner alertCount={alertCount} onShowHistory={() => setShowAlertHistory(true)} />
        <RiskBanner summaries={exposureSummaries} />

        <StaleWrapper isStale={!connected} style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>
          <div style={{ flex: 1, overflow: 'auto', display: 'flex', flexDirection: 'column' }}>
            {tab === 'exposure'   && <ExposureTable summaries={exposureSummaries} prices={prices} onNavigate={setTab} />}
            {tab === 'positions'  && <PositionsGrid positions={positions} />}
            {tab === 'pnl'        && <PnLPanel />}
            {tab === 'netpnl'     && <PeriodPnLPanel />}
            {tab === 'equitypnl'  && <EquityPnLPage />}
            {tab === 'compare'    && <PositionsCompare prices={prices} />}
            {tab === 'mappings'   && <SymbolMappingAdmin />}
            {tab === 'alerts'     && <AlertHistoryTabStub onOpen={() => setShowAlertHistory(true)} />}
            {tab === 'settings'   && <SettingsPanel />}
          </div>
        </StaleWrapper>
      </div>

      <AlertToast alerts={newAlerts} soundEnabled={soundEnabled} onAcknowledge={acknowledgeAlert} />
      {showAlertHistory && (
        <AlertHistory onClose={() => setShowAlertHistory(false)} onAcknowledge={acknowledgeAlert} />
      )}
      <KeyboardShortcutsOverlay />
      <UserGuideOverlay open={showUserGuide} onClose={() => setShowUserGuide(false)} />
      <CommandPalette
        open={showPalette}
        onClose={() => setShowPalette(false)}
        onNavigate={setTab}
        onOpenGuide={() => setShowUserGuide(true)}
        onOpenAlerts={() => setShowAlertHistory(true)}
        onToggleTheme={toggleTheme}
      />
      <TweaksPanel
        open={showTweaks}
        onClose={() => setShowTweaks(false)}
        tweaks={tweaks}
        setTweaks={setTweaks}
      />
      {/* Silence unused-var warning; soundEnabled toggle will land on the Tweaks panel in Phase 5. */}
      <input type="hidden" value={String(soundEnabled)} readOnly />
    </div>
  );
}

/** Until Phase 4 rebuilds the full Alerts tab, clicking the sidebar's Alerts
 *  entry just opens the existing `AlertHistory` modal. Keeps the nav item
 *  honest without a half-built screen. */
function AlertHistoryTabStub({ onOpen }: { onOpen: () => void }) {
  useEffect(() => { onOpen(); }, [onOpen]);
  return (
    <div style={{ padding: 24, color: 'var(--t3)' }}>
      Alert history opened in an overlay.
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
