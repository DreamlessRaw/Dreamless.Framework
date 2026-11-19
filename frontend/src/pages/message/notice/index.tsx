import { ModalForm, ProFormSelect, ProFormText, ProFormTextArea } from '@ant-design/pro-components';
import { App, Button, Descriptions, Drawer, Popconfirm, Radio, Space, Table, Tabs, Tag } from 'antd';
import dayjs from 'dayjs';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import { useAuth } from '@/auth/AuthContext';
import { scopeTag, useMessages, type InboxMessage } from '@/components/MessageCenter';

type SentMessage = {
  id: number;
  title: string;
  content: string;
  scope: 'Tenant' | 'Dept' | 'Users';
  recipientCount: number;
  createTime: string;
};

type UserOption = { id: number; userName: string; nickName: string; deptName?: string };

export default function NoticePage() {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const { can, profile } = useAuth();
  const { revision, reload } = useMessages();
  const [tab, setTab] = useState('inbox');
  const [open, setOpen] = useState(false);
  const [scope, setScope] = useState('Tenant');
  const [inbox, setInbox] = useState<InboxMessage[]>([]);
  const [inboxTotal, setInboxTotal] = useState(0);
  const [sent, setSent] = useState<SentMessage[]>([]);
  const [sentTotal, setSentTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [users, setUsers] = useState<UserOption[]>([]);
  const [viewing, setViewing] = useState<InboxMessage>();
  const canSend = can('message:notice:send');
  const canDelete = can('message:notice:delete');

  const loadInbox = async (next = page) => {
    const response = await api.get<{ total: number; items: InboxMessage[] }>('/messages/inbox', { params: { page: next, pageSize: 10 } });
    setInbox(response.data.items || []);
    setInboxTotal(response.data.total || 0);
  };

  const loadSent = async (next = page) => {
    const response = await api.get<{ total: number; items: SentMessage[] }>('/messages/sent', { params: { page: next, pageSize: 10 } });
    setSent(response.data.items || []);
    setSentTotal(response.data.total || 0);
  };

  useEffect(() => {
    if (open && scope === 'Users') {
      void api.get<UserOption[]>('/messages/recipients').then((response) => setUsers(response.data));
    }
  }, [open, scope]);

  useEffect(() => {
    void (tab === 'sent' ? loadSent(page) : loadInbox(page));
  }, [tab, page, revision]);

  useEffect(() => {
    if (!viewing) return;
    const next = inbox.find((item) => item.id === viewing.id);
    if (next) setViewing(next);
  }, [inbox]);

  const mark = async (row: InboxMessage) => {
    await api.put(`/messages/${row.messageId}/read`);
    await reload();
  };

  return (
    <>
      <Tabs
        activeKey={tab}
        onChange={(key) => { setTab(key); setPage(1); }}
        tabBarExtraContent={(
          <Space>
            {tab === 'inbox' && (
              <Button onClick={async () => { await api.put('/messages/read-all'); await reload(); }}>{t('message.readAll')}</Button>
            )}
            {canSend && <Button type="primary" onClick={() => { setScope('Tenant'); setOpen(true); }}>{t('message.send')}</Button>}
          </Space>
        )}
        items={[
          {
            key: 'inbox',
            label: t('message.inbox'),
            children: (
              <Table
                rowKey="id"
                dataSource={inbox}
                scroll={{ x: 1280 }}
                pagination={{ current: page, pageSize: 10, total: inboxTotal, onChange: setPage }}
                columns={[
                  { title: t('message.subject'), dataIndex: 'title', render: (value: string, row) => <a onClick={() => setViewing(row)} style={{ fontWeight: row.isRead ? 400 : 600 }}>{value}</a> },
                  { title: t('message.content'), dataIndex: 'content', ellipsis: true },
                  { title: t('message.scope'), dataIndex: 'scope', width: 120, render: (value: string) => scopeTag(value, t) },
                  { title: t('message.sender'), dataIndex: 'senderName', width: 120 },
                  { title: t('message.state'), dataIndex: 'isRead', width: 90, render: (value: boolean) => value ? <Tag>{t('message.readState')}</Tag> : <Tag color="processing">{t('message.unread')}</Tag> },
                  { title: t('message.sendTime'), dataIndex: 'createTime', width: 180, render: (value: string) => formatTime(value) },
                  { title: t('message.readTime'), dataIndex: 'readTime', width: 180, render: (value?: string) => formatTime(value) },
                  {
                    title: t('common.actions'),
                    width: 180,
                    render: (_, row) => (
                      <Space>
                        <a onClick={() => setViewing(row)}>{t('message.view')}</a>
                        {!row.isRead && <a onClick={() => { void mark(row); }}>{t('message.read')}</a>}
                        {canDelete && (
                          <Popconfirm title={t('common.confirmDelete')} onConfirm={async () => { await api.delete(`/messages/inbox/${row.id}`); message.success(t('common.deleted')); await reload(); }}>
                            <a className="danger-link">{t('common.delete')}</a>
                          </Popconfirm>
                        )}
                      </Space>
                    ),
                  },
                ]}
              />
            ),
          },
          ...(canSend ? [{
            key: 'sent',
            label: t('message.sent'),
            children: (
              <Table
                rowKey="id"
                dataSource={sent}
                scroll={{ x: 900 }}
                pagination={{ current: page, pageSize: 10, total: sentTotal, onChange: setPage }}
                columns={[
                  { title: t('message.subject'), dataIndex: 'title' },
                  { title: t('message.content'), dataIndex: 'content', ellipsis: true },
                  { title: t('message.scope'), dataIndex: 'scope', width: 120, render: (value: string) => scopeTag(value, t) },
                  { title: t('message.recipients'), dataIndex: 'recipientCount', width: 90 },
                  { title: t('message.sendTime'), dataIndex: 'createTime', width: 180, render: (value: string) => formatTime(value) },
                ]}
              />
            ),
          }] : []),
        ]}
      />
      <ModalForm
        title={t('message.send')}
        open={open}
        modalProps={{ destroyOnHidden: true, onCancel: () => setOpen(false) }}
        onFinish={async (values) => {
          await api.post('/messages', { title: values.title, content: values.content, scope, userIds: values.userIds || [] });
          message.success(t('message.sentOk'));
          setOpen(false);
          setTab('sent');
          setPage(1);
          await reload();
          return true;
        }}
      >
        <ProFormText name="title" label={t('message.subject')} rules={[{ required: true, max: 120 }]} />
        <ProFormTextArea name="content" label={t('message.content')} rules={[{ required: true, max: 2000 }]} fieldProps={{ rows: 4 }} />
        <div style={{ marginBottom: 16 }}>
          <div style={{ marginBottom: 8 }}>{t('message.scope')}</div>
          <Radio.Group value={scope} onChange={(event) => setScope(event.target.value)}>
            <Radio value="Tenant">{t('message.scopeTenant')}{profile?.tenantName ? ` · ${profile.tenantName}` : ''}</Radio>
            <Radio value="Dept" disabled={!profile?.deptId}>{t('message.scopeDept')}{profile?.deptName ? ` · ${profile.deptName}` : ''}</Radio>
            <Radio value="Users">{t('message.scopeUsers')}</Radio>
          </Radio.Group>
        </div>
        {scope === 'Users' && (
          <ProFormSelect
            name="userIds"
            label={t('message.users')}
            rules={[{ required: true }]}
            fieldProps={{ mode: 'multiple' }}
            options={users.map((user) => ({ value: user.id, label: `${user.nickName} (${user.userName})${user.deptName ? ` · ${user.deptName}` : ''}` }))}
          />
        )}
      </ModalForm>
      <Drawer title={viewing?.title} open={!!viewing} width={480} onClose={() => setViewing(undefined)}>
        {viewing && (
          <Descriptions column={1}>
            <Descriptions.Item label={t('message.sender')}>{viewing.senderName}</Descriptions.Item>
            <Descriptions.Item label={t('message.scope')}>{scopeTag(viewing.scope, t)}</Descriptions.Item>
            <Descriptions.Item label={t('message.sendTime')}>{formatTime(viewing.createTime)}</Descriptions.Item>
            <Descriptions.Item label={t('message.state')}>{viewing.isRead ? t('message.readState') : t('message.unread')}</Descriptions.Item>
            <Descriptions.Item label={t('message.readTime')}>{formatTime(viewing.readTime)}</Descriptions.Item>
            <Descriptions.Item label={t('message.content')}>{viewing.content}</Descriptions.Item>
          </Descriptions>
        )}
        {viewing && !viewing.isRead && (
          <Button type="primary" style={{ marginTop: 16 }} onClick={() => { void mark(viewing); }}>{t('message.read')}</Button>
        )}
      </Drawer>
    </>
  );
}

function formatTime(value?: string) {
  return value ? dayjs(value).format('YYYY-MM-DD HH:mm:ss') : '—';
}
