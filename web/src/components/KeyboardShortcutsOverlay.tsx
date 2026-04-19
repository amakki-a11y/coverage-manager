import { useEffect, useState } from 'react';
import { THEME } from '../theme';

/**
 * Keyboard shortcut cheat-sheet overlay. Mount once near the app root; it
 * toggles itself via `?` (or `Shift+/`) and closes on Escape.
 *
 * Adding new shortcuts? Update the `SHORTCUTS` list below AND the relevant
 * component's listener — this is documentation, not the actual handler.
 */
interface Shortcut {
  key: string;
  label: string;
  context: string;
}

const SHORTCUTS: Shortcut[] = [
  { key: 'T', label: 'Today',                 context: 'Date range' },
  { key: 'Y', label: 'Yesterday',             context: 'Date range' },
  { key: 'W', label: 'This week (Mon-today)', context: 'Date range' },
  { key: 'M', label: 'Month-to-date',         context: 'Date range' },
  { key: '?', label: 'Show / hide this help', context: 'Global' },
  { key: 'Esc', label: 'Close modals / help', context: 'Global' },
];

export function KeyboardShortcutsOverlay() {
  const [open, setOpen] = useState(false);

  useEffect(() => {
    const isEditable = (el: EventTarget | null): boolean => {
      if (!(el instanceof HTMLElement)) return false;
      if (el.isContentEditable) return true;
      const tag = el.tagName;
      return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT';
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && open) { setOpen(false); return; }
      if (isEditable(e.target)) return;
      if (e.key === '?' || (e.key === '/' && e.shiftKey)) {
        e.preventDefault();
        setOpen(o => !o);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open]);

  if (!open) return null;

  const byContext = new Map<string, Shortcut[]>();
  for (const s of SHORTCUTS) {
    const arr = byContext.get(s.context) ?? [];
    arr.push(s);
    byContext.set(s.context, arr);
  }

  return (
    <div
      onClick={() => setOpen(false)}
      style={{
        position: 'fixed',
        inset: 0,
        background: THEME.shadowOverlay,
        zIndex: 10_001,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      <div
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-label="Keyboard shortcuts"
        style={{
          background: THEME.bg2,
          border: `1px solid ${THEME.border}`,
          borderRadius: 8,
          padding: 24,
          minWidth: 360,
          maxWidth: 520,
          boxShadow: THEME.shadowModal,
        }}
      >
        <div style={{ color: THEME.t1, fontSize: 15, fontWeight: 700, marginBottom: 16 }}>
          Keyboard shortcuts
        </div>
        {[...byContext.entries()].map(([ctx, items]) => (
          <div key={ctx} style={{ marginBottom: 14 }}>
            <div style={{
              fontSize: 10,
              color: THEME.t3,
              textTransform: 'uppercase',
              letterSpacing: 0.5,
              fontWeight: 700,
              marginBottom: 6,
            }}>
              {ctx}
            </div>
            {items.map(s => (
              <div
                key={s.key}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  padding: '4px 0',
                  fontSize: 13,
                }}
              >
                <kbd style={{
                  background: THEME.bg3,
                  border: `1px solid ${THEME.border}`,
                  borderRadius: 4,
                  padding: '2px 8px',
                  fontSize: 11,
                  fontFamily: "'JetBrains Mono', ui-monospace, Menlo, monospace",
                  color: THEME.t1,
                  minWidth: 34,
                  textAlign: 'center',
                  marginRight: 12,
                }}>
                  {s.key}
                </kbd>
                <span style={{ color: THEME.t2 }}>{s.label}</span>
              </div>
            ))}
          </div>
        ))}
        <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
          <button
            onClick={() => setOpen(false)}
            style={{
              padding: '6px 14px',
              borderRadius: 4,
              border: `1px solid ${THEME.border}`,
              background: 'transparent',
              color: THEME.t2,
              cursor: 'pointer',
              fontSize: 12,
              fontWeight: 600,
            }}
          >
            Close (Esc)
          </button>
        </div>
      </div>
    </div>
  );
}
