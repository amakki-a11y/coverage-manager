/* global React, I */
const { useState, useEffect, useRef, useMemo, useCallback } = React;

// ========== Formatters ==========
function fmt(v, d = 2) {
  if (v === undefined || v === null) return '';
  return (+v).toLocaleString('en-US', { minimumFractionDigits: d, maximumFractionDigits: d });
}
function fp(v, d = 2) {
  const n = +v || 0;
  return (n >= 0 ? '+' : '') + n.toLocaleString('en-US', { minimumFractionDigits: d, maximumFractionDigits: d });
}
function pc(v) { return v > 0 ? 'pos' : v < 0 ? 'neg' : 't2'; }
function hedgeColorClass(r) { return r >= 80 ? 'pos' : r >= 50 ? 'amb' : 'neg'; }
function hedgeColorVar(r)   { return r >= 80 ? 'var(--green)' : r >= 50 ? 'var(--amber)' : 'var(--red)'; }
function assetColor(cls) {
  return { metal: 'var(--amber)', fx: 'var(--blue)', idx: 'var(--purple)', crypto: 'var(--teal)', comm: '#EF9A5A' }[cls] || 'var(--t3)';
}
function assetBg(cls) {
  return { metal: 'var(--amber-dim)', fx: 'var(--blue-dim)', idx: 'var(--purple-dim)', crypto: 'var(--teal-dim)', comm: 'rgba(239,154,90,0.14)' }[cls] || 'var(--bg-3)';
}
window.fmt = fmt; window.fp = fp; window.pc = pc;

// ========== Ticking prices hook ==========
function useTickingPrices(symbols) {
  const [prices, setPrices] = useState(() => {
    const m = {};
    for (const s of symbols) m[s.sym] = { bid: s.bid, dir: 'flat', key: 0 };
    return m;
  });
  useEffect(() => {
    const id = setInterval(() => {
      setPrices(prev => {
        const next = { ...prev };
        // tick 2-3 random symbols each beat
        const picks = new Set();
        while (picks.size < 3) picks.add(symbols[Math.floor(Math.random() * symbols.length)].sym);
        for (const sym of picks) {
          const s = symbols.find(x => x.sym === sym);
          const p = prev[sym] || { bid: s.bid };
          const delta = (Math.random() - 0.48) * s.vol;
          const newBid = +(p.bid + delta).toFixed(s.digits);
          next[sym] = { bid: newBid, dir: newBid > p.bid ? 'up' : newBid < p.bid ? 'down' : p.dir, key: (p.key || 0) + 1 };
        }
        return next;
      });
    }, 600);
    return () => clearInterval(id);
  }, [symbols]);
  return prices;
}
window.useTickingPrices = useTickingPrices;

// ========== Asset icon square ==========
function AssetMark({ sym, cls }) {
  const letters = sym.slice(0, 2);
  return (
    <span className="asset-icon" style={{ background: assetBg(cls), color: assetColor(cls) }}>
      {letters}
    </span>
  );
}
window.AssetMark = AssetMark;

// ========== Hedge bar ==========
function HedgeBar({ pct }) {
  const clamped = Math.min(100, Math.max(0, pct));
  return (
    <span>
      <span className="hedge-bar">
        <span className="fill" style={{ width: clamped + '%', background: hedgeColorVar(pct) }} />
      </span>
      <span className="hedge-pct" style={{ color: hedgeColorVar(pct) }}>{pct.toFixed(0)}%</span>
    </span>
  );
}
window.HedgeBar = HedgeBar;

