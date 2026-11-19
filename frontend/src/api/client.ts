import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios';
import { message } from 'antd';
import i18n from '@/i18n';

export const api = axios.create({ baseURL: '/api' });

type RetryConfig = InternalAxiosRequestConfig & { retried?: boolean };

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('access_token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  config.headers['Accept-Language'] = i18n.language || 'zh-CN';
  return config;
});

let refreshTask: Promise<string> | null = null;

async function refreshAccessToken() {
  const refreshToken = localStorage.getItem('refresh_token');
  if (!refreshToken) {
    throw new Error('missing refresh token');
  }
  const response = await axios.post('/api/auth/refresh', { refreshToken }, {
    headers: { 'Accept-Language': i18n.language || 'zh-CN' },
  });
  localStorage.setItem('access_token', response.data.accessToken);
  localStorage.setItem('refresh_token', response.data.refreshToken);
  return response.data.accessToken as string;
}

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<{ message?: string }>) => {
    const config = error.config as RetryConfig | undefined;
    if (error.response?.status === 401 && config && !config.retried && !config.url?.includes('/auth/login')) {
      config.retried = true;
      try {
        refreshTask ??= refreshAccessToken().finally(() => {
          refreshTask = null;
        });
        const token = await refreshTask;
        config.headers.Authorization = `Bearer ${token}`;
        return api(config);
      } catch {
        localStorage.removeItem('access_token');
        localStorage.removeItem('refresh_token');
        window.dispatchEvent(new Event('dreamless:logout'));
      }
    }

    const data = error.response?.data;
    let text = data?.message || error.message;
    if (data instanceof Blob) {
      try {
        text = JSON.parse(await data.text()).message || text;
      } catch {
        text = error.message;
      }
    }
    if (text && error.response?.status !== 401) {
      message.error(text);
    }
    return Promise.reject(error);
  },
);
