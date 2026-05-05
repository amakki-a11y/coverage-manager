import { useEffect, useReducer, useCallback, useRef } from 'react';
import type { ExposureSummary, PriceQuote, AlertEvent, WsMessage } from '../types';
import { WS_BASE } from '../config';

/**
 * Live-exposure WebSocket hook. Subscribes to the C# backend's `/ws` feed and
 * exposes the most-recent state to any consumer:
 *
 * - `exposureSummaries` — one row per canonical symbol (B-Book + Coverage + Summary).
 * - `prices`            — latest bid/ask per MT5 symbol (used by Exposure table + flashing cells).
 * - `connected`         — socket open/closed; drives the StaleWrapper diagonal-hatch overlay.
 * - `newAlerts`         — queue of alerts not yet shown to the user (consumed by AlertToast).
 * - `alertCount`        — unacknowledged count (drives the bell badge).
 *
 * Backend throttles the feed to ~10 msg/sec. Reconnects with linear backoff
 * (1 s → 10 s cap) if the socket drops. Uses `useReducer` so multiple message
 * types arriving in one React batch don't tear state.
 *
 * @returns State snapshot plus `acknowledgeAlert(id)` for clearing a toast.
 */
/**
 * Per-canonical-symbol settled-P&L delta accumulator. Each entry tracks the sum
 * of `settledDelta` from all `deal_settled` WS frames received since the last
 * REST refresh, plus the set of `dealId`s included so a hard refresh can prove
 * which deals are already in the REST snapshot.
 *
 * <p>Phase 2.19. Frontend overlays this onto cached `PeriodPnLResponse.rows[*].
 * bBook.settled` so the Net P&L tab's SETTLED column ticks within ~50 ms of a
 * deal close. Cleared by `clearSettledDeltas()` after each REST refresh.</p>
 */
export interface SettledDelta {
  bbook: number;
  coverage: number;
  dealIds: Set<string>;
}

interface State {
  exposureSummaries: ExposureSummary[];
  prices: PriceQuote[];
  connected: boolean;
  // Phase 2.20: true when the WS is technically open but no frame has arrived
  // for >LAG_WARNING_MS. Drives the StaleWrapper amber-hatch overlay so the
  // dealer notices stale data even when ws.onclose hasn't fired (e.g. silent
  // packet drop on the dealer's ISP path). At >FORCE_RECONNECT_MS the watchdog
  // calls ws.close() to trigger the existing reconnect path.
  lagging: boolean;
  newAlerts: AlertEvent[];
  alertCount: number;
  // Map<canonicalKey, SettledDelta> — accumulated deal_settled deltas waiting
  // to be reconciled by the next REST refresh. Each entry is one canonical
  // symbol; the dealIds set guards against double-counting if the same deal
  // event arrives twice (rare but possible across reconnect).
  pendingSettled: Map<string, SettledDelta>;
}

type Action =
  | { type: 'CONNECTED' }
  | { type: 'DISCONNECTED' }
  | { type: 'LAGGING'; payload: boolean }
  | { type: 'EXPOSURE_UPDATE'; payload: Extract<WsMessage, { type: 'exposure_update' }>['data'] }
  | { type: 'PRICE_UPDATE'; payload: Extract<WsMessage, { type: 'price_update' }>['data'] }
  | { type: 'DEAL_SETTLED'; payload: Extract<WsMessage, { type: 'deal_settled' }>['data'] }
  | { type: 'CLEAR_SETTLED_DELTAS' }
  | { type: 'CLEAR_NEW_ALERTS' };

