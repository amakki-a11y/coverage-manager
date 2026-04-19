import { THEME } from '../theme';
import { useConnectionHealth, type Status } from '../hooks/useConnectionHealth';

/**
 * Four-dot per-source health indicator for the top bar.
 *
 * Each dot surfaces the reachability of one upstream service. Colour mapping:
 *   green = healthy
 *   amber = stale / connecting (service reachable but not ready)
 *   red   = unreachable / errored
 *   gray  = unknown (first poll pending, or explicitly disconnected)
 *
 * The hook in `useConnectionHealth.ts` owns polling; this component is pure view.
 */
const LABEL: Record<keyof ReturnType<typeof useConnectionHealth>, string> = {
  mt5:       'MT5 Manager',
  collector: 'Coverage Collector',
  centroid:  'Centroid Bridge',
  supabase:  'Supabase',
};

const ORDER: Array<keyof ReturnType<typeof useConnectionHealth>> = [
  'mt5', 'collector', 'centroid', 'supabase',
];

function dotColor(s: Status): string {
  switch (s) {
    case 'green': return THEME.green;
    case 'amber': return THEME.amber;
    case 'red':   return THEME.red;
    default:      return THEME.t3;
  }
}

function statusText(s: Status): string {
  switch (s) {
    case 'green': return 'Healthy';
    case 'amber': return 'Degraded';
    case 'red':   return 'Unreachable';
    default:      return 'Unknown';
  }
}

export function ConnectionHealthDots() {
  const health = useConnectionHealth();

  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: 6,
        marginLeft: 12,
        paddingLeft: 12,
        borderLeft: `1px solid ${THEME.border}`,
      }}
      aria-label="Connection health"
    >
      {ORDER.map((key) => (
        <span
          key={key}
          title={`${LABEL[key]}: ${statusText(health[key])}`}
          style={{
            display: 'inline-block',
            width: 8,
            height: 8,
            borderRadius: '50%',
            background: dotColor(health[key]),
            boxShadow: health[key] === 'green' ? `0 0 4px ${THEME.green}` : 'none',
            transition: 'background 0.2s ease',
          }}
        />
      ))}
    </div>
  );
}
