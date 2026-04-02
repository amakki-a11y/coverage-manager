import React, { useState, useEffect, useRef, useCallback } from 'react';
import { THEME } from '../theme';
import type { ExposureSummary, PriceQuote } from '../types';

type SortField = 'custom' | 'symbol' | 'bbNet' | 'bbPnL' | 'covNet' | 'covPnL' | 'netPnL' | 'hedge' | 'toCover';

interface ExposureTableProps {
  summaries: ExposureSummary[];
  prices: PriceQuote[];
}

interface ClosedSymbol {
  symbol: string;
  dealCount: number;
  totalProfit: number;
  totalCommission: number;
  totalSwap: number;
  totalFee: number;
  netPnL: number;
  totalVolume: number;
  buyVolume: number;
  sellVolume: number;
}

const c: React.CSSProperties = {
  padding: '6px 10px',
  fontFamily: 'monospace',
  fontSize: 12,
  textAlign: 'center',
  verticalAlign: 'middle',
  whiteSpace: 'nowrap',
};

function todayStr() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

export function ExposureTable({ summaries, prices }: ExposureTableProps) {
  // Build price lookup by symbol (exact match + strip trailing - for canonical matching)
  const priceMap: Record<string, PriceQuote> = {};
  for (const p of prices) {
    priceMap[p.symbol] = p;
    // Also index by stripped symbol (e.g. "XAUUSD-" → "XAUUSD")
    const stripped = p.symbol.replace(/[-.]$/, '');
    if (stripped !== p.symbol && !priceMap[stripped]) priceMap[stripped] = p;
  }
  // Helper: find price for a canonical symbol
  const findPrice = (sym: string): PriceQuote | undefined => {
    return priceMap[sym] || priceMap[sym.replace(/[-.]$/, '')];
  };

  // Track price direction (up/down/flat)
  const prevPrices = useRef<Record<string, number>>({});
  const priceDir = useRef<Record<string, 'up' | 'down' | 'flat'>>({});
  for (const p of prices) {
    const prev = prevPrices.current[p.symbol];
    if (prev !== undefined && p.bid !== prev) {
      priceDir.current[p.symbol] = p.bid > prev ? 'up' : 'down';
    }
    prevPrices.current[p.symbol] = p.bid;
  }

  const [bbClosedMap, setBbClosedMap] = useState<Record<string, ClosedSymbol>>({});
  const [covClosedMap, setCovClosedMap] = useState<Record<string, ClosedSymbol>>({});
  const [closedFrom, setClosedFrom] = useState(todayStr);
  const [closedTo, setClosedTo] = useState(todayStr);
  const [showGrid, setShowGrid] = useState(() => {
    return localStorage.getItem('exposureGrid') !== 'false';
  });
  const [sortField, setSortField] = useState<SortField>(() => {
    return (localStorage.getItem('exposureSort') as SortField) || 'custom';
  });
  const [sortAsc, setSortAsc] = useState(() => {
    return localStorage.getItem('exposureSortAsc') !== 'false';
  });
  const [customOrder, setCustomOrder] = useState<string[]>(() => {
    try { return JSON.parse(localStorage.getItem('exposureOrder') || '[]'); } catch { return []; }
  });
  const dragSymbol = useRef<string | null>(null);
  const dragOverSymbol = useRef<string | null>(null);

  // Theme-dependent styles (must be inside component so they update on theme change)
  const secBorder = `2px solid ${THEME.border}`;
  const rowBorder = `1px solid ${THEME.border}`;
  const hdr: React.CSSProperties = {
    ...c,
    fontSize: 9,
    fontWeight: 600,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    fontFamily: 'inherit',
    color: THEME.t3,
    borderBottom: rowBorder,
    position: 'sticky',
    background: THEME.bg2,
    zIndex: 1,
    whiteSpace: 'nowrap',
  };

  useEffect(() => {
    const fetchClosed = async () => {
      try {
        const [bbRes, covRes, mapRes] = await Promise.all([
          fetch('http://localhost:5000/api/exposure/pnl'),
          fetch(`http://localhost:8100/deals?from=${closedFrom}&to=${closedTo}`),
          fetch('http://localhost:5000/api/mappings'),
        ]);

        const bbMap: Record<string, ClosedSymbol> = {};
        const bbSymbols: string[] = [];
        if (bbRes.ok) {
          const bb = await bbRes.json();
          for (const s of (bb.symbols ?? []) as ClosedSymbol[]) {
            bbMap[s.symbol] = s;
            bbSymbols.push(s.symbol);
          }
        }
        setBbClosedMap(bbMap);

        if (covRes.ok && mapRes.ok) {
          const cov = await covRes.json();
          const mappings: { canonical_name: string; coverage_symbol: string }[] = await mapRes.json();
          const covToCanonical: Record<string, string> = {};
          for (const m of mappings) {
            if (m.coverage_symbol) covToCanonical[m.coverage_symbol] = m.canonical_name;
          }
          const findBB = (sym: string): string => {
            const can = covToCanonical[sym] || covToCanonical[sym.replace(/[-.].*$/, '')] || sym;
            return bbSymbols.find(s => s === can || s.startsWith(can)) || sym;
          };
          const remapped: Record<string, ClosedSymbol> = {};
          for (const s of (cov.symbols ?? []) as ClosedSymbol[]) {
            const key = findBB(s.symbol);
            if (remapped[key]) {
              remapped[key].dealCount += s.dealCount;
              remapped[key].netPnL += s.netPnL;
              remapped[key].totalProfit += s.totalProfit;
              remapped[key].totalVolume += s.totalVolume;
              remapped[key].buyVolume += s.buyVolume;
              remapped[key].sellVolume += s.sellVolume;
            } else {
              remapped[key] = { ...s, symbol: key };
            }
          }
          setCovClosedMap(remapped);
        }
      } catch { /* ignore */ }
    };
    fetchClosed();
    const interval = setInterval(fetchClosed, 5000);
    return () => clearInterval(interval);
  }, [closedFrom, closedTo]);

  // Totals
  const openTotals = summaries.reduce(
    (a, s) => ({
      bbBuy: a.bbBuy + (s.bBookBuyVolume ?? 0),
      bbSell: a.bbSell + (s.bBookSellVolume ?? 0),
      bbNet: a.bbNet + (s.bBookNetVolume ?? 0),
      bbPnL: a.bbPnL + (s.bBookPnL ?? 0),
      covBuy: a.covBuy + (s.coverageBuyVolume ?? 0),
      covSell: a.covSell + (s.coverageSellVolume ?? 0),
      covNet: a.covNet + (s.coverageNetVolume ?? 0),
      covPnL: a.covPnL + (s.coveragePnL ?? 0),
      netVol: a.netVol + (s.netVolume ?? 0),
      netPnL: a.netPnL + (s.netPnL ?? 0),
    }),
    { bbBuy: 0, bbSell: 0, bbNet: 0, bbPnL: 0, covBuy: 0, covSell: 0, covNet: 0, covPnL: 0, netVol: 0, netPnL: 0 }
  );

  const closedBBPnL = Object.values(bbClosedMap).reduce((a, s) => a + s.netPnL, 0);
  const closedCovPnL = Object.values(covClosedMap).reduce((a, s) => a + s.netPnL, 0);

  const typeCell: React.CSSProperties = {
    ...c,
    textAlign: 'center',
    fontFamily: 'inherit',
    fontSize: 12,
    fontWeight: 600,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    width: 50,
    padding: '6px 6px',
  };

  const gridBorder = showGrid ? `2px solid ${THEME.t3}` : 'none';
  const gridSecBorder = showGrid ? secBorder : 'none';
  const gc: React.CSSProperties = showGrid ? { borderLeft: `1px solid ${THEME.border}`, borderBottom: `1px solid ${THEME.border}` } : {};

  const toggleGrid = () => {
    setShowGrid(prev => {
      const next = !prev;
      localStorage.setItem('exposureGrid', String(next));
      return next;
    });
  };

  // Sort logic
  const handleSort = (field: SortField) => {
    if (field === sortField && field !== 'custom') {
      const next = !sortAsc;
      setSortAsc(next);
      localStorage.setItem('exposureSortAsc', String(next));
    } else {
      setSortField(field);
      setSortAsc(field === 'symbol');
      localStorage.setItem('exposureSort', field);
      localStorage.setItem('exposureSortAsc', String(field === 'symbol'));
    }
  };

  const sortedSummaries = React.useMemo(() => {
    const arr = [...summaries];
    if (sortField === 'custom') {
      if (customOrder.length === 0) return arr;
      const orderMap = new Map(customOrder.map((s, i) => [s, i]));
      arr.sort((a, b) => {
        const ai = orderMap.get(a.canonicalSymbol) ?? 9999;
        const bi = orderMap.get(b.canonicalSymbol) ?? 9999;
        return ai - bi;
      });
      return arr;
    }
    const dir = sortAsc ? 1 : -1;
    arr.sort((a, b) => {
      let av = 0, bv = 0;
      switch (sortField) {
        case 'symbol': return dir * a.canonicalSymbol.localeCompare(b.canonicalSymbol);
        case 'bbNet': av = a.bBookNetVolume ?? 0; bv = b.bBookNetVolume ?? 0; break;
        case 'bbPnL': av = a.bBookPnL ?? 0; bv = b.bBookPnL ?? 0; break;
        case 'covNet': av = a.coverageNetVolume ?? 0; bv = b.coverageNetVolume ?? 0; break;
        case 'covPnL': av = a.coveragePnL ?? 0; bv = b.coveragePnL ?? 0; break;
        case 'netPnL': av = a.netPnL ?? 0; bv = b.netPnL ?? 0; break;
        case 'hedge': av = a.hedgeRatio ?? 0; bv = b.hedgeRatio ?? 0; break;
        case 'toCover':
          av = (a.bBookNetVolume ?? 0) - (a.coverageNetVolume ?? 0);
          bv = (b.bBookNetVolume ?? 0) - (b.coverageNetVolume ?? 0);
          break;
      }
      return dir * (av - bv);
    });
    return arr;
  }, [summaries, sortField, sortAsc, customOrder]);

  // Drag and drop handlers
  const handleDragStart = useCallback((sym: string) => {
    dragSymbol.current = sym;
  }, []);

  const handleDragOver = useCallback((e: React.DragEvent, sym: string) => {
    e.preventDefault();
    dragOverSymbol.current = sym;
  }, []);

  const handleDrop = useCallback(() => {
    const from = dragSymbol.current;
    const to = dragOverSymbol.current;
    if (!from || !to || from === to) return;

    const currentOrder = customOrder.length > 0
      ? [...customOrder]
      : sortedSummaries.map(s => s.canonicalSymbol);

    // Ensure both symbols exist in the order
    if (!currentOrder.includes(from)) currentOrder.push(from);
    if (!currentOrder.includes(to)) currentOrder.push(to);

    const fromIdx = currentOrder.indexOf(from);
    const toIdx = currentOrder.indexOf(to);
    currentOrder.splice(fromIdx, 1);
    currentOrder.splice(toIdx, 0, from);

    setCustomOrder(currentOrder);
    localStorage.setItem('exposureOrder', JSON.stringify(currentOrder));
    setSortField('custom');
    localStorage.setItem('exposureSort', 'custom');

    dragSymbol.current = null;
    dragOverSymbol.current = null;
  }, [customOrder, sortedSummaries]);

  const sortArrow = (field: SortField) => {
    if (sortField !== field) return '';
    return sortAsc ? ' \u25B2' : ' \u25BC';
  };

  const sortBtn: React.CSSProperties = {
    background: 'transparent',
    border: 'none',
    cursor: 'pointer',
    padding: 0,
    font: 'inherit',
    color: 'inherit',
    fontWeight: 'inherit',
    fontSize: 'inherit',
    textTransform: 'inherit' as React.CSSProperties['textTransform'],
    letterSpacing: 'inherit',
    whiteSpace: 'nowrap',
  };

  return (
    <div style={{ overflow: 'auto', flex: 1 }}>
      {/* Toolbar */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 8,
        padding: '4px 12px', background: THEME.bg2,
        borderBottom: `1px solid ${THEME.border}`,
      }}>
        <button
          onClick={toggleGrid}
          style={{
            background: showGrid ? THEME.bg3 : 'transparent',
            border: `1px solid ${THEME.border}`,
            borderRadius: 4,
            padding: '3px 10px',
            cursor: 'pointer',
            fontSize: 11,
            fontWeight: 600,
            color: showGrid ? THEME.t1 : THEME.t3,
          }}
        >
          Grid
        </button>
        <div style={{ borderLeft: `1px solid ${THEME.border}`, height: 20, margin: '0 4px' }} />
        <span style={{ color: THEME.t3, fontSize: 11, fontWeight: 600 }}>Sort:</span>
        <select
          value={sortField}
          onChange={e => handleSort(e.target.value as SortField)}
          style={{
            background: THEME.bg3,
            border: `1px solid ${THEME.border}`,
            borderRadius: 4,
            padding: '3px 8px',
            fontSize: 11,
            color: THEME.t1,
            fontFamily: 'inherit',
            cursor: 'pointer',
          }}
        >
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
        <button
          onClick={() => { setSortAsc(p => { const n = !p; localStorage.setItem('exposureSortAsc', String(n)); return n; }); }}
          style={{
            background: 'transparent',
            border: `1px solid ${THEME.border}`,
            borderRadius: 4,
            padding: '3px 8px',
            cursor: 'pointer',
            fontSize: 11,
            fontWeight: 600,
            color: THEME.t3,
          }}
          title={sortAsc ? 'Ascending' : 'Descending'}
        >
          {sortAsc ? '\u25B2' : '\u25BC'}
        </button>
        <div style={{ borderLeft: `1px solid ${THEME.border}`, height: 20, margin: '0 4px' }} />
        <span style={{ color: THEME.t3, fontSize: 11, fontWeight: 600 }}>Closed:</span>
        <input
          type="date"
          value={closedFrom}
          onChange={e => setClosedFrom(e.target.value)}
          style={{
            background: THEME.bg3,
            border: `1px solid ${THEME.border}`,
            borderRadius: 4,
            padding: '3px 8px',
            fontSize: 11,
            color: THEME.t1,
            fontFamily: 'inherit',
          }}
        />
        <span style={{ color: THEME.t3, fontSize: 11 }}>to</span>
        <input
          type="date"
          value={closedTo}
          onChange={e => setClosedTo(e.target.value)}
          style={{
            background: THEME.bg3,
            border: `1px solid ${THEME.border}`,
            borderRadius: 4,
            padding: '3px 8px',
            fontSize: 11,
            color: THEME.t1,
            fontFamily: 'inherit',
          }}
        />
      </div>
      <table style={{ width: '100%', borderCollapse: 'collapse' }}>
        <thead>
          {/* Group headers */}
          <tr>
            <th style={{ ...hdr, width: 80, borderBottom: 'none', top: 0, zIndex: 2 }} rowSpan={2}></th>
            <th style={{ ...hdr, width: 50, borderBottom: 'none', top: 0, zIndex: 2 }} rowSpan={2}></th>

            <th style={{ ...hdr, textAlign: 'center', color: THEME.blue, borderLeft: secBorder, borderBottom: 'none', fontSize: 11, fontWeight: 700, letterSpacing: 1, top: 0, zIndex: 2 }} colSpan={4}>Clients</th>
            <th style={{ ...hdr, textAlign: 'center', color: THEME.teal, borderLeft: secBorder, borderBottom: 'none', fontSize: 11, fontWeight: 700, letterSpacing: 1, top: 0, zIndex: 2 }} colSpan={4}>Coverage</th>
            <th style={{ ...hdr, textAlign: 'center', color: THEME.t2, borderLeft: secBorder, borderBottom: 'none', fontSize: 11, fontWeight: 700, letterSpacing: 1, top: 0, zIndex: 2 }} colSpan={3}>Summary</th>
          </tr>
          {/* Sub headers */}
          <tr>
            <th style={{ ...hdr, borderLeft: secBorder, top: 26 }}>Buy</th>
            <th style={{ ...hdr, top: 26 }}>Sell</th>
            <th style={{ ...hdr, top: 26 }}>Net</th>
            <th style={{ ...hdr, top: 26 }}>P&L</th>
            <th style={{ ...hdr, borderLeft: secBorder, top: 26 }}>Buy</th>
            <th style={{ ...hdr, top: 26 }}>Sell</th>
            <th style={{ ...hdr, top: 26 }}>Net</th>
            <th style={{ ...hdr, top: 26 }}>P&L</th>
            <th style={{ ...hdr, borderLeft: secBorder, top: 26 }}>To Cover</th>
            <th style={{ ...hdr, top: 26 }}>Net P&L</th>
            <th style={{ ...hdr, top: 26 }}>Hedge</th>
          </tr>
        </thead>
        <tbody>
          {sortedSummaries.map((s) => {
            const hr = s.hedgeRatio ?? 0;
            const bb = bbClosedMap[s.canonicalSymbol];
            const cv = covClosedMap[s.canonicalSymbol];
            const hasClosed = bb || cv;
            return (
              <React.Fragment key={s.canonicalSymbol}>
                {/* OPEN row */}
                <tr
                  style={{ borderTop: gridBorder }}
                  draggable
                  onDragStart={() => handleDragStart(s.canonicalSymbol)}
                  onDragOver={(e) => handleDragOver(e, s.canonicalSymbol)}
                  onDrop={handleDrop}
                >
                  <td rowSpan={2} style={{
                    ...c, ...gc, color: THEME.t1, fontWeight: 700, fontFamily: 'inherit',
                    borderLeft: 'none', cursor: sortField === 'custom' ? 'grab' : 'default',
                  }}>
                    {s.canonicalSymbol}
                    {(() => {
                      const price = findPrice(s.canonicalSymbol);
                      if (!price) return null;
                      const dir = priceDir.current[price.symbol] || 'flat';
                      const dirColor = dir === 'up' ? THEME.green : dir === 'down' ? THEME.red : THEME.t3;
                      return (
                        <div style={{ fontSize: 11, fontWeight: 700, fontFamily: 'monospace', color: dirColor, marginTop: 2 }}>
                          {price.bid}
                        </div>
                      );
                    })()}
                  </td>
                  <td style={{ ...typeCell, ...gc, color: THEME.blue }}>O</td>
                  <td style={{ ...c, ...gc, color: THEME.green, borderLeft: gridSecBorder }}>{fmt(s.bBookBuyVolume)}</td>
                  <td style={{ ...c, ...gc, color: THEME.red }}>{fmt(s.bBookSellVolume)}</td>
                  <td style={{ ...c, ...gc, color: pc(s.bBookNetVolume), fontWeight: 600 }}>{fmt(s.bBookNetVolume)}</td>
                  <td style={{ ...c, ...gc, color: pc(s.bBookPnL) }}>{fp(s.bBookPnL)}</td>
                  <td style={{ ...c, ...gc, color: THEME.green, borderLeft: gridSecBorder }}>{fmt(s.coverageBuyVolume)}</td>
                  <td style={{ ...c, ...gc, color: THEME.red }}>{fmt(s.coverageSellVolume)}</td>
                  <td style={{ ...c, ...gc, color: pc(s.coverageNetVolume), fontWeight: 600 }}>{fmt(s.coverageNetVolume)}</td>
                  <td style={{ ...c, ...gc, color: pc(s.coveragePnL) }}>{fp(s.coveragePnL)}</td>
                  <td style={{ ...c, ...gc, borderLeft: gridSecBorder, color: toCoverColor(toCoverValue(s.bBookNetVolume, s.coverageNetVolume)), fontWeight: 600 }}>{fmtToCover(toCoverValue(s.bBookNetVolume, s.coverageNetVolume))}</td>
                  <td style={{ ...c, ...gc, color: pc(s.netPnL), fontWeight: 700 }}>{fp(s.netPnL)}</td>
                  <td style={{ ...c, ...gc, color: hedgeColor(hr), fontWeight: 600 }}>{hr.toFixed(0)}%</td>
                </tr>
                {/* CLOSED row — always shown for consistent row height */}
                <tr
                  style={{ background: 'rgba(255,255,255,0.015)', borderBottom: gridBorder }}
                  onDragOver={(e) => handleDragOver(e, s.canonicalSymbol)}
                  onDrop={handleDrop}
                >
                    <td style={{ ...typeCell, ...gc, color: THEME.t3 }}>C</td>
                    {/* Clients closed */}
                    <td style={{ ...c, ...gc, color: THEME.green, borderLeft: gridSecBorder }}>{bb ? fmt(bb.buyVolume) : ''}</td>
                    <td style={{ ...c, ...gc, color: THEME.red }}>{bb ? fmt(bb.sellVolume) : ''}</td>
                    <td style={{ ...c, ...gc, color: THEME.t3 }}>{bb ? fmt(bb.totalVolume) : ''}</td>
                    <td style={{ ...c, ...gc, color: bb ? pc(bb.netPnL) : THEME.t3, fontWeight: 600 }}>
                      {bb ? fp(bb.netPnL) : ''}
                    </td>
                    {/* Coverage closed */}
                    <td style={{ ...c, ...gc, color: THEME.green, borderLeft: gridSecBorder }}>{cv ? fmt(cv.buyVolume) : ''}</td>
                    <td style={{ ...c, ...gc, color: THEME.red }}>{cv ? fmt(cv.sellVolume) : ''}</td>
                    <td style={{ ...c, ...gc, color: THEME.t3 }}>{cv ? fmt(cv.totalVolume) : ''}</td>
                    <td style={{ ...c, ...gc, color: cv ? pc(cv.netPnL) : THEME.t3, fontWeight: 600 }}>
                      {cv ? fp(cv.netPnL) : ''}
                    </td>
                    {/* Summary closed */}
                    <td style={{ ...c, ...gc, borderLeft: gridSecBorder }}></td>
                    <td style={{ ...c, ...gc }}></td>
                    <td style={{ ...c, ...gc, color: pc((bb?.netPnL ?? 0) + (cv?.netPnL ?? 0)), fontWeight: 600 }}>
                      {(bb || cv) ? fp((bb?.netPnL ?? 0) + (cv?.netPnL ?? 0)) : ''}
                    </td>
                  </tr>
              </React.Fragment>
            );
          })}
        </tbody>
        <tfoot>
          {/* Open totals */}
          <tr style={{ background: THEME.bg3, borderTop: `2px solid ${THEME.border}` }}>
            <td style={{ ...c, ...gc, fontFamily: 'inherit', color: THEME.t2, fontWeight: 600, borderLeft: 'none' }}>TOTAL</td>
            <td style={{ ...typeCell, ...gc, color: THEME.blue }}>O</td>
            <td style={{ ...c, ...gc, color: THEME.green, borderLeft: gridSecBorder }}>{fmt(openTotals.bbBuy)}</td>
            <td style={{ ...c, ...gc, color: THEME.red }}>{fmt(openTotals.bbSell)}</td>
            <td style={{ ...c, ...gc, color: pc(openTotals.bbNet), fontWeight: 600 }}>{fmt(openTotals.bbNet)}</td>
            <td style={{ ...c, ...gc, color: pc(openTotals.bbPnL) }}>{fp(openTotals.bbPnL)}</td>
            <td style={{ ...c, ...gc, color: THEME.green, borderLeft: gridSecBorder }}>{fmt(openTotals.covBuy)}</td>
            <td style={{ ...c, ...gc, color: THEME.red }}>{fmt(openTotals.covSell)}</td>
            <td style={{ ...c, ...gc, color: pc(openTotals.covNet), fontWeight: 600 }}>{fmt(openTotals.covNet)}</td>
            <td style={{ ...c, ...gc, color: pc(openTotals.covPnL) }}>{fp(openTotals.covPnL)}</td>
            <td style={{ ...c, ...gc, borderLeft: gridSecBorder, color: toCoverColor(toCoverValue(openTotals.bbNet, openTotals.covNet)), fontWeight: 600 }}>{fmtToCover(toCoverValue(openTotals.bbNet, openTotals.covNet))}</td>
            <td style={{ ...c, ...gc, color: pc(openTotals.netPnL), fontWeight: 700 }}>{fp(openTotals.netPnL)}</td>
            <td style={{ ...c, ...gc }} />
          </tr>
          {/* Closed totals */}
          <tr style={{ background: 'rgba(255,255,255,0.02)' }}>
            <td style={{ ...c, ...gc, fontFamily: 'inherit', color: THEME.t3, fontWeight: 600, borderLeft: 'none' }}>TOTAL</td>
            <td style={{ ...typeCell, ...gc, color: THEME.t3 }}>C</td>
            <td style={{ ...c, ...gc, borderLeft: gridSecBorder }}></td>
            <td style={{ ...c, ...gc }}></td>
            <td style={{ ...c, ...gc }}></td>
            <td style={{ ...c, ...gc, color: pc(closedBBPnL), fontWeight: 600 }}>{fp(closedBBPnL)}</td>
            <td style={{ ...c, ...gc, borderLeft: gridSecBorder }}></td>
            <td style={{ ...c, ...gc }}></td>
            <td style={{ ...c, ...gc }}></td>
            <td style={{ ...c, ...gc, color: pc(closedCovPnL), fontWeight: 600 }}>{fp(closedCovPnL)}</td>
            <td style={{ ...c, ...gc, borderLeft: gridSecBorder }}></td>
            <td style={{ ...c, ...gc }}></td>
            <td style={{ ...c, ...gc, color: pc(closedBBPnL + closedCovPnL), fontWeight: 700 }}>{fp(closedBBPnL + closedCovPnL)}</td>
          </tr>
        </tfoot>
      </table>
    </div>
  );
}

function fmt(v: number | undefined): string {
  return (v ?? 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function fp(v: number | undefined): string {
  const n = v ?? 0;
  return (n >= 0 ? '+' : '') + n.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function pc(v: number | undefined): string {
  const n = v ?? 0;
  return n > 0 ? THEME.green : n < 0 ? THEME.red : THEME.t2;
}

function toCoverValue(bbNet: number | undefined, covNet: number | undefined): number {
  return (bbNet ?? 0) - (covNet ?? 0);
}

function fmtToCover(v: number): string {
  if (Math.abs(v) < 0.005) return '';
  if (v < 0) return '-' + Math.abs(v).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  return v.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function toCoverColor(v: number): string {
  if (Math.abs(v) < 0.005) return THEME.t3;
  return v < 0 ? THEME.red : THEME.green;
}

function hedgeColor(r: number): string {
  if (r >= 80) return THEME.green;
  if (r >= 50) return THEME.amber;
  return THEME.red;
}
