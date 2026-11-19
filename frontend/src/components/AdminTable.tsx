import { CaretDownOutlined, CaretUpOutlined } from '@ant-design/icons';
import { DrawerForm, ProTable, type ActionType, type ProColumns } from '@ant-design/pro-components';
import { App, Button, InputNumber, Popconfirm, Select, Space, Switch, Upload } from 'antd';
import { forwardRef, useEffect, useImperativeHandle, useRef, useState, type Ref } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import { useAuth } from '@/auth/AuthContext';
import { useFontSize } from '@/theme/fontSize';
import { clearColumnCache, usePageColumns, type ColumnMeta } from './columns';

export type AdminTableRef = { reload: () => void };

export type TableAction = {
  key: string;
  label: string;
  permission?: string;
  confirm?: string;
  className?: string;
  onClick: (row: Record<string, unknown>) => Promise<void> | void;
};

type Props = {
  pageCode: string;
  headerTitle?: string;
  request: (params: Record<string, unknown>) => Promise<{ items: unknown[]; total: number }>;
  permissions?: { create?: string; edit?: string; delete?: string; import?: string; export?: string };
  importUrl?: string;
  exportUrl?: string;
  templateUrl?: string;
  onCreate?: () => void;
  onEdit?: (row: Record<string, unknown>) => void;
  onDelete?: (row: Record<string, unknown>) => Promise<void> | void;
  actions?: TableAction[];
  pagination?: false;
  rowKey?: string;
  actionRef?: Ref<AdminTableRef>;
};

