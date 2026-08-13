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
      isLoggedIn: () => Boolean(get().accessToken),
      setAuth: (auth) =>
        set({
          accessToken: auth.accessToken,
          refreshToken: auth.refreshToken,
          expiresAt: auth.expiresAt,
          userInfo: auth.userInfo,
        }),
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
