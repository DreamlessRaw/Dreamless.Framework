import { BellOutlined } from '@ant-design/icons';
import { HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { App, Badge, Dropdown, Tag } from 'antd';
import dayjs from 'dayjs';
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { api } from '@/api/client';
import { useAuth } from '@/auth/AuthContext';

export type InboxMessage = {
  id: number;
  messageId: number;
  title: string;
  content: string;
  scope: 'Tenant' | 'Dept' | 'Users';
  senderName: string;
  isRead: boolean;
  createTime: string;
  readTime?: string;
};

type Notice = {
  title: string;
  content: string;
  senderName?: string;
  createTime?: string;
};

type MessageState = {
  unread: number;
  recent: InboxMessage[];
  revision: number;
  reload: () => Promise<void>;
};

const MessageContext = createContext<MessageState | null>(null);

export function MessageProvider({ children }: { children: ReactNode }) {
  const { notification } = App.useApp();
  const { profile } = useAuth();
  const [unread, setUnread] = useState(0);
  const [recent, setRecent] = useState<InboxMessage[]>([]);
  const [revision, setRevision] = useState(0);

  const reload = useCallback(async () => {
    const [count, inbox] = await Promise.all([
      api.get<number>('/messages/unread-count'),
      api.get<{ items: InboxMessage[] }>('/messages/inbox', { params: { page: 1, pageSize: 5, unread: true } }),
    ]);
    setUnread(Number(count.data) || 0);
    setRecent(inbox.data.items || []);
    setRevision((value) => value + 1);
  }, []);

  useEffect(() => {
    if (!profile) return undefined;
    let stopped = false;
    void reload();
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/notify', { accessTokenFactory: () => localStorage.getItem('access_token') || '' })
      .withAutomaticReconnect()
      .build();
    connection.on('Receive', (notice: Notice) => {
      const when = notice.createTime ? dayjs(notice.createTime).format('YYYY-MM-DD HH:mm:ss') : '';
      notification.info({
        message: notice.title,
        description: [notice.senderName, when, notice.content].filter(Boolean).join(' · '),
        placement: 'topRight',
      });
      void reload();
    });
    connection.onreconnected(() => { void reload(); });
    void connection.start().then(() => {
      if (stopped) void connection.stop();
    });
    return () => {
      stopped = true;
      if (connection.state !== HubConnectionState.Disconnected) void connection.stop();
    };
  }, [profile?.id, notification, reload]);

  const value = useMemo(() => ({ unread, recent, revision, reload }), [unread, recent, revision, reload]);
  return <MessageContext.Provider value={value}>{children}</MessageContext.Provider>;
}

export function useMessages() {
  const value = useContext(MessageContext);
  if (!value) {
    throw new Error('MessageProvider is missing');
  }
  return value;
}

export function MessageBell() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { unread, recent, reload } = useMessages();

  const open = async (item: InboxMessage) => {
    await api.put(`/messages/${item.messageId}/read`);
    await reload();
    navigate('/message/notice');
  };

  return (
    <Dropdown
      trigger={['click']}
      placement="bottomRight"
      overlayStyle={{ width: 320 }}
      menu={{
        items: recent.length === 0
          ? [{ key: 'empty', label: t('message.empty'), disabled: true }]
          : [
              ...recent.map((item) => ({
                key: String(item.id),
                label: (
                  <span className="message-menu-item">
                    <strong>{item.title}</strong>
                    <em>{item.senderName} · {dayjs(item.createTime).format('YYYY-MM-DD HH:mm:ss')}</em>
                    <span>{item.content}</span>
                  </span>
                ),
                onClick: () => { void open(item); },
              })),
              { type: 'divider' as const },
              { key: 'all', label: t('message.all'), onClick: () => navigate('/message/notice') },
            ],
      }}
    >
      <Badge count={unread} size="small" offset={[-4, 4]}>
        <button type="button" className="header-tool" aria-label={t('message.title')}>
          <BellOutlined />
        </button>
      </Badge>
    </Dropdown>
  );
}

export function scopeTag(scope: string, t: (key: string) => string) {
  const key = scope === 'Dept' ? 'message.scopeDept' : scope === 'Users' ? 'message.scopeUsers' : 'message.scopeTenant';
  return <Tag>{t(key)}</Tag>;
}
