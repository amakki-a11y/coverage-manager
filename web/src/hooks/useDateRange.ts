import { useEffect, useState } from 'react';

const KEY_FROM = 'dashboard.dateRange.from';
const KEY_TO   = 'dashboard.dateRange.to';

const todayStr = () => new Date().toISOString().slice(0, 10);

function readInitial(key: string): string {
  if (typeof localStorage === 'undefined') return todayStr();
  const v = localStorage.getItem(key);
  // Accept YYYY-MM-DD only — reject legacy / corrupt values silently.
  return v && /^\d{4}-\d{2}-\d{2}$/.test(v) ? v : todayStr();
}

/**
 * Shared date-range state persisted to localStorage. All tabs using this hook see
 * the same range; changing it in one tab (Exposure / P&L / Net P&L) updates the
 * others immediately via a 'storage' event, so switching tabs no longer resets
 * the picker to today.
 *
 * @returns `[from, to, setFrom, setTo]` — `from` / `to` are `YYYY-MM-DD` strings.
 */
export function useDateRange(): [string, string, (v: string) => void, (v: string) => void] {
  const [from, setFromRaw] = useState<string>(() => readInitial(KEY_FROM));
  const [to,   setToRaw]   = useState<string>(() => readInitial(KEY_TO));

  const setFrom = (v: string) => {
    setFromRaw(v);
    try { localStorage.setItem(KEY_FROM, v); window.dispatchEvent(new StorageEvent('storage', { key: KEY_FROM, newValue: v })); } catch { /* ignore */ }
  };
  const setTo = (v: string) => {
    setToRaw(v);
    try { localStorage.setItem(KEY_TO, v); window.dispatchEvent(new StorageEvent('storage', { key: KEY_TO, newValue: v })); } catch { /* ignore */ }
  };

  useEffect(() => {
    const onStorage = (e: StorageEvent) => {
      if (e.key === KEY_FROM && e.newValue && /^\d{4}-\d{2}-\d{2}$/.test(e.newValue)) setFromRaw(e.newValue);
      if (e.key === KEY_TO   && e.newValue && /^\d{4}-\d{2}-\d{2}$/.test(e.newValue)) setToRaw(e.newValue);
    };
    window.addEventListener('storage', onStorage);
    return () => window.removeEventListener('storage', onStorage);
  }, []);

  return [from, to, setFrom, setTo];
}
