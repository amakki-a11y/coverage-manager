import { useState, useEffect, useRef, useCallback } from 'react';
import { THEME } from '../../../theme';

interface Account {
  login: number;
  name: string;
  group_name: string;
  leverage: number;
  balance: number;
  equity: number;
  margin: number;
  free_margin: number;
  currency: string;
  registration_time: string;
  last_trade_time: string;
  status: string;
  comment: string;
}

interface Position {
  symbol: string;
  direction: string;
  volumeLots: number;
  openPrice: number;
  currentPrice: number;
  profit: number;
  swap: number;
  openTime: string;
}

interface Deal {
  deal_id: number;
  symbol: string;
  direction: string;
  action: number;
  entry: number;
  volume: number;
  price: number;
  profit: number;
  commission: number;
  swap: number;
  fee: number;
  deal_time: string;
}

interface AccountModalProps {
  login: number;
  onClose: () => void;
}

function nc(v: number): string {
  return v >= 0 ? THEME.blue : THEME.red;
}

function fmtMoney(v: number): string {
  return v.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function fmtDate(s: string): string {
  const d = new Date(s);
  return d.toLocaleString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit' });
}

function fmtTime(s: string): string {
  const d = new Date(s);
  return d.toLocaleString('en-GB', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
}

function entryLabel(e: number): string {
  switch (e) {
    case 0: return 'IN';
    case 1: return 'OUT';
    case 2: return 'INOUT';
    case 3: return 'OUT_BY';
    default: return String(e);
  }
}

export function AccountModal({ login, onClose }: AccountModalProps) {
  const [account, setAccount] = useState<Account | null>(null);
  const [positions, setPositions] = useState<Position[]>([]);
  const [deals, setDeals] = useState<Deal[]>([]);
  const [tab, setTab] = useState<'positions' | 'history'>('positions');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchData = async () => {
      setLoading(true);
      try {
        const [accRes, posRes, dealRes] = await Promise.all([
          fetch('http://localhost:5000/api/accounts'),
          fetch('http://localhost:5000/api/exposure/positions'),
          fetch(`http://localhost:5000/api/accounts/deals?source=bbook&from=${todayStr()}&to=${tomorrowStr()}`),
        ]);

        if (accRes.ok) {
          const accData = await accRes.json();
          const accs = accData.accounts || [];
          const found = accs.find((a: Account) => a.login === login);
          if (found) setAccount(found);
        }

        if (posRes.ok) {
          const allPos: Position[] = await posRes.json();
          setPositions(allPos.filter((p: any) => p.login === login));
        }

        if (dealRes.ok) {
          const dealData = await dealRes.json();
          const allDeals: Deal[] = dealData.deals || dealData || [];
          setDeals(
            allDeals
              .filter((d: any) => d.login === login)
              .sort((a, b) => new Date(b.deal_time).getTime() - new Date(a.deal_time).getTime())
              .slice(0, 200)
          );
        }
      } catch {}
      setLoading(false);
    };
    fetchData();
  }, [login]);

  const hdr: React.CSSProperties = {
    padding: '4px 8px',
    fontSize: 9,
    fontWeight: 600,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    color: THEME.t3,
    textAlign: 'right',
    borderBottom: `1px solid ${THEME.border}`,
    position: 'sticky',
    top: 0,
    background: THEME.bg2,
    zIndex: 1,
  };

  const cell: React.CSSProperties = {
    padding: '3px 8px',
    fontFamily: 'monospace',
    fontSize: 11,
    textAlign: 'right',
    borderBottom: `1px solid ${THEME.border}`,
    whiteSpace: 'nowrap',
  };

  const totalPnl = positions.reduce((a, p) => a + p.profit + (p.swap || 0), 0);

  return (
    <div
      onClick={onClose}
      style={{
        position: 'fixed',
        inset: 0,
        background: 'rgba(0,0,0,0.6)',
        zIndex: 1000,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      <div
        onClick={e => e.stopPropagation()}
        style={{
          background: THEME.bg2,
          border: `1px solid ${THEME.border}`,
          borderRadius: 8,
          width: '85vw',
          maxWidth: 900,
          maxHeight: '85vh',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
          boxShadow: '0 20px 60px rgba(0,0,0,0.5)',
        }}
      >
        {/* Title bar */}
        <div style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '10px 16px',
          borderBottom: `1px solid ${THEME.border}`,
          flexShrink: 0,
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
            <span style={{ fontFamily: 'monospace', fontSize: 16, fontWeight: 700, color: THEME.teal }}>{login}</span>
            {account && (
              <span style={{ fontSize: 12, color: THEME.t2, fontWeight: 500 }}>{account.name}</span>
            )}
            {account && (
              <span style={{
                fontSize: 9, fontWeight: 600, padding: '2px 8px', borderRadius: 3,
                background: `${THEME.teal}22`, color: THEME.teal, textTransform: 'uppercase',
              }}>
                {account.group_name}
              </span>
            )}
          </div>
          <button
            onClick={onClose}
            style={{
              background: 'transparent',
              border: 'none',
              color: THEME.t3,
              fontSize: 18,
              cursor: 'pointer',
              padding: '0 4px',
              lineHeight: 1,
            }}
          >
            &times;
          </button>
        </div>

        {/* Account summary cards */}
        {account && (
          <div style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(6, 1fr)',
            gap: 8,
            padding: '10px 16px',
            borderBottom: `1px solid ${THEME.border}`,
            flexShrink: 0,
          }}>
            {[
              { label: 'Balance', value: fmtMoney(account.balance), color: THEME.t1 },
              { label: 'Equity', value: fmtMoney(account.equity), color: THEME.t1 },
              { label: 'Margin', value: fmtMoney(account.margin), color: THEME.t1 },
              { label: 'Free Margin', value: fmtMoney(account.free_margin), color: THEME.t1 },
              { label: 'Leverage', value: `1:${account.leverage}`, color: THEME.t1 },
              { label: 'Floating P&L', value: `${totalPnl >= 0 ? '+' : ''}${fmtMoney(totalPnl)}`, color: nc(totalPnl) },
            ].map(c => (
              <div key={c.label} style={{
                background: THEME.card,
                borderRadius: 4,
                border: `1px solid ${THEME.border}`,
                padding: '6px 8px',
                textAlign: 'center',
              }}>
                <div style={{ fontSize: 8, fontWeight: 600, color: THEME.t3, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 2 }}>{c.label}</div>
                <div style={{ fontFamily: 'monospace', fontSize: 12, fontWeight: 700, color: c.color }}>{c.value}</div>
              </div>
            ))}
          </div>
        )}

        {/* Equity Curve */}
        {deals.length > 0 && (
          <EquityChart deals={deals} />
        )}

        {/* Tabs */}
        <div style={{
          display: 'flex',
          borderBottom: `1px solid ${THEME.border}`,
          flexShrink: 0,
        }}>
          {(['positions', 'history'] as const).map(t => (
            <button
              key={t}
              onClick={() => setTab(t)}
              style={{
                background: 'transparent',
                border: 'none',
                borderBottom: tab === t ? `2px solid ${THEME.teal}` : '2px solid transparent',
                color: tab === t ? THEME.teal : THEME.t3,
                fontSize: 10,
                fontWeight: 600,
                textTransform: 'uppercase',
                letterSpacing: 0.5,
                padding: '8px 20px',
                cursor: 'pointer',
              }}
            >
              {t === 'positions' ? `Open Positions (${positions.length})` : `Deal History (${deals.length})`}
            </button>
          ))}
        </div>

        {/* Content */}
        <div style={{ flex: 1, overflow: 'auto' }}>
          {loading ? (
            <div style={{ padding: 30, textAlign: 'center', color: THEME.t3, fontSize: 11 }}>Loading...</div>
          ) : tab === 'positions' ? (
            positions.length === 0 ? (
              <div style={{ padding: 30, textAlign: 'center', color: THEME.t3, fontSize: 11 }}>No open positions</div>
            ) : (
              <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                <thead>
                  <tr>
                    <th style={{ ...hdr, textAlign: 'left' }}>Symbol</th>
                    <th style={hdr}>Direction</th>
                    <th style={hdr}>Volume</th>
                    <th style={hdr}>Open Price</th>
                    <th style={hdr}>Current</th>
                    <th style={hdr}>Open Time</th>
                    <th style={hdr}>Swap</th>
                    <th style={hdr}>Profit</th>
                  </tr>
                </thead>
                <tbody>
                  {positions.map((p, i) => (
                    <tr key={i}>
                      <td style={{ ...cell, textAlign: 'left', color: THEME.t1, fontWeight: 600 }}>{p.symbol}</td>
                      <td style={{ ...cell, color: p.direction === 'BUY' ? THEME.blue : THEME.red, fontWeight: 600 }}>{p.direction}</td>
                      <td style={{ ...cell, color: THEME.t1 }}>{p.volumeLots.toFixed(2)}</td>
                      <td style={{ ...cell, color: THEME.t1 }}>{p.openPrice.toFixed(5)}</td>
                      <td style={{ ...cell, color: THEME.t1 }}>{p.currentPrice.toFixed(5)}</td>
                      <td style={{ ...cell, color: THEME.t2, fontSize: 10 }}>{fmtDate(p.openTime)}</td>
                      <td style={{ ...cell, color: THEME.t2 }}>{(p.swap || 0).toFixed(2)}</td>
                      <td style={{ ...cell, color: nc(p.profit), fontWeight: 700 }}>{p.profit >= 0 ? '+' : ''}{fmtMoney(p.profit)}</td>
                    </tr>
                  ))}
                  {/* Totals row */}
                  <tr style={{ background: 'rgba(255,255,255,0.02)' }}>
                    <td colSpan={7} style={{ ...cell, textAlign: 'right', color: THEME.t2, fontWeight: 700, borderBottom: 'none' }}>Total:</td>
                    <td style={{ ...cell, color: nc(totalPnl), fontWeight: 700, borderBottom: 'none' }}>{totalPnl >= 0 ? '+' : ''}{fmtMoney(totalPnl)}</td>
                  </tr>
                </tbody>
              </table>
            )
          ) : (
            deals.length === 0 ? (
              <div style={{ padding: 30, textAlign: 'center', color: THEME.t3, fontSize: 11 }}>No deals today</div>
            ) : (
              <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                <thead>
                  <tr>
                    <th style={{ ...hdr, textAlign: 'left' }}>Deal</th>
                    <th style={{ ...hdr, textAlign: 'left' }}>Symbol</th>
                    <th style={hdr}>Dir</th>
                    <th style={hdr}>Entry</th>
                    <th style={hdr}>Volume</th>
                    <th style={hdr}>Price</th>
                    <th style={hdr}>Commission</th>
                    <th style={hdr}>Swap</th>
                    <th style={hdr}>Profit</th>
                    <th style={hdr}>Time</th>
                  </tr>
                </thead>
                <tbody>
                  {deals.map(d => {
                    const total = d.profit + d.commission + d.swap + d.fee;
                    return (
                      <tr key={d.deal_id}>
                        <td style={{ ...cell, textAlign: 'left', color: THEME.t2, fontSize: 10 }}>{d.deal_id}</td>
                        <td style={{ ...cell, textAlign: 'left', color: THEME.t1, fontWeight: 600 }}>{d.symbol}</td>
                        <td style={{ ...cell, color: d.direction === 'BUY' ? THEME.blue : THEME.red, fontWeight: 600 }}>{d.direction}</td>
                        <td style={{
                          ...cell,
                          fontSize: 9,
                          fontWeight: 600,
                          color: d.entry === 0 ? THEME.teal : d.entry === 1 ? THEME.amber : THEME.t2,
                        }}>
                          {entryLabel(d.entry)}
                        </td>
                        <td style={{ ...cell, color: THEME.t1 }}>{d.volume.toFixed(2)}</td>
                        <td style={{ ...cell, color: THEME.t1 }}>{d.price.toFixed(5)}</td>
                        <td style={{ ...cell, color: d.commission !== 0 ? THEME.red : THEME.t3 }}>{d.commission.toFixed(2)}</td>
                        <td style={{ ...cell, color: d.swap !== 0 ? nc(d.swap) : THEME.t3 }}>{d.swap.toFixed(2)}</td>
                        <td style={{
                          ...cell,
                          color: d.entry >= 1 ? nc(total) : THEME.t3,
                          fontWeight: d.entry >= 1 ? 700 : 400,
                        }}>
                          {d.entry >= 1 ? `${total >= 0 ? '+' : ''}${fmtMoney(total)}` : ''}
                        </td>
                        <td style={{ ...cell, color: THEME.t2, fontSize: 10 }}>{fmtTime(d.deal_time)}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )
          )}
        </div>

        {/* Footer */}
        {account && (
          <div style={{
            padding: '6px 16px',
            borderTop: `1px solid ${THEME.border}`,
            display: 'flex',
            gap: 16,
            fontSize: 10,
            color: THEME.t3,
            flexShrink: 0,
          }}>
            <span>Registered: {fmtDate(account.registration_time)}</span>
            <span>Last Trade: {fmtDate(account.last_trade_time)}</span>
            <span>Currency: {account.currency}</span>
          </div>
        )}
      </div>
    </div>
  );
}

function EquityChart({ deals }: { deals: Deal[] }) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const drawTimeout = useRef<number>(0);

  const draw = useCallback(() => {
    const canvas = canvasRef.current;
    const container = containerRef.current;
    if (!canvas || !container) return;

    const dpr = window.devicePixelRatio || 1;
    const w = container.clientWidth;
    const h = 140;
    canvas.width = w * dpr;
    canvas.height = h * dpr;
    canvas.style.width = `${w}px`;
    canvas.style.height = `${h}px`;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    ctx.scale(dpr, dpr);
    ctx.clearRect(0, 0, w, h);

    const pad = { top: 18, right: 60, bottom: 22, left: 60 };
    const plotW = w - pad.left - pad.right;
    const plotH = h - pad.top - pad.bottom;

    // Build equity points from OUT deals sorted by time
    const outDeals = [...deals]
      .filter(d => d.entry >= 1)
      .sort((a, b) => new Date(a.deal_time).getTime() - new Date(b.deal_time).getTime());

    if (outDeals.length === 0) {
      ctx.fillStyle = THEME.t3;
      ctx.font = '10px monospace';
      ctx.textAlign = 'center';
      ctx.fillText('No closed deals for equity curve', w / 2, h / 2);
      return;
    }

    let cumPnl = 0;
    const points: { time: number; pnl: number }[] = [];
    // Start at zero before first deal
    points.push({ time: new Date(outDeals[0].deal_time).getTime() - 60000, pnl: 0 });
    for (const d of outDeals) {
      cumPnl += d.profit + d.commission + d.swap + d.fee;
      points.push({ time: new Date(d.deal_time).getTime(), pnl: cumPnl });
    }

    const times = points.map(p => p.time);
    const pnls = points.map(p => p.pnl);
    const tMin = Math.min(...times);
    const tMax = Math.max(...times);
    const pnlMin = Math.min(...pnls, 0);
    const pnlMax = Math.max(...pnls, 0);
    const tRange = tMax - tMin || 3600000;
    const pnlRange = pnlMax - pnlMin || 100;

    const xStart = tMin - tRange * 0.02;
    const xEnd = tMax + tRange * 0.02;
    const yBottom = pnlMin - pnlRange * 0.12;
    const yTop = pnlMax + pnlRange * 0.12;

    const toX = (t: number) => pad.left + ((t - xStart) / (xEnd - xStart)) * plotW;
    const toY = (v: number) => pad.top + plotH - ((v - yBottom) / (yTop - yBottom)) * plotH;

    // Grid
    ctx.strokeStyle = THEME.border;
    ctx.lineWidth = 0.5;
    for (let i = 0; i <= 4; i++) {
      const val = yBottom + ((yTop - yBottom) / 4) * i;
      const y = toY(val);
      ctx.beginPath();
      ctx.moveTo(pad.left, y);
      ctx.lineTo(pad.left + plotW, y);
      ctx.stroke();

      ctx.fillStyle = THEME.t3;
      ctx.font = '8px monospace';
      ctx.textAlign = 'right';
      const label = Math.abs(val) >= 1000
        ? `${val >= 0 ? '+' : ''}${(val / 1000).toFixed(1)}K`
        : `${val >= 0 ? '+' : ''}${val.toFixed(0)}`;
      ctx.fillText(label, pad.left - 4, y + 3);
    }

    // Zero line
    const zeroY = toY(0);
    if (zeroY > pad.top && zeroY < pad.top + plotH) {
      ctx.strokeStyle = THEME.t3 + '50';
      ctx.lineWidth = 1;
      ctx.setLineDash([3, 3]);
      ctx.beginPath();
      ctx.moveTo(pad.left, zeroY);
      ctx.lineTo(pad.left + plotW, zeroY);
      ctx.stroke();
      ctx.setLineDash([]);
    }

    // Time labels
    const numLabels = Math.min(6, points.length);
    ctx.fillStyle = THEME.t3;
    ctx.font = '8px monospace';
    ctx.textAlign = 'center';
    for (let i = 0; i <= numLabels; i++) {
      const t = xStart + ((xEnd - xStart) / numLabels) * i;
      const x = toX(t);
      const d = new Date(t);
      ctx.fillText(`${d.getHours().toString().padStart(2, '0')}:${d.getMinutes().toString().padStart(2, '0')}`, x, h - 4);
    }

    // Draw equity curve (step line)
    const lastPnl = points[points.length - 1].pnl;
    const lineColor = lastPnl >= 0 ? THEME.blue : THEME.red;

    ctx.beginPath();
    ctx.moveTo(toX(points[0].time), toY(points[0].pnl));
    for (let i = 1; i < points.length; i++) {
      const x = toX(points[i].time);
      ctx.lineTo(x, toY(points[i - 1].pnl));
      ctx.lineTo(x, toY(points[i].pnl));
    }
    ctx.strokeStyle = lineColor;
    ctx.lineWidth = 1.5;
    ctx.stroke();

    // Fill under curve to zero
    ctx.beginPath();
    ctx.moveTo(toX(points[0].time), toY(0));
    ctx.lineTo(toX(points[0].time), toY(points[0].pnl));
    for (let i = 1; i < points.length; i++) {
      const x = toX(points[i].time);
      ctx.lineTo(x, toY(points[i - 1].pnl));
      ctx.lineTo(x, toY(points[i].pnl));
    }
    ctx.lineTo(toX(points[points.length - 1].time), toY(0));
    ctx.closePath();
    const grad = ctx.createLinearGradient(0, toY(pnlMax), 0, toY(pnlMin));
    if (lastPnl >= 0) {
      grad.addColorStop(0, THEME.blue + '20');
      grad.addColorStop(1, THEME.blue + '03');
    } else {
      grad.addColorStop(0, THEME.red + '03');
      grad.addColorStop(1, THEME.red + '20');
    }
    ctx.fillStyle = grad;
    ctx.fill();

    // End value label
    const lastX = toX(points[points.length - 1].time);
    const lastY = toY(lastPnl);
    const pnlLabel = `${lastPnl >= 0 ? '+' : '-'}$${Math.abs(lastPnl).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    ctx.font = 'bold 9px monospace';
    const tw = ctx.measureText(pnlLabel).width;
    const lx = Math.min(lastX + 6, pad.left + plotW - tw - 8);

    ctx.fillStyle = lastPnl >= 0 ? THEME.blue + '20' : THEME.red + '20';
    ctx.strokeStyle = lineColor;
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.roundRect(lx - 3, lastY - 7, tw + 6, 14, 3);
    ctx.fill();
    ctx.stroke();

    ctx.fillStyle = lineColor;
    ctx.textAlign = 'left';
    ctx.fillText(pnlLabel, lx, lastY + 3);

    // Title
    ctx.fillStyle = THEME.t3;
    ctx.font = '9px sans-serif';
    ctx.textAlign = 'left';
    ctx.fillText('EQUITY CURVE', pad.left, 11);
    ctx.textAlign = 'right';
    ctx.fillText(`${outDeals.length} deals`, pad.left + plotW, 11);
  }, [deals]);

  useEffect(() => {
    clearTimeout(drawTimeout.current);
    drawTimeout.current = window.setTimeout(draw, 100);
  }, [draw]);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;
    const ro = new ResizeObserver(() => {
      clearTimeout(drawTimeout.current);
      drawTimeout.current = window.setTimeout(draw, 100);
    });
    ro.observe(container);
    return () => ro.disconnect();
  }, [draw]);

  return (
    <div
      ref={containerRef}
      style={{
        padding: '8px 16px',
        borderBottom: `1px solid ${THEME.border}`,
        flexShrink: 0,
      }}
    >
      <canvas ref={canvasRef} style={{ display: 'block', width: '100%' }} />
    </div>
  );
}

function todayStr() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function tomorrowStr() {
  const d = new Date();
  d.setDate(d.getDate() + 1);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}
