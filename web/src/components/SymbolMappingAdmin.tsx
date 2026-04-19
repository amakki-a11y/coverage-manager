import { useState, useEffect, useCallback } from 'react';
import { THEME } from '../theme';
import type { SymbolMapping } from '../types';
import { ConfirmDialog } from './ConfirmDialog';

const API_BASE = 'http://localhost:5000/api/mappings';

const inputStyle: React.CSSProperties = {
  background: THEME.bg,
  border: `1px solid ${THEME.border}`,
  color: THEME.t1,
  padding: '6px 10px',
  borderRadius: 4,
  fontSize: 13,
  fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace",
  outline: 'none',
  width: '100%',
};

const cellStyle: React.CSSProperties = {
  padding: '6px 10px',
  fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace",
  fontSize: 13,
  whiteSpace: 'nowrap',
};

const headerStyle: React.CSSProperties = {
  ...cellStyle,
  color: THEME.t3,
  fontSize: 10,
  textTransform: 'uppercase',
  letterSpacing: 0.5,
  fontWeight: 600,
  fontFamily: 'inherit',
  borderBottom: `1px solid ${THEME.border}`,
};

const btnStyle: React.CSSProperties = {
  padding: '4px 12px',
  borderRadius: 4,
  border: 'none',
  cursor: 'pointer',
  fontSize: 12,
  fontWeight: 600,
};

type EditDraft = {
  canonical_name: string;
  bbook_symbol: string;
  bbook_contract_size: string;
  coverage_symbol: string;
  coverage_contract_size: string;
  digits: string;
  profit_currency: string;
};

