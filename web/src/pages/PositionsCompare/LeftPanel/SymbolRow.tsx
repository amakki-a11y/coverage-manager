import { useState, useRef, useEffect } from 'react';
import { THEME } from '../../../theme';
import type { SymbolExposure } from '../../../types/compare';

interface SymbolRowProps {
  data: SymbolExposure;
  isSelected: boolean;
  onClick: () => void;
  price?: number;
}

function hedgeColor(pct: number): string {
  if (pct >= 80) return THEME.green;
  if (pct >= 50) return THEME.amber;
  return THEME.red;
}

function nc(v: number): string {
  return v >= 0 ? THEME.blue : THEME.red;
}

function fmtNet(v: number): string {
  if (Math.abs(v) < 0.005) return '0.00';
  return `${v > 0 ? '+' : ''}${v.toFixed(2)}`;
}

function fmtPnl(v: number): string {
  const abs = Math.abs(v);
  const sign = v >= 0 ? '+' : '-';
  if (abs >= 1000) return `${sign}$${(abs / 1000).toFixed(1)}K`;
  return `${sign}$${abs.toFixed(0)}`;
}

function fmtPrice(p: number): string {
  if (p >= 1000) return p.toFixed(1);
  if (p >= 100) return p.toFixed(2);
  if (p >= 10) return p.toFixed(3);
  if (p >= 1) return p.toFixed(4);
  return p.toFixed(5);
}

export function SymbolRow({ data, isSelected, onClick, price }: SymbolRowProps) {
  const [expanded, setExpanded] = useState(false);
  const prevPrice = useRef(price);
  const [priceDir, setPriceDir] = useState<'up' | 'down' | 'none'>('none');

  useEffect(() => {
    if (price != null && prevPrice.current != null && price !== prevPrice.current) {
      setPriceDir(price > prevPrice.current ? 'up' : 'down');
      const t = setTimeout(() => setPriceDir('none'), 1500);
      prevPrice.current = price;
      return () => clearTimeout(t);
    }
    prevPrice.current = price;
  }, [price]);

  const netDiff = data.clientNetVolume - data.coverageNetVolume;
  const pnlDiff = data.netPnl;
  const hc = hedgeColor(data.hedgePercent);
  const priceColor = priceDir === 'up' ? THEME.green : priceDir === 'down' ? THEME.red : THEME.t3;

  const toggleExpand = (e: React.MouseEvent) => {
    e.stopPropagation();
    setExpanded(prev => !prev);
  };

  return (
    <div
      onClick={onClick}
      style={{
        borderBottom: `1px solid ${THEME.border}`,
        cursor: 'pointer',
        background: isSelected ? `${THEME.teal}11` : 'transparent',
        borderLeft: isSelected ? `3px solid ${THEME.teal}` : '3px solid transparent',
        transition: 'background 0.15s',
      }}
      onMouseEnter={e => { if (!isSelected) e.currentTarget.style.background = `${THEME.t3}08`; }}
      onMouseLeave={e => { if (!isSelected) e.currentTarget.style.background = 'transparent'; }}
    >
      {/* Header row */}
      <div
        onClick={toggleExpand}
        style={{ padding: '8px 10px', display: 'flex', alignItems: 'center', gap: 8 }}
      >
        {/* Symbol + Price stacked */}
        <div style={{ minWidth: 90 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <span style={{ fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace", fontWeight: 700, color: THEME.t1, fontSize: 13 }}>
              {data.symbol}
            </span>
            <span style={{
              fontSize: 9, fontWeight: 700, fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace",
              padding: '1px 6px', borderRadius: 8,
              background: `${hc}18`, color: hc,
            }}>
              {data.hedgePercent.toFixed(0)}%
            </span>
          </div>
          {price != null && price > 0 && (
            <div style={{
              fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace", fontSize: 11, marginTop: 2,
              color: priceColor, fontWeight: priceDir !== 'none' ? 700 : 400,
              transition: 'color 0.3s',
            }}>
              {fmtPrice(price)}
            </div>
          )}
        </div>

        {/* Spacer */}
        <div style={{ flex: 1 }} />

        {/* Net position */}
        <div style={{ textAlign: 'right', minWidth: 60 }}>
          <div style={{ fontSize: 8, color: THEME.t3, textTransform: 'uppercase', fontWeight: 600 }}>Net</div>
          <div style={{
            fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace", fontSize: 13, fontWeight: 700,
            color: nc(netDiff),
          }}>
            {fmtNet(netDiff)}
          </div>
        </div>

        {/* Net P&L */}
        <div style={{ textAlign: 'right', minWidth: 60 }}>
          <div style={{ fontSize: 8, color: THEME.t3, textTransform: 'uppercase', fontWeight: 600 }}>P&L</div>
          <div style={{
            fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace", fontSize: 13, fontWeight: 700,
            color: nc(pnlDiff),
          }}>
            {fmtPnl(pnlDiff)}
          </div>
        </div>
      </div>

      {/* Expanded: CLI and COV sub-rows under NET / P&L columns */}
      {expanded && (
        <div style={{ padding: '0 10px 8px', borderTop: `1px dashed ${THEME.border}` }}>
          {/* CLI row */}
          <div style={{ display: 'flex', alignItems: 'center', padding: '5px 0 2px' }}>
            <span style={{ fontSize: 9, fontWeight: 700, color: THEME.blue, textTransform: 'uppercase', minWidth: 90, letterSpacing: 0.5 }}>
              Client
            </span>
            <div style={{ flex: 1 }} />
            <span style={{ fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace", fontSize: 13, fontWeight: 600, color: THEME.t1, minWidth: 60, textAlign: 'right' }}>
              {fmtNet(data.clientNetVolume)}
            </span>
            <span style={{ fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace", fontSize: 13, fontWeight: 600, color: THEME.t2, minWidth: 60, textAlign: 'right', marginLeft: 8 }}>
              {fmtPnl(data.clientPnl)}
            </span>
          </div>
          {/* COV row */}
          <div style={{ display: 'flex', alignItems: 'center', padding: '2px 0 4px' }}>
            <span style={{ fontSize: 9, fontWeight: 700, color: THEME.teal, textTransform: 'uppercase', minWidth: 90, letterSpacing: 0.5 }}>
              Coverage
            </span>
            <div style={{ flex: 1 }} />
            <span style={{ fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace", fontSize: 13, fontWeight: 600, color: THEME.t1, minWidth: 60, textAlign: 'right' }}>
              {fmtNet(data.coverageNetVolume)}
            </span>
            <span style={{ fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace", fontSize: 13, fontWeight: 600, color: THEME.t2, minWidth: 60, textAlign: 'right', marginLeft: 8 }}>
              {fmtPnl(data.coveragePnl)}
            </span>
          </div>
        </div>
      )}
    </div>
  );
}
