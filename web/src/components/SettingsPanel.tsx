import React, { useState, useEffect, useCallback } from 'react';
import { THEME } from '../theme';
import type { AccountSettings } from '../types';
import { BridgeSettingsCard } from './BridgeSettingsCard';
import type { SnapshotSchedule, SnapshotCadence, ExposureSnapshot, ReconciliationRun } from '../types';
import { formatBeirut, formatBeirutDate } from '../utils/time';
import { ConfirmDialog } from './ConfirmDialog';
import { EquityPnLClientConfigCard } from './EquityPnLClientConfigCard';
import { SpreadRebatesCard } from './SpreadRebatesCard';
import { LoginGroupsCard } from './LoginGroupsCard';

/**
 * Settings tab — organized into 5 sub-tabs to keep what used to be one long
 * scroll tractable:
 *
 *   - **Connections**    — MT5 Manager & Coverage account credentials.
 *   - **Equity P&L**     — Client config · Spread rebates · Login groups.
 *   - **Snapshots**      — Snapshot schedules (cron) · Snapshot history.
 *   - **Data Integrity** — Deal verification · Reconciliation history.
 *   - **Reference**      — Moved accounts · Alert rules.
 *
 * Sub-tab selection persisted in localStorage so a reload returns you to the
 * same section. All writes redact passwords — the panel only sees
 * `passwordSet: true|false`.
 */
const API_BASE = 'http://localhost:5000/api/settings/accounts';

