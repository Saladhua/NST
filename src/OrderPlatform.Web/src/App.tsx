// 应用路由定义：登录/注册为独立页面，其余页面位于 AppLayout 内并通过 AuthGuard 鉴权。
import { BrowserRouter, Navigate, Route, Routes } from 'react-router';
import AuthGuard from './router/AuthGuard';
import AppLayout from './layout/AppLayout';
import LoginPage from './pages/Login/LoginPage';
import RegisterPage from './pages/Register/RegisterPage';
import DashboardPage from './pages/Dashboard/DashboardPage';
import UploadPage from './pages/Upload/UploadPage';
import OrderListPage from './pages/Orders/OrderListPage';
import OrderDetailPage from './pages/Orders/OrderDetailPage';
import UserManagePage from './pages/Users/UserManagePage';
import SettingsPage from './pages/Settings/SettingsPage';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* 无需登录的页面 */}
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        {/* 需要登录的页面，统一包在 AppLayout 布局内 */}
        <Route
          path="/"
          element={
            <AuthGuard>
              <AppLayout />
            </AuthGuard>
          }
        >
          <Route index element={<DashboardPage />} />
          <Route path="upload" element={<UploadPage />} />
          <Route path="orders" element={<OrderListPage />} />
          <Route path="orders/:id" element={<OrderDetailPage />} />
          {/* 用户管理与系统设置仅管理员可见 */}
          <Route
            path="users"
            element={
              <AuthGuard roles={['Admin']}>
                <UserManagePage />
              </AuthGuard>
            }
          />
          <Route
            path="settings"
            element={
              <AuthGuard roles={['Admin']}>
                <SettingsPage />
              </AuthGuard>
            }
          />
        </Route>
        {/* 未知路径回退到首页 */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}