/* bundled */

// ===== icons.jsx =====
/* global React */
// Tiny icon set (Lucide-style strokes, 16px). All inherit currentColor.

const Icon = ({ d, size = 16, fill = 'none', strokeWidth = 1.6, style }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill={fill}
    stroke="currentColor" strokeWidth={strokeWidth}
    strokeLinecap="round" strokeLinejoin="round"
    style={style} className="icon">
    {d}
  </svg>
);

const I = {
  grid: () => <Icon d={<><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/></>} />,
  list: () => <Icon d={<><line x1="8" y1="6" x2="21" y2="6"/><line x1="8" y1="12" x2="21" y2="12"/><line x1="8" y1="18" x2="21" y2="18"/><circle cx="4" cy="6" r="1"/><circle cx="4" cy="12" r="1"/><circle cx="4" cy="18" r="1"/></>} />,
  pulse: () => <Icon d={<polyline points="3 12 7 12 10 6 14 18 17 12 21 12"/>} />,
  wallet: () => <Icon d={<><path d="M3 7a2 2 0 012-2h14v12a2 2 0 01-2 2H5a2 2 0 01-2-2V7z"/><path d="M16 12h3"/></>} />,
  layers: () => <Icon d={<><path d="M12 3L3 8l9 5 9-5-9-5z"/><path d="M3 14l9 5 9-5"/></>} />,
  split: () => <Icon d={<><path d="M4 4h6v16H4z"/><path d="M14 4h6v16h-6z"/></>} />,
  arrows: () => <Icon d={<><path d="M7 7h10M7 7l3-3M7 7l3 3"/><path d="M17 17H7M17 17l-3-3M17 17l-3 3"/></>} />,
  bridge: () => <Icon d={<><path d="M3 12h18"/><path d="M7 12V8a2 2 0 012-2h6a2 2 0 012 2v4"/><path d="M5 16v-4M19 16v-4"/></>} />,
  map: () => <Icon d={<><polygon points="3 6 9 3 15 6 21 3 21 18 15 21 9 18 3 21 3 6"/><line x1="9" y1="3" x2="9" y2="18"/><line x1="15" y1="6" x2="15" y2="21"/></>} />,
  gear: () => <Icon d={<><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 00.3 1.8l.1.1a2 2 0 11-2.8 2.8l-.1-.1a1.7 1.7 0 00-1.8-.3 1.7 1.7 0 00-1 1.5v.2a2 2 0 11-4 0v-.1a1.7 1.7 0 00-1.1-1.6 1.7 1.7 0 00-1.8.3l-.1.1a2 2 0 11-2.8-2.8l.1-.1a1.7 1.7 0 00.3-1.8 1.7 1.7 0 00-1.5-1H3a2 2 0 110-4h.1a1.7 1.7 0 001.6-1.1 1.7 1.7 0 00-.3-1.8l-.1-.1a2 2 0 112.8-2.8l.1.1a1.7 1.7 0 001.8.3H9a1.7 1.7 0 001-1.5V3a2 2 0 114 0v.1a1.7 1.7 0 001 1.5 1.7 1.7 0 001.8-.3l.1-.1a2 2 0 112.8 2.8l-.1.1a1.7 1.7 0 00-.3 1.8v.1a1.7 1.7 0 001.5 1H21a2 2 0 110 4h-.1a1.7 1.7 0 00-1.5 1z"/></>} />,
  bell: () => <Icon d={<><path d="M18 8A6 6 0 006 8c0 7-3 9-3 9h18s-3-2-3-9"/><path d="M13.7 21a2 2 0 01-3.4 0"/></>} />,
  sun: () => <Icon d={<><circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/></>} />,
  moon: () => <Icon d={<path d="M21 12.8A9 9 0 1111.2 3a7 7 0 009.8 9.8z"/>} />,
  search: () => <Icon d={<><circle cx="11" cy="11" r="7"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></>} />,
  sliders: () => <Icon d={<><line x1="4" y1="21" x2="4" y2="14"/><line x1="4" y1="10" x2="4" y2="3"/><line x1="12" y1="21" x2="12" y2="12"/><line x1="12" y1="8" x2="12" y2="3"/><line x1="20" y1="21" x2="20" y2="16"/><line x1="20" y1="12" x2="20" y2="3"/><circle cx="4" cy="12" r="2"/><circle cx="12" cy="10" r="2"/><circle cx="20" cy="14" r="2"/></>} />,
  close: () => <Icon d={<><line x1="6" y1="6" x2="18" y2="18"/><line x1="6" y1="18" x2="18" y2="6"/></>} />,
  check: () => <Icon d={<polyline points="4 12 10 18 20 6"/>} />,
  warn: () => <Icon d={<><path d="M12 2L2 21h20L12 2z"/><line x1="12" y1="9" x2="12" y2="14"/><circle cx="12" cy="17.5" r="0.8" fill="currentColor"/></>} />,
  info: () => <Icon d={<><circle cx="12" cy="12" r="9"/><line x1="12" y1="11" x2="12" y2="16"/><circle cx="12" cy="8" r="0.8" fill="currentColor"/></>} />,
  bolt: () => <Icon d={<polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2" fill="currentColor" stroke="none"/>} />,
  plus: () => <Icon d={<><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></>} />,
  drag: () => <Icon size={12} d={<><circle cx="9" cy="6" r="1" fill="currentColor"/><circle cx="15" cy="6" r="1" fill="currentColor"/><circle cx="9" cy="12" r="1" fill="currentColor"/><circle cx="15" cy="12" r="1" fill="currentColor"/><circle cx="9" cy="18" r="1" fill="currentColor"/><circle cx="15" cy="18" r="1" fill="currentColor"/></>} />,
  chevLeft: () => <Icon d={<polyline points="15 6 9 12 15 18"/>} />,
  chevRight: () => <Icon d={<polyline points="9 6 15 12 9 18"/>} />,
  chevDown: () => <Icon d={<polyline points="6 9 12 15 18 9"/>} />,
  download: () => <Icon d={<><path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></>} />,
  refresh: () => <Icon d={<><polyline points="23 4 23 10 17 10"/><polyline points="1 20 1 14 7 14"/><path d="M3.5 9a9 9 0 0114.9-3.4L23 10M1 14l4.6 4.4A9 9 0 0020.5 15"/></>} />,
  kbd: () => <Icon d={<><rect x="2" y="4" width="20" height="16" rx="2"/><path d="M6 8h2M10 8h2M14 8h2M18 8h2M6 12h2M10 12h8M6 16h12"/></>} />,
  camera: () => <Icon d={<><path d="M23 19a2 2 0 01-2 2H3a2 2 0 01-2-2V8a2 2 0 012-2h4l2-3h6l2 3h4a2 2 0 012 2z"/><circle cx="12" cy="13" r="4"/></>} />,
  book: () => <Icon d={<><path d="M2 3h6a4 4 0 014 4v14a3 3 0 00-3-3H2z"/><path d="M22 3h-6a4 4 0 00-4 4v14a3 3 0 013-3h7z"/></>} />,
};

window.I = I;


// ===== ExposureTable.jsx =====

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


// ===== HedgeModal.jsx =====



// ========== Hedge Modal ==========
function HedgeModal({ row, onClose, onConfirm }) {
  const dir = row.toCover < 0 ? 'SELL' : 'BUY';
  const amount = Math.abs(row.toCover);
  const [vol, setVol] = useState(amount.toFixed(2));
  const [lpAcct, setLpAcct] = useState('96900 · fXGROW LP');
  const [partial, setPartial] = useState(100);
  const [confirm, setConfirm] = useState(true);
  const volN = parseFloat(vol) || 0;

  const price = window.SYMBOLS.find(s => s.sym === row.s.sym).bid;

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal" onClick={e => e.stopPropagation()} style={{ width: 520 }}>
        <div className="modal-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
            <div style={{ width: 36, height: 36, borderRadius: 8, background: dir === 'BUY' ? 'var(--green-dim)' : 'var(--red-dim)', color: dir === 'BUY' ? 'var(--green)' : 'var(--red)', display: 'grid', placeItems: 'center', fontWeight: 700, fontSize: 11 }}>
              {dir}
            </div>
            <div>
              <div className="modal-title">
                Hedge {row.s.sym}
                <span style={{ color: 'var(--t3)', fontWeight: 500, marginLeft: 8 }}>· {row.s.asset}</span>
              </div>
              <div className="modal-sub">
                {dir === 'BUY' ? 'Buy on LP to cover short client exposure' : 'Sell on LP to cover long client exposure'}
              </div>
            </div>
          </div>
        </div>
        <div className="modal-body">
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10, marginBottom: 18 }}>
            <div className="card" style={{ padding: 12, borderRadius: 8 }}>
              <div className="t3" style={{ fontSize: 10, fontWeight: 600, letterSpacing: '0.08em' }}>CURRENT EXPOSURE</div>
              <div className="mono" style={{ fontSize: 18, fontWeight: 700, marginTop: 4 }}>{fmt(row.bbNet - row.covNet)}</div>
              <div className="t3 mono" style={{ fontSize: 11 }}>Client {fmt(row.bbNet)} · Cov {fmt(row.covNet)}</div>
            </div>
            <div className="card" style={{ padding: 12, borderRadius: 8 }}>
              <div className="t3" style={{ fontSize: 10, fontWeight: 600, letterSpacing: '0.08em' }}>HEDGE RATIO</div>
              <div style={{ marginTop: 6 }}><HedgeBar pct={row.hedge} /></div>
              <div className="t3" style={{ fontSize: 11, marginTop: 2 }}>after hedge: <span className="pos">100%</span></div>
            </div>
          </div>

          <label style={{ display: 'flex', flexDirection: 'column', gap: 6, marginBottom: 14 }}>
            <span className="t3" style={{ fontSize: 10.5, fontWeight: 600, letterSpacing: '0.08em' }}>VOLUME (LOTS)</span>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
              <input className="date-input" style={{ flex: 1, fontSize: 16, padding: '8px 12px', fontWeight: 600 }} value={vol} onChange={e => setVol(e.target.value)} />
              <div className="segmented" style={{ padding: 3 }}>
                {[25, 50, 75, 100].map(p => (
                  <button key={p} className={partial === p ? 'active' : ''} onClick={() => { setPartial(p); setVol(((amount * p) / 100).toFixed(2)); }}>{p}%</button>
                ))}
              </div>
            </div>
            <div className="t3" style={{ fontSize: 11 }}>
              Market: <span className="mono t1">{price}</span> · Est. notional <span className="mono t1">${fmt(volN * price * (row.s.cls === 'fx' ? 100000 : 1))}</span>
            </div>
          </label>

          <label style={{ display: 'flex', flexDirection: 'column', gap: 6, marginBottom: 14 }}>
            <span className="t3" style={{ fontSize: 10.5, fontWeight: 600, letterSpacing: '0.08em' }}>LP ACCOUNT</span>
            <select className="select" style={{ padding: '8px 26px 8px 12px', fontSize: 13 }} value={lpAcct} onChange={e => setLpAcct(e.target.value)}>
              <option>96900 · fXGROW LP</option>
              <option>96901 · Centroid LP</option>
              <option>96902 · Equiti Prime</option>
            </select>
          </label>

          <div className="card" style={{ padding: 12, borderRadius: 8, marginBottom: 14 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 6 }}>
              <span className="t3" style={{ fontSize: 10.5, fontWeight: 600, letterSpacing: '0.08em' }}>PRE-TRADE CHECK</span>
              <span className="chip live"><span className="dot"/>All clear</span>
            </div>
            <div style={{ display: 'flex', gap: 14, fontSize: 11.5 }}>
              <div><span className="t3">Margin</span> <span className="mono t1">$48,210</span></div>
              <div><span className="t3">Free</span> <span className="mono pos">$412,790</span></div>
              <div><span className="t3">Slippage</span> <span className="mono t1">0.2 pips</span></div>
            </div>
          </div>

          <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, color: 'var(--t2)', cursor: 'pointer' }}>
            <span className={`switch ${confirm ? 'on' : ''}`} onClick={() => setConfirm(!confirm)} />
            Require confirmation before firing (recommended)
          </label>
        </div>
        <div className="modal-footer">
          <button className="icon-btn" onClick={onClose}>Cancel</button>
          <button className={`icon-btn ${dir === 'BUY' ? 'primary' : 'danger'}`} onClick={() => onConfirm({ row, dir, vol: volN, lpAcct })}>
            <I.bolt /> {dir} {volN.toFixed(2)} lots
          </button>
        </div>
      </div>
    </div>
  );
}
window.HedgeModal = HedgeModal;

