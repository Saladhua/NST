// 订单接口封装（列表、详情、推送、删除）。
import http from './http';
import type {
  OrderDetailDto,
  OrderListDto,
  PagedResult,
  PushStatus,
  MatchStatus,
} from '../types';

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
  // 推送订单
  push: (orderId: string) => http.post<{ logId: string }>('/order/push', { orderId }),
  // 删除订单（管理员）
  delete: (id: string) => http.delete<boolean>(`/order/${id}`),
};