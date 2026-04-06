import { useRef, useEffect, useCallback, useState } from 'react';
import { THEME } from '../../../theme';
import type { TradeRecord } from '../../../types/compare';

interface PriceChartProps {
  trades: TradeRecord[];
  symbol: string;
}

interface EquityPoint {
  time: number;
  cumPnl: number;
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

export function PriceChart({ trades, symbol }: PriceChartProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const drawTimeout = useRef<number>(0);
  const [equityPoints, setEquityPoints] = useState<EquityPoint[]>([]);

  // Fetch closed deals to build equity curve
  useEffect(() => {
    const fetchEquity = async () => {
      try {
        const canonical = symbol.replace(/[-.]$/, '').toUpperCase();
        const res = await fetch(`http://localhost:5000/api/accounts/deals?source=bbook&from=${todayStr()}&to=${tomorrowStr()}`);
        if (!res.ok) return;
        const data = await res.json();
        const allDeals: any[] = data.deals || data || [];

        // Filter by symbol, only OUT deals (entry >= 1) that have profit
        const symbolDeals = allDeals
          .filter(d => {
            const dSym = (d.canonical_symbol || d.symbol || '').replace(/[-.]$/, '').toUpperCase();
            return dSym === canonical && d.entry >= 1;
          })
          .sort((a, b) => new Date(a.deal_time).getTime() - new Date(b.deal_time).getTime());

        // Build cumulative P&L
        let cumPnl = 0;
        const points: EquityPoint[] = [{ time: 0, cumPnl: 0 }];
        for (const deal of symbolDeals) {
          const pnl = (deal.profit || 0) + (deal.commission || 0) + (deal.swap || 0) + (deal.fee || 0);
          cumPnl += pnl;
          points.push({
            time: new Date(deal.deal_time).getTime(),
            cumPnl,
          });
        }

        // Set the first point's time to just before the first real deal
        if (points.length > 1) {
          points[0].time = points[1].time - 60000;
        }

        setEquityPoints(points.length > 1 ? points : []);
      } catch {}
    };

    fetchEquity();
    const interval = setInterval(fetchEquity, 5000);
    return () => clearInterval(interval);
  }, [symbol]);

  const draw = useCallback(() => {
    const canvas = canvasRef.current;
    const container = containerRef.current;
    if (!canvas || !container) return;

    const dpr = window.devicePixelRatio || 1;
    const w = container.clientWidth;
    const h = 205;
    canvas.width = w * dpr;
    canvas.height = h * dpr;
    canvas.style.width = `${w}px`;
    canvas.style.height = `${h}px`;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    ctx.scale(dpr, dpr);

    // Clear
    ctx.clearRect(0, 0, w, h);

    const pad = { top: 20, right: 60, bottom: 28, left: 70 };
    const plotW = w - pad.left - pad.right;
    const plotH = h - pad.top - pad.bottom;

    // If no equity data and no trades, show empty state
    if (equityPoints.length === 0 && trades.length === 0) {
      ctx.fillStyle = THEME.t3;
      ctx.font = '11px monospace';
      ctx.textAlign = 'center';
      ctx.fillText('No trade data for this symbol', w / 2, h / 2);
      return;
    }

    // Use equity curve data
    const points = equityPoints.length > 0 ? equityPoints : [];

    if (points.length < 2) {
      ctx.fillStyle = THEME.t3;
      ctx.font = '11px monospace';
      ctx.textAlign = 'center';
      ctx.fillText('No closed deals for equity curve', w / 2, h / 2);
      return;
    }

    // Calculate ranges
    const times = points.map(p => p.time);
    const pnls = points.map(p => p.cumPnl);
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

    // Draw grid lines
    ctx.strokeStyle = THEME.border;
    ctx.lineWidth = 0.5;
    const gridSteps = 5;
    for (let i = 0; i <= gridSteps; i++) {
      const val = yBottom + ((yTop - yBottom) / gridSteps) * i;
      const y = toY(val);
      ctx.beginPath();
      ctx.moveTo(pad.left, y);
      ctx.lineTo(pad.left + plotW, y);
      ctx.stroke();

      ctx.fillStyle = THEME.t3;
      ctx.font = '9px monospace';
      ctx.textAlign = 'right';
      const label = Math.abs(val) >= 1000
        ? `${val >= 0 ? '+' : ''}${(val / 1000).toFixed(1)}K`
        : `${val >= 0 ? '+' : ''}${val.toFixed(0)}`;
      ctx.fillText(label, pad.left - 6, y + 3);
    }

    // Draw zero line
    const zeroY = toY(0);
    if (zeroY > pad.top && zeroY < pad.top + plotH) {
      ctx.strokeStyle = THEME.t3 + '60';
      ctx.lineWidth = 1;
      ctx.setLineDash([4, 4]);
      ctx.beginPath();
      ctx.moveTo(pad.left, zeroY);
      ctx.lineTo(pad.left + plotW, zeroY);
      ctx.stroke();
      ctx.setLineDash([]);
    }

    // Time labels
    const numLabels = Math.min(6, points.length);
    ctx.fillStyle = THEME.t3;
    ctx.font = '9px monospace';
    ctx.textAlign = 'center';
    for (let i = 0; i <= numLabels; i++) {
      const t = xStart + ((xEnd - xStart) / numLabels) * i;
      const x = toX(t);
      const d = new Date(t);
      ctx.fillText(`${d.getHours().toString().padStart(2, '0')}:${d.getMinutes().toString().padStart(2, '0')}`, x, h - 6);
    }

    // Draw equity curve
    ctx.beginPath();
    ctx.moveTo(toX(points[0].time), toY(points[0].cumPnl));
    for (let i = 1; i < points.length; i++) {
      const x = toX(points[i].time);
      const y = toY(points[i].cumPnl);
      // Step line for equity curve (horizontal then vertical)
      ctx.lineTo(x, toY(points[i - 1].cumPnl));
      ctx.lineTo(x, y);
    }

    const lastPnl = points[points.length - 1].cumPnl;
    const lineColor = lastPnl >= 0 ? THEME.blue : THEME.red;
    ctx.strokeStyle = lineColor;
    ctx.lineWidth = 2;
    ctx.stroke();

    // Fill area under/above zero
    // Clone the path and close to zero line
    ctx.beginPath();
    ctx.moveTo(toX(points[0].time), toY(0));
    ctx.lineTo(toX(points[0].time), toY(points[0].cumPnl));
    for (let i = 1; i < points.length; i++) {
      const x = toX(points[i].time);
      ctx.lineTo(x, toY(points[i - 1].cumPnl));
      ctx.lineTo(x, toY(points[i].cumPnl));
    }
    ctx.lineTo(toX(points[points.length - 1].time), toY(0));
    ctx.closePath();

    const grad = ctx.createLinearGradient(0, toY(pnlMax), 0, toY(pnlMin));
    if (lastPnl >= 0) {
      grad.addColorStop(0, THEME.blue + '25');
      grad.addColorStop(1, THEME.blue + '05');
    } else {
      grad.addColorStop(0, THEME.red + '05');
      grad.addColorStop(1, THEME.red + '25');
    }
    ctx.fillStyle = grad;
    ctx.fill();

    // Draw dots at each equity point
    for (let i = 1; i < points.length; i++) {
      const x = toX(points[i].time);
      const y = toY(points[i].cumPnl);
      ctx.beginPath();
      ctx.arc(x, y, 2.5, 0, Math.PI * 2);
      ctx.fillStyle = points[i].cumPnl >= 0 ? THEME.blue : THEME.red;
      ctx.fill();
    }

    // Current value label at the end
    const lastPoint = points[points.length - 1];
    const lastX = toX(lastPoint.time);
    const lastY = toY(lastPoint.cumPnl);
    const pnlLabel = lastPoint.cumPnl >= 0
      ? `+$${Math.abs(lastPoint.cumPnl).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
      : `-$${Math.abs(lastPoint.cumPnl).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

    // Label background
    const labelWidth = ctx.measureText(pnlLabel).width + 12;
    ctx.font = 'bold 10px monospace';
    const textWidth = ctx.measureText(pnlLabel).width;
    const labelX = Math.min(lastX + 8, pad.left + plotW - textWidth - 10);
    const labelY = lastY;

    ctx.fillStyle = lastPoint.cumPnl >= 0 ? THEME.blue + '20' : THEME.red + '20';
    ctx.strokeStyle = lastPoint.cumPnl >= 0 ? THEME.blue : THEME.red;
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.roundRect(labelX - 4, labelY - 8, textWidth + 8, 16, 3);
    ctx.fill();
    ctx.stroke();

    ctx.fillStyle = lastPoint.cumPnl >= 0 ? THEME.blue : THEME.red;
    ctx.textAlign = 'left';
    ctx.fillText(pnlLabel, labelX, labelY + 4);

    // Title
    ctx.fillStyle = THEME.t3;
    ctx.font = '9px sans-serif';
    ctx.textAlign = 'left';
    ctx.fillText('EQUITY CURVE', pad.left, 12);

    // Deal count
    ctx.textAlign = 'right';
    ctx.fillText(`${points.length - 1} deals`, pad.left + plotW, 12);
  }, [trades, symbol, equityPoints]);

  useEffect(() => {
    clearTimeout(drawTimeout.current);
    drawTimeout.current = window.setTimeout(draw, 200);
  }, [draw]);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;
    const ro = new ResizeObserver(() => {
      clearTimeout(drawTimeout.current);
      drawTimeout.current = window.setTimeout(draw, 200);
    });
    ro.observe(container);
    return () => ro.disconnect();
  }, [draw]);

  return (
    <div ref={containerRef} style={{ width: '100%', height: 205 }}>
      <canvas ref={canvasRef} style={{ display: 'block' }} />
    </div>
  );
}
