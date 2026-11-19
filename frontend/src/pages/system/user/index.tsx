import { ModalForm, ProFormSelect, ProFormText, ProFormTreeSelect } from '@ant-design/pro-components';
import { UserOutlined } from '@ant-design/icons';
import { App, Avatar, Button, Form, Upload } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import AdminTable, { type AdminTableRef } from '@/components/AdminTable';
import SchemaFields from '@/components/SchemaFields';
import type { ColumnMeta } from '@/components/columns';

export default function UserPage() {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const actionRef = useRef<AdminTableRef>(null);
  const [open, setOpen] = useState(false);
  const [passwordOpen, setPasswordOpen] = useState(false);
  const [current, setCurrent] = useState<Record<string, unknown>>();
  const [depts, setDepts] = useState<{ id: number; name: string; children?: unknown[] }[]>([]);
  const [roles, setRoles] = useState<{ id: number; name: string }[]>([]);
  const [positions, setPositions] = useState<{ id: number; name: string }[]>([]);
  useEffect(() => {
    void api.get('/depts/tree').then((response) => setDepts(response.data));
    void api.get('/roles', { params: { page: 1, pageSize: 200 } }).then((response) => setRoles(response.data.items));
    void api.get('/positions', { params: { page: 1, pageSize: 200 } }).then((response) => setPositions(response.data.items));
  }, []);
  return (
    <>
      <AdminTable
        ref={actionRef}
        pageCode="system:user"
        headerTitle={t('dashboard.users')}
        permissions={{ create: 'system:user:add', edit: 'system:user:edit', delete: 'system:user:delete', import: 'system:user:import', export: 'system:user:export' }}
        importUrl="/users/import"
        exportUrl="/users/export"
        templateUrl="/users/import-template"
        request={(params) => api.get('/users', { params }).then((response) => response.data)}
        onCreate={() => { setCurrent(undefined); setOpen(true); }}
        onEdit={(row) => { setCurrent(row); setOpen(true); }}
        onDelete={async (row) => { await api.delete(`/users/${row.id}`); }}
      />
      <ModalForm
        title={current ? t('common.edit') : t('common.create')}
        open={open}
        initialValues={current ?? { status: 'Enabled' }}
        modalProps={{ destroyOnHidden: true, onCancel: () => setOpen(false) }}
        submitter={{
          render: (_, dom) => current ? [<Button key="reset" onClick={() => setPasswordOpen(true)}>{t('user.resetPassword')}</Button>, ...dom] : dom,
        }}
        onFinish={async (values) => {
          if (current?.id) await api.put(`/users/${current.id}`, values);
          else await api.post('/users', values);
          message.success(t('common.saved'));
          setOpen(false);
          actionRef.current?.reload();
          return true;
        }}
      >
        {!current && <ProFormText.Password name="password" label={t('user.password')} rules={[{ required: true, min: 6 }]} />}
        <SchemaFields
          pageCode="system:user"
          skip={['createTime', 'deptName', 'positionName']}
          renderers={{
            deptId: (column: ColumnMeta) => (
              <ProFormTreeSelect name="deptId" label={column.label} disabled={!column.canEdit} fieldProps={{ treeData: depts as never, fieldNames: { label: 'name', value: 'id', children: 'children' }, allowClear: true }} />
            ),
            positionId: (column: ColumnMeta) => (
              <ProFormSelect name="positionId" label={column.label} disabled={!column.canEdit} options={positions.map((item) => ({ label: item.name, value: item.id }))} />
            ),
            avatar: (column: ColumnMeta) => <AvatarField column={column} />,
          }}
        />
        <ProFormSelect name="roleIds" label={t('user.roles')} mode="multiple" options={roles.map((role) => ({ label: role.name, value: role.id }))} />
      </ModalForm>
      <ModalForm
        title={t('user.resetPassword')}
        open={passwordOpen}
        modalProps={{ destroyOnHidden: true, onCancel: () => setPasswordOpen(false) }}
        onFinish={async (values) => {
          await api.put(`/users/${current?.id}/password`, values);
          message.success(t('common.saved'));
          setPasswordOpen(false);
          return true;
        }}
      >
        <ProFormText.Password name="password" label={t('common.newPassword')} rules={[{ required: true, min: 6 }]} />
      </ModalForm>
    </>
  );
}

function AvatarField({ column }: { column: ColumnMeta }) {
  const form = Form.useFormInstance();
  const avatar = Form.useWatch('avatar', form) as string | undefined;
  return (
    <>
      <ProFormText name="avatar" hidden />
      <Form.Item label={column.label}>
        <Upload
          disabled={!column.canEdit}
          accept="image/*"
          showUploadList={false}
          customRequest={async ({ file, onSuccess, onError }) => {
            try {
              const body = new FormData();
              body.append('file', file as File);
              const response = await api.post('/users/avatar', body);
              form.setFieldValue('avatar', response.data.url);
              onSuccess?.({});
            } catch (error) {
              onError?.(error as Error);
            }
          }}
        >
          <Avatar src={avatar || undefined} size={64} icon={<UserOutlined />} />
        </Upload>
      </Form.Item>
    </>
  );
}
