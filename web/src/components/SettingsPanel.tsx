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

export function SettingsPanel() {
  const [accounts, setAccounts] = useState<AccountSettings[]>([]);
  const [showForm, setShowForm] = useState<'manager' | 'coverage' | null>(null);
  const [form, setForm] = useState<AccountFormData>(emptyForm);
  const [editId, setEditId] = useState<string | null>(null);

  const fetchAccounts = useCallback(async () => {
    try {
      const res = await fetch(API_BASE);
      if (res.ok) setAccounts(await res.json());
    } catch { /* ignore */ }
  }, []);

  useEffect(() => { fetchAccounts(); }, [fetchAccounts]);

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
