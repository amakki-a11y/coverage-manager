import { useRef, useEffect, useCallback } from 'react';
import { THEME } from '../../../theme';
import type { TradeRecord } from '../../../types/compare';

interface PriceChartProps {
  trades: TradeRecord[];
  symbol: string;
}

export function PriceChart({ trades, symbol }: PriceChartProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const drawTimeout = useRef<number>(0);

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

    const pad = { top: 16, right: 50, bottom: 28, left: 60 };
    const plotW = w - pad.left - pad.right;
    const plotH = h - pad.top - pad.bottom;

    if (trades.length === 0) {
      ctx.fillStyle = THEME.t3;
      ctx.font = '11px monospace';
      ctx.textAlign = 'center';
      ctx.fillText('No trade data for this symbol', w / 2, h / 2);
      return;
    }

    // Collect all prices for Y range
    const allPrices = trades.flatMap(t => [t.entryPrice, t.exitPrice]).filter(p => p > 0);
    if (allPrices.length === 0) return;

    const minPrice = Math.min(...allPrices);
    const maxPrice = Math.max(...allPrices);
    const priceRange = maxPrice - minPrice || 1;
    const yMin = minPrice - priceRange * 0.1;
    const yMax = maxPrice + priceRange * 0.1;

    // Collect all times for X range
    const allTimes = trades.flatMap(t => [new Date(t.entryTime).getTime(), new Date(t.exitTime).getTime()]);
    const tMin = Math.min(...allTimes);
    const tMax = Math.max(...allTimes);
    const tRange = tMax - tMin || 3600000;
    const xMin = tMin - tRange * 0.05;
    const xMax = tMax + tRange * 0.05;

    const toX = (t: number) => pad.left + ((t - xMin) / (xMax - xMin)) * plotW;
    const toY = (p: number) => pad.top + plotH - ((p - yMin) / (yMax - yMin)) * plotH;

    // Draw grid
    ctx.strokeStyle = THEME.border;
    ctx.lineWidth = 0.5;
    for (let i = 0; i <= 4; i++) {
      const y = pad.top + (plotH / 4) * i;
      ctx.beginPath();
      ctx.moveTo(pad.left, y);
      ctx.lineTo(pad.left + plotW, y);
      ctx.stroke();

      const price = yMax - ((yMax - yMin) / 4) * i;
      ctx.fillStyle = THEME.t3;
      ctx.font = '9px monospace';
      ctx.textAlign = 'right';
      ctx.fillText(price.toFixed(2), pad.left - 6, y + 3);
    }

    // Time labels
    const numLabels = Math.min(6, trades.length);
    ctx.fillStyle = THEME.t3;
    ctx.font = '9px monospace';
    ctx.textAlign = 'center';
    for (let i = 0; i <= numLabels; i++) {
      const t = xMin + ((xMax - xMin) / numLabels) * i;
      const x = toX(t);
      const d = new Date(t);
      ctx.fillText(`${d.getHours().toString().padStart(2, '0')}:${d.getMinutes().toString().padStart(2, '0')}`, x, h - 6);
    }

    // Draw price sparkline (teal gradient)
    const sortedTrades = [...trades].sort((a, b) => new Date(a.exitTime).getTime() - new Date(b.exitTime).getTime());
    const sparkPoints: [number, number][] = sortedTrades.map(t => [toX(new Date(t.exitTime).getTime()), toY(t.exitPrice)]);

    if (sparkPoints.length > 1) {
      ctx.beginPath();
      ctx.moveTo(sparkPoints[0][0], sparkPoints[0][1]);
      for (let i = 1; i < sparkPoints.length; i++) {
        const cp1x = sparkPoints[i - 1][0] + (sparkPoints[i][0] - sparkPoints[i - 1][0]) * 0.5;
        ctx.bezierCurveTo(cp1x, sparkPoints[i - 1][1], cp1x, sparkPoints[i][1], sparkPoints[i][0], sparkPoints[i][1]);
      }
      ctx.strokeStyle = THEME.teal + '60';
      ctx.lineWidth = 1.5;
      ctx.stroke();

      // Fill under sparkline
      ctx.lineTo(sparkPoints[sparkPoints.length - 1][0], pad.top + plotH);
      ctx.lineTo(sparkPoints[0][0], pad.top + plotH);
      ctx.closePath();
      const grad = ctx.createLinearGradient(0, pad.top, 0, pad.top + plotH);
      grad.addColorStop(0, THEME.teal + '18');
      grad.addColorStop(1, THEME.teal + '02');
      ctx.fillStyle = grad;
      ctx.fill();
    }

    // Draw trades
    for (const trade of trades) {
      const entryX = toX(new Date(trade.entryTime).getTime());
      const entryY = toY(trade.entryPrice);
      const exitX = toX(new Date(trade.exitTime).getTime());
      const exitY = toY(trade.exitPrice);
      const isClient = trade.side === 'client';
      const color = isClient ? THEME.blue : THEME.amber;
      const isBuy = trade.direction === 'BUY';

      // Line from entry to exit
      ctx.beginPath();
      ctx.moveTo(entryX, entryY);
      ctx.lineTo(exitX, exitY);
      ctx.strokeStyle = color + '90';
      ctx.lineWidth = 1.5;
      if (!isBuy) ctx.setLineDash([4, 3]);
      ctx.stroke();
      ctx.setLineDash([]);

      // Entry circle
      ctx.beginPath();
      ctx.arc(entryX, entryY, 3.5, 0, Math.PI * 2);
      ctx.fillStyle = color;
      ctx.fill();

      // Exit X marker
      const exitColor = trade.profit >= 0 ? THEME.green : THEME.red;
      ctx.strokeStyle = exitColor;
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.moveTo(exitX - 3, exitY - 3);
      ctx.lineTo(exitX + 3, exitY + 3);
      ctx.moveTo(exitX + 3, exitY - 3);
      ctx.lineTo(exitX - 3, exitY + 3);
      ctx.stroke();

      // P&L label near exit
      const pnlLabel = trade.profit >= 0
        ? `+${(trade.profit / 1000).toFixed(1)}K`
        : `${(trade.profit / 1000).toFixed(1)}K`;
      ctx.font = '8px monospace';
      ctx.fillStyle = exitColor;
      ctx.textAlign = 'left';
      ctx.fillText(pnlLabel, exitX + 6, exitY + 3);
    }

    // Title
    ctx.fillStyle = THEME.t3;
    ctx.font = '9px sans-serif';
    ctx.textAlign = 'left';
    ctx.fillText('PRICE TIMELINE', pad.left, 10);
  }, [trades, symbol]);

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
