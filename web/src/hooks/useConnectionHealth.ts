import { useEffect, useState } from 'react';

/**
 * Polls the four upstream services every 5 s and surfaces their health so the
 * header can render a per-source status dot.
 *
 * - MT5        → /api/exposure/status           (mt5Connected)
 * - Collector  → http://localhost:8100/health   (status ∈ 'ok' | 'stale' | 'disconnected')
 * - Centroid   → /api/bridge/health             (state ∈ 'LoggedIn' | 'Connecting' | 'Error' | 'Stubbed' | 'Disconnected')
 * - Supabase   → derived: MT5 status responded 200 AND /api/mappings responded
 *                (if the API itself is up but Supa is down those endpoints throw 500s;
 *                so we track /api/mappings as the Supa canary)
 */
export interface ConnectionHealth {
  mt5:       Status;
  collector: Status;
  centroid:  Status;
  supabase:  Status;
}

export type Status = 'green' | 'amber' | 'red' | 'gray';

const API = 'http://localhost:5000';
const COLLECTOR = 'http://localhost:8100';

const INITIAL: ConnectionHealth = {
  mt5: 'gray', collector: 'gray', centroid: 'gray', supabase: 'gray',
};

export function useConnectionHealth(): ConnectionHealth {
  const [health, setHealth] = useState<ConnectionHealth>(INITIAL);

  useEffect(() => {
    let cancelled = false;
    let inFlight = false;

    async function poll() {
      if (inFlight) return;
      inFlight = true;
      const next: ConnectionHealth = { ...INITIAL };
      try {
        // MT5 + Supabase (both behind the same API)
        try {
          const r = await fetch(`${API}/api/exposure/status`);
          if (r.ok) {
            const j = await r.json();
            next.mt5 = j.mt5Connected ? 'green' : 'red';
          } else {
            next.mt5 = 'red';
          }
        } catch { next.mt5 = 'red'; }

        try {
          const r = await fetch(`${API}/api/mappings`);
          next.supabase = r.ok ? 'green' : 'red';
        } catch { next.supabase = 'red'; }

        // Collector
        try {
          const r = await fetch(`${COLLECTOR}/health`);
          if (r.ok) {
            const j = await r.json();
            next.collector =
              j.status === 'ok' ? 'green'
              : j.status === 'stale' ? 'amber'
              : 'red';
          } else {
            next.collector = 'red';
          }
        } catch { next.collector = 'red'; }

        // Centroid (Bridge feed)
        try {
          const r = await fetch(`${API}/api/bridge/health`);
          if (r.ok) {
            const j = await r.json();
            const s = j.state as string | undefined;
            next.centroid =
              s === 'LoggedIn' || s === 'Stubbed' ? 'green'
              : s === 'Connecting' ? 'amber'
              : s === 'Disconnected' ? 'gray'
              : 'red';
          } else {
            next.centroid = 'red';
          }
        } catch { next.centroid = 'red'; }
      } finally {
        if (!cancelled) setHealth(next);
        inFlight = false;
      }
    }

    poll();
    const id = setInterval(poll, 5000);
    return () => { cancelled = true; clearInterval(id); };
  }, []);

  return health;
}
