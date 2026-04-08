import { useState, useEffect, useCallback } from 'react';
import { THEME } from '../theme';
import type { AccountSettings } from '../types';

const API_BASE = 'http://localhost:5000/api/settings/accounts';

const inputStyle: React.CSSProperties = {
  background: THEME.bg,
  border: `1px solid ${THEME.border}`,
  color: THEME.t1,
  padding: '8px 12px',
  borderRadius: 4,
  fontSize: 13,
  fontFamily: 'monospace',
  outline: 'none',
  width: '100%',
};

const labelStyle: React.CSSProperties = {
  color: THEME.t3,
  fontSize: 10,
  textTransform: 'uppercase',
  letterSpacing: 0.5,
  marginBottom: 4,
  display: 'block',
};

const btnStyle: React.CSSProperties = {
  padding: '6px 16px',
  borderRadius: 4,
  border: 'none',
  cursor: 'pointer',
  fontSize: 12,
  fontWeight: 600,
};

const cardStyle: React.CSSProperties = {
  background: THEME.card,
  borderRadius: 8,
  padding: 20,
  border: `1px solid ${THEME.border}`,
};

const thStyle: React.CSSProperties = {
  padding: '8px 6px',
  textAlign: 'center',
  fontSize: 11,
  fontWeight: 600,
  color: THEME.t2,
  textTransform: 'uppercase',
  letterSpacing: 0.3,
};

const thSubStyle: React.CSSProperties = {
  padding: '4px 6px',
  textAlign: 'center',
  fontSize: 10,
  fontWeight: 500,
  color: THEME.t3,
};

const tdStyle: React.CSSProperties = {
  padding: '6px 6px',
  textAlign: 'center',
  fontSize: 12,
  color: THEME.t2,
};

interface AccountFormData {
  label: string;
  server: string;
  login: string;
  password: string;
  group_mask: string;
}

const emptyForm: AccountFormData = {
  label: '',
  server: '',
  login: '',
  password: '',
  group_mask: '*',
};

interface VerifySymbol {
  symbol: string;
  mt5: { buyVolume: number; sellVolume: number; pnl: number; dealCount: number } | null;
  supabase: { buyVolume: number; sellVolume: number; pnl: number; dealCount: number } | null;
  diff: { buyVolume: number; sellVolume: number; pnl: number; dealCount: number };
  match: boolean;
}

interface VerifyResult {
  from: string;
  to: string;
  symbols: VerifySymbol[];
  summary: { totalSymbols: number; matched: number; mismatched: number };
  loginsProcessed: number;
  mt5TotalDeals: number;
  supabaseTotalDeals: number;
  fixed_: number;
  elapsed: string;
}

