// New / rebuilt tab panels:
// - EquityPnLPanel (12 cols + login groups config)
// - MarkupPanel
// - PositionsComparePanel (split layout + PnLRings widget)
// - BridgePanel (CLIENT + COV OUT rowSpan pairs)
// - AlertsPanel (rules + history)
// - SettingsPanel (sub-tabs: Connections · Equity P&L · Snapshots · Data Integrity · Reference)
// - NetPnLPanel (Begin / FloatΔ / Settled / Net / Edge)

/* global React */
const { useState: nS, useEffect: nE, useMemo: nM, useRef: nR } = React;

// =================================================================
// Equity P&L Tab
// =================================================================
function EquityPnLPanel({ dateRange, setDateRange }) {
  const [sub, setSub] = nS('table'); // table | groups | rebates
  const [source, setSource] = nS('client'); // client | coverage
  const rows = source === 'client' ? window.EQUITY_PNL_CLIENTS : window.EQUITY_PNL_COVERAGE;

  const totals = nM(() => {
    return rows.reduce((acc, r) => ({
      beginEq: acc.beginEq + r.beginEq, netDepW: acc.netDepW + r.netDepW, netCred: acc.netCred + r.netCred,
      commReb: acc.commReb + r.commReb, spreadReb: acc.spreadReb + r.spreadReb, adj: acc.adj + r.adj, ps: acc.ps + r.ps,
      supposedEq: acc.supposedEq + r.supposedEq, currentEq: acc.currentEq + r.currentEq, pl: acc.pl + r.pl, netPl: acc.netPl + r.netPl,
    }), { beginEq: 0, netDepW: 0, netCred: 0, commReb: 0, spreadReb: 0, adj: 0, ps: 0, supposedEq: 0, currentEq: 0, pl: 0, netPl: 0 });
  }, [rows]);

  const brokerEdge = nM(() => {
    const cli = window.EQUITY_PNL_CLIENTS.reduce((s, r) => s + r.netPl, 0);
    const cov = window.EQUITY_PNL_COVERAGE.reduce((s, r) => s + r.netPl, 0);
    return -cli + cov;
  }, []);

  return (
    <div>
      <div className="toolbar">
        <div className="segmented">
          <button className={sub === 'table' ? 'active' : ''} onClick={() => setSub('table')}>Table</button>
          <button className={sub === 'groups' ? 'active' : ''} onClick={() => setSub('groups')}>Login Groups</button>
          <button className={sub === 'rebates' ? 'active' : ''} onClick={() => setSub('rebates')}>Spread Rebates</button>
        </div>
        <div className="sep"/>
        {sub === 'table' && (
          <div className="segmented">
            <button className={source === 'client' ? 'active' : ''} onClick={() => setSource('client')}>Clients ({window.EQUITY_PNL_CLIENTS.length})</button>
            <button className={source === 'coverage' ? 'active' : ''} onClick={() => setSource('coverage')}>Coverage ({window.EQUITY_PNL_COVERAGE.length})</button>
          </div>
        )}
        <div className="spacer"/>
        <window.DateRangePicker value={dateRange} onChange={setDateRange}/>
        <div className="sep"/>
        <div className="edge-pill">
          <span className="t3">Broker edge</span>
          <span className={`mono ${pc(brokerEdge)}`} style={{ fontWeight: 700 }}>{fp(brokerEdge)}</span>
        </div>
      </div>

      {sub === 'table' && (
        <div className="card" style={{ overflow: 'auto' }}>
          <table className="ep-table">
            <thead>
              <tr>
                <th className="sticky-col">Login</th>
                <th>Group</th>
                <th className="num">Begin Eq</th>
                <th className="num">Net Dep/W</th>
                <th className="num">Net Cred</th>
                <th className="num">Comm Reb</th>
                <th className="num">Spread Reb</th>
                <th className="num">Adj</th>
                <th className="num">PS</th>
                <th className="num">Supposed Eq</th>
                <th className="num">Current Eq</th>
                <th className="num">PL</th>
                <th className="num bold">Net PL</th>
              </tr>
            </thead>
            <tbody>
              {rows.map(r => (
                <tr key={r.login}>
                  <td className="sticky-col mono">{r.login}</td>
                  <td><span className={`chip sm ${groupColor(r.group)}`}>{r.group}</span></td>
                  <td className="num mono">{fmt(r.beginEq)}</td>
                  <td className={`num mono ${pc(r.netDepW)}`}>{r.netDepW === 0 ? '—' : fp(r.netDepW)}</td>
                  <td className={`num mono ${pc(r.netCred)}`}>{r.netCred === 0 ? '—' : fp(r.netCred)}</td>
                  <td className="num mono t3">{r.commReb === 0 ? '—' : fmt(r.commReb)}</td>
                  <td className="num mono t3">{r.spreadReb === 0 ? '—' : fmt(r.spreadReb)}</td>
                  <td className="num mono t3">{r.adj === 0 ? '—' : fmt(r.adj)}</td>
                  <td className={`num mono ${r.ps > 0 ? 'pos' : 't3'}`}>{r.ps === 0 ? '—' : fmt(r.ps)}</td>
                  <td className="num mono">{fmt(r.supposedEq)}</td>
                  <td className="num mono">{fmt(r.currentEq)}</td>
                  <td className={`num mono ${pc(r.pl)}`}>{fp(r.pl)}</td>
                  <td className={`num mono bold ${pc(r.netPl)}`}>{fp(r.netPl)}</td>
                </tr>
              ))}
              <tr className="ep-total">
                <td className="sticky-col">TOTAL</td>
                <td>—</td>
                <td className="num mono">{fmt(totals.beginEq)}</td>
                <td className={`num mono ${pc(totals.netDepW)}`}>{fp(totals.netDepW)}</td>
                <td className={`num mono ${pc(totals.netCred)}`}>{fp(totals.netCred)}</td>
                <td className="num mono t2">{fmt(totals.commReb)}</td>
                <td className="num mono t2">{fmt(totals.spreadReb)}</td>
                <td className="num mono t2">{fmt(totals.adj)}</td>
                <td className={`num mono ${totals.ps > 0 ? 'pos' : 't2'}`}>{fmt(totals.ps)}</td>
                <td className="num mono">{fmt(totals.supposedEq)}</td>
                <td className="num mono">{fmt(totals.currentEq)}</td>
                <td className={`num mono ${pc(totals.pl)}`}>{fp(totals.pl)}</td>
                <td className={`num mono bold ${pc(totals.netPl)}`}>{fp(totals.netPl)}</td>
              </tr>
            </tbody>
          </table>
        </div>
      )}

      {sub === 'groups' && <LoginGroupsConfig/>}
      {sub === 'rebates' && <SpreadRebatesConfig/>}
    </div>
  );
}
window.EquityPnLPanel = EquityPnLPanel;

