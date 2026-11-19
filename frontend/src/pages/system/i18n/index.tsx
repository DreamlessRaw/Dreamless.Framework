import { ModalForm } from '@ant-design/pro-components';
import { App } from 'antd';
import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import AdminTable, { type AdminTableRef } from '@/components/AdminTable';
import SchemaFields from '@/components/SchemaFields';

export default function I18nPage() {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const actionRef = useRef<AdminTableRef>(null);
  const [open, setOpen] = useState(false);
  const [current, setCurrent] = useState<Record<string, unknown>>();
  return (
    <>
      <AdminTable
        ref={actionRef}
        pageCode="system:i18n"
        headerTitle={t('common.language')}
        permissions={{ create: 'system:i18n:add', edit: 'system:i18n:edit', delete: 'system:i18n:delete', import: 'system:i18n:import', export: 'system:i18n:export' }}
        importUrl="/i18n/import"
        exportUrl="/i18n/export"
        templateUrl="/i18n/import-template"
        request={(params) => api.get('/i18n', { params }).then((response) => response.data)}
        onCreate={() => { setCurrent(undefined); setOpen(true); }}
        onEdit={(row) => { setCurrent(row); setOpen(true); }}
        onDelete={async (row) => { await api.delete(`/i18n/${row.id}`); }}
      />
      <ModalForm
        title={current ? t('common.edit') : t('common.create')}
        open={open}
        initialValues={current ?? { culture: 'zh-CN' }}
        modalProps={{ destroyOnHidden: true, onCancel: () => setOpen(false) }}
        onFinish={async (values) => {
          if (current?.id) await api.put(`/i18n/${current.id}`, values);
          else await api.post('/i18n', values);
          message.success(t('common.saved'));
          setOpen(false);
          actionRef.current?.reload();
          return true;
        }}
      >
        <SchemaFields pageCode="system:i18n" />
      </ModalForm>
    </>
  );
}
