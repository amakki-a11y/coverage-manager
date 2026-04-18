/**
 * Asia/Beirut timezone helpers.
 *
 * The MT5 server displays deals in Asia/Beirut local time, and all date pickers
 * in the dashboard already interpret their input as Beirut midnight. UI timestamps
 * should therefore render in Beirut too, not in the browser's local zone — which
 * on the dealer's Windows machine defaults to PDT (−7h) and makes every displayed
 * time look "wrong" vs what they see in MT5 Manager.
 *
 * These formatters pin every render to `Asia/Beirut` via Intl.DateTimeFormat.
 */

const DEFAULT_TZ = 'Asia/Beirut';

function toDate(input: string | number | Date | null | undefined): Date | null {
  if (input == null) return null;
  if (input instanceof Date) return isFinite(input.getTime()) ? input : null;
  const d = new Date(input);
  return isFinite(d.getTime()) ? d : null;
}

/**
 * Format an absolute timestamp (UTC string, Date, or epoch ms) as `YYYY-MM-DD HH:mm:ss`
 * in Asia/Beirut. Returns `—` for null/invalid input.
 */
export function formatBeirut(input: string | number | Date | null | undefined): string {
  const d = toDate(input);
  if (!d) return '—';
  const parts = new Intl.DateTimeFormat('en-GB', {
    timeZone: DEFAULT_TZ,
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', second: '2-digit',
    hour12: false,
  }).formatToParts(d);
  const m: Record<string, string> = {};
  for (const p of parts) if (p.type !== 'literal') m[p.type] = p.value;
  return `${m.year}-${m.month}-${m.day} ${m.hour}:${m.minute}:${m.second}`;
}

/** Date-only `YYYY-MM-DD` in Asia/Beirut. Useful for header chips and grouping keys. */
export function formatBeirutDate(input: string | number | Date | null | undefined): string {
  const d = toDate(input);
  if (!d) return '—';
  const parts = new Intl.DateTimeFormat('en-GB', {
    timeZone: DEFAULT_TZ,
    year: 'numeric', month: '2-digit', day: '2-digit',
  }).formatToParts(d);
  const m: Record<string, string> = {};
  for (const p of parts) if (p.type !== 'literal') m[p.type] = p.value;
  return `${m.year}-${m.month}-${m.day}`;
}

/** Time-only `HH:mm:ss` in Asia/Beirut. Handy for intraday rows. */
export function formatBeirutTime(input: string | number | Date | null | undefined): string {
  const d = toDate(input);
  if (!d) return '—';
  return new Intl.DateTimeFormat('en-GB', {
    timeZone: DEFAULT_TZ,
    hour: '2-digit', minute: '2-digit', second: '2-digit',
    hour12: false,
  }).format(d);
}
