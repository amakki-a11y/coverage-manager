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
interface State {
  exposureSummaries: ExposureSummary[];
  prices: PriceQuote[];
  connected: boolean;
  newAlerts: AlertEvent[];
  alertCount: number;
}

type Action =
  | { type: 'CONNECTED' }
  | { type: 'DISCONNECTED' }
  | { type: 'EXPOSURE_UPDATE'; payload: Extract<WsMessage, { type: 'exposure_update' }>['data'] }
  | { type: 'PRICE_UPDATE'; payload: Extract<WsMessage, { type: 'price_update' }>['data'] }
  | { type: 'CLEAR_NEW_ALERTS' };

function reducer(state: State, action: Action): State {
  switch (action.type) {
    case 'CONNECTED':
      return { ...state, connected: true };
    case 'DISCONNECTED':
      return { ...state, connected: false };
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
  newAlerts: [],
  alertCount: 0,
};

const WS_URL = `${WS_BASE}/ws/exposure`;
const MIN_RECONNECT_DELAY = 1000;
const MAX_RECONNECT_DELAY = 10000;

export function useExposureSocket() {
  const [state, dispatch] = useReducer(reducer, initialState);
  const wsRef = useRef<WebSocket | null>(null);
  const reconnectDelay = useRef(MIN_RECONNECT_DELAY);

  const connect = useCallback(() => {
    if (wsRef.current?.readyState === WebSocket.OPEN) return;

    const ws = new WebSocket(WS_URL);
    wsRef.current = ws;

    ws.onopen = () => {
      dispatch({ type: 'CONNECTED' });
      reconnectDelay.current = MIN_RECONNECT_DELAY;
    };

    ws.onmessage = (event) => {
      try {
        const message = JSON.parse(event.data) as WsMessage;
        if (message.type === 'exposure_update') {
          dispatch({ type: 'EXPOSURE_UPDATE', payload: message.data });
        } else if (message.type === 'price_update') {
          dispatch({ type: 'PRICE_UPDATE', payload: message.data });
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
    return () => {
      wsRef.current?.close();
    };
  }, [connect]);

  const clearNewAlerts = useCallback(() => {
    dispatch({ type: 'CLEAR_NEW_ALERTS' });
  }, []);

  return { ...state, clearNewAlerts };
}
