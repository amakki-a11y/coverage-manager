import { Search, Sun, Moon, Sliders, Bell, BookOpen } from 'lucide-react';
import type { ExposureSummary } from '../types';
import { ConnectionHealthDots } from '../components/ConnectionHealthDots';

/**
 * Top bar — metric tiles on the left (live totals over WebSocket-fed
 * exposure data), utility chips + avatar on the right. Matches the
 * "Client Exposure / Coverage / Net P&L Today" layout in the new
 * design; dropped the "Active Clients" and "Open Positions" tiles
 * that aren't computed on the backend.
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
  const sign = n > 0 ? '+' : n < 0 ? '' : '';
  const abs = Math.abs(n);
  return `${sign}${n < 0 ? '-' : ''}${abs.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

export function Topbar({
  summaries, mode, alertCount,
  onToggleTheme, onOpenPalette, onOpenAlerts, onOpenGuide, onOpenTweaks,
}: Props) {
  const bbNet = summaries.reduce((s, x) => s + (x.bBookNetVolume || 0), 0);
  const covNet = summaries.reduce((s, x) => s + (x.coverageNetVolume || 0), 0);
  const netPnl = summaries.reduce((s, x) => s + (x.netPnL || 0), 0);

  return (
    <div className="topbar">
      <div className="top-totals">
        <div className="top-metric">
          <span className="lbl">Client Exposure</span>
          <span className={`val ${colorClass(bbNet)}`}>{formatSigned(bbNet)}</span>
        </div>
        <div className="top-metric">
          <span className="lbl">Coverage</span>
          <span className={`val ${colorClass(covNet)}`}>{formatSigned(covNet)}</span>
        </div>
        <div className="top-divider" />
        <div className="top-metric highlight">
          <span className="lbl">Net P&L Today</span>
          <span className={`val ${colorClass(netPnl)}`}>{formatSigned(netPnl)}</span>
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
