import { createContext, useCallback, useContext, useEffect, useState, useRef } from 'react';
import { THEME } from '../theme';

/**
 * Global, throttled error toast — shown at the bottom-right when any fetch or
 * WebSocket call raises. Components opt in by calling `useErrorToast()` once
 * and invoking `showError('context', err)` wherever they currently swallow
 * the error in a `catch { /* ignore *\/ }` block.
 *
 * De-duplication: identical messages in a 3-second window collapse into one
 * toast so a repeated 500 from the API doesn't stack dozens of identical
 * notifications while the user is trying to read.
 */

interface ErrorToastMsg {
  id: string;
  label: string;   // short tag for the source (e.g. "Exposure")
  message: string;
  createdAt: number;
}

interface ErrorToastCtx {
  showError: (label: string, err: unknown) => void;
}

const Ctx = createContext<ErrorToastCtx | null>(null);

const DEDUPE_WINDOW_MS = 3000;
const AUTO_DISMISS_MS = 6000;

function messageFrom(err: unknown): string {
  if (err instanceof Error) return err.message;
  if (typeof err === 'string') return err;
  try { return JSON.stringify(err); } catch { return 'Unknown error'; }
}

export function ErrorToastProvider({ children }: { children: React.ReactNode }) {
  const [toasts, setToasts] = useState<ErrorToastMsg[]>([]);
  const recentRef = useRef<Map<string, number>>(new Map());

  const showError = useCallback((label: string, err: unknown) => {
    const message = messageFrom(err);
    const key = `${label}::${message}`;
    const now = Date.now();
    const last = recentRef.current.get(key) ?? 0;
    if (now - last < DEDUPE_WINDOW_MS) return;
    recentRef.current.set(key, now);

    const id = `${now}-${Math.random().toString(36).slice(2, 7)}`;
    setToasts(prev => [{ id, label, message, createdAt: now }, ...prev].slice(0, 4));
    setTimeout(() => {
      setToasts(prev => prev.filter(t => t.id !== id));
    }, AUTO_DISMISS_MS);
  }, []);

  // Garbage-collect the dedupe map every 30s so we don't leak.
  useEffect(() => {
    const i = setInterval(() => {
      const cutoff = Date.now() - DEDUPE_WINDOW_MS * 2;
      for (const [k, t] of recentRef.current) {
        if (t < cutoff) recentRef.current.delete(k);
      }
    }, 30_000);
    return () => clearInterval(i);
  }, []);

  return (
    <Ctx.Provider value={{ showError }}>
      {children}
      <div style={{
        position: 'fixed',
        bottom: 16,
        right: 16,
        zIndex: 10_000,
        display: 'flex',
        flexDirection: 'column',
        gap: 8,
        maxWidth: 420,
      }}>
        {toasts.map(t => (
          <div
            key={t.id}
            style={{
              background: THEME.card,
              border: `1px solid ${THEME.red}`,
              borderLeft: `4px solid ${THEME.red}`,
              borderRadius: 6,
              padding: '10px 14px',
              boxShadow: THEME.shadow,
              animation: 'slideInUp 0.25s ease',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 3 }}>
              <span style={{ fontSize: 13 }}>{'\u26A0'}</span>
              <span style={{
                fontSize: 11,
                fontWeight: 700,
                textTransform: 'uppercase',
                letterSpacing: 0.5,
                color: THEME.red,
              }}>
                {t.label}
              </span>
              <button
                onClick={() => setToasts(prev => prev.filter(x => x.id !== t.id))}
                style={{
                  marginLeft: 'auto',
                  background: 'none',
                  border: 'none',
                  color: THEME.t3,
                  cursor: 'pointer',
                  fontSize: 14,
                  lineHeight: 1,
                }}
              >
                {'\u00D7'}
              </button>
            </div>
            <div style={{ fontSize: 12, color: THEME.t1, lineHeight: 1.4, wordBreak: 'break-word' }}>
              {t.message}
            </div>
          </div>
        ))}
      </div>
      <style>{`
        @keyframes slideInUp {
          from { opacity: 0; transform: translateY(16px); }
          to { opacity: 1; transform: translateY(0); }
        }
      `}</style>
    </Ctx.Provider>
  );
}

/**
 * Returns the global `showError` function. Safe to call from any component
 * rendered inside `<ErrorToastProvider>`. No-ops silently if the provider
 * isn't mounted (tests, Storybook).
 */
export function useErrorToast(): ErrorToastCtx {
  const v = useContext(Ctx);
  return v ?? { showError: () => {} };
}
