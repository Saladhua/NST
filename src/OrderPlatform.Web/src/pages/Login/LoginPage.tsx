import { useEffect } from 'react';
import { App, Button, Card, Form, Input, Typography } from 'antd';
import { LockOutlined, UserOutlined } from '@ant-design/icons';
import { Link, useNavigate } from 'react-router';
import { authApi } from '../../api/auth';
import { useAuthStore } from '../../store/authStore';

interface LoginFormValues {
  userName: string;
  password: string;
}

export default function LoginPage() {
  const navigate = useNavigate();
  const { message } = App.useApp();
  const isLoggedIn = useAuthStore((state) => state.isLoggedIn());
  const setAuth = useAuthStore((state) => state.setAuth);

  useEffect(() => {
    if (isLoggedIn) {
      navigate('/', { replace: true });
    }
  }, [isLoggedIn, navigate]);

  const onFinish = async (values: LoginFormValues) => {
    try {
      const result = await authApi.login(values);
      setAuth(result);
      message.success('登录成功');
      navigate('/', { replace: true });
    } catch {
      // 错误提示已由响应拦截器统一处理
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center">
      <Card className="w-96" title={<Typography.Title level={3} className="mb-0 text-center">订单平台</Typography.Title>}>
        <Form<LoginFormValues> onFinish={onFinish} size="large">
          <Form.Item name="userName" rules={[{ required: true, message: '请输入用户名' }]}>
            <Input prefix={<UserOutlined />} placeholder="用户名" autoComplete="username" />
          </Form.Item>
          <Form.Item name="password" rules={[{ required: true, message: '请输入密码' }]}>
            <Input.Password prefix={<LockOutlined />} placeholder="密码" autoComplete="current-password" />
          </Form.Item>
          <Form.Item className="mb-2">
            <Button type="primary" htmlType="submit" block>
              登录
            </Button>
          </Form.Item>
          <div className="text-center text-sm">
            没有账号？
            <Link to="/register" className="text-blue-600">
              立即注册
            </Link>
          </div>
        </Form>
      </Card>
    </div>
  );
}
