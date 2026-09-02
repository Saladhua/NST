// 订单详情页：展示订单头信息与明细行（规格/长度/收口/材质拆分列、物料同步状态、行级推送状态），
// 支持整单/单行物料同步、行级推送（部分关联订单可推送已匹配行），推送后展示结果与 ERP 报文。
import { useCallback, useEffect, useState } from 'react';
import type { Key, SyntheticEvent } from 'react';
import {
  App,
  Button,
  Card,
  Descriptions,
  Modal,
  Space,
  Table,
  Tag,
  Tooltip,
  Typography,
} from 'antd';
import {
  ArrowLeftOutlined,
  CloudSyncOutlined,
  PushpinOutlined,
  ReloadOutlined,
} from '@ant-design/icons';
import { useNavigate, useParams } from 'react-router';
import type { ResizeCallbackData } from 'react-resizable';
import { orderApi } from '../../api/order';
import ResizableTitle from '../../components/ResizableTitle';
import type {
  ErpApiCall,
  ItemPushStatus,
  MatchStatus,
  MaterialSyncStatus,
  OrderDetailDto,
  OrderItemDto,
  PushResult,
  PushStatus,
} from '../../types';

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

function materialSyncTag(status: MaterialSyncStatus, prdNo: string) {
  const map: Record<string, { color: string; text: string }> = {
    NotSynced: { color: 'default', text: '未同步' },
    Synced: { color: 'success', text: '已同步' },
    NotFound: { color: 'warning', text: '未找到' },
    Failed: { color: 'error', text: '失败' },
  };
  const item = map[status] ?? { color: 'default', text: status };
  return (
    <Tooltip title={status === 'Synced' && prdNo ? `货品代号：${prdNo}` : undefined}>
      <Tag color={item.color}>{item.text}</Tag>
    </Tooltip>
  );
}

function itemPushTag(status: ItemPushStatus) {
  const map: Record<string, { color: string; text: string }> = {
    NotPushed: { color: 'default', text: '未推送' },
    Pushed: { color: 'success', text: '已推送' },
    Failed: { color: 'error', text: '失败' },
  };
  const item = map[status] ?? { color: 'default', text: status };
  return <Tag color={item.color}>{item.text}</Tag>;
}

// 明细表列默认宽度（行备注 230 = 原 180 + 50）
const columnDefaults: Record<string, number> = {
  lineNo: 55,
  materialCode: 170,
  spec: 170,
  length: 80,
  shouKou: 70,
  material: 80,
  customerPartNo: 120,
  nestPartNo: 110,
  remark: 230,
  receiveDate: 110,
  quantity: 90,
  amount: 100,
  matchStatus: 90,
  materialSync: 150,
  itemPushStatus: 85,
  itemErpOsNo: 130,
};

// 各列最小宽度（拖拽下限）：窄列放宽下限，避免误触
const columnMinWidths: Record<string, number> = {
  lineNo: 40,
  shouKou: 50,
};

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