function groupColor(name) {
  const m = { 'VIP-TierA': 'amber', 'IB-Lebanon': 'blue', 'Retail': 'teal', 'Cov-Centroid': 'gray' };
  return m[name] || 'gray';
}

function LoginGroupsConfig() {
  return (
    <div className="card">
      <div className="card-head">
        <span className="card-title">Login Groups</span>
        <span className="t3 sm">Per-group rebate/PS config. Login-specific overrides always win.</span>
        <div className="spacer"/>
        <button className="icon-btn"><I.plus/> New group</button>
      </div>
      <table className="ep-table">
        <thead>
          <tr>
            <th>Group</th>
            <th className="num">Members</th>
            <th className="num">Comm %</th>
            <th className="num">PS %</th>
            <th className="num">Spread rate ($/lot)</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {window.LOGIN_GROUPS.map(g => (
            <tr key={g.id}>
              <td><span className={`chip ${g.color}`}>{g.name}</span></td>
              <td className="num mono">{g.members}</td>
              <td className="num mono">{g.commPct.toFixed(2)}%</td>
              <td className="num mono">{g.psPct}%</td>
              <td className="num mono">${g.spreadRate.toFixed(2)}</td>
              <td className="num"><button className="icon-btn ghost sm">Edit</button></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function SpreadRebatesConfig() {
  return (
    <div className="card">
      <div className="card-head">
        <span className="card-title">Spread Rebates</span>
        <span className="t3 sm">Per-(login/group, symbol) rate. Login overrides group.</span>
        <div className="spacer"/>
        <button className="icon-btn"><I.plus/> New rate</button>
      </div>
      <table className="ep-table">
        <thead>
          <tr>
            <th>Scope</th>
            <th>Target</th>
            <th>Symbol</th>
            <th className="num">Rate ($/lot)</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {window.SPREAD_REBATES.map(r => (
            <tr key={r.id}>
              <td><span className={`chip sm ${r.scope === 'login' ? 'blue' : 'teal'}`}>{r.scope}</span></td>
              <td className="mono">{r.login || r.group}</td>
              <td className="mono">{r.sym}</td>
              <td className="num mono">${r.rate.toFixed(2)}</td>
              <td className="num"><button className="icon-btn ghost sm">Edit</button></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// =================================================================
// Markup Tab
// =================================================================
function MarkupPanel({ dateRange, setDateRange }) {
  const totals = nM(() => window.MARKUP.reduce((a, r) => ({
    cliDeals: a.cliDeals + r.cliDeals, covDeals: a.covDeals + r.covDeals, mkUsd: a.mkUsd + r.mkUsd,
    cliPnL: a.cliPnL + r.cliPnL, covPnL: a.covPnL + r.covPnL,
  }), { cliDeals: 0, covDeals: 0, mkUsd: 0, cliPnL: 0, covPnL: 0 }), []);
  const maxAbs = Math.max(...window.MARKUP.map(r => Math.abs(r.mkUsd)));
  return (
    <div>
      <div className="toolbar">
        <div className="card-title">Broker Markup · client vs coverage VWAP</div>
        <span className="t3 sm">Positive edge = broker captured price spread on the way to LP.</span>
        <div className="spacer"/>
        <window.DateRangePicker value={dateRange} onChange={setDateRange}/>
        <div className="sep"/>
        <div className="edge-pill">
          <span className="t3">Total markup</span>
          <span className={`mono ${pc(totals.mkUsd)}`} style={{ fontWeight: 700 }}>{fp(totals.mkUsd)}</span>
        </div>
      </div>
      <div className="card" style={{ overflow: 'auto' }}>
        <table className="ep-table">
          <thead>
            <tr>
              <th>Symbol</th>
              <th className="num">Cli Deals</th>
              <th className="num">Cov Deals</th>
              <th className="num">Cli VWAP</th>
              <th className="num">Cov VWAP</th>
              <th className="num">Pip Edge</th>
              <th>Edge distribution</th>
              <th className="num">Markup $</th>
              <th className="num">Client P&L</th>
              <th className="num">Coverage P&L</th>
            </tr>
          </thead>
          <tbody>
            {window.MARKUP.map(r => {
              const pct = Math.max(5, Math.abs(r.mkUsd) / maxAbs * 100);
              const pos = r.mkUsd >= 0;
              return (
                <tr key={r.sym}>
                  <td className="mono bold">{r.sym}</td>
                  <td className="num mono">{r.cliDeals}</td>
                  <td className="num mono">{r.covDeals}</td>
                  <td className="num mono">{r.cliVWAP}</td>
                  <td className="num mono">{r.covVWAP}</td>
                  <td className={`num mono ${pc(r.pipEdge)}`}>{r.pipEdge > 0 ? '+' : ''}{r.pipEdge.toFixed(1)}</td>
                  <td>
                    <div className="mk-bar-wrap">
                      <div className={`mk-bar ${pos ? 'pos' : 'neg'}`} style={{ width: `${pct}%`, marginLeft: pos ? '50%' : `${50 - pct}%` }}/>
                      <div className="mk-bar-axis"/>
                    </div>
                  </td>
                  <td className={`num mono bold ${pc(r.mkUsd)}`}>{fp(r.mkUsd)}</td>
                  <td className={`num mono ${pc(r.cliPnL)}`}>{fp(r.cliPnL)}</td>
                  <td className={`num mono ${pc(r.covPnL)}`}>{fp(r.covPnL)}</td>
                </tr>
              );
            })}
            <tr className="ep-total">
              <td>TOTAL</td>
              <td className="num mono">{totals.cliDeals}</td>
              <td className="num mono">{totals.covDeals}</td>
              <td className="num">—</td>
              <td className="num">—</td>
              <td className="num">—</td>
              <td></td>
              <td className={`num mono bold ${pc(totals.mkUsd)}`}>{fp(totals.mkUsd)}</td>
              <td className={`num mono ${pc(totals.cliPnL)}`}>{fp(totals.cliPnL)}</td>
              <td className={`num mono ${pc(totals.covPnL)}`}>{fp(totals.covPnL)}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  );
}
window.MarkupPanel = MarkupPanel;

// =================================================================
// Positions Compare Tab (split layout)
// =================================================================
function PositionsComparePanel({ dateRange, setDateRange }) {
  const [sel, setSel] = nS('XAUUSD');
  const [leftW, setLeftW] = nS(() => +(localStorage.getItem('cm-cmp-left') || 420));
  const resize = (dx) => {
    const w = Math.min(720, Math.max(320, leftW + dx));
    setLeftW(w);
    localStorage.setItem('cm-cmp-left', w);
  };
  const dragRef = nR(null);
  const startDrag = (e) => {
    const startX = e.clientX;
    const startW = leftW;
    const onMove = (ev) => {
      const w = Math.min(720, Math.max(320, startW + (ev.clientX - startX)));
      setLeftW(w);
    };
    const onUp = () => {
      document.removeEventListener('mousemove', onMove);
      document.removeEventListener('mouseup', onUp);
      localStorage.setItem('cm-cmp-left', String(leftW));
    };
    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup', onUp);
  };
  const row = window.COMPARE_SUMMARY.find(r => r.sym === sel);
  return (
    <div>
      <div className="toolbar">
        <div className="card-title">Positions Compare</div>
        <span className="t3 sm">Client vs coverage — hedge ratios, exposure deltas, P&L edge.</span>
        <div className="spacer"/>
        <window.DateRangePicker value={dateRange} onChange={setDateRange}/>
      </div>
      <div className="cmp-split">
        <div className="cmp-left" style={{ width: leftW }}>
          <div className="cmp-left-head">
            <div className="grid-head" style={{ gridTemplateColumns: '1fr 60px 1fr 1fr' }}>
              <div>Symbol</div><div className="num">Hedge</div><div className="num">Net Δ</div><div className="num">P&L Δ</div>
            </div>
          </div>
          {window.COMPARE_SUMMARY.map(r => (
            <div key={r.sym} className={`cmp-row ${sel === r.sym ? 'active' : ''}`} onClick={() => setSel(r.sym)}>
              <div className="cmp-row-grid">
                <div>
                  <div className="mono bold" style={{ fontSize: 13 }}>{r.sym}</div>
                  <div className="mono t3" style={{ fontSize: 10.5 }}>CLI {fp(r.cliNet)} · COV {fp(r.covNet)}</div>
                </div>
                <div className="num">
                  <span className={`hedge-chip ${r.hedge >= 80 ? 'ok' : r.hedge >= 50 ? 'warn' : 'bad'}`}>{r.hedge}%</span>
                </div>
                <div className={`num mono ${pc(r.netD)}`}>{fp(r.netD)}</div>
                <div className={`num mono ${pc(r.plD)}`}>{fp(r.plD)}</div>
              </div>
              <HedgeBar ratio={r.hedge}/>
            </div>
          ))}
        </div>
        <div className="cmp-gutter" onMouseDown={startDrag}/>
        <div className="cmp-right">
          {row && (
            <>
              <div className="cmp-detail-head">
                <window.AssetMark sym={row.sym} cls="fx"/>
                <div>
                  <div className="mono" style={{ fontSize: 18, fontWeight: 700 }}>{row.sym}</div>
                  <div className="t3 sm">{row.hedge}% hedged · Net Δ {fp(row.netD)} lots</div>
                </div>
                <div className="spacer"/>
                <button className="icon-btn primary"><I.bolt/> Hedge</button>
              </div>
              <div className="cmp-cards">
                <CmpCard label="Client P&L" val={fp(row.cliPL)} tone={pc(row.cliPL)}/>
                <CmpCard label="Coverage P&L" val={fp(row.covPL)} tone={pc(row.covPL)}/>
                <CmpCard label="Broker Edge" val={fp(row.plD)} tone={pc(row.plD)} big/>
                <CmpCard label="Client Net" val={fp(row.cliNet)} tone={pc(row.cliNet)} suffix="lots"/>
                <CmpCard label="Coverage Net" val={fp(row.covNet)} tone={pc(row.covNet)} suffix="lots"/>
              </div>
              <div className="cmp-rings">
                <PnLRings floating={{ cli: row.cliPL * 0.6, cov: row.covPL * 0.6 }} settled={{ cli: row.cliPL * 0.4, cov: row.covPL * 0.4 }}/>
              </div>
              <div className="cmp-compare-table">
                <div className="card-head"><span className="card-title">Detail comparison</span></div>
                <table className="ep-table">
                  <thead><tr><th>Metric</th><th className="num">Client</th><th className="num">Coverage</th><th className="num">Δ (edge)</th></tr></thead>
                  <tbody>
                    <tr><td>Deals</td><td className="num mono">142</td><td className="num mono">92</td><td className="num mono">−50</td></tr>
                    <tr><td>Volume (lots)</td><td className="num mono">{fmt(Math.abs(row.cliNet) * 2.4)}</td><td className="num mono">{fmt(Math.abs(row.covNet) * 2.4)}</td><td className="num mono">{fp((Math.abs(row.cliNet) - Math.abs(row.covNet)) * 2.4)}</td></tr>
                    <tr><td>Win rate</td><td className="num mono">52%</td><td className="num mono">61%</td><td className={`num mono pos`}>+9 pp</td></tr>
                    <tr><td>Avg slippage</td><td className="num mono">0.4 pip</td><td className="num mono">0.8 pip</td><td className={`num mono neg`}>−0.4 pip</td></tr>
                    <tr><td>Realized P&L</td><td className={`num mono ${pc(row.cliPL * 0.4)}`}>{fp(row.cliPL * 0.4)}</td><td className={`num mono ${pc(row.covPL * 0.4)}`}>{fp(row.covPL * 0.4)}</td><td className={`num mono bold ${pc(row.plD * 0.4)}`}>{fp(row.plD * 0.4)}</td></tr>
                    <tr><td>Floating P&L</td><td className={`num mono ${pc(row.cliPL * 0.6)}`}>{fp(row.cliPL * 0.6)}</td><td className={`num mono ${pc(row.covPL * 0.6)}`}>{fp(row.covPL * 0.6)}</td><td className={`num mono bold ${pc(row.plD * 0.6)}`}>{fp(row.plD * 0.6)}</td></tr>
                  </tbody>
                </table>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
window.PositionsComparePanel = PositionsComparePanel;

function CmpCard({ label, val, tone, big, suffix }) {
  return (
    <div className="cmp-card">
      <div className="cmp-card-lbl">{label}</div>
      <div className={`cmp-card-val mono ${tone} ${big ? 'big' : ''}`}>{val} {suffix && <span className="t3 sm">{suffix}</span>}</div>
    </div>
  );
}

// Concentric P&L rings (inner = floating, outer = settled)
function PnLRings({ floating, settled }) {
  const size = 220;
  const cx = size / 2, cy = size / 2;
  const rOuter = 90, rInner = 62;
  const max = Math.max(Math.abs(floating.cli), Math.abs(floating.cov), Math.abs(settled.cli), Math.abs(settled.cov), 1);
  const arc = (r, val, start, color) => {
    const norm = Math.min(1, Math.abs(val) / max);
    const angle = 180 * norm;
    const end = start + (val >= 0 ? angle : -angle);
    const rad = (a) => a * Math.PI / 180;
    const x1 = cx + r * Math.cos(rad(start)), y1 = cy + r * Math.sin(rad(start));
    const x2 = cx + r * Math.cos(rad(end)),   y2 = cy + r * Math.sin(rad(end));
    const large = Math.abs(end - start) > 180 ? 1 : 0;
    const sweep = end > start ? 1 : 0;
    const d = `M ${x1} ${y1} A ${r} ${r} 0 ${large} ${sweep} ${x2} ${y2}`;
    return <path d={d} fill="none" stroke={color} strokeWidth="10" strokeLinecap="round"/>;
  };
  const brokerEdgeF = floating.cov - floating.cli;
  const brokerEdgeS = settled.cov - settled.cli;
  const brokerNet = brokerEdgeF + brokerEdgeS;
  return (
    <div className="rings-wrap">
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
        <circle cx={cx} cy={cy} r={rOuter} fill="none" stroke="var(--bg-3)" strokeWidth="10"/>
        <circle cx={cx} cy={cy} r={rInner} fill="none" stroke="var(--bg-3)" strokeWidth="10"/>
        {arc(rOuter, settled.cli,  180, 'var(--blue)')}
        {arc(rOuter, settled.cov,    0, 'var(--teal)')}
        {arc(rInner, floating.cli, 180, 'var(--blue-dim2)')}
        {arc(rInner, floating.cov,   0, 'var(--teal-dim2)')}
        <text x={cx} y={cy - 4} textAnchor="middle" fontSize="10" fill="var(--t3)" className="mono">BROKER NET</text>
        <text x={cx} y={cy + 14} textAnchor="middle" fontSize="16" fontWeight="700" fill={brokerNet >= 0 ? 'var(--green)' : 'var(--red)'} className="mono">{fp(brokerNet)}</text>
      </svg>
      <div className="rings-legend">
        <div className="rings-row"><span className="rings-dot" style={{ background: 'var(--blue)' }}/>Client · settled<b className={`mono ${pc(settled.cli)}`}>{fp(settled.cli)}</b></div>
        <div className="rings-row"><span className="rings-dot" style={{ background: 'var(--teal)' }}/>Coverage · settled<b className={`mono ${pc(settled.cov)}`}>{fp(settled.cov)}</b></div>
        <div className="rings-row"><span className="rings-dot" style={{ background: 'var(--blue-dim2)' }}/>Client · floating<b className={`mono ${pc(floating.cli)}`}>{fp(floating.cli)}</b></div>
        <div className="rings-row"><span className="rings-dot" style={{ background: 'var(--teal-dim2)' }}/>Coverage · floating<b className={`mono ${pc(floating.cov)}`}>{fp(floating.cov)}</b></div>
        <div className="rings-divider"/>
        <div className="rings-row"><span style={{ width: 10 }}/>Edge floating<b className={`mono ${pc(brokerEdgeF)}`}>{fp(brokerEdgeF)}</b></div>
        <div className="rings-row"><span style={{ width: 10 }}/>Edge settled<b className={`mono ${pc(brokerEdgeS)}`}>{fp(brokerEdgeS)}</b></div>
      </div>
    </div>
  );
}

function HedgeBar({ ratio }) {
  const tone = ratio >= 80 ? 'ok' : ratio >= 50 ? 'warn' : 'bad';
  const w = Math.min(100, ratio);
  return (
    <div className="hedge-bar">
      <div className={`hedge-bar-fill ${tone}`} style={{ width: `${w}%` }}/>
    </div>
  );
}
