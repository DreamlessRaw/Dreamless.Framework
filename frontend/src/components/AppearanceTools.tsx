import { BgColorsOutlined, FontSizeOutlined, MoonOutlined, SunOutlined } from '@ant-design/icons';
import { Dropdown } from 'antd';
import { useTranslation } from 'react-i18next';
import i18n from '@/i18n';
import { themeOptions, useAppearance } from '@/theme/appearance';
import { fontSizeOptions, useFontSize } from '@/theme/fontSize';

export function ThemeTool() {
  const { t } = useTranslation();
  const { themeKey, setThemeKey } = useAppearance();
  return (
    <Dropdown
      trigger={['click']}
      placement="bottomRight"
      menu={{
        selectedKeys: [themeKey],
        items: themeOptions.map((item) => ({
          key: item.key,
          label: (
            <span className="theme-option">
              <i style={{ background: item.color }} />
              {t(item.labelKey)}
            </span>
          ),
          onClick: () => setThemeKey(item.key),
        })),
      }}
    >
      <button type="button" className="header-tool" aria-label={t('common.theme')}>
        <BgColorsOutlined />
      </button>
    </Dropdown>
  );
}

export function ModeTool() {
  const { t } = useTranslation();
  const { mode, setMode } = useAppearance();
  return (
    <Dropdown
      trigger={['click']}
      placement="bottomRight"
      menu={{
        selectedKeys: [mode],
        items: [
          { key: 'light', icon: <SunOutlined />, label: t('common.modeLight'), onClick: () => setMode('light') },
          { key: 'dark', icon: <MoonOutlined />, label: t('common.modeDark'), onClick: () => setMode('dark') },
        ],
      }}
    >
      <button type="button" className="header-tool" aria-label={t('common.colorMode')}>
        {mode === 'dark' ? <MoonOutlined /> : <SunOutlined />}
      </button>
    </Dropdown>
  );
}

export function FontSizeTool() {
  const { t } = useTranslation();
  const { fontSize, setFontSize } = useFontSize();
  return (
    <Dropdown
      trigger={['click']}
      placement="bottomRight"
      menu={{
        selectedKeys: [String(fontSize)],
        items: fontSizeOptions.map((item) => ({
          key: String(item.value),
          label: t(item.labelKey),
          onClick: () => setFontSize(item.value),
        })),
      }}
    >
      <button type="button" className="header-tool" aria-label={t('common.fontSize')}>
        <FontSizeOutlined />
      </button>
    </Dropdown>
  );
}

export function LanguageTool({ onChange }: { onChange: (language: string) => void | Promise<void> }) {
  const { t } = useTranslation();
  const current = i18n.language.startsWith('en') ? 'en-US' : 'zh-CN';
  return (
    <Dropdown
      trigger={['click']}
      placement="bottomRight"
      menu={{
        selectedKeys: [current],
        items: [
          { key: 'zh-CN', label: '中文', onClick: () => void onChange('zh-CN') },
          { key: 'en-US', label: 'English', onClick: () => void onChange('en-US') },
        ],
      }}
    >
      <button type="button" className="header-tool" aria-label={t('common.language')}>
        {current === 'en-US' ? 'EN' : '中'}
      </button>
    </Dropdown>
  );
}