/** 推送调用轨迹报文（可折叠的只读代码块）。 */
function ErpCallList({ calls }: { calls: ErpApiCall[] }) {
  if (calls.length === 0) {
    return <Typography.Text type="secondary">无 ERP 调用记录</Typography.Text>;
  }
  return (
    <div className="space-y-2">
      {calls.map((call, index) => (
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
  );
}

/** 弹窗展示推送结果与 ERP 返回报文。 */
function usePushResultModal() {
  const [result, setResult] = useState<PushResult | null>(null);
  const close = () => setResult(null);
  const modal = result && (
    <Modal
      title={
        <Space>
          <Tag color={result.status === 'Success' ? 'success' : result.status === 'Partial' ? 'warning' : 'error'}>
            {result.status === 'Success' ? '推送成功' : result.status === 'Partial' ? '部分推送成功' : '推送失败'}
          </Tag>
          {result.osNo ? `ERP 受订单号：${result.osNo}` : '未取得受订单号'}
        </Space>
      }
      open
      footer={<Button type="primary" onClick={close}>知道了</Button>}
      width={760}
      onCancel={close}
    >
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
      <ErpCallList calls={result.erpCalls} />
    </Modal>
  );
  return { show: setResult, modal };
}

export default function OrderDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { message } = App.useApp();
  const navigate = useNavigate();
  const pushResult = usePushResultModal();

  const [detail, setDetail] = useState<OrderDetailDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [pushing, setPushing] = useState(false);
  const [syncing, setSyncing] = useState(false);
  // 勾选推送的明细行 ID（仅未推送且已匹配的行可勾选）
  const [selectedItemIds, setSelectedItemIds] = useState<Key[]>([]);
  // 列宽拖拽覆盖值（仅本次会话，刷新后恢复默认）
  const [columnWidths, setColumnWidths] = useState<Record<string, number>>({});

  // 加载订单详情
  const load = useCallback(async () => {
    if (!id) {
      return;
    }
    setLoading(true);
    try {
      setDetail(await orderApi.detail(id));
    } catch {
      message.error('加载订单详情失败');
    } finally {
      setLoading(false);
    }
  }, [id, message]);

  useEffect(() => {
    void load();
  }, [load]);

  // 推送勾选的明细行（行级：只推未推送且已匹配的行），完成后弹窗展示结果与 ERP 报文
  const pushOrder = async () => {
    if (!id) {
      return;
    }
    if (selectedItemIds.length === 0) {
      message.warning('请先勾选要推送的明细行');
      return;
    }
    setPushing(true);
    try {
      const result = await orderApi.push(id, selectedItemIds as string[]);
      setSelectedItemIds([]);
      pushResult.show(result);
      await load();
    } catch {
      message.error('推送请求失败，请检查网络或 ERP 服务后重试');
    } finally {
      setPushing(false);
    }
  };

  // 整单物料同步
  const syncOrderMaterial = async () => {
    if (!id) {
      return;
    }
    setSyncing(true);
    try {
      const result = await orderApi.syncMaterial(id);
      const createdText = result.created > 0 ? `，新建货品 ${result.created}` : '';
      message.success(`物料同步完成：成功 ${result.synced}${createdText}，未找到 ${result.notFound}，失败 ${result.failed}`);
      await load();
    } catch {
      message.error('物料同步失败，请重试');
    } finally {
      setSyncing(false);
    }
  };

  // 单行物料同步
  const syncItemMaterial = async (itemId: string) => {
    if (!id) {
      return;
    }
    try {
      const result = await orderApi.syncMaterial(id, itemId);
      message.success(result.created > 0 ? '物料同步完成（已在 ERP 新建货品）' : '物料同步完成');
      await load();
    } catch {
      message.error('物料同步失败');
    }
  };

  const widthOf = (key: string) => columnWidths[key] ?? columnDefaults[key];

  const minWidthOf = (key: string) => columnMinWidths[key] ?? 60;

  // 表头拖拽回调：更新对应列宽（不小于最小宽度）；仅存于 state，刷新页面即恢复默认
  const handleResize =
    (key: string) =>
    (_: SyntheticEvent, { size }: ResizeCallbackData) => {
      setColumnWidths((prev) => ({ ...prev, [key]: Math.max(Math.round(size.width), minWidthOf(key)) }));
    };

  // 客户差异化展示：三可客户图号 = 物料编码-收口；创达数量取行备注「XXX根」
  const isSanke = detail?.customerName?.includes('三可') ?? false;
  const isChuangda = detail?.customerName?.includes('创达') ?? false;

  // 从行备注提取「XXX根」数量（创达）
  const qtyFromRemark = (remark: string): number | null => {
    const m = remark.match(/(\d+(?:\.\d+)?)\s*根/);
    return m ? Number(m[1]) : null;
  };

  // 收口数值规范化：4.0 → 4、4.50 → 4.5；非数值文本原样返回
  const normalizeShouKou = (value: string): string => {
    const v = value?.trim() ?? '';
    if (!v) {
      return '-';
    }
    if (v === '不收口') {
      return v;
    }
    const num = Number(v);
    return Number.isFinite(num) ? String(num) : v;
  };

  // 明细表每列均支持拖拽调宽：width 取「拖拽覆盖值 ?? 默认宽度」，onHeaderCell 注入拖拽回调
  const columns = [
    {
      title: '行号',
      dataIndex: 'lineNo',
      key: 'lineNo',
      width: widthOf('lineNo'),
      onHeaderCell: () => ({
        width: widthOf('lineNo'),
        minWidth: minWidthOf('lineNo'),
        onResize: handleResize('lineNo'),
      }),
    },
    {
      title: '物料编码',
      dataIndex: 'materialCode',
      key: 'materialCode',
      width: widthOf('materialCode'),
      onHeaderCell: () => ({
        width: widthOf('materialCode'),
        minWidth: minWidthOf('materialCode'),
        onResize: handleResize('materialCode'),
      }),
    },
    {
      title: '收口',
      dataIndex: 'shouKou',
      key: 'shouKou',
      width: widthOf('shouKou'),
      onHeaderCell: () => ({
        width: widthOf('shouKou'),
        minWidth: minWidthOf('shouKou'),
        onResize: handleResize('shouKou'),
      }),
      render: (value: string) => normalizeShouKou(value),
    },
    {
      title: '规格',
      dataIndex: 'spec',
      key: 'spec',
      width: widthOf('spec'),
      onHeaderCell: () => ({
        width: widthOf('spec'),
        minWidth: minWidthOf('spec'),
        onResize: handleResize('spec'),
      }),
      ellipsis: true,
    },
    {
      title: '长度',
      dataIndex: 'length',
      key: 'length',
      width: widthOf('length'),
      onHeaderCell: () => ({
        width: widthOf('length'),
        minWidth: minWidthOf('length'),
        onResize: handleResize('length'),
      }),
      render: (value: number | null) => (value === null ? '-' : value),
    },
    {
      title: '材质',
      dataIndex: 'material',
      key: 'material',
      width: widthOf('material'),
      onHeaderCell: () => ({
        width: widthOf('material'),
        minWidth: minWidthOf('material'),
        onResize: handleResize('material'),
      }),
      render: (value: string) => value || '-',
    },
    {
      title: '客户图号',
      dataIndex: 'customerPartNo',
      key: 'customerPartNo',
      width: widthOf('customerPartNo'),
      onHeaderCell: () => ({
        width: widthOf('customerPartNo'),
        minWidth: minWidthOf('customerPartNo'),
        onResize: handleResize('customerPartNo'),
      }),
      ellipsis: true,
      render: (_: unknown, record: OrderItemDto) => {
        // 三可：客户图号 = 物料编码/收口值.0收口（如 JSJ0406437/4.0收口）
        if (isSanke) {
          const shouKou = normalizeShouKou(record.shouKou);
          if (shouKou && shouKou !== '0' && shouKou !== '-') {
            // 数值统一保留一位小数并固定「收口」后缀
            const num = Number(shouKou);
            const formatted = Number.isFinite(num) ? num.toFixed(1) : shouKou;
            return `${record.materialCode}/${formatted}收口`;
          }
          return record.materialCode || '-';
        }
        return record.customerPartNo || '-';
      },
    },
    {
      title: 'NEST图号',
      dataIndex: 'nestPartNo',
      key: 'nestPartNo',
      width: widthOf('nestPartNo'),
      onHeaderCell: () => ({
        width: widthOf('nestPartNo'),
        minWidth: minWidthOf('nestPartNo'),
        onResize: handleResize('nestPartNo'),
      }),
      ellipsis: true,
    },
    {
      title: '行备注',
      dataIndex: 'remark',
      key: 'remark',
      width: widthOf('remark'),
      onHeaderCell: () => ({
        width: widthOf('remark'),
        minWidth: minWidthOf('remark'),
        onResize: handleResize('remark'),
      }),
      ellipsis: true,
      render: (value: string) =>
        value ? (
          <Tooltip title={value}>
            <span>{value}</span>
          </Tooltip>
        ) : (
          '-'
        ),
    },
    {
      title: '交货日期',
      dataIndex: 'receiveDate',
      key: 'receiveDate',
      width: widthOf('receiveDate'),
      onHeaderCell: () => ({
        width: widthOf('receiveDate'),
        minWidth: minWidthOf('receiveDate'),
        onResize: handleResize('receiveDate'),
      }),
      render: (_: unknown, record: OrderItemDto) => {
        // 要货日期：优先 PDF/Excel 里的交货日期，没有则用推送日期（点击推送的日期）
        const date = record.receiveDate ?? record.pushedAt;
        return date ? date.slice(0, 10) : '-';
      },
    },
    {
      title: '数量',
      dataIndex: 'quantity',
      key: 'quantity',
      width: widthOf('quantity'),
      onHeaderCell: () => ({
        width: widthOf('quantity'),
        minWidth: minWidthOf('quantity'),
        onResize: handleResize('quantity'),
      }),
      render: (_: unknown, record: OrderItemDto) => {
        // 创达：数量取行备注「XXX根」，只显示数量不带单位
        if (isChuangda) {
          const qty = qtyFromRemark(record.remark) ?? record.quantity;
          return qty.toLocaleString('zh-CN');
        }
        const unit = record.unit ? ` ${record.unit}` : '';
        return `${record.quantity.toLocaleString('zh-CN')}${unit}`;
      },
    },
    {
      title: '金额（元）',
      dataIndex: 'amount',
      key: 'amount',
      width: widthOf('amount'),
      onHeaderCell: () => ({
        width: widthOf('amount'),
        minWidth: minWidthOf('amount'),
        onResize: handleResize('amount'),
      }),
      render: (value: number) => value.toLocaleString('zh-CN', { minimumFractionDigits: 2 }),
    },
    {
      title: '关联状态',
      dataIndex: 'matchStatus',
      key: 'matchStatus',
      width: widthOf('matchStatus'),
      onHeaderCell: () => ({
        width: widthOf('matchStatus'),
        minWidth: minWidthOf('matchStatus'),
        onResize: handleResize('matchStatus'),
      }),
      render: (value: MatchStatus) => matchStatusTag(value),
    },
    {
      title: '物料同步',
      key: 'materialSync',
      width: widthOf('materialSync'),
      onHeaderCell: () => ({
        width: widthOf('materialSync'),
        minWidth: minWidthOf('materialSync'),
        onResize: handleResize('materialSync'),
      }),
      render: (_: unknown, record: OrderItemDto) =>
        record.matchStatus === 'Matched' ? (
          <Space size={4}>
            {materialSyncTag(record.materialSyncStatus, record.erpPrdNo)}
            {record.materialSyncStatus !== 'Synced' && (
              <Button type="link" size="small" onClick={() => void syncItemMaterial(record.id)}>
                同步
              </Button>
            )}
          </Space>
        ) : (
          '-'
        ),
    },
    {
      title: 'ERP受单号',
      dataIndex: 'erpOsNo',
      key: 'itemErpOsNo',
      width: widthOf('itemErpOsNo'),
      onHeaderCell: () => ({
        width: widthOf('itemErpOsNo'),
        minWidth: minWidthOf('itemErpOsNo'),
        onResize: handleResize('itemErpOsNo'),
      }),
      ellipsis: true,
      render: (value: string) => value || '-',
    },
    {
      title: '行推送',
      dataIndex: 'itemPushStatus',
      key: 'itemPushStatus',
      width: widthOf('itemPushStatus'),
      onHeaderCell: () => ({
        width: widthOf('itemPushStatus'),
        minWidth: minWidthOf('itemPushStatus'),
        onResize: handleResize('itemPushStatus'),
      }),
      render: (value: ItemPushStatus) => itemPushTag(value),
    },
  ];

  // 已关联明细行数（用于展示关联占比）
  const matchedCount = detail?.items.filter((i) => i.matchStatus === 'Matched').length ?? 0;
  const pushedCount = detail?.items.filter((i) => i.itemPushStatus === 'Pushed').length ?? 0;
  // 是否还有可推送的行（未推送/失败且已匹配）
  const hasPushable =
    (detail?.items.some((i) => i.itemPushStatus !== 'Pushed' && i.matchStatus === 'Matched') ?? false);

  return (
    <div className="space-y-4">
      <Space>
        <Button icon={<ArrowLeftOutlined />} onClick={() => navigate('/orders')}>
          返回
        </Button>
        <Button icon={<ReloadOutlined />} onClick={() => void load()}>
          刷新
        </Button>
      </Space>

      <Card
        title="订单信息"
        loading={loading}
        extra={
          <Space>
            <Button
              icon={<CloudSyncOutlined />}
              loading={syncing}
              disabled={!matchedCount}
              onClick={() => void syncOrderMaterial()}
            >
              同步物料
            </Button>
            <Button
              type="primary"
              icon={<PushpinOutlined />}
              disabled={detail?.pushStatus === 'Pushed' || !hasPushable}
              loading={pushing}
              onClick={() => void pushOrder()}
            >
              {detail?.pushStatus === 'Pushed'
                ? '已推送'
                : selectedItemIds.length > 0
                  ? `推送勾选行（${selectedItemIds.length}）`
                  : '推送'}
            </Button>
          </Space>
        }
      >
        {detail && (
          <Descriptions bordered size="small" column={3}>
            <Descriptions.Item label="订单号">{detail.orderNo}</Descriptions.Item>
            <Descriptions.Item label="客户">{detail.customerName}</Descriptions.Item>
            <Descriptions.Item label="订单日期">
              {detail.orderDate ? new Date(detail.orderDate).toLocaleDateString('zh-CN') : '-'}
            </Descriptions.Item>
            <Descriptions.Item label="总数量">{detail.totalQuantity.toLocaleString('zh-CN')}</Descriptions.Item>
            <Descriptions.Item label="总金额">
              {detail.totalAmount.toLocaleString('zh-CN', { minimumFractionDigits: 2 })} 元
            </Descriptions.Item>
            <Descriptions.Item label="创建时间">
              {new Date(detail.createdAt).toLocaleString('zh-CN')}
            </Descriptions.Item>
            <Descriptions.Item label="关联状态">{matchStatusTag(detail.parseStatus)}</Descriptions.Item>
            <Descriptions.Item label="推送状态">{pushStatusTag(detail.pushStatus)}</Descriptions.Item>
            <Descriptions.Item label="关联 / 推送行">
              <Typography.Text>
                关联 {matchedCount} / {detail.items.length}，已推 {pushedCount} 行
              </Typography.Text>
            </Descriptions.Item>
          </Descriptions>
        )}
      </Card>

      <Card
        title="订单明细"
        extra={
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            勾选未推送的明细行后点击「推送」；已推送行不可勾选
          </Typography.Text>
        }
      >
        <Table<OrderItemDto>
          rowKey="id"
          columns={columns}
          dataSource={detail?.items ?? []}
          loading={loading}
          pagination={false}
          size="small"
          rowSelection={{
            selectedRowKeys: selectedItemIds,
            onChange: (keys) => setSelectedItemIds(keys),
            getCheckboxProps: (record) => ({
              disabled: record.matchStatus !== 'Matched' || record.itemPushStatus === 'Pushed',
            }),
          }}
          components={{ header: { cell: ResizableTitle } }}
          scroll={{ x: columns.reduce((sum, col) => sum + ((col.width as number) ?? 0), 0) }}
        />
      </Card>

      {pushResult.modal}
    </div>
  );
}
