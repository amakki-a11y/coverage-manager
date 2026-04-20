/**
 * Runtime-resolved service base URLs.
 *
 * In dev we serve the frontend from Vite on :5173 and the C# backend on :5000 —
 * so we need absolute URLs pointing at :5000. In production the C# backend
 * serves the bundle from its own `wwwroot`, so same-origin relative URLs work
 * and the dashboard is reachable wherever the box is reachable (office IP,
 * public IP, VPN, whatever).
 *
 * WebSocket URLs are always absolute — `new WebSocket(...)` requires it —
 * but we derive host + protocol from `window.location` so we never hard-code
 * a hostname.
 *
 * Coverage (Python collector) calls go through the C# backend's
 * `/api/coverage/*` proxies — the collector itself binds to 127.0.0.1:8100
 * on the server and is firewalled from the outside world.
 */

const isDev = import.meta.env.DEV;
const DEV_API_ORIGIN = 'http://localhost:5000';

/**
 * REST base URL. Empty string → same-origin relative URLs (production).
 * In dev mode returns `http://localhost:5000` to reach the C# backend directly.
 *
 * @example
 *   fetch(`${API_BASE}/api/exposure/summary`)
 */
export const API_BASE = isDev ? DEV_API_ORIGIN : '';

/**
 * WebSocket base URL — always absolute, derived from `window.location` at
 * runtime so the frontend works on any hostname/port. Protocol auto-selects
 * `wss://` when the page is served over HTTPS, `ws://` otherwise.
 *
 * @example
 *   new WebSocket(`${WS_BASE}/ws/exposure`)
 */
export const WS_BASE: string = (() => {
  if (isDev) return 'ws://localhost:5000';
  if (typeof window === 'undefined') return '';
  const proto = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
  return `${proto}//${window.location.host}`;
})();
