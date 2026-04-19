import { THEME } from '../theme';

/**
 * A single shimmering placeholder block. Use in place of real content while a
 * fetch is in flight so the user sees a structural hint of the incoming data
 * instead of a blank region (which looks broken on slow networks).
 *
 * Accepts width/height overrides; defaults to a full-width inline bar.
 */
export function Skeleton({
  width = '100%',
  height = 14,
  radius = 4,
  style,
}: {
  width?: number | string;
  height?: number | string;
  radius?: number;
  style?: React.CSSProperties;
}) {
  return (
    <span
      style={{
        display: 'inline-block',
        width,
        height,
        borderRadius: radius,
        background: `linear-gradient(90deg, ${THEME.bg3} 0%, ${THEME.bg2} 50%, ${THEME.bg3} 100%)`,
        backgroundSize: '200% 100%',
        animation: 'skeleton-shimmer 1.4s ease-in-out infinite',
        ...style,
      }}
    >
      <style>{`
        @keyframes skeleton-shimmer {
          0% { background-position: 200% 0; }
          100% { background-position: -200% 0; }
        }
      `}</style>
    </span>
  );
}

/**
 * Stacked skeleton rows — a convenience wrapper for table-like placeholders.
 */
export function SkeletonRows({ rows = 6, columns = 4 }: { rows?: number; columns?: number }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8, padding: 12 }}>
      {Array.from({ length: rows }).map((_, r) => (
        <div key={r} style={{ display: 'flex', gap: 12 }}>
          {Array.from({ length: columns }).map((_, c) => (
            <Skeleton key={c} width={`${100 / columns}%`} height={14} />
          ))}
        </div>
      ))}
    </div>
  );
}

/**
 * Wraps children in a desaturated / dimmed wrapper when `isStale=true` so the
 * dealer can see at a glance that the numbers on screen are older than expected
 * (e.g., WebSocket disconnected, last refresh > X seconds ago).
 */
export function StaleWrapper({
  isStale,
  children,
  style,
}: {
  isStale: boolean;
  children: React.ReactNode;
  style?: React.CSSProperties;
}) {
  return (
    <div
      style={{
        position: 'relative',
        filter: isStale ? 'saturate(0.5) brightness(0.9)' : undefined,
        transition: 'filter 0.3s ease',
        ...style,
      }}
      title={isStale ? 'Data may be stale \u2014 connection lost or refresh overdue.' : undefined}
    >
      {children}
      {isStale && (
        <div style={{
          position: 'absolute',
          inset: 0,
          pointerEvents: 'none',
          backgroundImage: `repeating-linear-gradient(135deg, transparent 0 8px, ${THEME.amber}0D 8px 16px)`,
        }} />
      )}
    </div>
  );
}
