import { useState, useEffect } from 'react';
import { THEME } from '../../../theme';
import { AccountModal } from './AccountModal';
import { API_BASE } from '../../../config';

interface Position {
  source: string;
  login: number;
  symbol: string;
  canonicalSymbol: string;
  direction: string;
  volumeLots: number;
  openPrice: number;
  currentPrice: number;
  profit: number;
  swap: number;
  openTime: string;
}

interface LoginSummary {
  login: number;
  direction: string;
  volume: number;
  profit: number;
  positions: number;
}

interface LoginsWidgetProps {
  symbol: string;
}

export function LoginsWidget({ symbol }: LoginsWidgetProps) {
  const [logins, setLogins] = useState<LoginSummary[]>([]);
  const [selectedLogin, setSelectedLogin] = useState<number | null>(null);

  useEffect(() => {
    const fetchPositions = async () => {
      try {
        const res = await fetch(`${API_BASE}/api/exposure/positions`);
        if (!res.ok) return;
        const positions: Position[] = await res.json();

        const canonical = symbol.replace(/[-.]$/, '').toUpperCase();

        const filtered = positions.filter(p => {
          const pSym = (p.canonicalSymbol || p.symbol).replace(/[-.]$/, '').toUpperCase();
          return pSym === canonical && p.source === 'bbook';
        });

        // Group by login
        const map = new Map<number, LoginSummary>();
        for (const p of filtered) {
          const existing = map.get(p.login);
          if (existing) {
            existing.volume += p.volumeLots;
            existing.profit += p.profit + (p.swap || 0);
            existing.positions += 1;
            // If mixed directions, show "MIXED"
            if (existing.direction !== p.direction) existing.direction = 'MIXED';
          } else {
            map.set(p.login, {
              login: p.login,
              direction: p.direction,
              volume: p.volumeLots,
              profit: p.profit + (p.swap || 0),
              positions: 1,
            });
          }
        }

        setLogins(Array.from(map.values()).sort((a, b) => b.volume - a.volume));
      } catch {}
    };

    fetchPositions();
    const interval = setInterval(fetchPositions, 2000);
    return () => clearInterval(interval);
  }, [symbol]);

  const hdr: React.CSSProperties = {
    padding: '5px 8px',
    fontSize: 9,
    fontWeight: 600,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    color: THEME.t3,
    textAlign: 'right',
    borderBottom: `1px solid ${THEME.border}`,
  };

  const cell: React.CSSProperties = {
    padding: '4px 8px',
    fontFamily: "'JetBrains Mono', ui-monospace, 'Cascadia Code', Menlo, monospace",
    fontSize: 11,
    textAlign: 'right',
    borderBottom: `1px solid ${THEME.border}`,
  };

  return (
    <div style={{
      background: THEME.card,
      borderRadius: 6,
      border: `1px solid ${THEME.border}`,
      overflow: 'hidden',
      marginTop: 8,
    }}>
      <div style={{
        padding: '5px 8px',
        fontSize: 9,
        fontWeight: 600,
        textTransform: 'uppercase',
        letterSpacing: 0.5,
        color: THEME.t3,
        borderBottom: `1px solid ${THEME.border}`,
      }}>
        Logins ({logins.length})
      </div>
      {logins.length === 0 ? (
        <div style={{ padding: '10px 8px', color: THEME.t3, fontSize: 10, textAlign: 'center' }}>
          No open positions
        </div>
      ) : (
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr>
              <th style={{ ...hdr, textAlign: 'left' }}>Login</th>
              <th style={hdr}>Dir</th>
              <th style={hdr}>Vol</th>
              <th style={hdr}>P&L</th>
            </tr>
          </thead>
          <tbody>
            {logins.map(l => (
              <tr key={l.login} style={{ cursor: 'pointer' }} onClick={() => setSelectedLogin(l.login)}
                onMouseEnter={e => e.currentTarget.style.background = THEME.rowHover}
                onMouseLeave={e => e.currentTarget.style.background = 'transparent'}
              >
                <td style={{ ...cell, textAlign: 'left', color: THEME.teal, fontWeight: 600, textDecoration: 'underline', textUnderlineOffset: 2 }}>{l.login}</td>
                <td style={{
                  ...cell,
                  color: l.direction === 'BUY' ? THEME.blue : l.direction === 'SELL' ? THEME.red : THEME.t2,
                  fontWeight: 600,
                  fontSize: 10,
                }}>
                  {l.direction}
                  {l.positions > 1 && <span style={{ color: THEME.t3, fontWeight: 400 }}> x{l.positions}</span>}
                </td>
                <td style={{ ...cell, color: THEME.t1 }}>{l.volume.toFixed(2)}</td>
                <td style={{
                  ...cell,
                  color: l.profit >= 0 ? THEME.blue : THEME.red,
                  fontWeight: 600,
                }}>
                  {l.profit >= 0 ? '+' : ''}{l.profit.toFixed(2)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {selectedLogin !== null && (
        <AccountModal login={selectedLogin} onClose={() => setSelectedLogin(null)} />
      )}
    </div>
  );
}
