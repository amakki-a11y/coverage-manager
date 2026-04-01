import React, { useState, useEffect } from 'react';
import { THEME } from '../theme';
import type { ExposureSummary } from '../types';

interface ExposureTableProps {
  summaries: ExposureSummary[];
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

const secBorder = `2px solid rgba(255,255,255,0.12)`;
const rowBorder = `1px solid ${THEME.border}`;

const c: React.CSSProperties = {
  padding: '6px 10px',
  fontFamily: 'monospace',
  fontSize: 13,
  textAlign: 'right',
  whiteSpace: 'nowrap',
};

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

function todayStr() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

export function ExposureTable({ summaries }: ExposureTableProps) {
  const [bbClosedMap, setBbClosedMap] = useState<Record<string, ClosedSymbol>>({});
  const [covClosedMap, setCovClosedMap] = useState<Record<string, ClosedSymbol>>({});

  useEffect(() => {
    const today = todayStr();
    const fetchClosed = async () => {
      try {
        const [bbRes, covRes, mapRes] = await Promise.all([
          fetch('http://localhost:5000/api/exposure/pnl'),
          fetch(`http://localhost:8100/deals?from=${today}&to=${today}`),
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
  }, []);

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
    textAlign: 'left',
    fontFamily: 'inherit',
    fontSize: 10,
    fontWeight: 600,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    width: 50,
    padding: '6px 6px',
  };

  return (
    <div style={{ overflow: 'auto', flex: 1 }}>
      <table style={{ width: '100%', borderCollapse: 'collapse' }}>
        <thead>
          {/* Group headers */}
          <tr>
            <th style={{ ...hdr, width: 80, textAlign: 'left', borderBottom: 'none', top: 0, zIndex: 2 }} rowSpan={2}></th>
            <th style={{ ...hdr, width: 50, borderBottom: 'none', top: 0, zIndex: 2 }} rowSpan={2}></th>
            <th style={{ ...hdr, textAlign: 'center', color: THEME.blue, borderLeft: secBorder, borderBottom: 'none', fontSize: 11, fontWeight: 700, letterSpacing: 1, top: 0, zIndex: 2 }} colSpan={4}>Clients</th>
            <th style={{ ...hdr, textAlign: 'center', color: THEME.teal, borderLeft: secBorder, borderBottom: 'none', fontSize: 11, fontWeight: 700, letterSpacing: 1, top: 0, zIndex: 2 }} colSpan={4}>Coverage</th>
            <th style={{ ...hdr, textAlign: 'center', color: THEME.t2, borderLeft: secBorder, borderBottom: 'none', fontSize: 11, fontWeight: 700, letterSpacing: 1, top: 0, zIndex: 2 }} colSpan={4}>Summary</th>
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
            <th style={{ ...hdr, borderLeft: secBorder, top: 26 }}>Net Exp</th>
            <th style={{ ...hdr, top: 26 }}>To Cover</th>
            <th style={{ ...hdr, top: 26 }}>Net P&L</th>
            <th style={{ ...hdr, top: 26 }}>Hedge</th>
          </tr>
        </thead>
        <tbody>
          {summaries.map((s) => {
            const hr = s.hedgeRatio ?? 0;
            const bb = bbClosedMap[s.canonicalSymbol];
            const cv = covClosedMap[s.canonicalSymbol];
            const hasClosed = bb || cv;
            const hedgeBg = hr >= 80 ? 'transparent' : hr >= 50 ? 'rgba(255,186,66,0.05)' : 'rgba(255,82,82,0.06)';

            return (
              <React.Fragment key={s.canonicalSymbol}>
                {/* OPEN row */}
                <tr style={{ background: hedgeBg, borderTop: rowBorder }}>
                  <td rowSpan={hasClosed ? 2 : 1} style={{
                    ...c, textAlign: 'left', color: THEME.t1, fontWeight: 700, fontFamily: 'inherit', fontSize: 13,
                    verticalAlign: 'middle', borderBottom: rowBorder,
                  }}>
                    {s.canonicalSymbol}
                  </td>
                  <td style={{ ...typeCell, color: THEME.blue }}>open</td>
                  <td style={{ ...c, color: THEME.green, borderLeft: secBorder }}>{fmt(s.bBookBuyVolume)}</td>
                  <td style={{ ...c, color: THEME.red }}>{fmt(s.bBookSellVolume)}</td>
                  <td style={{ ...c, color: pc(s.bBookNetVolume), fontWeight: 600 }}>{fmt(s.bBookNetVolume)}</td>
                  <td style={{ ...c, color: pc(s.bBookPnL) }}>{fp(s.bBookPnL)}</td>
                  <td style={{ ...c, color: THEME.green, borderLeft: secBorder }}>{fmt(s.coverageBuyVolume)}</td>
                  <td style={{ ...c, color: THEME.red }}>{fmt(s.coverageSellVolume)}</td>
                  <td style={{ ...c, color: pc(s.coverageNetVolume), fontWeight: 600 }}>{fmt(s.coverageNetVolume)}</td>
                  <td style={{ ...c, color: pc(s.coveragePnL) }}>{fp(s.coveragePnL)}</td>
                  <td style={{ ...c, borderLeft: secBorder, color: pc(s.netVolume), fontWeight: 700 }}>{fmt(s.netVolume)}</td>
                  <td style={{ ...c, color: THEME.amber, fontWeight: 600 }}>{calcToCover(s.bBookNetVolume, s.coverageNetVolume)}</td>
                  <td style={{ ...c, color: pc(s.netPnL), fontWeight: 700 }}>{fp(s.netPnL)}</td>
                  <td style={{ ...c, color: hedgeColor(hr), fontWeight: 600 }}>{hr.toFixed(0)}%</td>
                </tr>
                {/* CLOSED row */}
                {hasClosed && (
                  <tr style={{ background: 'rgba(255,255,255,0.015)', borderBottom: rowBorder }}>
                    <td style={{ ...typeCell, color: THEME.t3 }}>closed</td>
                    {/* Clients closed */}
                    <td style={{ ...c, color: THEME.green, fontSize: 11, borderLeft: secBorder }}>{bb ? fmt(bb.buyVolume) : ''}</td>
                    <td style={{ ...c, color: THEME.red, fontSize: 11 }}>{bb ? fmt(bb.sellVolume) : ''}</td>
                    <td style={{ ...c, color: THEME.t3, fontSize: 11 }}>{bb ? fmt(bb.totalVolume) : ''}</td>
                    <td style={{ ...c, color: bb ? pc(bb.netPnL) : THEME.t3, fontSize: 11, fontWeight: 600 }}>
                      {bb ? fp(bb.netPnL) : ''}
                    </td>
                    {/* Coverage closed */}
                    <td style={{ ...c, color: THEME.green, fontSize: 11, borderLeft: secBorder }}>{cv ? fmt(cv.buyVolume) : ''}</td>
                    <td style={{ ...c, color: THEME.red, fontSize: 11 }}>{cv ? fmt(cv.sellVolume) : ''}</td>
                    <td style={{ ...c, color: THEME.t3, fontSize: 11 }}>{cv ? fmt(cv.totalVolume) : ''}</td>
                    <td style={{ ...c, color: cv ? pc(cv.netPnL) : THEME.t3, fontSize: 11, fontWeight: 600 }}>
                      {cv ? fp(cv.netPnL) : ''}
                    </td>
                    {/* Summary closed */}
                    <td style={{ ...c, borderLeft: secBorder }} colSpan={3}></td>
                    <td style={{ ...c, color: pc((bb?.netPnL ?? 0) + (cv?.netPnL ?? 0)), fontSize: 11, fontWeight: 600 }}>
                      {fp((bb?.netPnL ?? 0) + (cv?.netPnL ?? 0))}
                    </td>
                  </tr>
                )}
              </React.Fragment>
            );
          })}
        </tbody>
        <tfoot>
          {/* Closed totals */}
          <tr style={{ background: 'rgba(255,255,255,0.02)', borderTop: `2px solid ${THEME.border}` }}>
            <td style={{ ...c, textAlign: 'left', fontFamily: 'inherit', fontSize: 11, color: THEME.t3, fontWeight: 600 }}>TOTAL</td>
            <td style={{ ...typeCell, color: THEME.t3 }}>closed</td>
            <td style={{ ...c, borderLeft: secBorder }} colSpan={3}></td>
            <td style={{ ...c, color: pc(closedBBPnL), fontSize: 11, fontWeight: 600 }}>{fp(closedBBPnL)}</td>
            <td style={{ ...c, borderLeft: secBorder }} colSpan={3}></td>
            <td style={{ ...c, color: pc(closedCovPnL), fontSize: 11, fontWeight: 600 }}>{fp(closedCovPnL)}</td>
            <td style={{ ...c, borderLeft: secBorder }} colSpan={3}></td>
            <td style={{ ...c, color: pc(closedBBPnL + closedCovPnL), fontSize: 11, fontWeight: 700 }}>{fp(closedBBPnL + closedCovPnL)}</td>
          </tr>
          {/* Open totals */}
          <tr style={{ background: THEME.bg3 }}>
            <td style={{ ...c, textAlign: 'left', fontFamily: 'inherit', fontSize: 11, color: THEME.t2, fontWeight: 600 }}>TOTAL</td>
            <td style={{ ...typeCell, color: THEME.blue }}>open</td>
            <td style={{ ...c, color: THEME.green, borderLeft: secBorder }}>{fmt(openTotals.bbBuy)}</td>
            <td style={{ ...c, color: THEME.red }}>{fmt(openTotals.bbSell)}</td>
            <td style={{ ...c, color: pc(openTotals.bbNet), fontWeight: 600 }}>{fmt(openTotals.bbNet)}</td>
            <td style={{ ...c, color: pc(openTotals.bbPnL) }}>{fp(openTotals.bbPnL)}</td>
            <td style={{ ...c, color: THEME.green, borderLeft: secBorder }}>{fmt(openTotals.covBuy)}</td>
            <td style={{ ...c, color: THEME.red }}>{fmt(openTotals.covSell)}</td>
            <td style={{ ...c, color: pc(openTotals.covNet), fontWeight: 600 }}>{fmt(openTotals.covNet)}</td>
            <td style={{ ...c, color: pc(openTotals.covPnL) }}>{fp(openTotals.covPnL)}</td>
            <td style={{ ...c, borderLeft: secBorder, color: pc(openTotals.netVol), fontWeight: 700 }}>{fmt(openTotals.netVol)}</td>
            <td style={{ ...c, color: THEME.amber, fontWeight: 600 }}>{calcToCover(openTotals.bbNet, openTotals.covNet)}</td>
            <td style={{ ...c, color: pc(openTotals.netPnL), fontWeight: 700 }}>{fp(openTotals.netPnL)}</td>
            <td style={c} />
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

function calcToCover(bbNet: number | undefined, covNet: number | undefined): string {
  const bb = bbNet ?? 0;
  const cov = covNet ?? 0;
  const remaining = bb - cov;
  if (Math.abs(remaining) < 0.005) return '—';
  return (remaining < 0 ? 'SELL ' : 'BUY ') + Math.abs(remaining).toFixed(2);
}

function hedgeColor(r: number): string {
  if (r >= 80) return THEME.green;
  if (r >= 50) return THEME.amber;
  return THEME.red;
}
