// 数据看板接口封装。
import http from './http';
import type { DashboardSummaryDto } from '../types';

export const dashboardApi = {
  // 看板汇总数据
  summary: () => http.get<DashboardSummaryDto>('/dashboard/summary'),
};