// 主布局组件：左侧侧边栏菜单 + 顶部用户信息区 + 内容区（Outlet 渲染子路由）。
// 菜单根据当前用户角色动态渲染，普通用户不显示「用户管理」「系统设置」。
import { useState } from 'react';
import {
  App,
  Avatar,
  Button,
  Dropdown,
  Form,
  Input,
  Layout,
  Menu,
  Modal,
  theme,
  Typography,
} from 'antd';
import {
  DashboardOutlined,
  FileTextOutlined,
  LogoutOutlined,
  KeyOutlined,
  SettingOutlined,
  TeamOutlined,
  UploadOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { Outlet, useLocation, useNavigate } from 'react-router';
import { authApi } from '../api/auth';
import { useAuthStore } from '../store/authStore';

const { Sider, Header, Content } = Layout;

interface ChangePasswordFormValues {
  oldPassword: string;
  newPassword: string;
  confirmPassword: string;
}

export default function AppLayout() {
  const navigate = useNavigate();
  // 当前路由信息，用于高亮侧边栏选中项
  const location = useLocation();
  const { message } = App.useApp();
  const { token } = theme.useToken();
  const userInfo = useAuthStore((state) => state.userInfo);
  const clearAuth = useAuthStore((state) => state.clearAuth);
  const [modalOpen, setModalOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [form] = Form.useForm<ChangePasswordFormValues>();

  const isAdmin = userInfo?.role === 'Admin';

  const menuItems = [
    { key: '/', icon: <DashboardOutlined />, label: '工作台' },
    { key: '/upload', icon: <UploadOutlined />, label: '上传订单' },
    { key: '/orders', icon: <FileTextOutlined />, label: '订单列表' },
    ...(isAdmin
      ? [
          { key: '/users', icon: <TeamOutlined />, label: '用户管理' },
          { key: '/settings', icon: <SettingOutlined />, label: '系统设置' },
        ]
      : []),
  ];

  // 退出登录：清空认证信息后跳回登录页
  const handleLogout = () => {
    clearAuth();
    message.success('已退出登录');
    navigate('/login', { replace: true });
  };

  // 修改密码：成功后强制重新登录
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

  // 根据当前路径匹配侧边栏高亮的菜单项（支持 /orders/:id 这类子路径）
  const selectedKey = menuItems
    .map((item) => item.key)
    .find((key) => location.pathname === key || location.pathname.startsWith(`${key}/`));

  return (
    <Layout className="min-h-screen">
      <Sider breakpoint="lg" collapsedWidth={64} theme="dark">
        <div className="flex items-center justify-center py-5">
          <Typography.Text strong className="text-lg !text-white">
            耐世拓订单平台
          </Typography.Text>
        </div>
        <Menu
          theme="dark"
          mode="inline"
          selectedKeys={selectedKey ? [selectedKey] : []}
          items={menuItems}
          onClick={({ key }) => navigate(key)}
        />
      </Sider>

      <Layout>
        <Header
          className="flex items-center justify-end gap-4 px-6"
          style={{ background: token.colorBgContainer }}
        >
          <Dropdown menu={{ items: dropdownItems }} placement="bottomRight">
            <Button type="text" className="flex items-center gap-2">
              <Avatar size="small" icon={<UserOutlined />} />
              {userInfo?.displayName ?? userInfo?.userName}
            </Button>
          </Dropdown>
        </Header>

        <Content className="p-6">
          <Outlet />
        </Content>
      </Layout>

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
    </Layout>
  );
}
