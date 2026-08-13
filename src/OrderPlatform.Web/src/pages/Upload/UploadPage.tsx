import { useCallback, useEffect, useRef, useState } from 'react';
import {
  App,
  Alert,
  Button,
  Card,
  Divider,
  Modal,
  Progress,
  Space,
  Table,
  Tag,
  Typography,
  Upload,
} from 'antd';
import { InboxOutlined } from '@ant-design/icons';
import type { UploadFile } from 'antd';
import { uploadApi } from '../../api/upload';
import type {
  CustomerImportDto,
  ExcelBatchDetailDto,
  OrderGeneratedDto,
  UploadBatchDto,
} from '../../types';

const { Dragger } = Upload;

const POLL_INTERVAL = 1500;
const PAGE_SIZE = 10;

export default function UploadPage() {
  const { message } = App.useApp();
  const [uploading, setUploading] = useState(false);
  const [uploadPercent, setUploadPercent] = useState(0);
  const [batches, setBatches] = useState<UploadBatchDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [orders, setOrders] = useState<OrderGeneratedDto[]>([]);
  const [customers, setCustomers] = useState<CustomerImportDto[]>([]);
  const [excelDetail, setExcelDetail] = useState<ExcelBatchDetailDto | null>(null);
  const [excelLoading, setExcelLoading] = useState(false);
  const pollTimerRef = useRef<number | null>(null);

  const refreshCustomers = useCallback(async () => {
    try {
      setCustomers(await uploadApi.customers());
    } catch {
      // 失败静默，不阻塞主流程
    }
  }, []);

  const loadBatches = useCallback(async (pageNum: number) => {
    try {
      const result = await uploadApi.batches(pageNum, PAGE_SIZE);
      setBatches(result.items);
      setTotal(result.total);
      setPage(pageNum);
    } catch {
      // 失败静默，不阻塞主流程
    }
  }, []);

  const stopPolling = () => {
    if (pollTimerRef.current !== null) {
      window.clearInterval(pollTimerRef.current);
      pollTimerRef.current = null;
    }
  };

  const loadBatchOrders = async (batchId: string) => {
    try {
      const result = await uploadApi.batchOrders(batchId);
      setOrders(result);
    } catch {
      setOrders([]);
    }
  };

  const allFinished = (list: UploadBatchDto[]) =>
    list.every((b) => b.status === 'Completed' || b.status === 'Failed');

  const refreshBatches = useCallback(async (ids: string[]) => {
    const updated: UploadBatchDto[] = [];
    for (const id of ids) {
      try {
        updated.push(await uploadApi.batch(id));
      } catch {
        // 单个批次查询失败跳过
      }
    }
    setBatches(updated);
    return updated;
  }, []);

  const handleUpload = async (fileList: UploadFile[]) => {
    const files = fileList
      .map((item) => item.originFileObj)
      .filter((f): f is NonNullable<typeof f> => Boolean(f))
      .map((f) => f as File);
    if (files.length === 0) {
      return;
    }

    setUploading(true);
    setUploadPercent(0);
    try {
      const result = await uploadApi.upload(files, ({ percent }) => setUploadPercent(percent));
      if (result.length === 0) {
        message.success('上传成功');
        void refreshCustomers();
        void loadBatches(1);
        return;
      }

      const ids = result.map((b) => b.batchId);
      void loadBatches(1);
      if (allFinished(result)) {
        await refreshBatches(ids);
        message.success('上传解析完成');
        void refreshCustomers();
        if (result.length > 0) {
          await loadBatchOrders(result[0].batchId);
        }
        return;
      }

      message.info('上传成功，正在后台解析...');
      stopPolling();
      pollTimerRef.current = window.setInterval(async () => {
        const updated = await refreshBatches(ids);
        if (allFinished(updated)) {
          stopPolling();
          const failed = updated.filter((b) => b.status === 'Failed');
          message.success(failed.length === 0 ? '上传解析完成' : `解析完成，${failed.length} 个文件失败`);
          void refreshCustomers();
          void loadBatches(1);
          if (updated.length > 0) {
            await loadBatchOrders(updated[0].batchId);
          }
        }
      }, POLL_INTERVAL);
    } catch {
      message.error('上传失败，请检查文件格式或是否重复上传');
    } finally {
      setUploading(false);
    }
  };

  const showExcelDetail = async (batchId: string) => {
    setExcelLoading(true);
    try {
      const detail = await uploadApi.batchExcel(batchId);
      setExcelDetail(detail);
    } catch {
      message.error('加载 Excel 数据失败');
    } finally {
      setExcelLoading(false);
    }
  };

  useEffect(() => {
    void loadBatches(1);
    void refreshCustomers();
  }, [loadBatches, refreshCustomers]);

  useEffect(() => stopPolling, []);

  const batchColumns = [
    { title: '批次号', dataIndex: 'batchNo', key: 'batchNo', width: 220 },
    { title: '文件名', dataIndex: 'fileName', key: 'fileName' },
    { title: '类型', dataIndex: 'fileType', key: 'fileType', width: 80 },
    {
      title: '客户',
      dataIndex: 'customerName',
      key: 'customerName',
      width: 120,
      render: (value: string | null) => value ?? '-',
    },
    {
      title: '状态',
      dataIndex: 'status',
      key: 'status',
      width: 100,
      render: (value: string) => (
        <Tag color={value === 'Completed' ? 'success' : value === 'Failed' ? 'error' : 'processing'}>
          {value === 'Completed' ? '完成' : value === 'Failed' ? '失败' : '解析中'}
        </Tag>
      ),
    },
    {
      title: '进度',
      dataIndex: 'progress',
      key: 'progress',
      width: 160,
      render: (value: number, record: UploadBatchDto) => (
        <Space direction="vertical" size={0} style={{ width: '100%' }}>
          <Progress
            percent={value}
            size="small"
            status={record.status === 'Failed' ? 'exception' : record.status === 'Completed' ? 'success' : 'active'}
          />
          {record.errorMessage && (
            <Typography.Text type="danger" style={{ fontSize: 12 }}>
              {record.errorMessage}
            </Typography.Text>
          )}
        </Space>
      ),
    },
    {
      title: '订单数',
      dataIndex: 'orderCount',
      key: 'orderCount',
      width: 80,
    },
    {
      title: '上传时间',
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 160,
      render: (value: string) => new Date(value).toLocaleString('zh-CN'),
    },
    {
      title: '操作',
      key: 'action',
      width: 180,
      render: (_: unknown, record: UploadBatchDto) => (
        <Space size={4}>
          {record.fileType === 'Excel' && record.status === 'Completed' && (
            <Button size="small" type="link" onClick={() => void showExcelDetail(record.batchId)}>
              查看数据
            </Button>
          )}
          <Typography.Link
            disabled={record.status !== 'Completed'}
            onClick={() => void loadBatchOrders(record.batchId)}
          >
            查看明细
          </Typography.Link>
        </Space>
      ),
    },
  ];

  const matchStatusTag = (status: string) => {
    const map: Record<string, { color: string; text: string }> = {
      Matched: { color: 'success', text: '已关联' },
      Manual: { color: 'warning', text: '待人工' },
      Unmatched: { color: 'error', text: '未关联' },
    };
    const item = map[status] ?? { color: 'default', text: status };
    return <Tag color={item.color}>{item.text}</Tag>;
  };

  const orderColumns = [
    { title: '订单号', dataIndex: 'orderNo', key: 'orderNo', width: 220 },
    { title: '客户', dataIndex: 'customerName', key: 'customerName', width: 140 },
    { title: '明细行数', dataIndex: 'itemCount', key: 'itemCount', width: 100 },
    {
      title: '关联状态',
      dataIndex: 'parseStatus',
      key: 'parseStatus',
      width: 100,
      render: (value: string) => matchStatusTag(value),
    },
    {
      title: '关联明细',
      key: 'items',
      render: (_: unknown, record: OrderGeneratedDto) => (
        <div className="max-h-64 overflow-auto">
          <table className="w-full text-xs">
            <thead>
              <tr className="border-b">
                <th className="py-1 pr-2 text-left">行</th>
                <th className="py-1 pr-2 text-left">编码</th>
                <th className="py-1 pr-2 text-left">规格</th>
                <th className="py-1 pr-2 text-left">数量</th>
                <th className="py-1 pr-2 text-left">客户图号</th>
                <th className="py-1 pr-2 text-left">套图图号</th>
                <th className="py-1 text-left">状态</th>
              </tr>
            </thead>
            <tbody>
              {record.items.map((item) => (
                <tr key={item.lineNo} className="border-b">
                  <td className="py-1 pr-2">{item.lineNo}</td>
                  <td className="py-1 pr-2">{item.materialCode || '-'}</td>
                  <td className="py-1 pr-2">{item.spec || '-'}</td>
                  <td className="py-1 pr-2">{item.quantity}</td>
                  <td className="py-1 pr-2">{item.customerPartNo || '-'}</td>
                  <td className="py-1 pr-2">{item.nestPartNo || '-'}</td>
                  <td className="py-1">{matchStatusTag(item.matchStatus)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ),
    },
  ];

  const customerColumns = [
    { title: '客户名称', dataIndex: 'customerName', key: 'customerName' },
    {
      title: '图号数量',
      dataIndex: 'partCount',
      key: 'partCount',
      width: 120,
    },
  ];

  return (
    <div className="space-y-6">
      <Card title="上传订单文件">
        <Alert
          type="info"
          showIcon
          className="mb-4"
          message="每次只能上传一种类型：PDF 订单 或 Excel 客户资料（含图号），PDF 和 Excel 不能同时上传。PDF 上传后将自动识别客户并解析订单明细。同名文件不可重复上传，重复订单号将自动跳过。"
        />
        <Dragger
          multiple
          accept=".pdf,.xlsx,.xls"
          beforeUpload={() => false}
          disabled={uploading}
          onChange={({ fileList }) => {
            if (!uploading && fileList.length > 0) {
              void handleUpload(fileList);
            }
          }}
          maxCount={5}
        >
          <p className="ant-upload-drag-icon">
            <InboxOutlined />
          </p>
          <p className="ant-upload-text">点击或拖拽文件到此区域上传</p>
          <p className="ant-upload-hint">支持 PDF / Excel，单文件不超过 50MB，一次只能选择一种类型</p>
        </Dragger>
        {uploading && (
          <Progress
            className="mt-4"
            percent={uploadPercent}
            status="active"
            format={(p) => (p === 100 ? '等待服务器处理...' : `上传中 ${p}%`)}
          />
        )}
      </Card>

      {batches.length > 0 && (
        <Card title="上传记录">
          <Table<UploadBatchDto>
            rowKey="batchId"
            columns={batchColumns}
            dataSource={batches}
            size="small"
            pagination={{
              current: page,
              pageSize: PAGE_SIZE,
              total,
              showSizeChanger: false,
              onChange: (p) => void loadBatches(p),
            }}
          />
        </Card>
      )}

      {orders.length > 0 && (
        <Card title="生成订单">
          <Table<OrderGeneratedDto>
            rowKey="orderId"
            columns={orderColumns}
            dataSource={orders}
            pagination={false}
            size="small"
          />
        </Card>
      )}

      <Divider />

      <Modal
        title={excelDetail ? `Excel 数据：${excelDetail.fileName}` : 'Excel 数据'}
        open={excelDetail !== null}
        loading={excelLoading}
        footer={null}
        width={900}
        onCancel={() => setExcelDetail(null)}
      >
        {excelDetail?.sheets.map((sheet) => (
          <div key={sheet.sheetName} className="mb-4">
            <Typography.Title level={5} className="mb-2">
              {sheet.sheetName}
            </Typography.Title>
            <Table
              rowKey={(_, index) => index ?? 0}
              size="small"
              dataSource={sheet.rows}
              pagination={{ pageSize: 10, showSizeChanger: false }}
              columns={sheet.headers.map((header) => ({
                title: header,
                dataIndex: header,
                key: header,
                ellipsis: true,
              }))}
            />
          </div>
        ))}
      </Modal>

      <Card title="已导入客户资料" extra={<Typography.Text type="secondary">供 PDF 自动关联图号</Typography.Text>}>
        <Table<CustomerImportDto>
          rowKey="customerId"
          columns={customerColumns}
          dataSource={customers}
          pagination={false}
          size="small"
          locale={{ emptyText: '暂无客户资料，请先上传 Excel' }}
        />
      </Card>
    </div>
  );
}