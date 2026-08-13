import http from './http';
import type { ConfigItemDto } from '../types';

export const configApi = {
  list: () => http.get<ConfigItemDto[]>('/config'),
  update: (id: string, configValue: string) =>
    http.put<null>(`/config/${id}`, { configValue }),
};
