import { useCallback, useEffect, useState } from 'react';
import { THEME } from '../theme';

interface BridgeSettingsState {
  enabled: boolean;
  mode: 'Stub' | 'Live';
  baseUrl: string;
  clientCode: string;
  username: string;
  passwordSet: boolean;
  notes: string;
  activeMode: string;
  feedHealth: {
    mode: string;
    state: string;
    lastMessageUtc: string | null;
    messagesReceived: number;
    lastError: string | null;
  } | null;
}

const DEFAULT: BridgeSettingsState = {
  enabled: false,
  mode: 'Stub',
  baseUrl: 'https://bridge.centroidsol.com',
  clientCode: '',
  username: '',
  passwordSet: false,
  notes: '',
  activeMode: 'Stub',
  feedHealth: null,
};

const API = 'http://localhost:5000/api/settings/bridge';

const labelStyle: React.CSSProperties = {
  color: THEME.t3,
  fontSize: 10,
  textTransform: 'uppercase',
  letterSpacing: 0.5,
  marginBottom: 4,
  display: 'block',
};

const inputStyle: React.CSSProperties = {
  background: THEME.bg,
  border: `1px solid ${THEME.border}`,
  color: THEME.t1,
  padding: '8px 10px',
  borderRadius: 4,
  fontSize: 13,
  fontFamily: 'monospace',
  outline: 'none',
  width: '100%',
  boxSizing: 'border-box',
};

const cardStyle: React.CSSProperties = {
  background: THEME.card,
  borderRadius: 8,
  padding: 20,
  border: `1px solid ${THEME.border}`,
};

const btnStyle: React.CSSProperties = {
  padding: '8px 20px',
  borderRadius: 4,
  border: 'none',
  cursor: 'pointer',
  fontSize: 12,
  fontWeight: 600,
};

interface TestResult {
  success: boolean;
  status: number;
  error: string | null;
  elapsedMs: number;
  hint?: string;
}

