/**
 * @file PnLRings.tsx
 * Broker-edge P&L widget for the Positions Compare tab. Renders the selected
 * symbol name inside two concentric rings — inner for floating (unrealized) P&L,
 * outer for settled (realized) P&L — plus a two-card breakdown underneath.
 *
 * Lives in the right panel of the Compare tab. Replaces the old PriceChart +
 * VolPnlChart stack. Data comes from the parent RightPanel via `data` (live
 * exposure snapshot) and `trades` (today's closed deals for this symbol).
 */
import { THEME } from '../../../theme';
import type { SymbolExposure, TradeRecord } from '../../../types/compare';

/**
 * Props for {@link PnLRings}.
 * @property symbol  Canonical symbol being displayed (e.g. "XAUUSD").
 * @property data    Live exposure snapshot for this symbol (floating P&L sides).
 * @property trades  Closed deals for this symbol within the current window.
 */
interface Props {
  symbol: string;
  data: SymbolExposure;
  trades: TradeRecord[];
}

/**
 * Format a signed USD amount as a compact display string (`+$1,234` / `-$567` / `$0`).
 * Strips fractional cents; the widget is a high-level read, not an audit surface.
 * @param v Amount in dollars.
 * @returns Human-readable string with leading sign and `$` prefix.
 * @example fmtUsd(-1234.56) // "-$1,235"
 */
const fmtUsd = (v: number) => {
  const sign = v > 0 ? '+' : v < 0 ? '-' : '';
  return sign + '$' + Math.abs(v).toLocaleString(undefined, { maximumFractionDigits: 0 });
};

/**
 * Map a signed P&L value to a theme color: green for profit, red for loss,
 * muted tertiary for flat. Used for both arc strokes and card accents.
 * @param v Signed P&L value.
 * @returns A theme color token string.
 */
const colorFor = (v: number) => v > 0 ? THEME.green : v < 0 ? THEME.red : THEME.t3;

/**
 * Build an SVG `path` `d` attribute for a signed arc on a circle centered at
 * the SVG origin. Positive `value` sweeps clockwise from 12 o'clock, negative
 * sweeps counter-clockwise. Arc length is `|value| / scale` of a half-turn,
 * capped at 180°.
 *
 * Returns `''` for zero value or zero scale — callers render an empty `d`
 * safely (browsers draw nothing).
 * @param radius Radius of the arc in SVG user units.
 * @param value  Signed magnitude to encode.
 * @param scale  Reference magnitude that maps to a full 180° sweep.
 * @returns SVG path data string, or `''` if nothing should be drawn.
 * @example arcPath(62, 500, 1000) // sweeps 90° clockwise from top on r=62
 */
function arcPath(radius: number, value: number, scale: number): string {
  if (value === 0 || scale === 0) return '';
  const frac = Math.min(1, Math.abs(value) / scale);
  const sweep = frac * Math.PI; // up to 180°
  const dir = value >= 0 ? 1 : -1;
  const startX = 0;
  const startY = -radius;
  const endX = radius * Math.sin(dir * sweep);
  const endY = -radius * Math.cos(sweep);
  const largeArc = sweep > Math.PI ? 1 : 0;
  const sweepFlag = dir > 0 ? 1 : 0;
  return `M ${startX} ${startY} A ${radius} ${radius} 0 ${largeArc} ${sweepFlag} ${endX} ${endY}`;
}

/**
 * Broker-edge P&L ring widget for a single selected symbol.
 *
 * Computes broker-perspective "edge" as `coverage − client` on both the
 * floating (unrealized) and settled (realized) sides, and renders them as two
 * concentric arcs around the symbol name. Arc lengths are normalized to the
 * larger of the two magnitudes so the rings stay visually comparable.
 *
 * Floating values are read from the live exposure snapshot (`data.clientPnl`,
 * `data.coveragePnl`). Settled values are summed from `trades` filtered by
 * `side` ('client' vs 'coverage'). An empty `trades` array produces a $0
 * settled ring, which is the correct steady state when the tab is loaded
 * intraday before any closes.
 *
 * @param props See {@link Props}.
 * @returns A JSX element that fills its parent flex container.
 * @example
 * <PnLRings symbol="XAUUSD" data={symbolExposure} trades={todaysTrades} />
 */
