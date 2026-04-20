// Shell components: RiskBanner, ConnectionHealthDots, DateRangePicker,
// StaleWrapper, KeyboardShortcutsOverlay, UnmappedPill.

const { useState: uS, useEffect: uE, useRef: uR } = React;

// ========== Risk Banner ==========
// Top-of-page amber/red watchdog. Trips when |netVolume| crosses threshold
// or any meaningful-volume symbol sits below 80% hedged.
function RiskBanner({ totals, symbolsAtRisk, threshAmber = 50, threshRed = 150, onEditThresh }) {
  const abs = Math.abs(totals.bbNet - totals.covNet);
  let tier = 'ok';
  if (abs >= threshRed) tier = 'red';
  else if (abs >= threshAmber) tier = 'amber';
  const poorlyHedged = symbolsAtRisk.filter(s => s.hedge < 80 && Math.abs(s.cliNet) > 5);
  if (poorlyHedged.length) tier = tier === 'red' ? 'red' : 'amber';
  if (tier === 'ok') return null;

  return (
    <div className={`risk-banner ${tier}`}>
      <span className="rb-pulse"/>
      <span className="rb-tag">{tier === 'red' ? 'RISK' : 'WATCH'}</span>
      <span className="rb-msg">
        <b>{fp(totals.bbNet - totals.covNet)} lots</b> uncovered
        {poorlyHedged.length > 0 && <span> · <b>{poorlyHedged.length}</b> symbol{poorlyHedged.length > 1 ? 's' : ''} under 80%</span>}
        <span className="t3" style={{ marginLeft: 8 }}>
          thresholds: {threshAmber}/{threshRed} lots
        </span>
      </span>
      <button className="rb-link" onClick={onEditThresh}>Edit thresholds</button>
    </div>
  );
}
window.RiskBanner = RiskBanner;

