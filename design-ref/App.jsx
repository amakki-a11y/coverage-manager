/* global React, ReactDOM, I */
const { useState: useS2, useEffect: useE2, useRef: useR2 } = React;
const fmt = window.fmt, fp = window.fp, pc = window.pc;

// ========== Sidebar ==========
function Sidebar({ tab, setTab, collapsed, setCollapsed, alertCount }) {
  const groups = [
    { label: 'Real-time', items: [
      { id: 'exposure',  label: 'Exposure',   icon: <I.grid/>,   kbd: '1' },
      { id: 'positions', label: 'Positions',  icon: <I.list/>,   kbd: '2' },
      { id: 'compare',   label: 'Compare',    icon: <I.split/>,  kbd: '3' },
      { id: 'bridge',    label: 'Bridge',     icon: <I.bridge/>, kbd: '4', pill: { kind: 'red', text: '1' } },
    ]},
    { label: 'P&L', items: [
      { id: 'pnl',     label: 'P&L',         icon: <I.wallet/>, kbd: '5' },
      { id: 'netpnl',  label: 'Net P&L',     icon: <I.layers/>, kbd: '6' },
      { id: 'equity',  label: 'Equity P&L',  icon: <I.wallet/>, kbd: '7' },
      { id: 'markup',  label: 'Markup',      icon: <I.bolt/>,   kbd: '8' },
    ]},
    { label: 'Config', items: [
      { id: 'mappings', label: 'Mappings',  icon: <I.map/> },
      { id: 'alerts',   label: 'Alerts',    icon: <I.bell/>, pill: alertCount ? { kind: 'red', text: alertCount } : null },
      { id: 'settings', label: 'Settings',  icon: <I.gear/> },
    ]},
  ];
  return (
    <div className="sidebar">
      <div className="brand" onClick={() => setCollapsed(!collapsed)} style={{ cursor: 'pointer' }}>
        <div className="brand-mark">C</div>
        {!collapsed && (
          <div>
            <div className="brand-name">Coverage Mgr</div>
            <div className="brand-sub">fxGROW · prod</div>
          </div>
        )}
      </div>
      {groups.map(g => (
        <div className="nav-section" key={g.label}>
          <div className="nav-label">{g.label}</div>
          {g.items.map(it => (
            <div key={it.id}
              className={`nav-item ${tab === it.id ? 'active' : ''}`}
              onClick={() => setTab(it.id)}
              title={collapsed ? it.label : ''}>
              <span className="icon">{it.icon}</span>
              <span className="label">{it.label}</span>
              {it.pill && <span className={`pill ${it.pill.kind}`}>{it.pill.text}</span>}
              <span className="kbd">{it.kbd}</span>
            </div>
          ))}
        </div>
      ))}
      <div className="sidebar-footer">
        <div className="connection">
          <span className="dot"/>
          <span className="label">MT5 · Coverage · Bridge live</span>
        </div>
        <div className="connection" style={{ color: 'var(--t3)' }}>
          <I.kbd/>
          {!collapsed && <span className="label" style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5 }}>⌘K · command</span>}
        </div>
      </div>
    </div>
  );
}

