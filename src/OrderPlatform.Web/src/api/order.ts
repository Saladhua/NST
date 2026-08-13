import http from './http';
import type {
  OrderDetailDto,
  OrderListDto,
  PagedResult,
  PushStatus,
  MatchStatus,
} from '../types';

export const orderApi = {
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
  detail: (id: string) => http.get<OrderDetailDto>(`/order/detail/${id}`),
  push: (orderId: string) => http.post<{ logId: string }>('/order/push', { orderId }),
};
