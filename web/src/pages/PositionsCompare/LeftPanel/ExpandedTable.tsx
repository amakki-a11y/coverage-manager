import React, { useState, useEffect, useRef } from 'react';
import { THEME } from '../../../theme';
import type { SymbolExposure } from '../../../types/compare';
import type { PriceQuote } from '../../../types';
import { useDateRange } from '../../../hooks/useDateRange';

interface ClosedSymbol {
  symbol: string;
  dealCount: number;
  totalProfit: number;
  netPnL: number;
  totalVolume: number;
  buyVolume: number;
  sellVolume: number;
}

interface ExpandedTableProps {
  symbols: SymbolExposure[];
  selectedSymbol: string | null;
  onSelect: (symbol: string) => void;
}

function hedgeColor(pct: number): string {
  if (pct >= 80) return THEME.green;
  if (pct >= 50) return THEME.amber;
  return THEME.red;
}

function nc(v: number): string {
  return v >= 0 ? THEME.blue : THEME.red;
}

function fp(v: number): string {
  const sign = v >= 0 ? '+' : '';
  return `${sign}${v.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

function fmt(v: number): string {
  return v.toFixed(2);
}

function toCoverValue(bbNet: number, covNet: number): number {
  return bbNet - covNet;
}

function fmtToCover(v: number): string {
  if (Math.abs(v) < 0.005) return '0.00';
  return `${v < 0 ? '-' : ''}${Math.abs(v).toFixed(2)}`;
}

const cellBase: React.CSSProperties = {
  padding: '6px 10px',
  fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace",
  fontSize: 12,
  textAlign: 'center',
  verticalAlign: 'middle',
  whiteSpace: 'nowrap',
};

export function ExpandedTable({ symbols, selectedSymbol, onSelect }: ExpandedTableProps) {
  const [showClosed, setShowClosed] = useState(true);
  // Shared range — same localStorage key as Exposure / P&L / Net P&L so the Full
  // Table settled totals match the other tabs without the user re-picking.
  const [closedFrom, closedTo, setClosedFrom, setClosedTo] = useDateRange();
  const [bbClosedMap, setBbClosedMap] = useState<Record<string, ClosedSymbol>>({});
  const [covClosedMap, setCovClosedMap] = useState<Record<string, ClosedSymbol>>({});
  const [prices, setPrices] = useState<PriceQuote[]>([]);
  const priceMapRef = useRef<Record<string, PriceQuote>>({});
  const prevPrices = useRef<Record<string, number>>({});
  const priceDir = useRef<Record<string, 'up' | 'down' | 'flat'>>({});

  // Fetch live prices
  useEffect(() => {
    const ws = new WebSocket('ws://localhost:5000/ws/exposure');
    ws.onmessage = (e) => {
      try {
        const msg = JSON.parse(e.data);
        if (msg.type === 'exposure_update' && msg.data?.prices) {
          setPrices(msg.data.prices);
        }
      } catch {}
    };
    return () => ws.close();
  }, []);

  // Build price map & direction tracking
  const priceMap: Record<string, PriceQuote> = {};
  for (const p of prices) {
    priceMap[p.symbol] = p;
    priceMap[p.symbol.toUpperCase()] = p;
    const prev = prevPrices.current[p.symbol];
    if (prev !== undefined && p.bid !== prev) {
      priceDir.current[p.symbol] = p.bid > prev ? 'up' : 'down';
    }
    prevPrices.current[p.symbol] = p.bid;
  }
  priceMapRef.current = priceMap;

  const findPrice = (sym: string): PriceQuote | undefined => {
    return priceMap[sym] || priceMap[sym.replace(/[-.]$/, '')] ||
      priceMap[sym.toUpperCase()] || priceMap[sym.toUpperCase().replace(/[-.]$/, '')];
  };

  // Fetch closed deals
  useEffect(() => {
    const fetchClosed = async () => {
      try {
        const [bbRes, covRes, mapRes] = await Promise.all([
          fetch(`http://localhost:5000/api/exposure/pnl?from=${closedFrom}&to=${closedTo}`),
          fetch(`http://localhost:8100/deals?from=${closedFrom}&to=${closedTo}`).catch(() => null),
          fetch('http://localhost:5000/api/mappings'),
        ]);

        const bbMap: Record<string, ClosedSymbol> = {};
        const bbSymbols: string[] = [];
        if (bbRes.ok) {
          const bb = await bbRes.json();
          for (const s of (bb.symbols ?? []) as ClosedSymbol[]) {
            // Single uppercase canonical key — prevents Object.values() from double-counting
            // when the footer totals reduce across entries.
            const canonical = s.symbol.replace(/[-.]$/, '').toUpperCase();
            if (bbMap[canonical]) {
              const e = bbMap[canonical];
              e.dealCount += s.dealCount;
              e.netPnL += s.netPnL;
              e.totalProfit += s.totalProfit;
              e.totalVolume += s.totalVolume;
              e.buyVolume += s.buyVolume;
              e.sellVolume += s.sellVolume;
            } else {
              bbMap[canonical] = { ...s };
            }
            bbSymbols.push(s.symbol);
          }
        }
        setBbClosedMap(bbMap);

        if (covRes?.ok && mapRes.ok) {
          const cov = await covRes.json();
          const mappings: { canonical_name: string; coverage_symbol: string }[] = await mapRes.json();
          const covToCanonical: Record<string, string> = {};
          for (const m of mappings) {
            if (m.coverage_symbol) covToCanonical[m.coverage_symbol] = m.canonical_name;
          }
          const toCanonical = (sym: string): string => {
            return (covToCanonical[sym] || sym.replace(/[-.].*$/, '') || sym).toUpperCase();
          };
          const remapped: Record<string, ClosedSymbol> = {};
          for (const s of (cov.symbols ?? []) as ClosedSymbol[]) {
            const canonical = toCanonical(s.symbol);
            if (remapped[canonical]) {
              remapped[canonical].dealCount += s.dealCount;
              remapped[canonical].netPnL += s.netPnL;
              remapped[canonical].totalProfit += s.totalProfit;
              remapped[canonical].totalVolume += s.totalVolume;
              remapped[canonical].buyVolume += s.buyVolume;
              remapped[canonical].sellVolume += s.sellVolume;
            } else {
              remapped[canonical] = { ...s, symbol: canonical };
            }
          }
          setCovClosedMap(remapped);
        }
      } catch {}
    };
    fetchClosed();
    const interval = setInterval(fetchClosed, 5000);
    return () => clearInterval(interval);
  }, [closedFrom, closedTo]);

  const secBorder = `2px solid ${THEME.border}`;
  const gridBorder = `1px solid ${THEME.border}`;
  const gridSecBorder = secBorder;

  const hdr: React.CSSProperties = {
    ...cellBase,
    fontSize: 9,
    fontWeight: 600,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    fontFamily: 'inherit',
    color: THEME.t3,
    borderBottom: gridBorder,
    position: 'sticky',
    background: THEME.bg2,
    zIndex: 1,
  };

  const c = cellBase;
  const gc: React.CSSProperties = { borderBottom: gridBorder };

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

  return (
    <div style={{ display: 'flex', flexDirection: 'column', flex: 1, overflow: 'hidden' }}>
      {/* Date range picker + closed toggle */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 8, padding: '6px 12px',
        borderBottom: `1px solid ${THEME.border}`, flexShrink: 0,
      }}>
        <label style={{ display: 'flex', alignItems: 'center', gap: 4, cursor: 'pointer' }}>
          <input
            type="checkbox"
            checked={showClosed}
            onChange={e => setShowClosed(e.target.checked)}
            style={{ cursor: 'pointer', accentColor: THEME.teal }}
          />
          <span style={{ color: THEME.t3, fontSize: 11, fontWeight: 600 }}>Closed</span>
        </label>
        {showClosed && (
          <>
            <input
              type="date"
              value={closedFrom}
              onChange={e => setClosedFrom(e.target.value)}
              style={{ fontSize: 11, fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace", background: THEME.bg3, color: THEME.t1, border: `1px solid ${THEME.border}`, borderRadius: 4, padding: '2px 6px' }}
            />
            <span style={{ color: THEME.t3, fontSize: 11 }}>to</span>
            <input
              type="date"
              value={closedTo}
              onChange={e => setClosedTo(e.target.value)}
              style={{ fontSize: 11, fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace", background: THEME.bg3, color: THEME.t1, border: `1px solid ${THEME.border}`, borderRadius: 4, padding: '2px 6px' }}
            />
          </>
        )}
      </div>

      <div style={{ overflow: 'auto', flex: 1 }}>
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr>
              <th style={{ ...hdr, textAlign: 'left', top: 0 }} rowSpan={2}>Symbol</th>
              <th style={{ ...hdr, top: 0 }} rowSpan={2}></th>
              <th colSpan={4} style={{ ...hdr, top: 0, borderLeft: gridSecBorder, color: THEME.blue }}>CLIENTS</th>
              <th colSpan={4} style={{ ...hdr, top: 0, borderLeft: gridSecBorder, color: THEME.amber }}>COVERAGE</th>
              <th colSpan={3} style={{ ...hdr, top: 0, borderLeft: gridSecBorder }}>SUMMARY</th>
            </tr>
            <tr>
              <th style={{ ...hdr, top: 26, borderLeft: gridSecBorder }}>Buy</th>
              <th style={{ ...hdr, top: 26 }}>Sell</th>
              <th style={{ ...hdr, top: 26 }}>Net</th>
              <th style={{ ...hdr, top: 26 }}>P&L</th>
              <th style={{ ...hdr, top: 26, borderLeft: gridSecBorder }}>Buy</th>
              <th style={{ ...hdr, top: 26 }}>Sell</th>
              <th style={{ ...hdr, top: 26 }}>Net</th>
              <th style={{ ...hdr, top: 26 }}>P&L</th>
              <th style={{ ...hdr, top: 26, borderLeft: gridSecBorder }}>To Cover</th>
              <th style={{ ...hdr, top: 26 }}>Net P&L</th>
              <th style={{ ...hdr, top: 26 }}>Hedge</th>
            </tr>
          </thead>
          <tbody>
            {symbols.map(s => {
              const canonKey = s.symbol.replace(/[-.]$/, '').toUpperCase();
              const bb = bbClosedMap[canonKey];
              const cv = covClosedMap[canonKey];
              const price = findPrice(s.symbol);
              const dir = price ? (priceDir.current[price.symbol] || 'flat') : 'flat';
              const dirColor = dir === 'up' ? THEME.green : dir === 'down' ? THEME.red : THEME.t3;
              const hr = s.hedgePercent ?? 0;

              return (
                <React.Fragment key={s.symbol}>
                  {/* OPEN row */}
                  <tr
                    onClick={() => onSelect(s.symbol)}
                    style={{
                      cursor: 'pointer',
                      borderTop: `2px solid ${THEME.border}`,
                      background: s.symbol === selectedSymbol ? THEME.rowSelected : 'transparent',
                    }}
                    onMouseEnter={e => e.currentTarget.style.background = THEME.rowHover}
                    onMouseLeave={e => e.currentTarget.style.background = s.symbol === selectedSymbol ? THEME.rowSelected : 'transparent'}
                  >
                    <td rowSpan={showClosed ? 2 : 1} style={{
                      ...c, ...gc, color: THEME.t1, fontWeight: 700, fontFamily: 'inherit',
                      borderLeft: 'none', textAlign: 'left',
                    }}>
                      {s.symbol}
                      {price && (
                        <div style={{ fontSize: 11, fontWeight: 700, fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace", color: dirColor, marginTop: 2 }}>
                          {price.bid}
                        </div>
                      )}
                    </td>
                    <td style={{ ...typeCell, ...gc, color: THEME.blue }}>O</td>
                    <td style={{ ...c, ...gc, color: THEME.t1, borderLeft: gridSecBorder }}>{fmt(s.clientBuyVolume)}</td>
                    <td style={{ ...c, ...gc, color: THEME.t1 }}>{fmt(s.clientSellVolume)}</td>
                    <td style={{ ...c, ...gc, color: nc(s.clientNetVolume), fontWeight: 600 }}>{fmt(s.clientNetVolume)}</td>
                    <td style={{ ...c, ...gc, color: nc(s.clientPnl) }}>{fp(s.clientPnl)}</td>
                    <td style={{ ...c, ...gc, color: THEME.t1, borderLeft: gridSecBorder }}>{fmt(s.coverageBuyVolume)}</td>
                    <td style={{ ...c, ...gc, color: THEME.t1 }}>{fmt(s.coverageSellVolume)}</td>
                    <td style={{ ...c, ...gc, color: nc(s.coverageNetVolume), fontWeight: 600 }}>{fmt(s.coverageNetVolume)}</td>
                    <td style={{ ...c, ...gc, color: nc(s.coveragePnl) }}>{fp(s.coveragePnl)}</td>
                    <td style={{ ...c, ...gc, borderLeft: gridSecBorder, color: nc(toCoverValue(s.clientNetVolume, s.coverageNetVolume)), fontWeight: 600 }}>
                      {fmtToCover(toCoverValue(s.clientNetVolume, s.coverageNetVolume))}
                    </td>
                    <td style={{ ...c, ...gc, color: nc(s.netPnl), fontWeight: 700 }}>{fp(s.netPnl)}</td>
                    <td style={{ ...c, ...gc, color: hedgeColor(hr), fontWeight: 600 }}>{hr.toFixed(0)}%</td>
                  </tr>
                  {/* CLOSED row */}
                  {showClosed && (
                  <tr
                    onClick={() => onSelect(s.symbol)}
                    style={{
                      cursor: 'pointer',
                      background: s.symbol === selectedSymbol ? THEME.rowSelected : THEME.rowAlt,
                      borderBottom: gridBorder,
                    }}
                  >
                    <td style={{ ...typeCell, ...gc, color: THEME.t3 }}>C</td>
                    {/* Clients closed */}
                    <td style={{ ...c, ...gc, color: THEME.t1, borderLeft: gridSecBorder }}>{bb ? fmt(bb.buyVolume) : ''}</td>
                    <td style={{ ...c, ...gc, color: THEME.t1 }}>{bb ? fmt(bb.sellVolume) : ''}</td>
                    <td style={{ ...c, ...gc, color: THEME.t1 }}>{bb ? fmt(bb.totalVolume) : ''}</td>
                    <td style={{ ...c, ...gc, color: bb ? nc(bb.netPnL) : THEME.t3, fontWeight: 600 }}>
                      {bb ? fp(bb.netPnL) : ''}
                    </td>
                    {/* Coverage closed */}
                    <td style={{ ...c, ...gc, color: THEME.t1, borderLeft: gridSecBorder }}>{cv ? fmt(cv.buyVolume) : ''}</td>
                    <td style={{ ...c, ...gc, color: THEME.t1 }}>{cv ? fmt(cv.sellVolume) : ''}</td>
                    <td style={{ ...c, ...gc, color: THEME.t1 }}>{cv ? fmt(cv.totalVolume) : ''}</td>
                    <td style={{ ...c, ...gc, color: cv ? nc(cv.netPnL) : THEME.t3, fontWeight: 600 }}>
                      {cv ? fp(cv.netPnL) : ''}
                    </td>
                    {/* Summary closed */}
                    <td style={{ ...c, ...gc, borderLeft: gridSecBorder }}></td>
                    <td style={{ ...c, ...gc, color: nc((cv?.netPnL ?? 0) - (bb?.netPnL ?? 0)), fontWeight: 600 }}>
                      {(bb || cv) ? fp((cv?.netPnL ?? 0) - (bb?.netPnL ?? 0)) : ''}
                    </td>
                    <td style={{ ...c, ...gc }}></td>
                  </tr>
                  )}
                </React.Fragment>
              );
            })}
          </tbody>
          <tfoot>
            {(() => {
              // Aggregate open (from symbols) + closed (from bb/cv maps keyed by canonical).
              const sumOpen = symbols.reduce((a, s) => ({
                cliBuy:  a.cliBuy  + s.clientBuyVolume,
                cliSell: a.cliSell + s.clientSellVolume,
                cliNet:  a.cliNet  + s.clientNetVolume,
                cliPnl:  a.cliPnl  + s.clientPnl,
                covBuy:  a.covBuy  + s.coverageBuyVolume,
                covSell: a.covSell + s.coverageSellVolume,
                covNet:  a.covNet  + s.coverageNetVolume,
                covPnl:  a.covPnl  + s.coveragePnl,
                netPnl:  a.netPnl  + s.netPnl,
              }), { cliBuy:0, cliSell:0, cliNet:0, cliPnl:0, covBuy:0, covSell:0, covNet:0, covPnl:0, netPnl:0 });

              const closedBB = Object.values(bbClosedMap).reduce((a, r) => ({
                buy: a.buy + r.buyVolume, sell: a.sell + r.sellVolume, total: a.total + r.totalVolume, pnl: a.pnl + r.netPnL,
              }), { buy:0, sell:0, total:0, pnl:0 });
              const closedCV = Object.values(covClosedMap).reduce((a, r) => ({
                buy: a.buy + r.buyVolume, sell: a.sell + r.sellVolume, total: a.total + r.totalVolume, pnl: a.pnl + r.netPnL,
              }), { buy:0, sell:0, total:0, pnl:0 });

              const openToCover = toCoverValue(sumOpen.cliNet, sumOpen.covNet);
              return (
                <>
                  {/* OPEN totals */}
                  <tr style={{ background: THEME.bg3, borderTop: `2px solid ${THEME.border}` }}>
                    <td style={{ ...c, ...gc, fontFamily: 'inherit', color: THEME.t2, fontWeight: 700, borderLeft: 'none', textAlign: 'left' }} rowSpan={showClosed ? 2 : 1}>TOTAL</td>
                    <td style={{ ...typeCell, ...gc, color: THEME.blue }}>O</td>
                    <td style={{ ...c, ...gc, borderLeft: gridSecBorder, color: THEME.t1 }}>{fmt(sumOpen.cliBuy)}</td>
                    <td style={{ ...c, ...gc, color: THEME.t1 }}>{fmt(sumOpen.cliSell)}</td>
                    <td style={{ ...c, ...gc, color: nc(sumOpen.cliNet), fontWeight: 600 }}>{fmt(sumOpen.cliNet)}</td>
                    <td style={{ ...c, ...gc, color: nc(sumOpen.cliPnl), fontWeight: 600 }}>{fp(sumOpen.cliPnl)}</td>
                    <td style={{ ...c, ...gc, borderLeft: gridSecBorder, color: THEME.t1 }}>{fmt(sumOpen.covBuy)}</td>
                    <td style={{ ...c, ...gc, color: THEME.t1 }}>{fmt(sumOpen.covSell)}</td>
                    <td style={{ ...c, ...gc, color: nc(sumOpen.covNet), fontWeight: 600 }}>{fmt(sumOpen.covNet)}</td>
                    <td style={{ ...c, ...gc, color: nc(sumOpen.covPnl), fontWeight: 600 }}>{fp(sumOpen.covPnl)}</td>
                    <td style={{ ...c, ...gc, borderLeft: gridSecBorder, color: nc(openToCover), fontWeight: 600 }}>{fmtToCover(openToCover)}</td>
                    <td style={{ ...c, ...gc, color: nc(sumOpen.netPnl), fontWeight: 700 }}>{fp(sumOpen.netPnl)}</td>
                    <td style={{ ...c, ...gc }}></td>
                  </tr>
                  {showClosed && (
                    <tr style={{ background: THEME.rowAlt }}>
                      <td style={{ ...typeCell, ...gc, color: THEME.t3 }}>C</td>
                      <td style={{ ...c, ...gc, borderLeft: gridSecBorder, color: THEME.t1 }}>{fmt(closedBB.buy)}</td>
                      <td style={{ ...c, ...gc, color: THEME.t1 }}>{fmt(closedBB.sell)}</td>
                      <td style={{ ...c, ...gc, color: THEME.t1 }}>{fmt(closedBB.total)}</td>
                      <td style={{ ...c, ...gc, color: nc(closedBB.pnl), fontWeight: 600 }}>{fp(closedBB.pnl)}</td>
                      <td style={{ ...c, ...gc, borderLeft: gridSecBorder, color: THEME.t1 }}>{fmt(closedCV.buy)}</td>
                      <td style={{ ...c, ...gc, color: THEME.t1 }}>{fmt(closedCV.sell)}</td>
                      <td style={{ ...c, ...gc, color: THEME.t1 }}>{fmt(closedCV.total)}</td>
                      <td style={{ ...c, ...gc, color: nc(closedCV.pnl), fontWeight: 600 }}>{fp(closedCV.pnl)}</td>
                      <td style={{ ...c, ...gc, borderLeft: gridSecBorder }}></td>
                      <td style={{ ...c, ...gc, color: nc(closedCV.pnl - closedBB.pnl), fontWeight: 700 }}>{fp(closedCV.pnl - closedBB.pnl)}</td>
                      <td style={{ ...c, ...gc }}></td>
                    </tr>
                  )}
                </>
              );
            })()}
          </tfoot>
        </table>
      </div>
    </div>
  );
}
