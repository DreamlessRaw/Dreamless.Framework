import { ProFormDatePicker, ProFormDigit, ProFormSelect, ProFormText, ProFormTextArea } from '@ant-design/pro-components';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { usePageColumns, type ColumnMeta } from './columns';

type Props = {
  pageCode: string;
  skip?: string[];
  renderers?: Record<string, (column: ColumnMeta) => ReactNode>;
};

export default function SchemaFields({ pageCode, skip = ['createTime', 'deptName'], renderers = {} }: Props) {
  const { t } = useTranslation();
  const { columns } = usePageColumns(pageCode);
  return (
    <>
      {columns.filter((column) => column.canView && !skip.includes(column.field)).map((column) => {
        if (renderers[column.field]) {
          return <span key={column.field}>{renderers[column.field](column)}</span>;
        }
        const disabled = !column.canEdit;
        const rules = column.lockVisible ? [{ required: true }] : undefined;
        if (column.valueType === 'select' || column.dictCode || column.options.length > 0) {
          const options = column.dictCode === 'bool'
            ? [{ label: t('common.yes'), value: true }, { label: t('common.no'), value: false }]
            : column.options.map((option) => ({
              label: option.label,
              value: /Id$/i.test(column.field) && /^-?\d+$/.test(option.value) ? Number(option.value) : option.value,
            }));
          return <ProFormSelect key={column.field} name={column.field} label={column.label} disabled={disabled} rules={rules} showSearch options={options as never} />;
        }
        if (column.valueType === 'digit') {
          return <ProFormDigit key={column.field} name={column.field} label={column.label} disabled={disabled} rules={rules} />;
        }
        if (column.valueType === 'date' || column.valueType === 'dateTime') {
          return <ProFormDatePicker key={column.field} name={column.field} label={column.label} disabled={disabled} rules={rules} fieldProps={{ format: 'YYYY-MM-DD' }} />;
        }
        if (column.valueType === 'textarea') {
          return <ProFormTextArea key={column.field} name={column.field} label={column.label} disabled={disabled} rules={rules} />;
        }
        return <ProFormText key={column.field} name={column.field} label={column.label} disabled={disabled} rules={rules} />;
      })}
    </>
  );
}
