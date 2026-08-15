// 上传相关接口封装（上传文件、批次查询、客户统计、批次明细）。
import http from './http';
import type {
  CustomerImportDto,
  ExcelBatchDetailDto,
  OrderGeneratedDto,
  PagedResult,
  UploadBatchDto,
} from '../types';

export interface UploadProgress {
  percent: number;
}

export const uploadApi = {
  // 上传文件（PDF 订单 / Excel 客户资料），支持上传进度回调
  upload: (
    files: File[],
    onProgress?: (progress: UploadProgress) => void,
  ) => {
    const formData = new FormData();
    files.forEach((file) => formData.append('files', file));
    return http.post<UploadBatchDto[]>('/upload', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 120000,
      onUploadProgress: (event) => {
        if (onProgress && event.total) {
          onProgress({ percent: Math.round((event.loaded / event.total) * 100) });
        }
      },
    });
  },
  // 查询单批次解析状态（用于轮询进度）
  batch: (batchId: string) => http.get<UploadBatchDto>(`/upload/batch/${batchId}`),
  // 分页查询历史上传记录
  batches: (page: number, pageSize: number) =>
    http.get<PagedResult<UploadBatchDto>>('/upload/batches', { params: { page, pageSize } }),
  // 查看 Excel 批次解析明细
  batchExcel: (batchId: string) => http.get<ExcelBatchDetailDto>(`/upload/batch/${batchId}/excel`),
  // 客户图号统计列表
  customers: () => http.get<CustomerImportDto[]>('/upload/customers'),
  // 查看 PDF 批次生成的订单
  batchOrders: (batchId: string) =>
    http.get<OrderGeneratedDto[]>(`/upload/batch/${batchId}/orders`),
};