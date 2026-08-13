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
  batch: (batchId: string) => http.get<UploadBatchDto>(`/upload/batch/${batchId}`),
  batches: (page: number, pageSize: number) =>
    http.get<PagedResult<UploadBatchDto>>('/upload/batches', { params: { page, pageSize } }),
  batchExcel: (batchId: string) => http.get<ExcelBatchDetailDto>(`/upload/batch/${batchId}/excel`),
  customers: () => http.get<CustomerImportDto[]>('/upload/customers'),
  batchOrders: (batchId: string) =>
    http.get<OrderGeneratedDto[]>(`/upload/batch/${batchId}/orders`),
};