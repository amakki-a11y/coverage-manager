import { createContext, useContext, useState, useCallback, useEffect } from 'react';
import { DARK_THEME, LIGHT_THEME, applyTheme, type ThemeColors } from './theme';

interface ThemeContextType {
  theme: ThemeColors;
  mode: 'dark' | 'light';
  toggleTheme: () => void;
}

const ThemeContext = createContext<ThemeContextType>({
  theme: DARK_THEME,
  mode: 'dark',
  toggleTheme: () => {},
});

function syncDataTheme(mode: 'dark' | 'light') {
  // The new design's styles.css keys its light palette off
  // `[data-theme="light"]` on <html>, so we mirror the existing THEME mode
  // onto that attribute whenever we switch. Lets CSS-class-based components
  // (sidebar, topbar, etc.) and inline-styled legacy components stay in sync.
  if (typeof document !== 'undefined') {
    document.documentElement.setAttribute('data-theme', mode);
  }
}

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const [mode, setMode] = useState<'dark' | 'light'>(() => {
    // Whitelist: only the literal 'light' flips to light mode. Any other value
    // (null, legacy, corrupt) coerces to dark so the UI can't boot into an
    // unsupported mode.
    return localStorage.getItem('theme') === 'light' ? 'light' : 'dark';
  });

  useEffect(() => { syncDataTheme(mode); }, [mode]);

  const toggleTheme = useCallback(() => {
    setMode((prev) => {
      const next = prev === 'dark' ? 'light' : 'dark';
      localStorage.setItem('theme', next);
      applyTheme(next);
      syncDataTheme(next);
      return next;
    });
  }, []);

  const theme = mode === 'dark' ? DARK_THEME : LIGHT_THEME;

  return (
    <ThemeContext.Provider value={{ theme, mode, toggleTheme }}>
      {children}
    </ThemeContext.Provider>
  );
}

export function useTheme() {
  return useContext(ThemeContext);
}
