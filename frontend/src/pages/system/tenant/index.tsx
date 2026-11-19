import { ModalForm } from '@ant-design/pro-components';
import { App } from 'antd';
import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import AdminTable, { type AdminTableRef } from '@/components/AdminTable';
import SchemaFields from '@/components/SchemaFields';

export default function TenantPage() {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const actionRef = useRef<AdminTableRef>(null);
  const [open, setOpen] = useState(false);
  const [current, setCurrent] = useState<Record<string, unknown>>();
  return (
    <>
      <AdminTable
        ref={actionRef}
        pageCode="system:tenant"
        headerTitle={t('dashboard.tenants')}
        permissions={{ create: 'system:tenant:add', edit: 'system:tenant:edit', delete: 'system:tenant:delete', import: 'system:tenant:import', export: 'system:tenant:export' }}
        importUrl="/tenants/import"
        exportUrl="/tenants/export"
        templateUrl="/tenants/import-template"
        request={(params) => api.get('/tenants', { params }).then((response) => response.data)}
        onCreate={() => { setCurrent(undefined); setOpen(true); }}
        onEdit={(row) => { setCurrent(row); setOpen(true); }}
        onDelete={async (row) => { await api.delete(`/tenants/${row.id}`); }}
      />
      <ModalForm
        title={current ? t('common.edit') : t('common.create')}
        open={open}
        initialValues={current ?? { status: 'Enabled' }}
        modalProps={{ destroyOnHidden: true, onCancel: () => setOpen(false) }}
        onFinish={async (values) => {
          if (current?.id) await api.put(`/tenants/${current.id}`, values);
          else await api.post('/tenants', values);
          message.success(t('common.saved'));
          setOpen(false);
          actionRef.current?.reload();
          return true;
        }}
      >
        <SchemaFields pageCode="system:tenant" />
      </ModalForm>
    </>
  );
}