const AdminTable = forwardRef<AdminTableRef, Props>(function AdminTable(props, ref) {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const { can } = useAuth();
  const tableRef = useRef<ActionType>(null);
  const sortsRef = useRef<SortRule[]>([]);
  const [sorts, setSorts] = useState<SortRule[]>([]);
  const { columns, reload } = usePageColumns(props.pageCode);
  const { fontSize } = useFontSize();
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState<ColumnMeta[]>([]);

  const tableApi = { reload: () => tableRef.current?.reload() };
  useImperativeHandle(ref, () => tableApi);
  useEffect(() => {
    if (props.actionRef && typeof props.actionRef !== 'function') {
      (props.actionRef as { current: AdminTableRef | null }).current = tableApi;
    }
  });
  useEffect(() => {
    if (columns.length === 0) return;
    const next = defaultSorts(columns);
    sortsRef.current = next;
    setSorts(next);
    tableRef.current?.reload();
  }, [columns]);

  const tableColumns: ProColumns[] = [
    { title: t('common.keyword'), dataIndex: 'keyword', hideInTable: true },
    ...columns.map((column) => {
      const sortable = column.valueType !== 'image';
      const rank = sorts.findIndex((item) => item.field === column.field);
      const order = rank >= 0 ? sorts[rank].order : undefined;
      const valueEnum = column.options?.length
        ? Object.fromEntries(column.options.map((option) => [String(option.value), { text: option.label }]))
        : undefined;
      return {
        title: (_schema: unknown, type?: string) => type === 'form' || !sortable ? column.label : (
          <button
            type="button"
            className="sort-header"
            title={rank < 0 ? t('common.sortAdd') : order === 'ascend' ? t('common.sortDesc') : t('common.sortClear')}
            onClick={() => cycle(column.field)}
          >
            <span>{column.label}</span>
            <span className="sort-carets" aria-hidden="true">
              <CaretUpOutlined className={order === 'ascend' ? 'on' : ''} />
              <CaretDownOutlined className={order === 'descend' ? 'on' : ''} />
            </span>
            {rank >= 0 ? <span className="sort-rank">{rank + 1}</span> : null}
          </button>
        ),
        dataIndex: column.field,
        valueType: searchValueType(column),
        valueEnum,
        width: columnWidth(column, fontSize),
        colSize: column.valueType === 'date' || column.valueType === 'dateTime' ? 2 : undefined,
        onCell: () => column.valueType === 'date' || column.valueType === 'dateTime' ? { style: { whiteSpace: 'nowrap' } } : {},
        fixed: column.fixed || undefined,
        hideInSearch: !column.canView || column.valueType === 'image',
        hideInTable: !column.visible,
        render: (_: unknown, row: Record<string, unknown>) => renderCell(column, row[column.field], t('common.yes'), t('common.no')),
      } as ProColumns;
    }),
    {
      title: t('common.actions'),
      valueType: 'option',
      width: 140 + (props.actions?.length ?? 0) * 56,
      fixed: 'right',
      render: (_, row) => [
        props.onEdit && can(props.permissions?.edit) ? <a key="edit" onClick={() => props.onEdit?.(row)}>{t('common.edit')}</a> : null,
        props.onDelete && can(props.permissions?.delete) ? (
          <Popconfirm key="delete" title={t('common.confirmDelete')} onConfirm={async () => { await props.onDelete?.(row); message.success(t('common.deleted')); tableRef.current?.reload(); }}>
            <a className="danger-link">{t('common.delete')}</a>
          </Popconfirm>
        ) : null,
        ...(props.actions ?? []).filter((action) => can(action.permission)).map((action) => action.confirm ? (
          <Popconfirm key={action.key} title={action.confirm} onConfirm={() => action.onClick(row)}>
            <a className={action.className}>{action.label}</a>
          </Popconfirm>
        ) : (
          <a key={action.key} className={action.className} onClick={() => action.onClick(row)}>{action.label}</a>
        )),
      ],
    },
  ];

  const download = async (url: string, fallback: string, params?: Record<string, unknown>) => {
    let response;
    try {
      response = await api.get(url, { params, responseType: 'blob' });
    } catch {
      return;
    }
    const blob = response.data as Blob;
    if (blob.type.includes('json')) {
      const body = JSON.parse(await blob.text());
      message.error(body.message || '导出失败');
      return;
    }
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = fallback;
    link.click();
    URL.revokeObjectURL(link.href);
  };

  return (
    <>
      <ProTable
        rowKey={props.rowKey || 'id'}
        actionRef={tableRef}
        headerTitle={props.headerTitle}
        columns={tableColumns}
        search={{ labelWidth: 'auto' }}
        scroll={{ x: 'max-content' }}
        dateFormatter="string"
        pagination={props.pagination === false ? false : { defaultPageSize: 20 }}
        request={async (params) => {
          const { current, pageSize, keyword, ...rest } = params;
          const filters = collectFilters(rest);
          const filtersParam = Object.keys(filters).length ? JSON.stringify(filters) : '';
          const sortsParam = sortsRef.current.map((item) => `${item.field}:${item.order === 'descend' ? 'desc' : 'asc'}`).join(',');
          const data = await props.request({
            page: current,
            pageSize,
            ...(keyword ? { keyword } : {}),
            ...(sortsParam ? { sorts: sortsParam } : {}),
            ...(filtersParam ? { filters: filtersParam } : {}),
          });
          const items = props.pagination === false
            ? sortTree(filterTree(data.items, filters, typeof keyword === 'string' ? keyword : ''), sortsRef.current)
            : data.items;
          return { data: items, total: props.pagination === false ? items.length : data.total, success: true };
        }}
        toolBarRender={() => [
          <Button key="columns" onClick={() => { setDraft(columns.map((column) => ({ ...column }))); setOpen(true); }}>{t('common.columns')}</Button>,
          props.templateUrl && can(props.permissions?.import) ? <Button key="template" onClick={() => download(props.templateUrl!, `${props.pageCode}-template.xlsx`)}>{t('common.template')}</Button> : null,
          props.importUrl && can(props.permissions?.import) ? (
            <Upload
              key="import"
              accept=".xlsx"
              showUploadList={false}
              customRequest={async ({ file, onSuccess, onError }) => {
                try {
                  const form = new FormData();
                  form.append('file', file as File);
                  const response = await api.post(props.importUrl!, form);
                  const errors = response.data.errors as string[];
                  if (errors?.length) message.error(errors.slice(0, 5).join('；'));
                  else message.success(`${t('common.imported')} ${response.data.success}`);
                  tableRef.current?.reload();
                  onSuccess?.({});
                } catch (error) {
                  onError?.(error as Error);
                }
              }}
            >
              <Button>{t('common.import')}</Button>
            </Upload>
          ) : null,
          props.exportUrl && can(props.permissions?.export) ? <Button key="export" onClick={() => download(props.exportUrl!, `${props.pageCode}.xlsx`)}>{t('common.export')}</Button> : null,
          props.onCreate && can(props.permissions?.create) ? <Button key="create" type="primary" onClick={props.onCreate}>{t('common.create')}</Button> : null,
        ]}
      />
      <DrawerForm
        title={t('common.columns')}
        open={open}
        width={720}
        onOpenChange={setOpen}
        onFinish={async () => {
          await api.put(`/meta/pages/${encodeURIComponent(props.pageCode)}/columns`, draft.map((column) => ({
            field: column.field,
            visible: column.lockVisible || column.visible,
            sort: column.sort,
            width: column.width,
            fixed: column.fixed,
            sortOrder: column.sortOrder || null,
            sortRank: column.sortOrder ? column.sortRank ?? 1 : null,
          })));
          clearColumnCache(props.pageCode);
          await reload();
          message.success(t('common.saved'));
          return true;
        }}
      >
        <Space direction="vertical" style={{ width: '100%' }} size={12}>
          <div style={{ display: 'grid', gridTemplateColumns: 'minmax(88px, 1fr) 52px 72px 96px 168px', gap: 8, opacity: 0.65, fontSize: 12 }}>
            <span />
            <span>{t('common.visible')}</span>
            <span>{t('common.columnOrder')}</span>
            <span>{t('common.fixed')}</span>
            <span>{t('common.defaultSort')}</span>
          </div>
          {draft.map((column, index) => (
            <div key={column.field} style={{ display: 'grid', gridTemplateColumns: 'minmax(88px, 1fr) 52px 72px 96px 168px', gap: 8, alignItems: 'center' }}>
              <span>{column.label}</span>
              <Switch checked={column.lockVisible || column.visible} disabled={column.lockVisible} onChange={(checked) => update(index, { visible: checked })} />
              <InputNumber min={0} max={9999} value={column.sort} onChange={(value) => update(index, { sort: Number(value || 0) })} />
              <Select
                value={column.fixed || ''}
                options={[
                  { value: '', label: t('common.none') },
                  { value: 'left', label: t('common.left') },
                  { value: 'right', label: t('common.right') },
                ]}
                onChange={(value) => update(index, { fixed: (value || null) as ColumnMeta['fixed'] })}
              />
              <div style={{ display: 'flex', gap: 4 }}>
                <Select
                  style={{ width: column.sortOrder ? 100 : 168 }}
                  disabled={column.valueType === 'image'}
                  value={column.sortOrder || ''}
                  options={[
                    { value: '', label: t('common.sortNone') },
                    { value: 'asc', label: t('common.sortAsc') },
                    { value: 'desc', label: t('common.sortDescLabel') },
                  ]}
                  onChange={(value) => {
                    if (!value) {
                      update(index, { sortOrder: null, sortRank: null });
                      return;
                    }
                    update(index, { sortOrder: value as ColumnMeta['sortOrder'], sortRank: column.sortRank ?? nextSortRank(draft) });
                  }}
                />
                {column.sortOrder ? (
                  <InputNumber
                    min={1}
                    max={5}
                    title={t('common.sortRank')}
                    value={column.sortRank ?? 1}
                    style={{ width: 64 }}
                    onChange={(value) => update(index, { sortRank: Number(value || 1) })}
                  />
                ) : null}
              </div>
            </div>
          ))}
        </Space>
      </DrawerForm>
    </>
  );

  function cycle(field: string) {
    const next = cycleSort(sortsRef.current, field);
    sortsRef.current = next;
    setSorts(next);
    tableRef.current?.reload();
  }

  function update(index: number, patch: Partial<ColumnMeta>) {
    setDraft((current) => current.map((column, itemIndex) => itemIndex === index ? { ...column, ...patch } : column));
  }
});

