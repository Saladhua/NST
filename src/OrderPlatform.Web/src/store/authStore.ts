// 认证状态管理（zustand + 持久化到 localStorage）。
// 保存访问令牌、刷新令牌与用户信息，供请求拦截器与页面鉴权使用。
import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { AuthResult, UserInfo } from '../types';

interface AuthState {
  accessToken: string;
  refreshToken: string;
  expiresAt: string | null;
  userInfo: UserInfo | null;
  isLoggedIn: () => boolean;
  setAuth: (auth: AuthResult) => void;
  clearAuth: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      accessToken: '',
      refreshToken: '',
      expiresAt: null,
      userInfo: null,
      // 是否有有效登录态：以是否存在访问令牌判断
      isLoggedIn: () => Boolean(get().accessToken),
      // 登录/刷新成功后写入认证信息
      setAuth: (auth) =>
        set({
          accessToken: auth.accessToken,
          refreshToken: auth.refreshToken,
          expiresAt: auth.expiresAt,
          userInfo: auth.userInfo,
        }),
      // 退出登录时清空认证信息
      clearAuth: () =>
        set({
          accessToken: '',
          refreshToken: '',
          expiresAt: null,
          userInfo: null,
        }),
    }),
    {
      name: 'orderplatform-auth',
    },
  ),
);