// ========== Hedge Confirm Step (brief) ==========
function HedgeConfirmToast({ text, onDone }) {
  React.useEffect(() => {
    const t = setTimeout(onDone, 3200);
    return () => clearTimeout(t);
  }, []);
  return (
    <div className="toast info" style={{ maxWidth: 360 }}>
      <div className="toast-icon" style={{ background: 'var(--green-dim)', color: 'var(--green)' }}>✓</div>
      <div className="toast-body">
        <div className="toast-title">Hedge fired</div>
        <div className="toast-desc">{text}</div>
        <div className="toast-meta">Bridge ID b-8402 · t+180ms</div>
      </div>
    </div>
  );
}
window.HedgeConfirmToast = HedgeConfirmToast;


// ===== Tabs.jsx =====

// Secondary tabs for the Coverage Manager shell.


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
  const [filter, setFilter] = useState('all');
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
  const [showCov, setShowCov] = useState(true);
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
  const [sel, setSel] = useState('XAUUSD');
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
  const ref = useRef(null);
  useEffect(() => {
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
  const [tab, setTab] = useState('rules');
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


// ===== App.jsx =====



// ========== Sidebar ==========
function Sidebar({ tab, setTab, collapsed, setCollapsed, alertCount }) {
  const groups = [
    { label: 'Real-time', items: [
      { id: 'exposure', label: 'Exposure',  icon: <I.grid/>,  kbd: '1', pill: null },
      { id: 'compare',  label: 'Compare',   icon: <I.split/>, kbd: '2', pill: null },
      { id: 'bridge',   label: 'Bridge',    icon: <I.bridge/>,kbd: '3', pill: { kind: 'red', text: '1' } },
    ]},
    { label: 'Analytics', items: [
      { id: 'pnl',      label: 'P&L',       icon: <I.wallet/>, kbd: '4' },
      { id: 'netpnl',   label: 'Net P&L',   icon: <I.layers/>, kbd: '5' },
      { id: 'positions',label: 'Positions', icon: <I.list/>,   kbd: '6' },
    ]},
    { label: 'Config', items: [
      { id: 'mappings', label: 'Mappings',  icon: <I.map/>, kbd: '7' },
      { id: 'alerts',   label: 'Alerts',    icon: <I.bell/>, kbd: '8', pill: alertCount ? { kind: 'red', text: alertCount } : null },
      { id: 'settings', label: 'Settings',  icon: <I.gear/>, kbd: '9' },
    ]},
  ];
  return (
    <div className="sidebar">
      <div className="brand">
        <div className="brand-mark">C</div>
        {!collapsed && (
          <div style={{ flex: 1, minWidth: 0 }}>
            <div className="brand-name">Coverage Mgr</div>
            <div className="brand-sub">fxGROW · prod</div>
          </div>
        )}
        {!collapsed && (
          <button
            className="sidebar-toggle"
            onClick={() => setCollapsed(true)}
            title="Collapse sidebar  ["
            aria-label="Collapse sidebar">
            <I.chevLeft/>
          </button>
        )}
      </div>
      {collapsed && (
        <button
          className="sidebar-expand-tab"
          onClick={() => setCollapsed(false)}
          title="Expand sidebar  ["
          aria-label="Expand sidebar">
          <I.chevRight/>
        </button>
      )}
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
  const [q, setQ] = useState('');
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
  const [tab, setTab] = useState('exposure');
  const [collapsed, setCollapsed] = useState(false);
  const [tweaks, setTweaks] = useState({
    theme: 'dark', accent: 'blue', density: 'compact', grid: false,
    persona: 'risk', exposureLayout: 'table', showClosed: true, flash: true,
    banner: true, toasts: true, sound: false,
  });
  const [showTweaks, setShowTweaks] = useState(false);
  const [showPalette, setShowPalette] = useState(false);
  const [hedgeRow, setHedgeRow] = useState(null);
  const [toasts, setToasts] = useState([]);
  const [alerts, setAlerts] = useState(window.ALERTS_SEED);
  const [bannerAlert, setBannerAlert] = useState(window.ALERTS_SEED.find(a => !a.ack && a.severity === 'crit'));

  // Apply theme + tweaks to <html>
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', tweaks.theme);
    document.documentElement.setAttribute('data-accent', tweaks.accent);
    document.documentElement.setAttribute('data-density', tweaks.density);
  }, [tweaks]);

  // Keyboard
  useEffect(() => {
    const handler = (e) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); setShowPalette(true); return; }
      if (e.target.tagName === 'INPUT' || e.target.tagName === 'SELECT') return;
      const map = { '1':'exposure','2':'compare','3':'bridge','4':'pnl','5':'netpnl','6':'positions','7':'mappings','8':'alerts','9':'settings' };
      if (map[e.key]) setTab(map[e.key]);
      if (e.key === 'Escape') { setShowPalette(false); setShowTweaks(false); setHedgeRow(null); }
      if (e.key === '[' && !e.metaKey && !e.ctrlKey) setCollapsed(c => !c);
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, []);

  // Tweak listener protocol (persistent tweaks toolbar)
  useEffect(() => {
    const h = (e) => {
      const m = e.data || {};
      if (m.type === '__activate_edit_mode') setShowTweaks(true);
      if (m.type === '__deactivate_edit_mode') setShowTweaks(false);
    };
    window.addEventListener('message', h);
    try { window.parent.postMessage({ type: '__edit_mode_available' }, '*'); } catch(e) {}
    return () => window.removeEventListener('message', h);
  }, []);

  // Periodic random alert
  useEffect(() => {
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
      case 'netpnl':    return <window.NetPnLPanel/>;
      case 'compare':   return <window.ComparePanel/>;
      case 'bridge':    return <window.BridgePanel/>;
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
        {tweaks.banner && bannerAlert && (
          <AlertBanner alert={bannerAlert}
            onDismiss={() => setBannerAlert(null)}
            onGoto={() => setTab('alerts')} />
        )}
        {renderTab()}
      </div>

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
  useEffect(() => { const id = setTimeout(onClose, 5500); return () => clearTimeout(id); }, []);
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

