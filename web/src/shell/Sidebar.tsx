import {
  LayoutGrid, List, Split, Wallet, Layers, Bell, Map as MapIcon,
  Settings, Keyboard,
} from 'lucide-react';

/**
 * Left navigation matching the new design — three labelled sections
 * (Real-time / P&L / Config) with numbered keyboard shortcuts (1-8) and
 * optional alert-count pill on the Alerts item.
 *
 * Tabs are identified by the same string union the main `App` uses, so
 * adding a new tab is two lines here + one line in the App.tsx router.
 */
export type SidebarTab =
  | 'exposure' | 'positions' | 'compare'
  | 'pnl' | 'netpnl' | 'equitypnl'
  | 'mappings' | 'alerts' | 'settings';

interface Item {
  id: SidebarTab;
  label: string;
  icon: React.ComponentType<{ size?: number }>;
  kbd?: string;
  pill?: { kind: 'red' | 'amber' | 'blue'; text: string };
}

interface Props {
  tab: SidebarTab;
  setTab: (t: SidebarTab) => void;
  collapsed: boolean;
  setCollapsed: (v: boolean) => void;
  alertCount: number;
}

export function Sidebar({ tab, setTab, collapsed, setCollapsed, alertCount }: Props) {
  const groups: { label: string; items: Item[] }[] = [
    {
      label: 'Real-time',
      items: [
        { id: 'exposure',  label: 'Exposure',   icon: LayoutGrid, kbd: '1' },
        { id: 'positions', label: 'Positions',  icon: List,       kbd: '2' },
        { id: 'compare',   label: 'Compare',    icon: Split,      kbd: '3' },
      ],
    },
    {
      label: 'P&L',
      items: [
        { id: 'pnl',        label: 'P&L',        icon: Wallet, kbd: '4' },
        { id: 'netpnl',     label: 'Net P&L',    icon: Layers, kbd: '5' },
        { id: 'equitypnl',  label: 'Equity P&L', icon: Wallet, kbd: '6' },
      ],
    },
    {
      label: 'Config',
      items: [
        { id: 'mappings', label: 'Mappings', icon: MapIcon },
        {
          id: 'alerts',
          label: 'Alerts',
          icon: Bell,
          pill: alertCount > 0 ? { kind: 'red', text: String(alertCount) } : undefined,
        },
        { id: 'settings', label: 'Settings', icon: Settings },
      ],
    },
  ];

  return (
    <div className={`sidebar ${collapsed ? 'collapsed' : ''}`}>
      <div className="brand" onClick={() => setCollapsed(!collapsed)} style={{ cursor: 'pointer' }}>
        <div className="brand-mark">C</div>
        {!collapsed && (
          <div>
            <div className="brand-name">Coverage Mgr</div>
            <div className="brand-sub">fxGROW · prod</div>
          </div>
        )}
      </div>

      {groups.map((g) => (
        <div className="nav-section" key={g.label}>
          <div className="nav-label">{g.label}</div>
          {g.items.map((it) => {
            const Icon = it.icon;
            const active = tab === it.id;
            return (
              <div
                key={it.id}
                className={`nav-item ${active ? 'active' : ''}`}
                onClick={() => setTab(it.id)}
                title={collapsed ? it.label : ''}
              >
                <span className="icon"><Icon size={16} /></span>
                <span className="label">{it.label}</span>
                {it.pill && <span className={`pill ${it.pill.kind}`}>{it.pill.text}</span>}
                {it.kbd && <span className="kbd">{it.kbd}</span>}
              </div>
            );
          })}
        </div>
      ))}

      <div className="sidebar-footer">
        <div className="connection">
          <span className="dot" />
          <span className="label">MT5 · Coverage · Bridge live</span>
        </div>
        {!collapsed && (
          <div className="connection" style={{ color: 'var(--t3)' }}>
            <Keyboard size={14} />
            <span className="label" style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5 }}>
              ⌘K · command
            </span>
          </div>
        )}
      </div>
    </div>
  );
}
