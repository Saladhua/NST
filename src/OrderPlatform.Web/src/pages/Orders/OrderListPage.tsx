// 订单列表页：支持关键词、客户、关联状态、推送状态筛选；
// 可查看详情、推送订单，管理员可删除未推送订单。
import { useCallback, useEffect, useState } from 'react';
import { App, Button, Card, Input, Select, Space, Table, Tag, Tooltip } from 'antd';
import { DeleteOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router';
import dayjs from 'dayjs';
import { orderApi } from '../../api/order';
import { uploadApi } from '../../api/upload';
import { useAuthStore } from '../../store/authStore';
import type { CustomerImportDto, MatchStatus, OrderListDto, PushStatus } from '../../types';

// 关联状态筛选项
const parseStatusOptions = [
  { value: 'Matched', label: '已关联' },
  { value: 'Partial', label: '部分关联' },
  { value: 'Unmatched', label: '未关联' },
];

// 推送状态筛选项
const pushStatusOptions = [
  { value: 'NotPushed', label: '未推送' },
  { value: 'Pushed', label: '已推送' },
  { value: 'Failed', label: '推送失败' },
];

function matchStatusTag(status: MatchStatus) {
  const map: Record<string, { color: string; text: string }> = {
    Matched: { color: 'success', text: '已关联' },
    Partial: { color: 'warning', text: '部分关联' },
    Unmatched: { color: 'error', text: '未关联' },
  };
  const item = map[status] ?? { color: 'default', text: status };
  return <Tag color={item.color}>{item.text}</Tag>;
}

function pushStatusTag(status: PushStatus) {
  const map: Record<string, { color: string; text: string }> = {
    NotPushed: { color: 'default', text: '未推送' },
    Pushed: { color: 'success', text: '已推送' },
    Failed: { color: 'error', text: '推送失败' },
  };
  const item = map[status] ?? { color: 'default', text: status };
  return <Tag color={item.color}>{item.text}</Tag>;
}

export default function OrderListPage() {
  const { message, modal } = App.useApp();
  const navigate = useNavigate();
  const isAdmin = useAuthStore((s) => s.userInfo?.role === 'Admin');

  const [loading, setLoading] = useState(false);
  const [data, setData] = useState<OrderListDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [keyword, setKeyword] = useState('');
  const [customerId, setCustomerId] = useState<string | undefined>();
  const [pushStatus, setPushStatus] = useState<PushStatus | undefined>();
  const [parseStatus, setParseStatus] = useState<MatchStatus | undefined>();
  const [customers, setCustomers] = useState<CustomerImportDto[]>([]);

  // 加载客户下拉列表
  const loadCustomers = useCallback(async () => {
    try {
      setCustomers(await uploadApi.customers());
    } catch {
      setCustomers([]);
    }
  }, []);

  // 按当前筛选条件加载订单列表
  const load = useCallback(async () => {
    setLoading(true);
    try {
      const result = await orderApi.list({
        page,
        pageSize,
        keyword: keyword || undefined,
        customerId,
        pushStatus,
        parseStatus,
      });
      setData(result.items);
      setTotal(result.total);
    } catch {
      message.error('加载订单列表失败');
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, keyword, customerId, pushStatus, parseStatus, message]);

  useEffect(() => {
    void load();
    void loadCustomers();
  }, [load, loadCustomers]);

  // 推送订单到客户系统
  const pushOrder = async (id: string) => {
    try {
      await orderApi.push(id);
      message.success('推送成功');
      void load();
    } catch {
      message.error('推送失败，请重试');
    }
  };

  const deleteOrder = async (id: string) => {
    try {
      await orderApi.delete(id);
      message.success('删除成功');
      void load();
    } catch {
      message.error('删除失败');
    }
  };

  // 删除订单前二次确认
  const handleDeleteOrder = (record: OrderListDto) => {
    modal.confirm({
      title: '删除订单',
      content: `确定删除订单「${record.orderNo}」吗？删除后不可恢复。`,
      okText: '删除',
      okButtonProps: { danger: true },
      cancelText: '取消',
      transitionName: 'ant-fade',
      maskTransitionName: 'ant-fade',
      onOk: () => deleteOrder(record.id),
    });
  };

  const columns = [
    {
      title: '订单号',
      dataIndex: 'orderNo',
      key: 'orderNo',
      render: (value: string, record: OrderListDto) => (
        <a onClick={() => navigate(`/orders/${record.id}`)}>{value}</a>
      ),
    },
    { title: '客户', dataIndex: 'customerName', key: 'customerName', width: 140 },
    {
      title: '订单日期',
      dataIndex: 'orderDate',
      key: 'orderDate',
      width: 120,
      render: (value: string | null) => (value ? dayjs(value).format('YYYY-MM-DD') : '-'),
    },
    {
      title: '总数量',
      dataIndex: 'totalQuantity',
      key: 'totalQuantity',
      width: 120,
      render: (value: number) => value.toLocaleString('zh-CN'),
    },
    {
      title: '总金额（元）',
      dataIndex: 'totalAmount',
      key: 'totalAmount',
      width: 140,
      render: (value: number) => value.toLocaleString('zh-CN', { minimumFractionDigits: 2 }),
    },
    {
      title: '关联状态',
      dataIndex: 'parseStatus',
      key: 'parseStatus',
      width: 100,
      render: (value: MatchStatus) => matchStatusTag(value),
    },
    {
      title: '推送状态',
      dataIndex: 'pushStatus',
      key: 'pushStatus',
      width: 100,
      render: (value: PushStatus) => pushStatusTag(value),
    },
    {
      title: '创建时间',
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 160,
      render: (value: string) => dayjs(value).format('YYYY-MM-DD HH:mm'),
    },
    {
      title: '操作',
      key: 'action',
      width: 150,
      render: (_: unknown, record: OrderListDto) => (
        <Space>
          <Button type="link" size="small" onClick={() => navigate(`/orders/${record.id}`)}>
            查看
          </Button>
          <Button
            type="link"
            size="small"
            disabled={record.pushStatus === 'Pushed'}
            onClick={() => void pushOrder(record.id)}
          >
            推送
          </Button>
          {isAdmin && (
            <Tooltip title={record.pushStatus === 'Pushed' ? '已推送的订单不可删除' : ''}>
              <Button
                type="link"
                size="small"
                danger
                icon={<DeleteOutlined />}
                disabled={record.pushStatus === 'Pushed'}
                onClick={() => handleDeleteOrder(record)}
              >
                删除
              </Button>
            </Tooltip>
          )}
        </Space>
      ),
    },
  ];

  return (
    <Card
      title="订单列表"
      extra={
        <Button icon={<ReloadOutlined />} onClick={() => void load()}>
          刷新
        </Button>
      }
    >
      <Space className="mb-4 flex flex-wrap">
        <Input
          placeholder="订单号 / 关键词"
          prefix={<SearchOutlined />}
          allowClear
          value={keyword}
          onChange={(e) => setKeyword(e.target.value)}
          onPressEnter={() => {
            setPage(1);
            void load();
          }}
          style={{ width: 220 }}
        />
        <Select
          placeholder="客户"
          allowClear
          showSearch
          optionFilterProp="label"
          value={customerId}
          onChange={(value) => {
            setCustomerId(value);
            setPage(1);
            void load();
          }}
          options={customers.map((c) => ({ value: c.customerId, label: c.customerName }))}
          style={{ width: 160 }}
        />
        <Select
          placeholder="关联状态"
          allowClear
          value={parseStatus}
          onChange={(value) => {
            setParseStatus(value as MatchStatus | undefined);
            setPage(1);
            void load();
          }}
          options={parseStatusOptions}
          style={{ width: 140 }}
        />
        <Select
          placeholder="推送状态"
          allowClear
          value={pushStatus}
          onChange={(value) => {
            setPushStatus(value as PushStatus | undefined);
            setPage(1);
            void load();
          }}
          options={pushStatusOptions}
          style={{ width: 140 }}
        />
        <Button type="primary" onClick={() => setPage(1)}>
          查询
        </Button>
      </Space>

      <Table<OrderListDto>
        rowKey="id"
        columns={columns}
        dataSource={data}
        loading={loading}
        pagination={{
          current: page,
          pageSize,
          total,
          showSizeChanger: true,
          showTotal: (t) => `共 ${t} 条`,
          onChange: (p, ps) => {
            setPage(p);
            setPageSize(ps);
          },
        }}
      />
    </Card>
  );
}