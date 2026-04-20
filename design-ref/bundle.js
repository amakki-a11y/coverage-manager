/* bundled */

// ===== icons.jsx =====
/* global React */
// Tiny icon set (Lucide-style strokes, 16px). All inherit currentColor.

const Icon = ({
  d,
  size = 16,
  fill = 'none',
  strokeWidth = 1.6,
  style
}) => /*#__PURE__*/React.createElement("svg", {
  width: size,
  height: size,
  viewBox: "0 0 24 24",
  fill: fill,
  stroke: "currentColor",
  strokeWidth: strokeWidth,
  strokeLinecap: "round",
  strokeLinejoin: "round",
  style: style,
  className: "icon"
}, d);
const I = {
  grid: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("rect", {
      x: "3",
      y: "3",
      width: "7",
      height: "7",
      rx: "1"
    }), /*#__PURE__*/React.createElement("rect", {
      x: "14",
      y: "3",
      width: "7",
      height: "7",
      rx: "1"
    }), /*#__PURE__*/React.createElement("rect", {
      x: "3",
      y: "14",
      width: "7",
      height: "7",
      rx: "1"
    }), /*#__PURE__*/React.createElement("rect", {
      x: "14",
      y: "14",
      width: "7",
      height: "7",
      rx: "1"
    }))
  }),
  list: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("line", {
      x1: "8",
      y1: "6",
      x2: "21",
      y2: "6"
    }), /*#__PURE__*/React.createElement("line", {
      x1: "8",
      y1: "12",
      x2: "21",
      y2: "12"
    }), /*#__PURE__*/React.createElement("line", {
      x1: "8",
      y1: "18",
      x2: "21",
      y2: "18"
    }), /*#__PURE__*/React.createElement("circle", {
      cx: "4",
      cy: "6",
      r: "1"
    }), /*#__PURE__*/React.createElement("circle", {
      cx: "4",
      cy: "12",
      r: "1"
    }), /*#__PURE__*/React.createElement("circle", {
      cx: "4",
      cy: "18",
      r: "1"
    }))
  }),
  pulse: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement("polyline", {
      points: "3 12 7 12 10 6 14 18 17 12 21 12"
    })
  }),
  wallet: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("path", {
      d: "M3 7a2 2 0 012-2h14v12a2 2 0 01-2 2H5a2 2 0 01-2-2V7z"
    }), /*#__PURE__*/React.createElement("path", {
      d: "M16 12h3"
    }))
  }),
  layers: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("path", {
      d: "M12 3L3 8l9 5 9-5-9-5z"
    }), /*#__PURE__*/React.createElement("path", {
      d: "M3 14l9 5 9-5"
    }))
  }),
  split: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("path", {
      d: "M4 4h6v16H4z"
    }), /*#__PURE__*/React.createElement("path", {
      d: "M14 4h6v16h-6z"
    }))
  }),
  arrows: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("path", {
      d: "M7 7h10M7 7l3-3M7 7l3 3"
    }), /*#__PURE__*/React.createElement("path", {
      d: "M17 17H7M17 17l-3-3M17 17l-3 3"
    }))
  }),
  bridge: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("path", {
      d: "M3 12h18"
    }), /*#__PURE__*/React.createElement("path", {
      d: "M7 12V8a2 2 0 012-2h6a2 2 0 012 2v4"
    }), /*#__PURE__*/React.createElement("path", {
      d: "M5 16v-4M19 16v-4"
    }))
  }),
  map: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("polygon", {
      points: "3 6 9 3 15 6 21 3 21 18 15 21 9 18 3 21 3 6"
    }), /*#__PURE__*/React.createElement("line", {
      x1: "9",
      y1: "3",
      x2: "9",
      y2: "18"
    }), /*#__PURE__*/React.createElement("line", {
      x1: "15",
      y1: "6",
      x2: "15",
      y2: "21"
    }))
  }),
  gear: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("circle", {
      cx: "12",
      cy: "12",
      r: "3"
    }), /*#__PURE__*/React.createElement("path", {
      d: "M19.4 15a1.7 1.7 0 00.3 1.8l.1.1a2 2 0 11-2.8 2.8l-.1-.1a1.7 1.7 0 00-1.8-.3 1.7 1.7 0 00-1 1.5v.2a2 2 0 11-4 0v-.1a1.7 1.7 0 00-1.1-1.6 1.7 1.7 0 00-1.8.3l-.1.1a2 2 0 11-2.8-2.8l.1-.1a1.7 1.7 0 00.3-1.8 1.7 1.7 0 00-1.5-1H3a2 2 0 110-4h.1a1.7 1.7 0 001.6-1.1 1.7 1.7 0 00-.3-1.8l-.1-.1a2 2 0 112.8-2.8l.1.1a1.7 1.7 0 001.8.3H9a1.7 1.7 0 001-1.5V3a2 2 0 114 0v.1a1.7 1.7 0 001 1.5 1.7 1.7 0 001.8-.3l.1-.1a2 2 0 112.8 2.8l-.1.1a1.7 1.7 0 00-.3 1.8v.1a1.7 1.7 0 001.5 1H21a2 2 0 110 4h-.1a1.7 1.7 0 00-1.5 1z"
    }))
  }),
  bell: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("path", {
      d: "M18 8A6 6 0 006 8c0 7-3 9-3 9h18s-3-2-3-9"
    }), /*#__PURE__*/React.createElement("path", {
      d: "M13.7 21a2 2 0 01-3.4 0"
    }))
  }),
  sun: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("circle", {
      cx: "12",
      cy: "12",
      r: "4"
    }), /*#__PURE__*/React.createElement("path", {
      d: "M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"
    }))
  }),
  moon: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement("path", {
      d: "M21 12.8A9 9 0 1111.2 3a7 7 0 009.8 9.8z"
    })
  }),
  search: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("circle", {
      cx: "11",
      cy: "11",
      r: "7"
    }), /*#__PURE__*/React.createElement("line", {
      x1: "21",
      y1: "21",
      x2: "16.65",
      y2: "16.65"
    }))
  }),
  sliders: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("line", {
      x1: "4",
      y1: "21",
      x2: "4",
      y2: "14"
    }), /*#__PURE__*/React.createElement("line", {
      x1: "4",
      y1: "10",
      x2: "4",
      y2: "3"
    }), /*#__PURE__*/React.createElement("line", {
      x1: "12",
      y1: "21",
      x2: "12",
      y2: "12"
    }), /*#__PURE__*/React.createElement("line", {
      x1: "12",
      y1: "8",
      x2: "12",
      y2: "3"
    }), /*#__PURE__*/React.createElement("line", {
      x1: "20",
      y1: "21",
      x2: "20",
      y2: "16"
    }), /*#__PURE__*/React.createElement("line", {
      x1: "20",
      y1: "12",
      x2: "20",
      y2: "3"
    }), /*#__PURE__*/React.createElement("circle", {
      cx: "4",
      cy: "12",
      r: "2"
    }), /*#__PURE__*/React.createElement("circle", {
      cx: "12",
      cy: "10",
      r: "2"
    }), /*#__PURE__*/React.createElement("circle", {
      cx: "20",
      cy: "14",
      r: "2"
    }))
  }),
  close: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("line", {
      x1: "6",
      y1: "6",
      x2: "18",
      y2: "18"
    }), /*#__PURE__*/React.createElement("line", {
      x1: "6",
      y1: "18",
      x2: "18",
      y2: "6"
    }))
  }),
  check: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement("polyline", {
      points: "4 12 10 18 20 6"
    })
  }),
  warn: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("path", {
      d: "M12 2L2 21h20L12 2z"
    }), /*#__PURE__*/React.createElement("line", {
      x1: "12",
      y1: "9",
      x2: "12",
      y2: "14"
    }), /*#__PURE__*/React.createElement("circle", {
      cx: "12",
      cy: "17.5",
      r: "0.8",
      fill: "currentColor"
    }))
  }),
  info: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("circle", {
      cx: "12",
      cy: "12",
      r: "9"
    }), /*#__PURE__*/React.createElement("line", {
      x1: "12",
      y1: "11",
      x2: "12",
      y2: "16"
    }), /*#__PURE__*/React.createElement("circle", {
      cx: "12",
      cy: "8",
      r: "0.8",
      fill: "currentColor"
    }))
  }),
  bolt: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement("polygon", {
      points: "13 2 3 14 12 14 11 22 21 10 12 10 13 2",
      fill: "currentColor",
      stroke: "none"
    })
  }),
  plus: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("line", {
      x1: "12",
      y1: "5",
      x2: "12",
      y2: "19"
    }), /*#__PURE__*/React.createElement("line", {
      x1: "5",
      y1: "12",
      x2: "19",
      y2: "12"
    }))
  }),
  drag: () => /*#__PURE__*/React.createElement(Icon, {
    size: 12,
    d: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("circle", {
      cx: "9",
      cy: "6",
      r: "1",
      fill: "currentColor"
    }), /*#__PURE__*/React.createElement("circle", {
      cx: "15",
      cy: "6",
      r: "1",
      fill: "currentColor"
    }), /*#__PURE__*/React.createElement("circle", {
      cx: "9",
      cy: "12",
      r: "1",
      fill: "currentColor"
    }), /*#__PURE__*/React.createElement("circle", {
      cx: "15",
      cy: "12",
      r: "1",
      fill: "currentColor"
    }), /*#__PURE__*/React.createElement("circle", {
      cx: "9",
      cy: "18",
      r: "1",
      fill: "currentColor"
    }), /*#__PURE__*/React.createElement("circle", {
      cx: "15",
      cy: "18",
      r: "1",
      fill: "currentColor"
    }))
  }),
  chevLeft: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement("polyline", {
      points: "15 6 9 12 15 18"
    })
  }),
  chevRight: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement("polyline", {
      points: "9 6 15 12 9 18"
    })
  }),
  chevDown: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement("polyline", {
      points: "6 9 12 15 18 9"
    })
  }),
  download: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("path", {
      d: "M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4"
    }), /*#__PURE__*/React.createElement("polyline", {
      points: "7 10 12 15 17 10"
    }), /*#__PURE__*/React.createElement("line", {
      x1: "12",
      y1: "15",
      x2: "12",
      y2: "3"
    }))
  }),
  refresh: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("polyline", {
      points: "23 4 23 10 17 10"
    }), /*#__PURE__*/React.createElement("polyline", {
      points: "1 20 1 14 7 14"
    }), /*#__PURE__*/React.createElement("path", {
      d: "M3.5 9a9 9 0 0114.9-3.4L23 10M1 14l4.6 4.4A9 9 0 0020.5 15"
    }))
  }),
  kbd: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("rect", {
      x: "2",
      y: "4",
      width: "20",
      height: "16",
      rx: "2"
    }), /*#__PURE__*/React.createElement("path", {
      d: "M6 8h2M10 8h2M14 8h2M18 8h2M6 12h2M10 12h8M6 16h12"
    }))
  }),
  camera: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("path", {
      d: "M23 19a2 2 0 01-2 2H3a2 2 0 01-2-2V8a2 2 0 012-2h4l2-3h6l2 3h4a2 2 0 012 2z"
    }), /*#__PURE__*/React.createElement("circle", {
      cx: "12",
      cy: "13",
      r: "4"
    }))
  }),
  book: () => /*#__PURE__*/React.createElement(Icon, {
    d: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("path", {
      d: "M2 3h6a4 4 0 014 4v14a3 3 0 00-3-3H2z"
    }), /*#__PURE__*/React.createElement("path", {
      d: "M22 3h-6a4 4 0 00-4 4v14a3 3 0 013-3h7z"
    }))
  })
};
window.I = I;

// ===== ExposureTable.jsx =====

const {
  useState,
  useEffect,
  useRef,
  useMemo,
  useCallback
} = React;

// ========== Formatters ==========
function fmt(v, d = 2) {
  if (v === undefined || v === null) return '';
  return (+v).toLocaleString('en-US', {
    minimumFractionDigits: d,
    maximumFractionDigits: d
  });
}
function fp(v, d = 2) {
  const n = +v || 0;
  return (n >= 0 ? '+' : '') + n.toLocaleString('en-US', {
    minimumFractionDigits: d,
    maximumFractionDigits: d
  });
}
function pc(v) {
  return v > 0 ? 'pos' : v < 0 ? 'neg' : 't2';
}
function hedgeColorClass(r) {
  return r >= 80 ? 'pos' : r >= 50 ? 'amb' : 'neg';
}
function hedgeColorVar(r) {
  return r >= 80 ? 'var(--green)' : r >= 50 ? 'var(--amber)' : 'var(--red)';
}
function assetColor(cls) {
  return {
    metal: 'var(--amber)',
    fx: 'var(--blue)',
    idx: 'var(--purple)',
    crypto: 'var(--teal)',
    comm: '#EF9A5A'
  }[cls] || 'var(--t3)';
}
function assetBg(cls) {
  return {
    metal: 'var(--amber-dim)',
    fx: 'var(--blue-dim)',
    idx: 'var(--purple-dim)',
    crypto: 'var(--teal-dim)',
    comm: 'rgba(239,154,90,0.14)'
  }[cls] || 'var(--bg-3)';
}
window.fmt = fmt;
window.fp = fp;
window.pc = pc;

