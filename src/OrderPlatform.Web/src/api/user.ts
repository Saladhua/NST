import http from './http';
import type {
  CreateUserPayload,
  PagedResult,
  ResetPasswordPayload,
  UpdateUserPayload,
  UserListDto,
} from '../types';

export const userApi = {
  list: (params: { page?: number; pageSize?: number }) =>
    http.get<PagedResult<UserListDto>>('/user/list', { params }),
  create: (payload: CreateUserPayload) => http.post<null>('/user', payload),
  update: (id: string, payload: UpdateUserPayload) => http.put<null>(`/user/${id}`, payload),
  resetPassword: (id: string, payload: ResetPasswordPayload) =>
    http.put<null>(`/user/${id}/reset-password`, payload),
  remove: (id: string) => http.delete<null>(`/user/${id}`),
};
