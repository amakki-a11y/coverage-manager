import { useCallback, useEffect, useMemo, useState } from 'react';
import { THEME } from '../theme';
import { ConfirmDialog } from './ConfirmDialog';
import type {
  LoginGroup,
  LoginGroupMember,
  EquityPnLGroupConfig,
  GroupSpreadRebateRate,
  SymbolMapping,
} from '../types';

/**
 * Phase 2 — per-group rebate/PS configuration editor.
 *
 * Dealer creates named groups (e.g. "IB-Lebanon", "VIP-A"), assigns logins to
 * them, and sets comm-rebate %, PS %, and per-symbol spread rebates at the
 * group level. The Equity P&L endpoint resolves effective rates as:
 *
 *   login-specific override → group config → 0 (default)
 *
 * Layout: a list of groups on the left; selecting one opens its detail panel
 * on the right with Config / Members / Spread Rebates sub-tabs.
 */
const API = 'http://localhost:5000/api/login-groups';
const ACCOUNTS_API = 'http://localhost:5000/api/accounts';
const MAPPINGS_API = 'http://localhost:5000/api/mappings';

const inputStyle: React.CSSProperties = {
  background: THEME.bg,
  border: `1px solid ${THEME.border}`,
  color: THEME.t1,
  padding: '6px 10px',
  borderRadius: 4,
  fontSize: 12,
  fontFamily: "'JetBrains Mono', ui-monospace, Menlo, monospace",
  outline: 'none',
};

const smallBtn: React.CSSProperties = {
  padding: '4px 10px',
  borderRadius: 4,
  border: 'none',
  fontSize: 11,
  fontWeight: 600,
  cursor: 'pointer',
};

interface AccountRow {
  login: number;
  source: 'bbook' | 'coverage';
  name: string;
}

type DetailTab = 'config' | 'members' | 'spread';

