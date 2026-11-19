import {
  ApartmentOutlined,
  BellOutlined,
  AppstoreOutlined,
  BankOutlined,
  CodeOutlined,
  DashboardOutlined,
  IdcardOutlined,
  MenuOutlined,
  SafetyOutlined,
  SettingOutlined,
  TableOutlined,
  TeamOutlined,
  TranslationOutlined,
  UserOutlined,
} from '@ant-design/icons';
import type { ReactNode } from 'react';

const icons: Record<string, ReactNode> = {
  DashboardOutlined: <DashboardOutlined />,
  IdcardOutlined: <IdcardOutlined />,
  SettingOutlined: <SettingOutlined />,
  BankOutlined: <BankOutlined />,
  ApartmentOutlined: <ApartmentOutlined />,
  UserOutlined: <UserOutlined />,
  TeamOutlined: <TeamOutlined />,
  SafetyOutlined: <SafetyOutlined />,
  MenuOutlined: <MenuOutlined />,
  TranslationOutlined: <TranslationOutlined />,
  CodeOutlined: <CodeOutlined />,
  TableOutlined: <TableOutlined />,
  AppstoreOutlined: <AppstoreOutlined />,
  BellOutlined: <BellOutlined />,
};

export function iconOf(name?: string) {
  return (name && icons[name]) || <TableOutlined />;
}
