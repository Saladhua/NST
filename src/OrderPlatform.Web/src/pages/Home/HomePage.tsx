// 首页（备用/直接访问 / 时的页面）：展示当前登录用户信息，支持修改密码与退出登录。
import { useState } from 'react';
import {
  App,
  Button,
  Card,
  Descriptions,
  Dropdown,
  Form,
  Input,
  Modal,
  Space,
  Tag,
  Typography,
} from 'antd';
import { KeyOutlined, LogoutOutlined, UserOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router';
import { authApi } from '../../api/auth';
import { useAuthStore } from '../../store/authStore';

interface ChangePasswordFormValues {
  oldPassword: string;
  newPassword: string;
  confirmPassword: string;
}

export default function HomePage() {
  const navigate = useNavigate();
  const { message } = App.useApp();
  const userInfo = useAuthStore((state) => state.userInfo);
  const clearAuth = useAuthStore((state) => state.clearAuth);
  const [modalOpen, setModalOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [form] = Form.useForm<ChangePasswordFormValues>();

  // 退出登录：清空认证信息后跳转登录页
  const handleLogout = () => {
    clearAuth();
    message.success('已退出登录');
    navigate('/login', { replace: true });
  };

  // 修改密码：成功则强制重新登录
  const handleChangePassword = async () => {
    try {
      const values = await form.validateFields();
      setSubmitting(true);
      await authApi.changePassword({
        oldPassword: values.oldPassword,
        newPassword: values.newPassword,
      });
      message.success('密码修改成功，请重新登录');
      setModalOpen(false);
      form.resetFields();
      clearAuth();
      navigate('/login', { replace: true });
    } catch {
      // 校验失败或接口错误均已提示
    } finally {
      setSubmitting(false);
    }
  };

  const dropdownItems = [
    {
      key: 'change-password',
      icon: <KeyOutlined />,
      label: '修改密码',
      onClick: () => setModalOpen(true),
    },
    { type: 'divider' as const },
    {
      key: 'logout',
      icon: <LogoutOutlined />,
      label: '退出登录',
      danger: true,
      onClick: handleLogout,
    },
  ];

  return (
    <div className="min-h-screen bg-gray-50">
      <header className="flex items-center justify-between border-b bg-white px-8 py-4">
        <Typography.Title level={4} className="mb-0">
          订单平台
        </Typography.Title>
        <Dropdown menu={{ items: dropdownItems }} placement="bottomRight">
          <Button type="text" icon={<UserOutlined />}>
            {userInfo?.displayName ?? userInfo?.userName}
          </Button>
        </Dropdown>
      </header>

      <main className="mx-auto max-w-3xl p-8">
        <Card title="当前登录用户">
          <Descriptions column={1} bordered size="middle">
            <Descriptions.Item label="用户名">{userInfo?.userName}</Descriptions.Item>
            <Descriptions.Item label="姓名">{userInfo?.displayName}</Descriptions.Item>
            <Descriptions.Item label="手机号">{userInfo?.phone ?? '-'}</Descriptions.Item>
            <Descriptions.Item label="邮箱">{userInfo?.email ?? '-'}</Descriptions.Item>
            <Descriptions.Item label="角色">
              <Tag color={userInfo?.role === 'Admin' ? 'red' : 'blue'}>
                {userInfo?.role === 'Admin' ? '管理员' : '普通用户'}
              </Tag>
            </Descriptions.Item>
          </Descriptions>
          <Space className="mt-4">
            <Button icon={<KeyOutlined />} onClick={() => setModalOpen(true)}>
              修改密码
            </Button>
            <Button danger icon={<LogoutOutlined />} onClick={handleLogout}>
              退出登录
            </Button>
          </Space>
        </Card>
      </main>

      <Modal
        title="修改密码"
        open={modalOpen}
        onOk={handleChangePassword}
        onCancel={() => {
          setModalOpen(false);
          form.resetFields();
        }}
        confirmLoading={submitting}
        okText="确认修改"
        cancelText="取消"
      >
        <Form<ChangePasswordFormValues> form={form} layout="vertical" className="mt-4">
          <Form.Item name="oldPassword" label="原密码" rules={[{ required: true, message: '请输入原密码' }]}>
            <Input.Password placeholder="请输入原密码" />
          </Form.Item>
          <Form.Item
            name="newPassword"
            label="新密码"
            rules={[
              { required: true, message: '请输入新密码' },
              { min: 6, max: 50, message: '密码长度 6-50 个字符' },
              ({ getFieldValue }) => ({
                validator(_, value) {
                  if (!value || getFieldValue('oldPassword') !== value) {
                    return Promise.resolve();
                  }
                  return Promise.reject(new Error('新密码不能与原密码相同'));
                },
              }),
            ]}
          >
            <Input.Password placeholder="请输入新密码" />
          </Form.Item>
          <Form.Item
            name="confirmPassword"
            label="确认新密码"
            dependencies={['newPassword']}
            rules={[
              { required: true, message: '请再次输入新密码' },
              ({ getFieldValue }) => ({
                validator(_, value) {
                  if (!value || getFieldValue('newPassword') === value) {
                    return Promise.resolve();
                  }
                  return Promise.reject(new Error('两次输入的密码不一致'));
                },
              }),
            ]}
          >
            <Input.Password placeholder="请再次输入新密码" />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
