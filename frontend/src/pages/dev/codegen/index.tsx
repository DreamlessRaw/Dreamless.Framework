import { ModalForm, ProFormSwitch, ProFormText, ProTable, type ActionType } from '@ant-design/pro-components';
import { Alert, App, Button, Checkbox, Form, Input, Modal, Select, Space, Table, Tabs } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import { useAuth } from '@/auth/AuthContext';

type TableInfo = { name: string; description?: string };
type ColumnInfo = { name: string; dbType: string; csType: string; nullable: boolean; inherited: boolean; length: number; description?: string; valueType: string };
type PreviewFile = { path: string; language: string; content: string };
type Control = 'text' | 'textarea' | 'digit' | 'date' | 'dateTime' | 'enum' | 'relation';
type FieldConfig = {
  name: string;
  control: Control;
  dictCode: string;
  optionsText: string;
  relationTable?: string;
  relationValue?: string;
  relationLabel?: string;
  inherited: boolean;
  dbType: string;
  length: number;
  description?: string;
};

type StandardButtons = { add: boolean; edit: boolean; delete: boolean; import: boolean; export: boolean };
type CustomButton = { id: number; code: string; name: string };
const defaultButtons: StandardButtons = { add: true, edit: true, delete: true, import: true, export: true };
const standardKeys = ['add', 'edit', 'delete', 'import', 'export'] as const;

const controls: Control[] = ['text', 'textarea', 'digit', 'date', 'dateTime', 'enum', 'relation'];
const dicts = ['custom', 'status', 'gender', 'bool', 'dataScope', 'menuType'];

