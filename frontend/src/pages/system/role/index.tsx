import { ModalForm, ProFormDigit, ProFormSelect, ProFormText, ProFormTextArea } from '@ant-design/pro-components';
import { App, Checkbox, Table, Tabs, Tree } from 'antd';
import type { DataNode } from 'antd/es/tree';
import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import AdminTable, { type AdminTableRef } from '@/components/AdminTable';

type FieldPage = { pageCode: string; pageName: string; fields: { id: number; field: string; label: string }[] };
type Grant = { fieldId: number; canView: boolean; canEdit: boolean; canExport: boolean };
type Dept = { id: number; name: string; children?: Dept[] };
type Menu = { id: number; name: string; permission?: string; children?: Menu[] };

const scopes = ['All', 'DeptAndChild', 'Dept', 'Self', 'Custom'];

export default function RolePage() {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const actionRef = useRef<AdminTableRef>(null);
  const [open, setOpen] = useState(false);
  const [current, setCurrent] = useState<Record<string, unknown>>();
  const [menus, setMenus] = useState<Menu[]>([]);
  const [depts, setDepts] = useState<Dept[]>([]);
  const [pages, setPages] = useState<FieldPage[]>([]);
  const [menuIds, setMenuIds] = useState<number[]>([]);
  const [deptIds, setDeptIds] = useState<number[]>([]);
  const [grants, setGrants] = useState<Record<number, Grant>>({});

  useEffect(() => {
    void api.get<Menu[]>('/menus/tree').then((response) => setMenus(response.data));
    void api.get<Dept[]>('/depts/tree').then((response) => setDepts(response.data));
    void api.get<FieldPage[]>('/meta/fields').then((response) => setPages(response.data));
  }, []);

  const openRole = async (row?: Record<string, unknown>) => {
    const fieldPages = pages.length ? pages : (await api.get<FieldPage[]>('/meta/fields')).data;
    if (!pages.length) setPages(fieldPages);
    const detail = row ? (await api.get(`/roles/${row.id}`)).data : { dataScope: 'Self', status: 'Enabled', menuIds: [], deptIds: [], fields: [] };
    setCurrent(row ? detail : undefined);
    setMenuIds(detail.menuIds || []);
    setDeptIds(detail.deptIds || []);
    const next: Record<number, Grant> = {};
    const existing = new Map<number, Grant>((detail.fields || []).map((item: Grant) => [item.fieldId, item]));
    const configured = (detail.fields || []).length > 0;
    fieldPages.forEach((page) => page.fields.forEach((field) => {
      const saved = existing.get(field.id);
      next[field.id] = saved || { fieldId: field.id, canView: !configured, canEdit: !configured, canExport: !configured };
    }));
    setGrants(next);
    setOpen(true);
  };

  const setGrant = (fieldId: number, patch: Partial<Grant>) => {
    setGrants((currentGrants) => {
      const grant = { ...currentGrants[fieldId], ...patch };
      if (!grant.canView) {
        grant.canEdit = false;
        grant.canExport = false;
      }
      return { ...currentGrants, [fieldId]: grant };
    });
  };

  return (
    <>
      <AdminTable
        ref={actionRef}
        pageCode="system:role"
        headerTitle={t('dashboard.roles')}
        permissions={{ create: 'system:role:add', edit: 'system:role:edit', delete: 'system:role:delete', import: 'system:role:import', export: 'system:role:export' }}
        importUrl="/roles/import"
        exportUrl="/roles/export"
        templateUrl="/roles/import-template"
        request={(params) => api.get('/roles', { params }).then((response) => response.data)}
        onCreate={() => void openRole()}
        onEdit={(row) => void openRole(row)}
        onDelete={async (row) => { await api.delete(`/roles/${row.id}`); }}
      />
      <ModalForm
        title={current ? t('common.edit') : t('common.create')}
        open={open}
        width={920}
        initialValues={current ?? { dataScope: 'Self', status: 'Enabled', sort: 0 }}
        modalProps={{ destroyOnHidden: true, onCancel: () => setOpen(false) }}
        onFinish={async (values) => {
          const payload = {
            ...values,
            menuIds,
            deptIds,
            fields: Object.values(grants),
          };
          if (current?.id) await api.put(`/roles/${current.id}`, payload);
          else await api.post('/roles', payload);
          message.success(t('common.saved'));
          setOpen(false);
          actionRef.current?.reload();
          return true;
        }}
      >
        <Tabs items={[
          {
            key: 'basic',
            label: t('role.basic'),
            children: (
              <>
                <ProFormText name="name" label={t('role.basic')} rules={[{ required: true }]} />
                <ProFormText name="code" label="Code" rules={[{ required: true }]} />
                <ProFormDigit name="sort" label={t('common.sort')} />
                <ProFormSelect name="status" label="Status" options={[{ value: 'Enabled', label: 'Enabled' }, { value: 'Disabled', label: 'Disabled' }]} />
                <ProFormTextArea name="remark" label="Remark" />
              </>
            ),
          },
          {
            key: 'menus',
            label: t('role.menus'),
            children: <Tree checkable checkStrictly checkedKeys={{ checked: menuIds, halfChecked: [] }} treeData={menuTree(menus)} onCheck={(keys) => setMenuIds((Array.isArray(keys) ? keys : keys.checked).map(Number))} />,
          },
          {
            key: 'scope',
            label: t('role.scope'),
            children: (
              <>
                <ProFormSelect name="dataScope" options={scopes.map((scope) => ({ value: scope, label: scope }))} />
                <Tree checkable checkedKeys={deptIds} treeData={deptTree(depts)} onCheck={(keys) => setDeptIds((Array.isArray(keys) ? keys : keys.checked).map(Number))} />
              </>
            ),
          },
          {
            key: 'fields',
            label: t('role.fields'),
            children: pages.map((page) => (
              <Table
                key={page.pageCode}
                size="small"
                pagination={false}
                title={() => page.pageName}
                rowKey="id"
                dataSource={page.fields}
                columns={[
                  { title: page.pageName, dataIndex: 'label' },
                  { title: t('role.view'), render: (_, row) => <Checkbox checked={grants[row.id]?.canView} onChange={(event) => setGrant(row.id, { canView: event.target.checked })} /> },
                  { title: t('role.edit'), render: (_, row) => <Checkbox checked={grants[row.id]?.canEdit} onChange={(event) => setGrant(row.id, { canEdit: event.target.checked, canView: event.target.checked || grants[row.id]?.canView })} /> },
                  { title: t('role.export'), render: (_, row) => <Checkbox checked={grants[row.id]?.canExport} onChange={(event) => setGrant(row.id, { canExport: event.target.checked, canView: event.target.checked || grants[row.id]?.canView })} /> },
                ]}
              />
            )),
          },
        ]} />
      </ModalForm>
    </>
  );
}

function menuTree(nodes: Menu[]): DataNode[] {
  return nodes.map((node) => ({ key: node.id, title: node.permission ? `${node.name} (${node.permission})` : node.name, children: menuTree(node.children || []) }));
}

function deptTree(nodes: Dept[]): DataNode[] {
  return nodes.map((node) => ({ key: node.id, title: node.name, children: deptTree(node.children || []) }));
}
