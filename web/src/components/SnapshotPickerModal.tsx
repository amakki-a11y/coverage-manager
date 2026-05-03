import { useEffect, useState, useCallback } from 'react';
import { THEME } from '../theme';
import { API_BASE } from '../config';
import type { ExposureSnapshot } from '../types';

/**
 * Modal that lists historical exposure_snapshots capture events grouped by
 * `snapshot_time` (rounded to the second), so the dealer can override the
 * Net P&L tab's BEGIN anchor with a specific snapshot instant.
 *
 * UX:
 *  - Fetches /api/exposure/snapshots for the last ~60 days
 *  - Groups all rows that share a snapshot_time second (one capture event
 *    typically writes one row per active canonical_symbol with the same
 *    timestamp ± a few hundred microseconds)
 *  - Shows: timestamp (UTC + Beirut), trigger_type, label, symbol count
 *  - User clicks a row → onPick fires with the chosen UTC ISO string,
 *    PeriodPnLPanel sends it as ?anchorOverrideUtc=... on the next fetch
 *  - "Use auto (from-date midnight Beirut)" row at the top resets the
 *    override and falls back to the default behavior
 *
 * Keyboard:
 *  - Escape closes
 *  - Enter on a focused row picks it
 */
interface SnapshotPickerModalProps {
  open: boolean;
  onClose: () => void;
  /**
   * Called when the dealer picks a snapshot. `null` means "reset to auto"
   * (use the from-date midnight-Beirut default).
   */
  onPick: (anchorUtcIso: string | null) => void;
  currentAnchorUtcIso: string | null;
  /** Default search range (last 60 days) — overridable later if needed. */
  daysBack?: number;
}

interface SnapshotEvent {
  /** Rounded-to-second ISO timestamp (UTC) — used purely for display + grouping bucket key. */
  utcIso: string;
  /**
   * Exact UTC ISO with sub-second precision — sent to backend as anchorOverrideUtc.
   * We send the LATEST snapshot_time in the bucket so the backend's
   * `snapshot_time <= anchor` filter always includes every row in this bucket.
   */
  pickIso: string;
  /** Display copy. */
  utcLabel: string;
  beirutLabel: string;
  /** The trigger_type of the rows in this group (daily / weekly / monthly / manual / scheduled). */
  triggerType: string;
  /** The first non-empty `label` in the group (or empty string). */
  label: string;
  /** Number of distinct canonical symbols captured at this instant. */
  symbolCount: number;
}

/**
 * Group raw snapshot rows by `snapshot_time` rounded to the nearest second.
 *
 * <p><b>pickIso preserves microsecond precision verbatim from the database
 * string</b> — JS <code>Date.toISOString()</code> drops microseconds (only
 * keeps milliseconds), but Postgres stores 6-digit fractional seconds and
 * the backend filter <code>snapshot_time &lt;= anchor</code> compares with
 * full precision. Passing the millisecond-truncated value would exclude
 * the very snapshots in the bucket (a 50.428046 row would not satisfy
 * <code>&lt;= 50.428000</code>). Passing the raw API string keeps
 * microseconds intact and the backend's DateTime binder parses it correctly
 * via ISO 8601 with offset.</p>
 *
 * <p>Within a bucket we keep the lexicographically LATEST raw string as
 * <code>pickIso</code> — the timestamps share the same timezone offset
 * (all from the same Postgres source), so lex order matches chronological
 * order within the bucket.</p>
 */
function groupSnapshots(rows: ExposureSnapshot[]): SnapshotEvent[] {
  const buckets = new Map<string, SnapshotEvent>();
  for (const r of rows) {
    if (!r.snapshot_time) continue;
    const d = new Date(r.snapshot_time);
    if (isNaN(d.getTime())) continue;
    // Drop sub-second precision so all rows from the same capture event group together.
    const secondTruncated = new Date(Math.floor(d.getTime() / 1000) * 1000);
    const utcIso = secondTruncated.toISOString();
    // pickIso = raw API string, preserves microseconds. Backend parses with DateTime binder.
    const existing = buckets.get(utcIso);
    if (existing) {
      existing.symbolCount++;
      if (!existing.label && r.label) existing.label = r.label;
      // Lex compare — same TZ offset for all rows from the same source, so it's chronological.
      if (r.snapshot_time > existing.pickIso) existing.pickIso = r.snapshot_time;
    } else {
      const utcLabel = utcIso.replace('T', ' ').replace(/\.\d+Z$/, 'Z');
      const beirutLabel = formatBeirut(secondTruncated);
      buckets.set(utcIso, {
        utcIso,
        pickIso: r.snapshot_time,
        utcLabel,
        beirutLabel,
        triggerType: r.trigger_type ?? 'unknown',
        label: r.label ?? '',
        symbolCount: 1,
      });
    }
  }
  return Array.from(buckets.values()).sort((a, b) => b.utcIso.localeCompare(a.utcIso));
}

