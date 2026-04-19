import { useEffect, useReducer, useCallback, useRef } from 'react';
import type { ExposureSummary, PriceQuote, AlertEvent, ExposureMessage } from '../types';

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
  | { type: 'EXPOSURE_UPDATE'; payload: ExposureMessage['data'] }
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

const WS_URL = 'ws://localhost:5000/ws/exposure';
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
        const message = JSON.parse(event.data) as ExposureMessage;
        if (message.type === 'exposure_update') {
          dispatch({ type: 'EXPOSURE_UPDATE', payload: message.data });
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
