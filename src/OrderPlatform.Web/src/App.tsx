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
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
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
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
