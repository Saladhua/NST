// 订单列表页：支持关键词、客户、关联状态、推送状态筛选；
// 可查看详情、推送订单（推送后弹窗展示结果与 ERP 报文），管理员可删除未推送订单。
import { useCallback, useEffect, useState } from 'react';
import type { Key } from 'react';
import { App, Button, Card, Descriptions, Input, Select, Space, Table, Tag, Tooltip, Typography } from 'antd';
import { DeleteOutlined, ReloadOutlined, SearchOutlined, SendOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router';
import dayjs from 'dayjs';
import { orderApi } from '../../api/order';
import { uploadApi } from '../../api/upload';
import { useAuthStore } from '../../store/authStore';
import type {
  BatchPushItemResult,
  BatchPushResult,
  CustomerImportDto,
  ErpApiCall,
  MatchStatus,
  OrderListDto,
  PushResult,
  PushStatus,
} from '../../types';

// 关联状态筛选项
const parseStatusOptions = [
  { value: 'Matched', label: '已关联' },
  { value: 'Partial', label: '部分关联' },
  { value: 'Unmatched', label: '未关联' },
];

// 推送状态筛选项
const pushStatusOptions = [
  { value: 'NotPushed', label: '未推送' },
  { value: 'PartialPushed', label: '部分推送' },
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
    PartialPushed: { color: 'processing', text: '部分推送' },
    Pushed: { color: 'success', text: '已推送' },
    Failed: { color: 'error', text: '推送失败' },
  };
  const item = map[status] ?? { color: 'default', text: status };
  return <Tag color={item.color}>{item.text}</Tag>;
}

