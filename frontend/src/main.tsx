import { App as AntdApp, ConfigProvider, theme } from 'antd';
import zhCN from 'antd/locale/zh_CN';
import enUS from 'antd/locale/en_US';
import { StrictMode, useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import App from './App';
import { AuthProvider } from './auth/AuthContext';
import i18n from './i18n';
import { AppearanceProvider, themeColor, useAppearance } from './theme/appearance';
import { controlHeightOf, FontSizeProvider, useFontSize } from './theme/fontSize';
import './styles.css';

function Root() {
  const [language, setLanguage] = useState(i18n.language);
  useEffect(() => {
    const handler = (lng: string) => setLanguage(lng);
    i18n.on('languageChanged', handler);
    return () => { i18n.off('languageChanged', handler); };
  }, []);
  return (
    <FontSizeProvider>
      <AppearanceProvider>
        <ThemedApp language={language} />
      </AppearanceProvider>
    </FontSizeProvider>
  );
}

function ThemedApp({ language }: { language: string }) {
  const { fontSize } = useFontSize();
  const { themeKey, mode } = useAppearance();
  const color = themeColor(themeKey);
  const controlHeight = controlHeightOf(fontSize);
  const timeCellHeight = Math.round((controlHeight / 32) * 28);
  return (
    <ConfigProvider
      locale={language.startsWith('en') ? enUS : zhCN}
      theme={{
        algorithm: mode === 'dark' ? theme.darkAlgorithm : theme.defaultAlgorithm,
        token: {
          colorPrimary: color,
          colorInfo: color,
          colorLink: color,
          fontSize,
          controlHeight,
        },
        components: {
          DatePicker: {
            timeCellHeight,
            timeColumnHeight: timeCellHeight * 8,
            timeColumnWidth: Math.round(controlHeight * 1.75),
          },
        },
      }}
    >
      <Surface />
      <AntdApp>
        <AuthProvider>
          <BrowserRouter useTransitions={false}>
            <App />
          </BrowserRouter>
        </AuthProvider>
      </AntdApp>
    </ConfigProvider>
  );
}

function Surface() {
  const { token } = theme.useToken();
  useEffect(() => {
    document.documentElement.style.setProperty('--dreamless-text-secondary', token.colorTextSecondary);
    document.documentElement.style.setProperty('--dreamless-danger', token.colorError);
    document.body.style.background = token.colorBgLayout;
    document.body.style.color = token.colorText;
  }, [token.colorBgLayout, token.colorText, token.colorTextSecondary, token.colorError]);
  return null;
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Root />
  </StrictMode>,
);
