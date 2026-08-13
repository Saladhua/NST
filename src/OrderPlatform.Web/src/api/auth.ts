import http from './http';
import type {
  AuthResult,
  ChangePasswordPayload,
  LoginPayload,
  RegisterPayload,
} from '../types';

export const authApi = {
  login: (payload: LoginPayload) => http.post<AuthResult>('/auth/login', payload),
  register: (payload: RegisterPayload) => http.post<null>('/auth/register', payload),
  changePassword: (payload: ChangePasswordPayload) =>
    http.post<null>('/auth/change-password', payload),
};
