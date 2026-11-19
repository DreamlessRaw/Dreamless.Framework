import { ModalForm, ProFormTreeSelect } from '@ant-design/pro-components';
import { App } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import AdminTable, { type AdminTableRef } from '@/components/AdminTable';
import SchemaFields from '@/components/SchemaFields';
import type { ColumnMeta } from '@/components/columns';

type Dept = { id: number; name: string; children?: Dept[] };

export default function DeptPage() {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const actionRef = useRef<AdminTableRef>(null);
  const [open, setOpen] = useState(false);
  const [current, setCurrent] = useState<Record<string, unknown>>();
  const [tree, setTree] = useState<Dept[]>([]);
  const loadTree = () => api.get<Dept[]>('/depts/tree').then((response) => setTree(response.data));
  useEffect(() => { void loadTree(); }, []);
  return (
    <>
      <AdminTable
        ref={actionRef}
        pageCode="system:dept"
        headerTitle={t('dashboard.depts')}
        pagination={false}
        permissions={{ create: 'system:dept:add', edit: 'system:dept:edit', delete: 'system:dept:delete', import: 'system:dept:import', export: 'system:dept:export' }}
        importUrl="/depts/import"
        exportUrl="/depts/export"
        templateUrl="/depts/import-template"
        request={async () => {
          const response = await api.get<Dept[]>('/depts/tree');
          setTree(response.data);
          return { items: response.data, total: response.data.length };
        }}
        onCreate={() => { setCurrent(undefined); setOpen(true); }}
        onEdit={(row) => { setCurrent(row); setOpen(true); }}
        onDelete={async (row) => { await api.delete(`/depts/${row.id}`); }}
      />
      <ModalForm
        title={current ? t('common.edit') : t('common.create')}
        open={open}
        initialValues={current ?? { status: 'Enabled', parentId: 0, sort: 0 }}
        modalProps={{ destroyOnHidden: true, onCancel: () => setOpen(false) }}
        onFinish={async (values) => {
          if (current?.id) await api.put(`/depts/${current.id}`, values);
          else await api.post('/depts', values);
          message.success(t('common.saved'));
          setOpen(false);
          actionRef.current?.reload();
          await loadTree();
          return true;
        }}
      >
        <SchemaFields
          pageCode="system:dept"
          skip={['createTime']}
          renderers={{ parentId: (column: ColumnMeta) => (
            <ProFormTreeSelect
              name="parentId"
              label={column.label}
              fieldProps={{ treeData: [{ id: 0, name: '—', children: tree }], fieldNames: { label: 'name', value: 'id', children: 'children' }, allowClear: true }}
            />
          ) }}
        />
      </ModalForm>
    </>
  );
}
