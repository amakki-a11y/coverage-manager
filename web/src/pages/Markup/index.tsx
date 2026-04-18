import React, { useState, useCallback } from 'react';
import { THEME } from '../../theme';
import { formatBeirutTime } from '../../utils/time';

interface BookSide {
  deals: number;
  buyVol: number;
  sellVol: number;
  avgBuy: number;
  avgSell: number;
  profit: number;
}

interface MarkupRow {
  symbol: string;
  client: BookSide;
  coverage: BookSide;
  priceEdgeBuy: number;
  priceEdgeSell: number;
  markup: number;
  hedgeRatioBuy: number;
  hedgeRatioSell: number;
}

interface CoverageMatch {
  ticket: number;
  type: string;
  entry: string;
  volume: number;
  price: number;
  time: string;
  timeDiffMs: number;
}

interface SampleMatch {
  clientDealId: number;
  clientLogin: number;
  clientDirection: string;
  clientVolume: number;
  clientPrice: number;
  clientTime: string;
  symbol: string;
  coverageMatches: CoverageMatch[];
}

interface MarkupResponse {
  from: string;
  to: string;
  summary: {
    totalMarkup: number;
    clientDealsTotal: number;
    coverageDealsTotal: number;
    symbolsCount: number;
    clientProfitTotal: number;
    coverageProfitTotal: number;
  };
  symbols: MarkupRow[];
  sampleMatches: SampleMatch[];
  elapsed: string;
}

const todayStr = new Date().toISOString().slice(0, 10);

function fmtMoney(v: number): string {
  const abs = Math.abs(v);
  const sign = v < 0 ? '-' : '';
  if (abs >= 1_000_000) return `${sign}$${(abs / 1_000_000).toFixed(2)}M`;
  if (abs >= 1_000) return `${sign}$${(abs / 1_000).toFixed(1)}K`;
  return `${sign}$${abs.toFixed(2)}`;
}

function fmtVol(v: number): string {
  if (v === 0) return '—';
  return v.toFixed(2);
}

function fmtPrice(v: number): string {
  if (v === 0) return '—';
  return v.toFixed(5);
}

function pnlColor(v: number): string {
  return v > 0 ? THEME.green : v < 0 ? THEME.red : THEME.t3;
}

// Convert price edge to pips based on typical pip size for the price level
function toPips(edge: number, price: number): number {
  if (price === 0) return 0;
  // Determine pip size from price magnitude
  let pipSize: number;
  if (price >= 1000) pipSize = 0.1;       // Indices (US30, UT100, XAUUSD)
  else if (price >= 10) pipSize = 0.01;   // Oil, Silver
  else if (price >= 1) pipSize = 0.001;   // XRPUSD
  else pipSize = 0.0001;                  // Forex (EURUSD, GBPUSD)
  return edge / pipSize;
}

function hedgeColor(pct: number): string {
  if (pct >= 90) return THEME.green;
  if (pct >= 50) return THEME.amber;
  return THEME.red;
}

