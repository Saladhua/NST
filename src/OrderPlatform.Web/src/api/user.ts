// 用户管理接口封装（管理员）。
import http from './http';
import type {
  CreateUserPayload,
  PagedResult,
  ResetPasswordPayload,
  UpdateUserPayload,
  UserListDto,
} from '../types';

export const userApi = {
  // 用户分页列表
  list: (params: { page?: number; pageSize?: number }) =>
    http.get<PagedResult<UserListDto>>('/user/list', { params }),
  // 新增用户
  create: (payload: CreateUserPayload) => http.post<null>('/user', payload),
  // 更新用户
  update: (id: string, payload: UpdateUserPayload) => http.put<null>(`/user/${id}`, payload),
  // 重置用户密码
  resetPassword: (id: string, payload: ResetPasswordPayload) =>
    http.put<null>(`/user/${id}/reset-password`, payload),
  // 删除用户
  remove: (id: string) => http.delete<null>(`/user/${id}`),
};