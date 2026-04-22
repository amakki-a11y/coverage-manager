import { Search, Sun, Moon, Sliders, Bell, BookOpen } from 'lucide-react';
import type { ExposureSummary } from '../types';
import { ConnectionHealthDots } from '../components/ConnectionHealthDots';

/**
 * Top bar — metric tiles on the left (live totals over WebSocket-fed
 * exposure data), utility chips + avatar on the right.
 *
 * Tiles show **floating P&L** (unrealized, from currently-open positions)
 * for each side, plus the broker Net P&L ("Net P&L Today") — the numbers
 * a dealer cares about at a glance. Don't confuse with net *volume* in
 * lots, which lives on the Exposure tab's Summary column.
 */
interface Props {
  summaries: ExposureSummary[];
  mode: 'dark' | 'light';
  alertCount: number;
  onToggleTheme: () => void;
  onOpenPalette: () => void;
  onOpenAlerts: () => void;
  onOpenGuide: () => void;
  onOpenTweaks: () => void;
}

function colorClass(n: number): string {
  if (n > 0) return 'pos';
  if (n < 0) return 'neg';
  return '';
}

function formatSigned(n: number): string {
  if (!Number.isFinite(n) || n === 0) return '0.00';
  const sign = n > 0 ? '+' : '\u2212';
  const abs = Math.abs(n).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  return `${sign}${abs}`;
}

export function Topbar({
  summaries, mode, alertCount,
  onToggleTheme, onOpenPalette, onOpenAlerts, onOpenGuide, onOpenTweaks,
}: Props) {
  // Sum floating P&L across every live symbol. bBookPnL / coveragePnL are
  // already per-symbol `sum(Profit + Swap)` on open positions per ExposureEngine.
  const bbookFloating    = summaries.reduce((s, x) => s + (x.bBookPnL    || 0), 0);
  const coverageFloating = summaries.reduce((s, x) => s + (x.coveragePnL || 0), 0);
  // Broker P&L convention (same as in ExposureEngine / PnLRings): negate
  // client because the broker is the counter-party to the client.
  const netPnlToday      = -bbookFloating + coverageFloating;

  return (
    <div className="topbar">
      <div className="top-totals">
        <div className="top-metric">
          <span className="lbl">Client Floating P&L</span>
          <span className={`val ${colorClass(bbookFloating)}`}>{formatSigned(bbookFloating)}</span>
        </div>
        <div className="top-metric">
          <span className="lbl">Coverage Floating P&L</span>
          <span className={`val ${colorClass(coverageFloating)}`}>{formatSigned(coverageFloating)}</span>
        </div>
        <div className="top-divider" />
        <div className="top-metric highlight">
          <span className="lbl">Net P&L Today</span>
          <span className={`val ${colorClass(netPnlToday)}`}>{formatSigned(netPnlToday)}</span>
        </div>
      </div>

      <div className="top-actions">
        <ConnectionHealthDots />

        <div className="search-wrap" onClick={onOpenPalette}>
          <Search size={14} />
          <input
            className="search"
            placeholder="Search symbol, login, rule…"
            readOnly
            onFocus={onOpenPalette}
          />
          <span className="kbd">⌘K</span>
        </div>

        <button className="icon-btn ghost" title="Alerts" onClick={onOpenAlerts}>
          <Bell size={16} />
          {alertCount > 0 && <span className="badge">{alertCount}</span>}
        </button>

        <button className="icon-btn ghost" title="User guide" onClick={onOpenGuide}>
          <BookOpen size={16} />
        </button>

        <button
          className="icon-btn ghost"
          title={`Switch to ${mode === 'dark' ? 'light' : 'dark'} mode`}
          onClick={onToggleTheme}
        >
          {mode === 'dark' ? <Sun size={16} /> : <Moon size={16} />}
        </button>

        <button className="icon-btn ghost" title="Tweaks" onClick={onOpenTweaks}>
          <Sliders size={16} />
        </button>
      </div>
    </div>
  );
}
