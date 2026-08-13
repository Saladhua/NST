import axios, { type AxiosRequestConfig, type AxiosResponse } from 'axios';
import { message } from 'antd';
import { useAuthStore } from '../store/authStore';
import { getMessageInstance } from '../utils/message-holder';
import type { AuthResult } from '../types';

export interface HttpInstance {
  get<T>(url: string, config?: AxiosRequestConfig): Promise<T>;
  post<T>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T>;
  put<T>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T>;
  delete<T>(url: string, config?: AxiosRequestConfig): Promise<T>;
  request<T>(config: AxiosRequestConfig): Promise<T>;
}

interface ApiResponse<T> {
  code: number;
  success: boolean;
  message: string;
  data: T;
}

interface RetriableRequestConfig extends AxiosRequestConfig {
  _retried?: boolean;
}

const axiosInstance = axios.create({
  baseURL: '/api',
  timeout: 15000,
});

const http = axiosInstance as unknown as HttpInstance;

function notifyError(content: string): void {
  const instance = getMessageInstance();
  if (instance) {
    void instance.error(content);
    return;
  }
  message.error(content);
}

axiosInstance.interceptors.request.use((config) => {
  const { accessToken } = useAuthStore.getState();
  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`;
  }
  return config;
});

let isRefreshing = false;
let pendingQueue: Array<(token: string) => void> = [];

function flushQueue(token: string) {
  pendingQueue.forEach((resolve) => resolve(token));
  pendingQueue = [];
}

function rejectQueue(reason: unknown) {
  pendingQueue.forEach((resolve) => resolve(''));
  pendingQueue = [];
  void reason;
}

async function refreshTokenOnce(): Promise<string> {
  const { refreshToken } = useAuthStore.getState();
  if (!refreshToken) {
    throw new Error('缂哄皯鍒锋柊浠ょ墝');
  }

  if (isRefreshing) {
    return new Promise<string>((resolve, reject) => {
      pendingQueue.push((token) => (token ? resolve(token) : reject(new Error('鍒锋柊浠ょ墝澶辫触'))));
    });
  }

  isRefreshing = true;
  try {
    const response = await axios.post<ApiResponse<AuthResult>>(
      '/api/auth/refresh',
      { refreshToken },
      { timeout: 15000 },
    );
    const body = response.data;
    if (body.code !== 200 || !body.data?.accessToken) {
      throw new Error(body.message || '鍒锋柊浠ょ墝澶辫触');
    }
    useAuthStore.getState().setAuth(body.data);
    flushQueue(body.data.accessToken);
    return body.data.accessToken;
  } catch (error) {
    rejectQueue(error);
    throw error;
  } finally {
    isRefreshing = false;
  }
}

function redirectToLogin() {
  useAuthStore.getState().clearAuth();
  if (window.location.pathname !== '/login') {
    window.location.href = '/login';
  }
}

axiosInstance.interceptors.response.use(
  (response) => {
    const body = response.data as ApiResponse<unknown>;
    if (body && typeof body === 'object' && 'code' in body) {
      if (body.code === 200) {
        return body.data as unknown as AxiosResponse;
      }
      notifyError(body.message || '璇锋眰澶辫触');
      return Promise.reject(new Error(body.message || '璇锋眰澶辫触'));
    }
    return body as unknown as AxiosResponse;
  },
  async (error) => {
    const original = error.config as RetriableRequestConfig | undefined;
    const status = error.response?.status as number | undefined;
    const body = error.response?.data as ApiResponse<unknown> | undefined;
    const isUnauthorized = status === 401 || body?.code === 401;

    if (isUnauthorized && original && !original._retried) {
      original._retried = true;
      try {
        const newToken = await refreshTokenOnce();
        if (!newToken) {
          throw new Error('鍒锋柊浠ょ墝澶辫触');
        }
        original.headers = original.headers ?? {};
        (original.headers as Record<string, string>).Authorization = `Bearer ${newToken}`;
        return http.request(original);
      } catch {
        notifyError('鐧诲綍鐘舵€佸凡澶辨晥锛岃閲嶆柊鐧诲綍');
        redirectToLogin();
        return Promise.reject(error);
      }
    }

    if (body?.message) {
      notifyError(body.message);
    } else if (status === 401) {
      notifyError('鐧诲綍鐘舵€佸凡澶辨晥锛岃閲嶆柊鐧诲綍');
      redirectToLogin();
    } else {
      notifyError(error.message || '缃戠粶閿欒');
    }
    return Promise.reject(error);
  },
);

export default http;