const inputStyle: React.CSSProperties = {
  background: THEME.bg,
  border: `1px solid ${THEME.border}`,
  color: THEME.t1,
  padding: '8px 12px',
  borderRadius: 4,
  fontSize: 13,
  fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace",
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
  const [pendingDelete, setPendingDelete] = useState<AccountSettings | null>(null);
  const [pendingFix, setPendingFix] = useState(false);

  // Settings is 10+ sections deep; a single long scroll overwhelms. Grouping
  // into five functional sub-tabs keeps one concern visible at a time.
  // localStorage-persisted so switching main tabs keeps the sub-tab.
  type SubTab = 'connections' | 'equity' | 'snapshots' | 'integrity' | 'reference';
  const [subTab, setSubTab] = useState<SubTab>(() => {
    const saved = localStorage.getItem('settings.subTab');
    return (saved as SubTab) || 'connections';
  });
  const switchTab = (t: SubTab) => {
    setSubTab(t);
    localStorage.setItem('settings.subTab', t);
  };

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

  const requestDelete = (account: AccountSettings) => setPendingDelete(account);

  const confirmDelete = async () => {
    if (!pendingDelete) return;
    const id = pendingDelete.id;
    setPendingDelete(null);
    await handleDelete(id);
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

  const requestFix = () => setPendingFix(true);

  const handleFix = async () => {
    setPendingFix(false);
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

  const subTabs: Array<{ id: SubTab; label: string; desc: string }> = [
    { id: 'connections', label: 'Connections',    desc: 'MT5 Manager, Coverage, Bridge' },
    { id: 'equity',      label: 'Equity P&L',     desc: 'Rebate + profit-share config' },
    { id: 'snapshots',   label: 'Snapshots',      desc: 'Schedules + capture history' },
    { id: 'integrity',   label: 'Data Integrity', desc: 'Reconciliation + verification' },
    { id: 'reference',   label: 'Reference',      desc: 'Moved accounts + read-only' },
  ];
  const subTabBtn = (t: typeof subTabs[number]): React.CSSProperties => ({
    padding: '10px 16px',
    border: 'none',
    background: subTab === t.id ? THEME.card : 'transparent',
    color: subTab === t.id ? THEME.t1 : THEME.t3,
    borderBottom: subTab === t.id ? `2px solid ${THEME.blue}` : '2px solid transparent',
    cursor: 'pointer',
    fontSize: 13,
    fontWeight: 600,
    textAlign: 'left',
    whiteSpace: 'nowrap',
  });

  return (
    <div style={{ padding: 24, maxWidth: 900, margin: '0 auto' }}>
      <h2 style={{ color: THEME.t1, margin: '0 0 8px', fontSize: 18 }}>Settings</h2>
      <p style={{ color: THEME.t3, fontSize: 12, margin: '0 0 16px' }}>
        {subTabs.find(t => t.id === subTab)?.desc}
      </p>

      {/* Sub-tab nav */}
      <div style={{
        display: 'flex',
        gap: 4,
        borderBottom: `1px solid ${THEME.border}`,
        marginBottom: 24,
        overflowX: 'auto',
      }}>
        {subTabs.map(t => (
          <button key={t.id} onClick={() => switchTab(t.id)} style={subTabBtn(t)}>
            {t.label}
          </button>
        ))}
      </div>

      {/* ── Connections ──────────────────────────────── */}
      {subTab === 'connections' && <>
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
            <AccountCard key={a.id} account={a} onEdit={handleEdit} onDelete={() => requestDelete(a)} />
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
            <h3 style={{ color: THEME.teal, margin: 0, fontSize: 14 }}>Coverage Accounts (LP Terminal)</h3>
            <p style={{ color: THEME.t3, margin: '4px 0 0', fontSize: 12 }}>
              MT5 Terminal — Python collector reads positions every 100ms
            </p>
          </div>
          <button
            onClick={() => { setShowForm('coverage'); setEditId(null); setForm(emptyForm); }}
            style={{ ...btnStyle, background: THEME.teal, color: '#000' }}
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
            <AccountCard key={a.id} account={a} onEdit={handleEdit} onDelete={() => requestDelete(a)} />
          ))
        ) : (
          <div style={{ ...cardStyle, textAlign: 'center', color: THEME.t3, padding: 32 }}>
            No coverage accounts configured
          </div>
        )}
      </div>

      {/* Bridge (Centroid Dropcopy FIX) */}
      <BridgeSettingsCard />
      </>}

      {/* ── Equity P&L ──────────────────────────────── */}
      {subTab === 'equity' && <>
        <LoginGroupsCard />
        <EquityPnLClientConfigCard />
        <SpreadRebatesCard />
      </>}

      {/* ── Snapshots ──────────────────────────────── */}
      {subTab === 'snapshots' && <>
        <SnapshotSchedulesCard />
        <SnapshotHistoryCard />
      </>}

      {/* ── Data Integrity: Reconciliation (above) + Deal Verification (below) ── */}
      {subTab === 'integrity' && <ReconciliationCard />}

      {/* ── Reference: Moved Accounts ──────────────── */}
      {subTab === 'reference' && movedAccounts.length > 0 && (
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
                    <td style={{ ...tdStyle, textAlign: 'left', fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace" }}>{a.login}</td>
                    <td style={{ ...tdStyle, textAlign: 'left', color: THEME.t1 }}>{a.name}</td>
                    <td style={{ ...tdStyle, textAlign: 'left' }}>{a.reason}</td>
                    <td style={{ ...tdStyle, textAlign: 'left' }}>{formatBeirutDate(a.moved_at)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Deal Verification (part of Data Integrity sub-tab) */}
      {subTab === 'integrity' && (
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
                  onClick={requestFix}
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
                background: THEME.badgeGreen,
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
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12, fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace" }}>
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
                        background: s.match ? 'transparent' : THEME.badgeRed,
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
      )}

      {/* Empty-state helpers when a sub-tab has nothing to show */}
      {subTab === 'reference' && movedAccounts.length === 0 && (
        <div style={{ ...cardStyle, textAlign: 'center', color: THEME.t3, padding: 40 }}>
          No moved accounts. When you remove a login from MT5 Manager, it shows up here for reference.
        </div>
      )}

      <ConfirmDialog
        open={pendingDelete !== null}
        title={pendingDelete?.account_type === 'manager' ? 'Delete manager account?' : 'Delete coverage account?'}
        message={pendingDelete
          ? `Login ${pendingDelete.login} on ${pendingDelete.server} will be removed from live connections.\n\nHistorical deals already stored in Supabase are preserved, but the dashboard will no longer receive live updates from this account.`
          : ''}
        confirmLabel="Delete account"
        danger
        onConfirm={confirmDelete}
        onCancel={() => setPendingDelete(null)}
      />

      <ConfirmDialog
        open={pendingFix}
        title="Backfill missing deals from MT5 Manager?"
        message={'This will query MT5 Manager for every login in the selected range and upsert missing deals into Supabase.\n\nWith many logins this puts significant load on the MT5 server. Run during low-activity hours whenever possible.'}
        confirmLabel="Run backfill"
        danger
        onConfirm={handleFix}
        onCancel={() => setPendingFix(false)}
      />
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
  const accentColor = type === 'manager' ? THEME.blue : THEME.teal;

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
  onDelete: () => void;
}) {
  const isManager = account.account_type === 'manager';
  const accentColor = isManager ? THEME.blue : THEME.teal;

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
        <div style={{ fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace", fontSize: 13, color: THEME.t2 }}>
          {account.server || '—'}
        </div>
        <div style={{ fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace", fontSize: 13, color: THEME.t2 }}>
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
        <button onClick={() => onDelete()} style={{ ...btnStyle, background: THEME.badgeRed, color: THEME.red }}>
          Delete
        </button>
      </div>
    </div>
  );
}

// =========================================================================
// Snapshot Schedules section — drives Period P&L "Begin" anchor captures.
// =========================================================================

const SCHEDULES_API = 'http://localhost:5000/api/snapshot-schedules';
const CADENCES: SnapshotCadence[] = ['daily', 'weekly', 'monthly', 'custom'];

interface ScheduleDraft {
  name: string;
  cadence: SnapshotCadence;
  cron_expr: string;
  tz: string;
  enabled: boolean;
}

function SnapshotSchedulesCard() {
  const [items, setItems] = useState<SnapshotSchedule[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [draft, setDraft] = useState<ScheduleDraft>({ name: '', cadence: 'daily', cron_expr: '', tz: 'Asia/Beirut', enabled: true });
  const [editingId, setEditingId] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [pendingDelete, setPendingDelete] = useState<SnapshotSchedule | null>(null);

  const fetchAll = useCallback(async () => {
    try {
      const res = await fetch(SCHEDULES_API);
      if (res.ok) setItems(await res.json());
    } catch { /* ignore */ }
  }, []);

  useEffect(() => { fetchAll(); }, [fetchAll]);

  const save = async () => {
    const body = { ...draft, cron_expr: draft.cron_expr || null };
    try {
      const url = editingId ? `${SCHEDULES_API}/${editingId}` : SCHEDULES_API;
      const method = editingId ? 'PUT' : 'POST';
      await fetch(url, {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      });
      setShowForm(false);
      setEditingId(null);
      setDraft({ name: '', cadence: 'daily', cron_expr: '', tz: 'Asia/Beirut', enabled: true });
      fetchAll();
    } catch { /* ignore */ }
  };

  const startEdit = (s: SnapshotSchedule) => {
    setEditingId(s.id);
    setDraft({
      name: s.name ?? '',
      cadence: s.cadence ?? 'daily',
      cron_expr: s.cron_expr ?? '',
      tz: s.tz ?? 'Asia/Beirut',
      enabled: s.enabled,
    });
    setShowForm(true);
  };

  const toggleEnabled = async (s: SnapshotSchedule) => {
    try {
      await fetch(`${SCHEDULES_API}/${s.id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ...s, enabled: !s.enabled }),
      });
      fetchAll();
    } catch { /* ignore */ }
  };

  const runNow = async (id: string) => {
    setBusy(id);
    try {
      await fetch(`${SCHEDULES_API}/${id}/run-now`, { method: 'POST' });
      await fetchAll();
    } catch { /* ignore */ }
    setBusy(null);
  };

  const requestDelete = (s: SnapshotSchedule) => setPendingDelete(s);

  const confirmDelete = async () => {
    if (!pendingDelete) return;
    const id = pendingDelete.id;
    setPendingDelete(null);
    try {
      await fetch(`${SCHEDULES_API}/${id}`, { method: 'DELETE' });
      fetchAll();
    } catch { /* ignore */ }
  };

  return (
    <div style={{ marginTop: 32 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <div>
          <h3 style={{ color: '#66bb6a', margin: 0, fontSize: 14 }}>Snapshot Schedules (Period P&amp;L)</h3>
          <p style={{ color: THEME.t3, margin: '4px 0 0', fontSize: 12 }}>
            Periodic captures that anchor the "Begin" balance for the Net P&amp;L tab.
            Default schedules use Lebanon time (Asia/Beirut).
          </p>
        </div>
        <button
          onClick={() => { setShowForm(v => !v); setEditingId(null); setDraft({ name: '', cadence: 'daily', cron_expr: '', tz: 'Asia/Beirut', enabled: true }); }}
          style={{ ...btnStyle, background: '#66bb6a', color: '#000' }}
        >
          {showForm ? 'Cancel' : '+ Add Schedule'}
        </button>
      </div>

      {showForm && (
        <div style={{ ...cardStyle, marginBottom: 12, borderColor: '#66bb6a', display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
          <div>
            <label style={labelStyle}>Name</label>
            <input style={inputStyle} value={draft.name} onChange={e => setDraft({ ...draft, name: e.target.value })} placeholder="Daily Close (Lebanon)" />
          </div>
          <div>
            <label style={labelStyle}>Cadence</label>
            <select
              value={draft.cadence}
              onChange={e => setDraft({ ...draft, cadence: e.target.value as SnapshotCadence })}
              style={{ ...inputStyle, width: '100%' }}
            >
              {CADENCES.map(c => <option key={c} value={c}>{c}</option>)}
            </select>
          </div>
          <div>
            <label style={labelStyle}>Timezone</label>
            <input style={inputStyle} value={draft.tz} onChange={e => setDraft({ ...draft, tz: e.target.value })} placeholder="Asia/Beirut" />
          </div>
          <div>
            <label style={labelStyle}>{draft.cadence === 'custom' ? 'Cron Expression' : 'Cron (override, optional)'}</label>
            <input style={inputStyle} value={draft.cron_expr} onChange={e => setDraft({ ...draft, cron_expr: e.target.value })} placeholder={draft.cadence === 'daily' ? '0 0 * * *' : '0 0 * * 1'} />
          </div>
          <div style={{ gridColumn: '1 / span 4', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <label style={{ color: THEME.t2, fontSize: 12, display: 'flex', alignItems: 'center', gap: 6 }}>
              <input type="checkbox" checked={draft.enabled} onChange={e => setDraft({ ...draft, enabled: e.target.checked })} />
              Enabled
            </label>
            <button onClick={save} style={{ ...btnStyle, background: THEME.green, color: '#000' }}>
              {editingId ? 'Update' : 'Save'} Schedule
            </button>
          </div>
        </div>
      )}

      <div style={{ ...cardStyle, padding: 0, overflow: 'auto' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12, fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace" }}>
          <thead>
            <tr style={{ borderBottom: `1px solid ${THEME.border}` }}>
              <th style={{ ...thStyle, textAlign: 'left' }}>Name</th>
              <th style={thStyle}>Cadence</th>
              <th style={thStyle}>Cron</th>
              <th style={thStyle}>Timezone</th>
              <th style={thStyle}>Last Run</th>
              <th style={thStyle}>Next Run</th>
              <th style={thStyle}>Enabled</th>
              <th style={thStyle}>Actions</th>
            </tr>
          </thead>
          <tbody>
            {items.map(s => (
              <tr key={s.id} style={{ borderBottom: `1px solid ${THEME.border}` }}>
                <td style={{ ...tdStyle, textAlign: 'left', color: THEME.t1, fontFamily: 'inherit' }}>{s.name}</td>
                <td style={{ ...tdStyle, color: THEME.teal }}>{s.cadence}</td>
                <td style={{ ...tdStyle, color: THEME.t3 }}>{s.cron_expr ?? '—'}</td>
                <td style={tdStyle}>{s.tz}</td>
                <td style={{ ...tdStyle, color: THEME.t3 }}>{formatBeirut(s.last_run_at)}</td>
                <td style={{ ...tdStyle, color: THEME.t3 }}>{formatBeirut(s.next_run_at)}</td>
                <td style={tdStyle}>
                  <button
                    onClick={() => toggleEnabled(s)}
                    style={{ ...btnStyle, background: s.enabled ? THEME.badgeGreen : THEME.bg3, color: s.enabled ? THEME.green : THEME.t3 }}
                  >
                    {s.enabled ? 'On' : 'Off'}
                  </button>
                </td>
                <td style={{ ...tdStyle, display: 'flex', gap: 4, justifyContent: 'center' }}>
                  <button
                    onClick={() => runNow(s.id)}
                    disabled={busy === s.id}
                    style={{ ...btnStyle, background: THEME.badgeBlue, color: THEME.blue, opacity: busy === s.id ? 0.5 : 1 }}
                  >
                    {busy === s.id ? '…' : 'Run Now'}
                  </button>
                  <button onClick={() => startEdit(s)} style={{ ...btnStyle, background: THEME.bg3, color: THEME.t2 }}>Edit</button>
                  <button onClick={() => requestDelete(s)} style={{ ...btnStyle, background: THEME.badgeRed, color: THEME.red }}>Delete</button>
                </td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr>
                <td colSpan={8} style={{ ...tdStyle, color: THEME.t3, padding: 24 }}>
                  No schedules configured. Add one to start capturing snapshots automatically.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <ConfirmDialog
        open={pendingDelete !== null}
        title="Delete snapshot schedule?"
        message={pendingDelete
          ? `The "${pendingDelete.name}" schedule (${pendingDelete.cadence}) will stop running.\n\nAlready-captured snapshots remain, but no new automatic captures will occur from this schedule.`
          : ''}
        confirmLabel="Delete schedule"
        danger
        onConfirm={confirmDelete}
        onCancel={() => setPendingDelete(null)}
      />
    </div>
  );
}

// =========================================================================
// Snapshot History — lists recent captures (manual + scheduled).
// Each capture = N symbol rows sharing the same snapshot_time, so we group by time.
// =========================================================================

const SNAPSHOTS_API = 'http://localhost:5000/api/exposure/snapshots';

interface GroupedSnapshot {
  snapshotTime: string;
  triggerType: string;
  label: string;
  rowCount: number;
  totalBBookPnl: number;
  totalCoveragePnl: number;
  totalNetPnl: number;
}

function SnapshotHistoryCard() {
  const [groups, setGroups] = useState<GroupedSnapshot[]>([]);
  const [expandedTime, setExpandedTime] = useState<string | null>(null);
  const [expandedRows, setExpandedRows] = useState<ExposureSnapshot[]>([]);
  const [loading, setLoading] = useState(false);

  const fetchHistory = useCallback(async () => {
    setLoading(true);
    try {
      // Default window: last 30 days
      const to = new Date().toISOString();
      const from = new Date(Date.now() - 30 * 24 * 3600_000).toISOString();
      const res = await fetch(`${SNAPSHOTS_API}?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`);
      if (!res.ok) return;
      const rows: ExposureSnapshot[] = await res.json();
      // Group by snapshot_time (same timestamp = single capture)
      const byTime = new Map<string, ExposureSnapshot[]>();
      for (const r of rows) {
        const k = r.snapshot_time;
        if (!byTime.has(k)) byTime.set(k, []);
        byTime.get(k)!.push(r);
      }
      const gs: GroupedSnapshot[] = Array.from(byTime.entries()).map(([t, arr]) => ({
        snapshotTime: t,
        triggerType: arr[0].trigger_type ?? 'scheduled',
        label: arr[0].label ?? '',
        rowCount: arr.length,
        totalBBookPnl: arr.reduce((a, r) => a + (r.bbook_pnl ?? 0), 0),
        totalCoveragePnl: arr.reduce((a, r) => a + (r.coverage_pnl ?? 0), 0),
        totalNetPnl: arr.reduce((a, r) => a + (r.net_pnl ?? 0), 0),
      }));
      gs.sort((a, b) => b.snapshotTime.localeCompare(a.snapshotTime));
      setGroups(gs);
    } catch { /* ignore */ }
    setLoading(false);
  }, []);

  useEffect(() => { fetchHistory(); }, [fetchHistory]);

  const toggleExpand = async (time: string) => {
    if (expandedTime === time) {
      setExpandedTime(null);
      setExpandedRows([]);
      return;
    }
    try {
      // One-tick window to fetch just this snapshot's rows
      const t = new Date(time);
      const from = new Date(t.getTime() - 500).toISOString();
      const to = new Date(t.getTime() + 500).toISOString();
      const res = await fetch(`${SNAPSHOTS_API}?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`);
      if (!res.ok) return;
      const rows: ExposureSnapshot[] = await res.json();
      rows.sort((a, b) => a.canonical_symbol.localeCompare(b.canonical_symbol));
      setExpandedTime(time);
      setExpandedRows(rows);
    } catch { /* ignore */ }
  };

  const fmtUsd = (v: number) => (v >= 0 ? '+' : '') + v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  const colorFor = (v: number) => v > 0 ? THEME.green : v < 0 ? THEME.red : THEME.t3;

  return (
    <div style={{ marginTop: 32 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <div>
          <h3 style={{ color: '#ffa726', margin: 0, fontSize: 14 }}>Snapshot History (last 30 days)</h3>
          <p style={{ color: THEME.t3, margin: '4px 0 0', fontSize: 12 }}>
            Every manual or scheduled capture. Click a row to expand per-symbol values.
          </p>
        </div>
        <button onClick={fetchHistory} disabled={loading} style={{ ...btnStyle, background: THEME.bg3, color: THEME.t2 }}>
          {loading ? 'Refreshing…' : 'Refresh'}
        </button>
      </div>

      <div style={{ ...cardStyle, padding: 0, overflow: 'auto' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12, fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace" }}>
          <thead>
            <tr style={{ borderBottom: `1px solid ${THEME.border}` }}>
              <th style={{ ...thStyle, textAlign: 'left' }}>Captured At (Beirut)</th>
              <th style={thStyle}>Trigger</th>
              <th style={{ ...thStyle, textAlign: 'left' }}>Label</th>
              <th style={thStyle}>Symbols</th>
              <th style={thStyle}>Clients P&amp;L</th>
              <th style={thStyle}>Coverage P&amp;L</th>
              <th style={thStyle}>Net P&amp;L</th>
            </tr>
          </thead>
          <tbody>
            {groups.map(g => (
              <React.Fragment key={g.snapshotTime}>
                <tr
                  onClick={() => toggleExpand(g.snapshotTime)}
                  style={{ borderBottom: `1px solid ${THEME.border}`, cursor: 'pointer', background: expandedTime === g.snapshotTime ? THEME.rowSelected : undefined }}
                >
                  <td style={{ ...tdStyle, textAlign: 'left', color: THEME.t1 }}>
                    {expandedTime === g.snapshotTime ? '▼' : '▶'} {formatBeirut(g.snapshotTime)}
                  </td>
                  <td style={{ ...tdStyle, color: THEME.teal }}>{g.triggerType}</td>
                  <td style={{ ...tdStyle, textAlign: 'left', color: THEME.t2 }}>{g.label || '—'}</td>
                  <td style={tdStyle}>{g.rowCount}</td>
                  <td style={{ ...tdStyle, color: colorFor(g.totalBBookPnl), fontWeight: 600 }}>{fmtUsd(g.totalBBookPnl)}</td>
                  <td style={{ ...tdStyle, color: colorFor(g.totalCoveragePnl), fontWeight: 600 }}>{fmtUsd(g.totalCoveragePnl)}</td>
                  <td style={{ ...tdStyle, color: colorFor(g.totalNetPnl), fontWeight: 700 }}>{fmtUsd(g.totalNetPnl)}</td>
                </tr>
                {expandedTime === g.snapshotTime && expandedRows.map(r => (
                  <tr key={g.snapshotTime + r.canonical_symbol} style={{ borderBottom: `1px solid ${THEME.border}`, background: THEME.rowAlt }}>
                    <td style={{ ...tdStyle, textAlign: 'left', color: THEME.t2, paddingLeft: 32, fontFamily: 'inherit' }} colSpan={3}>
                      {r.canonical_symbol}
                    </td>
                    <td style={tdStyle}>—</td>
                    <td style={{ ...tdStyle, color: colorFor(r.bbook_pnl) }}>{fmtUsd(r.bbook_pnl)}</td>
                    <td style={{ ...tdStyle, color: colorFor(r.coverage_pnl) }}>{fmtUsd(r.coverage_pnl)}</td>
                    <td style={{ ...tdStyle, color: colorFor(r.net_pnl), fontWeight: 600 }}>{fmtUsd(r.net_pnl)}</td>
                  </tr>
                ))}
              </React.Fragment>
            ))}
            {groups.length === 0 && (
              <tr>
                <td colSpan={7} style={{ ...tdStyle, color: THEME.t3, padding: 24 }}>
                  No snapshots captured yet. Use "Capture Snapshot Now" on the Net P&amp;L tab or set up a schedule above.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

const RECON_API = 'http://localhost:5000/api/reconciliation';

function ReconciliationCard() {
  const [runs, setRuns] = useState<ReconciliationRun[]>([]);
  const [loading, setLoading] = useState(false);
  const [running, setRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchRuns = useCallback(async () => {
    setLoading(true);
    try {
      const res = await fetch(`${RECON_API}/status?limit=30`);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = await res.json();
      setRuns(data.runs ?? []);
      setError(null);
    } catch (e: any) {
      setError(e?.message ?? 'Failed to load reconciliation runs');
    }
    setLoading(false);
  }, []);

  useEffect(() => { fetchRuns(); }, [fetchRuns]);

  const runNow = async () => {
    if (!confirm('Run reconciliation sweep now? Defaults to last 14 days. This will backfill missing deals, patch modifications, and delete ghost deals.')) return;
    setRunning(true);
    setError(null);
    try {
      const res = await fetch(`${RECON_API}/run`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({}),
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      await fetchRuns();
    } catch (e: any) {
      setError(e?.message ?? 'Reconciliation run failed');
    }
    setRunning(false);
  };

  const fmtTime = (s?: string | null) => formatBeirut(s);
  const fmtDate = (s?: string | null) => formatBeirutDate(s);
  const fmtNum = (n: number) => (n ?? 0).toLocaleString();
  const colorFor = (n: number) => n > 0 ? THEME.amber : THEME.t3;

  return (
    <div style={{ marginTop: 32 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <div>
          <h3 style={{ color: '#ffa726', margin: 0, fontSize: 14 }}>Deal Reconciliation</h3>
          <p style={{ color: THEME.t3, margin: '4px 0 0', fontSize: 12 }}>
            Nightly sweep (02:05 UTC) diffs MT5 Manager vs Supabase: backfills missed deals, patches dealer modifications, deletes ghost deals. Default lookback 14 days.
          </p>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <button onClick={fetchRuns} disabled={loading || running} style={{ ...btnStyle, background: THEME.bg3, color: THEME.t2 }}>
            {loading ? 'Refreshing…' : 'Refresh'}
          </button>
          <button onClick={runNow} disabled={running} style={{ ...btnStyle, background: THEME.teal, color: '#fff' }}>
            {running ? 'Running…' : 'Run Now'}
          </button>
        </div>
      </div>

      {error && (
        <div style={{ ...cardStyle, borderColor: THEME.red, color: THEME.red, fontSize: 12, marginBottom: 12 }}>
          {error}
        </div>
      )}

      <div style={{ ...cardStyle, padding: 0, overflow: 'auto' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12, fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace" }}>
          <thead>
            <tr style={{ borderBottom: `1px solid ${THEME.border}` }}>
              <th style={{ ...thStyle, textAlign: 'left' }}>Started (Beirut)</th>
              <th style={thStyle}>Trigger</th>
              <th style={thStyle}>Window</th>
              <th style={thStyle}>MT5</th>
              <th style={thStyle}>Supa</th>
              <th style={thStyle}>Backfilled</th>
              <th style={thStyle}>Ghosts Deleted</th>
              <th style={thStyle}>Modified</th>
              <th style={{ ...thStyle, textAlign: 'left' }}>Notes / Error</th>
            </tr>
          </thead>
          <tbody>
            {runs.map(r => (
              <tr key={r.id} style={{ borderBottom: `1px solid ${THEME.border}` }}>
                <td style={{ ...tdStyle, textAlign: 'left', color: THEME.t1 }}>{fmtTime(r.started_at)}</td>
                <td style={{ ...tdStyle, color: r.trigger_type === 'manual' ? THEME.teal : THEME.t2 }}>{r.trigger_type}</td>
                <td style={{ ...tdStyle, color: THEME.t3 }}>{fmtDate(r.window_from)} → {fmtDate(r.window_to)}</td>
                <td style={{ ...tdStyle, color: THEME.t2 }}>{fmtNum(r.mt5_deal_count)}</td>
                <td style={{ ...tdStyle, color: THEME.t2 }}>{fmtNum(r.supabase_deal_count)}</td>
                <td style={{ ...tdStyle, color: colorFor(r.backfilled), fontWeight: r.backfilled > 0 ? 600 : 400 }}>{fmtNum(r.backfilled)}</td>
                <td style={{ ...tdStyle, color: r.ghost_deleted > 0 ? THEME.red : THEME.t3, fontWeight: r.ghost_deleted > 0 ? 600 : 400 }}>{fmtNum(r.ghost_deleted)}</td>
                <td style={{ ...tdStyle, color: colorFor(r.modified), fontWeight: r.modified > 0 ? 600 : 400 }}>{fmtNum(r.modified)}</td>
                <td style={{ ...tdStyle, textAlign: 'left', color: r.error ? THEME.red : THEME.t3 }}>
                  {r.error ? r.error : (r.notes || '—')}
                </td>
              </tr>
            ))}
            {runs.length === 0 && (
              <tr>
                <td colSpan={9} style={{ ...tdStyle, color: THEME.t3, padding: 24 }}>
                  No reconciliation runs yet. Click "Run Now" to trigger the first sweep.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