// ========== Ticking prices hook ==========
function useTickingPrices(symbols) {
  const [prices, setPrices] = useState(() => {
    const m = {};
    for (const s of symbols) m[s.sym] = {
      bid: s.bid,
      dir: 'flat',
      key: 0
    };
    return m;
  });
  useEffect(() => {
    const id = setInterval(() => {
      setPrices(prev => {
        const next = {
          ...prev
        };
        // tick 2-3 random symbols each beat
        const picks = new Set();
        while (picks.size < 3) picks.add(symbols[Math.floor(Math.random() * symbols.length)].sym);
        for (const sym of picks) {
          const s = symbols.find(x => x.sym === sym);
          const p = prev[sym] || {
            bid: s.bid
          };
          const delta = (Math.random() - 0.48) * s.vol;
          const newBid = +(p.bid + delta).toFixed(s.digits);
          next[sym] = {
            bid: newBid,
            dir: newBid > p.bid ? 'up' : newBid < p.bid ? 'down' : p.dir,
            key: (p.key || 0) + 1
          };
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
function AssetMark({
  sym,
  cls
}) {
  const letters = sym.slice(0, 2);
  return /*#__PURE__*/React.createElement("span", {
    className: "asset-icon",
    style: {
      background: assetBg(cls),
      color: assetColor(cls)
    }
  }, letters);
}
window.AssetMark = AssetMark;

// ========== Hedge bar ==========
function HedgeBar({
  pct
}) {
  const clamped = Math.min(100, Math.max(0, pct));
  return /*#__PURE__*/React.createElement("span", null, /*#__PURE__*/React.createElement("span", {
    className: "hedge-bar"
  }, /*#__PURE__*/React.createElement("span", {
    className: "fill",
    style: {
      width: clamped + '%',
      background: hedgeColorVar(pct)
    }
  })), /*#__PURE__*/React.createElement("span", {
    className: "hedge-pct",
    style: {
      color: hedgeColorVar(pct)
    }
  }, pct.toFixed(0), "%"));
}
window.HedgeBar = HedgeBar;

// ========== Exposure Table ==========
function ExposureTable({
  showGrid,
  onOpenHedge,
  layout
}) {
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
      flashKeys.current[sym] = {
        dir: p.dir,
        key: p.key
      };
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
      const hedge = Math.abs(bbNet) > 0.001 ? Math.min(999, Math.abs(covNet) / Math.abs(bbNet) * 100) : 0;
      const closed = CLOSED[sym];
      return {
        s,
        e,
        bbNet,
        covNet,
        toCover,
        netPnL,
        hedge,
        closed,
        bbClosed: closed.bb,
        covClosed: closed.cov
      };
    });
    if (sort === 'custom') return list;
    const dir = sortAsc ? 1 : -1;
    const getKey = r => {
      switch (sort) {
        case 'symbol':
          return r.s.sym;
        case 'bbNet':
          return r.bbNet;
        case 'bbPnL':
          return r.e.bbPnL;
        case 'covNet':
          return r.covNet;
        case 'covPnL':
          return r.e.covPnL;
        case 'toCover':
          return r.toCover;
        case 'netPnL':
          return r.netPnL;
        case 'hedge':
          return r.hedge;
        default:
          return 0;
      }
    };
    return [...list].sort((a, b) => {
      const ak = getKey(a),
        bk = getKey(b);
      if (typeof ak === 'string') return dir * ak.localeCompare(bk);
      return dir * (ak - bk);
    });
  }, [order, sort, sortAsc]);
  const totals = useMemo(() => rows.reduce((a, r) => ({
    bbBuy: a.bbBuy + r.e.bbBuy,
    bbSell: a.bbSell + r.e.bbSell,
    bbNet: a.bbNet + r.bbNet,
    bbPnL: a.bbPnL + r.e.bbPnL,
    covBuy: a.covBuy + r.e.covBuy,
    covSell: a.covSell + r.e.covSell,
    covNet: a.covNet + r.covNet,
    covPnL: a.covPnL + r.e.covPnL,
    netPnL: a.netPnL + r.netPnL,
    bbClosed: a.bbClosed + r.bbClosed,
    covClosed: a.covClosed + r.covClosed
  }), {
    bbBuy: 0,
    bbSell: 0,
    bbNet: 0,
    bbPnL: 0,
    covBuy: 0,
    covSell: 0,
    covNet: 0,
    covPnL: 0,
    netPnL: 0,
    bbClosed: 0,
    covClosed: 0
  }), [rows]);
  const onDragStart = sym => {
    dragFrom.current = sym;
  };
  const onDragOver = (e, sym) => {
    e.preventDefault();
    dragTo.current = sym;
  };
  const onDrop = () => {
    const from = dragFrom.current,
      to = dragTo.current;
    if (!from || !to || from === to) return;
    const next = [...order];
    const fi = next.indexOf(from),
      ti = next.indexOf(to);
    next.splice(fi, 1);
    next.splice(ti, 0, from);
    setOrder(next);
    setSort('custom');
    dragFrom.current = dragTo.current = null;
  };
  const handleSort = k => {
    if (sort === k) setSortAsc(a => !a);else {
      setSort(k);
      setSortAsc(k === 'symbol');
    }
  };
  const arrow = k => sort === k ? sortAsc ? ' ↑' : ' ↓' : '';
  if (layout === 'cards') return /*#__PURE__*/React.createElement(ExposureCards, {
    rows: rows,
    prices: prices,
    onOpenHedge: onOpenHedge
  });
  return /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement(ExposureToolbar, {
    sort: sort,
    setSort: setSort,
    sortAsc: sortAsc,
    setSortAsc: setSortAsc,
    dateFrom: dateFrom,
    setDateFrom: setDateFrom,
    dateTo: dateTo,
    setDateTo: setDateTo
  }), /*#__PURE__*/React.createElement("div", {
    className: `exposure ${showGrid ? 'grid-on' : ''}`
  }, /*#__PURE__*/React.createElement("table", null, /*#__PURE__*/React.createElement("thead", null, /*#__PURE__*/React.createElement("tr", null, /*#__PURE__*/React.createElement("th", {
    style: {
      width: 160,
      textAlign: 'left',
      paddingLeft: 16
    },
    rowSpan: 2
  }), /*#__PURE__*/React.createElement("th", {
    style: {
      width: 36
    },
    rowSpan: 2
  }), /*#__PURE__*/React.createElement("th", {
    className: "group clients sec-l",
    colSpan: 4
  }, "CLIENTS"), /*#__PURE__*/React.createElement("th", {
    className: "group coverage sec-l",
    colSpan: 4
  }, "COVERAGE"), /*#__PURE__*/React.createElement("th", {
    className: "group summary sec-l",
    colSpan: 4
  }, "SUMMARY")), /*#__PURE__*/React.createElement("tr", {
    className: "sub"
  }, /*#__PURE__*/React.createElement("th", {
    className: "sec-l"
  }, "Buy"), /*#__PURE__*/React.createElement("th", null, "Sell"), /*#__PURE__*/React.createElement("th", null, /*#__PURE__*/React.createElement("button", {
    className: "sort-btn",
    onClick: () => handleSort('bbNet')
  }, "Net", arrow('bbNet'))), /*#__PURE__*/React.createElement("th", null, /*#__PURE__*/React.createElement("button", {
    className: "sort-btn",
    onClick: () => handleSort('bbPnL')
  }, "P&L", arrow('bbPnL'))), /*#__PURE__*/React.createElement("th", {
    className: "sec-l"
  }, "Buy"), /*#__PURE__*/React.createElement("th", null, "Sell"), /*#__PURE__*/React.createElement("th", null, /*#__PURE__*/React.createElement("button", {
    className: "sort-btn",
    onClick: () => handleSort('covNet')
  }, "Net", arrow('covNet'))), /*#__PURE__*/React.createElement("th", null, /*#__PURE__*/React.createElement("button", {
    className: "sort-btn",
    onClick: () => handleSort('covPnL')
  }, "P&L", arrow('covPnL'))), /*#__PURE__*/React.createElement("th", {
    className: "sec-l"
  }, /*#__PURE__*/React.createElement("button", {
    className: "sort-btn",
    onClick: () => handleSort('toCover')
  }, "To Cover", arrow('toCover'))), /*#__PURE__*/React.createElement("th", null, /*#__PURE__*/React.createElement("button", {
    className: "sort-btn",
    onClick: () => handleSort('netPnL')
  }, "Net P&L", arrow('netPnL'))), /*#__PURE__*/React.createElement("th", null, /*#__PURE__*/React.createElement("button", {
    className: "sort-btn",
    onClick: () => handleSort('hedge')
  }, "Hedge", arrow('hedge'))), /*#__PURE__*/React.createElement("th", null, "Action"))), /*#__PURE__*/React.createElement("tbody", null, rows.map((r, i) => {
    const price = prices[r.s.sym] || {
      bid: r.s.bid,
      dir: 'flat',
      key: 0
    };
    const flashCls = price.dir === 'up' ? 'flash-up' : price.dir === 'down' ? 'flash-down' : '';
    return /*#__PURE__*/React.createElement(React.Fragment, {
      key: r.s.sym
    }, /*#__PURE__*/React.createElement("tr", {
      className: `open-row sym-div ${flashCls}`,
      key: `${r.s.sym}-${price.key}`,
      draggable: true,
      onDragStart: () => onDragStart(r.s.sym),
      onDragOver: e => onDragOver(e, r.s.sym),
      onDrop: onDrop
    }, /*#__PURE__*/React.createElement("td", {
      rowSpan: 2,
      className: "sym-cell"
    }, /*#__PURE__*/React.createElement("span", {
      className: "drag-handle"
    }, /*#__PURE__*/React.createElement(I.drag, null)), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        alignItems: 'center'
      }
    }, /*#__PURE__*/React.createElement(AssetMark, {
      sym: r.s.sym,
      cls: r.s.cls
    }), /*#__PURE__*/React.createElement("span", {
      className: "sym-name"
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        fontWeight: 600,
        fontSize: 13
      }
    }, r.s.sym))), /*#__PURE__*/React.createElement("div", {
      className: `sym-bid ${price.dir}`
    }, /*#__PURE__*/React.createElement("span", {
      className: "arrow"
    }, price.dir === 'up' ? '▲' : price.dir === 'down' ? '▼' : '·'), " ", price.bid.toFixed(r.s.digits))), /*#__PURE__*/React.createElement("td", {
      className: "oc open"
    }, "O"), /*#__PURE__*/React.createElement("td", {
      className: "pos sec-l"
    }, fmt(r.e.bbBuy)), /*#__PURE__*/React.createElement("td", {
      className: "neg"
    }, fmt(r.e.bbSell)), /*#__PURE__*/React.createElement("td", {
      className: pc(r.bbNet),
      style: {
        fontWeight: 600
      }
    }, fmt(r.bbNet)), /*#__PURE__*/React.createElement("td", {
      className: pc(r.e.bbPnL)
    }, fp(r.e.bbPnL)), /*#__PURE__*/React.createElement("td", {
      className: "pos sec-l"
    }, fmt(r.e.covBuy)), /*#__PURE__*/React.createElement("td", {
      className: "neg"
    }, fmt(r.e.covSell)), /*#__PURE__*/React.createElement("td", {
      className: pc(r.covNet),
      style: {
        fontWeight: 600
      }
    }, fmt(r.covNet)), /*#__PURE__*/React.createElement("td", {
      className: pc(r.e.covPnL)
    }, fp(r.e.covPnL)), /*#__PURE__*/React.createElement("td", {
      className: `sec-l ${pc(-r.toCover)}`,
      style: {
        fontWeight: 600
      }
    }, Math.abs(r.toCover) < 0.005 ? '—' : fp(-r.toCover)), /*#__PURE__*/React.createElement("td", {
      className: pc(r.netPnL),
      style: {
        fontWeight: 700
      }
    }, fp(r.netPnL)), /*#__PURE__*/React.createElement("td", null, /*#__PURE__*/React.createElement(HedgeBar, {
      pct: r.hedge
    })), /*#__PURE__*/React.createElement("td", null, Math.abs(r.toCover) > 0.01 ? /*#__PURE__*/React.createElement("button", {
      className: `hedge-btn ${r.toCover < 0 ? 'sell' : 'buy'}`,
      onClick: () => onOpenHedge(r)
    }, r.toCover < 0 ? 'SELL ' : 'BUY ', Math.abs(r.toCover).toFixed(2)) : /*#__PURE__*/React.createElement("span", {
      style: {
        color: 'var(--t4)',
        fontSize: 11
      }
    }, "\u2014"))), /*#__PURE__*/React.createElement("tr", {
      className: "close-row"
    }, /*#__PURE__*/React.createElement("td", {
      className: "oc close"
    }, "C"), /*#__PURE__*/React.createElement("td", {
      className: "pos sec-l"
    }, r.closed.bb >= 0 ? fmt(Math.abs(r.e.bbBuy) * 0.4) : '—'), /*#__PURE__*/React.createElement("td", {
      className: "neg"
    }, r.closed.bb <= 0 ? fmt(Math.abs(r.e.bbSell) * 0.4) : '—'), /*#__PURE__*/React.createElement("td", {
      className: "t3"
    }, fmt((Math.abs(r.e.bbBuy) + Math.abs(r.e.bbSell)) * 0.4)), /*#__PURE__*/React.createElement("td", {
      className: pc(r.closed.bb),
      style: {
        fontWeight: 600
      }
    }, fp(r.closed.bb)), /*#__PURE__*/React.createElement("td", {
      className: "pos sec-l"
    }, r.closed.cov >= 0 ? fmt(Math.abs(r.e.covBuy) * 0.35) : '—'), /*#__PURE__*/React.createElement("td", {
      className: "neg"
    }, r.closed.cov <= 0 ? fmt(Math.abs(r.e.covSell) * 0.35) : '—'), /*#__PURE__*/React.createElement("td", {
      className: "t3"
    }, fmt((Math.abs(r.e.covBuy) + Math.abs(r.e.covSell)) * 0.35)), /*#__PURE__*/React.createElement("td", {
      className: pc(r.closed.cov),
      style: {
        fontWeight: 600
      }
    }, fp(r.closed.cov)), /*#__PURE__*/React.createElement("td", {
      className: "sec-l"
    }), /*#__PURE__*/React.createElement("td", {
      className: pc(r.closed.cov - r.closed.bb),
      style: {
        fontWeight: 600
      }
    }, fp(r.closed.cov - r.closed.bb)), /*#__PURE__*/React.createElement("td", {
      className: "t3",
      style: {
        fontSize: 10.5
      }
    }, "settled"), /*#__PURE__*/React.createElement("td", null)));
  })), /*#__PURE__*/React.createElement("tfoot", null, /*#__PURE__*/React.createElement("tr", {
    className: "total-row"
  }, /*#__PURE__*/React.createElement("td", {
    style: {
      paddingLeft: 16,
      textAlign: 'left',
      fontFamily: 'var(--font-ui)',
      color: 'var(--t2)'
    }
  }, "TOTAL"), /*#__PURE__*/React.createElement("td", {
    className: "oc open"
  }, "O"), /*#__PURE__*/React.createElement("td", {
    className: "pos sec-l"
  }, fmt(totals.bbBuy)), /*#__PURE__*/React.createElement("td", {
    className: "neg"
  }, fmt(totals.bbSell)), /*#__PURE__*/React.createElement("td", {
    className: pc(totals.bbNet)
  }, fmt(totals.bbNet)), /*#__PURE__*/React.createElement("td", {
    className: pc(totals.bbPnL)
  }, fp(totals.bbPnL)), /*#__PURE__*/React.createElement("td", {
    className: "pos sec-l"
  }, fmt(totals.covBuy)), /*#__PURE__*/React.createElement("td", {
    className: "neg"
  }, fmt(totals.covSell)), /*#__PURE__*/React.createElement("td", {
    className: pc(totals.covNet)
  }, fmt(totals.covNet)), /*#__PURE__*/React.createElement("td", {
    className: pc(totals.covPnL)
  }, fp(totals.covPnL)), /*#__PURE__*/React.createElement("td", {
    className: `sec-l ${pc(-(totals.bbNet - totals.covNet))}`
  }, fp(-(totals.bbNet - totals.covNet))), /*#__PURE__*/React.createElement("td", {
    className: pc(totals.netPnL)
  }, fp(totals.netPnL)), /*#__PURE__*/React.createElement("td", {
    colSpan: 2
  })), /*#__PURE__*/React.createElement("tr", {
    className: "total-row",
    style: {
      fontSize: 11
    }
  }, /*#__PURE__*/React.createElement("td", {
    style: {
      paddingLeft: 16,
      textAlign: 'left',
      fontFamily: 'var(--font-ui)',
      color: 'var(--t3)'
    }
  }, "TOTAL"), /*#__PURE__*/React.createElement("td", {
    className: "oc close"
  }, "C"), /*#__PURE__*/React.createElement("td", {
    colSpan: 3,
    className: "sec-l t3",
    style: {
      textAlign: 'center'
    }
  }, "settled"), /*#__PURE__*/React.createElement("td", {
    className: pc(totals.bbClosed),
    style: {
      fontWeight: 600
    }
  }, fp(totals.bbClosed)), /*#__PURE__*/React.createElement("td", {
    colSpan: 3,
    className: "sec-l t3",
    style: {
      textAlign: 'center'
    }
  }, "settled"), /*#__PURE__*/React.createElement("td", {
    className: pc(totals.covClosed),
    style: {
      fontWeight: 600
    }
  }, fp(totals.covClosed)), /*#__PURE__*/React.createElement("td", {
    className: "sec-l"
  }), /*#__PURE__*/React.createElement("td", {
    className: pc(totals.covClosed - totals.bbClosed),
    style: {
      fontWeight: 700
    }
  }, fp(totals.covClosed - totals.bbClosed)), /*#__PURE__*/React.createElement("td", {
    colSpan: 2
  }))))));
}
function ExposureToolbar({
  sort,
  setSort,
  sortAsc,
  setSortAsc,
  dateFrom,
  setDateFrom,
  dateTo,
  setDateTo
}) {
  const [persona, setPersona] = useState('dealer');
  const [group, setGroup] = useState('ALL');
  return /*#__PURE__*/React.createElement("div", {
    className: "toolbar"
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      color: 'var(--t3)',
      fontSize: 11,
      fontWeight: 600,
      letterSpacing: '0.06em'
    }
  }, "SORT"), /*#__PURE__*/React.createElement("select", {
    className: "select",
    value: sort,
    onChange: e => setSort(e.target.value)
  }, /*#__PURE__*/React.createElement("option", {
    value: "custom"
  }, "Custom (drag)"), /*#__PURE__*/React.createElement("option", {
    value: "symbol"
  }, "Symbol"), /*#__PURE__*/React.createElement("option", {
    value: "bbNet"
  }, "Client Net"), /*#__PURE__*/React.createElement("option", {
    value: "bbPnL"
  }, "Client P&L"), /*#__PURE__*/React.createElement("option", {
    value: "covNet"
  }, "Coverage Net"), /*#__PURE__*/React.createElement("option", {
    value: "covPnL"
  }, "Coverage P&L"), /*#__PURE__*/React.createElement("option", {
    value: "toCover"
  }, "To Cover"), /*#__PURE__*/React.createElement("option", {
    value: "netPnL"
  }, "Net P&L"), /*#__PURE__*/React.createElement("option", {
    value: "hedge"
  }, "Hedge %")), /*#__PURE__*/React.createElement("button", {
    className: "icon-btn",
    onClick: () => setSortAsc(!sortAsc),
    title: sortAsc ? 'Ascending' : 'Descending'
  }, sortAsc ? '↑' : '↓'), /*#__PURE__*/React.createElement("div", {
    className: "sep"
  }), /*#__PURE__*/React.createElement("span", {
    style: {
      color: 'var(--t3)',
      fontSize: 11,
      fontWeight: 600,
      letterSpacing: '0.06em'
    }
  }, "GROUP"), /*#__PURE__*/React.createElement("select", {
    className: "select",
    value: group,
    onChange: e => setGroup(e.target.value)
  }, /*#__PURE__*/React.createElement("option", {
    value: "ALL"
  }, "All accounts"), /*#__PURE__*/React.createElement("option", null, "Real-USD"), /*#__PURE__*/React.createElement("option", null, "Real-EUR"), /*#__PURE__*/React.createElement("option", null, "VIP"), /*#__PURE__*/React.createElement("option", null, "Demo")), /*#__PURE__*/React.createElement("div", {
    className: "sep"
  }), /*#__PURE__*/React.createElement("span", {
    style: {
      color: 'var(--t3)',
      fontSize: 11,
      fontWeight: 600,
      letterSpacing: '0.06em'
    }
  }, "CLOSED"), /*#__PURE__*/React.createElement("input", {
    type: "date",
    className: "date-input",
    value: dateFrom,
    onChange: e => setDateFrom(e.target.value)
  }), /*#__PURE__*/React.createElement("span", {
    style: {
      color: 'var(--t3)',
      fontSize: 11
    }
  }, "\u2192"), /*#__PURE__*/React.createElement("input", {
    type: "date",
    className: "date-input",
    value: dateTo,
    onChange: e => setDateTo(e.target.value)
  }), /*#__PURE__*/React.createElement("div", {
    className: "spacer"
  }), /*#__PURE__*/React.createElement("span", {
    className: "chip live"
  }, /*#__PURE__*/React.createElement("span", {
    className: "dot"
  }), "Live \xB7 10 updates/sec"));
}

