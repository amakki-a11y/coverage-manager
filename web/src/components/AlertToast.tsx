import { useEffect, useRef, useState } from 'react';
import { THEME } from '../theme';
import type { AlertEvent } from '../types';
import { formatBeirutTime } from '../utils/time';

const TOAST_DURATION = 8000;
const MAX_TOASTS = 5;

const severityColor = (s: string) =>
  s === 'critical' ? THEME.red : s === 'warning' ? THEME.amber : THEME.blue;

const severityIcon = (s: string) =>
  s === 'critical' ? '\u26A0' : s === 'warning' ? '\u26A0' : '\u2139';

interface Toast {
  alert: AlertEvent;
  id: string;
  fadeOut: boolean;
}

export function AlertToast({
  alerts,
  soundEnabled,
  onAcknowledge,
}: {
  alerts: AlertEvent[];
  soundEnabled: boolean;
  onAcknowledge: (id: string) => void;
}) {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const processedRef = useRef<Set<string>>(new Set());
  const audioRef = useRef<HTMLAudioElement | null>(null);

  // Create audio element for alert sound
  useEffect(() => {
    const ctx = new AudioContext();
    const createBeep = () => {
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.frequency.value = 880;
      osc.type = 'sine';
      gain.gain.setValueAtTime(0.3, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.3);
      osc.start(ctx.currentTime);
      osc.stop(ctx.currentTime + 0.3);
    };
    audioRef.current = { play: createBeep } as any;
    return () => { ctx.close(); };
  }, []);

  // Process new alerts
  useEffect(() => {
    if (!alerts || alerts.length === 0) return;

    const newAlerts = alerts.filter(a => !processedRef.current.has(a.id));
    if (newAlerts.length === 0) return;

    newAlerts.forEach(a => processedRef.current.add(a.id));

    // Play sound
    if (soundEnabled && audioRef.current) {
      try { (audioRef.current as any).play(); } catch { /* ignore */ }
    }

    setToasts(prev => {
      const added = newAlerts.map(a => ({ alert: a, id: a.id, fadeOut: false }));
      return [...added, ...prev].slice(0, MAX_TOASTS);
    });

    // Auto-dismiss
    const ids = newAlerts.map(a => a.id);
    setTimeout(() => {
      setToasts(prev => prev.map(t => ids.includes(t.id) ? { ...t, fadeOut: true } : t));
      setTimeout(() => {
        setToasts(prev => prev.filter(t => !ids.includes(t.id)));
      }, 300);
    }, TOAST_DURATION);
  }, [alerts, soundEnabled]);

  const dismiss = (id: string) => {
    setToasts(prev => prev.map(t => t.id === id ? { ...t, fadeOut: true } : t));
    setTimeout(() => {
      setToasts(prev => prev.filter(t => t.id !== id));
    }, 300);
  };

  if (toasts.length === 0) return null;

  return (
    <div style={{
      position: 'fixed',
      top: 12,
      right: 12,
      zIndex: 9999,
      display: 'flex',
      flexDirection: 'column',
      gap: 8,
      maxWidth: 380,
    }}>
      {toasts.map(t => {
        const c = severityColor(t.alert.severity);
        return (
          <div
            key={t.id}
            style={{
              background: THEME.card,
              border: `1px solid ${c}`,
              borderLeft: `4px solid ${c}`,
              borderRadius: 8,
              padding: '10px 14px',
              boxShadow: '0 4px 20px rgba(0,0,0,0.4)',
              opacity: t.fadeOut ? 0 : 1,
              transform: t.fadeOut ? 'translateX(100%)' : 'translateX(0)',
              transition: 'all 0.3s ease',
              animation: 'slideIn 0.3s ease',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
              <span style={{ fontSize: 16 }}>{severityIcon(t.alert.severity)}</span>
              <span style={{
                fontSize: 11,
                fontWeight: 700,
                textTransform: 'uppercase',
                color: c,
                letterSpacing: 0.5,
              }}>
                {t.alert.severity}
              </span>
              <span style={{ fontSize: 11, color: THEME.t3, marginLeft: 'auto' }}>
                {t.alert.symbol}
              </span>
              <button
                onClick={() => dismiss(t.id)}
                style={{
                  background: 'none',
                  border: 'none',
                  color: THEME.t3,
                  cursor: 'pointer',
                  fontSize: 16,
                  padding: '0 2px',
                  lineHeight: 1,
                }}
              >
                \u00D7
              </button>
            </div>
            <div style={{ fontSize: 12, color: THEME.t1, lineHeight: 1.4 }}>
              {t.alert.message}
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 6 }}>
              <span style={{ fontSize: 10, color: THEME.t3 }}>
                {formatBeirutTime(t.alert.triggered_at)}
              </span>
              <button
                onClick={() => { onAcknowledge(t.id); dismiss(t.id); }}
                style={{
                  marginLeft: 'auto',
                  background: 'none',
                  border: `1px solid ${THEME.t3}`,
                  borderRadius: 4,
                  color: THEME.t2,
                  fontSize: 10,
                  padding: '2px 8px',
                  cursor: 'pointer',
                }}
              >
                Acknowledge
              </button>
            </div>
          </div>
        );
      })}
      <style>{`
        @keyframes slideIn {
          from { opacity: 0; transform: translateX(100%); }
          to { opacity: 1; transform: translateX(0); }
        }
      `}</style>
    </div>
  );
}