// ========== Topbar ==========
function Topbar({ totals, onToggleTheme, theme, onOpenTweaks, showBanner, onOpenPalette, alertCount, onOpenAlerts }) {
  const tkey = totals.bbNet >= 0 ? 'pos' : 'neg';
  return (
    <div className="topbar">
      <div className="top-totals">
        <div className="top-metric">
          <span className="lbl">Active Clients</span>
          <span className="val">28,421</span>
        </div>
        <div className="top-divider"/>
        <div className="top-metric">
          <span className="lbl">Open Positions</span>
          <span className="val">1,842</span>
        </div>
        <div className="top-divider"/>
        <div className="top-metric">
          <span className="lbl">Client Exposure</span>
          <span className={`val ${pc(totals.bbNet)}`}>{fp(totals.bbNet)}</span>
        </div>
        <div className="top-metric">
          <span className="lbl">Coverage</span>
          <span className={`val ${pc(totals.covNet)}`}>{fp(totals.covNet)}</span>
        </div>
        <div className="top-divider"/>
        <div className="top-metric highlight">
          <span className="lbl">Net P&L Today</span>
          <span className={`val ${pc(totals.netPnL)}`}>{fp(totals.netPnL)}</span>
        </div>
      </div>
      <div className="top-actions">
        <window.ConnectionHealthDots/>
        <div className="search-wrap">
          <I.search/>
          <input className="search" placeholder="Search symbol, login, rule…" onFocus={onOpenPalette} readOnly/>
          <span className="kbd">⌘K</span>
        </div>
        <button className="icon-btn ghost" title="Alerts" onClick={onOpenAlerts}>
          <I.bell/>
          {alertCount > 0 && <span className="badge">{alertCount}</span>}
        </button>
        <button className="icon-btn ghost" title="Toggle theme" onClick={onToggleTheme}>
          {theme === 'dark' ? <I.sun/> : <I.moon/>}
        </button>
        <button className="icon-btn ghost" title="Tweaks" onClick={onOpenTweaks}>
          <I.sliders/>
        </button>
        <div style={{ width: 1, height: 22, background: 'var(--border)', margin: '0 2px' }}/>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <div style={{ width: 26, height: 26, borderRadius: '50%', background: 'linear-gradient(135deg, var(--blue), var(--purple))', display: 'grid', placeItems: 'center', fontSize: 11, fontWeight: 700, color: 'white' }}>MX</div>
          <div style={{ fontSize: 11.5, lineHeight: 1.2 }}>
            <div style={{ fontWeight: 600 }}>Max Carter</div>
            <div className="t3" style={{ fontSize: 10.5 }}>Dealer · London</div>
          </div>
        </div>
      </div>
    </div>
  );
}

