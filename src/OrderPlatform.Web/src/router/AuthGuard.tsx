// 路由鉴权守卫：未登录跳转登录页；指定了 roles 时校验当前用户角色，不满足则回首页。
import type { ReactNode } from 'react';
import { Navigate } from 'react-router';
import { useAuthStore } from '../store/authStore';

interface AuthGuardProps {
  children: ReactNode;
  roles?: string[];
}

export default function AuthGuard({ children, roles }: AuthGuardProps) {
  const isLoggedIn = useAuthStore((state) => state.isLoggedIn());
  const userInfo = useAuthStore((state) => state.userInfo);

  // 未登录：跳转登录页
  if (!isLoggedIn) {
    return <Navigate to="/login" replace />;
  }

  // 角色校验：当前用户角色不在允许列表中则回到首页
  if (roles && roles.length > 0) {
    const hasRole = roles.some((role) => userInfo?.role === role);
    if (!hasRole) {
      return <Navigate to="/" replace />;
    }
  }

  return <>{children}</>;
}