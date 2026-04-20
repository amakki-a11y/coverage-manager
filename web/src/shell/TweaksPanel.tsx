import { useEffect } from 'react';
import { Sliders, X } from 'lucide-react';

/**
 * Tweaks panel — right-edge slide-out with dealer-configurable shell
 * preferences. Theme toggle lives in the topbar too; this is the
 * "everything else" drawer for density, accent, and misc booleans.
 *
 * State is held in localStorage via the parent's `tweaks` / `setTweaks`
 * pair, so reloads preserve the dealer's setup. The CSS consumes these
 * via data attributes on <html>:
 *   data-accent="blue|teal|purple"
 *   data-density="compact|cozy|spacious"
 * The parent is responsible for applying those attributes when tweaks
 * change.
 */
export interface Tweaks {
  accent: 'blue' | 'teal' | 'purple';
  density: 'compact' | 'cozy' | 'spacious';
  grid: boolean;
  flash: boolean;
}

export const DEFAULT_TWEAKS: Tweaks = {
  accent: 'blue',
  density: 'compact',
  grid: false,
  flash: true,
};

interface Props {
  open: boolean;
  onClose: () => void;
  tweaks: Tweaks;
  setTweaks: (next: Tweaks) => void;
}

export function TweaksPanel({ open, onClose, tweaks, setTweaks }: Props) {
  const set = <K extends keyof Tweaks>(k: K, v: Tweaks[K]) => setTweaks({ ...tweaks, [k]: v });
  const toggle = (k: 'grid' | 'flash') => setTweaks({ ...tweaks, [k]: !tweaks[k] });

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  return (
    <div className={`tweaks ${open ? 'show' : ''}`}>
      <div className="tweaks-header">
        <Sliders size={16} />
        <div style={{ fontWeight: 600, fontSize: 13 }}>Tweaks</div>
        <div className="spacer" />
        <button className="icon-btn ghost" style={{ padding: '2px 6px' }} onClick={onClose} title="Close (Esc)">
          <X size={14} />
        </button>
      </div>
      <div className="tweaks-body">
        <TGroup label="Appearance">
          <TRow label="Accent">
            <div className="segmented">
              {(['blue', 'teal', 'purple'] as const).map((c) => (
                <button
                  key={c}
                  className={tweaks.accent === c ? 'active' : ''}
                  onClick={() => set('accent', c)}
                >
                  <span style={{
                    display: 'inline-block',
                    width: 9,
                    height: 9,
                    borderRadius: '50%',
                    marginRight: 4,
                    background: c === 'blue' ? '#60A5FA' : c === 'teal' ? '#2DD4BF' : '#A78BFA',
                  }} />
                  {c}
                </button>
              ))}
            </div>
          </TRow>
          <TRow label="Density">
            <div className="segmented">
              {(['compact', 'cozy', 'spacious'] as const).map((d) => (
                <button
                  key={d}
                  className={tweaks.density === d ? 'active' : ''}
                  onClick={() => set('density', d)}
                >
                  {d}
                </button>
              ))}
            </div>
          </TRow>
          <TRow label="Grid lines">
            <span className={`switch ${tweaks.grid ? 'on' : ''}`} onClick={() => toggle('grid')} />
          </TRow>
        </TGroup>
        <TGroup label="Realtime">
          <TRow label="Animate ticks">
            <span className={`switch ${tweaks.flash ? 'on' : ''}`} onClick={() => toggle('flash')} />
          </TRow>
        </TGroup>
      </div>
    </div>
  );
}

function TGroup({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div style={{ padding: '8px 0' }}>
      <div style={{
        color: 'var(--t3)',
        fontSize: 10,
        fontWeight: 700,
        letterSpacing: '0.08em',
        textTransform: 'uppercase',
        marginBottom: 4,
      }}>
        {label}
      </div>
      {children}
    </div>
  );
}

function TRow({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="tweak-row">
      <span className="lbl">{label}</span>
      {children}
    </div>
  );
}
