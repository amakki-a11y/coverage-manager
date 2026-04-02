import React, { useState, useEffect, useRef } from 'react';
import { THEME } from '../../../theme';
import type { SymbolExposure } from '../../../types/compare';
import type { PriceQuote, ExposureSummary } from '../../../types';

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

function todayStr() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function hedgeColor(pct: number): string {
  if (pct >= 80) return THEME.green;
  if (pct >= 50) return THEME.amber;
  return THEME.red;
}

function pc(v: number): string {
  return v >= 0 ? THEME.green : THEME.red;
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

function toCoverColor(v: number): string {
  if (v > 0.005) return THEME.green;
  if (v < -0.005) return THEME.red;
  return THEME.t3;
}

function fmtToCover(v: number): string {
  if (Math.abs(v) < 0.005) return '0.00';
  return `${v < 0 ? '-' : ''}${Math.abs(v).toFixed(2)}`;
}

const cellBase: React.CSSProperties = {
  padding: '6px 10px',
  fontFamily: 'monospace',
  fontSize: 12,
  textAlign: 'center',
  verticalAlign: 'middle',
  whiteSpace: 'nowrap',
};

export function ExpandedTable({ symbols, selectedSymbol, onSelect }: ExpandedTableProps) {
  const [closedFrom, setClosedFrom] = useState(todayStr);
  const [closedTo, setClosedTo] = useState(todayStr);
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
            const canonical = s.symbol.replace(/[-.]$/, '');
            bbMap[s.symbol] = s;
            bbMap[s.symbol.toUpperCase()] = s;
            bbMap[canonical] = s;
            bbMap[canonical.toUpperCase()] = s;
            bbSymbols.push(s.symbol);
            bbSymbols.push(canonical);
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

  // Compute totals
  const totalBBPnL = symbols.reduce((a, s) => a + s.clientPnl, 0);
  const totalCovPnL = symbols.reduce((a, s) => a + s.coveragePnl, 0);
  const closedBBPnL = Object.values(bbClosedMap).reduce((a, s) => a + s.netPnL, 0);
  const closedCovPnL = Object.values(covClosedMap).reduce((a, s) => a + s.netPnL, 0);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', flex: 1, overflow: 'hidden' }}>
      {/* Date range picker */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 8, padding: '6px 12px',
        borderBottom: `1px solid ${THEME.border}`, flexShrink: 0,
      }}>
        <span style={{ color: THEME.t3, fontSize: 11, fontWeight: 600 }}>Closed:</span>
        <input
          type="date"
          value={closedFrom}
          onChange={e => setClosedFrom(e.target.value)}
          style={{ fontSize: 11, fontFamily: 'monospace', background: THEME.bg3, color: THEME.t1, border: `1px solid ${THEME.border}`, borderRadius: 4, padding: '2px 6px' }}
        />
        <span style={{ color: THEME.t3, fontSize: 11 }}>to</span>
        <input
          type="date"
          value={closedTo}
          onChange={e => setClosedTo(e.target.value)}
          style={{ fontSize: 11, fontFamily: 'monospace', background: THEME.bg3, color: THEME.t1, border: `1px solid ${THEME.border}`, borderRadius: 4, padding: '2px 6px' }}
        />
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
              const bb = bbClosedMap[s.symbol] || bbClosedMap[s.symbol.toUpperCase()];
              const cv = covClosedMap[s.symbol] || covClosedMap[s.symbol.toUpperCase()];
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
                      background: s.symbol === selectedSymbol ? 'rgba(20,184,166,0.06)' : 'transparent',
                    }}
                    onMouseEnter={e => e.currentTarget.style.background = 'rgba(255,255,255,0.02)'}
                    onMouseLeave={e => e.currentTarget.style.background = s.symbol === selectedSymbol ? 'rgba(20,184,166,0.06)' : 'transparent'}
                  >
                    <td rowSpan={2} style={{
                      ...c, ...gc, color: THEME.t1, fontWeight: 700, fontFamily: 'inherit',
                      borderLeft: 'none', textAlign: 'left',
                    }}>
                      {s.symbol}
                      {price && (
                        <div style={{ fontSize: 11, fontWeight: 700, fontFamily: 'monospace', color: dirColor, marginTop: 2 }}>
                          {price.bid}
                        </div>
                      )}
                    </td>
                    <td style={{ ...typeCell, ...gc, color: THEME.blue }}>O</td>
                    <td style={{ ...c, ...gc, color: THEME.green, borderLeft: gridSecBorder }}>{fmt(s.clientBuyVolume)}</td>
                    <td style={{ ...c, ...gc, color: THEME.red }}>{fmt(s.clientSellVolume)}</td>
                    <td style={{ ...c, ...gc, color: pc(s.clientNetVolume), fontWeight: 600 }}>{fmt(s.clientNetVolume)}</td>
                    <td style={{ ...c, ...gc, color: pc(s.clientPnl) }}>{fp(s.clientPnl)}</td>
                    <td style={{ ...c, ...gc, color: THEME.green, borderLeft: gridSecBorder }}>{fmt(s.coverageBuyVolume)}</td>
                    <td style={{ ...c, ...gc, color: THEME.red }}>{fmt(s.coverageSellVolume)}</td>
                    <td style={{ ...c, ...gc, color: pc(s.coverageNetVolume), fontWeight: 600 }}>{fmt(s.coverageNetVolume)}</td>
                    <td style={{ ...c, ...gc, color: pc(s.coveragePnl) }}>{fp(s.coveragePnl)}</td>
                    <td style={{ ...c, ...gc, borderLeft: gridSecBorder, color: toCoverColor(toCoverValue(s.clientNetVolume, s.coverageNetVolume)), fontWeight: 600 }}>
                      {fmtToCover(toCoverValue(s.clientNetVolume, s.coverageNetVolume))}
                    </td>
                    <td style={{ ...c, ...gc, color: pc(s.netPnl), fontWeight: 700 }}>{fp(s.netPnl)}</td>
                    <td style={{ ...c, ...gc, color: hedgeColor(hr), fontWeight: 600 }}>{hr.toFixed(0)}%</td>
                  </tr>
                  {/* CLOSED row */}
                  <tr
                    onClick={() => onSelect(s.symbol)}
                    style={{
                      cursor: 'pointer',
                      background: s.symbol === selectedSymbol ? 'rgba(20,184,166,0.04)' : 'rgba(255,255,255,0.015)',
                      borderBottom: gridBorder,
                    }}
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
                    <td style={{ ...c, ...gc, color: pc((bb?.netPnL ?? 0) + (cv?.netPnL ?? 0)), fontWeight: 600 }}>
                      {(bb || cv) ? fp((bb?.netPnL ?? 0) + (cv?.netPnL ?? 0)) : ''}
                    </td>
                    <td style={{ ...c, ...gc }}></td>
                  </tr>
                </React.Fragment>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
