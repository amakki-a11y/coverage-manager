import { THEME } from '../theme';

/**
 * Thin horizontal progress bar for a per-symbol hedge ratio.
 *
 * Colour mirrors the existing hedgeColor() rules in the exposure table:
 *   green  >= 80%
 *   amber  >= 50%
 *   red    <  50%
 *
 * Widths saturate at 100% so 110-120% over-hedges still look "full" rather
 * than overflowing the cell. An over-hedge is still useful info but the
 * cell's numeric label carries that — the bar just needs to show "at/over".
 */
interface Props {
  hedgeRatio: number;
}

export function HedgeBar({ hedgeRatio }: Props) {
  const clamped = Math.max(0, Math.min(100, hedgeRatio));
  const color =
    hedgeRatio >= 80 ? THEME.green
    : hedgeRatio >= 50 ? THEME.amber
    : THEME.red;

  return (
    <div style={{
      width: '100%',
      height: 4,
      borderRadius: 2,
      background: THEME.bg3,
      marginTop: 4,
      overflow: 'hidden',
    }}>
      <div style={{
        width: `${clamped}%`,
        height: '100%',
        background: color,
        transition: 'width 0.25s ease, background 0.25s ease',
      }} />
    </div>
  );
}