export function LoginGroupsCard() {
  const [groups, setGroups] = useState<LoginGroup[]>([]);
  const [configs, setConfigs] = useState<EquityPnLGroupConfig[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [detailTab, setDetailTab] = useState<DetailTab>('config');
  const [members, setMembers] = useState<LoginGroupMember[]>([]);
  const [rates, setRates] = useState<GroupSpreadRebateRate[]>([]);
  const [accounts, setAccounts] = useState<AccountRow[]>([]);
  const [canonicalSymbols, setCanonicalSymbols] = useState<string[]>([]);
  const [newGroupName, setNewGroupName] = useState('');
  const [pendingDelete, setPendingDelete] = useState<LoginGroup | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Add-member form
  const [addLogin, setAddLogin] = useState('');
  const [addSource, setAddSource] = useState<'bbook' | 'coverage'>('bbook');
  const [addPriority, setAddPriority] = useState('0');

  // Add-rate form
  const [rateSymbol, setRateSymbol] = useState('');
  const [ratePerLot, setRatePerLot] = useState('');

  const loadAll = useCallback(async () => {
    try {
      const [gs, cs, accs, maps] = await Promise.all([
        fetch(API).then(r => (r.ok ? r.json() : [])),
        fetch(`${API}/config`).then(r => (r.ok ? r.json() : [])),
        fetch(ACCOUNTS_API).then(r => (r.ok ? r.json() : { accounts: [] })),
        fetch(MAPPINGS_API).then(r => (r.ok ? r.json() : [])),
      ]);
      setGroups(Array.isArray(gs) ? gs : []);
      setConfigs(Array.isArray(cs) ? cs : []);
      setAccounts(Array.isArray(accs?.accounts) ? accs.accounts.map((a: AccountRow) => ({
        login: a.login, source: a.source, name: a.name,
      })) : []);
      setCanonicalSymbols(
        Array.isArray(maps)
          ? Array.from(new Set(maps.map((m: SymbolMapping) => m.canonical_name).filter(Boolean))).sort()
          : [],
      );
    } catch { /* ignore */ }
  }, []);

  useEffect(() => { loadAll(); }, [loadAll]);

  const loadDetail = useCallback(async (groupId: string) => {
    try {
      const [ms, rs] = await Promise.all([
        fetch(`${API}/${groupId}/members`).then(r => (r.ok ? r.json() : [])),
        fetch(`${API}/${groupId}/spread-rebates`).then(r => (r.ok ? r.json() : [])),
      ]);
      setMembers(Array.isArray(ms) ? ms : []);
      setRates(Array.isArray(rs) ? rs : []);
    } catch { /* ignore */ }
  }, []);

  useEffect(() => {
    if (selectedId) loadDetail(selectedId);
    else { setMembers([]); setRates([]); }
  }, [selectedId, loadDetail]);

  const selectedGroup = useMemo(
    () => groups.find(g => g.id === selectedId) ?? null,
    [groups, selectedId],
  );
  const selectedConfig = useMemo(
    () => configs.find(c => c.group_id === selectedId) ?? null,
    [configs, selectedId],
  );

  // ── Group CRUD ─────────────────────────────────────────────────
  const addGroup = async () => {
    const name = newGroupName.trim();
    if (!name) { setError('Group name required'); return; }
    setError(null);
    try {
      const res = await fetch(API, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name }),
      });
      if (!res.ok) { setError(`HTTP ${res.status}`); return; }
      setNewGroupName('');
      loadAll();
    } catch { setError('Request failed'); }
  };

  const confirmDeleteGroup = async () => {
    if (!pendingDelete?.id) return;
    const id = pendingDelete.id;
    setPendingDelete(null);
    try {
      await fetch(`${API}/${id}`, { method: 'DELETE' });
      if (selectedId === id) setSelectedId(null);
      loadAll();
    } catch { /* ignore */ }
  };

  // ── Config editor ─────────────────────────────────────────────
  const saveConfig = async (commPct: number, psPct: number) => {
    if (!selectedId) return;
    try {
      await fetch(`${API}/config`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          group_id: selectedId,
          comm_rebate_pct: commPct,
          ps_pct: psPct,
        }),
      });
      loadAll();
    } catch { /* ignore */ }
  };

  // ── Member CRUD ───────────────────────────────────────────────
  const addMember = async () => {
    if (!selectedId) return;
    const loginN = Number(addLogin);
    if (!Number.isInteger(loginN) || loginN <= 0) { setError('Login must be a positive integer'); return; }
    setError(null);
    try {
      const res = await fetch(`${API}/${selectedId}/members`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          group_id: selectedId,
          login: loginN,
          source: addSource,
          priority: Number(addPriority) || 0,
        }),
      });
      if (!res.ok) { setError(`HTTP ${res.status}`); return; }
      setAddLogin('');
      loadDetail(selectedId);
    } catch { setError('Request failed'); }
  };

  const removeMember = async (m: LoginGroupMember) => {
    if (!selectedId) return;
    try {
      await fetch(`${API}/${selectedId}/members?login=${m.login}&source=${m.source}`, { method: 'DELETE' });
      loadDetail(selectedId);
    } catch { /* ignore */ }
  };

  // ── Spread rebate rows ────────────────────────────────────────
  const addRate = async () => {
    if (!selectedId) return;
    const sym = rateSymbol.trim().toUpperCase();
    const r = Number(ratePerLot);
    if (!sym) { setError('Symbol required'); return; }
    if (!Number.isFinite(r) || r < 0) { setError('Rate must be >= 0'); return; }
    setError(null);
    try {
      const res = await fetch(`${API}/${selectedId}/spread-rebates`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify([{ group_id: selectedId, canonical_symbol: sym, rate_per_lot: r }]),
      });
      if (!res.ok) { setError(`HTTP ${res.status}`); return; }
      setRateSymbol('');
      setRatePerLot('');
      loadDetail(selectedId);
    } catch { setError('Request failed'); }
  };

  const removeRate = async (row: GroupSpreadRebateRate) => {
    if (!selectedId) return;
    try {
      await fetch(`${API}/${selectedId}/spread-rebates?symbol=${encodeURIComponent(row.canonical_symbol)}`, { method: 'DELETE' });
      loadDetail(selectedId);
    } catch { /* ignore */ }
  };

  const nameOf = (login: number) => accounts.find(a => a.login === login)?.name ?? '—';

  return (
    <div style={{ marginTop: 32 }}>
      <div style={{ marginBottom: 12 }}>
        <h3 style={{ color: THEME.t1, margin: 0, fontSize: 14 }}>Login Groups (Phase 2)</h3>
        <p style={{ color: THEME.t3, margin: '4px 0 0', fontSize: 12 }}>
          Group logins under a named bucket and set comm-rebate %, PS %, and per-symbol spread rebates at the group level.
          Per-login overrides in the cards above still win when both are set.
        </p>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '260px 1fr', gap: 16 }}>
        {/* Left — groups list */}
        <div style={{ background: THEME.card, borderRadius: 8, border: `1px solid ${THEME.border}`, padding: 12 }}>
          <div style={{ display: 'flex', gap: 6, marginBottom: 10 }}>
            <input
              style={{ ...inputStyle, flex: 1 }}
              value={newGroupName}
              placeholder="New group name"
              onChange={e => setNewGroupName(e.target.value)}
              onKeyDown={e => { if (e.key === 'Enter') addGroup(); }}
            />
            <button
              onClick={addGroup}
              style={{ ...smallBtn, background: THEME.blue, color: '#fff', padding: '6px 12px' }}
            >
              Add
            </button>
          </div>

          {groups.length === 0 ? (
            <div style={{ color: THEME.t3, fontSize: 12, padding: 12, textAlign: 'center' }}>
              No groups yet.
            </div>
          ) : groups.map(g => {
            const cfg = configs.find(c => c.group_id === g.id);
            const isSelected = g.id === selectedId;
            return (
              <div
                key={g.id}
                onClick={() => setSelectedId(g.id ?? null)}
                style={{
                  padding: '8px 10px',
                  borderRadius: 4,
                  marginBottom: 4,
                  cursor: 'pointer',
                  background: isSelected ? THEME.rowSelected : 'transparent',
                  borderLeft: `3px solid ${isSelected ? THEME.blue : 'transparent'}`,
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                }}
              >
                <div style={{ minWidth: 0 }}>
                  <div style={{ color: THEME.t1, fontSize: 13, fontWeight: 600, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                    {g.name}
                  </div>
                  <div style={{ color: THEME.t3, fontSize: 10, marginTop: 2 }}>
                    {cfg ? `Comm ${cfg.comm_rebate_pct}% · PS ${cfg.ps_pct}%` : 'no config'}
                  </div>
                </div>
                <button
                  onClick={e => { e.stopPropagation(); setPendingDelete(g); }}
                  style={{
                    ...smallBtn,
                    background: 'transparent',
                    color: THEME.t3,
                    border: `1px solid ${THEME.border}`,
                    padding: '2px 6px',
                    fontSize: 10,
                  }}
                >
                  ×
                </button>
              </div>
            );
          })}
        </div>

        {/* Right — detail panel */}
        <div style={{ background: THEME.card, borderRadius: 8, border: `1px solid ${THEME.border}`, padding: 14, minHeight: 220 }}>
          {!selectedGroup ? (
            <div style={{ color: THEME.t3, fontSize: 13, padding: 40, textAlign: 'center' }}>
              Select a group on the left, or add a new one.
            </div>
          ) : (
            <>
              <div style={{ display: 'flex', alignItems: 'baseline', gap: 12, marginBottom: 12 }}>
                <h4 style={{ color: THEME.t1, margin: 0, fontSize: 15 }}>{selectedGroup.name}</h4>
                <span style={{ color: THEME.t3, fontSize: 11 }}>{members.length} members</span>
              </div>

              <div style={{ display: 'flex', gap: 4, borderBottom: `1px solid ${THEME.border}`, marginBottom: 14 }}>
                {(['config', 'members', 'spread'] as DetailTab[]).map(t => (
                  <button
                    key={t}
                    onClick={() => setDetailTab(t)}
                    style={{
                      padding: '6px 12px',
                      background: detailTab === t ? THEME.bg3 : 'transparent',
                      color: detailTab === t ? THEME.t1 : THEME.t3,
                      border: 'none',
                      borderBottom: detailTab === t ? `2px solid ${THEME.blue}` : '2px solid transparent',
                      cursor: 'pointer',
                      fontSize: 11,
                      fontWeight: 600,
                      textTransform: 'uppercase',
                      letterSpacing: 0.4,
                    }}
                  >
                    {t === 'config' ? 'Config' : t === 'members' ? `Members (${members.length})` : `Spread Rebates (${rates.length})`}
                  </button>
                ))}
              </div>

              {detailTab === 'config' && (
                <ConfigEditor cfg={selectedConfig ?? { group_id: selectedGroup.id!, comm_rebate_pct: 0, ps_pct: 0 }} onSave={saveConfig} />
              )}

              {detailTab === 'members' && (
                <div>
                  <div style={{ display: 'flex', gap: 8, marginBottom: 10, alignItems: 'end' }}>
                    <input
                      style={{ ...inputStyle, width: 100 }}
                      type="number"
                      placeholder="Login"
                      value={addLogin}
                      onChange={e => setAddLogin(e.target.value)}
                    />
                    <select
                      style={{ ...inputStyle, width: 110 }}
                      value={addSource}
                      onChange={e => setAddSource(e.target.value as 'bbook' | 'coverage')}
                    >
                      <option value="bbook">bbook</option>
                      <option value="coverage">coverage</option>
                    </select>
                    <input
                      style={{ ...inputStyle, width: 80 }}
                      type="number"
                      placeholder="Priority"
                      value={addPriority}
                      onChange={e => setAddPriority(e.target.value)}
                      title="Higher priority wins when a login is in multiple groups"
                    />
                    <button
                      onClick={addMember}
                      style={{ ...smallBtn, background: THEME.green, color: '#000' }}
                    >
                      + Add member
                    </button>
                  </div>

                  {members.length === 0 ? (
                    <div style={{ color: THEME.t3, fontSize: 12, padding: 16, textAlign: 'center' }}>
                      No members yet.
                    </div>
                  ) : (
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
                      <thead>
                        <tr style={{ borderBottom: `1px solid ${THEME.border}` }}>
                          <th style={th}>Login</th>
                          <th style={th}>Name</th>
                          <th style={th}>Source</th>
                          <th style={th}>Priority</th>
                          <th style={th}></th>
                        </tr>
                      </thead>
                      <tbody>
                        {members.map(m => (
                          <tr key={`${m.login}:${m.source}`} style={{ borderBottom: `1px solid ${THEME.border}` }}>
                            <td style={{ ...td, fontWeight: 600 }}>{m.login}</td>
                            <td style={{ ...td, color: THEME.t2 }}>{nameOf(m.login)}</td>
                            <td style={{ ...td, color: m.source === 'coverage' ? THEME.teal : THEME.blue, fontSize: 10, fontWeight: 700 }}>
                              {m.source.toUpperCase()}
                            </td>
                            <td style={td}>{m.priority}</td>
                            <td style={{ ...td, textAlign: 'right' }}>
                              <button
                                onClick={() => removeMember(m)}
                                style={{ ...smallBtn, background: THEME.badgeRed, color: THEME.red }}
                              >
                                Remove
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  )}
                </div>
              )}

              {detailTab === 'spread' && (
                <div>
                  <div style={{ display: 'flex', gap: 8, marginBottom: 10, alignItems: 'end' }}>
                    <input
                      style={{ ...inputStyle, width: 120 }}
                      list="group-rebate-symbols"
                      placeholder="Symbol"
                      value={rateSymbol}
                      onChange={e => setRateSymbol(e.target.value)}
                    />
                    <datalist id="group-rebate-symbols">
                      {canonicalSymbols.map(s => <option key={s} value={s} />)}
                    </datalist>
                    <input
                      style={{ ...inputStyle, width: 110, textAlign: 'right' }}
                      type="number"
                      step={0.01}
                      placeholder="Rate / lot"
                      value={ratePerLot}
                      onChange={e => setRatePerLot(e.target.value)}
                    />
                    <button
                      onClick={addRate}
                      style={{ ...smallBtn, background: THEME.green, color: '#000' }}
                    >
                      + Add rate
                    </button>
                  </div>

                  {rates.length === 0 ? (
                    <div style={{ color: THEME.t3, fontSize: 12, padding: 16, textAlign: 'center' }}>
                      No spread rebate rates yet.
                    </div>
                  ) : (
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
                      <thead>
                        <tr style={{ borderBottom: `1px solid ${THEME.border}` }}>
                          <th style={th}>Symbol</th>
                          <th style={thRight}>Rate / Lot</th>
                          <th style={thRight}></th>
                        </tr>
                      </thead>
                      <tbody>
                        {rates.map(r => (
                          <tr key={r.canonical_symbol} style={{ borderBottom: `1px solid ${THEME.border}` }}>
                            <td style={{ ...td, fontWeight: 600 }}>{r.canonical_symbol}</td>
                            <td style={{ ...td, textAlign: 'right', color: THEME.teal, fontWeight: 600 }}>
                              ${r.rate_per_lot.toFixed(2)}
                            </td>
                            <td style={{ ...td, textAlign: 'right' }}>
                              <button
                                onClick={() => removeRate(r)}
                                style={{ ...smallBtn, background: THEME.badgeRed, color: THEME.red }}
                              >
                                Remove
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  )}
                </div>
              )}
            </>
          )}

          {error && (
            <div style={{ marginTop: 10, color: THEME.red, fontSize: 11 }}>{error}</div>
          )}
        </div>
      </div>

      <ConfirmDialog
        open={pendingDelete !== null}
        title="Delete login group?"
        message={pendingDelete
          ? `The "${pendingDelete.name}" group and all its members / config / spread rates will be removed.\n\nPer-login overrides (in the cards above) are NOT affected. Logins inheriting from this group will fall back to 0 rebate on the next refresh.`
          : ''}
        confirmLabel="Delete group"
        danger
        onConfirm={confirmDeleteGroup}
        onCancel={() => setPendingDelete(null)}
      />
    </div>
  );
}

function ConfigEditor({
  cfg, onSave,
}: {
  cfg: EquityPnLGroupConfig;
  onSave: (commPct: number, psPct: number) => Promise<void> | void;
}) {
  const [commPct, setCommPct] = useState(String(cfg.comm_rebate_pct));
  const [psPct, setPsPct] = useState(String(cfg.ps_pct));

  useEffect(() => {
    setCommPct(String(cfg.comm_rebate_pct));
    setPsPct(String(cfg.ps_pct));
  }, [cfg.group_id, cfg.comm_rebate_pct, cfg.ps_pct]);

  return (
    <div style={{ display: 'flex', gap: 12, alignItems: 'end' }}>
      <div>
        <label style={{ color: THEME.t3, fontSize: 10, textTransform: 'uppercase', display: 'block', marginBottom: 3 }}>
          Comm Rebate %
        </label>
        <input
          style={{ ...inputStyle, width: 100, textAlign: 'right' }}
          type="number" min={0} max={100} step={0.1}
          value={commPct}
          onChange={e => setCommPct(e.target.value)}
        />
      </div>
      <div>
        <label style={{ color: THEME.t3, fontSize: 10, textTransform: 'uppercase', display: 'block', marginBottom: 3 }}>
          PS %
        </label>
        <input
          style={{ ...inputStyle, width: 100, textAlign: 'right' }}
          type="number" min={0} max={100} step={0.1}
          value={psPct}
          onChange={e => setPsPct(e.target.value)}
        />
      </div>
      <button
        onClick={() => onSave(Number(commPct) || 0, Number(psPct) || 0)}
        style={{ ...smallBtn, background: THEME.green, color: '#000', padding: '6px 14px' }}
      >
        Save config
      </button>
      <span style={{ color: THEME.t3, fontSize: 10, marginLeft: 'auto' }}>
        Applies to every member login unless they have their own override.
      </span>
    </div>
  );
}

const th: React.CSSProperties = {
  padding: '6px 8px',
  color: THEME.t3,
  fontSize: 10,
  fontWeight: 700,
  textTransform: 'uppercase',
  letterSpacing: 0.4,
  textAlign: 'left',
};

const thRight: React.CSSProperties = { ...th, textAlign: 'right' };

const td: React.CSSProperties = {
  padding: '5px 8px',
  color: THEME.t1,
  fontSize: 12,
  fontFamily: "'JetBrains Mono', ui-monospace, Menlo, monospace",
};
