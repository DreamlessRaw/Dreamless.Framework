import { ModalForm, ProFormText, ProLayout } from '@ant-design/pro-components';
import { Dropdown } from 'antd';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { api } from '@/api/client';
import { useAuth, type MenuNode } from '@/auth/AuthContext';
import { FontSizeTool, LanguageTool, ModeTool, ThemeTool } from '@/components/AppearanceTools';
import { MessageBell, MessageProvider } from '@/components/MessageCenter';
import { iconOf } from '@/components/icons';
import i18n from '@/i18n';
import { themeColor, useAppearance } from '@/theme/appearance';

export default function BasicLayout() {
  return (
    <MessageProvider>
      <LayoutBody />
    </MessageProvider>
  );
}

function LayoutBody() {
  const { t } = useTranslation();
  const { profile, logout, reloadProfile } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const { mode, themeKey } = useAppearance();
  const color = themeColor(themeKey);
  const selectedBg = mode === 'dark'
    ? `color-mix(in srgb, ${color} 32%, transparent)`
    : `color-mix(in srgb, ${color} 16%, white)`;
  const selectedText = mode === 'dark' ? `color-mix(in srgb, ${color} 62%, white)` : color;
  const hoverBg = mode === 'dark'
    ? `color-mix(in srgb, ${color} 18%, transparent)`
    : `color-mix(in srgb, ${color} 8%, white)`;
  const [passwordOpen, setPasswordOpen] = useState(false);

  const changeLanguage = async (language: string) => {
    localStorage.setItem('dreamless-lang', language);
    await i18n.changeLanguage(language);
    await reloadProfile();
  };

  return (
    <>
      <ProLayout
        title={t('app.name')}
        logo={false}
        layout="mix"
        navTheme={mode === 'dark' ? 'realDark' : 'light'}
        token={{
          sider: {
            colorBgMenuItemSelected: selectedBg,
            colorBgMenuItemHover: hoverBg,
            colorBgMenuItemActive: selectedBg,
            colorTextMenuSelected: selectedText,
            colorTextMenuItemHover: selectedText,
            colorTextMenuActive: selectedText,
            colorTextSubMenuSelected: selectedText,
          },
          header: {
            colorBgMenuItemSelected: selectedBg,
            colorBgMenuItemHover: hoverBg,
            colorTextMenuSelected: selectedText,
            colorTextMenuActive: selectedText,
          },
        }}
        fixSiderbar
        location={{ pathname: location.pathname }}
        route={{ path: '/', routes: toRoutes(profile?.menus || []) }}
        menuItemRender={(item, dom) => item.path && !(item.children as unknown[] | undefined)?.length && !(item as { routes?: unknown[] }).routes?.length
          ? <Link to={item.path}>{dom}</Link>
          : dom}
        avatarProps={{
          src: profile?.avatar || undefined,
          title: profile?.nickName,
          size: 'small',
          render: (_, dom) => (
            <Dropdown
              menu={{
                items: [
                  { key: 'password', label: t('common.password'), onClick: () => setPasswordOpen(true) },
                  { key: 'logout', label: t('common.logout'), onClick: async () => { await logout(); navigate('/login'); } },
                ],
              }}
            >
              {dom}
            </Dropdown>
          ),
        }}
        actionsRender={() => [
          <MessageBell key="message" />,
          <ThemeTool key="theme" />,
          <ModeTool key="mode" />,
          <FontSizeTool key="font" />,
          <LanguageTool key="lang" onChange={changeLanguage} />,
        ]}
        contentStyle={{ padding: 16 }}
      >
        <Outlet />
      </ProLayout>
      <ModalForm
        title={t('common.password')}
        open={passwordOpen}
        modalProps={{ destroyOnHidden: true, onCancel: () => setPasswordOpen(false) }}
        onFinish={async (values) => {
          await api.put('/auth/password', values);
          setPasswordOpen(false);
          return true;
        }}
      >
        <ProFormText.Password name="oldPassword" label={t('common.oldPassword')} rules={[{ required: true }]} />
        <ProFormText.Password name="newPassword" label={t('common.newPassword')} rules={[{ required: true, min: 6 }]} />
      </ModalForm>
    </>
  );
}

function toRoutes(nodes: MenuNode[]): Array<Record<string, unknown>> {
  return nodes.filter((node) => node.type !== 'Button' && node.type !== 3).map((node) => ({
    path: node.path,
    name: node.name,
    icon: iconOf(node.icon),
    routes: toRoutes(node.children || []),
  }));
}