export default function CodegenPage() {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const { can } = useAuth();
  const actionRef = useRef<ActionType>(null);
  const [tables, setTables] = useState<TableInfo[]>([]);
  const [open, setOpen] = useState(false);
  const [loadingFields, setLoadingFields] = useState(false);
  const [tableName, setTableName] = useState<string>();
  const [fields, setFields] = useState<FieldConfig[]>([]);
  const [draft, setDraft] = useState<FieldConfig>();
  const [relatedColumns, setRelatedColumns] = useState<Record<string, ColumnInfo[]>>({});
  const [files, setFiles] = useState<PreviewFile[]>([]);
  const [base, setBase] = useState('');
  const [warnings, setWarnings] = useState<string[]>([]);
  const [plan, setPlan] = useState<Record<string, unknown>>();
  const [buttons, setButtons] = useState<StandardButtons>(defaultButtons);
  const [customButtons, setCustomButtons] = useState<CustomButton[]>([]);

  useEffect(() => {
    void api.get<TableInfo[]>('/codegen/tables').then((response) => setTables(response.data));
  }, []);

  useEffect(() => {
    actionRef.current?.reload();
  }, [tables]);

  const loadRelated = async (table?: string) => {
    if (!table || relatedColumns[table]) return;
    const response = await api.get<ColumnInfo[]>(`/codegen/tables/${table}/columns`);
    setRelatedColumns((current) => ({ ...current, [table]: response.data }));
  };

  const suggest = (table: string) => {
    const raw = table.replace(/^Biz_/, '').replace(/^Sys/, '');
    const parts = raw.split('_').filter(Boolean);
    const entity = parts.map((part) => part[0].toUpperCase() + part.slice(1).toLowerCase()).join('');
    return { entity, module: 'biz', title: entity, icon: 'TableOutlined' };
  };

  const guess = (column: ColumnInfo): FieldConfig => {
    const baseConfig = { name: column.name, dictCode: 'custom', optionsText: '', inherited: column.inherited, dbType: column.dbType, length: column.length, description: column.description };
    if (/status/i.test(column.name) && column.csType.startsWith('string')) return { ...baseConfig, control: 'enum', dictCode: 'status' };
    if (column.csType.startsWith('bool')) return { ...baseConfig, control: 'enum', dictCode: 'bool' };
    if (/Id$/i.test(column.name) && column.name.toLowerCase() !== 'id') {
      const stem = column.name.replace(/Id$/i, '');
      const table = tables.find((item) => item.name === `Sys${stem}` || item.name === `Biz_${stem}` || item.name.toLowerCase() === stem.toLowerCase());
      if (table) {
        void loadRelated(table.name);
        return { ...baseConfig, control: 'relation', relationTable: table.name, relationValue: 'Id', relationLabel: 'Name' };
      }
    }
    const control = column.valueType === 'select' ? 'enum' : (column.valueType as Control);
    return { ...baseConfig, control: controls.includes(control) ? control : 'text' };
  };

  const openConfig = async (name: string) => {
    setTableName(name);
    setFiles([]);
    setBase('');
    setWarnings([]);
    setPlan(undefined);
    setButtons(defaultButtons);
    setCustomButtons([]);
    setOpen(true);
    setLoadingFields(true);
    try {
      const response = await api.get<ColumnInfo[]>(`/codegen/tables/${name}/columns`);
      const next = response.data.filter((column) => !column.inherited || column.name === 'DeptId').map(guess);
      setFields(next);
      next.filter((field) => field.relationTable).forEach((field) => void loadRelated(field.relationTable));
    } finally {
      setLoadingFields(false);
    }
  };

  const summary = (field: FieldConfig) => {
    const label = t(`codegen.${field.control}`);
    if (field.control === 'enum') {
      return field.dictCode === 'custom' ? `${label} · ${t('codegen.custom')}` : `${label} · ${field.dictCode}`;
    }
    if (field.control === 'relation' && field.relationTable) return `${label} · ${field.relationTable}`;
    return label;
  };

  const payloadFields = () => fields.map((field) => ({
    name: field.name,
    valueType: field.control === 'enum' || field.control === 'relation' ? 'select' : field.control,
    dictCode: field.control === 'enum' && field.dictCode !== 'custom' ? field.dictCode : undefined,
    options: field.control === 'enum' && field.dictCode === 'custom' ? parseOptions(field.optionsText) : [],
    relationTable: field.control === 'relation' ? field.relationTable : undefined,
    relationValue: field.control === 'relation' ? field.relationValue : undefined,
    relationLabel: field.control === 'relation' ? field.relationLabel : undefined,
  }));

  const payloadButtons = () => ({
    ...buttons,
    custom: customButtons
      .filter((item) => item.code.trim() || item.name.trim())
      .map((item) => ({ code: item.code.trim(), name: item.name.trim() })),
  });

  return (
    <>
      <ProTable<TableInfo>
        rowKey="name"
        actionRef={actionRef}
        headerTitle={t('codegen.table')}
        search={{ labelWidth: 'auto' }}
        options={false}
        pagination={{ pageSize: 10 }}
        request={async (params) => {
          const keyword = String(params.keyword ?? '').trim().toLowerCase();
          const matched = tables.filter((item) => !keyword || item.name.toLowerCase().includes(keyword) || (item.description ?? '').toLowerCase().includes(keyword));
          const current = Number(params.current || 1);
          const pageSize = Number(params.pageSize || 10);
          const start = (current - 1) * pageSize;
          return { data: matched.slice(start, start + pageSize), success: true, total: matched.length };
        }}
        columns={[
          { title: t('common.keyword'), dataIndex: 'keyword', hideInTable: true },
          { title: t('codegen.table'), dataIndex: 'name', search: false },
          { title: t('codegen.title'), dataIndex: 'description', search: false },
          {
            title: t('common.actions'),
            valueType: 'option',
            width: 100,
            render: (_, row) => can('dev:codegen:preview') || can('dev:codegen:generate')
              ? [<Button key="config" type="link" size="small" onClick={() => void openConfig(row.name)}>{t('codegen.configure')}</Button>]
              : [],
          },
        ]}
      />
      <ModalForm
        title={tableName}
        open={open}
        width={960}
        initialValues={tableName ? suggest(tableName) : {}}
        modalProps={{ destroyOnHidden: true, onCancel: () => setOpen(false), styles: { body: { maxHeight: '70vh', overflow: 'auto' } } }}
        submitter={{ searchConfig: { submitText: t('codegen.preview') } }}
        onFinish={async (values) => {
          const payload = { tableName, entityName: values.entity, module: values.module, title: values.title, icon: values.icon, overwrite: values.overwrite, fields: payloadFields(), buttons: payloadButtons() };
          const response = await api.post('/codegen/preview', payload);
          setPlan(payload);
          setFiles(response.data.files);
          setBase(response.data.entityBase);
          setWarnings(response.data.warnings || []);
          return false;
        }}
      >
        <ProFormText name="entity" label={t('codegen.entity')} rules={[{ required: true }]} />
        <ProFormText name="module" label={t('codegen.module')} rules={[{ required: true }]} />
        <ProFormText name="title" label={t('codegen.title')} rules={[{ required: true }]} />
        <ProFormText name="icon" label={t('codegen.icon')} rules={[{ required: true }]} />
        <ProFormSwitch name="overwrite" label={t('codegen.overwrite')} />
        <div style={{ marginBottom: 8 }}>{t('codegen.buttons')}</div>
        <Space wrap>
          {standardKeys.map((key) => (
            <Checkbox key={key} checked={buttons[key]} onChange={(event) => setButtons({ ...buttons, [key]: event.target.checked })}>
              {t(`codegen.${key}`)}
            </Checkbox>
          ))}
        </Space>
        <div style={{ margin: '8px 0', color: '#888' }}>{t('codegen.buttonHint')}</div>
        {customButtons.length > 0 && (
          <Table
            rowKey="id"
            size="small"
            pagination={false}
            dataSource={customButtons}
            columns={[
              {
                title: t('codegen.buttonName'),
                render: (_, row) => <Input value={row.name} placeholder={t('codegen.buttonName')} onChange={(event) => setCustomButtons((current) => current.map((item) => item.id === row.id ? { ...item, name: event.target.value } : item))} />,
              },
              {
                title: t('codegen.buttonCode'),
                render: (_, row) => <Input value={row.code} placeholder="audit" onChange={(event) => setCustomButtons((current) => current.map((item) => item.id === row.id ? { ...item, code: event.target.value } : item))} />,
              },
              {
                title: t('common.actions'),
                width: 80,
                render: (_, row) => <Button type="link" danger size="small" onClick={() => setCustomButtons((current) => current.filter((item) => item.id !== row.id))}>{t('common.delete')}</Button>,
              },
            ]}
          />
        )}
        <Button type="dashed" style={{ margin: '8px 0 16px' }} onClick={() => setCustomButtons((current) => [...current, { id: Date.now(), code: '', name: '' }])}>
          {t('codegen.customButton')}
        </Button>
        <Table
          rowKey="name"
          size="small"
          loading={loadingFields}
          dataSource={fields}
          pagination={false}
          columns={[
            { title: t('codegen.field'), dataIndex: 'name', width: 140 },
            { title: t('codegen.dbType'), dataIndex: 'dbType', width: 120 },
            { title: t('codegen.length'), dataIndex: 'length', width: 80, render: (length: number) => length > 0 ? length : '—' },
            { title: t('codegen.description'), dataIndex: 'description', ellipsis: true, render: (description?: string) => description || '—' },
            { title: t('codegen.control'), render: (_, field) => summary(field) },
            {
              title: t('common.actions'),
              width: 80,
              render: (_, field) => <Button type="link" size="small" onClick={() => { setDraft({ ...field }); if (field.relationTable) void loadRelated(field.relationTable); }}>{t('codegen.setting')}</Button>,
            },
          ]}
        />
        {base && <div style={{ marginTop: 12 }}>{t('codegen.base')}：{base}</div>}
        {warnings.map((warning) => <Alert key={warning} type="warning" showIcon style={{ marginTop: 8 }} message={warning} />)}
        {files.length > 0 && (
          <Space direction="vertical" style={{ width: '100%', marginTop: 12 }}>
            <Tabs items={files.map((file) => ({ key: file.path, label: file.path.split('/').slice(-2).join('/'), children: <pre className="code-preview">{file.content}</pre> }))} />
            {can('dev:codegen:generate') && (
              <Button
                type="primary"
                onClick={async () => {
                  const response = await api.post('/codegen/generate', plan);
                  message.success(response.data.message);
                  setOpen(false);
                }}
              >
                {t('codegen.generate')}
              </Button>
            )}
          </Space>
        )}
      </ModalForm>
      <Modal
        title={draft?.name}
        open={!!draft}
        destroyOnHidden
        onCancel={() => setDraft(undefined)}
        onOk={() => {
          if (!draft) return;
          setFields((current) => current.map((field) => field.name === draft.name ? draft : field));
          setDraft(undefined);
        }}
      >
        {draft && (
          <Form layout="vertical">
            <Form.Item label={t('codegen.control')}>
              <Select
                value={draft.control}
                options={controls.map((control) => ({ value: control, label: t(`codegen.${control}`) }))}
                onChange={(control) => setDraft({ ...draft, control })}
              />
            </Form.Item>
            {draft.control === 'enum' && (
              <>
                <Form.Item label={t('codegen.dict')}>
                  <Select
                    value={draft.dictCode}
                    options={dicts.map((dict) => ({ value: dict, label: dict === 'custom' ? t('codegen.custom') : dict }))}
                    onChange={(dictCode) => setDraft({ ...draft, dictCode })}
                  />
                </Form.Item>
                {draft.dictCode === 'custom' && (
                  <Form.Item label={t('codegen.config')}>
                    <Input.TextArea rows={4} placeholder={t('codegen.options')} value={draft.optionsText} onChange={(event) => setDraft({ ...draft, optionsText: event.target.value })} />
                  </Form.Item>
                )}
              </>
            )}
            {draft.control === 'relation' && (
              <>
                <Form.Item label={t('codegen.target')}>
                  <Select
                    showSearch
                    optionFilterProp="label"
                    value={draft.relationTable}
                    options={tables.map((table) => ({ value: table.name, label: table.name }))}
                    onChange={(relationTable) => { setDraft({ ...draft, relationTable, relationValue: 'Id', relationLabel: 'Name' }); void loadRelated(relationTable); }}
                  />
                </Form.Item>
                <Form.Item label={t('codegen.valueField')}>
                  <Select
                    value={draft.relationValue}
                    options={(relatedColumns[draft.relationTable || ''] || []).map((column) => ({ value: column.name, label: column.name }))}
                    onChange={(relationValue) => setDraft({ ...draft, relationValue })}
                  />
                </Form.Item>
                <Form.Item label={t('codegen.labelField')}>
                  <Select
                    value={draft.relationLabel}
                    options={(relatedColumns[draft.relationTable || ''] || []).map((column) => ({ value: column.name, label: column.name }))}
                    onChange={(relationLabel) => setDraft({ ...draft, relationLabel })}
                  />
                </Form.Item>
              </>
            )}
          </Form>
        )}
      </Modal>
    </>
  );
}

function parseOptions(text: string) {
  return text.split('\n').map((line) => line.trim()).filter(Boolean).map((line) => {
    const split = line.indexOf('=');
    if (split < 0) return { value: line, label: line };
    return { value: line.slice(0, split).trim(), label: line.slice(split + 1).trim() || line.slice(0, split).trim() };
  });
}
