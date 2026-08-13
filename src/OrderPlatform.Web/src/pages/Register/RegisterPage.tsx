import { App, Button, Card, Form, Input, Typography } from 'antd';
import { LockOutlined, MailOutlined, MobileOutlined, SmileOutlined, UserOutlined } from '@ant-design/icons';
import { Link, useNavigate } from 'react-router';
import { authApi } from '../../api/auth';

interface RegisterFormValues {
  userName: string;
  password: string;
  confirmPassword: string;
  displayName: string;
  phone?: string;
  email?: string;
}

export default function RegisterPage() {
  const navigate = useNavigate();
  const { message } = App.useApp();

  const onFinish = async (values: RegisterFormValues) => {
    try {
      await authApi.register({
        userName: values.userName,
        password: values.password,
        displayName: values.displayName,
        phone: values.phone,
        email: values.email,
      });
      message.success('注册成功，请登录');
      navigate('/login', { replace: true });
    } catch {
      // 错误提示已由响应拦截器统一处理
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center">
      <Card
        className="w-96"
        title={
          <Typography.Title level={3} className="mb-0 text-center">
            注册账号
          </Typography.Title>
        }
      >
        <Form<RegisterFormValues> onFinish={onFinish} size="large">
          <Form.Item
            name="userName"
            rules={[
              { required: true, message: '请输入用户名' },
              { min: 3, max: 50, message: '用户名长度 3-50 个字符' },
            ]}
          >
            <Input prefix={<UserOutlined />} placeholder="用户名" />
          </Form.Item>
          <Form.Item
            name="password"
            rules={[
              { required: true, message: '请输入密码' },
              { min: 6, max: 50, message: '密码长度 6-50 个字符' },
            ]}
          >
            <Input.Password prefix={<LockOutlined />} placeholder="密码" />
          </Form.Item>
          <Form.Item
            name="confirmPassword"
            dependencies={['password']}
            rules={[
              { required: true, message: '请再次输入密码' },
              ({ getFieldValue }) => ({
                validator(_, value) {
                  if (!value || getFieldValue('password') === value) {
                    return Promise.resolve();
                  }
                  return Promise.reject(new Error('两次输入的密码不一致'));
                },
              }),
            ]}
          >
            <Input.Password prefix={<LockOutlined />} placeholder="确认密码" />
          </Form.Item>
          <Form.Item name="displayName" rules={[{ required: true, message: '请输入姓名' }]}>
            <Input prefix={<SmileOutlined />} placeholder="姓名" />
          </Form.Item>
          <Form.Item
            name="phone"
            rules={[{ pattern: /^1\d{10}$/, message: '手机号格式不正确' }]}
          >
            <Input prefix={<MobileOutlined />} placeholder="手机号（选填）" maxLength={11} />
          </Form.Item>
          <Form.Item
            name="email"
            rules={[{ type: 'email', message: '邮箱格式不正确' }]}
          >
            <Input prefix={<MailOutlined />} placeholder="邮箱（选填）" />
          </Form.Item>
          <Form.Item className="mb-2">
            <Button type="primary" htmlType="submit" block>
              注册
            </Button>
          </Form.Item>
          <div className="text-center text-sm">
            已有账号？
            <Link to="/login" className="text-blue-600">
              返回登录
            </Link>
          </div>
        </Form>
      </Card>
    </div>
  );
}