// ========== Exposure Table ==========
function ExposureTable({ showGrid, onOpenHedge, layout }) {
  const SYMBOLS = window.SYMBOLS;
  const EXPOSURE = window.EXPOSURE;
  const CLOSED = window.CLOSED_PNL;
  const prices = useTickingPrices(SYMBOLS);
  const [order, setOrder] = useState(() => SYMBOLS.map(s => s.sym));
  const [sort, setSort] = useState('custom');
  const [sortAsc, setSortAsc] = useState(true);
  const [dateFrom, setDateFrom] = useState('2026-04-18');
  const [dateTo, setDateTo] = useState('2026-04-18');
  const dragFrom = useRef(null);
  const dragTo = useRef(null);
  const [, force] = useState(0);

  // Flash keys for row animation
  const flashKeys = useRef({});
  useEffect(() => {
    for (const [sym, p] of Object.entries(prices)) {
      flashKeys.current[sym] = { dir: p.dir, key: p.key };
    }
  }, [prices]);

  const rows = useMemo(() => {
    const list = order.map(sym => {
      const s = SYMBOLS.find(x => x.sym === sym);
      const e = EXPOSURE[sym];
      const bbNet = e.bbBuy - e.bbSell;
      const covNet = e.covBuy - e.covSell;
      const toCover = bbNet - covNet;
      const netPnL = -e.bbPnL + e.covPnL;
      const hedge = Math.abs(bbNet) > 0.001 ? Math.min(999, (Math.abs(covNet) / Math.abs(bbNet)) * 100) : 0;
      const closed = CLOSED[sym];
      return { s, e, bbNet, covNet, toCover, netPnL, hedge, closed, bbClosed: closed.bb, covClosed: closed.cov };
    });
    if (sort === 'custom') return list;
    const dir = sortAsc ? 1 : -1;
    const getKey = r => {
      switch (sort) {
        case 'symbol': return r.s.sym;
        case 'bbNet': return r.bbNet;
        case 'bbPnL': return r.e.bbPnL;
        case 'covNet': return r.covNet;
        case 'covPnL': return r.e.covPnL;
        case 'toCover': return r.toCover;
        case 'netPnL': return r.netPnL;
        case 'hedge': return r.hedge;
        default: return 0;
      }
    };
    return [...list].sort((a, b) => {
      const ak = getKey(a), bk = getKey(b);
      if (typeof ak === 'string') return dir * ak.localeCompare(bk);
      return dir * (ak - bk);
    });
  }, [order, sort, sortAsc]);

  const totals = useMemo(() => rows.reduce((a, r) => ({
    bbBuy: a.bbBuy + r.e.bbBuy, bbSell: a.bbSell + r.e.bbSell, bbNet: a.bbNet + r.bbNet, bbPnL: a.bbPnL + r.e.bbPnL,
    covBuy: a.covBuy + r.e.covBuy, covSell: a.covSell + r.e.covSell, covNet: a.covNet + r.covNet, covPnL: a.covPnL + r.e.covPnL,
    netPnL: a.netPnL + r.netPnL, bbClosed: a.bbClosed + r.bbClosed, covClosed: a.covClosed + r.covClosed,
  }), { bbBuy:0, bbSell:0, bbNet:0, bbPnL:0, covBuy:0, covSell:0, covNet:0, covPnL:0, netPnL:0, bbClosed:0, covClosed:0 }), [rows]);

  const onDragStart = sym => { dragFrom.current = sym; };
  const onDragOver = (e, sym) => { e.preventDefault(); dragTo.current = sym; };
  const onDrop = () => {
    const from = dragFrom.current, to = dragTo.current;
    if (!from || !to || from === to) return;
    const next = [...order];
    const fi = next.indexOf(from), ti = next.indexOf(to);
    next.splice(fi, 1);
    next.splice(ti, 0, from);
    setOrder(next);
    setSort('custom');
    dragFrom.current = dragTo.current = null;
  };

  const handleSort = (k) => {
    if (sort === k) setSortAsc(a => !a);
    else { setSort(k); setSortAsc(k === 'symbol'); }
  };
  const arrow = k => sort === k ? (sortAsc ? ' ↑' : ' ↓') : '';

  if (layout === 'cards') return <ExposureCards rows={rows} prices={prices} onOpenHedge={onOpenHedge} />;

  return (
    <>
      <ExposureToolbar
        sort={sort} setSort={setSort} sortAsc={sortAsc} setSortAsc={setSortAsc}
        dateFrom={dateFrom} setDateFrom={setDateFrom}
        dateTo={dateTo} setDateTo={setDateTo}
      />
      <div className={`exposure ${showGrid ? 'grid-on' : ''}`}>
        <table>
          <thead>
            <tr>
              <th style={{ width: 160, textAlign: 'left', paddingLeft: 16 }} rowSpan={2}></th>
              <th style={{ width: 36 }} rowSpan={2}></th>
              <th className="group clients sec-l" colSpan={4}>CLIENTS</th>
              <th className="group coverage sec-l" colSpan={4}>COVERAGE</th>
              <th className="group summary sec-l" colSpan={4}>SUMMARY</th>
            </tr>
            <tr className="sub">
              <th className="sec-l">Buy</th>
              <th>Sell</th>
              <th><button className="sort-btn" onClick={() => handleSort('bbNet')}>Net{arrow('bbNet')}</button></th>
              <th><button className="sort-btn" onClick={() => handleSort('bbPnL')}>P&L{arrow('bbPnL')}</button></th>
              <th className="sec-l">Buy</th>
              <th>Sell</th>
              <th><button className="sort-btn" onClick={() => handleSort('covNet')}>Net{arrow('covNet')}</button></th>
              <th><button className="sort-btn" onClick={() => handleSort('covPnL')}>P&L{arrow('covPnL')}</button></th>
              <th className="sec-l"><button className="sort-btn" onClick={() => handleSort('toCover')}>To Cover{arrow('toCover')}</button></th>
              <th><button className="sort-btn" onClick={() => handleSort('netPnL')}>Net P&L{arrow('netPnL')}</button></th>
              <th><button className="sort-btn" onClick={() => handleSort('hedge')}>Hedge{arrow('hedge')}</button></th>
              <th>Action</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r, i) => {
              const price = prices[r.s.sym] || { bid: r.s.bid, dir: 'flat', key: 0 };
              const flashCls = price.dir === 'up' ? 'flash-up' : price.dir === 'down' ? 'flash-down' : '';
              return (
                <React.Fragment key={r.s.sym}>
                  <tr
                    className={`open-row sym-div ${flashCls}`}
                    key={`${r.s.sym}-${price.key}`}
                    draggable
                    onDragStart={() => onDragStart(r.s.sym)}
                    onDragOver={(e) => onDragOver(e, r.s.sym)}
                    onDrop={onDrop}
                  >
                    <td rowSpan={2} className="sym-cell">
                      <span className="drag-handle"><I.drag /></span>
                      <div style={{ display: 'flex', alignItems: 'center' }}>
                        <AssetMark sym={r.s.sym} cls={r.s.cls} />
                        <span className="sym-name">
                          <div style={{ fontWeight: 600, fontSize: 13 }}>{r.s.sym}</div>
                          <div style={{ color: 'var(--t3)', fontSize: 10.5, fontWeight: 500, marginTop: 1 }}>{r.s.asset}</div>
                        </span>
                      </div>
                      <div className={`sym-bid ${price.dir}`}>
                        <span className="arrow">{price.dir === 'up' ? '▲' : price.dir === 'down' ? '▼' : '·'}</span> {price.bid.toFixed(r.s.digits)}
                      </div>
                    </td>
                    <td className="oc open">O</td>
                    <td className="pos sec-l">{fmt(r.e.bbBuy)}</td>
                    <td className="neg">{fmt(r.e.bbSell)}</td>
                    <td className={pc(r.bbNet)} style={{ fontWeight: 600 }}>{fmt(r.bbNet)}</td>
                    <td className={pc(r.e.bbPnL)}>{fp(r.e.bbPnL)}</td>
                    <td className="pos sec-l">{fmt(r.e.covBuy)}</td>
                    <td className="neg">{fmt(r.e.covSell)}</td>
                    <td className={pc(r.covNet)} style={{ fontWeight: 600 }}>{fmt(r.covNet)}</td>
                    <td className={pc(r.e.covPnL)}>{fp(r.e.covPnL)}</td>
                    <td className={`sec-l ${pc(-r.toCover)}`} style={{ fontWeight: 600 }}>{Math.abs(r.toCover) < 0.005 ? '—' : fp(-r.toCover)}</td>
                    <td className={pc(r.netPnL)} style={{ fontWeight: 700 }}>{fp(r.netPnL)}</td>
                    <td><HedgeBar pct={r.hedge} /></td>
                    <td>
                      {Math.abs(r.toCover) > 0.01 ? (
                        <button className={`hedge-btn ${r.toCover < 0 ? 'sell' : 'buy'}`}
                          onClick={() => onOpenHedge(r)}>
                          {r.toCover < 0 ? 'SELL ' : 'BUY '}{Math.abs(r.toCover).toFixed(2)}
                        </button>
                      ) : <span style={{ color: 'var(--t4)', fontSize: 11 }}>—</span>}
                    </td>
                  </tr>
                  <tr className="close-row">
                    <td className="oc close">C</td>
                    <td className="pos sec-l">{r.closed.bb >= 0 ? fmt(Math.abs(r.e.bbBuy) * 0.4) : '—'}</td>
                    <td className="neg">{r.closed.bb <= 0 ? fmt(Math.abs(r.e.bbSell) * 0.4) : '—'}</td>
                    <td className="t3">{fmt((Math.abs(r.e.bbBuy) + Math.abs(r.e.bbSell)) * 0.4)}</td>
                    <td className={pc(r.closed.bb)} style={{ fontWeight: 600 }}>{fp(r.closed.bb)}</td>
                    <td className="pos sec-l">{r.closed.cov >= 0 ? fmt(Math.abs(r.e.covBuy) * 0.35) : '—'}</td>
                    <td className="neg">{r.closed.cov <= 0 ? fmt(Math.abs(r.e.covSell) * 0.35) : '—'}</td>
                    <td className="t3">{fmt((Math.abs(r.e.covBuy) + Math.abs(r.e.covSell)) * 0.35)}</td>
                    <td className={pc(r.closed.cov)} style={{ fontWeight: 600 }}>{fp(r.closed.cov)}</td>
                    <td className="sec-l"></td>
                    <td className={pc(r.closed.cov - r.closed.bb)} style={{ fontWeight: 600 }}>{fp(r.closed.cov - r.closed.bb)}</td>
                    <td className="t3" style={{ fontSize: 10.5 }}>settled</td>
                    <td></td>
                  </tr>
                </React.Fragment>
              );
            })}
          </tbody>
          <tfoot>
            <tr className="total-row">
              <td style={{ paddingLeft: 16, textAlign: 'left', fontFamily: 'var(--font-ui)', color: 'var(--t2)' }}>TOTAL</td>
              <td className="oc open">O</td>
              <td className="pos sec-l">{fmt(totals.bbBuy)}</td>
              <td className="neg">{fmt(totals.bbSell)}</td>
              <td className={pc(totals.bbNet)}>{fmt(totals.bbNet)}</td>
              <td className={pc(totals.bbPnL)}>{fp(totals.bbPnL)}</td>
              <td className="pos sec-l">{fmt(totals.covBuy)}</td>
              <td className="neg">{fmt(totals.covSell)}</td>
              <td className={pc(totals.covNet)}>{fmt(totals.covNet)}</td>
              <td className={pc(totals.covPnL)}>{fp(totals.covPnL)}</td>
              <td className={`sec-l ${pc(-(totals.bbNet - totals.covNet))}`}>{fp(-(totals.bbNet - totals.covNet))}</td>
              <td className={pc(totals.netPnL)}>{fp(totals.netPnL)}</td>
              <td colSpan={2}></td>
            </tr>
            <tr className="total-row" style={{ fontSize: 11 }}>
              <td style={{ paddingLeft: 16, textAlign: 'left', fontFamily: 'var(--font-ui)', color: 'var(--t3)' }}>TOTAL</td>
              <td className="oc close">C</td>
              <td colSpan={3} className="sec-l t3" style={{ textAlign: 'center' }}>settled</td>
              <td className={pc(totals.bbClosed)} style={{ fontWeight: 600 }}>{fp(totals.bbClosed)}</td>
              <td colSpan={3} className="sec-l t3" style={{ textAlign: 'center' }}>settled</td>
              <td className={pc(totals.covClosed)} style={{ fontWeight: 600 }}>{fp(totals.covClosed)}</td>
              <td className="sec-l"></td>
              <td className={pc(totals.covClosed - totals.bbClosed)} style={{ fontWeight: 700 }}>{fp(totals.covClosed - totals.bbClosed)}</td>
              <td colSpan={2}></td>
            </tr>
          </tfoot>
        </table>
      </div>
    </>
  );
}

