import { useState, useRef, useMemo, useCallback } from 'react';
import { THEME } from '../../../theme';
import type { SymbolExposure } from '../../../types/compare';
import { SymbolRow } from './SymbolRow';
import { ExpandedTable } from './ExpandedTable';

interface LeftPanelProps {
  symbols: SymbolExposure[];
  selectedSymbol: string | null;
  onSelect: (symbol: string) => void;
}

export function LeftPanel({ symbols, selectedSymbol, onSelect }: LeftPanelProps) {
  const [expanded, setExpanded] = useState(false);
  const [panelWidth, setPanelWidth] = useState(() => {
    const saved = localStorage.getItem('comparePanelWidth');
    return saved ? Number(saved) : 520;
  });
  const [customOrder, setCustomOrder] = useState<string[]>(() => {
    try { return JSON.parse(localStorage.getItem('compareOrder') || '[]'); } catch { return []; }
  });
  const dragSymbol = useRef<string | null>(null);
  const dragOverSymbol = useRef<string | null>(null);
  const isResizing = useRef(false);
  const panelRef = useRef<HTMLDivElement>(null);

  const handleSelect = (symbol: string) => {
    onSelect(symbol);
    if (expanded) setExpanded(false);
  };

  // Sort symbols by custom order
  const sortedSymbols = useMemo(() => {
    if (customOrder.length === 0) return symbols;
    const orderMap = new Map(customOrder.map((s, i) => [s, i]));
    return [...symbols].sort((a, b) => {
      const ai = orderMap.get(a.symbol) ?? 9999;
      const bi = orderMap.get(b.symbol) ?? 9999;
      if (ai !== bi) return ai - bi;
      return 0;
    });
  }, [symbols, customOrder]);

  const handleDragStart = (symbol: string) => {
    dragSymbol.current = symbol;
  };

  const handleDragOver = (e: React.DragEvent, symbol: string) => {
    e.preventDefault();
    dragOverSymbol.current = symbol;
  };

  const handleDrop = () => {
    if (!dragSymbol.current || !dragOverSymbol.current || dragSymbol.current === dragOverSymbol.current) return;
    const currentOrder = sortedSymbols.map(s => s.symbol);
    const fromIdx = currentOrder.indexOf(dragSymbol.current);
    const toIdx = currentOrder.indexOf(dragOverSymbol.current);
    if (fromIdx === -1 || toIdx === -1) return;

    const newOrder = [...currentOrder];
    newOrder.splice(fromIdx, 1);
    newOrder.splice(toIdx, 0, dragSymbol.current);

    setCustomOrder(newOrder);
    localStorage.setItem('compareOrder', JSON.stringify(newOrder));
    dragSymbol.current = null;
    dragOverSymbol.current = null;
  };

  // Resize handle
  const startResize = useCallback((e: React.MouseEvent) => {
    e.preventDefault();
    isResizing.current = true;
    const startX = e.clientX;
    const startWidth = panelWidth;

    const onMouseMove = (ev: MouseEvent) => {
      if (!isResizing.current) return;
      const newWidth = Math.max(200, Math.min(window.innerWidth - 200, startWidth + (ev.clientX - startX)));
      setPanelWidth(newWidth);
    };

    const onMouseUp = () => {
      isResizing.current = false;
      localStorage.setItem('comparePanelWidth', String(panelWidth));
      document.removeEventListener('mousemove', onMouseMove);
      document.removeEventListener('mouseup', onMouseUp);
      document.body.style.cursor = '';
      document.body.style.userSelect = '';
    };

    document.body.style.cursor = 'col-resize';
    document.body.style.userSelect = 'none';
    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup', onMouseUp);
  }, [panelWidth]);

  return (
    <div
      ref={panelRef}
      style={{
        position: expanded ? 'absolute' : 'relative',
        width: expanded ? '100%' : panelWidth,
        minWidth: expanded ? undefined : 200,
        height: '100%',
        zIndex: expanded ? 10 : 1,
        background: THEME.bg2,
        borderRight: `1px solid ${THEME.border}`,
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
      }}
    >
      {/* Header */}
      <div style={{
        padding: '8px 12px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        borderBottom: `1px solid ${THEME.border}`,
        flexShrink: 0,
      }}>
        <span style={{ fontSize: 10, fontWeight: 700, color: THEME.t3, textTransform: 'uppercase', letterSpacing: 1 }}>
          Symbols ({symbols.length})
        </span>
        <button
          onClick={() => setExpanded(!expanded)}
          style={{
            background: 'transparent',
            border: `1px solid ${THEME.border}`,
            borderRadius: 4,
            padding: '2px 8px',
            fontSize: 9,
            fontWeight: 600,
            color: THEME.teal,
            cursor: 'pointer',
            textTransform: 'uppercase',
          }}
        >
          {expanded ? 'Compact' : 'Full Table'}
        </button>
      </div>

      {/* Content */}
      {expanded ? (
        <ExpandedTable
          symbols={sortedSymbols}
          selectedSymbol={selectedSymbol}
          onSelect={handleSelect}
        />
      ) : (
        <div style={{ overflow: 'auto', flex: 1 }}>
          {sortedSymbols.map(s => (
            <div
              key={s.symbol}
              draggable
              onDragStart={() => handleDragStart(s.symbol)}
              onDragOver={(e) => handleDragOver(e, s.symbol)}
              onDrop={handleDrop}
              style={{ cursor: 'grab' }}
            >
              <SymbolRow
                data={s}
                isSelected={s.symbol === selectedSymbol}
                onClick={() => handleSelect(s.symbol)}
              />
            </div>
          ))}
          {symbols.length === 0 && (
            <div style={{ padding: 20, textAlign: 'center', color: THEME.t3, fontSize: 11 }}>
              No symbols — waiting for data...
            </div>
          )}
        </div>
      )}

      {/* Resize handle (right edge) — only in compact mode */}
      {!expanded && (
        <div
          onMouseDown={startResize}
          style={{
            position: 'absolute',
            top: 0,
            right: -3,
            width: 6,
            height: '100%',
            cursor: 'col-resize',
            zIndex: 20,
          }}
          onMouseEnter={e => (e.currentTarget.style.background = THEME.teal + '40')}
          onMouseLeave={e => (e.currentTarget.style.background = 'transparent')}
        />
      )}
    </div>
  );
}
