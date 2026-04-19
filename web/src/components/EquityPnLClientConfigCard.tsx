import { useCallback, useEffect, useState } from 'react';
import { THEME } from '../theme';
import type { EquityPnLClientConfig } from '../types';
import { formatBeirutDate } from '../utils/time';

/**
 * Per-login config for the Equity P&L tab: commission rebate %, PS %, contract
 * start date. Also surfaces the read-only running PS state (cum_pl, low-water
 * mark, last-processed month) so the dealer can audit what the engine has
 * accumulated. Dealers shouldn't edit those fields by hand — they're engine-
 * managed.
 */
const API = 'http://localhost:5000/api/equity-pnl-config';
const ACCOUNTS_API = 'http://localhost:5000/api/accounts';

const inputStyle: React.CSSProperties = {
  background: THEME.bg,
  border: `1px solid ${THEME.border}`,
  color: THEME.t1,
  padding: '4px 8px',
  borderRadius: 4,
  fontSize: 12,
  fontFamily: "'JetBrains Mono', ui-monospace, Menlo, monospace",
  outline: 'none',
  width: '100%',
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

interface AccountRow {
  login: number;
  source: 'bbook' | 'coverage';
  name: string;
}

type Draft = Pick<EquityPnLClientConfig,
  'login' | 'source' | 'comm_rebate_pct' | 'ps_pct' | 'ps_contract_start' | 'notes'>;

export function EquityPnLClientConfigCard() {
  const [accounts, setAccounts] = useState<AccountRow[]>([]);
  const [configs, setConfigs] = useState<EquityPnLClientConfig[]>([]);
  const [editing, setEditing] = useState<string | null>(null);
  const [draft, setDraft] = useState<Draft | null>(null);

  const load = useCallback(async () => {
    try {
      const [accRes, cfgRes] = await Promise.all([
        fetch(ACCOUNTS_API),
        fetch(API),
      ]);
      if (accRes.ok) {
        // `/api/accounts` returns `{accounts: [...], count: N}` — unwrap here
        // so the card's `.map(...)` can iterate. Previously we stored the
        // wrapper object into state and every render threw when the iteration
        // ran, which appeared in the UI as the Settings panel "flashing" —
        // React unmounts and remounts after a render-time throw.
        const body = await accRes.json();
        const list = Array.isArray(body) ? body : (Array.isArray(body?.accounts) ? body.accounts : []);
        setAccounts(list.filter((a: AccountRow) => a.source === 'bbook' || a.source === 'coverage'));
      }
      if (cfgRes.ok) {
        const body = await cfgRes.json();
        setConfigs(Array.isArray(body) ? body : []);
      }
    } catch { /* ignore */ }
  }, []);

  useEffect(() => { load(); }, [load]);

  const keyFor = (login: number, source: string) => `${source}:${login}`;
  const configMap = new Map(configs.map(c => [keyFor(c.login, c.source), c]));

  const startEdit = (acc: AccountRow) => {
    const existing = configMap.get(keyFor(acc.login, acc.source));
    setEditing(keyFor(acc.login, acc.source));
    setDraft({
      login: acc.login,
      source: acc.source,
      comm_rebate_pct: existing?.comm_rebate_pct ?? 0,
      ps_pct: existing?.ps_pct ?? 0,
      ps_contract_start: existing?.ps_contract_start ?? null,
      notes: existing?.notes ?? null,
    });
  };

  const save = async () => {
    if (!draft) return;
    try {
      const body = {
        ...draft,
        comm_rebate_pct: Number(draft.comm_rebate_pct),
        ps_pct: Number(draft.ps_pct),
        // Preserve engine-managed fields if a config already exists.
        ps_cum_pl: configMap.get(keyFor(draft.login, draft.source))?.ps_cum_pl ?? 0,
        ps_low_water_mark: configMap.get(keyFor(draft.login, draft.source))?.ps_low_water_mark ?? 0,
        ps_last_processed_month: configMap.get(keyFor(draft.login, draft.source))?.ps_last_processed_month ?? null,
      };
      const res = await fetch(API, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      });
      if (!res.ok) return;
      setEditing(null);
      setDraft(null);
      load();
    } catch { /* ignore */ }
  };

  const cancel = () => { setEditing(null); setDraft(null); };

  return (
    <div style={{ marginTop: 32 }}>
      <div style={{ marginBottom: 12 }}>
        <h3 style={{ color: THEME.t1, margin: 0, fontSize: 14 }}>
          Equity P&L — Client Config
        </h3>
        <p style={{ color: THEME.t3, margin: '4px 0 0', fontSize: 12 }}>
          Per-login commission rebate % and profit-share (loss-share) settings.
          PS low-water-mark state advances automatically once a month — read-only here.
        </p>
      </div>

      <div style={{
        background: THEME.card,
        borderRadius: 8,
        border: `1px solid ${THEME.border}`,
        overflow: 'auto',
      }}>
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr>
              <th style={{ ...hdrStyle, textAlign: 'left' }}>Login</th>
              <th style={{ ...hdrStyle, textAlign: 'left' }}>Name</th>
              <th style={{ ...hdrStyle, textAlign: 'left' }}>Source</th>
              <th style={hdrStyle}>Comm Reb %</th>
              <th style={hdrStyle}>PS %</th>
              <th style={hdrStyle}>PS Start</th>
              <th style={hdrStyle}>PS Cum PL</th>
              <th style={hdrStyle}>PS LWM</th>
              <th style={hdrStyle}>Last Month</th>
              <th style={hdrStyle}>Actions</th>
            </tr>
          </thead>
          <tbody>
            {accounts.map(acc => {
              const cfg = configMap.get(keyFor(acc.login, acc.source));
              const k = keyFor(acc.login, acc.source);
              const isEditing = editing === k && draft !== null;
              const srcColor = acc.source === 'coverage' ? THEME.teal : THEME.blue;
              if (isEditing && draft) {
                return (
                  <tr key={k} style={{ borderTop: `1px solid ${THEME.border}`, background: THEME.rowSelected }}>
                    <td style={{ ...cellStyle, color: THEME.t1, fontWeight: 600 }}>{acc.login}</td>
                    <td style={{ ...cellStyle, color: THEME.t1, fontFamily: 'inherit' }}>{acc.name}</td>
                    <td style={{ ...cellStyle, color: srcColor, fontSize: 10, fontWeight: 700 }}>{acc.source.toUpperCase()}</td>
                    <td style={cellStyle}>
                      <input
                        style={{ ...inputStyle, textAlign: 'right', width: 80 }}
                        type="number" min={0} max={100} step={0.1}
                        value={draft.comm_rebate_pct}
                        onChange={e => setDraft({ ...draft, comm_rebate_pct: Number(e.target.value) })}
                      />
                    </td>
                    <td style={cellStyle}>
                      <input
                        style={{ ...inputStyle, textAlign: 'right', width: 80 }}
                        type="number" min={0} max={100} step={0.1}
                        value={draft.ps_pct}
                        onChange={e => setDraft({ ...draft, ps_pct: Number(e.target.value) })}
                      />
                    </td>
                    <td style={cellStyle}>
                      <input
                        style={{ ...inputStyle, width: 140 }}
                        type="date"
                        value={(draft.ps_contract_start ?? '').toString().slice(0, 10)}
                        onChange={e => setDraft({ ...draft, ps_contract_start: e.target.value || null })}
                      />
                    </td>
                    <td style={{ ...cellStyle, color: THEME.t3, textAlign: 'right' }}>{cfg?.ps_cum_pl?.toFixed(2) ?? '—'}</td>
                    <td style={{ ...cellStyle, color: THEME.t3, textAlign: 'right' }}>{cfg?.ps_low_water_mark?.toFixed(2) ?? '—'}</td>
                    <td style={{ ...cellStyle, color: THEME.t3, textAlign: 'right' }}>
                      {cfg?.ps_last_processed_month ? formatBeirutDate(cfg.ps_last_processed_month) : '—'}
                    </td>
                    <td style={{ ...cellStyle, textAlign: 'right' }}>
                      <button
                        onClick={save}
                        style={{
                          padding: '4px 10px', marginRight: 4, border: 'none', borderRadius: 4,
                          background: THEME.green, color: '#000', fontSize: 11, fontWeight: 700, cursor: 'pointer',
                        }}
                      >Save</button>
                      <button
                        onClick={cancel}
                        style={{
                          padding: '4px 10px', border: `1px solid ${THEME.border}`, borderRadius: 4,
                          background: 'transparent', color: THEME.t2, fontSize: 11, cursor: 'pointer',
                        }}
                      >Cancel</button>
                    </td>
                  </tr>
                );
              }
              const psActive = cfg?.ps_contract_start != null;
              return (
                <tr key={k} style={{ borderTop: `1px solid ${THEME.border}` }}>
                  <td style={{ ...cellStyle, color: THEME.t1, fontWeight: 600 }}>{acc.login}</td>
                  <td style={{ ...cellStyle, color: THEME.t1, fontFamily: 'inherit' }}>{acc.name}</td>
                  <td style={{ ...cellStyle, color: srcColor, fontSize: 10, fontWeight: 700 }}>{acc.source.toUpperCase()}</td>
                  <td style={{ ...cellStyle, textAlign: 'right', color: (cfg?.comm_rebate_pct ?? 0) > 0 ? THEME.teal : THEME.t3 }}>
                    {(cfg?.comm_rebate_pct ?? 0).toFixed(2)}%
                  </td>
                  <td style={{ ...cellStyle, textAlign: 'right', color: (cfg?.ps_pct ?? 0) > 0 ? THEME.teal : THEME.t3 }}>
                    {(cfg?.ps_pct ?? 0).toFixed(2)}%
                  </td>
                  <td style={{ ...cellStyle, textAlign: 'right', color: psActive ? THEME.t1 : THEME.t3 }}>
                    {cfg?.ps_contract_start ? formatBeirutDate(cfg.ps_contract_start) : '—'}
                  </td>
                  <td style={{ ...cellStyle, textAlign: 'right', color: THEME.t2 }}>{(cfg?.ps_cum_pl ?? 0).toFixed(2)}</td>
                  <td style={{ ...cellStyle, textAlign: 'right', color: THEME.t2 }}>{(cfg?.ps_low_water_mark ?? 0).toFixed(2)}</td>
                  <td style={{ ...cellStyle, textAlign: 'right', color: THEME.t3 }}>
                    {cfg?.ps_last_processed_month ? formatBeirutDate(cfg.ps_last_processed_month) : '—'}
                  </td>
                  <td style={{ ...cellStyle, textAlign: 'right' }}>
                    <button
                      onClick={() => startEdit(acc)}
                      style={{
                        padding: '4px 10px', border: 'none', borderRadius: 4,
                        background: THEME.badgeBlue, color: THEME.blue, fontSize: 11, fontWeight: 600, cursor: 'pointer',
                      }}
                    >Edit</button>
                  </td>
                </tr>
              );
            })}
            {accounts.length === 0 && (
              <tr>
                <td colSpan={10} style={{ ...cellStyle, textAlign: 'center', color: THEME.t3, padding: 24 }}>
                  No trading accounts to configure yet — the scheduler will populate them on first sync.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
