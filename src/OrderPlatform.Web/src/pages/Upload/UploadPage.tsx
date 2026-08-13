import { useCallback, useEffect, useRef, useState } from 'react';
import {
  App,
  Alert,
  Button,
  Card,
  Descriptions,
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
  const [fileList, setFileList] = useState<UploadFile[]>([]);
  const [batches, setBatches] = useState<UploadBatchDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [orders, setOrders] = useState<OrderGeneratedDto[] | null>(null);
  const [orderLoading, setOrderLoading] = useState(false);
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
    setOrderLoading(true);
    try {
      setOrders(await uploadApi.batchOrders(batchId));
    } catch {
      message.error('加载订单明细失败');
      setOrders(null);
    } finally {
      setOrderLoading(false);
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
        }
      }, POLL_INTERVAL);
    } catch {
      message.error('上传失败，请检查文件格式或是否重复上传');
    } finally {
      setUploading(false);
      setFileList([]);
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
    {
      title: '文件名',
      dataIndex: 'fileName',
      key: 'fileName',
      render: (value: string, record: UploadBatchDto) => (
        <Space size={4}>
          <span>{value}</span>
          {record.orderDeleted && <Tag color="red">订单已删除</Tag>}
        </Space>
      ),
    },
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
      title: '上传时间',
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 160,
      render: (value: string) => new Date(value).toLocaleString('zh-CN'),
    },
    {
      title: '操作',
      key: 'action',
      width: 220,
      render: (_: unknown, record: UploadBatchDto) => (
        <Space size={4}>
          {record.fileType === 'Excel' && record.status === 'Completed' && (
            <Button size="small" type="link" onClick={() => void showExcelDetail(record.batchId)}>
              查看数据
            </Button>
          )}
          {record.fileType === 'PDF' && record.status === 'Completed' && (
            <Button size="small" type="link" onClick={() => void loadBatchOrders(record.batchId)}>
              查看明细
            </Button>
          )}
        </Space>
      ),
    },
  ];

  const matchStatusTag = (status: string) => {
    const map: Record<string, { color: string; text: string }> = {
      Matched: { color: 'success', text: '已关联' },
      Partial: { color: 'warning', text: '部分关联' },
      Unmatched: { color: 'error', text: '未关联' },
    };
    const item = map[status] ?? { color: 'default', text: status };
    return <Tag color={item.color}>{item.text}</Tag>;
  };

  const itemColumns = [
    { title: '行', dataIndex: 'lineNo', key: 'lineNo', width: 60 },
    { title: '编码', dataIndex: 'materialCode', key: 'materialCode', render: (v: string) => v || '-' },
    { title: '规格', dataIndex: 'spec', key: 'spec', render: (v: string) => v || '-' },
    { title: '数量', dataIndex: 'quantity', key: 'quantity', width: 100 },
    {
      title: '客户图号',
      dataIndex: 'customerPartNo',
      key: 'customerPartNo',
      render: (v: string | null) => v || '-',
    },
    {
      title: '套图图号',
      dataIndex: 'nestPartNo',
      key: 'nestPartNo',
      render: (v: string | null) => v || '-',
    },
    {
      title: '状态',
      dataIndex: 'matchStatus',
      key: 'matchStatus',
      width: 100,
      render: (v: string) => matchStatusTag(v),
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
          fileList={fileList}
          onChange={({ fileList: newList }) => {
            setFileList(newList);
            if (!uploading && newList.length > 0) {
              void handleUpload(newList);
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

      <Modal
        title="订单明细"
        open={orders !== null}
        loading={orderLoading}
        footer={null}
        width={1100}
        onCancel={() => setOrders(null)}
        styles={{ body: { maxHeight: '70vh', overflow: 'auto' } }}
      >
        {orders && orders.length > 0 ? (
          <div className="space-y-4">
            {orders.map((order) => (
              <Card key={order.orderId} size="small" title={`订单：${order.orderNo}`}>
                <Descriptions
                  size="small"
                  column={4}
                  className="mb-4"
                  items={[
                    { key: 'orderNo', label: '订单号', children: order.orderNo },
                    { key: 'customerName', label: '客户', children: order.customerName || '-' },
                    { key: 'itemCount', label: '明细行数', children: order.itemCount },
                    { key: 'parseStatus', label: '关联状态', children: matchStatusTag(order.parseStatus) },
                  ]}
                />
                <Table
                  rowKey="lineNo"
                  columns={itemColumns}
                  dataSource={order.items}
                  pagination={false}
                  size="small"
                  scroll={{ x: 'max-content' }}
                />
              </Card>
            ))}
          </div>
        ) : (
          <Typography.Text type="secondary">该批次未查询到关联订单（历史批次可能未建立批次关联）。</Typography.Text>
        )}
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