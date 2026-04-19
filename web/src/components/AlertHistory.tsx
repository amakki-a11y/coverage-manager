import { useEffect, useState } from 'react';
import { THEME } from '../theme';
import type { AlertEvent } from '../types';
import { formatBeirut } from '../utils/time';

const severityColor = (s: string) =>
  s === 'critical' ? THEME.red : s === 'warning' ? THEME.amber : THEME.blue;

export function AlertHistory({
  onClose,
  onAcknowledge,
}: {
  onClose: () => void;
  onAcknowledge: (id: string) => void;
}) {
  const [alerts, setAlerts] = useState<AlertEvent[]>([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState<'all' | 'unacknowledged'>('unacknowledged');

  const fetchAlerts = async () => {
    try {
      const only = filter === 'unacknowledged' ? '?unacknowledgedOnly=true' : '';
      const res = await fetch(`http://localhost:5000/api/alerts/history${only}&limit=200`);
      if (res.ok) {
        const data = await res.json();
        setAlerts(data.events ?? []);
      }
    } catch { /* ignore */ }
    setLoading(false);
  };

  useEffect(() => { fetchAlerts(); }, [filter]);

  const handleAck = async (id: string) => {
    onAcknowledge(id);
    setAlerts(prev => prev.map(a => a.id === id ? { ...a, acknowledged: true, acknowledged_at: new Date().toISOString() } : a));
  };

  const handleAckAll = async () => {
    for (const a of alerts.filter(a => !a.acknowledged)) {
      await onAcknowledge(a.id);
    }
    setAlerts(prev => prev.map(a => ({ ...a, acknowledged: true, acknowledged_at: new Date().toISOString() })));
  };

  return (
    <div style={{
      position: 'fixed',
      inset: 0,
      zIndex: 9998,
      display: 'flex',
      justifyContent: 'center',
      alignItems: 'flex-start',
      paddingTop: 60,
      background: THEME.shadowOverlay,
    }} onClick={(e) => { if (e.target === e.currentTarget) onClose(); }}>
      <div style={{
        width: 600,
        maxHeight: '70vh',
        background: THEME.bg2,
        border: `1px solid ${THEME.border}`,
        borderRadius: 10,
        display: 'flex',
        flexDirection: 'column',
        boxShadow: THEME.shadowModal,
      }}>
        {/* Header */}
        <div style={{
          display: 'flex',
          alignItems: 'center',
          padding: '12px 16px',
          borderBottom: `1px solid ${THEME.border}`,
          gap: 12,
        }}>
          <span style={{ fontSize: 14, fontWeight: 700, color: THEME.t1 }}>Alert History</span>

          <div style={{ display: 'flex', gap: 4, marginLeft: 12 }}>
            {(['unacknowledged', 'all'] as const).map(f => (
              <button
                key={f}
                onClick={() => setFilter(f)}
                style={{
                  padding: '3px 10px',
                  fontSize: 11,
                  border: `1px solid ${filter === f ? THEME.blue : THEME.border}`,
                  borderRadius: 4,
                  background: filter === f ? THEME.blue + '20' : 'transparent',
                  color: filter === f ? THEME.blue : THEME.t3,
                  cursor: 'pointer',
                  textTransform: 'capitalize',
                }}
              >
                {f}
              </button>
            ))}
          </div>

          {filter === 'unacknowledged' && alerts.some(a => !a.acknowledged) && (
            <button
              onClick={handleAckAll}
              style={{
                marginLeft: 'auto',
                padding: '3px 10px',
                fontSize: 11,
                border: `1px solid ${THEME.teal}`,
                borderRadius: 4,
                background: 'transparent',
                color: THEME.teal,
                cursor: 'pointer',
              }}
            >
              Acknowledge All
            </button>
          )}

          <button
            onClick={onClose}
            style={{
              marginLeft: filter === 'unacknowledged' && alerts.some(a => !a.acknowledged) ? 8 : 'auto',
              background: 'none',
              border: 'none',
              color: THEME.t3,
              cursor: 'pointer',
              fontSize: 18,
              lineHeight: 1,
            }}
          >
            {'\u00D7'}
          </button>
        </div>

        {/* Alert list */}
        <div style={{ flex: 1, overflow: 'auto', padding: '8px 0' }}>
          {loading ? (
            <div style={{ padding: 20, textAlign: 'center', color: THEME.t3, fontSize: 12 }}>Loading...</div>
          ) : alerts.length === 0 ? (
            <div style={{ padding: 20, textAlign: 'center', color: THEME.t3, fontSize: 12 }}>No alerts</div>
          ) : (
            alerts.map(a => {
              const c = severityColor(a.severity);
              return (
                <div
                  key={a.id}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: 10,
                    padding: '8px 16px',
                    borderBottom: `1px solid ${THEME.border}`,
                    opacity: a.acknowledged ? 0.5 : 1,
                  }}
                >
                  <div style={{
                    width: 3,
                    height: 32,
                    borderRadius: 2,
                    background: c,
                    flexShrink: 0,
                  }} />
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 2 }}>
                      <span style={{ fontSize: 10, fontWeight: 700, color: c, textTransform: 'uppercase' }}>
                        {a.severity}
                      </span>
                      <span style={{ fontSize: 11, color: THEME.t2, fontWeight: 600 }}>{a.symbol}</span>
                      <span style={{ fontSize: 10, color: THEME.t3, background: THEME.bg3, padding: '1px 6px', borderRadius: 3 }}>
                        {a.trigger_type}
                      </span>
                    </div>
                    <div style={{ fontSize: 11, color: THEME.t1, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                      {a.message}
                    </div>
                  </div>
                  <div style={{ textAlign: 'right', flexShrink: 0 }}>
                    <div style={{ fontSize: 10, color: THEME.t3 }}>
                      {formatBeirut(a.triggered_at)}
                    </div>
                    {!a.acknowledged ? (
                      <button
                        onClick={() => handleAck(a.id)}
                        style={{
                          marginTop: 2,
                          padding: '2px 8px',
                          fontSize: 10,
                          border: `1px solid ${THEME.t3}`,
                          borderRadius: 3,
                          background: 'transparent',
                          color: THEME.t2,
                          cursor: 'pointer',
                        }}
                      >
                        Ack
                      </button>
                    ) : (
                      <span style={{ fontSize: 10, color: THEME.teal }}>Acknowledged</span>
                    )}
                  </div>
                </div>
              );
            })
          )}
        </div>
      </div>
    </div>
  );
}
