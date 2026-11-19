import { useCallback, useEffect, useState } from 'react';
import { api } from '@/api/client';
import i18n from '@/i18n';

export type ColumnMeta = {
  field: string;
  label: string;
  valueType: string;
  canView: boolean;
  canEdit: boolean;
  canExport: boolean;
  visible: boolean;
  sort: number;
  width?: number;
  fixed?: 'left' | 'right' | null;
  sortOrder?: 'asc' | 'desc' | null;
  sortRank?: number | null;
  lockVisible: boolean;
  dictCode?: string;
  options: { label: string; value: string }[];
};

const cache = new Map<string, ColumnMeta[]>();

export function clearColumnCache(pageCode?: string) {
  if (!pageCode) {
    cache.clear();
    return;
  }
  [...cache.keys()].filter((key) => key.startsWith(`${pageCode}:`)).forEach((key) => cache.delete(key));
}

export function usePageColumns(pageCode: string) {
  const [columns, setColumns] = useState<ColumnMeta[]>([]);
  const [loading, setLoading] = useState(false);

  const reload = useCallback(async () => {
    const key = `${pageCode}:${i18n.language}`;
    setLoading(true);
    try {
      const response = await api.get<{ columns: ColumnMeta[] }>(`/meta/pages/${encodeURIComponent(pageCode)}/columns`);
      cache.set(key, response.data.columns);
      setColumns(response.data.columns);
    } finally {
      setLoading(false);
    }
  }, [pageCode]);

  useEffect(() => {
    const key = `${pageCode}:${i18n.language}`;
    const cached = cache.get(key);
    if (cached) {
      setColumns(cached);
      return;
    }
    void reload();
  }, [pageCode, reload, i18n.language]);

  return { columns, loading, reload };
}
