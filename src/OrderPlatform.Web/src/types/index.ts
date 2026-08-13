export interface UserInfo {
  id: string;
  userName: string;
  displayName: string;
  phone?: string;
  email?: string;
  role: string;
}

export interface AuthResult {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  userInfo: UserInfo;
}

export interface LoginPayload {
  userName: string;
  password: string;
}

export interface RegisterPayload {
  userName: string;
  password: string;
  displayName: string;
  phone?: string;
  email?: string;
}

export interface ChangePasswordPayload {
  oldPassword: string;
  newPassword: string;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
}

export type MatchStatus = 'Matched' | 'Partial' | 'Unmatched';
export type PushStatus = 'NotPushed' | 'Pushed' | 'Failed';
export type UserRole = 'Admin' | 'Operator';
export type UserStatus = 'Active' | 'Disabled';

export interface OrderListDto {
  id: string;
  orderNo: string;
  customerId: string;
  customerName: string;
  orderDate: string | null;
  totalQuantity: number;
  totalAmount: number;
  parseStatus: MatchStatus;
  pushStatus: PushStatus;
  createdAt: string;
}

export interface OrderItemDto {
  id: string;
  lineNo: number;
  materialCode: string;
  materialName: string;
  spec: string;
  customerPartNo: string;
  nestPartNo: string;
  alloy: string;
  spray: string;
  length: number | null;
  quantity: number;
  unit: string;
  price: number;
  amount: number;
  receiveDate: string | null;
  matchStatus: MatchStatus;
}

export interface OrderDetailDto extends OrderListDto {
  sourceFileId: string | null;
  items: OrderItemDto[];
}

export interface UploadBatchDto {
  batchId: string;
  batchNo: string;
  fileType: string;
  fileName: string;
  customerId: string | null;
  customerName: string | null;
  status: string;
  progress: number;
  errorMessage: string | null;
  parsed: boolean;
  orderCount: number;
  rawData: unknown;
  createdAt: string;
}

export interface CustomerImportDto {
  customerId: string;
  customerName: string;
  partCount: number;
}

export interface ExcelSheetDetailDto {
  sheetName: string;
  headers: string[];
  rows: Record<string, string>[];
}

export interface ExcelBatchDetailDto {
  batchId: string;
  batchNo: string;
  fileName: string;
  sheets: ExcelSheetDetailDto[];
}

export interface OrderGeneratedDto {
  orderId: string;
  orderNo: string;
  customerId: string | null;
  customerName: string | null;
  itemCount: number;
  parseStatus: MatchStatus;
  items: MatchResultItem[];
}

export interface MatchResultItem {
  lineNo: number;
  materialCode: string;
  spec: string;
  quantity: number;
  unit: string;
  customerPartNo: string | null;
  nestPartNo: string | null;
  matchStatus: MatchStatus;
}

export interface UserListDto {
  id: string;
  userName: string;
  displayName: string;
  phone: string | null;
  email: string | null;
  role: UserRole;
  status: UserStatus;
  createdAt: string;
}

export interface CreateUserPayload {
  userName: string;
  password: string;
  displayName: string;
  phone?: string;
  email?: string;
  role: UserRole;
}

export interface UpdateUserPayload {
  displayName: string;
  phone?: string;
  email?: string;
  role: UserRole;
  status: UserStatus;
}

export interface ResetPasswordPayload {
  newPassword: string;
}

export interface ConfigItemDto {
  id: string;
  configKey: string;
  configValue: string;
  description: string | null;
  updatedAt: string | null;
}

export interface DashboardSummaryDto {
  totalOrders: number;
  todayOrders: number;
  totalAmount: number;
  pendingPush: number;
  totalCustomers: number;
  customerOrderStats: CustomerOrderStatDto[];
  recentOrders: RecentOrderDto[];
}

export interface CustomerOrderStatDto {
  customerId: string;
  customerName: string;
  orderCount: number;
  totalAmount: number;
}

export interface RecentOrderDto {
  id: string;
  orderNo: string;
  customerName: string;
  totalAmount: number;
  createdAt: string;
}