function columnWidth(column: ColumnMeta, fontSize: number) {
  const scale = fontSize / 14;
  const floor = column.valueType === 'dateTime' ? 200 : column.valueType === 'date' ? 128 : 0;
  const base = column.width ?? floor;
  return base ? Math.round(base * scale) : undefined;
}

function searchValueType(column: ColumnMeta): 'text' | 'select' | 'digit' | 'dateRange' {
  if (column.valueType === 'select' || column.dictCode || column.options?.length) return 'select';
  if (column.valueType === 'digit') return 'digit';
  if (column.valueType === 'date' || column.valueType === 'dateTime') return 'dateRange';
  return 'text';
}

function collectFilters(rest: Record<string, unknown>) {
  const filters: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(rest)) {
    if (!key || key.startsWith('_') || value == null || value === '') continue;
    if (Array.isArray(value)) {
      const items = value.filter((item) => item != null && item !== '').map((item) => (
        typeof item === 'string' || typeof item === 'number' || typeof item === 'boolean' ? item : String(item)
      ));
      if (items.length) filters[key] = items;
      continue;
    }
    if (typeof value === 'object') continue;
    filters[key] = value;
  }
  return filters;
}

function filterTree(items: unknown[], filters: Record<string, unknown>, keyword: string): unknown[] {
  return items.flatMap((item) => {
    if (!item || typeof item !== 'object') return [];
    const row = item as Record<string, unknown>;
    const children = Array.isArray(row.children) ? filterTree(row.children, filters, keyword) : undefined;
    if (matches(row, filters, keyword)) return [item];
    if (children?.length) return [{ ...row, children }];
    return [];
  });
}