function formatBeirut(d: Date): string {
  // Asia/Beirut is UTC+2 (standard) or UTC+3 (DST). Use Intl for correctness.
  try {
    return new Intl.DateTimeFormat('en-GB', {
      timeZone: 'Asia/Beirut',
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
    }).format(d).replace(',', '');
  } catch {
    return d.toISOString();
  }
}

const TRIGGER_COLORS: Record<string, string> = {
  daily: '#60A5FA',
  weekly: '#A78BFA',
  monthly: '#F59E0B',
  manual: '#10B981',
  scheduled: '#94A3B8',
  unknown: '#6B7280',
};

export function SnapshotPickerModal({ open, onClose, onPick, currentAnchorUtcIso, daysBack = 60 }: SnapshotPickerModalProps) {
  const [events, setEvents] = useState<SnapshotEvent[]>([]);
  const [loading, setLoading] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setErr(null);
    try {
      const to = new Date();
      const from = new Date(to.getTime() - daysBack * 86400_000);
      const params = new URLSearchParams({
        from: from.toISOString(),
        to: to.toISOString(),
      });
      const res = await fetch(`${API_BASE}/api/exposure/snapshots?${params}`);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const rows: ExposureSnapshot[] = await res.json();
      setEvents(groupSnapshots(rows));
    } catch (e) {
      setErr(e instanceof Error ? e.message : 'fetch failed');
    } finally {
      setLoading(false);
    }
  }, [daysBack]);

  useEffect(() => {
    if (open) load();
  }, [open, load]);

  // Esc to close
  useEffect(() => {
    if (!open) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [open, onClose]);

  if (!open) return null;

  // Round the current anchor for comparison highlighting.
  const currentRoundedIso = currentAnchorUtcIso
    ? new Date(Math.floor(new Date(currentAnchorUtcIso).getTime() / 1000) * 1000).toISOString()
    : null;

  return (
    <div
      onClick={onClose}
      style={{
        position: 'fixed',
        inset: 0,
        background: 'rgba(0,0,0,0.6)',
        zIndex: 1000,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      <div
        onClick={e => e.stopPropagation()}
        style={{
          background: THEME.bg2 ?? '#10141B',
          border: `1px solid ${THEME.border}`,
          borderRadius: 8,
          padding: 0,
          width: 'min(720px, 90vw)',
          maxHeight: '80vh',
          display: 'flex',
          flexDirection: 'column',
          boxShadow: '0 20px 60px rgba(0,0,0,0.5)',
        }}
      >
        {/* Header */}
        <div style={{
          padding: '14px 18px',
          borderBottom: `1px solid ${THEME.border}`,
          display: 'flex',
          alignItems: 'center',
          gap: 12,
        }}>
          <div style={{ flex: 1 }}>
            <div style={{ fontSize: 14, fontWeight: 700, color: THEME.t1 }}>Pick BEGIN snapshot</div>
            <div style={{ fontSize: 11, color: THEME.t3, marginTop: 3 }}>
              Each row is one snapshot-capture event. Picking one anchors Net P&amp;L's BEGIN to that instant.
            </div>
          </div>
          <button
            onClick={onClose}
            style={{
              background: 'transparent',
              border: `1px solid ${THEME.border}`,
              color: THEME.t2,
              padding: '4px 10px',
              borderRadius: 6,
              cursor: 'pointer',
              fontSize: 12,
            }}
          >
            Esc
          </button>
        </div>

        {/* Body */}
        <div style={{ flex: 1, overflow: 'auto' }}>
          {loading && (
            <div style={{ padding: 24, textAlign: 'center', color: THEME.t3, fontSize: 12 }}>
              Loading snapshot history…
            </div>
          )}
          {err && (
            <div style={{ padding: 16, color: THEME.red, fontSize: 12 }}>
              Error: {err}
            </div>
          )}
          {!loading && !err && (
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
              <thead>
                <tr style={{ position: 'sticky', top: 0, background: THEME.bg2 ?? '#10141B', zIndex: 1 }}>
                  <th style={thStyle}>UTC</th>
                  <th style={thStyle}>Asia/Beirut</th>
                  <th style={thStyle}>Trigger</th>
                  <th style={thStyle}>Label</th>
                  <th style={{ ...thStyle, textAlign: 'right' }}># Symbols</th>
                </tr>
              </thead>
              <tbody>
                {/* Reset / use auto row */}
                <tr
                  onClick={() => { onPick(null); onClose(); }}
                  style={{
                    cursor: 'pointer',
                    background: currentRoundedIso === null ? 'rgba(96,165,250,0.10)' : 'transparent',
                  }}
                  onMouseEnter={e => (e.currentTarget.style.background = 'rgba(255,255,255,0.04)')}
                  onMouseLeave={e => (e.currentTarget.style.background = currentRoundedIso === null ? 'rgba(96,165,250,0.10)' : 'transparent')}
                >
                  <td style={tdStyle} colSpan={5}>
                    <span style={{ color: THEME.blue, fontWeight: 600 }}>↩ Use auto anchor</span>
                    <span style={{ color: THEME.t3, marginLeft: 8 }}>(from-date 00:00 Asia/Beirut → UTC; default behavior)</span>
                  </td>
                </tr>
                {events.length === 0 && (
                  <tr>
                    <td style={{ ...tdStyle, color: THEME.t3, textAlign: 'center', padding: 24 }} colSpan={5}>
                      No snapshots in the last {daysBack} days.
                    </td>
                  </tr>
                )}
                {events.map(ev => {
                  const isCurrent = currentRoundedIso === ev.utcIso;
                  return (
                    <tr
                      key={ev.utcIso}
                      onClick={() => { onPick(ev.pickIso); onClose(); }}
                      style={{
                        cursor: 'pointer',
                        background: isCurrent ? 'rgba(96,165,250,0.10)' : 'transparent',
                      }}
                      onMouseEnter={e => (e.currentTarget.style.background = 'rgba(255,255,255,0.04)')}
                      onMouseLeave={e => (e.currentTarget.style.background = isCurrent ? 'rgba(96,165,250,0.10)' : 'transparent')}
                    >
                      <td style={tdStyle}>
                        <code style={{ color: isCurrent ? THEME.blue : THEME.t1 }}>{ev.utcLabel}</code>
                      </td>
                      <td style={{ ...tdStyle, color: THEME.t2 }}>{ev.beirutLabel}</td>
                      <td style={tdStyle}>
                        <span style={{
                          padding: '2px 8px',
                          borderRadius: 10,
                          fontSize: 10,
                          fontWeight: 700,
                          background: 'rgba(255,255,255,0.06)',
                          color: TRIGGER_COLORS[ev.triggerType] ?? THEME.t2,
                          textTransform: 'uppercase',
                          letterSpacing: 0.5,
                        }}>
                          {ev.triggerType}
                        </span>
                      </td>
                      <td style={{ ...tdStyle, color: THEME.t3, fontSize: 11 }}>
                        {ev.label || '—'}
                      </td>
                      <td style={{ ...tdStyle, textAlign: 'right', color: THEME.t2 }}>
                        {ev.symbolCount}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>

        {/* Footer */}
        <div style={{
          padding: '10px 18px',
          borderTop: `1px solid ${THEME.border}`,
          fontSize: 11,
          color: THEME.t3,
          display: 'flex',
          justifyContent: 'space-between',
        }}>
          <span>{events.length} snapshot events · last {daysBack} days</span>
          <span>Esc to close</span>
        </div>
      </div>
    </div>
  );
}

const thStyle: React.CSSProperties = {
  padding: '8px 12px',
  textAlign: 'left',
  fontSize: 10,
  fontWeight: 700,
  color: '#94A3B8',
  textTransform: 'uppercase',
  letterSpacing: 0.5,
  borderBottom: '1px solid rgba(255,255,255,0.07)',
  background: '#10141B',
};

const tdStyle: React.CSSProperties = {
  padding: '8px 12px',
  borderBottom: '1px solid rgba(255,255,255,0.04)',
  fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace",
};
