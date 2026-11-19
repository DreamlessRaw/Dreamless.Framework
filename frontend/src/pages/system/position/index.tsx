import { ModalForm } from '@ant-design/pro-components';
import { App } from 'antd';
import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import AdminTable, { type AdminTableRef } from '@/components/AdminTable';
import SchemaFields from '@/components/SchemaFields';

export default function PositionPage() {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const actionRef = useRef<AdminTableRef>(null);
  const [open, setOpen] = useState(false);
  const [current, setCurrent] = useState<Record<string, unknown>>();
  return (
    <>
      <AdminTable
        ref={actionRef}
        pageCode="system:position"
        headerTitle={t('position.title')}
        permissions={{ create: 'system:position:add', edit: 'system:position:edit', delete: 'system:position:delete', import: 'system:position:import', export: 'system:position:export' }}
        importUrl="/positions/import"
        exportUrl="/positions/export"
        templateUrl="/positions/import-template"
        request={(params) => api.get('/positions', { params }).then((response) => response.data)}
        onCreate={() => { setCurrent(undefined); setOpen(true); }}
        onEdit={(row) => { setCurrent(row); setOpen(true); }}
        onDelete={async (row) => { await api.delete(`/positions/${row.id}`); }}
      />
      <ModalForm
        title={current ? t('common.edit') : t('common.create')}
        open={open}
        initialValues={current ?? { status: 'Enabled', sort: 0 }}
        modalProps={{ destroyOnHidden: true, onCancel: () => setOpen(false) }}
        onFinish={async (values) => {
          if (current?.id) await api.put(`/positions/${current.id}`, values);
          else await api.post('/positions', values);
          message.success(t('common.saved'));
          setOpen(false);
          actionRef.current?.reload();
          return true;
        }}
      >
        <SchemaFields pageCode="system:position" />
      </ModalForm>
    </>
  );
}
