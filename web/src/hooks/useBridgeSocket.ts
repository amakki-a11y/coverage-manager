import { useEffect, useRef, useState, useCallback } from 'react';
import type { ExecutionPair, BridgeSocketMessage } from '../types/bridge';

const WS_URL = 'ws://localhost:5000/ws/bridge';
const MIN_RECONNECT_DELAY = 1000;
const MAX_RECONNECT_DELAY = 10000;

/**
 * WebSocket hook mirroring useExposureSocket's reconnect pattern.
 * Emits onPair for each incoming ExecutionPair update (new row or update to an existing one).
 */
export function useBridgeSocket(onPair: (pair: ExecutionPair) => void) {
  const [connected, setConnected] = useState(false);
  const wsRef = useRef<WebSocket | null>(null);
  const onPairRef = useRef(onPair);
  const reconnectDelay = useRef(MIN_RECONNECT_DELAY);

  // Keep latest callback without forcing a reconnect when the caller re-renders.
  useEffect(() => {
    onPairRef.current = onPair;
  }, [onPair]);

  const connect = useCallback(() => {
    if (wsRef.current?.readyState === WebSocket.OPEN) return;

    let ws: WebSocket;
    try {
      ws = new WebSocket(WS_URL);
    } catch {
      // Some browsers throw synchronously if the URL is wrong
      setTimeout(connect, MAX_RECONNECT_DELAY);
      return;
    }
    wsRef.current = ws;

    ws.onopen = () => {
      setConnected(true);
      reconnectDelay.current = MIN_RECONNECT_DELAY;
    };

    ws.onmessage = (event) => {
      try {
        const msg = JSON.parse(event.data) as BridgeSocketMessage;
        if (msg.type === 'pair') {
          onPairRef.current(msg.pair);
        }
      } catch (err) {
        const preview = typeof event.data === 'string' ? event.data.slice(0, 200) : '(non-string)';
        console.warn('[ws/bridge] malformed frame:', err, 'payload=', preview);
      }
    };

    ws.onclose = () => {
      setConnected(false);
      const delay = reconnectDelay.current;
      reconnectDelay.current = Math.min(delay * 1.5, MAX_RECONNECT_DELAY);
      setTimeout(connect, delay);
    };

    ws.onerror = () => {
      try { ws.close(); } catch { /* ignore */ }
    };
  }, []);

  useEffect(() => {
    connect();
    return () => {
      wsRef.current?.close();
    };
  }, [connect]);

  return { connected };
}
