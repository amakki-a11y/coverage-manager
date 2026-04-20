/* global React, I */
const { useState: useS } = React;
const fmt = window.fmt, fp = window.fp, pc = window.pc;
const HedgeBar = window.HedgeBar;

// ========== Hedge Modal ==========
function HedgeModal({ row, onClose, onConfirm }) {
  const dir = row.toCover < 0 ? 'SELL' : 'BUY';
  const amount = Math.abs(row.toCover);
  const [vol, setVol] = useS(amount.toFixed(2));
  const [lpAcct, setLpAcct] = useS('96900 · fXGROW LP');
  const [partial, setPartial] = useS(100);
  const [confirm, setConfirm] = useS(true);
  const volN = parseFloat(vol) || 0;

  const price = window.SYMBOLS.find(s => s.sym === row.s.sym).bid;

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal" onClick={e => e.stopPropagation()} style={{ width: 520 }}>
        <div className="modal-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
            <div style={{ width: 36, height: 36, borderRadius: 8, background: dir === 'BUY' ? 'var(--green-dim)' : 'var(--red-dim)', color: dir === 'BUY' ? 'var(--green)' : 'var(--red)', display: 'grid', placeItems: 'center', fontWeight: 700, fontSize: 11 }}>
              {dir}
            </div>
            <div>
              <div className="modal-title">
                Hedge {row.s.sym}
                <span style={{ color: 'var(--t3)', fontWeight: 500, marginLeft: 8 }}>· {row.s.asset}</span>
              </div>
              <div className="modal-sub">
                {dir === 'BUY' ? 'Buy on LP to cover short client exposure' : 'Sell on LP to cover long client exposure'}
              </div>
            </div>
          </div>
        </div>
        <div className="modal-body">
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10, marginBottom: 18 }}>
            <div className="card" style={{ padding: 12, borderRadius: 8 }}>
              <div className="t3" style={{ fontSize: 10, fontWeight: 600, letterSpacing: '0.08em' }}>CURRENT EXPOSURE</div>
              <div className="mono" style={{ fontSize: 18, fontWeight: 700, marginTop: 4 }}>{fmt(row.bbNet - row.covNet)}</div>
              <div className="t3 mono" style={{ fontSize: 11 }}>Client {fmt(row.bbNet)} · Cov {fmt(row.covNet)}</div>
            </div>
            <div className="card" style={{ padding: 12, borderRadius: 8 }}>
              <div className="t3" style={{ fontSize: 10, fontWeight: 600, letterSpacing: '0.08em' }}>HEDGE RATIO</div>
              <div style={{ marginTop: 6 }}><HedgeBar pct={row.hedge} /></div>
              <div className="t3" style={{ fontSize: 11, marginTop: 2 }}>after hedge: <span className="pos">100%</span></div>
            </div>
          </div>

          <label style={{ display: 'flex', flexDirection: 'column', gap: 6, marginBottom: 14 }}>
            <span className="t3" style={{ fontSize: 10.5, fontWeight: 600, letterSpacing: '0.08em' }}>VOLUME (LOTS)</span>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
              <input className="date-input" style={{ flex: 1, fontSize: 16, padding: '8px 12px', fontWeight: 600 }} value={vol} onChange={e => setVol(e.target.value)} />
              <div className="segmented" style={{ padding: 3 }}>
                {[25, 50, 75, 100].map(p => (
                  <button key={p} className={partial === p ? 'active' : ''} onClick={() => { setPartial(p); setVol(((amount * p) / 100).toFixed(2)); }}>{p}%</button>
                ))}
              </div>
            </div>
            <div className="t3" style={{ fontSize: 11 }}>
              Market: <span className="mono t1">{price}</span> · Est. notional <span className="mono t1">${fmt(volN * price * (row.s.cls === 'fx' ? 100000 : 1))}</span>
            </div>
          </label>

          <label style={{ display: 'flex', flexDirection: 'column', gap: 6, marginBottom: 14 }}>
            <span className="t3" style={{ fontSize: 10.5, fontWeight: 600, letterSpacing: '0.08em' }}>LP ACCOUNT</span>
            <select className="select" style={{ padding: '8px 26px 8px 12px', fontSize: 13 }} value={lpAcct} onChange={e => setLpAcct(e.target.value)}>
              <option>96900 · fXGROW LP</option>
              <option>96901 · Centroid LP</option>
              <option>96902 · Equiti Prime</option>
            </select>
          </label>

          <div className="card" style={{ padding: 12, borderRadius: 8, marginBottom: 14 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 6 }}>
              <span className="t3" style={{ fontSize: 10.5, fontWeight: 600, letterSpacing: '0.08em' }}>PRE-TRADE CHECK</span>
              <span className="chip live"><span className="dot"/>All clear</span>
            </div>
            <div style={{ display: 'flex', gap: 14, fontSize: 11.5 }}>
              <div><span className="t3">Margin</span> <span className="mono t1">$48,210</span></div>
              <div><span className="t3">Free</span> <span className="mono pos">$412,790</span></div>
              <div><span className="t3">Slippage</span> <span className="mono t1">0.2 pips</span></div>
            </div>
          </div>

          <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, color: 'var(--t2)', cursor: 'pointer' }}>
            <span className={`switch ${confirm ? 'on' : ''}`} onClick={() => setConfirm(!confirm)} />
            Require confirmation before firing (recommended)
          </label>
        </div>
        <div className="modal-footer">
          <button className="icon-btn" onClick={onClose}>Cancel</button>
          <button className={`icon-btn ${dir === 'BUY' ? 'primary' : 'danger'}`} onClick={() => onConfirm({ row, dir, vol: volN, lpAcct })}>
            <I.bolt /> {dir} {volN.toFixed(2)} lots
          </button>
        </div>
      </div>
    </div>
  );
}
window.HedgeModal = HedgeModal;

// ========== Hedge Confirm Step (brief) ==========
function HedgeConfirmToast({ text, onDone }) {
  React.useEffect(() => {
    const t = setTimeout(onDone, 3200);
    return () => clearTimeout(t);
  }, []);
  return (
    <div className="toast info" style={{ maxWidth: 360 }}>
      <div className="toast-icon" style={{ background: 'var(--green-dim)', color: 'var(--green)' }}>✓</div>
      <div className="toast-body">
        <div className="toast-title">Hedge fired</div>
        <div className="toast-desc">{text}</div>
        <div className="toast-meta">Bridge ID b-8402 · t+180ms</div>
      </div>
    </div>
  );
}
window.HedgeConfirmToast = HedgeConfirmToast;
