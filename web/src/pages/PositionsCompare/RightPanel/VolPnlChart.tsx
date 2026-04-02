import { useRef, useEffect, useCallback } from 'react';
import { THEME } from '../../../theme';
import type { TradeRecord } from '../../../types/compare';

interface VolPnlChartProps {
  trades: TradeRecord[];
  symbol: string;
  height?: number;
}

export function VolPnlChart({ trades, symbol, height = 200 }: VolPnlChartProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const drawTimeout = useRef<number>(0);

  const draw = useCallback(() => {
    const canvas = canvasRef.current;
    const container = containerRef.current;
    if (!canvas || !container) return;

    const dpr = window.devicePixelRatio || 1;
    const w = container.clientWidth;
    const h = height;
    canvas.width = w * dpr;
    canvas.height = h * dpr;
    canvas.style.width = `${w}px`;
    canvas.style.height = `${h}px`;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    ctx.scale(dpr, dpr);
    ctx.clearRect(0, 0, w, h);

    const pad = { top: 16, right: 50, bottom: 28, left: 60 };
    const plotW = w - pad.left - pad.right;
    const halfH = (h - pad.top - pad.bottom) / 2;

    if (trades.length === 0) {
      ctx.fillStyle = THEME.t3;
      ctx.font = '11px monospace';
      ctx.textAlign = 'center';
      ctx.fillText('No trade data', w / 2, h / 2);
      return;
    }

    // Bucket trades by hour
    const buckets: Record<number, { cliVol: number; covVol: number; cliPnl: number; covPnl: number }> = {};
    for (const t of trades) {
      const hr = new Date(t.entryTime).getHours();
      if (!buckets[hr]) buckets[hr] = { cliVol: 0, covVol: 0, cliPnl: 0, covPnl: 0 };
      if (t.side === 'client') {
        buckets[hr].cliVol += t.volume;
        buckets[hr].cliPnl += t.profit;
      } else {
        buckets[hr].covVol += t.volume;
        buckets[hr].covPnl += t.profit;
      }
    }

    const hours = Object.keys(buckets).map(Number).sort((a, b) => a - b);
    if (hours.length === 0) return;

    const minHr = Math.min(...hours);
    const maxHr = Math.max(...hours);
    const hrRange = maxHr - minHr || 1;

    // --- TOP HALF: Volume bars ---
    const maxVol = Math.max(...hours.map(hr => Math.max(buckets[hr].cliVol, buckets[hr].covVol)), 0.01);
    const barW = Math.max(6, Math.min(20, plotW / (hrRange + 1) / 3));

    // Section label
    ctx.fillStyle = THEME.t3;
    ctx.font = '8px sans-serif';
    ctx.textAlign = 'left';
    ctx.fillText('VOL', pad.left, pad.top + 8);

    for (const hr of hours) {
      const b = buckets[hr];
      const cx = pad.left + ((hr - minHr) / hrRange) * plotW + plotW / (hrRange + 1) / 2;
      const volTop = pad.top;

      // Client bar (blue, left)
      const cliH = (b.cliVol / maxVol) * halfH * 0.85;
      ctx.fillStyle = THEME.blue + 'CC';
      ctx.fillRect(cx - barW - 1, volTop + halfH - cliH, barW, cliH);

      // Coverage bar (amber, right)
      const covH = (b.covVol / maxVol) * halfH * 0.85;
      ctx.fillStyle = THEME.amber + 'CC';
      ctx.fillRect(cx + 1, volTop + halfH - covH, barW, covH);
    }

    // --- BOTTOM HALF: Cumulative P&L lines ---
    const pnlTop = pad.top + halfH;

    // Section label
    ctx.fillStyle = THEME.t3;
    ctx.font = '8px sans-serif';
    ctx.textAlign = 'left';
    ctx.fillText('P&L', pad.left, pnlTop + 12);

    // Dashed zero line
    ctx.strokeStyle = THEME.t3 + '40';
    ctx.lineWidth = 1;
    ctx.setLineDash([4, 3]);
    ctx.beginPath();
    ctx.moveTo(pad.left, pnlTop + halfH / 2);
    ctx.lineTo(pad.left + plotW, pnlTop + halfH / 2);
    ctx.stroke();
    ctx.setLineDash([]);

    // Compute cumulative P&L
    let cliCum = 0, covCum = 0;
    const cliPoints: [number, number][] = [];
    const covPoints: [number, number][] = [];

    for (const hr of hours) {
      const b = buckets[hr];
      cliCum += b.cliPnl;
      covCum += b.covPnl;
      const x = pad.left + ((hr - minHr) / hrRange) * plotW + plotW / (hrRange + 1) / 2;
      cliPoints.push([x, cliCum]);
      covPoints.push([x, covCum]);
    }

    const allCum = [...cliPoints.map(p => p[1]), ...covPoints.map(p => p[1])];
    const maxCum = Math.max(Math.abs(Math.max(...allCum)), Math.abs(Math.min(...allCum)), 1);
    const pnlZero = pnlTop + halfH / 2;
    const pnlScale = (halfH / 2 - 8) / maxCum;

    const toY = (v: number) => pnlZero - v * pnlScale;

    // Draw cumulative lines
    const drawLine = (points: [number, number][], color: string) => {
      if (points.length < 1) return;

      // Filled area
      ctx.beginPath();
      ctx.moveTo(points[0][0], pnlZero);
      for (const [x, v] of points) ctx.lineTo(x, toY(v));
      ctx.lineTo(points[points.length - 1][0], pnlZero);
      ctx.closePath();
      ctx.fillStyle = color + '15';
      ctx.fill();

      // Line
      ctx.beginPath();
      ctx.moveTo(points[0][0], toY(points[0][1]));
      for (let i = 1; i < points.length; i++) ctx.lineTo(points[i][0], toY(points[i][1]));
      ctx.strokeStyle = color;
      ctx.lineWidth = 1.5;
      ctx.stroke();

      // Dots
      for (const [x, v] of points) {
        ctx.beginPath();
        ctx.arc(x, toY(v), 2.5, 0, Math.PI * 2);
        ctx.fillStyle = color;
        ctx.fill();
      }

      // Final value label
      const last = points[points.length - 1];
      ctx.font = '9px monospace';
      ctx.fillStyle = color;
      ctx.textAlign = 'left';
      ctx.fillText(
        `${last[1] >= 0 ? '+' : ''}${last[1].toFixed(0)}`,
        last[0] + 6,
        toY(last[1]) + 3
      );
    };

    drawLine(cliPoints, THEME.blue);
    drawLine(covPoints, THEME.amber);

    // Hour labels at bottom
    ctx.fillStyle = THEME.t3;
    ctx.font = '9px monospace';
    ctx.textAlign = 'center';
    for (const hr of hours) {
      const x = pad.left + ((hr - minHr) / hrRange) * plotW + plotW / (hrRange + 1) / 2;
      ctx.fillText(`${hr}:00`, x, h - 6);
    }
  }, [trades, symbol, height]);

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
    <div ref={containerRef} style={{ width: '100%', height }}>
      <canvas ref={canvasRef} style={{ display: 'block' }} />
    </div>
  );
}