export function SymbolMappingAdmin() {
  const [mappings, setMappings] = useState<SymbolMapping[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState({
    canonical_name: '',
    bbook_symbol: '',
    bbook_contract_size: '100000',
    coverage_symbol: '',
    coverage_contract_size: '100000',
    digits: '5',
    profit_currency: 'USD',
  });

  // Inline edit state: id of the row being edited + its draft values.
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editDraft, setEditDraft] = useState<EditDraft | null>(null);

  // Delete confirmation prompt state.
  const [pendingDelete, setPendingDelete] = useState<SymbolMapping | null>(null);

  // Validation error surfaced to the form / inline edit row.
  const [formError, setFormError] = useState<string | null>(null);

  // Validate a draft before submit — catches the most common dealer-desk
  // footguns: 0-or-negative contract size, digits outside MT5's 0..8 range,
  // and an empty canonical key that breaks aggregation silently.
  function validateDraft(d: { canonical_name: string; bbook_symbol: string; bbook_contract_size: string; coverage_symbol: string; coverage_contract_size: string; digits: string; }): string | null {
    if (!d.canonical_name.trim()) return 'Canonical name is required.';
    if (!d.bbook_symbol.trim()) return 'B-Book symbol is required.';
    if (!d.coverage_symbol.trim()) return 'Coverage symbol is required.';
    const bb = Number(d.bbook_contract_size);
    const cov = Number(d.coverage_contract_size);
    if (!Number.isFinite(bb) || bb <= 0) return 'B-Book contract size must be > 0.';
    if (!Number.isFinite(cov) || cov <= 0) return 'Coverage contract size must be > 0.';
    const dig = Number(d.digits);
    if (!Number.isInteger(dig) || dig < 0 || dig > 8) return 'Digits must be an integer between 0 and 8.';
    return null;
  }

  const fetchMappings = useCallback(async () => {
    try {
      const res = await fetch(API_BASE);
      if (res.ok) setMappings(await res.json());
    } catch { /* ignore */ }
  }, []);

  useEffect(() => { fetchMappings(); }, [fetchMappings]);

  const handleSubmit = async () => {
    const err = validateDraft(form);
    if (err) { setFormError(err); return; }
    setFormError(null);
    try {
      await fetch(API_BASE, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          canonical_name: form.canonical_name,
          bbook_symbol: form.bbook_symbol,
          bbook_contract_size: Number(form.bbook_contract_size),
          coverage_symbol: form.coverage_symbol,
          coverage_contract_size: Number(form.coverage_contract_size),
          digits: Number(form.digits),
          profit_currency: form.profit_currency,
          is_active: true,
        }),
      });
      setShowForm(false);
      setForm({ canonical_name: '', bbook_symbol: '', bbook_contract_size: '100000', coverage_symbol: '', coverage_contract_size: '100000', digits: '5', profit_currency: 'USD' });
      fetchMappings();
    } catch { /* ignore */ }
  };

  const handleDelete = async (id: string) => {
    try {
      await fetch(`${API_BASE}/${id}`, { method: 'DELETE' });
      fetchMappings();
    } catch { /* ignore */ }
  };

  const confirmDelete = async () => {
    if (!pendingDelete) return;
    const id = pendingDelete.id;
    setPendingDelete(null);
    await handleDelete(id);
  };

  const startEdit = (m: SymbolMapping) => {
    setEditingId(m.id);
    setEditDraft({
      canonical_name: m.canonical_name ?? '',
      bbook_symbol: m.bbook_symbol ?? '',
      bbook_contract_size: String(m.bbook_contract_size ?? 0),
      coverage_symbol: m.coverage_symbol ?? '',
      coverage_contract_size: String(m.coverage_contract_size ?? 0),
      digits: String(m.digits ?? 0),
      profit_currency: m.profit_currency ?? 'USD',
    });
  };

  const cancelEdit = () => { setEditingId(null); setEditDraft(null); };

  const saveEdit = async (id: string) => {
    if (!editDraft) return;
    const err = validateDraft(editDraft);
    if (err) { setFormError(err); return; }
    setFormError(null);
    try {
      const res = await fetch(`${API_BASE}/${id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          id,
          canonical_name: editDraft.canonical_name,
          bbook_symbol: editDraft.bbook_symbol,
          bbook_contract_size: Number(editDraft.bbook_contract_size),
          coverage_symbol: editDraft.coverage_symbol,
          coverage_contract_size: Number(editDraft.coverage_contract_size),
          digits: Number(editDraft.digits),
          profit_currency: editDraft.profit_currency,
          is_active: true,
        }),
      });
      if (!res.ok) return;
      cancelEdit();
      fetchMappings();
    } catch { /* ignore */ }
  };

  const conversionPreview = () => {
    const bb = Number(form.bbook_contract_size) || 1;
    const cov = Number(form.coverage_contract_size) || 1;
    const ratio = cov / bb;
    return `1 LP lot = ${ratio.toFixed(4)} B-Book lots`;
  };

  return (
    <div style={{ padding: 20 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
        <h3 style={{ color: THEME.t1, margin: 0, fontSize: 16 }}>Symbol Mappings</h3>
        <button
          onClick={() => setShowForm(!showForm)}
          style={{ ...btnStyle, background: THEME.blue, color: '#fff' }}
        >
          {showForm ? 'Cancel' : '+ Add Mapping'}
        </button>
      </div>

      {showForm && (
        <div style={{
          background: THEME.bg3,
          borderRadius: 6,
          padding: 16,
          marginBottom: 16,
          display: 'grid',
          gridTemplateColumns: 'repeat(4, 1fr)',
          gap: 12,
        }}>
          <div>
            <label style={{ color: THEME.t3, fontSize: 10, textTransform: 'uppercase' }}>Canonical Name</label>
            <input style={inputStyle} value={form.canonical_name} onChange={e => setForm({ ...form, canonical_name: e.target.value })} placeholder="XAUUSD" />
          </div>
          <div>
            <label style={{ color: THEME.t3, fontSize: 10, textTransform: 'uppercase' }}>B-Book Symbol</label>
            <input style={inputStyle} value={form.bbook_symbol} onChange={e => setForm({ ...form, bbook_symbol: e.target.value })} placeholder="XAUUSD" />
          </div>
          <div>
            <label style={{ color: THEME.t3, fontSize: 10, textTransform: 'uppercase' }}>B-Book Contract Size</label>
            <input style={inputStyle} type="number" value={form.bbook_contract_size} onChange={e => setForm({ ...form, bbook_contract_size: e.target.value })} />
          </div>
          <div>
            <label style={{ color: THEME.t3, fontSize: 10, textTransform: 'uppercase' }}>Coverage Symbol</label>
            <input style={inputStyle} value={form.coverage_symbol} onChange={e => setForm({ ...form, coverage_symbol: e.target.value })} placeholder="GOLD" />
          </div>
          <div>
            <label style={{ color: THEME.t3, fontSize: 10, textTransform: 'uppercase' }}>Coverage Contract Size</label>
            <input style={inputStyle} type="number" value={form.coverage_contract_size} onChange={e => setForm({ ...form, coverage_contract_size: e.target.value })} />
          </div>
          <div>
            <label style={{ color: THEME.t3, fontSize: 10, textTransform: 'uppercase' }}>Digits</label>
            <input style={inputStyle} type="number" value={form.digits} onChange={e => setForm({ ...form, digits: e.target.value })} />
          </div>
          <div>
            <label style={{ color: THEME.t3, fontSize: 10, textTransform: 'uppercase' }}>Profit Currency</label>
            <input style={inputStyle} value={form.profit_currency} onChange={e => setForm({ ...form, profit_currency: e.target.value })} />
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', justifyContent: 'flex-end' }}>
            <div style={{ color: THEME.teal, fontSize: 11, marginBottom: 6 }}>{conversionPreview()}</div>
            <button onClick={handleSubmit} style={{ ...btnStyle, background: THEME.green, color: '#000' }}>
              Save Mapping
            </button>
          </div>
          {formError && (
            <div style={{
              gridColumn: '1 / -1',
              color: THEME.red,
              fontSize: 12,
              padding: '6px 10px',
              border: `1px solid ${THEME.red}`,
              borderRadius: 4,
              background: THEME.badgeRed,
            }}>
              {formError}
            </div>
          )}
        </div>
      )}

      <table style={{ width: '100%', borderCollapse: 'collapse' }}>
        <thead>
          <tr>
            <th style={{ ...headerStyle, textAlign: 'left' }}>Canonical</th>
            <th style={{ ...headerStyle, textAlign: 'left' }}>B-Book Symbol</th>
            <th style={headerStyle}>BB Contract</th>
            <th style={{ ...headerStyle, textAlign: 'left' }}>Coverage Symbol</th>
            <th style={headerStyle}>Cov Contract</th>
            <th style={headerStyle}>Digits</th>
            <th style={headerStyle}>Currency</th>
            <th style={headerStyle}>Conversion</th>
            <th style={headerStyle}>Actions</th>
          </tr>
        </thead>
        <tbody>
          {mappings.map((m) => {
            const ratio = m.coverage_contract_size / m.bbook_contract_size;
            const isEditing = editingId === m.id && editDraft != null;
            const cellInput: React.CSSProperties = {
              ...inputStyle,
              padding: '4px 6px',
              fontSize: 12,
              width: '100%',
              minWidth: 70,
            };
            if (isEditing) {
              const d = editDraft!;
              const editRatio = (Number(d.coverage_contract_size) || 1) / (Number(d.bbook_contract_size) || 1);
              return (
                <tr key={m.id} style={{ borderBottom: `1px solid ${THEME.border}`, background: THEME.rowSelected }}>
                  <td style={cellStyle}>
                    <input style={cellInput} value={d.canonical_name} onChange={e => setEditDraft({ ...d, canonical_name: e.target.value })} />
                  </td>
                  <td style={cellStyle}>
                    <input style={cellInput} value={d.bbook_symbol} onChange={e => setEditDraft({ ...d, bbook_symbol: e.target.value })} />
                  </td>
                  <td style={cellStyle}>
                    <input style={{ ...cellInput, textAlign: 'right' }} type="number" value={d.bbook_contract_size} onChange={e => setEditDraft({ ...d, bbook_contract_size: e.target.value })} />
                  </td>
                  <td style={cellStyle}>
                    <input style={cellInput} value={d.coverage_symbol} onChange={e => setEditDraft({ ...d, coverage_symbol: e.target.value })} />
                  </td>
                  <td style={cellStyle}>
                    <input style={{ ...cellInput, textAlign: 'right' }} type="number" value={d.coverage_contract_size} onChange={e => setEditDraft({ ...d, coverage_contract_size: e.target.value })} />
                  </td>
                  <td style={cellStyle}>
                    <input style={{ ...cellInput, textAlign: 'right' }} type="number" value={d.digits} onChange={e => setEditDraft({ ...d, digits: e.target.value })} />
                  </td>
                  <td style={cellStyle}>
                    <input style={{ ...cellInput, textAlign: 'right' }} value={d.profit_currency} onChange={e => setEditDraft({ ...d, profit_currency: e.target.value })} />
                  </td>
                  <td style={{ ...cellStyle, textAlign: 'right', color: THEME.teal }}>
                    1 LP = {isFinite(editRatio) ? editRatio.toFixed(4) : '—'} BB
                  </td>
                  <td style={{ ...cellStyle, textAlign: 'right', display: 'flex', gap: 4, justifyContent: 'flex-end' }}>
                    <button
                      onClick={() => saveEdit(m.id)}
                      style={{ ...btnStyle, background: THEME.green, color: '#000' }}
                    >
                      Save
                    </button>
                    <button
                      onClick={cancelEdit}
                      style={{ ...btnStyle, background: THEME.bg3, color: THEME.t2 }}
                    >
                      Cancel
                    </button>
                  </td>
                </tr>
              );
            }
            return (
              <tr key={m.id} style={{ borderBottom: `1px solid ${THEME.border}` }}>
                <td style={{ ...cellStyle, color: THEME.t1, fontWeight: 600 }}>{m.canonical_name}</td>
                <td style={{ ...cellStyle, color: THEME.blue }}>{m.bbook_symbol}</td>
                <td style={{ ...cellStyle, textAlign: 'right', color: THEME.t2 }}>{m.bbook_contract_size}</td>
                <td style={{ ...cellStyle, color: THEME.teal }}>{m.coverage_symbol}</td>
                <td style={{ ...cellStyle, textAlign: 'right', color: THEME.t2 }}>{m.coverage_contract_size}</td>
                <td style={{ ...cellStyle, textAlign: 'right', color: THEME.t2 }}>{m.digits}</td>
                <td style={{ ...cellStyle, textAlign: 'right', color: THEME.t2 }}>{m.profit_currency}</td>
                <td style={{ ...cellStyle, textAlign: 'right', color: THEME.teal }}>1 LP = {isFinite(ratio) ? ratio.toFixed(4) : '—'} BB</td>
                <td style={{ ...cellStyle, textAlign: 'right', display: 'flex', gap: 4, justifyContent: 'flex-end' }}>
                  <button
                    onClick={() => startEdit(m)}
                    style={{ ...btnStyle, background: THEME.badgeBlue, color: THEME.blue }}
                  >
                    Edit
                  </button>
                  <button
                    onClick={() => setPendingDelete(m)}
                    style={{ ...btnStyle, background: THEME.badgeRed, color: THEME.red }}
                  >
                    Delete
                  </button>
                </td>
              </tr>
            );
          })}
          {mappings.length === 0 && (
            <tr>
              <td colSpan={9} style={{ ...cellStyle, textAlign: 'center', color: THEME.t3, padding: 40 }}>
                No mappings configured
              </td>
            </tr>
          )}
        </tbody>
      </table>

      <ConfirmDialog
        open={pendingDelete !== null}
        title="Delete symbol mapping?"
        message={pendingDelete
          ? `This will remove the mapping for "${pendingDelete.canonical_name}" (B-Book ${pendingDelete.bbook_symbol} \u2194 Coverage ${pendingDelete.coverage_symbol}).\n\nLive positions for this symbol will fall back to the raw MT5 name and may be excluded from aggregation until a new mapping exists.`
          : ''}
        confirmLabel="Delete"
        danger
        onConfirm={confirmDelete}
        onCancel={() => setPendingDelete(null)}
      />
    </div>
  );
}
