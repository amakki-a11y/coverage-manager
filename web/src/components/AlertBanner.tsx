import { THEME } from '../theme';

export function AlertBanner({
  alertCount,
  onShowHistory,
}: {
  alertCount: number;
  onShowHistory: () => void;
}) {
  if (alertCount === 0) return null;

  const isCritical = alertCount >= 3;
  const bg = isCritical ? 'rgba(255,82,82,0.15)' : 'rgba(255,186,66,0.12)';
  const borderColor = isCritical ? THEME.red : THEME.amber;
  const textColor = isCritical ? THEME.red : THEME.amber;

  return (
    <div
      onClick={onShowHistory}
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: 10,
        padding: '6px 16px',
        background: bg,
        borderBottom: `1px solid ${borderColor}`,
        cursor: 'pointer',
        animation: isCritical ? 'pulse 2s infinite' : undefined,
      }}
    >
      <span style={{ fontSize: 14 }}>{'\u26A0'}</span>
      <span style={{ fontSize: 12, fontWeight: 600, color: textColor }}>
        {alertCount} unacknowledged alert{alertCount > 1 ? 's' : ''}
      </span>
      <span style={{ fontSize: 11, color: THEME.t3, marginLeft: 'auto' }}>
        Click to view
      </span>
      <style>{`
        @keyframes pulse {
          0%, 100% { opacity: 1; }
          50% { opacity: 0.7; }
        }
      `}</style>
    </div>
  );
}