export function SettingsPanel() {
  const [accounts, setAccounts] = useState<AccountSettings[]>([]);
  const [showForm, setShowForm] = useState<'manager' | 'coverage' | null>(null);
  const [form, setForm] = useState<AccountFormData>(emptyForm);
  const [editId, setEditId] = useState<string | null>(null);
  const [verifyFrom, setVerifyFrom] = useState(() => new Date().toISOString().slice(0, 10));
  const [verifyTo, setVerifyTo] = useState(() => new Date().toISOString().slice(0, 10));
  const [verifyResult, setVerifyResult] = useState<VerifyResult | null>(null);
  const [verifying, setVerifying] = useState(false);
  const [verifyError, setVerifyError] = useState<string | null>(null);
  const [fixing, setFixing] = useState(false);
  const [fixedCount, setFixedCount] = useState<number | null>(null);
  const [movedAccounts, setMovedAccounts] = useState<Array<{ login: number; name: string; reason: string; moved_at: string }>>([]);

  const fetchAccounts = useCallback(async () => {
    try {
      const res = await fetch(API_BASE);
      if (res.ok) setAccounts(await res.json());
    } catch { /* ignore */ }
  }, []);

  useEffect(() => {
    fetchAccounts();
    fetch('http://localhost:5000/api/settings/moved-accounts')
      .then(r => r.ok ? r.json() : [])
      .then(setMovedAccounts)
      .catch(() => {});
  }, [fetchAccounts]);

  const managerAccounts = accounts.filter(a => a.account_type === 'manager');
  const coverageAccounts = accounts.filter(a => a.account_type === 'coverage');

  const handleSave = async () => {
    if (!showForm) return;
    try {
      const body: Record<string, unknown> = {
        account_type: showForm,
        label: form.label,
        server: form.server,
        login: Number(form.login),
        password: form.password,
        group_mask: form.group_mask,
        is_active: true,
      };
      if (editId) body.id = editId;

      const method = editId ? 'PUT' : 'POST';
      const url = editId ? `${API_BASE}/${editId}` : API_BASE;

      await fetch(url, {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      });
      setShowForm(null);
      setEditId(null);
      setForm(emptyForm);
      fetchAccounts();
    } catch { /* ignore */ }
  };

  const handleEdit = (account: AccountSettings) => {
    setShowForm(account.account_type);
    setEditId(account.id);
    setForm({
      label: account.label,
      server: account.server,
      login: String(account.login),
      password: account.password,
      group_mask: account.group_mask,
    });
  };

  const handleDelete = async (id: string) => {
    try {
      await fetch(`${API_BASE}/${id}`, { method: 'DELETE' });
      fetchAccounts();
    } catch { /* ignore */ }
  };

  const handleCancel = () => {
    setShowForm(null);
    setEditId(null);
    setForm(emptyForm);
  };

  const handleVerify = async () => {
    setVerifying(true);
    setVerifyError(null);
    setVerifyResult(null);
    try {
      const res = await fetch(`http://localhost:5000/api/exposure/verify?from=${verifyFrom}&to=${verifyTo}`);
      if (!res.ok) {
        const err = await res.json().catch(() => ({ error: 'Request failed' }));
        setVerifyError(err.error || `HTTP ${res.status}`);
        return;
      }
      setVerifyResult(await res.json());
      setFixedCount(null);
    } catch {
      setVerifyError('Failed to connect to backend');
    } finally {
      setVerifying(false);
    }
  };

  const handleFix = async () => {
    if (!confirm(
      'WARNING: This will query MT5 Manager for all logins and upsert missing deals to Supabase.\n\n' +
      'With many logins this puts load on the MT5 server.\n' +
      'Best to run during low-activity hours.\n\n' +
      'Continue?'
    )) return;

    setFixing(true);
    setVerifyError(null);
    try {
      const res = await fetch(`http://localhost:5000/api/exposure/verify?from=${verifyFrom}&to=${verifyTo}&fix=true`);
      if (!res.ok) {
        const err = await res.json().catch(() => ({ error: 'Request failed' }));
        setVerifyError(err.error || `HTTP ${res.status}`);
        return;
      }
      const result = await res.json();
      setVerifyResult(result);
      setFixedCount(result.fixed_);
    } catch {
      setVerifyError('Failed to connect to backend');
    } finally {
      setFixing(false);
    }
  };

  return (
    <div style={{ padding: 24, maxWidth: 900, margin: '0 auto' }}>
      <h2 style={{ color: THEME.t1, margin: '0 0 24px', fontSize: 18 }}>Account Settings</h2>

      {/* Manager Accounts */}
      <div style={{ marginBottom: 32 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
          <div>
            <h3 style={{ color: THEME.blue, margin: 0, fontSize: 14 }}>Manager Accounts (B-Book)</h3>
            <p style={{ color: THEME.t3, margin: '4px 0 0', fontSize: 12 }}>
              MT5 Manager API — receives deal callbacks for client positions
            </p>
          </div>
          <button
            onClick={() => { setShowForm('manager'); setEditId(null); setForm(emptyForm); }}
            style={{ ...btnStyle, background: THEME.blue, color: '#fff' }}
          >
            + Add Manager
          </button>
        </div>

        {showForm === 'manager' && (
          <AccountForm
            form={form}
            setForm={setForm}
            onSave={handleSave}
            onCancel={handleCancel}
            type="manager"
            isEdit={!!editId}
          />
        )}

        {managerAccounts.length > 0 ? (
          managerAccounts.map(a => (
            <AccountCard key={a.id} account={a} onEdit={handleEdit} onDelete={handleDelete} />
          ))
        ) : (
          <div style={{ ...cardStyle, textAlign: 'center', color: THEME.t3, padding: 32 }}>
            No manager accounts configured
          </div>
        )}
      </div>

      {/* Coverage Accounts */}
      <div>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
          <div>
            <h3 style={{ color: '#FF8A80', margin: 0, fontSize: 14 }}>Coverage Accounts (LP Terminal)</h3>
            <p style={{ color: THEME.t3, margin: '4px 0 0', fontSize: 12 }}>
              MT5 Terminal — Python collector reads positions every 100ms
            </p>
          </div>
          <button
            onClick={() => { setShowForm('coverage'); setEditId(null); setForm(emptyForm); }}
            style={{ ...btnStyle, background: '#FF8A80', color: '#000' }}
          >
            + Add Coverage
          </button>
        </div>

        {showForm === 'coverage' && (
          <AccountForm
            form={form}
            setForm={setForm}
            onSave={handleSave}
            onCancel={handleCancel}
            type="coverage"
            isEdit={!!editId}
          />
        )}

        {coverageAccounts.length > 0 ? (
          coverageAccounts.map(a => (
            <AccountCard key={a.id} account={a} onEdit={handleEdit} onDelete={handleDelete} />
          ))
        ) : (
          <div style={{ ...cardStyle, textAlign: 'center', color: THEME.t3, padding: 32 }}>
            No coverage accounts configured
          </div>
        )}
      </div>

      {/* Moved Accounts */}
      {movedAccounts.length > 0 && (
        <div style={{ marginTop: 32 }}>
          <div style={{ marginBottom: 12 }}>
            <h3 style={{ color: THEME.t3, margin: 0, fontSize: 14 }}>Moved Accounts (Excluded from Exposure)</h3>
            <p style={{ color: THEME.t3, margin: '4px 0 0', fontSize: 12 }}>
              Accounts removed from MT5 Manager — deals kept in Supabase but hidden from dashboard
            </p>
          </div>
          <div style={{ ...cardStyle, padding: 0, overflow: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
              <thead>
                <tr style={{ borderBottom: `1px solid ${THEME.border}` }}>
                  <th style={{ ...thStyle, textAlign: 'left' }}>Login</th>
                  <th style={{ ...thStyle, textAlign: 'left' }}>Name</th>
                  <th style={{ ...thStyle, textAlign: 'left' }}>Reason</th>
                  <th style={{ ...thStyle, textAlign: 'left' }}>Date Moved</th>
                </tr>
              </thead>
              <tbody>
                {movedAccounts.map(a => (
                  <tr key={a.login} style={{ borderBottom: `1px solid ${THEME.border}` }}>
                    <td style={{ ...tdStyle, textAlign: 'left', fontFamily: 'monospace' }}>{a.login}</td>
                    <td style={{ ...tdStyle, textAlign: 'left', color: THEME.t1 }}>{a.name}</td>
                    <td style={{ ...tdStyle, textAlign: 'left' }}>{a.reason}</td>
                    <td style={{ ...tdStyle, textAlign: 'left' }}>{new Date(a.moved_at).toLocaleDateString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Deal Verification */}
      <div style={{ marginTop: 32 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
          <div>
            <h3 style={{ color: '#FFA726', margin: 0, fontSize: 14 }}>Deal Verification (MT5 vs Supabase)</h3>
            <p style={{ color: THEME.t3, margin: '4px 0 0', fontSize: 12 }}>
              Compare MT5 Manager deals against Supabase to detect missing or mismatched data
            </p>
          </div>
        </div>

        <div style={{ ...cardStyle, marginBottom: 12 }}>
          <div style={{ display: 'flex', gap: 12, alignItems: 'flex-end', flexWrap: 'wrap' }}>
            <div>
              <label style={labelStyle}>From</label>
              <input
                style={{ ...inputStyle, width: 150 }}
                type="date"
                value={verifyFrom}
                onChange={e => setVerifyFrom(e.target.value)}
              />
            </div>
            <div>
              <label style={labelStyle}>To</label>
              <input
                style={{ ...inputStyle, width: 150 }}
                type="date"
                value={verifyTo}
                onChange={e => setVerifyTo(e.target.value)}
              />
            </div>
            <button
              onClick={handleVerify}
              disabled={verifying}
              style={{
                ...btnStyle,
                background: verifying ? THEME.t3 : '#FFA726',
                color: '#000',
                padding: '8px 24px',
                cursor: verifying ? 'wait' : 'pointer',
              }}
            >
              {verifying ? 'Verifying...' : 'Run Verification'}
            </button>
          </div>

          {verifying && (
            <p style={{ color: THEME.t3, fontSize: 12, marginTop: 12, marginBottom: 0 }}>
              Querying MT5 Manager (batching 1,000 logins at a time)... This may take a few minutes.
            </p>
          )}

          {verifyError && (
            <p style={{ color: THEME.red, fontSize: 12, marginTop: 12, marginBottom: 0 }}>
              Error: {verifyError}
            </p>
          )}
        </div>

        {verifyResult && (
          <>
            {/* Summary */}
            <div style={{
              ...cardStyle,
              marginBottom: 12,
              display: 'flex',
              gap: 24,
              flexWrap: 'wrap',
              alignItems: 'center',
            }}>
              <div style={{ fontSize: 13, color: THEME.t1 }}>
                <strong>{verifyResult.summary.totalSymbols}</strong>
                <span style={{ color: THEME.t3, marginLeft: 4 }}>symbols</span>
              </div>
              <div style={{ fontSize: 13, color: THEME.green }}>
                <strong>{verifyResult.summary.matched}</strong> matched
              </div>
              <div style={{ fontSize: 13, color: verifyResult.summary.mismatched > 0 ? THEME.red : THEME.t3 }}>
                <strong>{verifyResult.summary.mismatched}</strong> mismatched
              </div>
              <div style={{ fontSize: 12, color: THEME.t3 }}>
                MT5: {verifyResult.mt5TotalDeals.toLocaleString()} deals
              </div>
              <div style={{ fontSize: 12, color: THEME.t3 }}>
                Supabase: {verifyResult.supabaseTotalDeals.toLocaleString()} deals
              </div>
              <div style={{ fontSize: 12, color: THEME.t3 }}>
                {verifyResult.loginsProcessed.toLocaleString()} logins
              </div>
              <div style={{ fontSize: 12, color: THEME.t3, marginLeft: 'auto' }}>
                Elapsed: {verifyResult.elapsed}
              </div>
            </div>

            {/* Fix Missing + Risk Warning */}
            {verifyResult.summary.mismatched > 0 && (
              <div style={{
                ...cardStyle,
                marginBottom: 12,
                borderColor: '#FFA726',
                display: 'flex',
                alignItems: 'center',
                gap: 16,
                flexWrap: 'wrap',
              }}>
                <div style={{ flex: 1, minWidth: 200 }}>
                  <div style={{ color: '#FFA726', fontSize: 13, fontWeight: 600, marginBottom: 4 }}>
                    {verifyResult.mt5TotalDeals - verifyResult.supabaseTotalDeals} deals missing from Supabase
                  </div>
                  <div style={{ color: THEME.t3, fontSize: 11, lineHeight: 1.4 }}>
                    This will re-query MT5 Manager and upsert missing deals to Supabase.
                    With many logins this puts load on the MT5 server — best during low-activity hours.
                  </div>
                </div>
                <button
                  onClick={handleFix}
                  disabled={fixing}
                  style={{
                    ...btnStyle,
                    background: fixing ? THEME.t3 : THEME.red,
                    color: '#fff',
                    padding: '8px 20px',
                    cursor: fixing ? 'wait' : 'pointer',
                  }}
                >
                  {fixing ? 'Fixing...' : 'Fix Missing Deals'}
                </button>
              </div>
            )}

            {/* Fixed confirmation */}
            {fixedCount !== null && (
              <div style={{
                ...cardStyle,
                marginBottom: 12,
                borderColor: THEME.green,
                background: 'rgba(76,175,80,0.08)',
              }}>
                <span style={{ color: THEME.green, fontSize: 13, fontWeight: 600 }}>
                  Fixed {fixedCount} missing deals — upserted to Supabase successfully.
                </span>
                {fixedCount === 0 && (
                  <span style={{ color: THEME.t3, fontSize: 12, marginLeft: 8 }}>
                    All deals already in sync.
                  </span>
                )}
              </div>
            )}

            {/* Results Table */}
            <div style={{ ...cardStyle, padding: 0, overflow: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12, fontFamily: 'monospace' }}>
                <thead>
                  <tr style={{ borderBottom: `2px solid ${THEME.border}` }}>
                    <th style={thStyle}>Symbol</th>
                    <th style={{ ...thStyle, color: THEME.blue }} colSpan={4}>MT5 Manager</th>
                    <th style={{ ...thStyle, color: '#FFA726' }} colSpan={4}>Supabase</th>
                    <th style={thStyle}>Diff Deals</th>
                    <th style={thStyle}>Status</th>
                  </tr>
                  <tr style={{ borderBottom: `1px solid ${THEME.border}` }}>
                    <th style={thStyle}></th>
                    <th style={thSubStyle}>Buy Vol</th>
                    <th style={thSubStyle}>Sell Vol</th>
                    <th style={thSubStyle}>Deals</th>
                    <th style={thSubStyle}>P&L</th>
                    <th style={thSubStyle}>Buy Vol</th>
                    <th style={thSubStyle}>Sell Vol</th>
                    <th style={thSubStyle}>Deals</th>
                    <th style={thSubStyle}>P&L</th>
                    <th style={thSubStyle}></th>
                    <th style={thSubStyle}></th>
                  </tr>
                </thead>
                <tbody>
                  {verifyResult.symbols.map(s => (
                    <tr
                      key={s.symbol}
                      style={{
                        borderBottom: `1px solid ${THEME.border}`,
                        background: s.match ? 'transparent' : 'rgba(255,82,82,0.08)',
                      }}
                    >
                      <td style={{ ...tdStyle, fontWeight: 600, color: THEME.t1 }}>{s.symbol}</td>
                      <td style={{ ...tdStyle, color: THEME.green }}>{s.mt5?.buyVolume.toFixed(2) ?? '—'}</td>
                      <td style={{ ...tdStyle, color: THEME.red }}>{s.mt5?.sellVolume.toFixed(2) ?? '—'}</td>
                      <td style={tdStyle}>{s.mt5?.dealCount.toLocaleString() ?? '—'}</td>
                      <td style={{ ...tdStyle, color: (s.mt5?.pnl ?? 0) >= 0 ? THEME.green : THEME.red }}>
                        {s.mt5?.pnl.toFixed(2) ?? '—'}
                      </td>
                      <td style={{ ...tdStyle, color: THEME.green }}>{s.supabase?.buyVolume.toFixed(2) ?? '—'}</td>
                      <td style={{ ...tdStyle, color: THEME.red }}>{s.supabase?.sellVolume.toFixed(2) ?? '—'}</td>
                      <td style={tdStyle}>{s.supabase?.dealCount.toLocaleString() ?? '—'}</td>
                      <td style={{ ...tdStyle, color: (s.supabase?.pnl ?? 0) >= 0 ? THEME.green : THEME.red }}>
                        {s.supabase?.pnl.toFixed(2) ?? '—'}
                      </td>
                      <td style={{
                        ...tdStyle,
                        fontWeight: s.diff.dealCount !== 0 ? 700 : 400,
                        color: s.diff.dealCount !== 0 ? THEME.red : THEME.t3,
                      }}>
                        {s.diff.dealCount !== 0 ? `${s.diff.dealCount > 0 ? '+' : ''}${s.diff.dealCount}` : '0'}
                      </td>
                      <td style={{ ...tdStyle, textAlign: 'center' }}>
                        {s.match
                          ? <span style={{ color: THEME.green, fontSize: 14 }}>OK</span>
                          : <span style={{ color: THEME.red, fontSize: 14, fontWeight: 700 }}>DIFF</span>
                        }
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}
      </div>
    </div>
  );
}

function AccountForm({
  form, setForm, onSave, onCancel, type, isEdit,
}: {
  form: AccountFormData;
  setForm: (f: AccountFormData) => void;
  onSave: () => void;
  onCancel: () => void;
  type: 'manager' | 'coverage';
  isEdit: boolean;
}) {
  const accentColor = type === 'manager' ? THEME.blue : '#FF8A80';

  return (
    <div style={{
      ...cardStyle,
      marginBottom: 12,
      borderColor: accentColor,
      borderWidth: 1,
    }}>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginBottom: 16 }}>
        <div>
          <label style={labelStyle}>Label</label>
          <input
            style={inputStyle}
            value={form.label}
            onChange={e => setForm({ ...form, label: e.target.value })}
            placeholder={type === 'manager' ? 'Main B-Book Server' : 'LP Coverage Terminal'}
          />
        </div>
        <div>
          <label style={labelStyle}>Server (IP:Port)</label>
          <input
            style={inputStyle}
            value={form.server}
            onChange={e => setForm({ ...form, server: e.target.value })}
            placeholder="89.21.67.56:443"
          />
        </div>
        <div>
          <label style={labelStyle}>Login</label>
          <input
            style={inputStyle}
            type="number"
            value={form.login}
            onChange={e => setForm({ ...form, login: e.target.value })}
            placeholder="12345"
          />
        </div>
        <div>
          <label style={labelStyle}>Password</label>
          <input
            style={{ ...inputStyle }}
            type="password"
            value={form.password}
            onChange={e => setForm({ ...form, password: e.target.value })}
            placeholder="••••••••"
          />
        </div>
        {type === 'manager' && (
          <div>
            <label style={labelStyle}>Group Mask</label>
            <input
              style={inputStyle}
              value={form.group_mask}
              onChange={e => setForm({ ...form, group_mask: e.target.value })}
              placeholder="*"
            />
          </div>
        )}
      </div>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
        <button onClick={onCancel} style={{ ...btnStyle, background: THEME.bg3, color: THEME.t2 }}>
          Cancel
        </button>
        <button onClick={onSave} style={{ ...btnStyle, background: THEME.green, color: '#000' }}>
          {isEdit ? 'Update' : 'Save'} Account
        </button>
      </div>
    </div>
  );
}

function AccountCard({
  account, onEdit, onDelete,
}: {
  account: AccountSettings;
  onEdit: (a: AccountSettings) => void;
  onDelete: (id: string) => void;
}) {
  const isManager = account.account_type === 'manager';
  const accentColor = isManager ? THEME.blue : '#FF8A80';

  return (
    <div style={{ ...cardStyle, marginBottom: 8, display: 'flex', alignItems: 'center', gap: 16 }}>
      <div style={{
        width: 4,
        height: 48,
        borderRadius: 2,
        background: accentColor,
        flexShrink: 0,
      }} />
      <div style={{ flex: 1, display: 'grid', gridTemplateColumns: '1.5fr 2fr 1fr 1fr', gap: 16, alignItems: 'center' }}>
        <div>
          <div style={{ color: THEME.t1, fontSize: 14, fontWeight: 600 }}>{account.label || 'Unnamed'}</div>
          <div style={{ color: THEME.t3, fontSize: 11, marginTop: 2 }}>
            {isManager ? 'Manager API' : 'Terminal'}
          </div>
        </div>
        <div style={{ fontFamily: 'monospace', fontSize: 13, color: THEME.t2 }}>
          {account.server || '—'}
        </div>
        <div style={{ fontFamily: 'monospace', fontSize: 13, color: THEME.t2 }}>
          Login: {account.login || '—'}
        </div>
        <div style={{
          display: 'flex',
          alignItems: 'center',
          gap: 6,
        }}>
          <div style={{
            width: 6, height: 6, borderRadius: '50%',
            background: account.is_active ? THEME.green : THEME.t3,
          }} />
          <span style={{ color: THEME.t3, fontSize: 11 }}>
            {account.is_active ? 'Active' : 'Inactive'}
          </span>
        </div>
      </div>
      <div style={{ display: 'flex', gap: 6, flexShrink: 0 }}>
        <button onClick={() => onEdit(account)} style={{ ...btnStyle, background: THEME.bg3, color: THEME.t2 }}>
          Edit
        </button>
        <button onClick={() => onDelete(account.id)} style={{ ...btnStyle, background: 'rgba(255,82,82,0.15)', color: THEME.red }}>
          Delete
        </button>
      </div>
    </div>
  );
}
