import { Spin } from 'antd';
import type { ComponentType } from 'react';
import { Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { flattenMenus, useAuth } from '@/auth/AuthContext';
import BasicLayout from '@/layouts/BasicLayout';
import LoginPage from '@/pages/login';
import NotFoundPage from '@/pages/exception/not-found';

const pages = import.meta.glob('./pages/**/*.tsx', { eager: true }) as Record<string, { default: ComponentType }>;

function RequireAuth() {
  const { profile, loading } = useAuth();
  const location = useLocation();
  if (loading) return <Spin style={{ margin: 80 }} />;
  if (!profile || !localStorage.getItem('access_token')) return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  return <BasicLayout />;
}

function DynamicPage() {
  const { profile } = useAuth();
  const location = useLocation();
  if (location.pathname === '/') return <Navigate to="/dashboard" replace />;
  const match = flattenMenus(profile?.menus).find((item) => item.path === location.pathname);
  const Page = (match?.component && pages[`./pages/${match.component}.tsx`]?.default) || NotFoundPage;
  return <Page key={location.pathname} />;
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth />}>
        <Route path="*" element={<DynamicPage />} />
      </Route>
    </Routes>
  );
}
