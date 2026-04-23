import { useEffect, useMemo, useRef, useState, type ReactElement } from 'react';
import type { SidebarTab } from './Sidebar';

/**
 * ⌘K / Ctrl-K fuzzy command palette — jump-to-tab + quick actions.
 *
 * Keeps its own search state and key handling; fires `onClose` when the
 * user picks a command or hits Escape. Arrow keys navigate the filtered
 * list, Enter runs the selection.
 *
 * `onNavigate(tab)` is the tab-switcher the parent passes in (same
 * setter that backs the Sidebar); extra actions like "Capture snapshot"
 * or "Open alerts" can be added to the `ACTIONS` list below.
 */
interface Props {
  open: boolean;
  onClose: () => void;
  onNavigate: (tab: SidebarTab) => void;
  onOpenGuide: () => void;
  onOpenAlerts: () => void;
  onToggleTheme: () => void;
}

interface Cmd {
  group: string;
  label: string;
  hint?: string;
  act: () => void;
}

export function CommandPalette({ open, onClose, onNavigate, onOpenGuide, onOpenAlerts, onToggleTheme }: Props) {
  const [q, setQ] = useState('');
  const [idx, setIdx] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);

  const all: Cmd[] = useMemo(() => {
    const go = (t: SidebarTab, label: string) => ({
      group: 'Go to',
      label,
      act: () => { onNavigate(t); onClose(); },
    });
    return [
      go('exposure',  'Exposure'),
      go('positions', 'Positions'),
      go('compare',   'Compare'),
      go('pnl',       'P&L'),
      go('netpnl',    'Net P&L'),
      go('equitypnl', 'Equity P&L'),
      go('mappings',  'Mappings'),
      go('alerts',    'Alerts'),
      go('settings',  'Settings'),
      { group: 'Actions', label: 'Open user guide',        act: () => { onOpenGuide(); onClose(); } },
      { group: 'Actions', label: 'Open alert history',     act: () => { onOpenAlerts(); onClose(); } },
      { group: 'Actions', label: 'Toggle light / dark',    act: () => { onToggleTheme(); onClose(); } },
    ];
  }, [onNavigate, onClose, onOpenGuide, onOpenAlerts, onToggleTheme]);

  const filtered = useMemo(() => {
    const needle = q.trim().toLowerCase();
    if (!needle) return all;
    return all.filter(c => c.label.toLowerCase().includes(needle));
  }, [all, q]);

  useEffect(() => { if (open) { setQ(''); setIdx(0); } }, [open]);
  useEffect(() => { if (open) inputRef.current?.focus(); }, [open]);
  useEffect(() => { setIdx(0); }, [q]);

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { e.preventDefault(); onClose(); return; }
      if (e.key === 'ArrowDown') { e.preventDefault(); setIdx(i => Math.min(i + 1, filtered.length - 1)); return; }
      if (e.key === 'ArrowUp')   { e.preventDefault(); setIdx(i => Math.max(i - 1, 0)); return; }
      if (e.key === 'Enter') {
        e.preventDefault();
        filtered[idx]?.act();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open, filtered, idx, onClose]);

  if (!open) return null;

  // Render with groups preserved — walk filtered in order, emit a group header
  // when the group name changes.
  const rendered: ReactElement[] = [];
  let lastGroup = '';
  filtered.forEach((c, i) => {
    if (c.group !== lastGroup) {
      rendered.push(
        <div key={`g-${c.group}`} style={{
          padding: '8px 18px 4px',
          color: 'var(--t3)',
          fontSize: 10,
          fontWeight: 700,
          letterSpacing: '0.08em',
          textTransform: 'uppercase',
        }}>{c.group}</div>
      );
      lastGroup = c.group;
    }
    const active = i === idx;
    rendered.push(
      <div
        key={`c-${i}`}
        onMouseEnter={() => setIdx(i)}
        onClick={c.act}
        style={{
          padding: '8px 18px',
          cursor: 'pointer',
          fontSize: 13,
          color: 'var(--t1)',
          background: active ? 'var(--card-hover)' : 'transparent',
        }}
      >
        {c.label}
      </div>
    );
  });

  return (
    <div className="palette-backdrop" onClick={onClose}>
      <div className="palette" onClick={e => e.stopPropagation()}>
        <input
          ref={inputRef}
          className="palette-input"
          placeholder="Search commands, symbols, logins…"
          value={q}
          onChange={e => setQ(e.target.value)}
        />
        <div style={{ maxHeight: 400, overflow: 'auto', padding: '6px 0' }}>
          {filtered.length === 0 ? (
            <div style={{ padding: '16px 18px', color: 'var(--t3)', fontSize: 12 }}>
              No matches.
            </div>
          ) : rendered}
        </div>
      </div>
    </div>
  );
}
