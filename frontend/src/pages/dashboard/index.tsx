import { StatisticCard } from '@ant-design/pro-components';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import { useAuth } from '@/auth/AuthContext';

type Summary = { userCount: number; deptCount: number; roleCount: number; tenantCount: number };

export default function DashboardPage() {
  const { t } = useTranslation();
  const { profile } = useAuth();
  const [summary, setSummary] = useState<Summary>();
  useEffect(() => {
    void api.get<Summary>('/dashboard/summary').then((response) => setSummary(response.data));
  }, []);
  return (
    <>
      <TypographyTitle name={profile?.nickName} tenant={profile?.tenantName} text={t('dashboard.welcome')} />
      <StatisticCard.Group>
        <StatisticCard statistic={{ title: t('dashboard.users'), value: summary?.userCount ?? 0 }} />
        <StatisticCard statistic={{ title: t('dashboard.depts'), value: summary?.deptCount ?? 0 }} />
        <StatisticCard statistic={{ title: t('dashboard.roles'), value: summary?.roleCount ?? 0 }} />
        <StatisticCard statistic={{ title: t('dashboard.tenants'), value: summary?.tenantCount ?? 0 }} />
      </StatisticCard.Group>
    </>
  );
}

function TypographyTitle({ name, tenant, text }: { name?: string; tenant?: string; text: string }) {
  return <h2 style={{ marginTop: 0 }}>{name} · {tenant}<div style={{ fontSize: 'var(--dreamless-font-size, 14px)', fontWeight: 400, color: 'var(--dreamless-text-secondary, #667085)' }}>{text}</div></h2>;
}