// Card layout variant
function ExposureCards({
  rows,
  prices,
  onOpenHedge
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      padding: 16,
      overflow: 'auto',
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: 'repeat(auto-fill, minmax(320px, 1fr))',
      gap: 12
    }
  }, rows.map(r => {
    const price = prices[r.s.sym] || {
      bid: r.s.bid,
      dir: 'flat'
    };
    return /*#__PURE__*/React.createElement("div", {
      key: r.s.sym,
      className: "card",
      style: {
        padding: 14,
        display: 'flex',
        flexDirection: 'column',
        gap: 10
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: 10
      }
    }, /*#__PURE__*/React.createElement(AssetMark, {
      sym: r.s.sym,
      cls: r.s.cls
    }), /*#__PURE__*/React.createElement("div", {
      style: {
        flex: 1
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        fontWeight: 600,
        fontSize: 14
      }
    }, r.s.sym)), /*#__PURE__*/React.createElement("div", {
      className: `mono ${price.dir === 'up' ? 'pos' : price.dir === 'down' ? 'neg' : 't2'}`,
      style: {
        fontSize: 14,
        fontWeight: 600
      }
    }, price.bid.toFixed(r.s.digits))), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: 8,
        marginTop: 4
      }
    }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
      style: {
        color: 'var(--blue)',
        fontSize: 10,
        fontWeight: 700,
        letterSpacing: '0.08em'
      }
    }, "CLIENTS"), /*#__PURE__*/React.createElement("div", {
      className: "mono",
      style: {
        fontSize: 15,
        fontWeight: 600
      }
    }, fmt(r.bbNet)), /*#__PURE__*/React.createElement("div", {
      className: `mono ${pc(r.e.bbPnL)}`,
      style: {
        fontSize: 12
      }
    }, fp(r.e.bbPnL))), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
      style: {
        color: 'var(--teal)',
        fontSize: 10,
        fontWeight: 700,
        letterSpacing: '0.08em'
      }
    }, "COVERAGE"), /*#__PURE__*/React.createElement("div", {
      className: "mono",
      style: {
        fontSize: 15,
        fontWeight: 600
      }
    }, fmt(r.covNet)), /*#__PURE__*/React.createElement("div", {
      className: `mono ${pc(r.e.covPnL)}`,
      style: {
        fontSize: 12
      }
    }, fp(r.e.covPnL)))), /*#__PURE__*/React.createElement("div", {
      style: {
        paddingTop: 8,
        borderTop: '1px solid var(--divider)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between'
      }
    }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
      style: {
        color: 'var(--t3)',
        fontSize: 10,
        fontWeight: 600,
        letterSpacing: '0.06em'
      }
    }, "HEDGE"), /*#__PURE__*/React.createElement(HedgeBar, {
      pct: r.hedge
    })), /*#__PURE__*/React.createElement("div", {
      style: {
        textAlign: 'right'
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        color: 'var(--t3)',
        fontSize: 10,
        fontWeight: 600,
        letterSpacing: '0.06em'
      }
    }, "NET P&L"), /*#__PURE__*/React.createElement("div", {
      className: `mono ${pc(r.netPnL)}`,
      style: {
        fontSize: 14,
        fontWeight: 700
      }
    }, fp(r.netPnL))), Math.abs(r.toCover) > 0.01 && /*#__PURE__*/React.createElement("button", {
      className: `hedge-btn ${r.toCover < 0 ? 'sell' : 'buy'}`,
      onClick: () => onOpenHedge(r),
      style: {
        padding: '6px 12px',
        fontSize: 11
      }
    }, r.toCover < 0 ? 'SELL' : 'BUY', " ", Math.abs(r.toCover).toFixed(2))));
  })));
}
window.ExposureTable = ExposureTable;

// ===== HedgeModal.jsx =====

// ========== Hedge Modal ==========
function HedgeModal({
  row,
  onClose,
  onConfirm
}) {
  const dir = row.toCover < 0 ? 'SELL' : 'BUY';
  const amount = Math.abs(row.toCover);
  const [vol, setVol] = useState(amount.toFixed(2));
  const [lpAcct, setLpAcct] = useState('96900 · fXGROW LP');
  const [partial, setPartial] = useState(100);
  const [confirm, setConfirm] = useState(true);
  const volN = parseFloat(vol) || 0;
  const price = window.SYMBOLS.find(s => s.sym === row.s.sym).bid;
  return /*#__PURE__*/React.createElement("div", {
    className: "modal-backdrop",
    onClick: onClose
  }, /*#__PURE__*/React.createElement("div", {
    className: "modal",
    onClick: e => e.stopPropagation(),
    style: {
      width: 520
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "modal-header"
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 12
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 36,
      height: 36,
      borderRadius: 8,
      background: dir === 'BUY' ? 'var(--green-dim)' : 'var(--red-dim)',
      color: dir === 'BUY' ? 'var(--green)' : 'var(--red)',
      display: 'grid',
      placeItems: 'center',
      fontWeight: 700,
      fontSize: 11
    }
  }, dir), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    className: "modal-title"
  }, "Hedge ", row.s.sym, /*#__PURE__*/React.createElement("span", {
    style: {
      color: 'var(--t3)',
      fontWeight: 500,
      marginLeft: 8
    }
  }, "\xB7 ", row.s.asset)), /*#__PURE__*/React.createElement("div", {
    className: "modal-sub"
  }, dir === 'BUY' ? 'Buy on LP to cover short client exposure' : 'Sell on LP to cover long client exposure')))), /*#__PURE__*/React.createElement("div", {
    className: "modal-body"
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: '1fr 1fr',
      gap: 10,
      marginBottom: 18
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "card",
    style: {
      padding: 12,
      borderRadius: 8
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "t3",
    style: {
      fontSize: 10,
      fontWeight: 600,
      letterSpacing: '0.08em'
    }
  }, "CURRENT EXPOSURE"), /*#__PURE__*/React.createElement("div", {
    className: "mono",
    style: {
      fontSize: 18,
      fontWeight: 700,
      marginTop: 4
    }
  }, fmt(row.bbNet - row.covNet)), /*#__PURE__*/React.createElement("div", {
    className: "t3 mono",
    style: {
      fontSize: 11
    }
  }, "Client ", fmt(row.bbNet), " \xB7 Cov ", fmt(row.covNet))), /*#__PURE__*/React.createElement("div", {
    className: "card",
    style: {
      padding: 12,
      borderRadius: 8
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "t3",
    style: {
      fontSize: 10,
      fontWeight: 600,
      letterSpacing: '0.08em'
    }
  }, "HEDGE RATIO"), /*#__PURE__*/React.createElement("div", {
    style: {
      marginTop: 6
    }
  }, /*#__PURE__*/React.createElement(HedgeBar, {
    pct: row.hedge
  })), /*#__PURE__*/React.createElement("div", {
    className: "t3",
    style: {
      fontSize: 11,
      marginTop: 2
    }
  }, "after hedge: ", /*#__PURE__*/React.createElement("span", {
    className: "pos"
  }, "100%")))), /*#__PURE__*/React.createElement("label", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 6,
      marginBottom: 14
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: "t3",
    style: {
      fontSize: 10.5,
      fontWeight: 600,
      letterSpacing: '0.08em'
    }
  }, "VOLUME (LOTS)"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 8,
      alignItems: 'center'
    }
  }, /*#__PURE__*/React.createElement("input", {
    className: "date-input",
    style: {
      flex: 1,
      fontSize: 16,
      padding: '8px 12px',
      fontWeight: 600
    },
    value: vol,
    onChange: e => setVol(e.target.value)
  }), /*#__PURE__*/React.createElement("div", {
    className: "segmented",
    style: {
      padding: 3
    }
  }, [25, 50, 75, 100].map(p => /*#__PURE__*/React.createElement("button", {
    key: p,
    className: partial === p ? 'active' : '',
    onClick: () => {
      setPartial(p);
      setVol((amount * p / 100).toFixed(2));
    }
  }, p, "%")))), /*#__PURE__*/React.createElement("div", {
    className: "t3",
    style: {
      fontSize: 11
    }
  }, "Market: ", /*#__PURE__*/React.createElement("span", {
    className: "mono t1"
  }, price), " \xB7 Est. notional ", /*#__PURE__*/React.createElement("span", {
    className: "mono t1"
  }, "$", fmt(volN * price * (row.s.cls === 'fx' ? 100000 : 1))))), /*#__PURE__*/React.createElement("label", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 6,
      marginBottom: 14
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: "t3",
    style: {
      fontSize: 10.5,
      fontWeight: 600,
      letterSpacing: '0.08em'
    }
  }, "LP ACCOUNT"), /*#__PURE__*/React.createElement("select", {
    className: "select",
    style: {
      padding: '8px 26px 8px 12px',
      fontSize: 13
    },
    value: lpAcct,
    onChange: e => setLpAcct(e.target.value)
  }, /*#__PURE__*/React.createElement("option", null, "96900 \xB7 fXGROW LP"), /*#__PURE__*/React.createElement("option", null, "96901 \xB7 Centroid LP"), /*#__PURE__*/React.createElement("option", null, "96902 \xB7 Equiti Prime"))), /*#__PURE__*/React.createElement("div", {
    className: "card",
    style: {
      padding: 12,
      borderRadius: 8,
      marginBottom: 14
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'space-between',
      marginBottom: 6
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: "t3",
    style: {
      fontSize: 10.5,
      fontWeight: 600,
      letterSpacing: '0.08em'
    }
  }, "PRE-TRADE CHECK"), /*#__PURE__*/React.createElement("span", {
    className: "chip live"
  }, /*#__PURE__*/React.createElement("span", {
    className: "dot"
  }), "All clear")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 14,
      fontSize: 11.5
    }
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("span", {
    className: "t3"
  }, "Margin"), " ", /*#__PURE__*/React.createElement("span", {
    className: "mono t1"
  }, "$48,210")), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("span", {
    className: "t3"
  }, "Free"), " ", /*#__PURE__*/React.createElement("span", {
    className: "mono pos"
  }, "$412,790")), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("span", {
    className: "t3"
  }, "Slippage"), " ", /*#__PURE__*/React.createElement("span", {
    className: "mono t1"
  }, "0.2 pips")))), /*#__PURE__*/React.createElement("label", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 8,
      fontSize: 12,
      color: 'var(--t2)',
      cursor: 'pointer'
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: `switch ${confirm ? 'on' : ''}`,
    onClick: () => setConfirm(!confirm)
  }), "Require confirmation before firing (recommended)")), /*#__PURE__*/React.createElement("div", {
    className: "modal-footer"
  }, /*#__PURE__*/React.createElement("button", {
    className: "icon-btn",
    onClick: onClose
  }, "Cancel"), /*#__PURE__*/React.createElement("button", {
    className: `icon-btn ${dir === 'BUY' ? 'primary' : 'danger'}`,
    onClick: () => onConfirm({
      row,
      dir,
      vol: volN,
      lpAcct
    })
  }, /*#__PURE__*/React.createElement(I.bolt, null), " ", dir, " ", volN.toFixed(2), " lots"))));
}
window.HedgeModal = HedgeModal;