export function BridgeSettingsCard() {
  const [state, setState] = useState<BridgeSettingsState>(DEFAULT);
  const [password, setPassword] = useState<string>(''); // pending edit only
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<TestResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);

  const fetchState = useCallback(async () => {
    try {
      const res = await fetch(API);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = await res.json();
      setState({ ...DEFAULT, ...data });
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load bridge settings');
    }
  }, []);

  useEffect(() => {
    fetchState();
    const id = setInterval(fetchState, 5000);
    return () => clearInterval(id);
  }, [fetchState]);

  const onSave = async () => {
    setSaving(true);
    setError(null);
    setInfo(null);
    try {
      const body = {
        enabled: state.enabled,
        mode: state.mode,
        baseUrl: state.baseUrl.trim() || 'https://bridge.centroidsol.com',
        clientCode: state.clientCode.trim(),
        username: state.username.trim(),
        password: password.length > 0 ? password : null,
        notes: state.notes,
      };
      const res = await fetch(API, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      });
      const data = await res.json();
      if (!res.ok) {
        setError(data?.error ?? `HTTP ${res.status}`);
      } else {
        setPassword('');
        setInfo(`Saved. Active mode: ${data.activeMode}${data.error ? ` (warning: ${data.error})` : ''}`);
        await fetchState();
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Save failed');
    } finally {
      setSaving(false);
    }
  };

  const onReload = async () => {
    setSaving(true);
    setError(null);
    setInfo(null);
    try {
      const res = await fetch(`${API}/reload`, { method: 'POST' });
      const data = await res.json();
      if (!res.ok) {
        setError(data?.error ?? `HTTP ${res.status}`);
      } else {
        setInfo(`Reloaded. Active mode: ${data.activeMode}`);
        await fetchState();
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Reload failed');
    } finally {
      setSaving(false);
    }
  };

  const onTest = async () => {
    setTesting(true);
    setTestResult(null);
    setError(null);
    setInfo(null);
    try {
      const body = {
        baseUrl: state.baseUrl.trim() || 'https://bridge.centroidsol.com',
        clientCode: state.clientCode.trim(),
        username: state.username.trim(),
        // send null if the user didn't type a new password — backend falls back to the stored one
        password: password.length > 0 ? password : null,
      };
      const res = await fetch(`${API}/test`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      });
      const data = (await res.json()) as TestResult;
      setTestResult(data);
    } catch (e) {
      setTestResult({ success: false, status: 0, error: e instanceof Error ? e.message : 'Test failed', elapsedMs: 0 });
    } finally {
      setTesting(false);
    }
  };

  const statusColor = (s: string) =>
    s === 'LoggedIn' ? THEME.green
      : s === 'Connecting' ? THEME.amber
      : s === 'Error' ? THEME.red
      : THEME.t3;

  const liveReady =
    state.baseUrl && state.clientCode && state.username && (state.passwordSet || password.length > 0);

  return (
    <div style={{ marginTop: 32 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <div>
          <h3 style={{ color: THEME.teal, margin: 0, fontSize: 14 }}>Bridge (Centroid CS 360 REST + WS)</h3>
          <p style={{ color: THEME.t3, margin: '4px 0 0', fontSize: 12 }}>
            Credentials for the CS 360 API. Used for REST login + the live_trades WebSocket. Password write-only.
          </p>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <span style={{
            padding: '4px 10px',
            background: `${statusColor(state.feedHealth?.state ?? 'Disconnected')}22`,
            border: `1px solid ${statusColor(state.feedHealth?.state ?? 'Disconnected')}`,
            borderRadius: 999,
            fontSize: 11,
            color: statusColor(state.feedHealth?.state ?? 'Disconnected'),
            fontFamily: 'monospace',
          }}>
            {state.feedHealth?.mode ?? 'Stub'} · {state.feedHealth?.state ?? '—'}
          </span>
        </div>
      </div>

      <div style={cardStyle}>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
          <div style={{ gridColumn: '1 / span 2', display: 'flex', gap: 24, alignItems: 'center' }}>
            <label style={{ display: 'flex', alignItems: 'center', gap: 8, color: THEME.t2, fontSize: 12, cursor: 'pointer' }}>
              <input
                type="checkbox"
                checked={state.enabled}
                onChange={(e) => setState({ ...state, enabled: e.target.checked })}
              />
              <span><strong>Enabled</strong> — connect on save</span>
            </label>
            <div>
              <span style={labelStyle}>Mode</span>
              <select
                value={state.mode}
                onChange={(e) => setState({ ...state, mode: e.target.value as BridgeSettingsState['mode'] })}
                style={{ ...inputStyle, width: 160 }}
              >
                <option value="Stub">Stub (synthetic)</option>
                <option value="Live">Live (REST + WS)</option>
              </select>
            </div>
            {state.mode === 'Live' && !liveReady && (
              <span style={{ color: THEME.amber, fontSize: 11 }}>
                Missing required fields for Live mode
              </span>
            )}
          </div>

          <div style={{ gridColumn: '1 / span 2' }}>
            <label style={labelStyle}>Base URL</label>
            <input
              type="text"
              placeholder="https://bridge.centroidsol.com"
              value={state.baseUrl}
              onChange={(e) => setState({ ...state, baseUrl: e.target.value })}
              style={inputStyle}
            />
          </div>

          <div>
            <label style={labelStyle}>Client code (x-forward-client)</label>
            <input
              type="text"
              placeholder="your-broker-code"
              value={state.clientCode}
              onChange={(e) => setState({ ...state, clientCode: e.target.value })}
              style={inputStyle}
            />
          </div>
          <div>
            <label style={labelStyle}>Username</label>
            <input
              type="text"
              autoComplete="off"
              value={state.username}
              onChange={(e) => setState({ ...state, username: e.target.value })}
              style={inputStyle}
            />
          </div>

          <div>
            <label style={labelStyle}>
              Password
              {state.passwordSet && password.length === 0 && (
                <span style={{ color: THEME.green, marginLeft: 6, fontSize: 10 }}>• set</span>
              )}
            </label>
            <input
              type="password"
              autoComplete="new-password"
              placeholder={state.passwordSet ? '•••••••• (leave blank to keep)' : ''}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              style={inputStyle}
            />
          </div>
          <div style={{ display: 'flex', alignItems: 'flex-end' }}>
            <button
              onClick={onTest}
              disabled={testing || !state.username || (!state.passwordSet && !password)}
              style={{
                ...btnStyle,
                width: '100%',
                background: testing ? THEME.t3 : 'transparent',
                color: THEME.t1,
                border: `1px solid ${THEME.teal}`,
                cursor: testing ? 'wait' : 'pointer',
              }}
              title="POST /v2/api/login with these credentials — nothing is persisted"
            >
              {testing ? 'Testing…' : 'Test connection'}
            </button>
          </div>

          <div style={{ gridColumn: '1 / span 2' }}>
            <label style={labelStyle}>Notes</label>
            <textarea
              rows={2}
              value={state.notes}
              onChange={(e) => setState({ ...state, notes: e.target.value })}
              style={{ ...inputStyle, resize: 'vertical', fontFamily: 'inherit' }}
            />
          </div>
        </div>

        {testResult && (
          <div style={{
            marginTop: 12,
            padding: 10,
            background: testResult.success ? 'rgba(102,187,106,0.08)' : 'rgba(255,82,82,0.08)',
            border: `1px solid ${testResult.success ? THEME.green : THEME.red}`,
            borderRadius: 4,
            color: testResult.success ? THEME.green : THEME.red,
            fontSize: 12,
          }}>
            <div style={{ fontWeight: 700 }}>
              {testResult.success ? '✓ Login succeeded' : '✗ Login failed'}
              <span style={{ color: THEME.t3, fontWeight: 400, marginLeft: 8 }}>
                {testResult.elapsedMs}ms · HTTP {testResult.status}
              </span>
            </div>
            {testResult.error && <div style={{ marginTop: 4 }}>{testResult.error}</div>}
            {testResult.hint && <div style={{ marginTop: 4, color: THEME.t2 }}>{testResult.hint}</div>}
          </div>
        )}

        <div style={{ marginTop: 16, display: 'flex', gap: 8, alignItems: 'center', justifyContent: 'flex-end', flexWrap: 'wrap' }}>
          <div style={{ marginRight: 'auto', fontSize: 11, color: THEME.t3, fontFamily: 'monospace' }}>
            {state.feedHealth?.messagesReceived != null && (
              <>Msgs received: {state.feedHealth.messagesReceived.toLocaleString()} · </>
            )}
            {state.feedHealth?.lastMessageUtc
              ? `Last: ${new Date(state.feedHealth.lastMessageUtc).toISOString().slice(11, 19)}Z`
              : 'No messages yet'}
          </div>
          <button
            onClick={onReload}
            disabled={saving}
            style={{
              ...btnStyle,
              background: 'transparent',
              color: THEME.t2,
              border: `1px solid ${THEME.border}`,
            }}
          >
            Reload feed
          </button>
          <button
            onClick={onSave}
            disabled={saving}
            style={{
              ...btnStyle,
              background: saving ? THEME.t3 : THEME.blue,
              color: '#fff',
              cursor: saving ? 'wait' : 'pointer',
            }}
          >
            {saving ? 'Saving…' : 'Save settings'}
          </button>
        </div>

        {error && (
          <div style={{ marginTop: 12, padding: 10, background: 'rgba(255,82,82,0.08)', border: `1px solid ${THEME.red}`, borderRadius: 4, color: THEME.red, fontSize: 12 }}>
            {error}
          </div>
        )}
        {info && !error && (
          <div style={{ marginTop: 12, padding: 10, background: 'rgba(102,187,106,0.08)', border: `1px solid ${THEME.green}`, borderRadius: 4, color: THEME.green, fontSize: 12 }}>
            {info}
          </div>
        )}

        {state.feedHealth?.lastError && (
          <div style={{ marginTop: 12, padding: 10, background: 'rgba(255,186,66,0.08)', border: `1px solid ${THEME.amber}`, borderRadius: 4, color: THEME.amber, fontSize: 12 }}>
            Feed error: {state.feedHealth.lastError}
          </div>
        )}
      </div>
    </div>
  );
}