function reducer(state: State, action: Action): State {
  switch (action.type) {
    case 'CONNECTED':
      // On reconnect the watchdog timestamp resets in the connect callback;
      // also clear `lagging` here so a stale overlay doesn't linger across
      // a reconnect that immediately resumes normal frame flow.
      return { ...state, connected: true, lagging: false };
    case 'DISCONNECTED':
      return { ...state, connected: false };
    case 'LAGGING':
      // Idempotent: same value -> return same state, no re-render. The watchdog
      // dispatches LAGGING every 1s so this guard avoids unnecessary churn.
      return state.lagging === action.payload ? state : { ...state, lagging: action.payload };
    case 'EXPOSURE_UPDATE':
      return {
        ...state,
        exposureSummaries: action.payload.exposure,
        prices: action.payload.prices,
        newAlerts: action.payload.alerts ?? [],
        alertCount: action.payload.alertCount ?? 0,
      };
    case 'PRICE_UPDATE': {
      // Phase 2.16: prices array updated at full tick cadence (~20 Hz).
      // Phase 2.17: floatingPnls (when present) overlays per-symbol B-Book /
      // Coverage / Net P&L onto exposureSummaries so the Exposure open-row
      // P&L cells, Net P&L tab "Current Floating", and Topbar "Net P&L
      // Today" tile all tick at the same 20 Hz cadence as the bid price.
      // Volumes / avg prices / hedge ratio stay frozen between exposure_update
      // frames — they only change when positions change, not when prices tick.
      const floating = action.payload.floatingPnls;
      if (!floating || floating.length === 0) {
        return { ...state, prices: action.payload.prices };
      }
      const liveBySymbol = new Map<string, { bBook: number; coverage: number }>();
      for (const f of floating) {
        const k = (f.canonicalSymbol || '').toUpperCase();
        if (k) liveBySymbol.set(k, { bBook: f.bBook ?? 0, coverage: f.coverage ?? 0 });
      }
      const overlaid = state.exposureSummaries.map(s => {
        const live = liveBySymbol.get((s.canonicalSymbol || '').toUpperCase());
        if (!live) return s;
        const bBookPnL = live.bBook;
        const coveragePnL = live.coverage;
        // netPnL convention: broker's edge on currently-open positions.
        // ExposureEngine ships this as `−bBookPnL + coveragePnL`. Mirror it
        // here so the Topbar tile + Exposure summary column tick correctly.
        const netPnL = -bBookPnL + coveragePnL;
        return { ...s, bBookPnL, coveragePnL, netPnL };
      });
      return {
        ...state,
        prices: action.payload.prices,
        exposureSummaries: overlaid,
      };
    }
    case 'DEAL_SETTLED': {
      // Accumulate per-canonical-symbol settled delta. Same dealId arriving
      // twice is a no-op (guards against duplicate emit on reconnect).
      const k = (action.payload.canonicalKey || '').toUpperCase();
      if (!k) return state;
      const next = new Map(state.pendingSettled);
      const cur = next.get(k) ?? { bbook: 0, coverage: 0, dealIds: new Set<string>() };
      if (cur.dealIds.has(action.payload.dealId)) return state;
      const deltaDealIds = new Set(cur.dealIds);
      deltaDealIds.add(action.payload.dealId);
      const isCov = action.payload.source === 'coverage';
      next.set(k, {
        bbook:    isCov ? cur.bbook    : cur.bbook    + (action.payload.settledDelta ?? 0),
        coverage: isCov ? cur.coverage + (action.payload.settledDelta ?? 0) : cur.coverage,
        dealIds:  deltaDealIds,
      });
      return { ...state, pendingSettled: next };
    }
    case 'CLEAR_SETTLED_DELTAS':
      // Called by panel after each REST refresh — REST result is now authoritative
      // for everything in Supabase, so reset the live overlay. New deal_settled
      // frames arriving after this point start a fresh accumulator.
      return state.pendingSettled.size === 0 ? state : { ...state, pendingSettled: new Map() };
    case 'CLEAR_NEW_ALERTS':
      return { ...state, newAlerts: [] };
    default:
      return state;
  }
}

const initialState: State = {
  exposureSummaries: [],
  prices: [],
  connected: false,
  lagging: false,
  newAlerts: [],
  alertCount: 0,
  pendingSettled: new Map(),
};

const WS_URL = `${WS_BASE}/ws/exposure`;
const MIN_RECONNECT_DELAY = 1000;
const MAX_RECONNECT_DELAY = 10000;
// Phase 2.20 — frame-lag watchdog thresholds. The backend's broadcast cadence
// during market hours is sub-second (price_update ~20 Hz, exposure_update ~10
// Hz). 5 s without ANY frame is the empirical "something is wrong" mark.
// Fully-quiet weekend periods can produce tens of seconds of silence; that
// triggers the amber overlay + a force-reconnect cycle, which is harmless
// (reconnect is fast, server is local) but does mean the watchdog is noisy
// when markets are dead. Acceptable trade-off vs the silent-staleness bug.
const LAG_WARNING_MS = 5_000;
const FORCE_RECONNECT_MS = 15_000;
const WATCHDOG_INTERVAL_MS = 1_000;

