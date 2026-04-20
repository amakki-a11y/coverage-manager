import { THEME } from '../theme';

/**
 * Small colored 2-letter chip rendered beside a canonical symbol in the
 * Exposure / Compare tables. Gives the dealer a fast asset-class cue:
 *
 *   metals   (XAU, XAG)              → amber
 *   indices  (US30, NAS, UK100, …)   → blue
 *   energy   (WTI, USOIL, BRENT)     → teal
 *   crypto   (BTC, ETH, XRP, …)      → purple
 *   FX       (anything else)         → t3 grey
 *
 * Matches the badges in the Coverage Mgr design reference
 * (`design-ref/screens/01-compare.png`).
 */
type Tone = 'amber' | 'blue' | 'teal' | 'purple' | 'gray';

const TONE_COLORS: Record<Tone, { bg: string; fg: string }> = {
  amber:  { bg: THEME.badgeAmber, fg: THEME.amber },
  blue:   { bg: THEME.badgeBlue,  fg: THEME.blue  },
  teal:   { bg: THEME.badgeGreen, fg: THEME.teal  },
  purple: { bg: 'rgba(167,139,250,0.15)', fg: '#A78BFA' },
  gray:   { bg: 'rgba(255,255,255,0.06)', fg: THEME.t2 },
};

function classify(sym: string): Tone {
  const s = sym.toUpperCase().replace(/[-.].*$/, '');
  if (s.startsWith('XAU') || s.startsWith('XAG') || s.startsWith('XPD') || s.startsWith('XPT')) return 'amber';
  if (/^(BTC|ETH|LTC|XRP|SOL|ADA|DOGE|DOT)/.test(s)) return 'purple';
  if (/^(WTI|USOIL|BRENT|UKOIL|NG)/.test(s) || s.includes('OIL')) return 'teal';
  if (/^(US30|US500|US100|NAS|SPX|NDX|DJI|UK100|UK250|DE30|DE40|DAX|FTSE|CAC|NIK|HSI|JP225|IBEX|AUS200|EU50|GCM)/.test(s)) return 'blue';
  if (/^[A-Z]{6}$/.test(s)) return 'gray'; // FX pair (EURUSD etc.)
  return 'gray';
}

export function SymbolBadge({ symbol }: { symbol: string }) {
  const tone = classify(symbol);
  const { bg, fg } = TONE_COLORS[tone];
  const letters = symbol.replace(/[-.].*$/, '').slice(0, 2).toUpperCase();

  return (
    <span
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        minWidth: 22,
        height: 22,
        padding: '0 5px',
        borderRadius: 4,
        background: bg,
        color: fg,
        fontSize: 10,
        fontWeight: 700,
        letterSpacing: 0.5,
        fontFamily: 'inherit',
        flexShrink: 0,
      }}
    >
      {letters}
    </span>
  );
}
