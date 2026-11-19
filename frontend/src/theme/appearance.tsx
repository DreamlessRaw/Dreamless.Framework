import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

const THEME_KEY = 'dreamless-theme';
const MODE_KEY = 'dreamless-mode';

export const themeOptions = [
  { key: 'blue', color: '#1677ff', labelKey: 'common.themeBlue' },
  { key: 'purple', color: '#722ed1', labelKey: 'common.themePurple' },
  { key: 'cyan', color: '#13c2c2', labelKey: 'common.themeCyan' },
  { key: 'green', color: '#52c41a', labelKey: 'common.themeGreen' },
  { key: 'orange', color: '#fa8c16', labelKey: 'common.themeOrange' },
  { key: 'red', color: '#f5222d', labelKey: 'common.themeRed' },
] as const;

export type ThemeKey = (typeof themeOptions)[number]['key'];
export type ColorMode = 'light' | 'dark';

export function readTheme(): ThemeKey {
  const value = localStorage.getItem(THEME_KEY);
  return themeOptions.some((item) => item.key === value) ? (value as ThemeKey) : 'blue';
}

export function readMode(): ColorMode {
  const value = localStorage.getItem(MODE_KEY);
  if (value === 'light' || value === 'dark') return value;
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

export function themeColor(key: ThemeKey) {
  return themeOptions.find((item) => item.key === key)?.color ?? themeOptions[0].color;
}

type AppearanceContextValue = {
  themeKey: ThemeKey;
  mode: ColorMode;
  setThemeKey: (value: ThemeKey) => void;
  setMode: (value: ColorMode) => void;
};

const AppearanceContext = createContext<AppearanceContextValue | null>(null);

export function AppearanceProvider({ children }: { children: ReactNode }) {
  const [themeKey, setThemeKeyState] = useState<ThemeKey>(readTheme);
  const [mode, setModeState] = useState<ColorMode>(readMode);
  const setThemeKey = (value: ThemeKey) => {
    localStorage.setItem(THEME_KEY, value);
    setThemeKeyState(value);
  };
  const setMode = (value: ColorMode) => {
    localStorage.setItem(MODE_KEY, value);
    setModeState(value);
  };
  useEffect(() => {
    const root = document.documentElement;
    root.dataset.mode = mode;
    root.style.setProperty('--dreamless-primary', themeColor(themeKey));
  }, [themeKey, mode]);
  const value = useMemo(() => ({ themeKey, mode, setThemeKey, setMode }), [themeKey, mode]);
  return <AppearanceContext.Provider value={value}>{children}</AppearanceContext.Provider>;
}

export function useAppearance() {
  const value = useContext(AppearanceContext);
  if (!value) throw new Error('useAppearance must be used within AppearanceProvider');
  return value;
}
