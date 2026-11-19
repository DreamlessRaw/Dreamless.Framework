import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api } from '@/api/client';

export type MenuNode = {
  id: number;
  parentId: number;
  name: string;
  path?: string;
  component?: string;
  icon?: string;
  type: 'Directory' | 'Menu' | 'Button' | number;
  permission?: string;
  children?: MenuNode[];
};

export type Profile = {
  id: number;
  userName: string;
  nickName: string;
  tenantName: string;
  deptId?: number;
  deptName?: string;
  avatar?: string;
  isHostAdmin: boolean;
  permissions: string[];
  menus: MenuNode[];
};

type AuthState = {
  profile?: Profile;
  loading: boolean;
  login: (values: { tenantCode: string; userName: string; password: string }) => Promise<void>;
  logout: () => Promise<void>;
  reloadProfile: () => Promise<void>;
  can: (code?: string) => boolean;
};

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [profile, setProfile] = useState<Profile>();
  const [loading, setLoading] = useState(true);

  const reloadProfile = async () => {
    const response = await api.get<Profile>('/auth/profile');
    setProfile(response.data);
  };

  useEffect(() => {
    const boot = async () => {
      if (!localStorage.getItem('access_token')) {
        setLoading(false);
        return;
      }
      try {
        await reloadProfile();
      } catch {
        setProfile(undefined);
      } finally {
        setLoading(false);
      }
    };
    void boot();
    const onLogout = () => setProfile(undefined);
    window.addEventListener('dreamless:logout', onLogout);
    return () => window.removeEventListener('dreamless:logout', onLogout);
  }, []);

  const value = useMemo<AuthState>(() => ({
    profile,
    loading,
    reloadProfile,
    can: (code) => !code || !!profile?.isHostAdmin || !!profile?.permissions.includes(code),
    login: async (values) => {
      const response = await api.post('/auth/login', values);
      localStorage.setItem('access_token', response.data.accessToken);
      localStorage.setItem('refresh_token', response.data.refreshToken);
      setProfile(response.data.profile);
    },
    logout: async () => {
      const refreshToken = localStorage.getItem('refresh_token');
      try {
        await api.post('/auth/logout', { refreshToken });
      } catch {
        /* 本地会话仍然清掉 */
      }
      localStorage.removeItem('access_token');
      localStorage.removeItem('refresh_token');
      setProfile(undefined);
    },
  }), [profile, loading]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) {
    throw new Error('AuthProvider is missing');
  }
  return value;
}

export function flattenMenus(nodes: MenuNode[] = []): MenuNode[] {
  return nodes.flatMap((node) => [node, ...flattenMenus(node.children || [])]);
}
