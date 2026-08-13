import { useCallback, useEffect, useState } from 'react';
import {
  App,
  Button,
  Card,
  Form,
  Input,
  Modal,
  Popconfirm,
  Select,
  Space,
  Table,
  Tag,
} from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { userApi } from '../../api/user';
import type { CreateUserPayload, UserListDto, UserRole, UserStatus } from '../../types';

const roleOptions = [
  { value: 'Admin', label: '管理员' },
  { value: 'Operator', label: '操作员' },
];

const statusOptions = [
  { value: 'Active', label: '启用' },
  { value: 'Disabled', label: '停用' },
];

function roleTag(role: UserRole) {
  return <Tag color={role === 'Admin' ? 'red' : 'blue'}>{role === 'Admin' ? '管理员' : '操作员'}</Tag>;
}

function statusTag(status: UserStatus) {
  return <Tag color={status === 'Active' ? 'success' : 'default'}>{status === 'Active' ? '启用' : '停用'}</Tag>;
}

interface CreateUserFormValues {
  userName: string;
  password: string;
  displayName: string;
  phone?: string;
  email?: string;
  role: UserRole;
}

interface EditUserFormValues {
  displayName: string;
  phone?: string;
  email?: string;
  role: UserRole;
  status: UserStatus;
}

export default function UserManagePage() {
  const { message } = App.useApp();
  const [data, setData] = useState<UserListDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [loading, setLoading] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [editOpen, setEditOpen] = useState(false);
  const [resetOpen, setResetOpen] = useState(false);
  const [editing, setEditing] = useState<UserListDto | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [createForm] = Form.useForm<CreateUserFormValues>();
  const [editForm] = Form.useForm<EditUserFormValues>();
  const [resetForm] = Form.useForm<{ newPassword: string }>();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const result = await userApi.list({ page, pageSize });
      setData(result.items);
      setTotal(result.total);
    } catch {
      message.error('加载用户列表失败');
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const handleCreate = async () => {
    try {
      const values = await createForm.validateFields();
      setSubmitting(true);
      await userApi.create(values as CreateUserPayload);
      message.success('新增用户成功');
      setCreateOpen(false);
      createForm.resetFields();
      void load();
    } catch {
      // 校验失败或接口错误均已提示
    } finally {
      setSubmitting(false);
    }
  };

  const handleEdit = (record: UserListDto) => {
    setEditing(record);
    editForm.setFieldsValue({
      displayName: record.displayName,
      phone: record.phone ?? undefined,
      email: record.email ?? undefined,
      role: record.role,
      status: record.status,
    });
    setEditOpen(true);
  };

  const handleEditSave = async () => {
    if (!editing) {
      return;
    }
    try {
      const values = await editForm.validateFields();
      setSubmitting(true);
      await userApi.update(editing.id, values);
      message.success('更新用户成功');
      setEditOpen(false);
      void load();
    } catch {
      // 校验失败或接口错误均已提示
    } finally {
      setSubmitting(false);
    }
  };

  const handleResetPassword = async () => {
    if (!editing) {
      return;
    }
    try {
      const values = await resetForm.validateFields();
      setSubmitting(true);
      await userApi.resetPassword(editing.id, values);
      message.success('密码重置成功');
      setResetOpen(false);
      resetForm.resetFields();
    } catch {
      // 校验失败或接口错误均已提示
    } finally {
      setSubmitting(false);
    }
  };

  const handleDelete = async (record: UserListDto) => {
    try {
      await userApi.remove(record.id);
      message.success('删除用户成功');
      void load();
    } catch {
      message.error('删除失败');
    }
  };

  const columns = [
    { title: '用户名', dataIndex: 'userName', key: 'userName' },
    { title: '姓名', dataIndex: 'displayName', key: 'displayName' },
    { title: '手机号', dataIndex: 'phone', key: 'phone', render: (v: string | null) => v ?? '-' },
    { title: '邮箱', dataIndex: 'email', key: 'email', render: (v: string | null) => v ?? '-' },
    { title: '角色', dataIndex: 'role', key: 'role', width: 100, render: (v: UserRole) => roleTag(v) },
    {
      title: '状态',
      dataIndex: 'status',
      key: 'status',
      width: 90,
      render: (v: UserStatus) => statusTag(v),
    },
    {
      title: '创建时间',
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 160,
      render: (v: string) => new Date(v).toLocaleString('zh-CN'),
    },
    {
      title: '操作',
      key: 'action',
      width: 180,
      render: (_: unknown, record: UserListDto) => (
        <Space size={0}>
          <Button type="link" size="small" onClick={() => handleEdit(record)}>
            编辑
          </Button>
          <Button
            type="link"
            size="small"
            onClick={() => {
              setEditing(record);
              setResetOpen(true);
            }}
          >
            重置密码
          </Button>
          {record.userName !== 'admin' && (
            <Popconfirm
              title="确认删除该用户？"
              onConfirm={() => void handleDelete(record)}
              okText="删除"
              cancelText="取消"
            >
              <Button type="link" size="small" danger>
                删除
              </Button>
            </Popconfirm>
          )}
        </Space>
      ),
    },
  ];

  return (
    <Card
      title="用户管理"
      extra={
        <Button
          type="primary"
          icon={<PlusOutlined />}
          onClick={() => {
            createForm.resetFields();
            setCreateOpen(true);
          }}
        >
          新增用户
        </Button>
      }
    >
      <Table<UserListDto>
        rowKey="id"
        columns={columns}
        dataSource={data}
        loading={loading}
        pagination={{
          current: page,
          pageSize,
          total,
          showTotal: (t) => `共 ${t} 条`,
          onChange: (p, ps) => {
            setPage(p);
            setPageSize(ps);
          },
        }}
      />

      <Modal
        title="新增用户"
        open={createOpen}
        onOk={handleCreate}
        onCancel={() => setCreateOpen(false)}
        confirmLoading={submitting}
        okText="确认新增"
        cancelText="取消"
      >
        <Form<CreateUserFormValues> form={createForm} layout="vertical" className="mt-4">
          <Form.Item name="userName" label="用户名" rules={[{ required: true, message: '请输入用户名' }]}>
            <Input placeholder="请输入用户名" />
          </Form.Item>
          <Form.Item
            name="password"
            label="初始密码"
            rules={[
              { required: true, message: '请输入初始密码' },
              { min: 6, max: 50, message: '密码长度 6-50 个字符' },
            ]}
          >
            <Input.Password placeholder="请输入初始密码" />
          </Form.Item>
          <Form.Item name="displayName" label="姓名" rules={[{ required: true, message: '请输入姓名' }]}>
            <Input placeholder="请输入姓名" />
          </Form.Item>
          <Form.Item name="phone" label="手机号">
            <Input placeholder="请输入手机号" />
          </Form.Item>
          <Form.Item name="email" label="邮箱">
            <Input placeholder="请输入邮箱" />
          </Form.Item>
          <Form.Item name="role" label="角色" initialValue="Operator" rules={[{ required: true, message: '请选择角色' }]}>
            <Select options={roleOptions} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title="编辑用户"
        open={editOpen}
        onOk={handleEditSave}
        onCancel={() => setEditOpen(false)}
        confirmLoading={submitting}
        okText="保存"
        cancelText="取消"
      >
        <Form<EditUserFormValues> form={editForm} layout="vertical" className="mt-4">
          <Form.Item name="displayName" label="姓名" rules={[{ required: true, message: '请输入姓名' }]}>
            <Input placeholder="请输入姓名" />
          </Form.Item>
          <Form.Item name="phone" label="手机号">
            <Input placeholder="请输入手机号" />
          </Form.Item>
          <Form.Item name="email" label="邮箱">
            <Input placeholder="请输入邮箱" />
          </Form.Item>
          <Form.Item name="role" label="角色" rules={[{ required: true, message: '请选择角色' }]}>
            <Select options={roleOptions} />
          </Form.Item>
          <Form.Item name="status" label="状态" rules={[{ required: true, message: '请选择状态' }]}>
            <Select options={statusOptions} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={editing ? `重置密码 - ${editing.displayName}` : '重置密码'}
        open={resetOpen}
        onOk={handleResetPassword}
        onCancel={() => {
          setResetOpen(false);
          resetForm.resetFields();
        }}
        confirmLoading={submitting}
        okText="确认重置"
        cancelText="取消"
      >
        <Form<{ newPassword: string }> form={resetForm} layout="vertical" className="mt-4">
          <Form.Item
            name="newPassword"
            label="新密码"
            rules={[
              { required: true, message: '请输入新密码' },
              { min: 6, max: 50, message: '密码长度 6-50 个字符' },
            ]}
          >
            <Input.Password placeholder="请输入新密码" />
          </Form.Item>
        </Form>
      </Modal>
    </Card>
  );
}