/** ERP 报文格式化：JSON 则美化缩进，否则原文展示。 */
function formatPayload(text: string): string {
  if (!text) {
    return '（空）';
  }
  try {
    return JSON.stringify(JSON.parse(text), null, 2);
  } catch {
    return text;
  }
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
  // 正在推送的订单 ID（按钮 loading 反馈，避免「点了没反应」）
  const [pushingId, setPushingId] = useState<string | null>(null);
  // 勾选的行（批量推送用；已推送订单不可选）
  const [selectedRowKeys, setSelectedRowKeys] = useState<Key[]>([]);
  // 批量推送进行中
  const [batchPushing, setBatchPushing] = useState(false);

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

  // 推送订单到 ERP，完成后弹窗展示成功/失败与 ERP 返回报文
  const pushOrder = async (id: string) => {
    setPushingId(id);
    try {
      const result = await orderApi.push(id);
      showPushResult(result);
      void load();
    } catch {
      message.error('推送请求失败，请检查网络或 ERP 服务后重试');
    } finally {
      setPushingId(null);
    }
  };

  // 弹窗展示推送结果与 ERP 报文
  const showPushResult = (result: PushResult) => {
    const statusTag =
      result.status === 'Success' ? 'success' : result.status === 'Partial' ? 'warning' : 'error';
    const statusText =
      result.status === 'Success' ? '推送成功' : result.status === 'Partial' ? '部分推送成功' : '推送失败';
    modal.info({
      title: (
        <Space>
          <Tag color={statusTag}>{statusText}</Tag>
          {result.osNo ? `ERP 受订单号：${result.osNo}` : '未取得受订单号'}
        </Space>
      ),
      width: 760,
      okText: '知道了',
      content: (
        <div className="mt-4">
          <Descriptions size="small" column={3} bordered className="mb-4">
            <Descriptions.Item label="待推行数">{result.totalCount}</Descriptions.Item>
            <Descriptions.Item label="成功行数">{result.pushedCount}</Descriptions.Item>
            <Descriptions.Item label="失败行数">{result.failedCount}</Descriptions.Item>
          </Descriptions>
          {result.errorMessage && (
            <Typography.Paragraph type="danger" className="mb-4">
              {result.errorMessage}
            </Typography.Paragraph>
          )}
          <Typography.Title level={5}>ERP 调用报文</Typography.Title>
          {(result.erpCalls as ErpApiCall[]).length === 0 ? (
            <Typography.Text type="secondary">无 ERP 调用记录</Typography.Text>
          ) : (
            <div className="space-y-2">
              {(result.erpCalls as ErpApiCall[]).map((call, index) => (
                <details
                  key={index}
                  open={!call.success}
                  style={{ border: '1px solid #f0f0f0', borderRadius: 6, padding: '6px 10px' }}
                >
                  <summary style={{ cursor: 'pointer' }}>
                    <Space size={8}>
                      <Tag color={call.success ? 'success' : 'error'}>{call.success ? '成功' : '失败'}</Tag>
                      <Typography.Text strong>{call.action}</Typography.Text>
                      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {call.httpStatus ?? '-'} · {call.durationMs}ms{call.error ? ` · ${call.error}` : ''}
                      </Typography.Text>
                    </Space>
                  </summary>
                  <div style={{ marginTop: 8 }}>
                    <Typography.Paragraph type="secondary" style={{ marginBottom: 4, fontSize: 12 }}>
                      请求报文：
                    </Typography.Paragraph>
                    <pre className="max-h-40 overflow-auto rounded bg-gray-50 p-2 text-xs whitespace-pre-wrap break-all">
                      {formatPayload(call.requestJson)}
                    </pre>
                    <Typography.Paragraph type="secondary" style={{ marginBottom: 4, marginTop: 8, fontSize: 12 }}>
                      响应报文：
                    </Typography.Paragraph>
                    <pre className="max-h-64 overflow-auto rounded bg-gray-50 p-2 text-xs whitespace-pre-wrap break-all">
                      {formatPayload(call.response)}
                    </pre>
                  </div>
                </details>
              ))}
            </div>
          )}
        </div>
      ),
    });
  };

  // 批量推送选中订单：先确认，逐个请求后端批量接口，成功后弹窗汇总并刷新列表
  const handleBatchPush = () => {
    const selected = data.filter((r) => selectedRowKeys.includes(r.id));
    if (selected.length === 0) {
      return;
    }
    const orderNoById = new Map(data.map((o) => [o.id, o.orderNo]));
    modal.confirm({
      title: '批量推送',
      content: `确定推送选中的 ${selected.length} 个订单吗？将逐个推送到 ERP。`,
      okText: '推送',
      cancelText: '取消',
      transitionName: 'ant-fade',
      maskTransitionName: 'ant-fade',
      onOk: async () => {
        setBatchPushing(true);
        try {
          const result = await orderApi.batchPush(selected.map((o) => o.id));
          showBatchPushResult(result, orderNoById);
          setSelectedRowKeys([]);
          void load();
        } catch {
          message.error('批量推送请求失败，请检查网络或 ERP 服务后重试');
        } finally {
          setBatchPushing(false);
        }
      },
    });
  };

  // 弹窗展示批量推送汇总：顶部统计，下方逐单状态与错误信息（报文详情仍走单笔推送）
  const showBatchPushResult = (result: BatchPushResult, orderNoById: Map<string, string>) => {
    const statusTag = (status: BatchPushItemResult['status']) =>
      status === 'Success' ? 'success' : status === 'Partial' ? 'warning' : 'error';
    const statusText = (status: BatchPushItemResult['status']) =>
      status === 'Success' ? '推送成功' : status === 'Partial' ? '部分推送' : '推送失败';
    modal.info({
      title: '批量推送结果',
      width: 720,
      okText: '知道了',
      content: (
        <div className="mt-4">
          <Descriptions size="small" column={3} bordered className="mb-4">
            <Descriptions.Item label="订单数">{result.total}</Descriptions.Item>
            <Descriptions.Item label="成功">{result.successCount}</Descriptions.Item>
            <Descriptions.Item label="失败">{result.failedCount}</Descriptions.Item>
          </Descriptions>
          <Typography.Title level={5}>逐单结果</Typography.Title>
          <div className="space-y-2" style={{ maxHeight: 320, overflow: 'auto' }}>
            {result.results.length === 0 ? (
              <Typography.Text type="secondary">无推送记录</Typography.Text>
            ) : (
              result.results.map((item) => (
                <div key={item.orderId} style={{ border: '1px solid #f0f0f0', borderRadius: 6, padding: '6px 10px' }}>
                  <Space size={8}>
                    <Tag color={statusTag(item.status)}>{statusText(item.status)}</Tag>
                    <Typography.Text strong>{orderNoById.get(item.orderId) ?? item.orderId}</Typography.Text>
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      成功 {item.pushedCount} 行 · 失败 {item.failedCount} 行
                    </Typography.Text>
                  </Space>
                  {item.errorMessage && (
                    <Typography.Paragraph type="danger" style={{ marginTop: 4, marginBottom: 0 }}>
                      {item.errorMessage}
                    </Typography.Paragraph>
                  )}
                </div>
              ))
            )}
          </div>
        </div>
      ),
    });
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
      ellipsis: true,
      render: (value: string, record: OrderListDto) => (
        <Tooltip title={value}>
          <a onClick={() => navigate(`/orders/${record.id}`)}>{value}</a>
        </Tooltip>
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
      title: 'ERP受订单号',
      dataIndex: 'erpOsNo',
      key: 'erpOsNo',
      width: 200,
      ellipsis: true,
      render: (value: string | null) =>
        value ? (
          <Tooltip title={value}>
            <span>{value}</span>
          </Tooltip>
        ) : (
          '-'
        ),
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
            loading={pushingId === record.id}
            disabled={
              record.pushStatus === 'Pushed' ||
              batchPushing ||
              (pushingId !== null && pushingId !== record.id)
            }
            onClick={() => void pushOrder(record.id)}
          >
            {pushingId === record.id ? '推送中...' : record.pushStatus === 'PartialPushed' ? '继续推送' : '推送'}
          </Button>
          {isAdmin && (
            <Tooltip title={record.pushStatus === 'Pushed' || record.pushStatus === 'PartialPushed' ? '已推送（含部分推送）的订单不可删除' : ''}>
              <Button
                type="link"
                size="small"
                danger
                icon={<DeleteOutlined />}
                disabled={record.pushStatus === 'Pushed' || record.pushStatus === 'PartialPushed'}
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
          }}
          options={pushStatusOptions}
          style={{ width: 140 }}
        />
        <Button type="primary" onClick={() => void load()}>
          查询
        </Button>
        <Button
          type="primary"
          icon={<SendOutlined />}
          loading={batchPushing}
          disabled={selectedRowKeys.length === 0}
          onClick={() => void handleBatchPush()}
        >
          批量推送{selectedRowKeys.length > 0 ? `（${selectedRowKeys.length}）` : ''}
        </Button>
      </Space>

      <Table<OrderListDto>
        rowKey="id"
        columns={columns}
        dataSource={data}
        loading={loading}
        scroll={{ x: 'max-content' }}
        rowSelection={{
          selectedRowKeys,
          onChange: (keys) => setSelectedRowKeys(keys),
          getCheckboxProps: (record) => ({ disabled: record.pushStatus === 'Pushed' }),
        }}
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