function ExposureToolbar({ sort, setSort, sortAsc, setSortAsc, dateFrom, setDateFrom, dateTo, setDateTo }) {
  const [persona, setPersona] = useState('dealer');
  const [group, setGroup] = useState('ALL');
  return (
    <div className="toolbar">
      <span style={{ color: 'var(--t3)', fontSize: 11, fontWeight: 600, letterSpacing: '0.06em' }}>SORT</span>
      <select className="select" value={sort} onChange={e => setSort(e.target.value)}>
        <option value="custom">Custom (drag)</option>
        <option value="symbol">Symbol</option>
        <option value="bbNet">Client Net</option>
        <option value="bbPnL">Client P&L</option>
        <option value="covNet">Coverage Net</option>
        <option value="covPnL">Coverage P&L</option>
        <option value="toCover">To Cover</option>
        <option value="netPnL">Net P&L</option>
        <option value="hedge">Hedge %</option>
      </select>
      <button className="icon-btn" onClick={() => setSortAsc(!sortAsc)} title={sortAsc ? 'Ascending' : 'Descending'}>
        {sortAsc ? '↑' : '↓'}
      </button>
      <div className="sep"/>
      <span style={{ color: 'var(--t3)', fontSize: 11, fontWeight: 600, letterSpacing: '0.06em' }}>GROUP</span>
      <select className="select" value={group} onChange={e => setGroup(e.target.value)}>
        <option value="ALL">All accounts</option>
        <option>Real-USD</option>
        <option>Real-EUR</option>
        <option>VIP</option>
        <option>Demo</option>
      </select>
      <div className="sep"/>
      <span style={{ color: 'var(--t3)', fontSize: 11, fontWeight: 600, letterSpacing: '0.06em' }}>CLOSED</span>
      <input type="date" className="date-input" value={dateFrom} onChange={e => setDateFrom(e.target.value)} />
      <span style={{ color: 'var(--t3)', fontSize: 11 }}>→</span>
      <input type="date" className="date-input" value={dateTo} onChange={e => setDateTo(e.target.value)} />
      <div className="spacer"/>
      <span className="chip live"><span className="dot"/>Live · 10 updates/sec</span>
    </div>
  );
}