// ========== Connection Health Dots ==========
function ConnectionHealthDots() {
  const [open, setOpen] = uS(false);
  const C = window.CONNECTIONS;
  const order = ['mt5', 'collector', 'centroid', 'supabase'];
  const worst = order.map(k => C[k].status).reduce((w, s) => {
    if (s === 'err') return 'err';
    if (s === 'warn' && w !== 'err') return 'warn';
    return w;
  }, 'ok');

  return (
    <div className="chd" onMouseLeave={() => setOpen(false)}>
      <button className={`chd-trigger ${worst}`} onClick={() => setOpen(o => !o)}>
        {order.map(k => (
          <span key={k} className={`chd-dot ${C[k].status}`} title={C[k].name}/>
        ))}
      </button>
      {open && (
        <div className="chd-pop">
          <div className="chd-pop-title">System health</div>
          {order.map(k => {
            const x = C[k];
            return (
              <div key={k} className="chd-row">
                <span className={`chd-dot lg ${x.status}`}/>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontWeight: 600, fontSize: 12 }}>{x.name}</div>
                  <div className="t3" style={{ fontSize: 10.5, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{x.detail}</div>
                </div>
                <div className="mono t2" style={{ fontSize: 11 }}>{x.latMs}ms</div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
window.ConnectionHealthDots = ConnectionHealthDots;

// ========== Date Range Picker (shared across tabs via localStorage) ==========
function DateRangePicker({ value, onChange, compact }) {
  const { from, to } = value;
  const setPreset = (kind) => {
    const now = new Date();
    const y = now.getFullYear(), m = now.getMonth(), d = now.getDate();
    const toS = (dt) => dt.toISOString().slice(0, 10);
    let f = from, t = to;
    if (kind === 'today')      { f = toS(new Date(y,m,d));   t = toS(new Date(y,m,d)); }
    if (kind === 'yesterday')  { f = toS(new Date(y,m,d-1)); t = toS(new Date(y,m,d-1)); }
    if (kind === 'week')       { f = toS(new Date(y,m,d-6)); t = toS(new Date(y,m,d)); }
    if (kind === 'mtd')        { f = toS(new Date(y,m,1));   t = toS(new Date(y,m,d)); }
    if (kind === '7d')         { f = toS(new Date(y,m,d-7)); t = toS(new Date(y,m,d)); }
    if (kind === '30d')        { f = toS(new Date(y,m,d-30));t = toS(new Date(y,m,d)); }
    onChange({ from: f, to: t });
  };
  return (
    <div className="drp">
      <span className="drp-tz">BEIRUT</span>
      <input type="date" className="drp-input" value={from} onChange={e => onChange({ from: e.target.value, to })} />
      <span className="drp-dash">→</span>
      <input type="date" className="drp-input" value={to} onChange={e => onChange({ from, to: e.target.value })} />
      {!compact && (
        <div className="drp-presets">
          <button onClick={() => setPreset('today')}>T</button>
          <button onClick={() => setPreset('yesterday')}>Y</button>
          <button onClick={() => setPreset('week')}>W</button>
          <button onClick={() => setPreset('mtd')}>MTD</button>
          <button onClick={() => setPreset('7d')}>7D</button>
          <button onClick={() => setPreset('30d')}>30D</button>
        </div>
      )}
    </div>
  );
}
window.DateRangePicker = DateRangePicker;

// ========== Stale Wrapper ==========
function StaleWrapper({ stale, children }) {
  return (
    <div className={`stale-wrap ${stale ? 'stale' : ''}`}>
      {children}
      {stale && (
        <div className="stale-overlay">
          <div className="stale-msg">
            <I.warn/> <span>Connection lost — data is stale</span>
            <button className="stale-retry" onClick={() => {}}>Retry</button>
          </div>
        </div>
      )}
    </div>
  );
}
window.StaleWrapper = StaleWrapper;

// ========== Keyboard Shortcuts Overlay ==========
function KbdOverlay({ show, onClose }) {
  if (!show) return null;
  const groups = [
    { label: 'Navigation', items: [
      ['1–9', 'Jump to tab'], ['⌘K', 'Command palette'], ['?', 'This overlay'], ['Esc', 'Close dialogs'],
    ]},
    { label: 'Date range', items: [
      ['T', 'Today'], ['Y', 'Yesterday'], ['W', 'This week'], ['M', 'Month to date'],
    ]},
    { label: 'Actions', items: [
      ['H', 'Hedge selected symbol'], ['S', 'Snapshot now'], ['A', 'Acknowledge banner alert'],
    ]},
  ];
  return (
    <div className="palette-backdrop" onClick={onClose}>
      <div className="kbd-overlay" onClick={e => e.stopPropagation()}>
        <div className="kbd-overlay-head">
          <I.kbd/> <span style={{ fontWeight: 600, fontSize: 13 }}>Keyboard shortcuts</span>
          <div className="spacer"/>
          <button className="icon-btn ghost" style={{ padding: '2px 6px' }} onClick={onClose}><I.close/></button>
        </div>
        <div className="kbd-overlay-body">
          {groups.map(g => (
            <div key={g.label} className="kbd-group">
              <div className="kbd-group-label">{g.label}</div>
              {g.items.map(([k, l]) => (
                <div key={k} className="kbd-row">
                  <span className="kbd-key">{k}</span>
                  <span className="kbd-desc">{l}</span>
                </div>
              ))}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
window.KbdOverlay = KbdOverlay;

// ========== Unmapped Pill ==========
function UnmappedPill() {
  return <span className="unmapped-pill" title="No mapping in symbol_mappings — contract-size conversion may fall back">UNMAPPED</span>;
}
window.UnmappedPill = UnmappedPill;

// ========== Loading Badge (for date-range fetches) ==========
function LoadingBadge({ show }) {
  if (!show) return null;
  return <span className="loading-badge"><span className="lb-spin"/> Loading…</span>;
}
window.LoadingBadge = LoadingBadge;
