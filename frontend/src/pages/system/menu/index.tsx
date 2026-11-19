import { ModalForm, ProFormTreeSelect } from '@ant-design/pro-components';
import { App } from 'antd';
import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import AdminTable, { type AdminTableRef } from '@/components/AdminTable';
import SchemaFields from '@/components/SchemaFields';

type Menu = { id: number; name: string; children?: Menu[] };

export default function MenuPage() {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const actionRef = useRef<AdminTableRef>(null);
  const [open, setOpen] = useState(false);
  const [copying, setCopying] = useState(false);
  const [current, setCurrent] = useState<Record<string, unknown>>();
  const [tree, setTree] = useState<Menu[]>([]);
  return (
    <>
      <AdminTable
        ref={actionRef}
        pageCode="system:menu"
        headerTitle={t('codegen.title')}
        pagination={false}
        permissions={{ create: 'system:menu:add', edit: 'system:menu:edit', delete: 'system:menu:delete', import: 'system:menu:import', export: 'system:menu:export' }}
        importUrl="/menus/import"
        exportUrl="/menus/export"
        templateUrl="/menus/import-template"
        request={async () => {
          const response = await api.get<Menu[]>('/menus/tree');
          setTree(response.data);
          return { items: response.data, total: response.data.length };
        }}
        onCreate={() => { setCopying(false); setCurrent(undefined); setOpen(true); }}
        onEdit={(row) => { setCopying(false); setCurrent(row); setOpen(true); }}
        onDelete={async (row) => { await api.delete(`/menus/${row.id}`); }}
        actions={[{
          key: 'copy',
          label: t('common.copy'),
          className: 'muted-link',
          permission: 'system:menu:add',
          onClick: (row) => {
            const rest = { ...row };
            delete rest.id;
            delete rest.children;
            setCopying(true);
            setCurrent(rest);
            setOpen(true);
          },
        }]}
      />
      <ModalForm
        title={copying ? t('common.copy') : current?.id ? t('common.edit') : t('common.create')}
        open={open}
        initialValues={current ?? { type: 'Menu', status: 'Enabled', visible: true, parentId: 0, sort: 0 }}
        modalProps={{ destroyOnHidden: true, onCancel: () => setOpen(false) }}
        onFinish={async (values) => {
          if (current?.id && !copying) await api.put(`/menus/${current.id}`, values);
          else await api.post('/menus', values);
          message.success(t('common.saved'));
          setOpen(false);
          actionRef.current?.reload();
          return true;
        }}
      >
        {copying && <p className="menu-copy-hint">{t('menu.copyHint')}</p>}
        <ProFormTreeSelect name="parentId" label="Parent" fieldProps={{ treeData: [{ id: 0, name: '—', children: tree }], fieldNames: { label: 'name', value: 'id', children: 'children' } }} />
        <SchemaFields pageCode="system:menu" />
      </ModalForm>
    </>
  );
}
