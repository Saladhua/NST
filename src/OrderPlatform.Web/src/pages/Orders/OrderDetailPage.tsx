import { useCallback, useEffect, useState } from 'react';
import {
  App,
  Button,
  Card,
  Descriptions,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import { ArrowLeftOutlined, PushpinOutlined, ReloadOutlined } from '@ant-design/icons';
import { useNavigate, useParams } from 'react-router';
import { orderApi } from '../../api/order';
import type { MatchStatus, OrderDetailDto, OrderItemDto } from '../../types';

function matchStatusTag(status: MatchStatus) {
  const map: Record<string, { color: string; text: string }> = {
    Matched: { color: 'success', text: '已关联' },
    Partial: { color: 'warning', text: '部分关联' },
    Unmatched: { color: 'error', text: '未关联' },
  };
  const item = map[status] ?? { color: 'default', text: status };
  return <Tag color={item.color}>{item.text}</Tag>;
}

export default function OrderDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { message } = App.useApp();
  const navigate = useNavigate();

  const [detail, setDetail] = useState<OrderDetailDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [pushing, setPushing] = useState(false);

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

  const pushOrder = async () => {
    if (!id) {
      return;
    }
    setPushing(true);
    try {
      await orderApi.push(id);
      message.success('推送成功');
      await load();
    } catch {
      message.error('推送失败，请重试');
    } finally {
      setPushing(false);
    }
  };

  const columns = [
    { title: '行号', dataIndex: 'lineNo', key: 'lineNo', width: 60 },
    { title: '物料编码', dataIndex: 'materialCode', key: 'materialCode', width: 180 },
    { title: '规格', dataIndex: 'spec', key: 'spec', width: 180 },
    { title: '客户图号', dataIndex: 'customerPartNo', key: 'customerPartNo', width: 140 },
    { title: '套图图号', dataIndex: 'nestPartNo', key: 'nestPartNo', width: 140 },
    {
      title: '数量',
      dataIndex: 'quantity',
      key: 'quantity',
      width: 110,
      render: (value: number) => value.toLocaleString('zh-CN'),
    },
    {
      title: '单价（元）',
      dataIndex: 'price',
      key: 'price',
      width: 100,
      render: (value: number) => value.toFixed(2),
    },
    {
      title: '金额（元）',
      dataIndex: 'amount',
      key: 'amount',
      width: 120,
      render: (value: number) => value.toLocaleString('zh-CN', { minimumFractionDigits: 2 }),
    },
    {
      title: '关联状态',
      dataIndex: 'matchStatus',
      key: 'matchStatus',
      width: 100,
      render: (value: MatchStatus) => matchStatusTag(value),
    },
  ];

  const matchedCount = detail?.items.filter((i) => i.matchStatus === 'Matched').length ?? 0;

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
          <Button
            type="primary"
            icon={<PushpinOutlined />}
            disabled={detail?.pushStatus === 'Pushed'}
            loading={pushing}
            onClick={() => void pushOrder()}
          >
            {detail?.pushStatus === 'Pushed' ? '已推送' : '推送'}
          </Button>
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
            <Descriptions.Item label="推送状态">
              <Tag color={detail.pushStatus === 'Pushed' ? 'success' : 'default'}>
                {detail.pushStatus === 'Pushed' ? '已推送' : detail.pushStatus === 'Failed' ? '推送失败' : '未推送'}
              </Tag>
            </Descriptions.Item>
            <Descriptions.Item label="关联行占比">
              <Typography.Text>
                {matchedCount} / {detail.items.length}
              </Typography.Text>
            </Descriptions.Item>
          </Descriptions>
        )}
      </Card>

      <Card title="订单明细">
        <Table<OrderItemDto>
          rowKey="id"
          columns={columns}
          dataSource={detail?.items ?? []}
          loading={loading}
          pagination={false}
          size="small"
          scroll={{ x: 1000 }}
        />
      </Card>
    </div>
  );
}