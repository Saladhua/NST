// 认证相关接口封装。
import http from './http';
import type {
  AuthResult,
  ChangePasswordPayload,
  LoginPayload,
  RegisterPayload,
} from '../types';

export const authApi = {
  // 登录，返回访问令牌 + 刷新令牌 + 用户信息
  login: (payload: LoginPayload) => http.post<AuthResult>('/auth/login', payload),
  // 注册普通用户
  register: (payload: RegisterPayload) => http.post<null>('/auth/register', payload),
  // 修改当前用户密码
  changePassword: (payload: ChangePasswordPayload) =>
    http.post<null>('/auth/change-password', payload),
};