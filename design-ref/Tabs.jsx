/* global React, I */
// Secondary tabs for the Coverage Manager shell.
const { useState: useSt, useMemo: useMem, useEffect: useEf, useRef: useRf } = React;
const fmt = window.fmt, fp = window.fp, pc = window.pc;
const AssetMark = window.AssetMark, HedgeBar = window.HedgeBar;

// ========== Stat Card with sparkline ==========
function Sparkline({ points, color, width = 80, height = 22 }) {
  const min = Math.min(...points), max = Math.max(...points);
  const span = max - min || 1;
  const path = points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${(i / (points.length - 1)) * width} ${height - ((p - min) / span) * height}`).join(' ');
  return (
    <svg className="spark" width={width} height={height}>
      <path d={path} fill="none" stroke={color} strokeWidth={1.4} strokeLinecap="round" />
    </svg>
  );
}

function StatCard({ label, value, delta, sparkColor, sparkPoints, accent }) {
  return (
    <div className="stat-card">
      <div className="lbl">{label}</div>
      <div className="val" style={{ color: accent || 'var(--t1)' }}>{value}</div>
      {delta !== undefined && (
        <div className={`delta ${pc(delta)}`}>{delta >= 0 ? '+' : ''}{fmt(delta)} <span className="t3">vs prev</span></div>
      )}
      {sparkPoints && <Sparkline points={sparkPoints} color={sparkColor || 'var(--t3)'} />}
    </div>
  );
}

// ========== Positions Tab ==========
function PositionsPanel() {
  const [filter, setFilter] = useSt('all');
  const POS = window.POSITIONS;
  const rows = POS.filter(p => filter === 'all' || p.source === filter);
  return (
    <>
      <div className="toolbar">
        <div className="segmented">
          <button className={filter === 'all' ? 'active' : ''} onClick={() => setFilter('all')}>All ({POS.length})</button>
          <button className={filter === 'bbook' ? 'active' : ''} onClick={() => setFilter('bbook')}>Clients ({POS.filter(p=>p.source==='bbook').length})</button>
          <button className={filter === 'coverage' ? 'active' : ''} onClick={() => setFilter('coverage')}>Coverage ({POS.filter(p=>p.source==='coverage').length})</button>
        </div>
        <div className="sep"/>
        <div className="search-wrap">
          <I.search />
          <input className="search" placeholder="Filter by symbol or login" />
        </div>
        <div className="spacer"/>
        <button className="icon-btn"><I.download /> Export</button>
      </div>
      <div className="exposure">
        <table>
          <thead>
            <tr>
              <th style={{ textAlign: 'left', paddingLeft: 16 }}>Login</th>
              <th style={{ textAlign: 'left' }}>Source</th>
              <th style={{ textAlign: 'left' }}>Symbol</th>
              <th>Direction</th>
              <th>Volume</th>
              <th>Open Price</th>
              <th>Current</th>
              <th>P&L</th>
              <th style={{ textAlign: 'right', paddingRight: 16 }}>Open Time</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((p, i) => (
              <tr key={i} className="open-row" style={{ borderBottom: '1px solid var(--divider)' }}>
                <td className="mono" style={{ textAlign: 'left', paddingLeft: 16 }}>{p.login}</td>
                <td style={{ textAlign: 'left' }}>
                  <span className={`tag ${p.source === 'bbook' ? 'cli' : 'cov'}`}>{p.source === 'bbook' ? 'CLIENT' : 'COVERAGE'}</span>
                </td>
                <td className="mono" style={{ textAlign: 'left' }}>
                  <span style={{ color: 'var(--t1)', fontWeight: 600 }}>{p.sym}</span>
                  <span className="t3" style={{ marginLeft: 8, fontSize: 10.5 }}>{p.canonical}</span>
                </td>
                <td><span className={`tag ${p.dir.toLowerCase()}`}>{p.dir}</span></td>
                <td className="mono">{fmt(p.vol)}</td>
                <td className="mono t2">{p.openPrice}</td>
                <td className="mono">{p.currentPrice}</td>
                <td className={`mono ${pc(p.pnl)}`} style={{ fontWeight: 600 }}>{fp(p.pnl)}</td>
                <td className="mono t3" style={{ textAlign: 'right', paddingRight: 16 }}>{p.openTime}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}
window.PositionsPanel = PositionsPanel;

// ========== P&L Tab ==========
function PnLPanel() {
  const [showCov, setShowCov] = useSt(true);
  const rows = window.SYMBOLS.map(s => {
    const c = window.CLOSED_PNL[s.sym];
    const e = window.EXPOSURE[s.sym];
    return { sym: s.sym, asset: s.asset, cls: s.cls, vol: (Math.abs(e.bbBuy) + Math.abs(e.bbSell)) * 0.4, bbPnL: c.bb, covPnL: c.cov, combined: c.bb + c.cov };
  });
  const tot = rows.reduce((a, r) => ({ vol: a.vol + r.vol, bb: a.bb + r.bbPnL, cov: a.cov + r.covPnL, comb: a.comb + r.combined }), { vol: 0, bb: 0, cov: 0, comb: 0 });
  return (
    <>
      <div className="toolbar">
        <span className="t3" style={{ fontSize: 11, fontWeight: 600, letterSpacing: '0.06em' }}>FROM</span>
        <input type="date" className="date-input" defaultValue="2026-04-18"/>
        <span className="t3" style={{ fontSize: 11, fontWeight: 600, letterSpacing: '0.06em' }}>TO</span>
        <input type="date" className="date-input" defaultValue="2026-04-18"/>
        <button className="icon-btn primary">Load</button>
        <div className="sep"/>
        <label style={{ display: 'flex', gap: 6, alignItems: 'center', fontSize: 12, color: 'var(--t2)', fontWeight: 500 }}>
          <span className={`switch ${showCov ? 'on' : ''}`} onClick={() => setShowCov(!showCov)}/>
          Coverage
        </label>
        <div className="spacer"/>
        <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
          <div className="t3" style={{ fontSize: 11 }}>
            {rows.reduce((a,r)=>a+r.vol,0).toFixed(0)} deals · {rows.length} symbols
          </div>
          <div className="mono" style={{ fontSize: 20, fontWeight: 700, color: tot.bb >= 0 ? 'var(--green)' : 'var(--red)' }}>
            {fp(tot.bb)}
          </div>
        </div>
      </div>
      <div className="exposure">
        <table>
          <thead>
            <tr>
              <th style={{ textAlign: 'left', paddingLeft: 16 }}>Symbol</th>
              <th style={{ textAlign: 'right' }}>Volume</th>
              <th style={{ textAlign: 'right' }}>Profit</th>
              <th style={{ textAlign: 'right' }}>Commission</th>
              <th style={{ textAlign: 'right' }}>Swap</th>
              <th style={{ textAlign: 'right' }}>Net P&L</th>
              {showCov && <><th className="sec-l" style={{ textAlign: 'right' }}>Cov P&L</th>
              <th style={{ textAlign: 'right', paddingRight: 16 }}>Combined</th></>}
            </tr>
          </thead>
          <tbody>
            {rows.map(r => (
              <tr key={r.sym} className="open-row" style={{ borderBottom: '1px solid var(--divider)' }}>
                <td className="sym-cell" style={{ paddingLeft: 16, padding: '8px 16px', textAlign: 'left' }}>
                  <AssetMark sym={r.sym} cls={r.cls} />
                  <span style={{ fontFamily: 'var(--font-ui)', fontWeight: 600 }}>{r.sym}</span>
                  <span className="t3" style={{ marginLeft: 8, fontSize: 10.5, fontFamily: 'var(--font-ui)' }}>{r.asset}</span>
                </td>
                <td className="mono t2" style={{ textAlign: 'right' }}>{fmt(r.vol)}</td>
                <td className={`mono ${pc(r.bbPnL * 1.2)}`} style={{ textAlign: 'right' }}>{fp(r.bbPnL * 1.2)}</td>
                <td className="mono neg" style={{ textAlign: 'right' }}>{fp(-Math.abs(r.bbPnL) * 0.05 - 12)}</td>
                <td className="mono t2" style={{ textAlign: 'right' }}>{fp((Math.random()-0.5) * 20)}</td>
                <td className={`mono ${pc(r.bbPnL)}`} style={{ textAlign: 'right', fontWeight: 700 }}>{fp(r.bbPnL)}</td>
                {showCov && <><td className={`mono sec-l ${pc(r.covPnL)}`} style={{ textAlign: 'right' }}>{fp(r.covPnL)}</td>
                <td className={`mono ${pc(r.combined)}`} style={{ textAlign: 'right', paddingRight: 16, fontWeight: 600 }}>{fp(r.combined)}</td></>}
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="total-row">
              <td style={{ textAlign: 'left', paddingLeft: 16, fontFamily: 'var(--font-ui)' }}>TOTAL</td>
              <td className="mono t2" style={{ textAlign: 'right' }}>{fmt(tot.vol)}</td>
              <td className="mono t2" style={{ textAlign: 'right' }}>{fp(tot.bb * 1.2)}</td>
              <td className="mono neg" style={{ textAlign: 'right' }}>{fp(-120)}</td>
              <td className="mono t2" style={{ textAlign: 'right' }}>{fp(24)}</td>
              <td className={`mono ${pc(tot.bb)}`} style={{ textAlign: 'right', fontWeight: 700, fontSize: 13 }}>{fp(tot.bb)}</td>
              {showCov && <><td className={`mono sec-l ${pc(tot.cov)}`} style={{ textAlign: 'right' }}>{fp(tot.cov)}</td>
              <td className={`mono ${pc(tot.comb)}`} style={{ textAlign: 'right', paddingRight: 16, fontWeight: 700 }}>{fp(tot.comb)}</td></>}
            </tr>
          </tfoot>
        </table>
      </div>
    </>
  );
}
window.PnLPanel = PnLPanel;

// ========== Net P&L Tab ==========
function NetPnLPanel() {
  const rows = window.SYMBOLS.map(s => {
    const e = window.EXPOSURE[s.sym];
    const c = window.CLOSED_PNL[s.sym];
    const beginFloat = -e.bbPnL * 0.6;
    const currFloat = -e.bbPnL;
    const delta = currFloat - beginFloat;
    const net = delta + c.bb;
    return { sym: s.sym, asset: s.asset, cls: s.cls, begin: beginFloat, curr: currFloat, delta, settled: c.bb, net, covBegin: e.covPnL * 0.5, covCurr: e.covPnL, covSettled: c.cov, covNet: e.covPnL * 0.5 + c.cov, edgeNet: (e.covPnL * 0.5 + c.cov) - net };
  });
  const tot = rows.reduce((a, r) => ({ begin: a.begin + r.begin, curr: a.curr + r.curr, delta: a.delta + r.delta, settled: a.settled + r.settled, net: a.net + r.net, covNet: a.covNet + r.covNet, edgeNet: a.edgeNet + r.edgeNet }), { begin:0, curr:0, delta:0, settled:0, net:0, covNet:0, edgeNet:0 });
  return (
    <div className="panel-wrap">
      <div className="row-flex between" style={{ gap: 16 }}>
        <div className="row-flex" style={{ gap: 10 }}>
          <span className="t3" style={{ fontSize: 11, fontWeight: 600, letterSpacing: '0.06em' }}>PERIOD</span>
          <input type="date" className="date-input" defaultValue="2026-04-18"/>
          <span className="t3">→</span>
          <input type="date" className="date-input" defaultValue="2026-04-18"/>
          <span className="chip" style={{ marginLeft: 6 }}>
            <span className="dot"/> Asia/Beirut
          </span>
        </div>
        <div className="row-flex" style={{ gap: 8 }}>
          <button className="icon-btn"><I.camera /> Capture snapshot</button>
          <button className="icon-btn"><I.refresh /></button>
        </div>
      </div>
      <div className="panel-grid">
        <StatCard label="Edge Net P&L" value={fp(tot.edgeNet)} accent="var(--teal)" sparkColor="var(--teal)" sparkPoints={[3,5,4,7,9,6,8,12,14,11,13,16]} />
        <StatCard label="Broker Net" value={fp(tot.net)} delta={tot.delta} sparkColor="var(--blue)" sparkPoints={[8,7,9,6,10,12,14,11,13,10,12,15]} />
        <StatCard label="Floating Δ" value={fp(tot.delta)} sparkColor={tot.delta >= 0 ? 'var(--green)' : 'var(--red)'} sparkPoints={[4,6,5,7,9,12,10,13,15,12,14,17]} />
        <StatCard label="Settled" value={fp(tot.settled)} sparkColor="var(--amber)" sparkPoints={[2,4,3,5,7,9,11,10,13,12,15,18]} />
      </div>
      <div className="card">
        <div className="card-header">
          <div className="card-title">Period P&L breakdown</div>
          <div className="spacer"/>
          <span className="chip"><I.info /> Floating Δ + Settled = Net</span>
        </div>
        <div className="exposure" style={{ maxHeight: 'none' }}>
          <table>
            <thead>
              <tr>
                <th style={{ textAlign: 'left', paddingLeft: 16 }} rowSpan={2}>Symbol</th>
                <th className="group clients sec-l" colSpan={5}>B-BOOK (CLIENT PERSPECTIVE, INVERTED)</th>
                <th className="group coverage sec-l" colSpan={3}>COVERAGE</th>
                <th className="group summary sec-l">EDGE</th>
              </tr>
              <tr className="sub">
                <th className="sec-l">Begin Float</th><th>Current</th><th>Δ Float</th><th>Settled</th><th>Net</th>
                <th className="sec-l">Current</th><th>Settled</th><th>Net</th>
                <th className="sec-l">Net</th>
              </tr>
            </thead>
            <tbody>
              {rows.map(r => (
                <tr key={r.sym} className="open-row">
                  <td className="sym-cell" style={{ paddingLeft: 16 }}>
                    <AssetMark sym={r.sym} cls={r.cls} />
                    <span style={{ fontFamily: 'var(--font-ui)', fontWeight: 600 }}>{r.sym}</span>
                  </td>
                  <td className="mono t2 sec-l">{fp(r.begin)}</td>
                  <td className={`mono ${pc(r.curr)}`}>{fp(r.curr)}</td>
                  <td className={`mono ${pc(r.delta)}`}>{fp(r.delta)}</td>
                  <td className={`mono ${pc(r.settled)}`}>{fp(r.settled)}</td>
                  <td className={`mono ${pc(r.net)}`} style={{ fontWeight: 700 }}>{fp(r.net)}</td>
                  <td className={`mono sec-l ${pc(r.covCurr)}`}>{fp(r.covCurr)}</td>
                  <td className={`mono ${pc(r.covSettled)}`}>{fp(r.covSettled)}</td>
                  <td className={`mono ${pc(r.covNet)}`} style={{ fontWeight: 700 }}>{fp(r.covNet)}</td>
                  <td className={`mono sec-l ${pc(r.edgeNet)}`} style={{ fontWeight: 700 }}>{fp(r.edgeNet)}</td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="total-row">
                <td style={{ paddingLeft: 16, textAlign: 'left', fontFamily: 'var(--font-ui)' }}>TOTAL</td>
                <td className="mono t2 sec-l">{fp(tot.begin)}</td>
                <td className={`mono ${pc(tot.curr)}`}>{fp(tot.curr)}</td>
                <td className={`mono ${pc(tot.delta)}`}>{fp(tot.delta)}</td>
                <td className={`mono ${pc(tot.settled)}`}>{fp(tot.settled)}</td>
                <td className={`mono ${pc(tot.net)}`}>{fp(tot.net)}</td>
                <td className="mono sec-l"></td>
                <td className="mono"></td>
                <td className={`mono ${pc(tot.covNet)}`}>{fp(tot.covNet)}</td>
                <td className={`mono sec-l ${pc(tot.edgeNet)}`}>{fp(tot.edgeNet)}</td>
              </tr>
            </tfoot>
          </table>
        </div>
      </div>
    </div>
  );
}
window.NetPnLPanel = NetPnLPanel;

// ========== Compare tab ==========
function ComparePanel() {
  const [sel, setSel] = useSt('XAUUSD');
  const rows = window.SYMBOLS.map(s => {
    const e = window.EXPOSURE[s.sym];
    const bbNet = e.bbBuy - e.bbSell;
    const covNet = e.covBuy - e.covSell;
    const hedge = Math.abs(bbNet) > 0 ? Math.min(999, (Math.abs(covNet) / Math.abs(bbNet)) * 100) : 0;
    return { s, bbNet, covNet, delta: bbNet - covNet, pnlDelta: e.covPnL - e.bbPnL, hedge };
  });
  const selRow = rows.find(r => r.s.sym === sel) || rows[0];
  return (
    <div style={{ display: 'grid', gridTemplateColumns: '380px 1fr', flex: 1, overflow: 'hidden' }}>
      <div style={{ borderRight: '1px solid var(--border)', background: 'var(--bg-2)', overflow: 'auto' }}>
        <div style={{ padding: '10px 14px', borderBottom: '1px solid var(--divider)', display: 'flex', alignItems: 'center', gap: 8 }}>
          <div className="t1" style={{ fontWeight: 600, fontSize: 13 }}>Symbols</div>
          <div className="spacer"/>
          <span className="chip live"><span className="dot"/>Live</span>
        </div>
        {rows.map(r => (
          <div key={r.s.sym} onClick={() => setSel(r.s.sym)} style={{
            padding: '10px 14px',
            borderBottom: '1px solid var(--divider)',
            cursor: 'pointer',
            background: sel === r.s.sym ? 'var(--accent-dim)' : 'transparent',
          }}>
            <div className="row-flex" style={{ gap: 10 }}>
              <AssetMark sym={r.s.sym} cls={r.s.cls}/>
              <div style={{ flex: 1 }}>
                <div style={{ fontWeight: 600, fontSize: 13 }}>{r.s.sym}</div>
                <div className="t3" style={{ fontSize: 10.5 }}>{r.s.asset}</div>
              </div>
              <div style={{ textAlign: 'right' }}>
                <HedgeBar pct={r.hedge}/>
                <div className={`mono ${pc(r.pnlDelta)}`} style={{ fontSize: 11, fontWeight: 600, marginTop: 2 }}>{fp(r.pnlDelta)}</div>
              </div>
            </div>
            <div className="mono" style={{ marginTop: 6, fontSize: 10.5, display: 'flex', gap: 10, color: 'var(--t3)' }}>
              <span>CLI <span className="t2">{fmt(r.bbNet)}</span></span>
              <span>COV <span className="t2">{fmt(r.covNet)}</span></span>
              <span>Δ <span className={pc(-r.delta)}>{fp(-r.delta)}</span></span>
            </div>
          </div>
        ))}
      </div>
      <div className="panel-wrap">
        <div className="row-flex" style={{ gap: 12 }}>
          <AssetMark sym={selRow.s.sym} cls={selRow.s.cls}/>
          <div>
            <div style={{ fontSize: 18, fontWeight: 700, letterSpacing: '-0.01em' }}>{selRow.s.sym}</div>
            <div className="t3" style={{ fontSize: 12 }}>{selRow.s.asset} · {selRow.s.cls.toUpperCase()}</div>
          </div>
          <div className="spacer"/>
          <span className="chip" style={{ background: 'var(--blue-dim)', color: 'var(--blue)', fontSize: 11 }}>CLI <span className="mono">{fmt(selRow.bbNet)}</span></span>
          <span className="chip" style={{ background: 'var(--teal-dim)', color: 'var(--teal)', fontSize: 11 }}>COV <span className="mono">{fmt(selRow.covNet)}</span></span>
          <span className="chip"><HedgeBar pct={selRow.hedge}/></span>
        </div>
        <div className="panel-grid">
          <StatCard label="Avg Entry (CLI)" value={window.SYMBOLS.find(s=>s.sym===sel).bid.toFixed(2)} sparkColor="var(--blue)" sparkPoints={[10,12,14,11,13,15,14,16,15,17,19,18]} />
          <StatCard label="Avg Exit (CLI)" value={(window.SYMBOLS.find(s=>s.sym===sel).bid + 0.8).toFixed(2)} sparkColor="var(--blue)" sparkPoints={[14,13,15,16,14,17,18,17,19,18,20,21]} />
          <StatCard label="Volume (lots)" value={fmt(Math.abs(selRow.bbNet))} sparkColor="var(--amber)" sparkPoints={[3,5,4,7,9,12,10,13,11,14,16,18]} />
          <StatCard label="P&L edge" value={fp(selRow.pnlDelta)} accent={selRow.pnlDelta >= 0 ? 'var(--green)' : 'var(--red)'} sparkColor={selRow.pnlDelta >= 0 ? 'var(--green)' : 'var(--red)'} sparkPoints={[2,4,3,5,8,6,10,13,11,14,17,20]} />
        </div>
        <div className="card" style={{ padding: 16 }}>
          <div className="row-flex between" style={{ marginBottom: 10 }}>
            <div className="card-title">Price timeline</div>
            <div className="row-flex" style={{ gap: 6 }}>
              <div className="segmented">
                <button>1H</button><button className="active">4H</button><button>1D</button><button>1W</button>
              </div>
            </div>
          </div>
          <MockChart sym={selRow.s.sym}/>
        </div>
      </div>
    </div>
  );
}

function MockChart({ sym }) {
  const ref = useRf(null);
  useEf(() => {
    const c = ref.current;
    const W = c.width = c.offsetWidth * 2, H = c.height = 320;
    const ctx = c.getContext('2d');
    ctx.scale(2,2);
    ctx.clearRect(0, 0, W, H);
    const N = 120;
    const base = window.SYMBOLS.find(s => s.sym === sym).bid;
    const pts = [];
    let p = base * 0.995;
    for (let i = 0; i < N; i++) {
      p += (Math.random() - 0.5) * base * 0.003;
      pts.push(p);
    }
    const min = Math.min(...pts) * 0.999, max = Math.max(...pts) * 1.001;
    const ww = c.offsetWidth, hh = H / 2;

    // grid
    ctx.strokeStyle = 'rgba(255,255,255,0.04)';
    ctx.lineWidth = 1;
    for (let y = 0; y < 5; y++) {
      ctx.beginPath();
      const yy = (y / 4) * (hh - 20) + 10;
      ctx.moveTo(0, yy); ctx.lineTo(ww, yy); ctx.stroke();
    }

    // price line
    const grad = ctx.createLinearGradient(0, 0, 0, hh);
    grad.addColorStop(0, 'rgba(45, 212, 191, 0.18)');
    grad.addColorStop(1, 'rgba(45, 212, 191, 0)');
    ctx.fillStyle = grad;
    ctx.beginPath();
    pts.forEach((v, i) => {
      const x = (i / (N-1)) * ww;
      const y = hh - 10 - ((v - min) / (max - min)) * (hh - 20);
      i === 0 ? ctx.moveTo(x, y) : ctx.lineTo(x, y);
    });
    ctx.lineTo(ww, hh); ctx.lineTo(0, hh); ctx.closePath(); ctx.fill();

    ctx.strokeStyle = '#2DD4BF';
    ctx.lineWidth = 1.4;
    ctx.beginPath();
    pts.forEach((v, i) => {
      const x = (i / (N-1)) * ww;
      const y = hh - 10 - ((v - min) / (max - min)) * (hh - 20);
      i === 0 ? ctx.moveTo(x, y) : ctx.lineTo(x, y);
    });
    ctx.stroke();

    // entry markers
    [20, 48, 82].forEach(i => {
      const x = (i / (N-1)) * ww;
      const y = hh - 10 - ((pts[i] - min) / (max - min)) * (hh - 20);
      ctx.fillStyle = '#60A5FA';
      ctx.beginPath(); ctx.arc(x, y, 4, 0, Math.PI*2); ctx.fill();
      ctx.strokeStyle = 'rgba(96,165,250,0.4)'; ctx.beginPath(); ctx.arc(x, y, 7, 0, Math.PI*2); ctx.stroke();
    });
  }, [sym]);
  return <canvas ref={ref} style={{ width: '100%', height: 160 }}/>;
}

window.ComparePanel = ComparePanel;

// ========== Bridge Tab ==========
function BridgePanel() {
  return (
    <>
      <div className="toolbar">
        <span className="t3" style={{ fontSize: 11, fontWeight: 600, letterSpacing: '0.06em' }}>UTC</span>
        <input type="date" className="date-input" defaultValue="2026-04-18"/>
        <span className="t3">→</span>
        <input type="date" className="date-input" defaultValue="2026-04-18"/>
        <div className="sep"/>
        <select className="select"><option>All symbols</option></select>
        <div className="segmented">
          <button className="active">All</button><button>Buy</button><button>Sell</button>
        </div>
        <div className="sep"/>
        <label style={{ display: 'flex', gap: 6, alignItems: 'center', fontSize: 12, color: 'var(--t2)' }}>
          <span className="switch"/> Anomalies only
        </label>
        <div className="spacer"/>
        <span className="chip live"><span className="dot"/>Centroid CS360 · Live</span>
      </div>
      <div className="exposure">
        <table>
          <thead>
            <tr>
              <th style={{ textAlign: 'left', paddingLeft: 16 }}>Time (UTC)</th>
              <th style={{ textAlign: 'left' }}>Symbol</th>
              <th>Side</th>
              <th>Party</th>
              <th>Volume</th>
              <th>Price</th>
              <th>Δ ms</th>
              <th>LP</th>
              <th>Price Edge</th>
              <th style={{ paddingRight: 16 }}>Pips</th>
            </tr>
          </thead>
          <tbody>
            {window.BRIDGE_ROWS.map(b => (
              <React.Fragment key={b.id}>
                <tr className="open-row bridge-row client" style={{ borderTop: '1px solid var(--border)' }}>
                  <td rowSpan={Math.max(1, b.covFills.length) + 1} className="mono" style={{ textAlign: 'left', paddingLeft: 16, verticalAlign: 'top', paddingTop: 10 }}>{b.time}</td>
                  <td rowSpan={Math.max(1, b.covFills.length) + 1} className="mono" style={{ textAlign: 'left', verticalAlign: 'top', paddingTop: 10 }}>
                    <span style={{ fontWeight: 600, color: 'var(--t1)' }}>{b.sym}</span>
                  </td>
                  <td><span className={`tag ${b.side.toLowerCase()}`}>{b.side}</span></td>
                  <td><span className="tag cli">CLIENT</span></td>
                  <td className="mono">{fmt(b.cliVol)}</td>
                  <td className="mono">{b.cliPrice}</td>
                  <td className="mono t3">—</td>
                  <td className="t3">login {b.cliLogin}</td>
                  <td rowSpan={Math.max(1, b.covFills.length) + 1} className={`mono ${pc(b.edge)}`} style={{ verticalAlign: 'top', paddingTop: 10, fontWeight: 600 }}>{b.edge ? fp(b.edge, 4) : '—'}</td>
                  <td rowSpan={Math.max(1, b.covFills.length) + 1} className={`mono ${pc(b.pips)}`} style={{ verticalAlign: 'top', paddingTop: 10, paddingRight: 16, fontWeight: 700 }}>{b.pips ? fp(b.pips, 1) : '—'}</td>
                </tr>
                {b.covFills.length === 0 ? (
                  <tr className="close-row" style={{ background: 'var(--red-dim)' }}>
                    <td></td>
                    <td><span className="chip red">No cov leg</span></td>
                    <td className="mono t3">—</td><td className="mono t3">—</td><td className="mono t3">—</td><td className="t3">anomaly</td>
                  </tr>
                ) : b.covFills.map((f, i) => (
                  <tr key={i} className="bridge-row cov">
                    <td><span className={`tag ${b.side === 'BUY' ? 'sell' : 'buy'}`}>{b.side === 'BUY' ? 'SELL' : 'BUY'}</span></td>
                    <td><span className="tag cov">COV OUT</span></td>
                    <td className="mono">{fmt(f.vol)}</td>
                    <td className="mono">{f.price}</td>
                    <td className={`mono ${f.diffMs <= 500 ? 'pos' : f.diffMs <= 2000 ? 'amb' : 'neg'}`}>{f.diffMs}ms</td>
                    <td className="t3">{f.lp}</td>
                  </tr>
                ))}
              </React.Fragment>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}
window.BridgePanel = BridgePanel;

// ========== Mappings Tab ==========
function MappingsPanel() {
  return (
    <>
      <div className="toolbar">
        <div className="search-wrap">
          <I.search/>
          <input className="search" placeholder="Filter mappings"/>
        </div>
        <div className="sep"/>
        <span className="chip blue">{window.MAPPINGS.length} active</span>
        <div className="spacer"/>
        <button className="icon-btn"><I.plus/> Add mapping</button>
      </div>
      <div className="exposure">
        <table>
          <thead>
            <tr>
              <th style={{ textAlign: 'left', paddingLeft: 16 }}>Canonical</th>
              <th style={{ textAlign: 'left' }}>B-Book symbol</th>
              <th>Contract</th>
              <th style={{ textAlign: 'left' }}>Coverage symbol</th>
              <th>Contract</th>
              <th>Digits</th>
              <th>Currency</th>
              <th style={{ paddingRight: 16 }}>Active</th>
            </tr>
          </thead>
          <tbody>
            {window.MAPPINGS.map(m => {
              const s = window.SYMBOLS.find(x => x.sym === m.canonical);
              return (
                <tr key={m.id} className="open-row" style={{ borderBottom: '1px solid var(--divider)' }}>
                  <td className="sym-cell" style={{ paddingLeft: 16 }}>
                    {s && <AssetMark sym={m.canonical} cls={s.cls}/>}
                    <span style={{ fontFamily: 'var(--font-ui)', fontWeight: 600 }}>{m.canonical}</span>
                  </td>
                  <td className="mono" style={{ textAlign: 'left' }}>{m.bbook}</td>
                  <td className="mono t2">{fmt(m.bbSize, 0)}</td>
                  <td className="mono" style={{ textAlign: 'left' }}>{m.covSym}</td>
                  <td className="mono t2">{fmt(m.covSize, 0)}</td>
                  <td className="mono t2">{m.digits}</td>
                  <td className="t2">{m.ccy}</td>
                  <td style={{ paddingRight: 16 }}><span className={`switch on`}/></td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </>
  );
}
window.MappingsPanel = MappingsPanel;

// ========== Alerts Panel ==========
function AlertsPanel({ alerts, ackAlert, setAlerts }) {
  const [tab, setTab] = useSt('rules');
  return (
    <>
      <div className="toolbar">
        <div className="segmented">
          <button className={tab === 'rules' ? 'active' : ''} onClick={() => setTab('rules')}>Rules ({window.ALERT_RULES.length})</button>
          <button className={tab === 'history' ? 'active' : ''} onClick={() => setTab('history')}>
            History ({alerts.length})
          </button>
          <button className={tab === 'channels' ? 'active' : ''} onClick={() => setTab('channels')}>Channels</button>
        </div>
        <div className="spacer"/>
        {tab === 'rules' && <button className="icon-btn primary"><I.plus/> New rule</button>}
        {tab === 'history' && <button className="icon-btn" onClick={() => setAlerts(alerts.map(a=>({...a, ack: true})))}>Acknowledge all</button>}
      </div>
      <div style={{ flex: 1, overflow: 'auto', padding: 16 }}>
        {tab === 'rules' && <RulesList/>}
        {tab === 'history' && <AlertHistory alerts={alerts} ackAlert={ackAlert}/>}
        {tab === 'channels' && <ChannelsPanel/>}
      </div>
    </>
  );
}

function RulesList() {
  return (
    <div className="card">
      {window.ALERT_RULES.map(r => {
        const desc = {
          hedge_ratio: <>hedge ratio <b>{r.op} {r.val}{r.unit}</b></>,
          exposure: <>net exposure <b>{r.op} {r.val} {r.unit}</b></>,
          net_pnl: <>net P&L <b>{r.op} {fp(r.val)}</b></>,
          news_event: <>alert <b>{r.val} {r.unit}</b> before high-impact news</>,
          single_client: <>single-client exposure <b>{r.op} {r.val} {r.unit}</b></>,
        }[r.kind];
        return (
          <div className="rule-row" key={r.id}>
            <div className="rule-sym">{r.sym}</div>
            <div className="rule-desc">Trigger when {desc}</div>
            <div className="row-flex" style={{ gap: 4 }}>
              {r.ch.map(c => <span key={c} className="chip" style={{ fontSize: 10 }}>{c}</span>)}
            </div>
            <div className="row-flex" style={{ gap: 8 }}>
              <span className={`rule-sev ${r.sev}`}>{r.sev}</span>
              <span className={`switch ${r.enabled ? 'on' : ''}`}/>
            </div>
          </div>
        );
      })}
    </div>
  );
}

function AlertHistory({ alerts, ackAlert }) {
  return (
    <div className="card">
      {alerts.map(a => (
        <div key={a.id} className="rule-row" style={{ opacity: a.ack ? 0.55 : 1 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <span className={`rule-sev ${a.severity === 'crit' ? 'crit' : a.severity === 'warn' ? 'warn' : 'info'}`}>{a.severity}</span>
          </div>
          <div>
            <div style={{ fontSize: 13, fontWeight: 600 }}>{a.title} <span className="t3" style={{ fontWeight: 400, fontFamily: 'var(--font-mono)', fontSize: 12 }}>· {a.sym}</span></div>
            <div className="t2" style={{ fontSize: 12, marginTop: 2 }}>{a.desc}</div>
          </div>
          <div className="t3 mono" style={{ fontSize: 11 }}>{a.time}</div>
          <div>
            {a.ack ? <span className="chip" style={{ color: 'var(--t3)' }}>acknowledged</span> :
              <button className="icon-btn" onClick={() => ackAlert(a.id)}><I.check/> Ack</button>}
          </div>
        </div>
      ))}
    </div>
  );
}

function ChannelsPanel() {
  const channels = [
    { name: 'In-app notification', desc: 'Banners + toast + sound', enabled: true, icon: '🔔' },
    { name: 'Email', desc: 'risk@fxgrow.com, ops@fxgrow.com', enabled: true, icon: '✉' },
    { name: 'Slack', desc: 'workspace: fxgrow · #risk-alerts', enabled: true, icon: 'S' },
    { name: 'Telegram', desc: 'Bot: @fxgrow_alerts_bot', enabled: false, icon: 'T' },
    { name: 'Webhook', desc: 'https://hooks.fxgrow.com/alerts', enabled: false, icon: '⇢' },
  ];
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px,1fr))', gap: 12 }}>
      {channels.map(c => (
        <div key={c.name} className="card" style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 10 }}>
          <div className="row-flex" style={{ gap: 10 }}>
            <div style={{ width: 34, height: 34, borderRadius: 8, background: 'var(--accent-dim)', color: 'var(--accent)', display: 'grid', placeItems: 'center', fontWeight: 700 }}>{c.icon}</div>
            <div style={{ flex: 1 }}>
              <div style={{ fontWeight: 600, fontSize: 13 }}>{c.name}</div>
              <div className="t3" style={{ fontSize: 11.5 }}>{c.desc}</div>
            </div>
            <span className={`switch ${c.enabled ? 'on' : ''}`}/>
          </div>
        </div>
      ))}
    </div>
  );
}
window.AlertsPanel = AlertsPanel;

// ========== Settings tab (lightweight) ==========
function SettingsPanel() {
  return (
    <div className="panel-wrap">
      <div className="card">
        <div className="card-header">
          <div className="card-title">Connections</div>
        </div>
        <div style={{ padding: 16, display: 'grid', gridTemplateColumns: 'repeat(2,1fr)', gap: 12 }}>
          <ConnCard name="MT5 Manager API" sub="rev-14 · 193.124.185.12:443" status="live" />
          <ConnCard name="Coverage Collector" sub="Python FastAPI · 100ms poll" status="live" />
          <ConnCard name="Centroid CS360" sub="FIX 4.4 Dropcopy · Session 1" status="live" />
          <ConnCard name="Supabase" sub="eu-central-1 · 280K deals persisted" status="live" />
        </div>
      </div>
      <div className="card">
        <div className="card-header">
          <div className="card-title">Snapshot schedules</div>
          <div className="spacer"/>
          <button className="icon-btn"><I.plus/> New schedule</button>
        </div>
        <div>
          {window.SCHEDULES.map(s => (
            <div key={s.id} className="rule-row">
              <div style={{ fontFamily: 'var(--font-ui)', fontWeight: 600 }}>{s.name}</div>
              <div className="rule-desc">
                <b>{s.cron}</b> <span className="t3">· {s.tz}</span>
                <span className="t3" style={{ marginLeft: 10 }}>Last: {s.last} · Next: {s.next}</span>
              </div>
              <div><span className={`chip ${s.enabled ? 'live' : ''}`}>{s.cadence}</span></div>
              <span className={`switch ${s.enabled ? 'on' : ''}`}/>
            </div>
          ))}
        </div>
      </div>
      <div className="card">
        <div className="card-header">
          <div className="card-title">Recent activity</div>
          <div className="spacer"/>
          <span className="t3" style={{ fontSize: 11 }}>past 24h</span>
        </div>
        <div style={{ padding: '4px 0' }}>
          {window.ACTIVITY.map((a, i) => (
            <div key={i} style={{ padding: '10px 16px', borderBottom: i < window.ACTIVITY.length-1 ? '1px solid var(--divider)' : 'none', display: 'flex', gap: 12, alignItems: 'center' }}>
              <div className="mono t3" style={{ fontSize: 11, width: 70 }}>{a.t}</div>
              <div style={{
                width: 6, height: 6, borderRadius: '50%',
                background: a.kind === 'hedge' ? 'var(--teal)' : a.kind === 'rule' ? 'var(--amber)' : a.kind === 'snap' ? 'var(--blue)' : 'var(--t4)',
              }}/>
              <div style={{ flex: 1, fontSize: 12.5 }}>{a.text}</div>
              <div className="t3" style={{ fontSize: 11 }}>{a.user}</div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function ConnCard({ name, sub, status }) {
  return (
    <div style={{ padding: 14, border: '1px solid var(--border)', borderRadius: 10, background: 'var(--bg-2)', display: 'flex', gap: 10 }}>
      <div className="dot" style={{ marginTop: 5 }}/>
      <div style={{ flex: 1 }}>
        <div style={{ fontWeight: 600, fontSize: 13 }}>{name}</div>
        <div className="t3 mono" style={{ fontSize: 11 }}>{sub}</div>
      </div>
      <span className="chip live"><span className="dot"/>{status}</span>
    </div>
  );
}
window.SettingsPanel = SettingsPanel;
