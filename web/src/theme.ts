// =========================================================================
// Color semantics — PLEASE READ BEFORE ADDING A NEW COLOR USAGE
// =========================================================================
// This is a dealing-desk UI. Colors carry meaning; swapping them for aesthetic
// reasons breaks the user's ability to scan fast.
//
//   green   \u2192 positive money / healthy state ("+$123", hedge ratio OK)
//   red     \u2192 negative money / danger        ("\u2212$45", unhedged, delete)
//   amber   \u2192 warning / needs attention      (stale data, degraded health)
//   blue    \u2192 informational / identifier     (CLIENT badge, tab indicator)
//   teal    \u2192 accent / secondary group       (COV OUT badge, conversion hint)
//   t1 / t2 / t3  \u2192 primary / secondary / tertiary foreground. NEVER use
//                        one of the semantic colors above in place of t1/t2/t3.
//
// Backgrounds:
//   bg   \u2192 app body          bg2 \u2192 toolbars         bg3 \u2192 inputs + alt rows
//
// Badge variants (15\u201318% alpha of the base) are for pill/button *backgrounds*,
// never foreground text.
// =========================================================================
export interface ThemeColors {
  bg: string;
  bg2: string;
  bg3: string;
  card: string;
  border: string;
  t1: string;
  t2: string;
  t3: string;
  green: string;
  red: string;
  amber: string;
  teal: string;
  blue: string;

  // --- Surface tokens for tables + expanded rows ---
  // Dark mode previously hardcoded `rgba(255,255,255,0.015-0.04)` for alt/selected
  // rows; those are invisible on a white background. These per-theme tokens make
  // the tints actually render in both modes.
  rowAlt: string;      // subtle alternating band (e.g. closed row under open row)
  rowSelected: string; // currently-selected / highlighted row
  rowHover: string;    // mouse-hover tint

  // --- Elevation ---
  shadow: string;         // toast / floating card shadow
  shadowModal: string;    // full modal dialog lift
  shadowOverlay: string;  // the scrim behind modals (dark backdrop on either theme)

  // --- Badge tints (15–18% alpha of a base color) ---
  // Used for source-type badges, info pills, destructive-action buttons etc.
  badgeBlue: string;
  badgeRed: string;
  badgeGreen: string;
  badgeAmber: string;
}

// Dark-mode hex values synced to `styles/styles.css` :root palette so the
// JS-side inline styles used by legacy components render in the same
// palette as the new CSS-class-based shell.
export const DARK_THEME: ThemeColors = {
  bg: '#0A0D12',
  bg2: '#10141B',
  bg3: '#171C25',
  card: '#1A202B',
  border: 'rgba(255,255,255,0.07)',
  t1: '#F1EEE6',
  t2: '#A6ADBA',
  t3: '#6A7280',
  green: '#4ADE80',
  red: '#F87171',
  amber: '#FBBF24',
  teal: '#2DD4BF',
  blue: '#60A5FA',

  rowAlt: 'rgba(255,255,255,0.02)',
  rowSelected: 'rgba(96,165,250,0.08)',
  rowHover: 'rgba(255,255,255,0.04)',

  shadow: '0 4px 20px rgba(0,0,0,0.4)',
  shadowModal: '0 20px 60px rgba(0,0,0,0.5)',
  shadowOverlay: 'rgba(0,0,0,0.6)',

  badgeBlue:  'rgba(91,158,255,0.15)',
  badgeRed:   'rgba(255,82,82,0.15)',
  badgeGreen: 'rgba(102,187,106,0.15)',
  badgeAmber: 'rgba(255,186,66,0.15)',
};

export const LIGHT_THEME: ThemeColors = {
  bg: '#F5F6F8',
  bg2: '#FFFFFF',
  bg3: '#EBEDF0',
  card: '#FFFFFF',
  border: 'rgba(0,0,0,0.10)',
  t1: '#1A1A2E',
  t2: '#4A4A5A',
  t3: '#6C6C80',           // was #8A8A9A (3.40:1 on white) — darkened to ~4.8:1
  green: '#2E7D32',
  red: '#C62828',
  amber: '#A65600',        // was #E68A00 (2.63:1 on white, WCAG FAIL) — darkened to ~4.9:1
  teal: '#00897B',
  blue: '#1565C0',

  // Light mode inverts the dark tints — darken the surface instead of lightening.
  rowAlt: 'rgba(0,0,0,0.025)',
  rowSelected: 'rgba(21,101,192,0.08)',  // 8% of blue, theme accent
  rowHover: 'rgba(0,0,0,0.04)',

  // Softer shadows in light mode — large black blur looks harsh on white.
  shadow: '0 2px 8px rgba(0,0,0,0.08)',
  shadowModal: '0 8px 32px rgba(0,0,0,0.12)',
  shadowOverlay: 'rgba(0,0,0,0.35)',

  badgeBlue:  'rgba(21,101,192,0.12)',
  badgeRed:   'rgba(198,40,40,0.12)',
  badgeGreen: 'rgba(46,125,50,0.12)',
  badgeAmber: 'rgba(166,86,0,0.14)',
};

// Mutable theme object — updated in place so all imports see changes.
// Dark is the enforced default: only the literal string 'light' flips the initial
// mode. Any other value (null, corrupt, legacy) falls through to dark so a bad
// localStorage entry can never strand a dealer in an unsupported mode.
const savedMode = (typeof localStorage !== 'undefined' && localStorage.getItem('theme')) || 'dark';
const initial = savedMode === 'light' ? LIGHT_THEME : DARK_THEME;

export const THEME: ThemeColors = { ...initial };

export function applyTheme(mode: 'dark' | 'light') {
  const src = mode === 'dark' ? DARK_THEME : LIGHT_THEME;
  Object.assign(THEME, src);
}
