import { useState, useEffect } from 'react';
import { THEME } from '../theme';
import type { ExposureSummary } from '../types';

/**
 * Top-of-page risk banner.
 *
 * Surfaces the two things a dealer needs to see on first glance:
 *   1. Portfolio-wide unhedged volume (|sum(netVolume)|) against dealer-set
 *      amber/red thresholds stored in localStorage.
 *   2. The worst under-hedged symbol (largest |net| with hedgeRatio < 80%).
 *
 * Hidden when everything is within tolerance to avoid banner fatigue.
 * Thresholds are configurable via the gear icon; defaults tuned for a
 * small-to-mid desk running ~100 lots aggregate.
 */
const LS_AMBER = 'riskBanner.amberLots';
const LS_RED   = 'riskBanner.redLots';
const DEFAULT_AMBER = 50;
const DEFAULT_RED   = 150;

interface Props {
  summaries: ExposureSummary[];
}

export function RiskBanner({ summaries }: Props) {
  const [amberLots, setAmberLots] = useState<number>(() => {
    const n = Number(localStorage.getItem(LS_AMBER));
    return Number.isFinite(n) && n > 0 ? n : DEFAULT_AMBER;
  });
  const [redLots, setRedLots] = useState<number>(() => {
    const n = Number(localStorage.getItem(LS_RED));
    return Number.isFinite(n) && n > 0 ? n : DEFAULT_RED;
  });
  const [configOpen, setConfigOpen] = useState(false);

  useEffect(() => { localStorage.setItem(LS_AMBER, String(amberLots)); }, [amberLots]);
  useEffect(() => { localStorage.setItem(LS_RED,   String(redLots));   }, [redLots]);

  if (summaries.length === 0) return null;

  const unhedgedTotal = summaries.reduce((a, s) => a + Math.abs(s.netVolume ?? 0), 0);

  // Worst-hedged symbol among those with meaningful B-Book net volume.
  // We treat anything < 0.5 lot net as "noise".
  const meaningful = summaries.filter(s => Math.abs(s.bBookNetVolume ?? 0) > 0.5);
  const worstHedge = meaningful
    .filter(s => (s.hedgeRatio ?? 0) < 80)
    .sort((a, b) => Math.abs(b.netVolume ?? 0) - Math.abs(a.netVolume ?? 0))[0];

  const level: 'ok' | 'amber' | 'red' =
    unhedgedTotal >= redLots ? 'red'
    : unhedgedTotal >= amberLots ? 'amber'
    : worstHedge ? 'amber'
    : 'ok';

  if (level === 'ok' && !configOpen) return null;

  const bg         = level === 'red' ? THEME.badgeRed : THEME.badgeAmber;
  const borderCol  = level === 'red' ? THEME.red : THEME.amber;
  const textColor  = level === 'red' ? THEME.red : THEME.amber;

  return (
    <div style={{
      display: 'flex',
      alignItems: 'center',
      gap: 16,
      padding: '8px 16px',
      background: level === 'ok' ? THEME.bg2 : bg,
      borderBottom: `1px solid ${level === 'ok' ? THEME.border : borderCol}`,
    }}>
      {level !== 'ok' && (
        <>
          <span style={{ fontSize: 14 }}>{'\u26A0'}</span>
          <span style={{ fontSize: 12, fontWeight: 700, color: textColor, letterSpacing: 0.3 }}>
            UNHEDGED {unhedgedTotal.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} lots
          </span>
          {worstHedge && (
            <span style={{ fontSize: 12, color: textColor }}>
              {'\u00B7'} worst: <strong>{worstHedge.canonicalSymbol}</strong>{' '}
              ({(worstHedge.hedgeRatio ?? 0).toFixed(0)}% hedged, net{' '}
              {(worstHedge.netVolume ?? 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })})
            </span>
          )}
        </>
      )}
      <button
        onClick={() => setConfigOpen(o => !o)}
        style={{
          marginLeft: 'auto',
          background: 'transparent',
          border: `1px solid ${level === 'ok' ? THEME.border : borderCol}`,
          borderRadius: 4,
          padding: '2px 10px',
          color: level === 'ok' ? THEME.t3 : textColor,
          fontSize: 11,
          fontWeight: 600,
          cursor: 'pointer',
        }}
        title="Configure risk thresholds"
      >
        {configOpen ? 'Close' : 'Thresholds'}
      </button>
      {configOpen && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <label style={{ fontSize: 11, color: textColor }}>
            Amber {'\u2265'}{' '}
            <input
              type="number"
              min={0}
              step={1}
              value={amberLots}
              onChange={e => setAmberLots(Math.max(0, Number(e.target.value) || 0))}
              style={{
                width: 70,
                background: THEME.bg,
                border: `1px solid ${THEME.border}`,
                color: THEME.t1,
                padding: '2px 6px',
                borderRadius: 3,
                fontFamily: "'JetBrains Mono', ui-monospace, Menlo, monospace",
                fontSize: 11,
              }}
            /> lots
          </label>
          <label style={{ fontSize: 11, color: textColor }}>
            Red {'\u2265'}{' '}
            <input
              type="number"
              min={0}
              step={1}
              value={redLots}
              onChange={e => setRedLots(Math.max(0, Number(e.target.value) || 0))}
              style={{
                width: 70,
                background: THEME.bg,
                border: `1px solid ${THEME.border}`,
                color: THEME.t1,
                padding: '2px 6px',
                borderRadius: 3,
                fontFamily: "'JetBrains Mono', ui-monospace, Menlo, monospace",
                fontSize: 11,
              }}
            /> lots
          </label>
        </div>
      )}
    </div>
  );
}
