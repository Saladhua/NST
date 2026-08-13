import http from './http';
import type {
  CustomerImportDto,
  OrderGeneratedDto,
  UploadBatchDto,
} from '../types';

export const uploadApi = {
  upload: (files: File[]) => {
    const formData = new FormData();
    files.forEach((file) => formData.append('files', file));
    return http.post<UploadBatchDto[]>('/upload', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 60000,
    });
  },
  batch: (batchId: string) => http.get<UploadBatchDto>(`/upload/batch/${batchId}`),
  customers: () => http.get<CustomerImportDto[]>('/upload/customers'),
  batchOrders: (batchId: string) =>
    http.get<OrderGeneratedDto[]>(`/upload/batch/${batchId}/orders`),
};
