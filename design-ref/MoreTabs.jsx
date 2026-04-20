// Upgraded panels that override bundle.js versions:
// - BridgePanel (CLIENT + COV OUT paired rows via rowSpan, anomaly toggle, time-diff coloring, mode pill)
// - AlertsPanel (rules + history with ack + severity filter)
// - SettingsPanel (sub-tabs: Connections / Equity P&L / Snapshots / Data Integrity / Reference)
// - NetPnLPanel (Begin / FloatΔ / Settled / Net / Edge + sentinel chip)

/* global React */
const { useState: mS, useMemo: mM, useEffect: mE } = React;

// =================================================================
// Bridge Panel — CLIENT row + COV OUT row(s) per deal group
// =================================================================
function BridgePanel({ dateRange, setDateRange }) {
  const [mode, setMode] = mS('stub'); // stub | live | replay
  const [anomalyOnly, setAnomalyOnly] = mS(false);
  const rows = mM(() => window.BRIDGE_ROWS.filter(r => {
    if (!anomalyOnly) return true;
    // anomaly: volume mismatch OR any fill > 500ms
    const volSum = r.covFills.reduce((a, f) => a + f.vol, 0);
    const slow = r.covFills.some(f => f.diffMs > 500);
    return Math.abs(volSum - r.cliVol) > 0.01 || slow;
  }), [anomalyOnly]);

  const stat = mM(() => {
    let total = window.BRIDGE_ROWS.length, anomalies = 0, avgMs = 0, count = 0, maxMs = 0;
    for (const r of window.BRIDGE_ROWS) {
      const volSum = r.covFills.reduce((a, f) => a + f.vol, 0);
      const slow = r.covFills.some(f => f.diffMs > 500);
      if (Math.abs(volSum - r.cliVol) > 0.01 || slow) anomalies++;
      for (const f of r.covFills) { avgMs += f.diffMs; count++; maxMs = Math.max(maxMs, f.diffMs); }
    }
    return { total, anomalies, avgMs: count ? avgMs / count : 0, maxMs };
  }, []);

  const timeClass = (ms) => ms < 200 ? 'ok' : ms < 500 ? 'warn' : 'bad';

  return (
    <div>
      <div className="toolbar">
        <div className="card-title">Bridge</div>
        <span className={`mode-pill ${mode}`}>{mode.toUpperCase()}</span>
        <span className="t3 sm">Pairs every client execution with its coverage fill(s).</span>
        <div className="spacer"/>
        <div className="segmented">
          <button className={mode === 'stub' ? 'active' : ''} onClick={() => setMode('stub')}>Stub</button>
          <button className={mode === 'live' ? 'active' : ''} onClick={() => setMode('live')}>Live</button>
          <button className={mode === 'replay' ? 'active' : ''} onClick={() => setMode('replay')}>Replay</button>
        </div>
        <label className="checkbox-lbl">
          <input type="checkbox" checked={anomalyOnly} onChange={e => setAnomalyOnly(e.target.checked)}/>
          Anomalies only ({stat.anomalies})
        </label>
        <window.DateRangePicker value={dateRange} onChange={setDateRange} compact/>
      </div>

      <div className="kpi-row">
        <KpiCard label="Total deals" val={stat.total} mono/>
        <KpiCard label="Anomalies" val={stat.anomalies} tone={stat.anomalies > 0 ? 'neg' : 't3'} mono/>
        <KpiCard label="Avg fill time" val={`${stat.avgMs.toFixed(0)}ms`} tone={stat.avgMs < 300 ? 'pos' : 'warn'} mono/>
        <KpiCard label="Max fill time" val={`${stat.maxMs}ms`} tone={stat.maxMs > 500 ? 'neg' : 't2'} mono/>
      </div>

      <div className="card" style={{ overflow: 'auto' }}>
        <table className="bridge-table">
          <thead>
            <tr>
              <th>Side</th>
              <th>Time</th>
              <th>Symbol</th>
              <th>Dir</th>
              <th className="num">Volume</th>
              <th className="num">Price</th>
              <th>Counterparty</th>
              <th className="num">Δt</th>
              <th>LP</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {rows.map(r => {
              const volSum = r.covFills.reduce((a, f) => a + f.vol, 0);
              const volMismatch = Math.abs(volSum - r.cliVol) > 0.01;
              return (
                <React.Fragment key={r.id}>
                  <tr className="bridge-client">
                    <td rowSpan={r.covFills.length + 1} className="bridge-side">
                      <span className="bridge-tag cli">CLIENT</span>
                    </td>
                    <td className="mono">{r.time}</td>
                    <td className="mono bold">{r.sym}</td>
                    <td><span className={`dir-chip ${r.side.toLowerCase()}`}>{r.side}</span></td>
                    <td className="num mono bold">{r.cliVol.toFixed(2)}</td>
                    <td className="num mono">{r.cliPrice}</td>
                    <td className="mono t2">login {r.cliLogin}</td>
                    <td className="num">—</td>
                    <td className="t3">—</td>
                    <td>
                      {volMismatch
                        ? <span className="status-chip bad">MISMATCH</span>
                        : <span className="status-chip ok">MATCHED</span>}
                    </td>
                  </tr>
                  {r.covFills.map((f, i) => (
                    <tr key={i} className="bridge-cov">
                      <td className="mono t3" style={{ fontSize: 10.5 }}>+{f.diffMs}ms</td>
                      <td className="mono t2">{r.sym}</td>
                      <td><span className={`dir-chip ${r.side === 'BUY' ? 'sell' : 'buy'} ghost`}>{r.side === 'BUY' ? 'SELL' : 'BUY'}</span></td>
                      <td className="num mono">{f.vol.toFixed(2)}</td>
                      <td className="num mono">{f.price}</td>
                      <td className="t3" style={{ paddingLeft: 16 }}>└ cov out</td>
                      <td className={`num mono diff-${timeClass(f.diffMs)}`}>{f.diffMs}ms</td>
                      <td className="mono">{f.lp}</td>
                      <td>
                        <span className={`status-chip ${f.diffMs < 500 ? 'ok' : 'warn'}`}>
                          {f.diffMs < 500 ? 'FILLED' : 'SLOW'}
                        </span>
                      </td>
                    </tr>
                  ))}
                </React.Fragment>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
window.BridgePanel = BridgePanel;

function KpiCard({ label, val, tone, mono }) {
  return (
    <div className="kpi-card">
      <div className="kpi-lbl">{label}</div>
      <div className={`kpi-val ${tone || ''} ${mono ? 'mono' : ''}`}>{val}</div>
    </div>
  );
}

// =================================================================
// Alerts Panel — tabbed: Active / Rules / History
// =================================================================
function AlertsPanel({ alerts, ackAlert }) {
  const [sub, setSub] = mS('active');
  const [sev, setSev] = mS('all');
  const visible = alerts.filter(a => sev === 'all' || a.severity === sev);
  return (
    <div>
      <div className="toolbar">
        <div className="segmented">
          <button className={sub === 'active' ? 'active' : ''} onClick={() => setSub('active')}>
            Active <span className="pill red sm">{alerts.filter(a => !a.ack).length}</span>
          </button>
          <button className={sub === 'rules' ? 'active' : ''} onClick={() => setSub('rules')}>Rules ({window.ALERT_RULES.length})</button>
          <button className={sub === 'history' ? 'active' : ''} onClick={() => setSub('history')}>History</button>
        </div>
        <div className="sep"/>
        {sub !== 'rules' && (
          <div className="segmented">
            <button className={sev === 'all' ? 'active' : ''} onClick={() => setSev('all')}>All</button>
            <button className={sev === 'crit' ? 'active' : ''} onClick={() => setSev('crit')}>Critical</button>
            <button className={sev === 'warn' ? 'active' : ''} onClick={() => setSev('warn')}>Warning</button>
            <button className={sev === 'info' ? 'active' : ''} onClick={() => setSev('info')}>Info</button>
          </div>
        )}
        <div className="spacer"/>
        {sub === 'rules' && <button className="icon-btn primary"><I.plus/> New rule</button>}
      </div>

      {sub === 'active' && (
        <div className="card">
          <table className="ep-table">
            <thead><tr><th></th><th>Alert</th><th>Symbol</th><th className="num">Actual</th><th className="num">Threshold</th><th>Time</th><th></th></tr></thead>
            <tbody>
              {visible.filter(a => !a.ack).map(a => (
                <tr key={a.id}>
                  <td><span className={`sev-chip ${a.severity}`}>{a.severity === 'crit' ? 'CRIT' : a.severity === 'warn' ? 'WARN' : 'INFO'}</span></td>
                  <td><div style={{ fontWeight: 600 }}>{a.title}</div><div className="t3 sm">{a.desc}</div></td>
                  <td className="mono">{a.sym}</td>
                  <td className="num mono bold">{a.actual}</td>
                  <td className="num mono t2">{a.threshold}</td>
                  <td className="t3 sm">{a.time}</td>
                  <td className="num">
                    <button className="icon-btn ghost sm" onClick={() => ackAlert(a.id)}>Ack</button>
                  </td>
                </tr>
              ))}
              {!visible.filter(a => !a.ack).length && (
                <tr><td colSpan="7" className="empty-cell">No active alerts. System nominal.</td></tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {sub === 'rules' && (
        <div className="card">
          <table className="ep-table">
            <thead><tr><th>Symbol</th><th>Trigger</th><th>Op</th><th className="num">Value</th><th>Severity</th><th>Channels</th><th>Enabled</th><th></th></tr></thead>
            <tbody>
              {window.ALERT_RULES.map(r => (
                <tr key={r.id}>
                  <td className="mono">{r.sym}</td>
                  <td><span className="chip sm gray">{r.kind.replace('_', ' ')}</span></td>
                  <td className="mono center bold">{r.op}</td>
                  <td className="num mono">{r.val} <span className="t3">{r.unit}</span></td>
                  <td><span className={`sev-chip ${r.sev}`}>{r.sev.toUpperCase()}</span></td>
                  <td><div style={{ display: 'flex', gap: 4 }}>{r.ch.map(c => <span key={c} className="chip sm gray">{c}</span>)}</div></td>
                  <td><span className={`switch sm ${r.enabled ? 'on' : ''}`}/></td>
                  <td className="num"><button className="icon-btn ghost sm">Edit</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {sub === 'history' && (
        <div className="card">
          <table className="ep-table">
            <thead><tr><th></th><th>Alert</th><th>Symbol</th><th className="num">Actual</th><th>Time</th><th>Ack by</th></tr></thead>
            <tbody>
              {visible.map(a => (
                <tr key={a.id} className={a.ack ? 'dim' : ''}>
                  <td><span className={`sev-chip ${a.severity}`}>{a.severity === 'crit' ? 'CRIT' : a.severity === 'warn' ? 'WARN' : 'INFO'}</span></td>
                  <td><div style={{ fontWeight: 600 }}>{a.title}</div><div className="t3 sm">{a.desc}</div></td>
                  <td className="mono">{a.sym}</td>
                  <td className="num mono">{a.actual}</td>
                  <td className="t3 sm">{a.time}</td>
                  <td className="mono t2 sm">{a.ack ? 'dealer.max' : '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
window.AlertsPanel = AlertsPanel;

// =================================================================
// Settings Panel — sub-tabs
// =================================================================
function SettingsPanel() {
  const [sub, setSub] = mS('connections');
  return (
    <div>
      <div className="toolbar">
        <div className="segmented">
          {[
            ['connections', 'Connections'],
            ['equity', 'Equity P&L'],
            ['snapshots', 'Snapshots'],
            ['integrity', 'Data Integrity'],
            ['reference', 'Reference'],
          ].map(([k, l]) => (
            <button key={k} className={sub === k ? 'active' : ''} onClick={() => setSub(k)}>{l}</button>
          ))}
        </div>
      </div>
      {sub === 'connections' && <ConnectionsSettings/>}
      {sub === 'equity' && <EquitySettings/>}
      {sub === 'snapshots' && <SnapshotsSettings/>}
      {sub === 'integrity' && <IntegritySettings/>}
      {sub === 'reference' && <ReferenceSettings/>}
    </div>
  );
}
window.SettingsPanel = SettingsPanel;

function SettingRow({ label, sub, children }) {
  return (
    <div className="set-row">
      <div className="set-row-lbl">
        <div style={{ fontWeight: 600 }}>{label}</div>
        {sub && <div className="t3 sm">{sub}</div>}
      </div>
      <div className="set-row-ctl">{children}</div>
    </div>
  );
}

function ConnectionsSettings() {
  const C = window.CONNECTIONS;
  return (
    <div className="settings-grid">
      {['mt5', 'collector', 'centroid', 'supabase'].map(k => {
        const x = C[k];
        return (
          <div className="card" key={k}>
            <div className="card-head">
              <span className={`chd-dot lg ${x.status}`}/>
              <span className="card-title">{x.name}</span>
              <span className="t3 sm mono">{x.latMs}ms</span>
              <div className="spacer"/>
              <span className={`status-chip ${x.status === 'ok' ? 'ok' : x.status === 'warn' ? 'warn' : 'bad'}`}>
                {x.status === 'ok' ? 'LIVE' : x.status === 'warn' ? 'DEGRADED' : 'DOWN'}
              </span>
            </div>
            <div className="card-body">
              <div className="t2 sm" style={{ marginBottom: 12 }}>{x.detail}</div>
              <SettingRow label="Host" sub="Endpoint the service dials">
                <input className="input mono" defaultValue={
                  k === 'mt5' ? 'mt5-mgr.fxgrow.com:443' :
                  k === 'collector' ? 'collector.internal:7019' :
                  k === 'centroid' ? 'bridge.centroid-lp.com:9443' :
                  'db.supabase.co:6543'
                }/>
              </SettingRow>
              <SettingRow label="Auth" sub="API key / cert pair">
                <input className="input mono" type="password" defaultValue="••••••••••••3219"/>
              </SettingRow>
              <SettingRow label="Timeout">
                <input className="input mono" defaultValue="5000" style={{ width: 80 }}/> <span className="t3 sm">ms</span>
              </SettingRow>
              <div style={{ display: 'flex', gap: 8, marginTop: 12 }}>
                <button className="icon-btn ghost sm">Test</button>
                <button className="icon-btn ghost sm">Reconnect</button>
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
}

function EquitySettings() {
  return (
    <div>
      <div className="card">
        <div className="card-head"><span className="card-title">Equity P&L engine</span></div>
        <div className="card-body">
          <SettingRow label="Begin equity source" sub="Where the period starting equity is anchored">
            <div className="segmented"><button className="active">Last snapshot</button><button>T-1 close</button><button>Session open</button></div>
          </SettingRow>
          <SettingRow label="Floating Δ basis" sub="How unrealized movement is priced">
            <div className="segmented"><button className="active">Current − Begin</button><button>Intra-period VWAP</button></div>
          </SettingRow>
          <SettingRow label="Credit treatment" sub="Include in Net P&L">
            <span className="switch on"/>
          </SettingRow>
          <SettingRow label="Adjustment treatment" sub="Manual deal corrections roll into equity">
            <span className="switch on"/>
          </SettingRow>
          <SettingRow label="Profit share %" sub="Default when no login-group override">
            <input className="input mono" defaultValue="0" style={{ width: 80 }}/> <span className="t3 sm">%</span>
          </SettingRow>
        </div>
      </div>
    </div>
  );
}

function SnapshotsSettings() {
  return (
    <div>
      <div className="card">
        <div className="card-head">
          <span className="card-title">Schedules</span>
          <div className="spacer"/>
          <button className="icon-btn primary sm"><I.plus/> New schedule</button>
        </div>
        <table className="ep-table">
          <thead><tr><th>Name</th><th>Cadence</th><th>Cron</th><th>Timezone</th><th>Last</th><th>Next</th><th>Enabled</th></tr></thead>
          <tbody>
            {window.SCHEDULES.map(s => (
              <tr key={s.id}>
                <td className="bold">{s.name}</td>
                <td><span className="chip sm gray">{s.cadence}</span></td>
                <td className="mono t2">{s.cron}</td>
                <td className="mono t2">{s.tz}</td>
                <td className="t3">{s.last}</td>
                <td className="t2 mono">{s.next}</td>
                <td><span className={`switch sm ${s.enabled ? 'on' : ''}`}/></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="card" style={{ marginTop: 12 }}>
        <div className="card-head">
          <span className="card-title">Snapshot history</span>
          <div className="spacer"/>
          <button className="icon-btn ghost sm">Capture now</button>
        </div>
        <table className="ep-table">
          <thead><tr><th>When</th><th>Trigger</th><th>Label</th><th className="num">Symbols</th><th>User</th><th></th></tr></thead>
          <tbody>
            {window.SNAPSHOTS.map(s => (
              <tr key={s.id}>
                <td className="mono">{s.t}</td>
                <td><span className={`chip sm ${s.trigger === 'scheduled' ? 'gray' : 'blue'}`}>{s.trigger}</span></td>
                <td className="bold">{s.label}</td>
                <td className="num mono">{s.symbols}</td>
                <td className="mono t2">{s.user}</td>
                <td className="num"><button className="icon-btn ghost sm">Restore</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function IntegritySettings() {
  return (
    <div className="card">
      <div className="card-head">
        <span className="card-title">Reconciliation runs</span>
        <span className="t3 sm">MT5 ↔ Supabase deal count check.</span>
        <div className="spacer"/>
        <button className="icon-btn ghost sm">Run now</button>
      </div>
      <table className="ep-table">
        <thead>
          <tr>
            <th>When</th>
            <th>Trigger</th>
            <th>Window</th>
            <th className="num">MT5</th>
            <th className="num">Supabase</th>
            <th className="num">Backfilled</th>
            <th className="num">Ghost</th>
            <th className="num">Modified</th>
            <th>Result</th>
          </tr>
        </thead>
        <tbody>
          {window.RECON_RUNS.map(r => (
            <tr key={r.id}>
              <td className="mono">{r.time} <span className="t3 sm">{r.tz}</span></td>
              <td><span className={`chip sm ${r.trigger === 'manual' ? 'blue' : 'gray'}`}>{r.trigger}</span></td>
              <td className="mono t2">{r.window}</td>
              <td className="num mono">{r.mt5.toLocaleString()}</td>
              <td className="num mono">{r.supa.toLocaleString()}</td>
              <td className={`num mono ${r.backfill > 0 ? 'warn' : 't3'}`}>{r.backfill}</td>
              <td className={`num mono ${r.ghost > 0 ? 'neg' : 't3'}`}>{r.ghost}</td>
              <td className={`num mono ${r.modified > 0 ? 'warn' : 't3'}`}>{r.modified}</td>
              <td><span className={`status-chip ${r.ok ? 'ok' : 'bad'}`}>{r.ok ? 'OK' : 'FAIL'}</span></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ReferenceSettings() {
  return (
    <div className="card">
      <div className="card-head"><span className="card-title">Reference data</span></div>
      <div className="card-body">
        <SettingRow label="Instrument contract sizes" sub="Pulled from symbol_mappings · fallback to 100k for FX">
          <button className="icon-btn ghost sm">View mapping</button>
        </SettingRow>
        <SettingRow label="Pip values" sub="Derived from contract size + quote currency rate">
          <button className="icon-btn ghost sm">Recompute</button>
        </SettingRow>
        <SettingRow label="Currency rates" sub="USD conversion table · refreshed hourly">
          <span className="t3 sm mono">Last: 14:30 UTC</span>
        </SettingRow>
        <SettingRow label="Trading sessions" sub="Per-symbol open/close windows">
          <button className="icon-btn ghost sm">Edit</button>
        </SettingRow>
      </div>
    </div>
  );
}

// =================================================================
// Net P&L Panel — period view with Begin / FloatΔ / Settled / Net
// =================================================================
function NetPnLPanel({ dateRange, setDateRange }) {
  const rows = window.NET_PNL_PERIOD;
  const tot = mM(() => rows.reduce((a, r) => ({
    bbBegin: a.bbBegin + r.bbBegin, bbCurr: a.bbCurr + r.bbCurr, bbFloatD: a.bbFloatD + r.bbFloatD, bbSettled: a.bbSettled + r.bbSettled, bbNet: a.bbNet + r.bbNet,
    covBegin: a.covBegin + r.covBegin, covCurr: a.covCurr + r.covCurr, covFloatD: a.covFloatD + r.covFloatD, covSettled: a.covSettled + r.covSettled, covNet: a.covNet + r.covNet,
  }), { bbBegin: 0, bbCurr: 0, bbFloatD: 0, bbSettled: 0, bbNet: 0, covBegin: 0, covCurr: 0, covFloatD: 0, covSettled: 0, covNet: 0 }), [rows]);
  const edgeTotal = -tot.bbNet + tot.covNet;
  return (
    <div>
      <div className="toolbar">
        <div className="card-title">Net P&L · period</div>
        <span className="t3 sm">
          Current − Begin = <b>FloatΔ</b>. Plus settled = Net. Broker edge = −Client + Coverage.
        </span>
        <div className="spacer"/>
        <window.DateRangePicker value={dateRange} onChange={setDateRange}/>
        <div className="sep"/>
        <div className="edge-pill">
          <span className="t3">Broker edge</span>
          <span className={`mono ${pc(edgeTotal)}`} style={{ fontWeight: 700 }}>{fp(edgeTotal)}</span>
        </div>
      </div>

      <div className="card" style={{ overflow: 'auto' }}>
        <table className="ep-table netpl-table">
          <thead>
            <tr>
              <th rowSpan="2" className="sticky-col">Symbol</th>
              <th colSpan="5" className="col-group cli">Client (broker pays out)</th>
              <th colSpan="5" className="col-group cov">Coverage (LP pays in)</th>
              <th rowSpan="2" className="num bold">Edge</th>
            </tr>
            <tr>
              <th className="num">Begin</th>
              <th className="num">Current</th>
              <th className="num">FloatΔ</th>
              <th className="num">Settled</th>
              <th className="num bold">Net</th>
              <th className="num">Begin</th>
              <th className="num">Current</th>
              <th className="num">FloatΔ</th>
              <th className="num">Settled</th>
              <th className="num bold">Net</th>
            </tr>
          </thead>
          <tbody>
            {rows.map(r => {
              const edge = -r.bbNet + r.covNet;
              return (
                <tr key={r.sym}>
                  <td className="sticky-col mono bold">
                    {r.sym}
                    {!r.beginFromSnap && <span className="sentinel-chip" title="No snapshot for this period — Begin anchored at 0">⚑ no anchor</span>}
                  </td>
                  <td className="num mono t2">{fmt(r.bbBegin)}</td>
                  <td className={`num mono ${pc(-r.bbCurr)}`}>{fp(-r.bbCurr)}</td>
                  <td className={`num mono ${pc(-r.bbFloatD)}`}>{fp(-r.bbFloatD)}</td>
                  <td className={`num mono ${pc(r.bbSettled)}`}>{fp(r.bbSettled)}</td>
                  <td className={`num mono bold ${pc(-r.bbNet)}`}>{fp(-r.bbNet)}</td>
                  <td className="num mono t2">{fmt(r.covBegin)}</td>
                  <td className={`num mono ${pc(r.covCurr)}`}>{fp(r.covCurr)}</td>
                  <td className={`num mono ${pc(r.covFloatD)}`}>{fp(r.covFloatD)}</td>
                  <td className={`num mono ${pc(r.covSettled)}`}>{fp(r.covSettled)}</td>
                  <td className={`num mono bold ${pc(r.covNet)}`}>{fp(r.covNet)}</td>
                  <td className={`num mono bold ${pc(edge)}`}>{fp(edge)}</td>
                </tr>
              );
            })}
            <tr className="ep-total">
              <td className="sticky-col">TOTAL</td>
              <td className="num mono t2">{fmt(tot.bbBegin)}</td>
              <td className={`num mono ${pc(-tot.bbCurr)}`}>{fp(-tot.bbCurr)}</td>
              <td className={`num mono ${pc(-tot.bbFloatD)}`}>{fp(-tot.bbFloatD)}</td>
              <td className={`num mono ${pc(tot.bbSettled)}`}>{fp(tot.bbSettled)}</td>
              <td className={`num mono bold ${pc(-tot.bbNet)}`}>{fp(-tot.bbNet)}</td>
              <td className="num mono t2">{fmt(tot.covBegin)}</td>
              <td className={`num mono ${pc(tot.covCurr)}`}>{fp(tot.covCurr)}</td>
              <td className={`num mono ${pc(tot.covFloatD)}`}>{fp(tot.covFloatD)}</td>
              <td className={`num mono ${pc(tot.covSettled)}`}>{fp(tot.covSettled)}</td>
              <td className={`num mono bold ${pc(tot.covNet)}`}>{fp(tot.covNet)}</td>
              <td className={`num mono bold ${pc(edgeTotal)}`}>{fp(edgeTotal)}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  );
}
window.NetPnLPanel = NetPnLPanel;