// Card layout variant
function ExposureCards({ rows, prices, onOpenHedge }) {
  return (
    <div style={{ padding: 16, overflow: 'auto', flex: 1 }}>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(320px, 1fr))', gap: 12 }}>
        {rows.map(r => {
          const price = prices[r.s.sym] || { bid: r.s.bid, dir: 'flat' };
          return (
            <div key={r.s.sym} className="card" style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 10 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <AssetMark sym={r.s.sym} cls={r.s.cls} />
                <div style={{ flex: 1 }}>
                  <div style={{ fontWeight: 600, fontSize: 14 }}>{r.s.sym}</div>
                  <div style={{ color: 'var(--t3)', fontSize: 11 }}>{r.s.asset}</div>
                </div>
                <div className={`mono ${price.dir === 'up' ? 'pos' : price.dir === 'down' ? 'neg' : 't2'}`} style={{ fontSize: 14, fontWeight: 600 }}>
                  {price.bid.toFixed(r.s.digits)}
                </div>
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, marginTop: 4 }}>
                <div>
                  <div style={{ color: 'var(--blue)', fontSize: 10, fontWeight: 700, letterSpacing: '0.08em' }}>CLIENTS</div>
                  <div className="mono" style={{ fontSize: 15, fontWeight: 600 }}>{fmt(r.bbNet)}</div>
                  <div className={`mono ${pc(r.e.bbPnL)}`} style={{ fontSize: 12 }}>{fp(r.e.bbPnL)}</div>
                </div>
                <div>
                  <div style={{ color: 'var(--teal)', fontSize: 10, fontWeight: 700, letterSpacing: '0.08em' }}>COVERAGE</div>
                  <div className="mono" style={{ fontSize: 15, fontWeight: 600 }}>{fmt(r.covNet)}</div>
                  <div className={`mono ${pc(r.e.covPnL)}`} style={{ fontSize: 12 }}>{fp(r.e.covPnL)}</div>
                </div>
              </div>
              <div style={{ paddingTop: 8, borderTop: '1px solid var(--divider)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <div>
                  <div style={{ color: 'var(--t3)', fontSize: 10, fontWeight: 600, letterSpacing: '0.06em' }}>HEDGE</div>
                  <HedgeBar pct={r.hedge} />
                </div>
                <div style={{ textAlign: 'right' }}>
                  <div style={{ color: 'var(--t3)', fontSize: 10, fontWeight: 600, letterSpacing: '0.06em' }}>NET P&L</div>
                  <div className={`mono ${pc(r.netPnL)}`} style={{ fontSize: 14, fontWeight: 700 }}>{fp(r.netPnL)}</div>
                </div>
                {Math.abs(r.toCover) > 0.01 && (
                  <button className={`hedge-btn ${r.toCover < 0 ? 'sell' : 'buy'}`} onClick={() => onOpenHedge(r)} style={{ padding: '6px 12px', fontSize: 11 }}>
                    {r.toCover < 0 ? 'SELL' : 'BUY'} {Math.abs(r.toCover).toFixed(2)}
                  </button>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

window.ExposureTable = ExposureTable;
