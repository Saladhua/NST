import http from './http';
import type { DashboardSummaryDto } from '../types';

export const dashboardApi = {
  summary: () => http.get<DashboardSummaryDto>('/dashboard/summary'),
};
