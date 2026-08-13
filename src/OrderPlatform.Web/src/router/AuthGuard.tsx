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

  if (!isLoggedIn) {
    return <Navigate to="/login" replace />;
  }

  if (roles && roles.length > 0) {
    const hasRole = roles.some((role) => userInfo?.role === role);
    if (!hasRole) {
      return <Navigate to="/" replace />;
    }
  }

  return <>{children}</>;
}
