import { THEME } from '../../theme';
import type { BridgeSide } from '../../types/bridge';

export type SideFilter = 'ALL' | BridgeSide;

interface Props {
  fromDate: string;
  toDate: string;
  symbol: string;
  symbols: string[];
  sideFilter: SideFilter;
  anomalyOnly: boolean;
  loading: boolean;
  onFromChange: (v: string) => void;
  onToChange: (v: string) => void;
  onSymbolChange: (v: string) => void;
  onSideChange: (v: SideFilter) => void;
  onAnomalyToggle: (v: boolean) => void;
  onRefresh: () => void;
  connected: boolean;
  healthMode: string;
}

const inputStyle: React.CSSProperties = {
  background: THEME.bg,
  border: `1px solid ${THEME.border}`,
  color: THEME.t1,
  padding: '6px 10px',
  borderRadius: 4,
  fontSize: 12,
  fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace",
};

const labelStyle: React.CSSProperties = {
  display: 'block',
  fontSize: 10,
  color: THEME.t3,
  textTransform: 'uppercase',
  marginBottom: 4,
  letterSpacing: 0.3,
};

export function BridgeFilters({
  fromDate, toDate, symbol, symbols, sideFilter, anomalyOnly, loading,
  onFromChange, onToChange, onSymbolChange, onSideChange, onAnomalyToggle,
  onRefresh, connected, healthMode,
}: Props) {
  return (
    <div style={{
      display: 'flex',
      gap: 12,
      alignItems: 'flex-end',
      padding: 16,
      background: THEME.card,
      borderRadius: 8,
      border: `1px solid ${THEME.border}`,
      marginBottom: 16,
      flexWrap: 'wrap',
    }}>
      <div>
        <label style={labelStyle}>From (UTC)</label>
        <input type="date" value={fromDate} onChange={(e) => onFromChange(e.target.value)} style={inputStyle} />
      </div>
      <div>
        <label style={labelStyle}>To (UTC)</label>
        <input type="date" value={toDate} onChange={(e) => onToChange(e.target.value)} style={inputStyle} />
      </div>

      <div>
        <label style={labelStyle}>Symbol</label>
        <select value={symbol} onChange={(e) => onSymbolChange(e.target.value)} style={{ ...inputStyle, minWidth: 110 }}>
          <option value="">ALL</option>
          {symbols.map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
      </div>

      <div>
        <label style={labelStyle}>Side</label>
        <select value={sideFilter} onChange={(e) => onSideChange(e.target.value as SideFilter)} style={{ ...inputStyle, minWidth: 80 }}>
          <option value="ALL">ALL</option>
          <option value="BUY">BUY</option>
          <option value="SELL">SELL</option>
        </select>
      </div>

      <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12, color: THEME.t2, cursor: 'pointer' }}>
        <input
          type="checkbox"
          checked={anomalyOnly}
          onChange={(e) => onAnomalyToggle(e.target.checked)}
        />
        <span>Anomalies only</span>
      </label>

      <button
        onClick={onRefresh}
        disabled={loading}
        style={{
          padding: '8px 24px',
          background: loading ? THEME.t3 : THEME.blue,
          color: '#fff',
          border: 'none',
          borderRadius: 4,
          fontSize: 12,
          fontWeight: 600,
          cursor: loading ? 'wait' : 'pointer',
        }}
      >
        {loading ? 'Loading\u2026' : 'Refresh'}
      </button>

      <div style={{
        marginLeft: 'auto',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'flex-end',
        fontSize: 10,
        color: THEME.t3,
        fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace",
      }}>
        <div>
          <span style={{
            display: 'inline-block',
            width: 8, height: 8, borderRadius: '50%',
            background: connected ? THEME.green : THEME.red,
            marginRight: 6,
          }} />
          Live {connected ? 'ON' : 'OFF'}
        </div>
        <div>Mode: {healthMode}</div>
      </div>
    </div>
  );
}
