import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

const STORAGE_KEY = 'dreamless-font-size';

export const fontSizeOptions = [
  { value: 12, labelKey: 'common.fontSmall' },
  { value: 14, labelKey: 'common.fontDefault' },
  { value: 16, labelKey: 'common.fontLarge' },
  { value: 20, labelKey: 'common.fontXLarge' },
] as const;

export type FontSize = (typeof fontSizeOptions)[number]['value'];

export function readFontSize(): FontSize {
  const value = Number(localStorage.getItem(STORAGE_KEY));
  return fontSizeOptions.some((item) => item.value === value) ? (value as FontSize) : 14;
}

/** Keep input and button height in step with the seed font size. 14px maps to the Ant Design default of 32px. */
export function controlHeightOf(fontSize: number) {
  return Math.round((fontSize / 14) * 16) * 2;
}

type FontSizeContextValue = { fontSize: FontSize; setFontSize: (value: FontSize) => void };

const FontSizeContext = createContext<FontSizeContextValue | null>(null);

export function FontSizeProvider({ children }: { children: ReactNode }) {
  const [fontSize, setFontSizeState] = useState<FontSize>(readFontSize);
  const setFontSize = (value: FontSize) => {
    localStorage.setItem(STORAGE_KEY, String(value));
    setFontSizeState(value);
  };
  useEffect(() => {
    const root = document.documentElement;
    root.style.setProperty('--dreamless-font-size', `${fontSize}px`);
    root.style.setProperty('--dreamless-control-height', `${controlHeightOf(fontSize)}px`);
  }, [fontSize]);
  const value = useMemo(() => ({ fontSize, setFontSize }), [fontSize]);
  return <FontSizeContext.Provider value={value}>{children}</FontSizeContext.Provider>;
}

export function useFontSize() {
  const value = useContext(FontSizeContext);
  if (!value) throw new Error('useFontSize must be used within FontSizeProvider');
  return value;
}
