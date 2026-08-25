// 订单接口封装（列表、详情、推送、物料同步、删除）。
import http from './http';
import type {
  OrderDetailDto,
  OrderListDto,
  PagedResult,
  PushResult,
  PushStatus,
  MatchStatus,
} from '../types';

/** 物料同步结果。 */
export interface MaterialSyncResult {
  orderId: string;
  total: number;
  synced: number;
  notFound: number;
  failed: number;
  errorMessage: string | null;
}

export const orderApi = {
  // 订单分页列表（支持关键词/客户/推送状态/关联状态筛选）
  list: (params: {
    page?: number;
    pageSize?: number;
    keyword?: string;
    customerId?: string;
    pushStatus?: PushStatus;
    parseStatus?: MatchStatus;
  }) =>
    http.get<PagedResult<OrderListDto>>('/order/list', {
      params,
    }),
  // 订单详情（含明细）
  detail: (id: string) => http.get<OrderDetailDto>(`/order/detail/${id}`),
  // 推送订单（行级：只推未推送且已匹配的行；ERP 调用多、耗时长，单独放宽超时到 120 秒）
  push: (orderId: string) =>
    http.post<PushResult>('/order/push', { orderId }, { timeout: 120000 }),
  // 物料同步（itemId 为空时同步整单已匹配行）
  syncMaterial: (orderId: string, itemId?: string) =>
    http.post<MaterialSyncResult>('/order/sync-material', { orderId, itemId }),
  // 删除订单（管理员）
  delete: (id: string) => http.delete<boolean>(`/order/${id}`),
};