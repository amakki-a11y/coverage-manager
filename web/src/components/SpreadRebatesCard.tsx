import { useCallback, useEffect, useMemo, useState } from 'react';
import { THEME } from '../theme';
import { ConfirmDialog } from './ConfirmDialog';
import type { SpreadRebateRate, SymbolMapping } from '../types';
import { API_BASE } from '../config';

/**
 * Per-(login, canonical-symbol) spread rebate rate editor. Dealer adds one row
 * per (account × symbol) combination they want to pay a rebate on — any trade
 * deal on that symbol is then credited `volume_lots × rate_per_lot`.
 *
 * Rows without a matching canonical symbol fall back to 0 in the engine.
 */
const API = `${API_BASE}/api/equity-pnl-config/spread-rebates`;
const MAPPINGS_API = `${API_BASE}/api/mappings`;

const inputStyle: React.CSSProperties = {
  background: THEME.bg,
  border: `1px solid ${THEME.border}`,
  color: THEME.t1,
  padding: '4px 8px',
  borderRadius: 4,
  fontSize: 12,
  fontFamily: "'JetBrains Mono', ui-monospace, Menlo, monospace",
  outline: 'none',
};

const cellStyle: React.CSSProperties = {
  padding: '6px 8px',
  fontFamily: "'JetBrains Mono', ui-monospace, Menlo, monospace",
  fontSize: 12,
  whiteSpace: 'nowrap',
};

const hdrStyle: React.CSSProperties = {
  ...cellStyle,
  color: THEME.t3,
  fontSize: 10,
  textTransform: 'uppercase',
  letterSpacing: 0.4,
  fontWeight: 700,
  fontFamily: 'inherit',
  borderBottom: `1px solid ${THEME.border}`,
  textAlign: 'right',
};

interface Draft {
  login: string;
  source: 'bbook' | 'coverage';
  canonical_symbol: string;
  rate_per_lot: string;
}

const EMPTY_DRAFT: Draft = { login: '', source: 'bbook', canonical_symbol: '', rate_per_lot: '' };