export function useExposureSocket() {
  const [state, dispatch] = useReducer(reducer, initialState);
  const wsRef = useRef<WebSocket | null>(null);
  const reconnectDelay = useRef(MIN_RECONNECT_DELAY);
  // Phase 2.20: timestamp (ms) of the most recent frame received on the
  // current connection. The watchdog interval below compares this against
  // wall-clock to detect silent-stall scenarios that ws.onclose misses (e.g.
  // ISP-level packet drops, NAT timeout, browser tab throttling). Reset on
  // every onmessage AND on every successful onopen.
  const lastFrameAtRef = useRef<number>(Date.now());

  const connect = useCallback(() => {
    if (wsRef.current?.readyState === WebSocket.OPEN) return;

    const ws = new WebSocket(WS_URL);
    wsRef.current = ws;

    ws.onopen = () => {
      dispatch({ type: 'CONNECTED' });
      reconnectDelay.current = MIN_RECONNECT_DELAY;
      lastFrameAtRef.current = Date.now();
    };

    ws.onmessage = (event) => {
      // Bump the watchdog timestamp BEFORE parsing — a malformed message that
      // throws below is still proof the socket is alive, so it should not
      // count as silence.
      lastFrameAtRef.current = Date.now();
      try {
        const message = JSON.parse(event.data) as WsMessage;
        if (message.type === 'exposure_update') {
          dispatch({ type: 'EXPOSURE_UPDATE', payload: message.data });
        } else if (message.type === 'price_update') {
          dispatch({ type: 'PRICE_UPDATE', payload: message.data });
        } else if (message.type === 'deal_settled') {
          dispatch({ type: 'DEAL_SETTLED', payload: message.data });
        }
      } catch (err) {
        // Upstream protocol break: log the first bytes so the backend team sees it
        // instead of silently dropping exposure updates.
        const preview = typeof event.data === 'string' ? event.data.slice(0, 200) : '(non-string)';
        console.warn('[ws/exposure] malformed message:', err, 'payload=', preview);
      }
    };

    ws.onclose = () => {
      dispatch({ type: 'DISCONNECTED' });
      setTimeout(() => {
        reconnectDelay.current = Math.min(reconnectDelay.current * 1.5, MAX_RECONNECT_DELAY);
        connect();
      }, reconnectDelay.current);
    };

    ws.onerror = () => {
      ws.close();
    };
  }, []);

  useEffect(() => {
    connect();

    // Phase 2.20 — frame-lag watchdog.
    //
    // Why this exists: the previous implementation only treated `connected:
    // false` (ws.onclose) as a stale-data signal. But on real dealer networks
    // a WebSocket can stay in readyState OPEN while the ISP / NAT / proxy
    // silently stops forwarding frames (most commonly on long-running tabs
    // backgrounded for hours). The dashboard then renders whatever was last
    // received, with no visual cue, until someone notices the volumes don't
    // match another device. Production reproducer: 2026-05-04, dealer's
    // laptop showed XAUUSD net 22.65 lots while the server-side Chrome and
    // backend ground truth were both ~21 lots; alert badge differed 16 vs
    // 34. ws.onclose never fired on the dealer's machine.
    //
    // Mitigation: every WATCHDOG_INTERVAL_MS, compare wall-clock against the
    // last-frame timestamp. >LAG_WARNING_MS dims the UI via StaleWrapper,
    // >FORCE_RECONNECT_MS hard-closes the socket so the existing reconnect
    // path fires. A successful reconnect resets the timestamp and clears
    // `lagging` (see CONNECTED reducer case).
    const watchdog = window.setInterval(() => {
      const ws = wsRef.current;
      if (!ws || ws.readyState !== WebSocket.OPEN) return;
      const sinceLast = Date.now() - lastFrameAtRef.current;
      if (sinceLast > FORCE_RECONNECT_MS) {
        // Reset the timestamp before close() so the next interval (which may
        // fire before onclose) doesn't see the old gap and double-trigger.
        console.warn(`[ws/exposure] no frame for ${sinceLast} ms — forcing reconnect`);
        lastFrameAtRef.current = Date.now();
        ws.close();
      } else {
        dispatch({ type: 'LAGGING', payload: sinceLast > LAG_WARNING_MS });
      }
    }, WATCHDOG_INTERVAL_MS);

    return () => {
      wsRef.current?.close();
      window.clearInterval(watchdog);
    };
  }, [connect]);

  const clearNewAlerts = useCallback(() => {
    dispatch({ type: 'CLEAR_NEW_ALERTS' });
  }, []);

  // Phase 2.19: panel calls this after each REST refresh resolves so the live
  // settled-delta overlay resets — REST result is authoritative for whatever
  // has been ingested into Supabase, and new deal_settled frames after this
  // point feed a fresh accumulator.
  const clearSettledDeltas = useCallback(() => {
    dispatch({ type: 'CLEAR_SETTLED_DELTAS' });
  }, []);

  return { ...state, clearNewAlerts, clearSettledDeltas };
}