export function MarkupPanel() {
  const [from, setFrom] = useState(todayStr);
  const [to, setTo] = useState(todayStr);
  const [data, setData] = useState<MarkupResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const run = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(`http://localhost:5000/api/markup/match?from=${from}&to=${to}`);
      if (!res.ok) {
        const err = await res.json().catch(() => ({ error: `HTTP ${res.status}` }));
        setError(err.error || 'Failed to load');
        return;
      }
      const json = await res.json();
      setData(json);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Request failed');
    } finally {
      setLoading(false);
    }
  }, [from, to]);

  return (
    <div style={{ padding: 20, overflow: 'auto', flex: 1 }}>
      <div style={{ marginBottom: 20 }}>
        <h2 style={{ margin: '0 0 4px', color: THEME.t1, fontSize: 18, fontWeight: 600 }}>
          Mark-up Report
        </h2>
        <div style={{ color: THEME.t3, fontSize: 12 }}>
          Broker profit from the spread between client execution prices and LP fill prices
        </div>
      </div>

      {/* Controls */}
      <div style={{
        display: 'flex',
        gap: 12,
        alignItems: 'flex-end',
        padding: 16,
        background: THEME.card,
        borderRadius: 8,
        border: `1px solid ${THEME.border}`,
        marginBottom: 16,
      }}>
        <div>
          <label style={{ display: 'block', fontSize: 10, color: THEME.t3, textTransform: 'uppercase', marginBottom: 4 }}>
            From
          </label>
          <input
            type="date"
            value={from}
            onChange={(e) => setFrom(e.target.value)}
            style={{
              background: THEME.bg,
              border: `1px solid ${THEME.border}`,
              color: THEME.t1,
              padding: '6px 10px',
              borderRadius: 4,
              fontSize: 12,
              fontFamily: 'monospace',
            }}
          />
        </div>
        <div>
          <label style={{ display: 'block', fontSize: 10, color: THEME.t3, textTransform: 'uppercase', marginBottom: 4 }}>
            To
          </label>
          <input
            type="date"
            value={to}
            onChange={(e) => setTo(e.target.value)}
            style={{
              background: THEME.bg,
              border: `1px solid ${THEME.border}`,
              color: THEME.t1,
              padding: '6px 10px',
              borderRadius: 4,
              fontSize: 12,
              fontFamily: 'monospace',
            }}
          />
        </div>
        <button
          onClick={run}
          disabled={loading}
          style={{
            padding: '8px 24px',
            background: loading ? THEME.t3 : THEME.blue,
            color: '#fff',
            border: 'none',
            borderRadius: 4,
            fontSize: 12,
            fontWeight: 600,
            cursor: loading ? 'wait' : 'pointer',
          }}
        >
          {loading ? 'Loading…' : 'Run Report'}
        </button>
        {data && (
          <div style={{ marginLeft: 'auto', fontSize: 11, color: THEME.t3 }}>
            Elapsed: {data.elapsed}
          </div>
        )}
      </div>

      {error && (
        <div style={{
          padding: 12,
          background: 'rgba(255,82,82,0.08)',
          border: `1px solid ${THEME.red}`,
          borderRadius: 4,
          color: THEME.red,
          fontSize: 12,
          marginBottom: 16,
        }}>
          {error}
        </div>
      )}

      {data && (
        <>
          {/* Summary cards */}
          <div style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(4, 1fr)',
            gap: 12,
            marginBottom: 16,
          }}>
            <SummaryCard
              label="Total Markup"
              value={fmtMoney(data.summary.totalMarkup)}
              color={pnlColor(data.summary.totalMarkup)}
              accent
            />
            <SummaryCard
              label="Client P&L"
              value={fmtMoney(data.summary.clientProfitTotal)}
              color={pnlColor(data.summary.clientProfitTotal)}
            />
            <SummaryCard
              label="Coverage P&L"
              value={fmtMoney(data.summary.coverageProfitTotal)}
              color={pnlColor(data.summary.coverageProfitTotal)}
            />
            <SummaryCard
              label="Deals (Cli / Cov)"
              value={`${data.summary.clientDealsTotal.toLocaleString()} / ${data.summary.coverageDealsTotal.toLocaleString()}`}
              color={THEME.t1}
            />
          </div>

          {/* Per-symbol table */}
          <div style={{
            background: THEME.card,
            borderRadius: 8,
            border: `1px solid ${THEME.border}`,
            overflow: 'hidden',
          }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
              <thead>
                <tr style={{ background: THEME.bg3, borderBottom: `1px solid ${THEME.border}` }}>
                  <th style={thStyle}>Symbol</th>
                  <th style={thStyle} colSpan={4}>Client</th>
                  <th style={thStyle} colSpan={4}>Coverage</th>
                  <th style={thStyle}>Hedge B/S</th>
                  <th style={thStyle}>Edge B</th>
                  <th style={thStyle}>Edge S</th>
                  <th style={thStyle}>Markup</th>
                </tr>
                <tr style={{ background: THEME.bg2, borderBottom: `1px solid ${THEME.border}`, fontSize: 10 }}>
                  <th style={thSubStyle}></th>
                  <th style={thSubStyle}>Buy Vol</th>
                  <th style={thSubStyle}>Sell Vol</th>
                  <th style={thSubStyle}>Avg Buy</th>
                  <th style={thSubStyle}>Avg Sell</th>
                  <th style={thSubStyle}>Buy Vol</th>
                  <th style={thSubStyle}>Sell Vol</th>
                  <th style={thSubStyle}>Avg Buy</th>
                  <th style={thSubStyle}>Avg Sell</th>
                  <th style={thSubStyle}></th>
                  <th style={thSubStyle}></th>
                  <th style={thSubStyle}></th>
                  <th style={thSubStyle}></th>
                </tr>
              </thead>
              <tbody>
                {data.symbols.map((s) => (
                  <tr key={s.symbol} style={{ borderBottom: `1px solid ${THEME.border}` }}>
                    <td style={{ ...tdStyle, color: THEME.t1, fontWeight: 600 }}>{s.symbol}</td>
                    <td style={{ ...tdStyle, color: THEME.green }}>{fmtVol(s.client.buyVol)}</td>
                    <td style={{ ...tdStyle, color: THEME.red }}>{fmtVol(s.client.sellVol)}</td>
                    <td style={tdStyle}>{fmtPrice(s.client.avgBuy)}</td>
                    <td style={tdStyle}>{fmtPrice(s.client.avgSell)}</td>
                    <td style={{ ...tdStyle, color: THEME.green }}>{fmtVol(s.coverage.buyVol)}</td>
                    <td style={{ ...tdStyle, color: THEME.red }}>{fmtVol(s.coverage.sellVol)}</td>
                    <td style={tdStyle}>{fmtPrice(s.coverage.avgBuy)}</td>
                    <td style={tdStyle}>{fmtPrice(s.coverage.avgSell)}</td>
                    <td style={{ ...tdStyle, fontSize: 10 }}>
                      <span style={{ color: hedgeColor(s.hedgeRatioBuy) }}>{s.hedgeRatioBuy}%</span>
                      <span style={{ color: THEME.t3 }}> / </span>
                      <span style={{ color: hedgeColor(s.hedgeRatioSell) }}>{s.hedgeRatioSell}%</span>
                    </td>
                    <td style={{ ...tdStyle, color: pnlColor(s.priceEdgeBuy) }}>
                      {s.priceEdgeBuy !== 0 ? s.priceEdgeBuy.toFixed(5) : '—'}
                    </td>
                    <td style={{ ...tdStyle, color: pnlColor(s.priceEdgeSell) }}>
                      {s.priceEdgeSell !== 0 ? s.priceEdgeSell.toFixed(5) : '—'}
                    </td>
                    <td style={{ ...tdStyle, color: pnlColor(s.markup), fontWeight: 700 }}>
                      {fmtMoney(s.markup)}
                    </td>
                  </tr>
                ))}
                {data.symbols.length === 0 && (
                  <tr>
                    <td colSpan={13} style={{ padding: 24, textAlign: 'center', color: THEME.t3 }}>
                      No data for the selected range
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          <div style={{ marginTop: 12, fontSize: 11, color: THEME.t3, lineHeight: 1.6 }}>
            <strong>Markup</strong> = Coverage P&L - Client P&L (broker net profit after routing to LP).<br />
            <strong>Edge B/S</strong> = Price difference per lot between client and LP execution.<br />
            <strong>Hedge B/S</strong> = Coverage volume as % of client volume on each side.
          </div>

          {/* Sample Matched Pairs */}
          {data.sampleMatches && data.sampleMatches.length > 0 && (
            <div style={{ marginTop: 24 }}>
              <h3 style={{ color: THEME.t1, fontSize: 14, margin: '0 0 12px' }}>
                Sample Matched Pairs ({data.sampleMatches.length})
                <span style={{ color: THEME.t3, fontWeight: 400, fontSize: 11, marginLeft: 8 }}>
                  Client deal matched to coverage deals within 500ms
                </span>
              </h3>
              <div style={{
                background: THEME.card,
                borderRadius: 8,
                border: `1px solid ${THEME.border}`,
                overflow: 'hidden',
              }}>
                <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 11 }}>
                  <thead>
                    <tr style={{ background: THEME.bg3, borderBottom: `1px solid ${THEME.border}` }}>
                      <th style={thStyle}>Symbol</th>
                      <th style={thStyle}>Source</th>
                      <th style={thStyle}>Side</th>
                      <th style={thStyle}>Volume</th>
                      <th style={thStyle}>Price</th>
                      <th style={thStyle}>Time</th>
                      <th style={thStyle}>Time Diff</th>
                      <th style={thStyle}>Price Edge</th>
                      <th style={thStyle}>Pips</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.sampleMatches.map((m, i) => {
                      const covTotalVol = m.coverageMatches.reduce((s, c) => s + c.volume, 0);
                      const covAvgPrice = m.coverageMatches.length > 0
                        ? m.coverageMatches.reduce((s, c) => s + c.price * c.volume, 0) / covTotalVol
                        : 0;
                      const avgDiffMs = m.coverageMatches.length > 0
                        ? m.coverageMatches.reduce((s, c) => s + c.timeDiffMs, 0) / m.coverageMatches.length
                        : 0;
                      const priceEdge = m.clientDirection === 'BUY'
                        ? m.clientPrice - covAvgPrice
                        : covAvgPrice - m.clientPrice;
                      const bg = i % 2 === 0 ? 'transparent' : `${THEME.bg3}44`;
                      return (
                        <React.Fragment key={`${m.clientDealId}-${i}`}>
                          {/* Client row */}
                          <tr style={{ background: bg, borderTop: `2px solid ${THEME.border}` }}>
                            <td rowSpan={1 + m.coverageMatches.length} style={{
                              ...tdStyle, color: THEME.t1, fontWeight: 600, fontSize: 12,
                              verticalAlign: 'middle', borderRight: `1px solid ${THEME.border}`,
                            }}>
                              {m.symbol}
                            </td>
                            <td style={{ ...tdStyle, color: THEME.blue, fontWeight: 700, fontSize: 10 }}>CLIENT</td>
                            <td style={{
                              ...tdStyle,
                              color: m.clientDirection === 'BUY' ? THEME.green : THEME.red,
                              fontWeight: 600,
                            }}>
                              {m.clientDirection}
                            </td>
                            <td style={{ ...tdStyle, color: THEME.t1 }}>{m.clientVolume.toFixed(2)}</td>
                            <td style={{ ...tdStyle, fontWeight: 600 }}>{m.clientPrice.toFixed(5)}</td>
                            <td style={{ ...tdStyle, fontSize: 10 }}>{formatBeirutTime(m.clientTime)}</td>
                            <td style={tdStyle}></td>
                            <td rowSpan={1 + m.coverageMatches.length} style={{
                              ...tdStyle,
                              color: priceEdge > 0 ? THEME.green : priceEdge < 0 ? THEME.red : THEME.t3,
                              fontWeight: 700, fontSize: 13, verticalAlign: 'middle',
                              borderLeft: `1px solid ${THEME.border}`,
                            }}>
                              {priceEdge > 0 ? '+' : ''}{priceEdge.toFixed(5)}
                            </td>
                            <td rowSpan={1 + m.coverageMatches.length} style={{
                              ...tdStyle,
                              color: priceEdge > 0 ? THEME.green : priceEdge < 0 ? THEME.red : THEME.t3,
                              fontWeight: 700, fontSize: 13, verticalAlign: 'middle',
                            }}>
                              {(() => {
                                const pips = toPips(priceEdge, m.clientPrice);
                                return `${pips > 0 ? '+' : ''}${pips.toFixed(1)}`;
                              })()}
                            </td>
                          </tr>
                          {/* Coverage rows (one per fill) */}
                          {m.coverageMatches.map((c, j) => (
                            <tr key={`cov-${j}`} style={{
                              background: bg,
                              borderBottom: j === m.coverageMatches.length - 1 ? `1px solid ${THEME.border}` : 'none',
                            }}>
                              <td style={{ ...tdStyle, color: THEME.teal, fontWeight: 700, fontSize: 10 }}>
                                COV {c.entry}
                              </td>
                              <td style={{
                                ...tdStyle,
                                color: c.type === 'buy' ? THEME.green : THEME.red,
                                fontWeight: 600,
                              }}>
                                {c.type.toUpperCase()}
                              </td>
                              <td style={{ ...tdStyle, color: THEME.t1 }}>{c.volume.toFixed(2)}</td>
                              <td style={{ ...tdStyle, fontWeight: 600 }}>{c.price.toFixed(5)}</td>
                              <td style={{ ...tdStyle, fontSize: 10 }}>{formatBeirutTime(c.time)}</td>
                              <td style={{
                                ...tdStyle, fontSize: 10,
                                color: Math.abs(c.timeDiffMs) < 200 ? THEME.green : THEME.amber,
                              }}>
                                {c.timeDiffMs > 0 ? '+' : ''}{c.timeDiffMs}ms
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
          )}
        </>
      )}

      {!data && !loading && (
        <div style={{ padding: 40, textAlign: 'center', color: THEME.t3, fontSize: 13 }}>
          Select a date range and click <strong>Run Report</strong> to see mark-up earned.
        </div>
      )}
    </div>
  );
}

function SummaryCard({
  label,
  value,
  color,
  accent = false,
}: {
  label: string;
  value: string;
  color: string;
  accent?: boolean;
}) {
  return (
    <div style={{
      padding: 16,
      background: THEME.card,
      borderRadius: 8,
      border: accent ? `2px solid ${THEME.blue}` : `1px solid ${THEME.border}`,
    }}>
      <div style={{ fontSize: 10, color: THEME.t3, textTransform: 'uppercase', marginBottom: 4, letterSpacing: 0.5 }}>
        {label}
      </div>
      <div style={{ fontSize: 20, fontWeight: 700, color, fontFamily: 'monospace' }}>
        {value}
      </div>
    </div>
  );
}

const thStyle: React.CSSProperties = {
  padding: '8px 10px',
  textAlign: 'center',
  fontSize: 11,
  color: THEME.t2,
  fontWeight: 600,
  textTransform: 'uppercase',
  letterSpacing: 0.3,
};

const thSubStyle: React.CSSProperties = {
  padding: '4px 10px',
  textAlign: 'center',
  fontSize: 9,
  color: THEME.t3,
  fontWeight: 500,
  textTransform: 'uppercase',
};

const tdStyle: React.CSSProperties = {
  padding: '8px 10px',
  textAlign: 'center',
  fontFamily: 'monospace',
  color: THEME.t2,
};
