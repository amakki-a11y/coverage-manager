import { useEffect, useRef } from 'react';
import { THEME } from '../theme';
import { useDateRange } from '../hooks/useDateRange';

/**
 * Shared "from / to" date picker used across tabs. Renders the two native
 * date inputs plus a strip of presets (Today / Yesterday / Week / MTD / 7D /
 * 30D) and an "Asia/Beirut" TZ pill so the dealer never has to remember which
 * timezone the picker is interpreted in.
 *
 * Keyboard shortcuts (fire only when focus is NOT inside an input/textarea):
 *   T \u2192 Today
 *   Y \u2192 Yesterday
 *   W \u2192 This week (Mon..today)
 *   M \u2192 Month-to-date (1st..today)
 *
 * The component shares its state with every other tab via `useDateRange` — so
 * picking a preset here also updates the Exposure / P&L / Net P&L / Compare
 * tabs instantly.
 */

function toISO(d: Date): string {
  // Local (Beirut-aligned via `date` input semantics) YYYY-MM-DD. Using the
  // built-in toISOString would shift by up to 3h into the past on Lebanon-time
  // Sundays — explicitly build from the local parts instead.
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

function today(): string { return toISO(new Date()); }

function addDays(base: Date, days: number): Date {
  const d = new Date(base);
  d.setDate(d.getDate() + days);
  return d;
}

function mondayOf(d: Date): Date {
  const base = new Date(d);
  const dow = base.getDay(); // 0=Sun..6=Sat
  const diff = dow === 0 ? -6 : 1 - dow;
  return addDays(base, diff);
}

function firstOfMonth(d: Date): Date {
  return new Date(d.getFullYear(), d.getMonth(), 1);
}

interface Props {
  /** Keyed namespace for the localStorage key. Shared picker by default. */
  compact?: boolean;
}

export function DateRangePicker({ compact = false }: Props) {
  const [from, to, setFrom, setTo] = useDateRange();
  const rootRef = useRef<HTMLDivElement | null>(null);

  // Keyboard shortcuts. We guard against firing while the user is typing inside
  // ANY input/textarea/contenteditable on the page — not just this component —
  // because the mapping admin form and confirm-dialog modals can also capture
  // letters on key repeat.
  useEffect(() => {
    const isEditable = (el: EventTarget | null): boolean => {
      if (!(el instanceof HTMLElement)) return false;
      if (el.isContentEditable) return true;
      const tag = el.tagName;
      return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT';
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.ctrlKey || e.metaKey || e.altKey) return;
      if (isEditable(e.target)) return;
      const now = new Date();
      switch (e.key.toLowerCase()) {
        case 't': setFrom(today()); setTo(today()); break;
        case 'y': {
          const y = toISO(addDays(now, -1));
          setFrom(y); setTo(y);
          break;
        }
        case 'w': setFrom(toISO(mondayOf(now))); setTo(today()); break;
        case 'm': setFrom(toISO(firstOfMonth(now))); setTo(today()); break;
        default: return;
      }
      e.preventDefault();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [setFrom, setTo]);

  const preset = (label: string, fromDate: string, toDate: string, hotkey?: string) => {
    const active = from === fromDate && to === toDate;
    return (
      <button
        key={label}
        onClick={() => { setFrom(fromDate); setTo(toDate); }}
        title={hotkey ? `Shortcut: ${hotkey.toUpperCase()}` : undefined}
        style={{
          padding: '3px 8px',
          borderRadius: 4,
          border: `1px solid ${active ? THEME.blue : THEME.border}`,
          background: active ? THEME.badgeBlue : 'transparent',
          color: active ? THEME.blue : THEME.t2,
          cursor: 'pointer',
          fontSize: 11,
          fontWeight: 600,
        }}
      >
        {label}
      </button>
    );
  };

  const now = new Date();
  const t = today();
  const yesterday = toISO(addDays(now, -1));
  const weekStart = toISO(mondayOf(now));
  const monthStart = toISO(firstOfMonth(now));
  const sevenAgo = toISO(addDays(now, -6));
  const thirtyAgo = toISO(addDays(now, -29));

  return (
    <div
      ref={rootRef}
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: compact ? 6 : 10,
        flexWrap: 'wrap',
      }}
    >
      <input
        type="date"
        value={from}
        max={to}
        onChange={(e) => setFrom(e.target.value)}
        style={{
          background: THEME.bg,
          border: `1px solid ${THEME.border}`,
          color: THEME.t1,
          padding: '3px 6px',
          borderRadius: 4,
          fontSize: 12,
          fontFamily: "'JetBrains Mono', ui-monospace, Menlo, monospace",
        }}
      />
      <span style={{ color: THEME.t3, fontSize: 11 }}>to</span>
      <input
        type="date"
        value={to}
        min={from}
        onChange={(e) => setTo(e.target.value)}
        style={{
          background: THEME.bg,
          border: `1px solid ${THEME.border}`,
          color: THEME.t1,
          padding: '3px 6px',
          borderRadius: 4,
          fontSize: 12,
          fontFamily: "'JetBrains Mono', ui-monospace, Menlo, monospace",
        }}
      />

      {!compact && (
        <>
          <div style={{ width: 1, height: 18, background: THEME.border }} />
          {preset('Today',     t,           t,  't')}
          {preset('Yesterday', yesterday,   yesterday, 'y')}
          {preset('Week',      weekStart,   t,  'w')}
          {preset('MTD',       monthStart,  t,  'm')}
          {preset('7D',        sevenAgo,    t)}
          {preset('30D',       thirtyAgo,   t)}
        </>
      )}

      <span
        title="Date pickers are interpreted in Asia/Beirut (MT5 server timezone). Database stores UTC; conversion happens server-side."
        style={{
          marginLeft: compact ? 4 : 8,
          padding: '2px 6px',
          fontSize: 9,
          fontWeight: 700,
          letterSpacing: 0.4,
          color: THEME.teal,
          background: THEME.bg3,
          border: `1px solid ${THEME.border}`,
          borderRadius: 3,
          textTransform: 'uppercase',
          fontFamily: "'JetBrains Mono', ui-monospace, Menlo, monospace",
        }}
      >
        Beirut
      </span>
    </div>
  );
}