// ========== Hedge Confirm Step (brief) ==========
function HedgeConfirmToast({
  text,
  onDone
}) {
  React.useEffect(() => {
    const t = setTimeout(onDone, 3200);
    return () => clearTimeout(t);
  }, []);
  return /*#__PURE__*/React.createElement("div", {
    className: "toast info",
    style: {
      maxWidth: 360
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "toast-icon",
    style: {
      background: 'var(--green-dim)',
      color: 'var(--green)'
    }
  }, "\u2713"), /*#__PURE__*/React.createElement("div", {
    className: "toast-body"
  }, /*#__PURE__*/React.createElement("div", {
    className: "toast-title"
  }, "Hedge fired"), /*#__PURE__*/React.createElement("div", {
    className: "toast-desc"
  }, text), /*#__PURE__*/React.createElement("div", {
    className: "toast-meta"
  }, "Bridge ID b-8402 \xB7 t+180ms")));
}
window.HedgeConfirmToast = HedgeConfirmToast;

// ===== Tabs.jsx =====

// Secondary tabs for the Coverage Manager shell.

// ========== Stat Card with sparkline ==========
function Sparkline({
  points,
  color,
  width = 80,
  height = 22
}) {
  const min = Math.min(...points),
    max = Math.max(...points);
  const span = max - min || 1;
  const path = points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${i / (points.length - 1) * width} ${height - (p - min) / span * height}`).join(' ');
  return /*#__PURE__*/React.createElement("svg", {
    className: "spark",
    width: width,
    height: height
  }, /*#__PURE__*/React.createElement("path", {
    d: path,
    fill: "none",
    stroke: color,
    strokeWidth: 1.4,
    strokeLinecap: "round"
  }));
}
function StatCard({
  label,
  value,
  delta,
  sparkColor,
  sparkPoints,
  accent
}) {
  return /*#__PURE__*/React.createElement("div", {
    className: "stat-card"
  }, /*#__PURE__*/React.createElement("div", {
    className: "lbl"
  }, label), /*#__PURE__*/React.createElement("div", {
    className: "val",
    style: {
      color: accent || 'var(--t1)'
    }
  }, value), delta !== undefined && /*#__PURE__*/React.createElement("div", {
    className: `delta ${pc(delta)}`
  }, delta >= 0 ? '+' : '', fmt(delta), " ", /*#__PURE__*/React.createElement("span", {
    className: "t3"
  }, "vs prev")), sparkPoints && /*#__PURE__*/React.createElement(Sparkline, {
    points: sparkPoints,
    color: sparkColor || 'var(--t3)'
  }));
}

// ========== Positions Tab ==========
function PositionsPanel() {
  const [filter, setFilter] = useState('all');
  const POS = window.POSITIONS;
  const rows = POS.filter(p => filter === 'all' || p.source === filter);
  return /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("div", {
    className: "toolbar"
  }, /*#__PURE__*/React.createElement("div", {
    className: "segmented"
  }, /*#__PURE__*/React.createElement("button", {
    className: filter === 'all' ? 'active' : '',
    onClick: () => setFilter('all')
  }, "All (", POS.length, ")"), /*#__PURE__*/React.createElement("button", {
    className: filter === 'bbook' ? 'active' : '',
    onClick: () => setFilter('bbook')
  }, "Clients (", POS.filter(p => p.source === 'bbook').length, ")"), /*#__PURE__*/React.createElement("button", {
    className: filter === 'coverage' ? 'active' : '',
    onClick: () => setFilter('coverage')
  }, "Coverage (", POS.filter(p => p.source === 'coverage').length, ")")), /*#__PURE__*/React.createElement("div", {
    className: "sep"
  }), /*#__PURE__*/React.createElement("div", {
    className: "search-wrap"
  }, /*#__PURE__*/React.createElement(I.search, null), /*#__PURE__*/React.createElement("input", {
    className: "search",
    placeholder: "Filter by symbol or login"
  })), /*#__PURE__*/React.createElement("div", {
    className: "spacer"
  }), /*#__PURE__*/React.createElement("button", {
    className: "icon-btn"
  }, /*#__PURE__*/React.createElement(I.download, null), " Export")), /*#__PURE__*/React.createElement("div", {
    className: "exposure"
  }, /*#__PURE__*/React.createElement("table", null, /*#__PURE__*/React.createElement("thead", null, /*#__PURE__*/React.createElement("tr", null, /*#__PURE__*/React.createElement("th", {
    style: {
      textAlign: 'left',
      paddingLeft: 16
    }
  }, "Login"), /*#__PURE__*/React.createElement("th", {
    style: {
      textAlign: 'left'
    }
  }, "Source"), /*#__PURE__*/React.createElement("th", {
    style: {
      textAlign: 'left'
    }
  }, "Symbol"), /*#__PURE__*/React.createElement("th", null, "Direction"), /*#__PURE__*/React.createElement("th", null, "Volume"), /*#__PURE__*/React.createElement("th", null, "Open Price"), /*#__PURE__*/React.createElement("th", null, "Current"), /*#__PURE__*/React.createElement("th", null, "P&L"), /*#__PURE__*/React.createElement("th", {
    style: {
      textAlign: 'right',
      paddingRight: 16
    }
  }, "Open Time"))), /*#__PURE__*/React.createElement("tbody", null, rows.map((p, i) => /*#__PURE__*/React.createElement("tr", {
    key: i,
    className: "open-row",
    style: {
      borderBottom: '1px solid var(--divider)'
    }
  }, /*#__PURE__*/React.createElement("td", {
    className: "mono",
    style: {
      textAlign: 'left',
      paddingLeft: 16
    }
  }, p.login), /*#__PURE__*/React.createElement("td", {
    style: {
      textAlign: 'left'
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: `tag ${p.source === 'bbook' ? 'cli' : 'cov'}`
  }, p.source === 'bbook' ? 'CLIENT' : 'COVERAGE')), /*#__PURE__*/React.createElement("td", {
    className: "mono",
    style: {
      textAlign: 'left'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      color: 'var(--t1)',
      fontWeight: 600
    }
  }, p.sym), /*#__PURE__*/React.createElement("span", {
    className: "t3",
    style: {
      marginLeft: 8,
      fontSize: 10.5
    }
  }, p.canonical)), /*#__PURE__*/React.createElement("td", null, /*#__PURE__*/React.createElement("span", {
    className: `tag ${p.dir.toLowerCase()}`
  }, p.dir)), /*#__PURE__*/React.createElement("td", {
    className: "mono"
  }, fmt(p.vol)), /*#__PURE__*/React.createElement("td", {
    className: "mono t2"
  }, p.openPrice), /*#__PURE__*/React.createElement("td", {
    className: "mono"
  }, p.currentPrice), /*#__PURE__*/React.createElement("td", {
    className: `mono ${pc(p.pnl)}`,
    style: {
      fontWeight: 600
    }
  }, fp(p.pnl)), /*#__PURE__*/React.createElement("td", {
    className: "mono t3",
    style: {
      textAlign: 'right',
      paddingRight: 16
    }
  }, p.openTime)))))));
}
window.PositionsPanel = PositionsPanel;

// ========== P&L Tab ==========
function PnLPanel() {
  const [showCov, setShowCov] = useState(true);
  const rows = window.SYMBOLS.map(s => {
    const c = window.CLOSED_PNL[s.sym];
    const e = window.EXPOSURE[s.sym];
    return {
      sym: s.sym,
      asset: s.asset,
      cls: s.cls,
      vol: (Math.abs(e.bbBuy) + Math.abs(e.bbSell)) * 0.4,
      bbPnL: c.bb,
      covPnL: c.cov,
      combined: c.bb + c.cov
    };
  });
  const tot = rows.reduce((a, r) => ({
    vol: a.vol + r.vol,
    bb: a.bb + r.bbPnL,
    cov: a.cov + r.covPnL,
    comb: a.comb + r.combined
  }), {
    vol: 0,
    bb: 0,
    cov: 0,
    comb: 0
  });
  return /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("div", {
    className: "toolbar"
  }, /*#__PURE__*/React.createElement("span", {
    className: "t3",
    style: {
      fontSize: 11,
      fontWeight: 600,
      letterSpacing: '0.06em'
    }
  }, "FROM"), /*#__PURE__*/React.createElement("input", {
    type: "date",
    className: "date-input",
    defaultValue: "2026-04-18"
  }), /*#__PURE__*/React.createElement("span", {
    className: "t3",
    style: {
      fontSize: 11,
      fontWeight: 600,
      letterSpacing: '0.06em'
    }
  }, "TO"), /*#__PURE__*/React.createElement("input", {
    type: "date",
    className: "date-input",
    defaultValue: "2026-04-18"
  }), /*#__PURE__*/React.createElement("button", {
    className: "icon-btn primary"
  }, "Load"), /*#__PURE__*/React.createElement("div", {
    className: "sep"
  }), /*#__PURE__*/React.createElement("label", {
    style: {
      display: 'flex',
      gap: 6,
      alignItems: 'center',
      fontSize: 12,
      color: 'var(--t2)',
      fontWeight: 500
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: `switch ${showCov ? 'on' : ''}`,
    onClick: () => setShowCov(!showCov)
  }), "Coverage"), /*#__PURE__*/React.createElement("div", {
    className: "spacer"
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 14
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "t3",
    style: {
      fontSize: 11
    }
  }, rows.reduce((a, r) => a + r.vol, 0).toFixed(0), " deals \xB7 ", rows.length, " symbols"), /*#__PURE__*/React.createElement("div", {
    className: "mono",
    style: {
      fontSize: 20,
      fontWeight: 700,
      color: tot.bb >= 0 ? 'var(--green)' : 'var(--red)'
    }
  }, fp(tot.bb)))), /*#__PURE__*/React.createElement("div", {
    className: "exposure"
  }, /*#__PURE__*/React.createElement("table", null, /*#__PURE__*/React.createElement("thead", null, /*#__PURE__*/React.createElement("tr", null, /*#__PURE__*/React.createElement("th", {
    style: {
      textAlign: 'left',
      paddingLeft: 16
    }
  }, "Symbol"), /*#__PURE__*/React.createElement("th", {
    style: {
      textAlign: 'right'
    }
  }, "Volume"), /*#__PURE__*/React.createElement("th", {
    style: {
      textAlign: 'right'
    }
  }, "Profit"), /*#__PURE__*/React.createElement("th", {
    style: {
      textAlign: 'right'
    }
  }, "Commission"), /*#__PURE__*/React.createElement("th", {
    style: {
      textAlign: 'right'
    }
  }, "Swap"), /*#__PURE__*/React.createElement("th", {
    style: {
      textAlign: 'right'
    }
  }, "Net P&L"), showCov && /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("th", {
    className: "sec-l",
    style: {
      textAlign: 'right'
    }
  }, "Cov P&L"), /*#__PURE__*/React.createElement("th", {
    style: {
      textAlign: 'right',
      paddingRight: 16
    }
  }, "Combined")))), /*#__PURE__*/React.createElement("tbody", null, rows.map(r => /*#__PURE__*/React.createElement("tr", {
    key: r.sym,
    className: "open-row",
    style: {
      borderBottom: '1px solid var(--divider)'
    }
  }, /*#__PURE__*/React.createElement("td", {
    className: "sym-cell",
    style: {
      paddingLeft: 16,
      padding: '8px 16px',
      textAlign: 'left'
    }
  }, /*#__PURE__*/React.createElement(AssetMark, {
    sym: r.sym,
    cls: r.cls
  }), /*#__PURE__*/React.createElement("span", {
    style: {
      fontFamily: 'var(--font-ui)',
      fontWeight: 600
    }
  }, r.sym), /*#__PURE__*/React.createElement("span", {
    className: "t3",
    style: {
      marginLeft: 8,
      fontSize: 10.5,
      fontFamily: 'var(--font-ui)'
    }
  }, r.asset)), /*#__PURE__*/React.createElement("td", {
    className: "mono t2",
    style: {
      textAlign: 'right'
    }
  }, fmt(r.vol)), /*#__PURE__*/React.createElement("td", {
    className: `mono ${pc(r.bbPnL * 1.2)}`,
    style: {
      textAlign: 'right'
    }
  }, fp(r.bbPnL * 1.2)), /*#__PURE__*/React.createElement("td", {
    className: "mono neg",
    style: {
      textAlign: 'right'
    }
  }, fp(-Math.abs(r.bbPnL) * 0.05 - 12)), /*#__PURE__*/React.createElement("td", {
    className: "mono t2",
    style: {
      textAlign: 'right'
    }
  }, fp((Math.random() - 0.5) * 20)), /*#__PURE__*/React.createElement("td", {
    className: `mono ${pc(r.bbPnL)}`,
    style: {
      textAlign: 'right',
      fontWeight: 700
    }
  }, fp(r.bbPnL)), showCov && /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("td", {
    className: `mono sec-l ${pc(r.covPnL)}`,
    style: {
      textAlign: 'right'
    }
  }, fp(r.covPnL)), /*#__PURE__*/React.createElement("td", {
    className: `mono ${pc(r.combined)}`,
    style: {
      textAlign: 'right',
      paddingRight: 16,
      fontWeight: 600
    }
  }, fp(r.combined)))))), /*#__PURE__*/React.createElement("tfoot", null, /*#__PURE__*/React.createElement("tr", {
    className: "total-row"
  }, /*#__PURE__*/React.createElement("td", {
    style: {
      textAlign: 'left',
      paddingLeft: 16,
      fontFamily: 'var(--font-ui)'
    }
  }, "TOTAL"), /*#__PURE__*/React.createElement("td", {
    className: "mono t2",
    style: {
      textAlign: 'right'
    }
  }, fmt(tot.vol)), /*#__PURE__*/React.createElement("td", {
    className: "mono t2",
    style: {
      textAlign: 'right'
    }
  }, fp(tot.bb * 1.2)), /*#__PURE__*/React.createElement("td", {
    className: "mono neg",
    style: {
      textAlign: 'right'
    }
  }, fp(-120)), /*#__PURE__*/React.createElement("td", {
    className: "mono t2",
    style: {
      textAlign: 'right'
    }
  }, fp(24)), /*#__PURE__*/React.createElement("td", {
    className: `mono ${pc(tot.bb)}`,
    style: {
      textAlign: 'right',
      fontWeight: 700,
      fontSize: 13
    }
  }, fp(tot.bb)), showCov && /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("td", {
    className: `mono sec-l ${pc(tot.cov)}`,
    style: {
      textAlign: 'right'
    }
  }, fp(tot.cov)), /*#__PURE__*/React.createElement("td", {
    className: `mono ${pc(tot.comb)}`,
    style: {
      textAlign: 'right',
      paddingRight: 16,
      fontWeight: 700
    }
  }, fp(tot.comb))))))));
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
    return {
      sym: s.sym,
      asset: s.asset,
      cls: s.cls,
      begin: beginFloat,
      curr: currFloat,
      delta,
      settled: c.bb,
      net,
      covBegin: e.covPnL * 0.5,
      covCurr: e.covPnL,
      covSettled: c.cov,
      covNet: e.covPnL * 0.5 + c.cov,
      edgeNet: e.covPnL * 0.5 + c.cov - net
    };
  });
  const tot = rows.reduce((a, r) => ({
    begin: a.begin + r.begin,
    curr: a.curr + r.curr,
    delta: a.delta + r.delta,
    settled: a.settled + r.settled,
    net: a.net + r.net,
    covNet: a.covNet + r.covNet,
    edgeNet: a.edgeNet + r.edgeNet
  }), {
    begin: 0,
    curr: 0,
    delta: 0,
    settled: 0,
    net: 0,
    covNet: 0,
    edgeNet: 0
  });
  return /*#__PURE__*/React.createElement("div", {
    className: "panel-wrap"
  }, /*#__PURE__*/React.createElement("div", {
    className: "row-flex between",
    style: {
      gap: 16
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "row-flex",
    style: {
      gap: 10
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: "t3",
    style: {
      fontSize: 11,
      fontWeight: 600,
      letterSpacing: '0.06em'
    }
  }, "PERIOD"), /*#__PURE__*/React.createElement("input", {
    type: "date",
    className: "date-input",
    defaultValue: "2026-04-18"
  }), /*#__PURE__*/React.createElement("span", {
    className: "t3"
  }, "\u2192"), /*#__PURE__*/React.createElement("input", {
    type: "date",
    className: "date-input",
    defaultValue: "2026-04-18"
  }), /*#__PURE__*/React.createElement("span", {
    className: "chip",
    style: {
      marginLeft: 6
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: "dot"
  }), " Asia/Beirut")), /*#__PURE__*/React.createElement("div", {
    className: "row-flex",
    style: {
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("button", {
    className: "icon-btn"
  }, /*#__PURE__*/React.createElement(I.camera, null), " Capture snapshot"), /*#__PURE__*/React.createElement("button", {
    className: "icon-btn"
  }, /*#__PURE__*/React.createElement(I.refresh, null)))), /*#__PURE__*/React.createElement("div", {
    className: "panel-grid"
  }, /*#__PURE__*/React.createElement(StatCard, {
    label: "Edge Net P&L",
    value: fp(tot.edgeNet),
    accent: "var(--teal)",
    sparkColor: "var(--teal)",
    sparkPoints: [3, 5, 4, 7, 9, 6, 8, 12, 14, 11, 13, 16]
  }), /*#__PURE__*/React.createElement(StatCard, {
    label: "Broker Net",
    value: fp(tot.net),
    delta: tot.delta,
    sparkColor: "var(--blue)",
    sparkPoints: [8, 7, 9, 6, 10, 12, 14, 11, 13, 10, 12, 15]
  }), /*#__PURE__*/React.createElement(StatCard, {
    label: "Floating \u0394",
    value: fp(tot.delta),
    sparkColor: tot.delta >= 0 ? 'var(--green)' : 'var(--red)',
    sparkPoints: [4, 6, 5, 7, 9, 12, 10, 13, 15, 12, 14, 17]
  }), /*#__PURE__*/React.createElement(StatCard, {
    label: "Settled",
    value: fp(tot.settled),
    sparkColor: "var(--amber)",
    sparkPoints: [2, 4, 3, 5, 7, 9, 11, 10, 13, 12, 15, 18]
  })), /*#__PURE__*/React.createElement("div", {
    className: "card"
  }, /*#__PURE__*/React.createElement("div", {
    className: "card-header"
  }, /*#__PURE__*/React.createElement("div", {
    className: "card-title"
  }, "Period P&L breakdown"), /*#__PURE__*/React.createElement("div", {
    className: "spacer"
  }), /*#__PURE__*/React.createElement("span", {
    className: "chip"
  }, /*#__PURE__*/React.createElement(I.info, null), " Floating \u0394 + Settled = Net")), /*#__PURE__*/React.createElement("div", {
    className: "exposure",
    style: {
      maxHeight: 'none'
    }
  }, /*#__PURE__*/React.createElement("table", null, /*#__PURE__*/React.createElement("thead", null, /*#__PURE__*/React.createElement("tr", null, /*#__PURE__*/React.createElement("th", {
    style: {
      textAlign: 'left',
      paddingLeft: 16
    },
    rowSpan: 2
  }, "Symbol"), /*#__PURE__*/React.createElement("th", {
    className: "group clients sec-l",
    colSpan: 5
  }, "B-BOOK (CLIENT PERSPECTIVE, INVERTED)"), /*#__PURE__*/React.createElement("th", {
    className: "group coverage sec-l",
    colSpan: 3
  }, "COVERAGE"), /*#__PURE__*/React.createElement("th", {
    className: "group summary sec-l"
  }, "EDGE")), /*#__PURE__*/React.createElement("tr", {
    className: "sub"
  }, /*#__PURE__*/React.createElement("th", {
    className: "sec-l"
  }, "Begin Float"), /*#__PURE__*/React.createElement("th", null, "Current"), /*#__PURE__*/React.createElement("th", null, "\u0394 Float"), /*#__PURE__*/React.createElement("th", null, "Settled"), /*#__PURE__*/React.createElement("th", null, "Net"), /*#__PURE__*/React.createElement("th", {
    className: "sec-l"
  }, "Current"), /*#__PURE__*/React.createElement("th", null, "Settled"), /*#__PURE__*/React.createElement("th", null, "Net"), /*#__PURE__*/React.createElement("th", {
    className: "sec-l"
  }, "Net"))), /*#__PURE__*/React.createElement("tbody", null, rows.map(r => /*#__PURE__*/React.createElement("tr", {
    key: r.sym,
    className: "open-row"
  }, /*#__PURE__*/React.createElement("td", {
    className: "sym-cell",
    style: {
      paddingLeft: 16
    }
  }, /*#__PURE__*/React.createElement(AssetMark, {
    sym: r.sym,
    cls: r.cls
  }), /*#__PURE__*/React.createElement("span", {
    style: {
      fontFamily: 'var(--font-ui)',
      fontWeight: 600
    }
  }, r.sym)), /*#__PURE__*/React.createElement("td", {
    className: "mono t2 sec-l"
  }, fp(r.begin)), /*#__PURE__*/React.createElement("td", {
    className: `mono ${pc(r.curr)}`
  }, fp(r.curr)), /*#__PURE__*/React.createElement("td", {
    className: `mono ${pc(r.delta)}`
  }, fp(r.delta)), /*#__PURE__*/React.createElement("td", {
    className: `mono ${pc(r.settled)}`
  }, fp(r.settled)), /*#__PURE__*/React.createElement("td", {
    className: `mono ${pc(r.net)}`,
    style: {
      fontWeight: 700
    }
  }, fp(r.net)), /*#__PURE__*/React.createElement("td", {
    className: `mono sec-l ${pc(r.covCurr)}`
  }, fp(r.covCurr)), /*#__PURE__*/React.createElement("td", {
    className: `mono ${pc(r.covSettled)}`
  }, fp(r.covSettled)), /*#__PURE__*/React.createElement("td", {
    className: `mono ${pc(r.covNet)}`,
    style: {
      fontWeight: 700
    }
  }, fp(r.covNet)), /*#__PURE__*/React.createElement("td", {
    className: `mono sec-l ${pc(r.edgeNet)}`,
    style: {
      fontWeight: 700
    }
  }, fp(r.edgeNet))))), /*#__PURE__*/React.createElement("tfoot", null, /*#__PURE__*/React.createElement("tr", {
    className: "total-row"
  }, /*#__PURE__*/React.createElement("td", {
    style: {
      paddingLeft: 16,
      textAlign: 'left',
      fontFamily: 'var(--font-ui)'
    }
  }, "TOTAL"), /*#__PURE__*/React.createElement("td", {
    className: "mono t2 sec-l"
  }, fp(tot.begin)), /*#__PURE__*/React.createElement("td", {
    className: `mono ${pc(tot.curr)}`
  }, fp(tot.curr)), /*#__PURE__*/React.createElement("td", {
    className: `mono ${pc(tot.delta)}`
  }, fp(tot.delta)), /*#__PURE__*/React.createElement("td", {
    className: `mono ${pc(tot.settled)}`
  }, fp(tot.settled)), /*#__PURE__*/React.createElement("td", {
    className: `mono ${pc(tot.net)}`
  }, fp(tot.net)), /*#__PURE__*/React.createElement("td", {
    className: "mono sec-l"
  }), /*#__PURE__*/React.createElement("td", {
    className: "mono"
  }), /*#__PURE__*/React.createElement("td", {
    className: `mono ${pc(tot.covNet)}`
  }, fp(tot.covNet)), /*#__PURE__*/React.createElement("td", {
    className: `mono sec-l ${pc(tot.edgeNet)}`
  }, fp(tot.edgeNet))))))));
}
window.NetPnLPanel = NetPnLPanel;

// ========== Compare tab ==========
function ComparePanel() {
  const [sel, setSel] = useState('XAUUSD');
  const rows = window.SYMBOLS.map(s => {
    const e = window.EXPOSURE[s.sym];
    const bbNet = e.bbBuy - e.bbSell;
    const covNet = e.covBuy - e.covSell;
    const hedge = Math.abs(bbNet) > 0 ? Math.min(999, Math.abs(covNet) / Math.abs(bbNet) * 100) : 0;
    return {
      s,
      bbNet,
      covNet,
      delta: bbNet - covNet,
      pnlDelta: e.covPnL - e.bbPnL,
      hedge
    };
  });
  const selRow = rows.find(r => r.s.sym === sel) || rows[0];
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: '380px 1fr',
      flex: 1,
      overflow: 'hidden'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      borderRight: '1px solid var(--border)',
      background: 'var(--bg-2)',
      overflow: 'auto'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '10px 14px',
      borderBottom: '1px solid var(--divider)',
      display: 'flex',
      alignItems: 'center',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "t1",
    style: {
      fontWeight: 600,
      fontSize: 13
    }
  }, "Symbols"), /*#__PURE__*/React.createElement("div", {
    className: "spacer"
  }), /*#__PURE__*/React.createElement("span", {
    className: "chip live"
  }, /*#__PURE__*/React.createElement("span", {
    className: "dot"
  }), "Live")), rows.map(r => /*#__PURE__*/React.createElement("div", {
    key: r.s.sym,
    onClick: () => setSel(r.s.sym),
    style: {
      padding: '10px 14px',
      borderBottom: '1px solid var(--divider)',
      cursor: 'pointer',
      background: sel === r.s.sym ? 'var(--accent-dim)' : 'transparent'
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "row-flex",
    style: {
      gap: 10
    }
  }, /*#__PURE__*/React.createElement(AssetMark, {
    sym: r.s.sym,
    cls: r.s.cls
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 600,
      fontSize: 13
    }
  }, r.s.sym)), /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: 'right'
    }
  }, /*#__PURE__*/React.createElement(HedgeBar, {
    pct: r.hedge
  }), /*#__PURE__*/React.createElement("div", {
    className: `mono ${pc(r.pnlDelta)}`,
    style: {
      fontSize: 11,
      fontWeight: 600,
      marginTop: 2
    }
  }, fp(r.pnlDelta)))), /*#__PURE__*/React.createElement("div", {
    className: "mono",
    style: {
      marginTop: 6,
      fontSize: 10.5,
      display: 'flex',
      gap: 10,
      color: 'var(--t3)'
    }
  }, /*#__PURE__*/React.createElement("span", null, "CLI ", /*#__PURE__*/React.createElement("span", {
    className: "t2"
  }, fmt(r.bbNet))), /*#__PURE__*/React.createElement("span", null, "COV ", /*#__PURE__*/React.createElement("span", {
    className: "t2"
  }, fmt(r.covNet))), /*#__PURE__*/React.createElement("span", null, "\u0394 ", /*#__PURE__*/React.createElement("span", {
    className: pc(-r.delta)
  }, fp(-r.delta))))))), /*#__PURE__*/React.createElement("div", {
    className: "panel-wrap"
  }, /*#__PURE__*/React.createElement("div", {
    className: "row-flex",
    style: {
      gap: 12
    }
  }, /*#__PURE__*/React.createElement(AssetMark, {
    sym: selRow.s.sym,
    cls: selRow.s.cls
  }), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 18,
      fontWeight: 700,
      letterSpacing: '-0.01em'
    }
  }, selRow.s.sym), /*#__PURE__*/React.createElement("div", {
    className: "t3",
    style: {
      fontSize: 12
    }
  }, selRow.s.asset, " \xB7 ", selRow.s.cls.toUpperCase())), /*#__PURE__*/React.createElement("div", {
    className: "spacer"
  }), /*#__PURE__*/React.createElement("span", {
    className: "chip",
    style: {
      background: 'var(--blue-dim)',
      color: 'var(--blue)',
      fontSize: 11
    }
  }, "CLI ", /*#__PURE__*/React.createElement("span", {
    className: "mono"
  }, fmt(selRow.bbNet))), /*#__PURE__*/React.createElement("span", {
    className: "chip",
    style: {
      background: 'var(--teal-dim)',
      color: 'var(--teal)',
      fontSize: 11
    }
  }, "COV ", /*#__PURE__*/React.createElement("span", {
    className: "mono"
  }, fmt(selRow.covNet))), /*#__PURE__*/React.createElement("span", {
    className: "chip"
  }, /*#__PURE__*/React.createElement(HedgeBar, {
    pct: selRow.hedge
  }))), /*#__PURE__*/React.createElement("div", {
    className: "panel-grid"
  }, /*#__PURE__*/React.createElement(StatCard, {
    label: "Avg Entry (CLI)",
    value: window.SYMBOLS.find(s => s.sym === sel).bid.toFixed(2),
    sparkColor: "var(--blue)",
    sparkPoints: [10, 12, 14, 11, 13, 15, 14, 16, 15, 17, 19, 18]
  }), /*#__PURE__*/React.createElement(StatCard, {
    label: "Avg Exit (CLI)",
    value: (window.SYMBOLS.find(s => s.sym === sel).bid + 0.8).toFixed(2),
    sparkColor: "var(--blue)",
    sparkPoints: [14, 13, 15, 16, 14, 17, 18, 17, 19, 18, 20, 21]
  }), /*#__PURE__*/React.createElement(StatCard, {
    label: "Volume (lots)",
    value: fmt(Math.abs(selRow.bbNet)),
    sparkColor: "var(--amber)",
    sparkPoints: [3, 5, 4, 7, 9, 12, 10, 13, 11, 14, 16, 18]
  }), /*#__PURE__*/React.createElement(StatCard, {
    label: "P&L edge",
    value: fp(selRow.pnlDelta),
    accent: selRow.pnlDelta >= 0 ? 'var(--green)' : 'var(--red)',
    sparkColor: selRow.pnlDelta >= 0 ? 'var(--green)' : 'var(--red)',
    sparkPoints: [2, 4, 3, 5, 8, 6, 10, 13, 11, 14, 17, 20]
  })), /*#__PURE__*/React.createElement("div", {
    className: "card",
    style: {
      padding: 16
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "row-flex between",
    style: {
      marginBottom: 10
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "card-title"
  }, "Price timeline"), /*#__PURE__*/React.createElement("div", {
    className: "row-flex",
    style: {
      gap: 6
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "segmented"
  }, /*#__PURE__*/React.createElement("button", null, "1H"), /*#__PURE__*/React.createElement("button", {
    className: "active"
  }, "4H"), /*#__PURE__*/React.createElement("button", null, "1D"), /*#__PURE__*/React.createElement("button", null, "1W")))), /*#__PURE__*/React.createElement(MockChart, {
    sym: selRow.s.sym
  }))));
}
function MockChart({
  sym
}) {
  const ref = useRef(null);
  useEffect(() => {
    const c = ref.current;
    const W = c.width = c.offsetWidth * 2,
      H = c.height = 320;
    const ctx = c.getContext('2d');
    ctx.scale(2, 2);
    ctx.clearRect(0, 0, W, H);
    const N = 120;
    const base = window.SYMBOLS.find(s => s.sym === sym).bid;
    const pts = [];
    let p = base * 0.995;
    for (let i = 0; i < N; i++) {
      p += (Math.random() - 0.5) * base * 0.003;
      pts.push(p);
    }
    const min = Math.min(...pts) * 0.999,
      max = Math.max(...pts) * 1.001;
    const ww = c.offsetWidth,
      hh = H / 2;

    // grid
    ctx.strokeStyle = 'rgba(255,255,255,0.04)';
    ctx.lineWidth = 1;
    for (let y = 0; y < 5; y++) {
      ctx.beginPath();
      const yy = y / 4 * (hh - 20) + 10;
      ctx.moveTo(0, yy);
      ctx.lineTo(ww, yy);
      ctx.stroke();
    }

    // price line
    const grad = ctx.createLinearGradient(0, 0, 0, hh);
    grad.addColorStop(0, 'rgba(45, 212, 191, 0.18)');
    grad.addColorStop(1, 'rgba(45, 212, 191, 0)');
    ctx.fillStyle = grad;
    ctx.beginPath();
    pts.forEach((v, i) => {
      const x = i / (N - 1) * ww;
      const y = hh - 10 - (v - min) / (max - min) * (hh - 20);
      i === 0 ? ctx.moveTo(x, y) : ctx.lineTo(x, y);
    });
    ctx.lineTo(ww, hh);
    ctx.lineTo(0, hh);
    ctx.closePath();
    ctx.fill();
    ctx.strokeStyle = '#2DD4BF';
    ctx.lineWidth = 1.4;
    ctx.beginPath();
    pts.forEach((v, i) => {
      const x = i / (N - 1) * ww;
      const y = hh - 10 - (v - min) / (max - min) * (hh - 20);
      i === 0 ? ctx.moveTo(x, y) : ctx.lineTo(x, y);
    });
    ctx.stroke();

    // entry markers
    [20, 48, 82].forEach(i => {
      const x = i / (N - 1) * ww;
      const y = hh - 10 - (pts[i] - min) / (max - min) * (hh - 20);
      ctx.fillStyle = '#60A5FA';
      ctx.beginPath();
      ctx.arc(x, y, 4, 0, Math.PI * 2);
      ctx.fill();
      ctx.strokeStyle = 'rgba(96,165,250,0.4)';
      ctx.beginPath();
      ctx.arc(x, y, 7, 0, Math.PI * 2);
      ctx.stroke();
    });
  }, [sym]);
  return /*#__PURE__*/React.createElement("canvas", {
    ref: ref,
    style: {
      width: '100%',
      height: 160
    }
  });
}
window.ComparePanel = ComparePanel;

// ========== Bridge Tab ==========
function BridgePanel() {
  return /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("div", {
    className: "toolbar"
  }, /*#__PURE__*/React.createElement("span", {
    className: "t3",
    style: {
      fontSize: 11,
      fontWeight: 600,
      letterSpacing: '0.06em'
    }
  }, "UTC"), /*#__PURE__*/React.createElement("input", {
    type: "date",
    className: "date-input",
    defaultValue: "2026-04-18"
  }), /*#__PURE__*/React.createElement("span", {
    className: "t3"
  }, "\u2192"), /*#__PURE__*/React.createElement("input", {
    type: "date",
    className: "date-input",
    defaultValue: "2026-04-18"
  }), /*#__PURE__*/React.createElement("div", {
    className: "sep"
  }), /*#__PURE__*/React.createElement("select", {
    className: "select"
  }, /*#__PURE__*/React.createElement("option", null, "All symbols")), /*#__PURE__*/React.createElement("div", {
    className: "segmented"
  }, /*#__PURE__*/React.createElement("button", {
    className: "active"
  }, "All"), /*#__PURE__*/React.createElement("button", null, "Buy"), /*#__PURE__*/React.createElement("button", null, "Sell")), /*#__PURE__*/React.createElement("div", {
    className: "sep"
  }), /*#__PURE__*/React.createElement("label", {
    style: {
      display: 'flex',
      gap: 6,
      alignItems: 'center',
      fontSize: 12,
      color: 'var(--t2)'
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: "switch"
  }), " Anomalies only"), /*#__PURE__*/React.createElement("div", {
    className: "spacer"
  }), /*#__PURE__*/React.createElement("span", {
    className: "chip live"
  }, /*#__PURE__*/React.createElement("span", {
    className: "dot"
  }), "Centroid CS360 \xB7 Live")), /*#__PURE__*/React.createElement("div", {
    className: "exposure"
  }, /*#__PURE__*/React.createElement("table", null, /*#__PURE__*/React.createElement("thead", null, /*#__PURE__*/React.createElement("tr", null, /*#__PURE__*/React.createElement("th", {
    style: {
      textAlign: 'left',
      paddingLeft: 16
    }
  }, "Time (UTC)"), /*#__PURE__*/React.createElement("th", {
    style: {
      textAlign: 'left'
    }
  }, "Symbol"), /*#__PURE__*/React.createElement("th", null, "Side"), /*#__PURE__*/React.createElement("th", null, "Party"), /*#__PURE__*/React.createElement("th", null, "Volume"), /*#__PURE__*/React.createElement("th", null, "Price"), /*#__PURE__*/React.createElement("th", null, "\u0394 ms"), /*#__PURE__*/React.createElement("th", null, "LP"), /*#__PURE__*/React.createElement("th", null, "Price Edge"), /*#__PURE__*/React.createElement("th", {
    style: {
      paddingRight: 16
    }
  }, "Pips"))), /*#__PURE__*/React.createElement("tbody", null, window.BRIDGE_ROWS.map(b => /*#__PURE__*/React.createElement(React.Fragment, {
    key: b.id
  }, /*#__PURE__*/React.createElement("tr", {
    className: "open-row bridge-row client",
    style: {
      borderTop: '1px solid var(--border)'
    }
  }, /*#__PURE__*/React.createElement("td", {
    rowSpan: Math.max(1, b.covFills.length) + 1,
    className: "mono",
    style: {
      textAlign: 'left',
      paddingLeft: 16,
      verticalAlign: 'top',
      paddingTop: 10
    }
  }, b.time), /*#__PURE__*/React.createElement("td", {
    rowSpan: Math.max(1, b.covFills.length) + 1,
    className: "mono",
    style: {
      textAlign: 'left',
      verticalAlign: 'top',
      paddingTop: 10
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontWeight: 600,
      color: 'var(--t1)'
    }
  }, b.sym)), /*#__PURE__*/React.createElement("td", null, /*#__PURE__*/React.createElement("span", {
    className: `tag ${b.side.toLowerCase()}`
  }, b.side)), /*#__PURE__*/React.createElement("td", null, /*#__PURE__*/React.createElement("span", {
    className: "tag cli"
  }, "CLIENT")), /*#__PURE__*/React.createElement("td", {
    className: "mono"
  }, fmt(b.cliVol)), /*#__PURE__*/React.createElement("td", {
    className: "mono"
  }, b.cliPrice), /*#__PURE__*/React.createElement("td", {
    className: "mono t3"
  }, "\u2014"), /*#__PURE__*/React.createElement("td", {
    className: "t3"
  }, "login ", b.cliLogin), /*#__PURE__*/React.createElement("td", {
    rowSpan: Math.max(1, b.covFills.length) + 1,
    className: `mono ${pc(b.edge)}`,
    style: {
      verticalAlign: 'top',
      paddingTop: 10,
      fontWeight: 600
    }
  }, b.edge ? fp(b.edge, 4) : '—'), /*#__PURE__*/React.createElement("td", {
    rowSpan: Math.max(1, b.covFills.length) + 1,
    className: `mono ${pc(b.pips)}`,
    style: {
      verticalAlign: 'top',
      paddingTop: 10,
      paddingRight: 16,
      fontWeight: 700
    }
  }, b.pips ? fp(b.pips, 1) : '—')), b.covFills.length === 0 ? /*#__PURE__*/React.createElement("tr", {
    className: "close-row",
    style: {
      background: 'var(--red-dim)'
    }
  }, /*#__PURE__*/React.createElement("td", null), /*#__PURE__*/React.createElement("td", null, /*#__PURE__*/React.createElement("span", {
    className: "chip red"
  }, "No cov leg")), /*#__PURE__*/React.createElement("td", {
    className: "mono t3"
  }, "\u2014"), /*#__PURE__*/React.createElement("td", {
    className: "mono t3"
  }, "\u2014"), /*#__PURE__*/React.createElement("td", {
    className: "mono t3"
  }, "\u2014"), /*#__PURE__*/React.createElement("td", {
    className: "t3"
  }, "anomaly")) : b.covFills.map((f, i) => /*#__PURE__*/React.createElement("tr", {
    key: i,
    className: "bridge-row cov"
  }, /*#__PURE__*/React.createElement("td", null, /*#__PURE__*/React.createElement("span", {
    className: `tag ${b.side === 'BUY' ? 'sell' : 'buy'}`
  }, b.side === 'BUY' ? 'SELL' : 'BUY')), /*#__PURE__*/React.createElement("td", null, /*#__PURE__*/React.createElement("span", {
    className: "tag cov"
  }, "COV OUT")), /*#__PURE__*/React.createElement("td", {
    className: "mono"
  }, fmt(f.vol)), /*#__PURE__*/React.createElement("td", {
    className: "mono"
  }, f.price), /*#__PURE__*/React.createElement("td", {
    className: `mono ${f.diffMs <= 500 ? 'pos' : f.diffMs <= 2000 ? 'amb' : 'neg'}`
  }, f.diffMs, "ms"), /*#__PURE__*/React.createElement("td", {
    className: "t3"
  }, f.lp)))))))));
}
window.BridgePanel = BridgePanel;

// ========== Mappings Tab ==========
function MappingsPanel() {
  return /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("div", {
    className: "toolbar"
  }, /*#__PURE__*/React.createElement("div", {
    className: "search-wrap"
  }, /*#__PURE__*/React.createElement(I.search, null), /*#__PURE__*/React.createElement("input", {
    className: "search",
    placeholder: "Filter mappings"
  })), /*#__PURE__*/React.createElement("div", {
    className: "sep"
  }), /*#__PURE__*/React.createElement("span", {
    className: "chip blue"
  }, window.MAPPINGS.length, " active"), /*#__PURE__*/React.createElement("div", {
    className: "spacer"
  }), /*#__PURE__*/React.createElement("button", {
    className: "icon-btn"
  }, /*#__PURE__*/React.createElement(I.plus, null), " Add mapping")), /*#__PURE__*/React.createElement("div", {
    className: "exposure"
  }, /*#__PURE__*/React.createElement("table", null, /*#__PURE__*/React.createElement("thead", null, /*#__PURE__*/React.createElement("tr", null, /*#__PURE__*/React.createElement("th", {
    style: {
      textAlign: 'left',
      paddingLeft: 16
    }
  }, "Canonical"), /*#__PURE__*/React.createElement("th", {
    style: {
      textAlign: 'left'
    }
  }, "B-Book symbol"), /*#__PURE__*/React.createElement("th", null, "Contract"), /*#__PURE__*/React.createElement("th", {
    style: {
      textAlign: 'left'
    }
  }, "Coverage symbol"), /*#__PURE__*/React.createElement("th", null, "Contract"), /*#__PURE__*/React.createElement("th", null, "Digits"), /*#__PURE__*/React.createElement("th", null, "Currency"), /*#__PURE__*/React.createElement("th", {
    style: {
      paddingRight: 16
    }
  }, "Active"))), /*#__PURE__*/React.createElement("tbody", null, window.MAPPINGS.map(m => {
    const s = window.SYMBOLS.find(x => x.sym === m.canonical);
    return /*#__PURE__*/React.createElement("tr", {
      key: m.id,
      className: "open-row",
      style: {
        borderBottom: '1px solid var(--divider)'
      }
    }, /*#__PURE__*/React.createElement("td", {
      className: "sym-cell",
      style: {
        paddingLeft: 16
      }
    }, s && /*#__PURE__*/React.createElement(AssetMark, {
      sym: m.canonical,
      cls: s.cls
    }), /*#__PURE__*/React.createElement("span", {
      style: {
        fontFamily: 'var(--font-ui)',
        fontWeight: 600
      }
    }, m.canonical)), /*#__PURE__*/React.createElement("td", {
      className: "mono",
      style: {
        textAlign: 'left'
      }
    }, m.bbook), /*#__PURE__*/React.createElement("td", {
      className: "mono t2"
    }, fmt(m.bbSize, 0)), /*#__PURE__*/React.createElement("td", {
      className: "mono",
      style: {
        textAlign: 'left'
      }
    }, m.covSym), /*#__PURE__*/React.createElement("td", {
      className: "mono t2"
    }, fmt(m.covSize, 0)), /*#__PURE__*/React.createElement("td", {
      className: "mono t2"
    }, m.digits), /*#__PURE__*/React.createElement("td", {
      className: "t2"
    }, m.ccy), /*#__PURE__*/React.createElement("td", {
      style: {
        paddingRight: 16
      }
    }, /*#__PURE__*/React.createElement("span", {
      className: `switch on`
    })));
  })))));
}
window.MappingsPanel = MappingsPanel;

// ========== Alerts Panel ==========
function AlertsPanel({
  alerts,
  ackAlert,
  setAlerts
}) {
  const [tab, setTab] = useState('rules');
  return /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("div", {
    className: "toolbar"
  }, /*#__PURE__*/React.createElement("div", {
    className: "segmented"
  }, /*#__PURE__*/React.createElement("button", {
    className: tab === 'rules' ? 'active' : '',
    onClick: () => setTab('rules')
  }, "Rules (", window.ALERT_RULES.length, ")"), /*#__PURE__*/React.createElement("button", {
    className: tab === 'history' ? 'active' : '',
    onClick: () => setTab('history')
  }, "History (", alerts.length, ")"), /*#__PURE__*/React.createElement("button", {
    className: tab === 'channels' ? 'active' : '',
    onClick: () => setTab('channels')
  }, "Channels")), /*#__PURE__*/React.createElement("div", {
    className: "spacer"
  }), tab === 'rules' && /*#__PURE__*/React.createElement("button", {
    className: "icon-btn primary"
  }, /*#__PURE__*/React.createElement(I.plus, null), " New rule"), tab === 'history' && /*#__PURE__*/React.createElement("button", {
    className: "icon-btn",
    onClick: () => setAlerts(alerts.map(a => ({
      ...a,
      ack: true
    })))
  }, "Acknowledge all")), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      overflow: 'auto',
      padding: 16
    }
  }, tab === 'rules' && /*#__PURE__*/React.createElement(RulesList, null), tab === 'history' && /*#__PURE__*/React.createElement(AlertHistory, {
    alerts: alerts,
    ackAlert: ackAlert
  }), tab === 'channels' && /*#__PURE__*/React.createElement(ChannelsPanel, null)));
}
function RulesList() {
  return /*#__PURE__*/React.createElement("div", {
    className: "card"
  }, window.ALERT_RULES.map(r => {
    const desc = {
      hedge_ratio: /*#__PURE__*/React.createElement(React.Fragment, null, "hedge ratio ", /*#__PURE__*/React.createElement("b", null, r.op, " ", r.val, r.unit)),
      exposure: /*#__PURE__*/React.createElement(React.Fragment, null, "net exposure ", /*#__PURE__*/React.createElement("b", null, r.op, " ", r.val, " ", r.unit)),
      net_pnl: /*#__PURE__*/React.createElement(React.Fragment, null, "net P&L ", /*#__PURE__*/React.createElement("b", null, r.op, " ", fp(r.val))),
      news_event: /*#__PURE__*/React.createElement(React.Fragment, null, "alert ", /*#__PURE__*/React.createElement("b", null, r.val, " ", r.unit), " before high-impact news"),
      single_client: /*#__PURE__*/React.createElement(React.Fragment, null, "single-client exposure ", /*#__PURE__*/React.createElement("b", null, r.op, " ", r.val, " ", r.unit))
    }[r.kind];
    return /*#__PURE__*/React.createElement("div", {
      className: "rule-row",
      key: r.id
    }, /*#__PURE__*/React.createElement("div", {
      className: "rule-sym"
    }, r.sym), /*#__PURE__*/React.createElement("div", {
      className: "rule-desc"
    }, "Trigger when ", desc), /*#__PURE__*/React.createElement("div", {
      className: "row-flex",
      style: {
        gap: 4
      }
    }, r.ch.map(c => /*#__PURE__*/React.createElement("span", {
      key: c,
      className: "chip",
      style: {
        fontSize: 10
      }
    }, c))), /*#__PURE__*/React.createElement("div", {
      className: "row-flex",
      style: {
        gap: 8
      }
    }, /*#__PURE__*/React.createElement("span", {
      className: `rule-sev ${r.sev}`
    }, r.sev), /*#__PURE__*/React.createElement("span", {
      className: `switch ${r.enabled ? 'on' : ''}`
    })));
  }));
}
function AlertHistory({
  alerts,
  ackAlert
}) {
  return /*#__PURE__*/React.createElement("div", {
    className: "card"
  }, alerts.map(a => /*#__PURE__*/React.createElement("div", {
    key: a.id,
    className: "rule-row",
    style: {
      opacity: a.ack ? 0.55 : 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 6
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: `rule-sev ${a.severity === 'crit' ? 'crit' : a.severity === 'warn' ? 'warn' : 'info'}`
  }, a.severity)), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 13,
      fontWeight: 600
    }
  }, a.title, " ", /*#__PURE__*/React.createElement("span", {
    className: "t3",
    style: {
      fontWeight: 400,
      fontFamily: 'var(--font-mono)',
      fontSize: 12
    }
  }, "\xB7 ", a.sym)), /*#__PURE__*/React.createElement("div", {
    className: "t2",
    style: {
      fontSize: 12,
      marginTop: 2
    }
  }, a.desc)), /*#__PURE__*/React.createElement("div", {
    className: "t3 mono",
    style: {
      fontSize: 11
    }
  }, a.time), /*#__PURE__*/React.createElement("div", null, a.ack ? /*#__PURE__*/React.createElement("span", {
    className: "chip",
    style: {
      color: 'var(--t3)'
    }
  }, "acknowledged") : /*#__PURE__*/React.createElement("button", {
    className: "icon-btn",
    onClick: () => ackAlert(a.id)
  }, /*#__PURE__*/React.createElement(I.check, null), " Ack")))));
}
function ChannelsPanel() {
  const channels = [{
    name: 'In-app notification',
    desc: 'Banners + toast + sound',
    enabled: true,
    icon: '🔔'
  }, {
    name: 'Email',
    desc: 'risk@fxgrow.com, ops@fxgrow.com',
    enabled: true,
    icon: '✉'
  }, {
    name: 'Slack',
    desc: 'workspace: fxgrow · #risk-alerts',
    enabled: true,
    icon: 'S'
  }, {
    name: 'Telegram',
    desc: 'Bot: @fxgrow_alerts_bot',
    enabled: false,
    icon: 'T'
  }, {
    name: 'Webhook',
    desc: 'https://hooks.fxgrow.com/alerts',
    enabled: false,
    icon: '⇢'
  }];
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: 'repeat(auto-fit, minmax(320px,1fr))',
      gap: 12
    }
  }, channels.map(c => /*#__PURE__*/React.createElement("div", {
    key: c.name,
    className: "card",
    style: {
      padding: 16,
      display: 'flex',
      flexDirection: 'column',
      gap: 10
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "row-flex",
    style: {
      gap: 10
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 34,
      height: 34,
      borderRadius: 8,
      background: 'var(--accent-dim)',
      color: 'var(--accent)',
      display: 'grid',
      placeItems: 'center',
      fontWeight: 700
    }
  }, c.icon), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 600,
      fontSize: 13
    }
  }, c.name), /*#__PURE__*/React.createElement("div", {
    className: "t3",
    style: {
      fontSize: 11.5
    }
  }, c.desc)), /*#__PURE__*/React.createElement("span", {
    className: `switch ${c.enabled ? 'on' : ''}`
  })))));
}
window.AlertsPanel = AlertsPanel;

// ========== Settings tab (lightweight) ==========
function SettingsPanel() {
  return /*#__PURE__*/React.createElement("div", {
    className: "panel-wrap"
  }, /*#__PURE__*/React.createElement("div", {
    className: "card"
  }, /*#__PURE__*/React.createElement("div", {
    className: "card-header"
  }, /*#__PURE__*/React.createElement("div", {
    className: "card-title"
  }, "Connections")), /*#__PURE__*/React.createElement("div", {
    style: {
      padding: 16,
      display: 'grid',
      gridTemplateColumns: 'repeat(2,1fr)',
      gap: 12
    }
  }, /*#__PURE__*/React.createElement(ConnCard, {
    name: "MT5 Manager API",
    sub: "rev-14 \xB7 193.124.185.12:443",
    status: "live"
  }), /*#__PURE__*/React.createElement(ConnCard, {
    name: "Coverage Collector",
    sub: "Python FastAPI \xB7 100ms poll",
    status: "live"
  }), /*#__PURE__*/React.createElement(ConnCard, {
    name: "Centroid CS360",
    sub: "FIX 4.4 Dropcopy \xB7 Session 1",
    status: "live"
  }), /*#__PURE__*/React.createElement(ConnCard, {
    name: "Supabase",
    sub: "eu-central-1 \xB7 280K deals persisted",
    status: "live"
  }))), /*#__PURE__*/React.createElement("div", {
    className: "card"
  }, /*#__PURE__*/React.createElement("div", {
    className: "card-header"
  }, /*#__PURE__*/React.createElement("div", {
    className: "card-title"
  }, "Snapshot schedules"), /*#__PURE__*/React.createElement("div", {
    className: "spacer"
  }), /*#__PURE__*/React.createElement("button", {
    className: "icon-btn"
  }, /*#__PURE__*/React.createElement(I.plus, null), " New schedule")), /*#__PURE__*/React.createElement("div", null, window.SCHEDULES.map(s => /*#__PURE__*/React.createElement("div", {
    key: s.id,
    className: "rule-row"
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontFamily: 'var(--font-ui)',
      fontWeight: 600
    }
  }, s.name), /*#__PURE__*/React.createElement("div", {
    className: "rule-desc"
  }, /*#__PURE__*/React.createElement("b", null, s.cron), " ", /*#__PURE__*/React.createElement("span", {
    className: "t3"
  }, "\xB7 ", s.tz), /*#__PURE__*/React.createElement("span", {
    className: "t3",
    style: {
      marginLeft: 10
    }
  }, "Last: ", s.last, " \xB7 Next: ", s.next)), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("span", {
    className: `chip ${s.enabled ? 'live' : ''}`
  }, s.cadence)), /*#__PURE__*/React.createElement("span", {
    className: `switch ${s.enabled ? 'on' : ''}`
  }))))), /*#__PURE__*/React.createElement("div", {
    className: "card"
  }, /*#__PURE__*/React.createElement("div", {
    className: "card-header"
  }, /*#__PURE__*/React.createElement("div", {
    className: "card-title"
  }, "Recent activity"), /*#__PURE__*/React.createElement("div", {
    className: "spacer"
  }), /*#__PURE__*/React.createElement("span", {
    className: "t3",
    style: {
      fontSize: 11
    }
  }, "past 24h")), /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '4px 0'
    }
  }, window.ACTIVITY.map((a, i) => /*#__PURE__*/React.createElement("div", {
    key: i,
    style: {
      padding: '10px 16px',
      borderBottom: i < window.ACTIVITY.length - 1 ? '1px solid var(--divider)' : 'none',
      display: 'flex',
      gap: 12,
      alignItems: 'center'
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "mono t3",
    style: {
      fontSize: 11,
      width: 70
    }
  }, a.t), /*#__PURE__*/React.createElement("div", {
    style: {
      width: 6,
      height: 6,
      borderRadius: '50%',
      background: a.kind === 'hedge' ? 'var(--teal)' : a.kind === 'rule' ? 'var(--amber)' : a.kind === 'snap' ? 'var(--blue)' : 'var(--t4)'
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      fontSize: 12.5
    }
  }, a.text), /*#__PURE__*/React.createElement("div", {
    className: "t3",
    style: {
      fontSize: 11
    }
  }, a.user))))));
}
function ConnCard({
  name,
  sub,
  status
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      padding: 14,
      border: '1px solid var(--border)',
      borderRadius: 10,
      background: 'var(--bg-2)',
      display: 'flex',
      gap: 10
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "dot",
    style: {
      marginTop: 5
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 600,
      fontSize: 13
    }
  }, name), /*#__PURE__*/React.createElement("div", {
    className: "t3 mono",
    style: {
      fontSize: 11
    }
  }, sub)), /*#__PURE__*/React.createElement("span", {
    className: "chip live"
  }, /*#__PURE__*/React.createElement("span", {
    className: "dot"
  }), status));
}
window.SettingsPanel = SettingsPanel;

// ===== App.jsx =====

// ========== Sidebar ==========
function Sidebar({
  tab,
  setTab,
  collapsed,
  setCollapsed,
  alertCount
}) {
  const groups = [{
    label: 'Real-time',
    items: [{
      id: 'exposure',
      label: 'Exposure',
      icon: /*#__PURE__*/React.createElement(I.grid, null),
      kbd: '1',
      pill: null
    }, {
      id: 'compare',
      label: 'Compare',
      icon: /*#__PURE__*/React.createElement(I.split, null),
      kbd: '2',
      pill: null
    }, {
      id: 'bridge',
      label: 'Bridge',
      icon: /*#__PURE__*/React.createElement(I.bridge, null),
      kbd: '3',
      pill: {
        kind: 'red',
        text: '1'
      }
    }]
  }, {
    label: 'Analytics',
    items: [{
      id: 'pnl',
      label: 'P&L',
      icon: /*#__PURE__*/React.createElement(I.wallet, null),
      kbd: '4'
    }, {
      id: 'netpnl',
      label: 'Net P&L',
      icon: /*#__PURE__*/React.createElement(I.layers, null),
      kbd: '5'
    }, {
      id: 'positions',
      label: 'Positions',
      icon: /*#__PURE__*/React.createElement(I.list, null),
      kbd: '6'
    }]
  }, {
    label: 'Config',
    items: [{
      id: 'mappings',
      label: 'Mappings',
      icon: /*#__PURE__*/React.createElement(I.map, null),
      kbd: '7'
    }, {
      id: 'alerts',
      label: 'Alerts',
      icon: /*#__PURE__*/React.createElement(I.bell, null),
      kbd: '8',
      pill: alertCount ? {
        kind: 'red',
        text: alertCount
      } : null
    }, {
      id: 'settings',
      label: 'Settings',
      icon: /*#__PURE__*/React.createElement(I.gear, null),
      kbd: '9'
    }]
  }];
  return /*#__PURE__*/React.createElement("div", {
    className: "sidebar"
  }, /*#__PURE__*/React.createElement("div", {
    className: "brand"
  }, /*#__PURE__*/React.createElement("div", {
    className: "brand-mark"
  }, "C"), !collapsed && /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      minWidth: 0
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "brand-name"
  }, "Coverage Mgr"), /*#__PURE__*/React.createElement("div", {
    className: "brand-sub"
  }, "fxGROW \xB7 prod")), !collapsed && /*#__PURE__*/React.createElement("button", {
    className: "sidebar-toggle",
    onClick: () => setCollapsed(true),
    title: "Collapse sidebar  [",
    "aria-label": "Collapse sidebar"
  }, /*#__PURE__*/React.createElement(I.chevLeft, null))), collapsed && /*#__PURE__*/React.createElement("button", {
    className: "sidebar-expand-tab",
    onClick: () => setCollapsed(false),
    title: "Expand sidebar  [",
    "aria-label": "Expand sidebar"
  }, /*#__PURE__*/React.createElement(I.chevRight, null)), groups.map(g => /*#__PURE__*/React.createElement("div", {
    className: "nav-section",
    key: g.label
  }, /*#__PURE__*/React.createElement("div", {
    className: "nav-label"
  }, g.label), g.items.map(it => /*#__PURE__*/React.createElement("div", {
    key: it.id,
    className: `nav-item ${tab === it.id ? 'active' : ''}`,
    onClick: () => setTab(it.id),
    title: collapsed ? it.label : ''
  }, /*#__PURE__*/React.createElement("span", {
    className: "icon"
  }, it.icon), /*#__PURE__*/React.createElement("span", {
    className: "label"
  }, it.label), it.pill && /*#__PURE__*/React.createElement("span", {
    className: `pill ${it.pill.kind}`
  }, it.pill.text), /*#__PURE__*/React.createElement("span", {
    className: "kbd"
  }, it.kbd))))), /*#__PURE__*/React.createElement("div", {
    className: "sidebar-footer"
  }, /*#__PURE__*/React.createElement("div", {
    className: "connection"
  }, /*#__PURE__*/React.createElement("span", {
    className: "dot"
  }), /*#__PURE__*/React.createElement("span", {
    className: "label"
  }, "MT5 \xB7 Coverage \xB7 Bridge live")), /*#__PURE__*/React.createElement("div", {
    className: "connection",
    style: {
      color: 'var(--t3)'
    }
  }, /*#__PURE__*/React.createElement(I.kbd, null), !collapsed && /*#__PURE__*/React.createElement("span", {
    className: "label",
    style: {
      fontFamily: 'var(--font-mono)',
      fontSize: 10.5
    }
  }, "\u2318K \xB7 command"))));
}

// ========== Topbar ==========
function Topbar({
  totals,
  onToggleTheme,
  theme,
  onOpenTweaks,
  showBanner,
  onOpenPalette,
  alertCount,
  onOpenAlerts
}) {
  const tkey = totals.bbNet >= 0 ? 'pos' : 'neg';
  return /*#__PURE__*/React.createElement("div", {
    className: "topbar"
  }, /*#__PURE__*/React.createElement("div", {
    className: "top-totals"
  }, /*#__PURE__*/React.createElement("div", {
    className: "top-metric"
  }, /*#__PURE__*/React.createElement("span", {
    className: "lbl"
  }, "Active Clients"), /*#__PURE__*/React.createElement("span", {
    className: "val"
  }, "28,421")), /*#__PURE__*/React.createElement("div", {
    className: "top-divider"
  }), /*#__PURE__*/React.createElement("div", {
    className: "top-metric"
  }, /*#__PURE__*/React.createElement("span", {
    className: "lbl"
  }, "Open Positions"), /*#__PURE__*/React.createElement("span", {
    className: "val"
  }, "1,842")), /*#__PURE__*/React.createElement("div", {
    className: "top-divider"
  }), /*#__PURE__*/React.createElement("div", {
    className: "top-metric"
  }, /*#__PURE__*/React.createElement("span", {
    className: "lbl"
  }, "Client Exposure"), /*#__PURE__*/React.createElement("span", {
    className: `val ${pc(totals.bbNet)}`
  }, fp(totals.bbNet))), /*#__PURE__*/React.createElement("div", {
    className: "top-metric"
  }, /*#__PURE__*/React.createElement("span", {
    className: "lbl"
  }, "Coverage"), /*#__PURE__*/React.createElement("span", {
    className: `val ${pc(totals.covNet)}`
  }, fp(totals.covNet))), /*#__PURE__*/React.createElement("div", {
    className: "top-divider"
  }), /*#__PURE__*/React.createElement("div", {
    className: "top-metric highlight"
  }, /*#__PURE__*/React.createElement("span", {
    className: "lbl"
  }, "Net P&L Today"), /*#__PURE__*/React.createElement("span", {
    className: `val ${pc(totals.netPnL)}`
  }, fp(totals.netPnL)))), /*#__PURE__*/React.createElement("div", {
    className: "top-actions"
  }, /*#__PURE__*/React.createElement("div", {
    className: "search-wrap"
  }, /*#__PURE__*/React.createElement(I.search, null), /*#__PURE__*/React.createElement("input", {
    className: "search",
    placeholder: "Search symbol, login, rule\u2026",
    onFocus: onOpenPalette,
    readOnly: true
  }), /*#__PURE__*/React.createElement("span", {
    className: "kbd"
  }, "\u2318K")), /*#__PURE__*/React.createElement("button", {
    className: "icon-btn ghost",
    title: "Alerts",
    onClick: onOpenAlerts
  }, /*#__PURE__*/React.createElement(I.bell, null), alertCount > 0 && /*#__PURE__*/React.createElement("span", {
    className: "badge"
  }, alertCount)), /*#__PURE__*/React.createElement("button", {
    className: "icon-btn ghost",
    title: "Toggle theme",
    onClick: onToggleTheme
  }, theme === 'dark' ? /*#__PURE__*/React.createElement(I.sun, null) : /*#__PURE__*/React.createElement(I.moon, null)), /*#__PURE__*/React.createElement("button", {
    className: "icon-btn ghost",
    title: "Tweaks",
    onClick: onOpenTweaks
  }, /*#__PURE__*/React.createElement(I.sliders, null)), /*#__PURE__*/React.createElement("div", {
    style: {
      width: 1,
      height: 22,
      background: 'var(--border)',
      margin: '0 2px'
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 26,
      height: 26,
      borderRadius: '50%',
      background: 'linear-gradient(135deg, var(--blue), var(--purple))',
      display: 'grid',
      placeItems: 'center',
      fontSize: 11,
      fontWeight: 700,
      color: 'white'
    }
  }, "MX"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11.5,
      lineHeight: 1.2
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 600
    }
  }, "Max Carter"), /*#__PURE__*/React.createElement("div", {
    className: "t3",
    style: {
      fontSize: 10.5
    }
  }, "Dealer \xB7 London")))));
}

// ========== Command Palette ==========
function CommandPalette({
  onClose,
  setTab
}) {
  const [q, setQ] = useState('');
  const cmds = [{
    group: 'Go to',
    items: [{
      label: 'Exposure',
      act: () => setTab('exposure')
    }, {
      label: 'Compare',
      act: () => setTab('compare')
    }, {
      label: 'Bridge',
      act: () => setTab('bridge')
    }, {
      label: 'P&L',
      act: () => setTab('pnl')
    }, {
      label: 'Net P&L',
      act: () => setTab('netpnl')
    }, {
      label: 'Alerts',
      act: () => setTab('alerts')
    }, {
      label: 'Settings',
      act: () => setTab('settings')
    }]
  }, {
    group: 'Actions',
    items: [{
      label: 'Hedge XAUUSD · 48.70 lots (BUY)',
      act: () => {}
    }, {
      label: 'Capture manual snapshot',
      act: () => {}
    }, {
      label: 'New alert rule',
      act: () => setTab('alerts')
    }]
  }];
  const filtered = cmds.map(g => ({
    ...g,
    items: g.items.filter(i => i.label.toLowerCase().includes(q.toLowerCase()))
  })).filter(g => g.items.length);
  return /*#__PURE__*/React.createElement("div", {
    className: "palette-backdrop",
    onClick: onClose
  }, /*#__PURE__*/React.createElement("div", {
    className: "palette",
    onClick: e => e.stopPropagation()
  }, /*#__PURE__*/React.createElement("input", {
    className: "palette-input",
    autoFocus: true,
    placeholder: "Search commands, symbols, logins\u2026",
    value: q,
    onChange: e => setQ(e.target.value)
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      maxHeight: 400,
      overflow: 'auto',
      padding: '6px 0'
    }
  }, filtered.map(g => /*#__PURE__*/React.createElement("div", {
    key: g.group
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '8px 18px 4px',
      color: 'var(--t3)',
      fontSize: 10,
      fontWeight: 700,
      letterSpacing: '0.08em',
      textTransform: 'uppercase'
    }
  }, g.group), g.items.map(it => /*#__PURE__*/React.createElement("div", {
    key: it.label,
    onClick: () => {
      it.act();
      onClose();
    },
    style: {
      padding: '8px 18px',
      cursor: 'pointer',
      fontSize: 13
    },
    onMouseEnter: e => e.currentTarget.style.background = 'var(--card-hover)',
    onMouseLeave: e => e.currentTarget.style.background = 'transparent'
  }, it.label)))))));
}

// ========== Tweaks ==========
function TweaksPanel({
  show,
  onClose,
  tweaks,
  setTweaks
}) {
  const set = (k, v) => setTweaks({
    ...tweaks,
    [k]: v
  });
  const toggle = k => set(k, !tweaks[k]);
  return /*#__PURE__*/React.createElement("div", {
    className: `tweaks ${show ? 'show' : ''}`
  }, /*#__PURE__*/React.createElement("div", {
    className: "tweaks-header"
  }, /*#__PURE__*/React.createElement(I.sliders, null), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 600,
      fontSize: 13
    }
  }, "Tweaks"), /*#__PURE__*/React.createElement("div", {
    className: "spacer"
  }), /*#__PURE__*/React.createElement("button", {
    className: "icon-btn ghost",
    style: {
      padding: '2px 6px'
    },
    onClick: onClose
  }, /*#__PURE__*/React.createElement(I.close, null))), /*#__PURE__*/React.createElement("div", {
    className: "tweaks-body"
  }, /*#__PURE__*/React.createElement(TGroup, {
    label: "Appearance"
  }, /*#__PURE__*/React.createElement(TRow, {
    label: "Theme"
  }, /*#__PURE__*/React.createElement("div", {
    className: "segmented"
  }, /*#__PURE__*/React.createElement("button", {
    className: tweaks.theme === 'dark' ? 'active' : '',
    onClick: () => set('theme', 'dark')
  }, "Dark"), /*#__PURE__*/React.createElement("button", {
    className: tweaks.theme === 'light' ? 'active' : '',
    onClick: () => set('theme', 'light')
  }, "Light"))), /*#__PURE__*/React.createElement(TRow, {
    label: "Accent"
  }, /*#__PURE__*/React.createElement("div", {
    className: "segmented"
  }, ['blue', 'teal', 'purple'].map(c => /*#__PURE__*/React.createElement("button", {
    key: c,
    className: tweaks.accent === c ? 'active' : '',
    onClick: () => set('accent', c)
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'inline-block',
      width: 9,
      height: 9,
      borderRadius: '50%',
      marginRight: 4,
      background: c === 'blue' ? '#60A5FA' : c === 'teal' ? '#2DD4BF' : '#A78BFA'
    }
  }), c)))), /*#__PURE__*/React.createElement(TRow, {
    label: "Density"
  }, /*#__PURE__*/React.createElement("div", {
    className: "segmented"
  }, /*#__PURE__*/React.createElement("button", {
    className: tweaks.density === 'compact' ? 'active' : '',
    onClick: () => set('density', 'compact')
  }, "Compact"), /*#__PURE__*/React.createElement("button", {
    className: tweaks.density === 'comfortable' ? 'active' : '',
    onClick: () => set('density', 'comfortable')
  }, "Cozy"), /*#__PURE__*/React.createElement("button", {
    className: tweaks.density === 'spacious' ? 'active' : '',
    onClick: () => set('density', 'spacious')
  }, "Spacious"))), /*#__PURE__*/React.createElement(TRow, {
    label: "Grid lines"
  }, /*#__PURE__*/React.createElement("span", {
    className: `switch ${tweaks.grid ? 'on' : ''}`,
    onClick: () => toggle('grid')
  }))), /*#__PURE__*/React.createElement(TGroup, {
    label: "Persona"
  }, /*#__PURE__*/React.createElement(TRow, {
    label: "Role"
  }, /*#__PURE__*/React.createElement("div", {
    className: "segmented"
  }, /*#__PURE__*/React.createElement("button", {
    className: tweaks.persona === 'risk' ? 'active' : '',
    onClick: () => set('persona', 'risk')
  }, "Risk"), /*#__PURE__*/React.createElement("button", {
    className: tweaks.persona === 'ops' ? 'active' : '',
    onClick: () => set('persona', 'ops')
  }, "Ops"), /*#__PURE__*/React.createElement("button", {
    className: tweaks.persona === 'comp' ? 'active' : '',
    onClick: () => set('persona', 'comp')
  }, "Compliance")))), /*#__PURE__*/React.createElement(TGroup, {
    label: "Exposure"
  }, /*#__PURE__*/React.createElement(TRow, {
    label: "Layout"
  }, /*#__PURE__*/React.createElement("div", {
    className: "segmented"
  }, /*#__PURE__*/React.createElement("button", {
    className: tweaks.exposureLayout === 'table' ? 'active' : '',
    onClick: () => set('exposureLayout', 'table')
  }, "Table"), /*#__PURE__*/React.createElement("button", {
    className: tweaks.exposureLayout === 'cards' ? 'active' : '',
    onClick: () => set('exposureLayout', 'cards')
  }, "Cards"))), /*#__PURE__*/React.createElement(TRow, {
    label: "Show closed rows"
  }, /*#__PURE__*/React.createElement("span", {
    className: `switch ${tweaks.showClosed ? 'on' : ''}`,
    onClick: () => toggle('showClosed')
  })), /*#__PURE__*/React.createElement(TRow, {
    label: "Animate ticks"
  }, /*#__PURE__*/React.createElement("span", {
    className: `switch ${tweaks.flash ? 'on' : ''}`,
    onClick: () => toggle('flash')
  }))), /*#__PURE__*/React.createElement(TGroup, {
    label: "Alerts"
  }, /*#__PURE__*/React.createElement(TRow, {
    label: "Banner"
  }, /*#__PURE__*/React.createElement("span", {
    className: `switch ${tweaks.banner ? 'on' : ''}`,
    onClick: () => toggle('banner')
  })), /*#__PURE__*/React.createElement(TRow, {
    label: "Toast on trigger"
  }, /*#__PURE__*/React.createElement("span", {
    className: `switch ${tweaks.toasts ? 'on' : ''}`,
    onClick: () => toggle('toasts')
  })), /*#__PURE__*/React.createElement(TRow, {
    label: "Sound"
  }, /*#__PURE__*/React.createElement("span", {
    className: `switch ${tweaks.sound ? 'on' : ''}`,
    onClick: () => toggle('sound')
  })))));
}
function TGroup({
  label,
  children
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '8px 0'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      color: 'var(--t3)',
      fontSize: 10,
      fontWeight: 700,
      letterSpacing: '0.08em',
      textTransform: 'uppercase',
      marginBottom: 4
    }
  }, label), children);
}
function TRow({
  label,
  children
}) {
  return /*#__PURE__*/React.createElement("div", {
    className: "tweak-row"
  }, /*#__PURE__*/React.createElement("span", {
    className: "lbl"
  }, label), children);
}

// ========== Alert Banner ==========
function AlertBanner({
  alert,
  onDismiss,
  onGoto
}) {
  if (!alert) return null;
  return /*#__PURE__*/React.createElement("div", {
    className: "alert-banner",
    style: {
      background: alert.severity === 'crit' ? 'var(--red-dim)' : 'var(--amber-dim)',
      color: alert.severity === 'crit' ? 'var(--red)' : 'var(--amber)',
      borderBottomColor: alert.severity === 'crit' ? 'var(--red)' : 'var(--amber)'
    }
  }, /*#__PURE__*/React.createElement(I.warn, null), /*#__PURE__*/React.createElement("span", {
    style: {
      fontWeight: 700,
      letterSpacing: '0.04em',
      fontSize: 11,
      textTransform: 'uppercase'
    }
  }, alert.severity === 'crit' ? 'Critical' : 'Warning'), /*#__PURE__*/React.createElement("span", {
    style: {
      fontWeight: 500
    }
  }, alert.desc), /*#__PURE__*/React.createElement("div", {
    className: "spacer"
  }), /*#__PURE__*/React.createElement("button", {
    className: "icon-btn ghost",
    style: {
      color: 'inherit',
      padding: '2px 8px',
      fontSize: 11
    },
    onClick: onGoto
  }, "Go to rules \u2192"), /*#__PURE__*/React.createElement("button", {
    className: "icon-btn ghost",
    style: {
      color: 'inherit',
      padding: '2px 6px'
    },
    onClick: onDismiss
  }, /*#__PURE__*/React.createElement(I.close, null)));
}