export function SpreadRebatesCard() {
  const [rates, setRates] = useState<SpreadRebateRate[]>([]);
  const [mappings, setMappings] = useState<SymbolMapping[]>([]);
  const [draft, setDraft] = useState<Draft>(EMPTY_DRAFT);
  const [showForm, setShowForm] = useState(false);
  const [pendingDelete, setPendingDelete] = useState<SpreadRebateRate | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const [r, m] = await Promise.all([fetch(API), fetch(MAPPINGS_API)]);
      if (r.ok) setRates(await r.json());
      if (m.ok) setMappings(await m.json());
    } catch { /* ignore */ }
  }, []);

  useEffect(() => { load(); }, [load]);

  const canonicalOptions = useMemo(
    () => Array.from(new Set(mappings.map(m => m.canonical_name).filter(Boolean))).sort(),
    [mappings],
  );

  const add = async () => {
    const login = Number(draft.login);
    const rate = Number(draft.rate_per_lot);
    if (!Number.isInteger(login) || login <= 0) { setError('Login must be a positive integer.'); return; }
    if (!draft.canonical_symbol.trim()) { setError('Symbol is required.'); return; }
    if (!Number.isFinite(rate) || rate < 0) { setError('Rate must be a non-negative number.'); return; }
    setError(null);
    try {
      const body: SpreadRebateRate[] = [{
        login,
        source: draft.source,
        canonical_symbol: draft.canonical_symbol.trim().toUpperCase(),
        rate_per_lot: rate,
      }];
      const res = await fetch(API, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      });
      if (!res.ok) { setError(`HTTP ${res.status}`); return; }
      setDraft(EMPTY_DRAFT);
      setShowForm(false);
      load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Request failed');
    }
  };

  const confirmDelete = async () => {
    if (!pendingDelete) return;
    const { login, source, canonical_symbol } = pendingDelete;
    setPendingDelete(null);
    try {
      await fetch(
        `${API}?login=${login}&source=${source}&symbol=${encodeURIComponent(canonical_symbol)}`,
        { method: 'DELETE' },
      );
      load();
    } catch { /* ignore */ }
  };

  return (
    <div style={{ marginTop: 32 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <div>
          <h3 style={{ color: THEME.t1, margin: 0, fontSize: 14 }}>Spread Rebate Rates</h3>
          <p style={{ color: THEME.t3, margin: '4px 0 0', fontSize: 12 }}>
            Per-(login, canonical symbol) spread rebate in USD per standard lot.
            Applied to every trade deal's volume to build the Spread Reb column.
          </p>
        </div>
        <button
          onClick={() => { setShowForm(v => !v); setError(null); }}
          style={{
            padding: '6px 14px', border: 'none', borderRadius: 4,
            background: THEME.blue, color: '#fff', fontSize: 12, fontWeight: 600, cursor: 'pointer',
          }}
        >
          {showForm ? 'Cancel' : '+ Add Rate'}
        </button>
      </div>

      {showForm && (
        <div style={{
          background: THEME.bg3, border: `1px solid ${THEME.border}`, borderRadius: 6,
          padding: 14, marginBottom: 12, display: 'grid',
          gridTemplateColumns: 'repeat(4, 1fr) auto', gap: 12, alignItems: 'end',
        }}>
          <div>
            <label style={{ color: THEME.t3, fontSize: 10, textTransform: 'uppercase', display: 'block', marginBottom: 3 }}>Login</label>
            <input style={{ ...inputStyle, width: '100%' }} type="number" value={draft.login}
                   onChange={e => setDraft({ ...draft, login: e.target.value })} placeholder="1053" />
          </div>
          <div>
            <label style={{ color: THEME.t3, fontSize: 10, textTransform: 'uppercase', display: 'block', marginBottom: 3 }}>Source</label>
            <select style={{ ...inputStyle, width: '100%' }} value={draft.source}
                    onChange={e => setDraft({ ...draft, source: e.target.value as 'bbook' | 'coverage' })}>
              <option value="bbook">bbook</option>
              <option value="coverage">coverage</option>
            </select>
          </div>
          <div>
            <label style={{ color: THEME.t3, fontSize: 10, textTransform: 'uppercase', display: 'block', marginBottom: 3 }}>Symbol</label>
            <input style={{ ...inputStyle, width: '100%' }} list="equity-rebate-symbols" value={draft.canonical_symbol}
                   onChange={e => setDraft({ ...draft, canonical_symbol: e.target.value })} placeholder="XAUUSD" />
            <datalist id="equity-rebate-symbols">
              {canonicalOptions.map(s => <option key={s} value={s} />)}
            </datalist>
          </div>
          <div>
            <label style={{ color: THEME.t3, fontSize: 10, textTransform: 'uppercase', display: 'block', marginBottom: 3 }}>Rate / Lot ($)</label>
            <input style={{ ...inputStyle, width: '100%', textAlign: 'right' }} type="number" step={0.01}
                   value={draft.rate_per_lot}
                   onChange={e => setDraft({ ...draft, rate_per_lot: e.target.value })} placeholder="5.00" />
          </div>
          <button
            onClick={add}
            style={{
              padding: '6px 14px', border: 'none', borderRadius: 4,
              background: THEME.green, color: '#000', fontSize: 12, fontWeight: 700, cursor: 'pointer',
            }}
          >
            Save
          </button>
          {error && (
            <div style={{
              gridColumn: '1 / -1', color: THEME.red, fontSize: 12,
              padding: '4px 8px', border: `1px solid ${THEME.red}`, background: THEME.badgeRed, borderRadius: 4,
            }}>{error}</div>
          )}
        </div>
      )}

      <div style={{ background: THEME.card, border: `1px solid ${THEME.border}`, borderRadius: 8, overflow: 'auto' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr>
              <th style={{ ...hdrStyle, textAlign: 'left' }}>Login</th>
              <th style={{ ...hdrStyle, textAlign: 'left' }}>Source</th>
              <th style={{ ...hdrStyle, textAlign: 'left' }}>Symbol</th>
              <th style={hdrStyle}>Rate / Lot</th>
              <th style={hdrStyle}>Actions</th>
            </tr>
          </thead>
          <tbody>
            {rates.map(r => (
              <tr key={`${r.login}:${r.source}:${r.canonical_symbol}`} style={{ borderTop: `1px solid ${THEME.border}` }}>
                <td style={{ ...cellStyle, color: THEME.t1, fontWeight: 600 }}>{r.login}</td>
                <td style={{ ...cellStyle, color: r.source === 'coverage' ? THEME.teal : THEME.blue, fontSize: 10, fontWeight: 700 }}>
                  {r.source.toUpperCase()}
                </td>
                <td style={{ ...cellStyle, color: THEME.t1 }}>{r.canonical_symbol}</td>
                <td style={{ ...cellStyle, textAlign: 'right', color: THEME.teal, fontWeight: 600 }}>
                  ${r.rate_per_lot.toFixed(2)}
                </td>
                <td style={{ ...cellStyle, textAlign: 'right' }}>
                  <button
                    onClick={() => setPendingDelete(r)}
                    style={{
                      padding: '3px 10px', border: 'none', borderRadius: 4,
                      background: THEME.badgeRed, color: THEME.red, fontSize: 11, fontWeight: 600, cursor: 'pointer',
                    }}
                  >Delete</button>
                </td>
              </tr>
            ))}
            {rates.length === 0 && (
              <tr>
                <td colSpan={5} style={{ ...cellStyle, textAlign: 'center', color: THEME.t3, padding: 24 }}>
                  No rebate rates configured.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <ConfirmDialog
        open={pendingDelete !== null}
        title="Delete spread rebate rate?"
        message={pendingDelete
          ? `Login ${pendingDelete.login} / ${pendingDelete.canonical_symbol} — $${pendingDelete.rate_per_lot.toFixed(2)} per lot.\n\nRemoving this row will make the Spread Reb column drop to 0 for this (account, symbol) pair on the next refresh.`
          : ''}
        confirmLabel="Delete rate"
        danger
        onConfirm={confirmDelete}
        onCancel={() => setPendingDelete(null)}
      />
    </div>
  );
}