function matches(row: Record<string, unknown>, filters: Record<string, unknown>, keyword: string) {
  if (keyword.trim()) {
    const text = keyword.trim().toLowerCase();
    const hit = Object.values(row).some((value) => typeof value === 'string' && value.toLowerCase().includes(text));
    if (!hit) return false;
  }
  return Object.entries(filters).every(([field, expected]) => matchValue(row[field], expected));
}

function matchValue(actual: unknown, expected: unknown) {
  if (Array.isArray(expected)) {
    const [start, end] = expected.map((item) => Date.parse(String(item)));
    const value = Date.parse(String(actual ?? ''));
    if (Number.isNaN(value)) return false;
    if (!Number.isNaN(start) && value < start) return false;
    if (!Number.isNaN(end) && value >= end + 24 * 60 * 60 * 1000) return false;
    return true;
  }
  if (actual == null) return false;
  return String(actual).toLowerCase().includes(String(expected).toLowerCase());
}

type SortOrder = 'ascend' | 'descend';
type SortRule = { field: string; order: SortOrder };

function defaultSorts(columns: ColumnMeta[]): SortRule[] {
  return columns
    .filter((column) => column.valueType !== 'image' && (column.sortOrder === 'asc' || column.sortOrder === 'desc'))
    .sort((left, right) => (left.sortRank ?? 99) - (right.sortRank ?? 99))
    .slice(0, 5)
    .map((column) => ({ field: column.field, order: column.sortOrder === 'desc' ? 'descend' : 'ascend' }));
}

function nextSortRank(columns: ColumnMeta[]) {
  const used = columns.filter((column) => column.sortOrder).map((column) => column.sortRank ?? 0);
  return Math.min(5, Math.max(0, ...used) + 1);
}

function cycleSort(current: SortRule[], field: string): SortRule[] {
  const index = current.findIndex((item) => item.field === field);
  if (index < 0) {
    const added: SortRule = { field, order: 'ascend' };
    return [...current, added].slice(0, 5);
  }
  const next = current.slice();
  if (next[index].order === 'ascend') {
    const updated: SortRule = { field, order: 'descend' };
    next[index] = updated;
    return next;
  }
  next.splice(index, 1);
  return next;
}

function sortTree(items: unknown[], sorts: SortRule[]): unknown[] {
  if (sorts.length === 0) return items;
  return [...items].sort((left, right) => compareRow(left, right, sorts)).map((item) => {
    if (!item || typeof item !== 'object' || !Array.isArray((item as { children?: unknown[] }).children)) return item;
    const row = item as { children: unknown[] };
    return { ...row, children: sortTree(row.children, sorts) };
  });
}

function compareRow(left: unknown, right: unknown, sorts: SortRule[]) {
  const a = left as Record<string, unknown>;
  const b = right as Record<string, unknown>;
  for (const sort of sorts) {
    const result = compareValue(a?.[sort.field], b?.[sort.field]);
    if (result !== 0) return sort.order === 'descend' ? -result : result;
  }
  return 0;
}

function compareValue(left: unknown, right: unknown) {
  const empty = (value: unknown) => value == null || value === '';
  if (empty(left) && empty(right)) return 0;
  if (empty(left)) return 1;
  if (empty(right)) return -1;
  if (typeof left === 'number' && typeof right === 'number') return left - right;
  const leftDate = Date.parse(String(left));
  const rightDate = Date.parse(String(right));
  if (!Number.isNaN(leftDate) && !Number.isNaN(rightDate) && /\d{4}-\d{2}-\d{2}/.test(String(left))) return leftDate - rightDate;
  return String(left).localeCompare(String(right), 'zh-CN', { numeric: true, sensitivity: 'base' });
}

function renderCell(column: ColumnMeta, value: unknown, yes: string, no: string) {
  if (value == null || value === '') return '-';
  if (column.valueType === 'image') {
    return <img src={String(value)} alt="" style={{ width: 32, height: 32, borderRadius: 16, objectFit: 'cover' }} />;
  }
  if (column.dictCode === 'bool') return value === true || value === 'true' ? yes : no;
  const matched = column.options?.find((option) => option.value === String(value));
  if (matched) return matched.label;
  if (column.valueType === 'date') return String(value).slice(0, 10);
  if (column.valueType === 'dateTime') return String(value).replace('T', ' ').slice(0, 19);
  return String(value);
}

export default AdminTable;
