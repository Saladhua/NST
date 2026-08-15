// 系统配置接口封装（管理员）。
import http from './http';
import type { ConfigItemDto } from '../types';

export const configApi = {
  // 配置列表
  list: () => http.get<ConfigItemDto[]>('/config'),
  // 更新配置值
  update: (id: string, configValue: string) =>
    http.put<null>(`/config/${id}`, { configValue }),
};