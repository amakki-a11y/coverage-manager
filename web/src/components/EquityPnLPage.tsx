import { useState } from 'react';
import { THEME } from '../theme';
import { EquityPnLPanel } from './EquityPnLPanel';
import { LoginGroupsCard } from './LoginGroupsCard';
import { SpreadRebatesCard } from './SpreadRebatesCard';

/**
 * Top-level page for the Equity P&L tab. Wraps the existing data-heavy
 * `EquityPnLPanel` with a sub-tab bar matching the new design:
 *
 *   Table | Login Groups | Spread Rebates
 *
 * The Login Groups and Spread Rebates cards previously lived under the
 * Settings → Equity P&L sub-tab; surfacing them here puts the dealer's
 * configuration one click away from the data they're looking at. The
 * Settings mount point is kept for discoverability but can be removed
 * once dealers are trained on the new layout.
 */
type Sub = 'table' | 'groups' | 'rebates';

const BTN_GROUP: React.CSSProperties = {
  display: 'inline-flex',
  background: THEME.bg3,
  border: `1px solid ${THEME.border}`,
  borderRadius: 6,
  padding: 2,
  gap: 2,
};

function subBtn(active: boolean): React.CSSProperties {
  return {
    padding: '5px 12px',
    fontSize: 12,
    fontWeight: 600,
    color: active ? THEME.t1 : THEME.t3,
    background: active ? THEME.bg : 'transparent',
    border: 'none',
    borderRadius: 4,
    cursor: 'pointer',
    transition: 'all 0.12s',
  };
}

export function EquityPnLPage() {
  const [sub, setSub] = useState<Sub>(() => {
    const saved = localStorage.getItem('equitypnl.sub');
    return saved === 'groups' || saved === 'rebates' ? saved : 'table';
  });

  const choose = (s: Sub) => {
    setSub(s);
    try { localStorage.setItem('equitypnl.sub', s); } catch { /* ignore */ }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0 }}>
      <div style={{
        display: 'flex',
        alignItems: 'center',
        padding: '10px 16px',
        gap: 10,
        borderBottom: `1px solid ${THEME.border}`,
        background: THEME.bg2,
      }}>
        <div style={BTN_GROUP}>
          <button style={subBtn(sub === 'table')}  onClick={() => choose('table')}>Table</button>
          <button style={subBtn(sub === 'groups')} onClick={() => choose('groups')}>Login Groups</button>
          <button style={subBtn(sub === 'rebates')}onClick={() => choose('rebates')}>Spread Rebates</button>
        </div>
      </div>

      <div style={{ flex: 1, overflow: 'auto', display: 'flex', flexDirection: 'column' }}>
        {sub === 'table'   && <EquityPnLPanel />}
        {sub === 'groups'  && <div style={{ padding: 16 }}><LoginGroupsCard /></div>}
        {sub === 'rebates' && <div style={{ padding: 16 }}><SpreadRebatesCard /></div>}
      </div>
    </div>
  );
}
