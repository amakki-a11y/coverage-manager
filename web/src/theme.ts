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
}

export const DARK_THEME: ThemeColors = {
  bg: '#0C0F14',
  bg2: '#141820',
  bg3: '#1A1F2A',
  card: '#1E2430',
  border: 'rgba(255,255,255,0.08)',
  t1: '#F0EDE6',
  t2: '#9BA3B0',
  t3: '#636B78',
  green: '#66BB6A',
  red: '#FF5252',
  amber: '#FFBA42',
  teal: '#3DD9A0',
  blue: '#5B9EFF',
};

export const LIGHT_THEME: ThemeColors = {
  bg: '#F5F6F8',
  bg2: '#FFFFFF',
  bg3: '#EBEDF0',
  card: '#FFFFFF',
  border: 'rgba(0,0,0,0.10)',
  t1: '#1A1A2E',
  t2: '#4A4A5A',
  t3: '#8A8A9A',
  green: '#2E7D32',
  red: '#C62828',
  amber: '#E68A00',
  teal: '#00897B',
  blue: '#1565C0',
};

// Mutable theme object — updated in place so all imports see changes
const savedMode = (typeof localStorage !== 'undefined' && localStorage.getItem('theme')) || 'dark';
const initial = savedMode === 'light' ? LIGHT_THEME : DARK_THEME;

export const THEME: ThemeColors = { ...initial };

export function applyTheme(mode: 'dark' | 'light') {
  const src = mode === 'dark' ? DARK_THEME : LIGHT_THEME;
  Object.assign(THEME, src);
}