// ========== Command Palette ==========
function CommandPalette({ onClose, setTab }) {
  const [q, setQ] = useS2('');
  const cmds = [
    { group: 'Go to', items: [
      { label: 'Exposure', act: () => setTab('exposure') },
      { label: 'Compare', act: () => setTab('compare') },
      { label: 'Bridge', act: () => setTab('bridge') },
      { label: 'P&L', act: () => setTab('pnl') },
      { label: 'Net P&L', act: () => setTab('netpnl') },
      { label: 'Alerts', act: () => setTab('alerts') },
      { label: 'Settings', act: () => setTab('settings') },
    ]},
    { group: 'Actions', items: [
      { label: 'Hedge XAUUSD · 48.70 lots (BUY)', act: () => {} },
      { label: 'Capture manual snapshot', act: () => {} },
      { label: 'New alert rule', act: () => setTab('alerts') },
    ]},
  ];
  const filtered = cmds.map(g => ({ ...g, items: g.items.filter(i => i.label.toLowerCase().includes(q.toLowerCase())) })).filter(g => g.items.length);
  return (
    <div className="palette-backdrop" onClick={onClose}>
      <div className="palette" onClick={e => e.stopPropagation()}>
        <input className="palette-input" autoFocus placeholder="Search commands, symbols, logins…" value={q} onChange={e => setQ(e.target.value)} />
        <div style={{ maxHeight: 400, overflow: 'auto', padding: '6px 0' }}>
          {filtered.map(g => (
            <div key={g.group}>
              <div style={{ padding: '8px 18px 4px', color: 'var(--t3)', fontSize: 10, fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase' }}>{g.group}</div>
              {g.items.map(it => (
                <div key={it.label} onClick={() => { it.act(); onClose(); }} style={{ padding: '8px 18px', cursor: 'pointer', fontSize: 13 }}
                  onMouseEnter={e => e.currentTarget.style.background = 'var(--card-hover)'}
                  onMouseLeave={e => e.currentTarget.style.background = 'transparent'}>
                  {it.label}
                </div>
              ))}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

// ========== Tweaks ==========
function TweaksPanel({ show, onClose, tweaks, setTweaks }) {
  const set = (k, v) => setTweaks({ ...tweaks, [k]: v });
  const toggle = k => set(k, !tweaks[k]);
  return (
    <div className={`tweaks ${show ? 'show' : ''}`}>
      <div className="tweaks-header">
        <I.sliders/>
        <div style={{ fontWeight: 600, fontSize: 13 }}>Tweaks</div>
        <div className="spacer"/>
        <button className="icon-btn ghost" style={{ padding: '2px 6px' }} onClick={onClose}><I.close/></button>
      </div>
      <div className="tweaks-body">
        <TGroup label="Appearance">
          <TRow label="Theme">
            <div className="segmented">
              <button className={tweaks.theme === 'dark' ? 'active' : ''} onClick={() => set('theme', 'dark')}>Dark</button>
              <button className={tweaks.theme === 'light' ? 'active' : ''} onClick={() => set('theme', 'light')}>Light</button>
            </div>
          </TRow>
          <TRow label="Accent">
            <div className="segmented">
              {['blue','teal','purple'].map(c => (
                <button key={c} className={tweaks.accent === c ? 'active' : ''} onClick={() => set('accent', c)}>
                  <span style={{ display: 'inline-block', width: 9, height: 9, borderRadius: '50%', marginRight: 4,
                    background: c === 'blue' ? '#60A5FA' : c === 'teal' ? '#2DD4BF' : '#A78BFA' }}/>
                  {c}
                </button>
              ))}
            </div>
          </TRow>
          <TRow label="Density">
            <div className="segmented">
              <button className={tweaks.density === 'compact' ? 'active' : ''} onClick={() => set('density', 'compact')}>Compact</button>
              <button className={tweaks.density === 'comfortable' ? 'active' : ''} onClick={() => set('density', 'comfortable')}>Cozy</button>
              <button className={tweaks.density === 'spacious' ? 'active' : ''} onClick={() => set('density', 'spacious')}>Spacious</button>
            </div>
          </TRow>
          <TRow label="Grid lines"><span className={`switch ${tweaks.grid ? 'on' : ''}`} onClick={() => toggle('grid')}/></TRow>
        </TGroup>
        <TGroup label="Persona">
          <TRow label="Role">
            <div className="segmented">
              <button className={tweaks.persona === 'risk' ? 'active' : ''} onClick={() => set('persona', 'risk')}>Risk</button>
              <button className={tweaks.persona === 'ops' ? 'active' : ''} onClick={() => set('persona', 'ops')}>Ops</button>
              <button className={tweaks.persona === 'comp' ? 'active' : ''} onClick={() => set('persona', 'comp')}>Compliance</button>
            </div>
          </TRow>
        </TGroup>
        <TGroup label="Exposure">
          <TRow label="Layout">
            <div className="segmented">
              <button className={tweaks.exposureLayout === 'table' ? 'active' : ''} onClick={() => set('exposureLayout', 'table')}>Table</button>
              <button className={tweaks.exposureLayout === 'cards' ? 'active' : ''} onClick={() => set('exposureLayout', 'cards')}>Cards</button>
            </div>
          </TRow>
          <TRow label="Show closed rows"><span className={`switch ${tweaks.showClosed ? 'on' : ''}`} onClick={() => toggle('showClosed')}/></TRow>
          <TRow label="Animate ticks"><span className={`switch ${tweaks.flash ? 'on' : ''}`} onClick={() => toggle('flash')}/></TRow>
        </TGroup>
        <TGroup label="Alerts">
          <TRow label="Banner"><span className={`switch ${tweaks.banner ? 'on' : ''}`} onClick={() => toggle('banner')}/></TRow>
          <TRow label="Toast on trigger"><span className={`switch ${tweaks.toasts ? 'on' : ''}`} onClick={() => toggle('toasts')}/></TRow>
          <TRow label="Sound"><span className={`switch ${tweaks.sound ? 'on' : ''}`} onClick={() => toggle('sound')}/></TRow>
        </TGroup>
      </div>
    </div>
  );
}
function TGroup({ label, children }) {
  return (
    <div style={{ padding: '8px 0' }}>
      <div style={{ color: 'var(--t3)', fontSize: 10, fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase', marginBottom: 4 }}>{label}</div>
      {children}
    </div>
  );
}
function TRow({ label, children }) {
  return <div className="tweak-row"><span className="lbl">{label}</span>{children}</div>;
}

// ========== Alert Banner ==========
function AlertBanner({ alert, onDismiss, onGoto }) {
  if (!alert) return null;
  return (
    <div className="alert-banner" style={{
      background: alert.severity === 'crit' ? 'var(--red-dim)' : 'var(--amber-dim)',
      color: alert.severity === 'crit' ? 'var(--red)' : 'var(--amber)',
      borderBottomColor: alert.severity === 'crit' ? 'var(--red)' : 'var(--amber)',
    }}>
      <I.warn/>
      <span style={{ fontWeight: 700, letterSpacing: '0.04em', fontSize: 11, textTransform: 'uppercase' }}>
        {alert.severity === 'crit' ? 'Critical' : 'Warning'}
      </span>
      <span style={{ fontWeight: 500 }}>{alert.desc}</span>
      <div className="spacer"/>
      <button className="icon-btn ghost" style={{ color: 'inherit', padding: '2px 8px', fontSize: 11 }} onClick={onGoto}>Go to rules →</button>
      <button className="icon-btn ghost" style={{ color: 'inherit', padding: '2px 6px' }} onClick={onDismiss}><I.close/></button>
    </div>
  );
}

// ========== Main App ==========
function App() {
  const [tab, setTab] = useS2('exposure');
  const [collapsed, setCollapsed] = useS2(false);
  const [tweaks, setTweaks] = useS2({
    theme: 'dark', accent: 'blue', density: 'compact', grid: false,
    persona: 'risk', exposureLayout: 'table', showClosed: true, flash: true,
    banner: true, toasts: true, sound: false,
  });
  const [showTweaks, setShowTweaks] = useS2(false);
  const [showPalette, setShowPalette] = useS2(false);
  const [hedgeRow, setHedgeRow] = useS2(null);
  const [toasts, setToasts] = useS2([]);
  const [alerts, setAlerts] = useS2(window.ALERTS_SEED);
  const [bannerAlert, setBannerAlert] = useS2(window.ALERTS_SEED.find(a => !a.ack && a.severity === 'crit'));
  const [showKbd, setShowKbd] = useS2(false);
  const [dateRange, setDateRange] = useS2(() => {
    const saved = localStorage.getItem('cm-date-range');
    if (saved) try { return JSON.parse(saved); } catch {}
    const tos = (d) => d.toISOString().slice(0, 10);
    const now = new Date();
    return { from: tos(now), to: tos(now) };
  });
  useE2(() => { localStorage.setItem('cm-date-range', JSON.stringify(dateRange)); }, [dateRange]);

  // Apply theme + tweaks to <html>
  useE2(() => {
    document.documentElement.setAttribute('data-theme', tweaks.theme);
    document.documentElement.setAttribute('data-accent', tweaks.accent);
    document.documentElement.setAttribute('data-density', tweaks.density);
  }, [tweaks]);

  // Keyboard
  useE2(() => {
    const handler = (e) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); setShowPalette(true); return; }
      if (e.target.tagName === 'INPUT' || e.target.tagName === 'SELECT') return;
      const map = { '1':'exposure','2':'positions','3':'compare','4':'bridge','5':'pnl','6':'netpnl','7':'equity','8':'markup' };
      if (map[e.key]) setTab(map[e.key]);
      if (e.key === 'Escape') { setShowPalette(false); setShowTweaks(false); setHedgeRow(null); }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, []);

  // Tweak listener protocol (persistent tweaks toolbar)
  useE2(() => {
    const h = (e) => {
      const m = e.data || {};
      if (m.type === '__activate_edit_mode') setShowTweaks(true);
      if (m.type === '__deactivate_edit_mode') setShowTweaks(false);
    };
    window.addEventListener('message', h);
    window.parent.postMessage({ type: '__edit_mode_available' }, '*');
    return () => window.removeEventListener('message', h);
  }, []);

  // Periodic random alert
  useE2(() => {
    const id = setInterval(() => {
      if (!tweaks.toasts) return;
      if (Math.random() > 0.78) {
        const samples = [
          { severity: 'warn', title: 'Hedge slippage', desc: 'US30 hedge slipped 1.2 pips vs target', meta: 'Bridge b-8411' },
          { severity: 'info', title: 'Snapshot saved', desc: '12-symbol hourly snapshot captured', meta: '15:00:02 UTC' },
          { severity: 'warn', title: 'LP latency', desc: 'Centroid-A round-trip 1842ms (threshold 500ms)', meta: 'avg last 60s' },
        ];
        const t = samples[Math.floor(Math.random() * samples.length)];
        setToasts(prev => [...prev, { ...t, id: Date.now() }]);
      }
    }, 6500);
    return () => clearInterval(id);
  }, [tweaks.toasts]);

  // Compute totals for topbar
  const totals = React.useMemo(() => {
    const S = window.SYMBOLS, E = window.EXPOSURE, C = window.CLOSED_PNL;
    let t = { bbNet: 0, covNet: 0, netPnL: 0 };
    for (const s of S) {
      const e = E[s.sym]; const c = C[s.sym];
      t.bbNet += e.bbBuy - e.bbSell;
      t.covNet += e.covBuy - e.covSell;
      t.netPnL += (-e.bbPnL + e.covPnL) + (c.cov - c.bb);
    }
    return t;
  }, []);

  const onOpenHedge = (row) => setHedgeRow(row);
  const onConfirmHedge = ({ row, dir, vol, lpAcct }) => {
    setHedgeRow(null);
    setToasts(prev => [...prev, {
      id: Date.now(),
      severity: 'info',
      title: 'Hedge fired',
      desc: `${dir} ${vol.toFixed(2)} ${row.s.sym} via ${lpAcct.split('·')[1].trim()}`,
      meta: `Bridge b-${Math.floor(Math.random()*9000+1000)} · t+180ms`,
      variant: 'ok',
    }]);
  };

  const unackCount = alerts.filter(a => !a.ack).length;

  const renderTab = () => {
    switch (tab) {
      case 'exposure':  return <window.ExposureTable showGrid={tweaks.grid} onOpenHedge={onOpenHedge} layout={tweaks.exposureLayout}/>;
      case 'positions': return <window.PositionsPanel/>;
      case 'pnl':       return <window.PnLPanel/>;
      case 'netpnl':    return <window.NetPnLPanel dateRange={dateRange} setDateRange={setDateRange}/>;
      case 'equity':    return <window.EquityPnLPanel dateRange={dateRange} setDateRange={setDateRange}/>;
      case 'markup':    return <window.MarkupPanel dateRange={dateRange} setDateRange={setDateRange}/>;
      case 'compare':   return <window.PositionsComparePanel dateRange={dateRange} setDateRange={setDateRange}/>;
      case 'bridge':    return <window.BridgePanel dateRange={dateRange} setDateRange={setDateRange}/>;
      case 'mappings':  return <window.MappingsPanel/>;
      case 'alerts':    return <window.AlertsPanel alerts={alerts} ackAlert={id => setAlerts(alerts.map(a => a.id === id ? { ...a, ack: true } : a))} setAlerts={setAlerts}/>;
      case 'settings':  return <window.SettingsPanel/>;
      default: return null;
    }
  };

  return (
    <div className={`app ${collapsed ? 'collapsed' : ''}`}>
      <Sidebar tab={tab} setTab={setTab} collapsed={collapsed} setCollapsed={setCollapsed} alertCount={unackCount}/>
      <Topbar
        totals={totals}
        onToggleTheme={() => setTweaks({ ...tweaks, theme: tweaks.theme === 'dark' ? 'light' : 'dark' })}
        theme={tweaks.theme}
        onOpenTweaks={() => setShowTweaks(!showTweaks)}
        onOpenPalette={() => setShowPalette(true)}
        alertCount={unackCount}
        onOpenAlerts={() => setTab('alerts')}
      />
      <div className="main" data-screen-label={tab}>
        <window.RiskBanner
          totals={totals}
          symbolsAtRisk={window.COMPARE_SUMMARY}
          onEditThresh={() => setTab('alerts')}
        />
        {tweaks.banner && bannerAlert && (
          <AlertBanner alert={bannerAlert}
            onDismiss={() => setBannerAlert(null)}
            onGoto={() => setTab('alerts')} />
        )}
        {renderTab()}
      </div>
      <window.KbdOverlay show={showKbd} onClose={() => setShowKbd(false)}/>

      {hedgeRow && <window.HedgeModal row={hedgeRow} onClose={() => setHedgeRow(null)} onConfirm={onConfirmHedge}/>}
      {showPalette && <CommandPalette onClose={() => setShowPalette(false)} setTab={setTab}/>}

      <TweaksPanel show={showTweaks} onClose={() => setShowTweaks(false)} tweaks={tweaks} setTweaks={setTweaks}/>

      <div className="toasts">
        {toasts.slice(-4).map(t => (
          <Toast key={t.id} t={t} onClose={() => setToasts(toasts.filter(x => x.id !== t.id))}/>
        ))}
      </div>
    </div>
  );
}

function Toast({ t, onClose }) {
  useE2(() => { const id = setTimeout(onClose, 5500); return () => clearTimeout(id); }, []);
  const cls = t.variant === 'ok' ? 'info' : t.severity;
  const iconEl = t.variant === 'ok' ? '✓' : t.severity === 'crit' ? '!' : t.severity === 'warn' ? '!' : 'i';
  return (
    <div className={`toast ${cls}`}>
      <div className="toast-icon">{iconEl}</div>
      <div className="toast-body">
        <div className="toast-title">{t.title}</div>
        <div className="toast-desc">{t.desc}</div>
        {t.meta && <div className="toast-meta">{t.meta}</div>}
      </div>
      <button className="toast-close" onClick={onClose}>×</button>
    </div>
  );
}

// Mount
ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
