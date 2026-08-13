import { useEffect, useState } from 'react';
import {
  App,
  Card,
  Col,
  Empty,
  Row,
  Statistic,
  Table,
  Tag,
  Typography,
} from 'antd';
import {
  FileTextOutlined,
  CheckCircleOutlined,
  RiseOutlined,
  TeamOutlined,
} from '@ant-design/icons';
import { useNavigate } from 'react-router';
import dayjs from 'dayjs';
import { dashboardApi } from '../../api/dashboard';
import type { CustomerOrderStatDto, DashboardSummaryDto, RecentOrderDto } from '../../types';

export default function DashboardPage() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const [data, setData] = useState<DashboardSummaryDto | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const load = async () => {
      setLoading(true);
      try {
        const result = await dashboardApi.summary();
        setData(result);
      } catch {
        message.error('加载统计信息失败');
      } finally {
        setLoading(false);
      }
    };
    void load();
  }, [message]);

  const customerColumns = [
    { title: '客户', dataIndex: 'customerName', key: 'customerName' },
    { title: '订单数', dataIndex: 'orderCount', key: 'orderCount', width: 100 },
    {
      title: '金额（元）',
      dataIndex: 'totalAmount',
      key: 'totalAmount',
      width: 150,
      render: (value: number) => value.toLocaleString('zh-CN'),
    },
  ];

  const recentColumns = [
    {
      title: '订单号',
      dataIndex: 'orderNo',
      key: 'orderNo',
      render: (value: string, record: RecentOrderDto) => (
        <Typography.Link onClick={() => navigate(`/orders/${record.id}`)}>{value}</Typography.Link>
      ),
    },
    { title: '客户', dataIndex: 'customerName', key: 'customerName' },
    {
      title: '金额（元）',
      dataIndex: 'totalAmount',
      key: 'totalAmount',
      width: 150,
      render: (value: number) => value.toLocaleString('zh-CN'),
    },
    {
      title: '时间',
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 160,
      render: (value: string) => dayjs(value).format('YYYY-MM-DD HH:mm'),
    },
  ];

  return (
    <div className="space-y-6">
      <Row gutter={[16, 16]}>
        <Col xs={12} md={6}>
          <Card loading={loading}>
            <Statistic
              title="订单总数"
              value={data?.totalOrders ?? 0}
              prefix={<FileTextOutlined />}
            />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card loading={loading}>
            <Statistic title="今日订单" value={data?.todayOrders ?? 0} prefix={<RiseOutlined />} />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card loading={loading}>
            <Statistic
              title="待推送订单"
              value={data?.pendingPush ?? 0}
              prefix={<CheckCircleOutlined />}
            />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card loading={loading}>
            <Statistic title="合作客户" value={data?.totalCustomers ?? 0} prefix={<TeamOutlined />} />
          </Card>
        </Col>
      </Row>

      <Row gutter={[16, 16]}>
        <Col xs={24} lg={10}>
          <Card title="客户订单统计" loading={loading}>
            {data && data.customerOrderStats.length > 0 ? (
              <Table<CustomerOrderStatDto>
                rowKey="customerId"
                columns={customerColumns}
                dataSource={data.customerOrderStats}
                pagination={false}
                size="small"
              />
            ) : (
              <Empty description="暂无订单" />
            )}
          </Card>
        </Col>
        <Col xs={24} lg={14}>
          <Card title="最近订单" loading={loading}>
            {data && data.recentOrders.length > 0 ? (
              <Table<RecentOrderDto>
                rowKey="id"
                columns={recentColumns}
                dataSource={data.recentOrders}
                pagination={false}
                size="small"
              />
            ) : (
              <Empty description="暂无订单" />
            )}
          </Card>
        </Col>
      </Row>

      <Card loading={loading}>
        <Statistic
          title="订单总金额（元）"
          value={data?.totalAmount ?? 0}
          precision={2}
          valueStyle={{ color: '#cf1322' }}
          prefix={<Tag color="red">¥</Tag>}
        />
      </Card>
    </div>
  );
}
