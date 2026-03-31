import { useState, useEffect, useCallback } from 'react';
import { THEME } from '../theme';
import type { SymbolMapping } from '../types';

const API_BASE = 'http://localhost:5000/api/mappings';

const inputStyle: React.CSSProperties = {
  background: THEME.bg,
  border: `1px solid ${THEME.border}`,
  color: THEME.t1,
  padding: '6px 10px',
  borderRadius: 4,
  fontSize: 13,
  fontFamily: 'monospace',
  outline: 'none',
  width: '100%',
};

const cellStyle: React.CSSProperties = {
  padding: '6px 10px',
  fontFamily: 'monospace',
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

export function SymbolMappingAdmin() {
  const [mappings, setMappings] = useState<SymbolMapping[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState({
    canonicalName: '',
    bbookSymbol: '',
    bbookContractSize: '100000',
    coverageSymbol: '',
    coverageContractSize: '100000',
    digits: '5',
    profitCurrency: 'USD',
  });

  const fetchMappings = useCallback(async () => {
    try {
      const res = await fetch(API_BASE);
      if (res.ok) setMappings(await res.json());
    } catch { /* ignore */ }
  }, []);

  useEffect(() => { fetchMappings(); }, [fetchMappings]);

  const handleSubmit = async () => {
    try {
      await fetch(API_BASE, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          canonicalName: form.canonicalName,
          bbookSymbol: form.bbookSymbol,
          bbookContractSize: Number(form.bbookContractSize),
          coverageSymbol: form.coverageSymbol,
          coverageContractSize: Number(form.coverageContractSize),
          digits: Number(form.digits),
          profitCurrency: form.profitCurrency,
          isActive: true,
        }),
      });
      setShowForm(false);
      setForm({ canonicalName: '', bbookSymbol: '', bbookContractSize: '100000', coverageSymbol: '', coverageContractSize: '100000', digits: '5', profitCurrency: 'USD' });
      fetchMappings();
    } catch { /* ignore */ }
  };

  const handleDelete = async (id: string) => {
    try {
      await fetch(`${API_BASE}/${id}`, { method: 'DELETE' });
      fetchMappings();
    } catch { /* ignore */ }
  };

  const conversionPreview = () => {
    const bb = Number(form.bbookContractSize) || 1;
    const cov = Number(form.coverageContractSize) || 1;
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
            <input style={inputStyle} value={form.canonicalName} onChange={e => setForm({ ...form, canonicalName: e.target.value })} placeholder="XAUUSD" />
          </div>
          <div>
            <label style={{ color: THEME.t3, fontSize: 10, textTransform: 'uppercase' }}>B-Book Symbol</label>
            <input style={inputStyle} value={form.bbookSymbol} onChange={e => setForm({ ...form, bbookSymbol: e.target.value })} placeholder="XAUUSD" />
          </div>
          <div>
            <label style={{ color: THEME.t3, fontSize: 10, textTransform: 'uppercase' }}>B-Book Contract Size</label>
            <input style={inputStyle} type="number" value={form.bbookContractSize} onChange={e => setForm({ ...form, bbookContractSize: e.target.value })} />
          </div>
          <div>
            <label style={{ color: THEME.t3, fontSize: 10, textTransform: 'uppercase' }}>Coverage Symbol</label>
            <input style={inputStyle} value={form.coverageSymbol} onChange={e => setForm({ ...form, coverageSymbol: e.target.value })} placeholder="GOLD" />
          </div>
          <div>
            <label style={{ color: THEME.t3, fontSize: 10, textTransform: 'uppercase' }}>Coverage Contract Size</label>
            <input style={inputStyle} type="number" value={form.coverageContractSize} onChange={e => setForm({ ...form, coverageContractSize: e.target.value })} />
          </div>
          <div>
            <label style={{ color: THEME.t3, fontSize: 10, textTransform: 'uppercase' }}>Digits</label>
            <input style={inputStyle} type="number" value={form.digits} onChange={e => setForm({ ...form, digits: e.target.value })} />
          </div>
          <div>
            <label style={{ color: THEME.t3, fontSize: 10, textTransform: 'uppercase' }}>Profit Currency</label>
            <input style={inputStyle} value={form.profitCurrency} onChange={e => setForm({ ...form, profitCurrency: e.target.value })} />
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', justifyContent: 'flex-end' }}>
            <div style={{ color: THEME.teal, fontSize: 11, marginBottom: 6 }}>{conversionPreview()}</div>
            <button onClick={handleSubmit} style={{ ...btnStyle, background: THEME.green, color: '#000' }}>
              Save Mapping
            </button>
          </div>
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
            const ratio = m.coverageContractSize / m.bbookContractSize;
            return (
              <tr key={m.id} style={{ borderBottom: `1px solid ${THEME.border}` }}>
                <td style={{ ...cellStyle, color: THEME.t1, fontWeight: 600 }}>{m.canonicalName}</td>
                <td style={{ ...cellStyle, color: THEME.blue }}>{m.bbookSymbol}</td>
                <td style={{ ...cellStyle, textAlign: 'right', color: THEME.t2 }}>{m.bbookContractSize}</td>
                <td style={{ ...cellStyle, color: '#FF8A80' }}>{m.coverageSymbol}</td>
                <td style={{ ...cellStyle, textAlign: 'right', color: THEME.t2 }}>{m.coverageContractSize}</td>
                <td style={{ ...cellStyle, textAlign: 'right', color: THEME.t2 }}>{m.digits}</td>
                <td style={{ ...cellStyle, textAlign: 'right', color: THEME.t2 }}>{m.profitCurrency}</td>
                <td style={{ ...cellStyle, textAlign: 'right', color: THEME.teal }}>1 LP = {ratio.toFixed(4)} BB</td>
                <td style={{ ...cellStyle, textAlign: 'right' }}>
                  <button
                    onClick={() => handleDelete(m.id)}
                    style={{ ...btnStyle, background: 'rgba(255,82,82,0.15)', color: THEME.red }}
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
    </div>
  );
}