export function PnLRings({ symbol, data, trades }: Props) {
  // Floating = live unrealized (from exposure snapshot).
  const floatingClient = data.clientPnl;
  const floatingCoverage = data.coveragePnl;
  const floatingNet = floatingCoverage - floatingClient; // broker perspective

  // Settled = realized from today's closed trades.
  const settledClient = trades.filter(t => t.side === 'client').reduce((a, t) => a + t.profit, 0);
  const settledCoverage = trades.filter(t => t.side === 'coverage').reduce((a, t) => a + t.profit, 0);
  const settledNet = settledCoverage - settledClient;

  // Normalize arc lengths against the larger of the two so both rings are comparable.
  const scale = Math.max(Math.abs(floatingNet), Math.abs(settledNet), 1);

  const OUTER_R = 86;
  const INNER_R = 62;
  const RING_W = 8;

  return (
    <div style={{
      width: '100%',
      height: '100%',
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      justifyContent: 'center',
      padding: 12,
      gap: 16,
    }}>
      <svg viewBox="-110 -110 220 220" style={{ width: 260, height: 260, maxWidth: '100%' }}>
        {/* Outer track — Settled */}
        <circle cx="0" cy="0" r={OUTER_R} fill="none" stroke={THEME.border} strokeWidth={RING_W} />
        <path
          d={arcPath(OUTER_R, settledNet, scale)}
          stroke={colorFor(settledNet)}
          strokeWidth={RING_W}
          fill="none"
          strokeLinecap="round"
        />

        {/* Inner track — Floating */}
        <circle cx="0" cy="0" r={INNER_R} fill="none" stroke={THEME.border} strokeWidth={RING_W} />
        <path
          d={arcPath(INNER_R, floatingNet, scale)}
          stroke={colorFor(floatingNet)}
          strokeWidth={RING_W}
          fill="none"
          strokeLinecap="round"
        />

        {/* Symbol name — center */}
        <text
          x="0"
          y="-4"
          textAnchor="middle"
          dominantBaseline="central"
          fill={THEME.t1}
          fontSize="20"
          fontWeight="700"
          fontFamily="monospace"
          letterSpacing="1"
        >
          {symbol}
        </text>
        <text
          x="0"
          y="16"
          textAnchor="middle"
          dominantBaseline="central"
          fill={THEME.t3}
          fontSize="9"
          fontFamily="monospace"
          letterSpacing="1.5"
        >
          BROKER EDGE
        </text>
      </svg>

      <div style={{
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: 12,
        width: '100%',
        maxWidth: 320,
        fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace",
      }}>
        <div style={{
          background: THEME.bg2,
          border: `1px solid ${THEME.border}`,
          borderRadius: 6,
          padding: '10px 12px',
          borderLeft: `3px solid ${colorFor(floatingNet)}`,
        }}>
          <div style={{ color: THEME.t3, fontSize: 10, letterSpacing: 1, marginBottom: 4 }}>FLOATING</div>
          <div style={{ color: colorFor(floatingNet), fontWeight: 700, fontSize: 16 }}>{fmtUsd(floatingNet)}</div>
          <div style={{ color: THEME.t3, fontSize: 10, marginTop: 6, lineHeight: 1.4 }}>
            <div>CLI <span style={{ color: colorFor(-floatingClient) }}>{fmtUsd(floatingClient)}</span></div>
            <div>COV <span style={{ color: colorFor(floatingCoverage) }}>{fmtUsd(floatingCoverage)}</span></div>
          </div>
        </div>

        <div style={{
          background: THEME.bg2,
          border: `1px solid ${THEME.border}`,
          borderRadius: 6,
          padding: '10px 12px',
          borderLeft: `3px solid ${colorFor(settledNet)}`,
        }}>
          <div style={{ color: THEME.t3, fontSize: 10, letterSpacing: 1, marginBottom: 4 }}>SETTLED</div>
          <div style={{ color: colorFor(settledNet), fontWeight: 700, fontSize: 16 }}>{fmtUsd(settledNet)}</div>
          <div style={{ color: THEME.t3, fontSize: 10, marginTop: 6, lineHeight: 1.4 }}>
            <div>CLI <span style={{ color: colorFor(-settledClient) }}>{fmtUsd(settledClient)}</span></div>
            <div>COV <span style={{ color: colorFor(settledCoverage) }}>{fmtUsd(settledCoverage)}</span></div>
          </div>
        </div>
      </div>
    </div>
  );
}
