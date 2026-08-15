// 前端类型定义：与后端 DTO 一一对应。

/** 当前登录用户信息。 */
export interface UserInfo {
  id: string;
  userName: string;
  displayName: string;
  phone?: string;
  email?: string;
  role: string;
}

/** 登录/刷新成功后的返回结果。 */
export interface AuthResult {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  userInfo: UserInfo;
}

/** 登录请求参数。 */
export interface LoginPayload {
  userName: string;
  password: string;
}

/** 注册请求参数。 */
export interface RegisterPayload {
  userName: string;
  password: string;
  displayName: string;
  phone?: string;
  email?: string;
}

/** 修改密码请求参数。 */
export interface ChangePasswordPayload {
  oldPassword: string;
  newPassword: string;
}

/** 分页结果通用包装。 */
export interface PagedResult<T> {
  items: T[];
  total: number;
}

/** 关联状态：已关联 / 部分关联 / 未关联。 */
export type MatchStatus = 'Matched' | 'Partial' | 'Unmatched';
/** 推送状态：未推送 / 已推送 / 推送失败。 */
export type PushStatus = 'NotPushed' | 'Pushed' | 'Failed';
/** 用户角色（注意：后端实际为 Admin / User，此处 Operator 用于界面显示操作员）。 */
export type UserRole = 'Admin' | 'Operator';
/** 用户状态：启用 / 禁用。 */
export type UserStatus = 'Active' | 'Disabled';

/** 订单列表项。 */
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

/** 订单明细行。 */
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

/** 订单详情（列表项 + 来源批次 + 明细）。 */
export interface OrderDetailDto extends OrderListDto {
  sourceFileId: string | null;
  items: OrderItemDto[];
}

/** 上传批次 DTO。 */
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
  orderDeleted: boolean;
  rawData: unknown;
  createdAt: string;
}

/** 客户图号统计项。 */
export interface CustomerImportDto {
  customerId: string;
  customerName: string;
  partCount: number;
}

/** Excel 批次单个 sheet 的解析数据。 */
export interface ExcelSheetDetailDto {
  sheetName: string;
  headers: string[];
  rows: Record<string, string>[];
}

/** Excel 批次解析明细。 */
export interface ExcelBatchDetailDto {
  batchId: string;
  batchNo: string;
  fileName: string;
  sheets: ExcelSheetDetailDto[];
}

/** PDF 批次生成的订单。 */
export interface OrderGeneratedDto {
  orderId: string;
  orderNo: string;
  customerId: string | null;
  customerName: string | null;
  itemCount: number;
  parseStatus: MatchStatus;
  items: MatchResultItem[];
}

/** PDF 明细行的匹配结果。 */
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

/** 用户列表项。 */
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

/** 新增用户请求参数。 */
export interface CreateUserPayload {
  userName: string;
  password: string;
  displayName: string;
  phone?: string;
  email?: string;
  role: UserRole;
}

/** 更新用户请求参数。 */
export interface UpdateUserPayload {
  displayName: string;
  phone?: string;
  email?: string;
  role: UserRole;
  status: UserStatus;
}

/** 重置密码请求参数。 */
export interface ResetPasswordPayload {
  newPassword: string;
}

/** 系统配置项。 */
export interface ConfigItemDto {
  id: string;
  configKey: string;
  configValue: string;
  description: string | null;
  updatedAt: string | null;
}

/** 看板汇总数据。 */
export interface DashboardSummaryDto {
  totalOrders: number;
  todayOrders: number;
  totalAmount: number;
  pendingPush: number;
  totalCustomers: number;
  customerOrderStats: CustomerOrderStatDto[];
  recentOrders: RecentOrderDto[];
}

/** 客户订单统计项。 */
export interface CustomerOrderStatDto {
  customerId: string;
  customerName: string;
  orderCount: number;
  totalAmount: number;
}

/** 最近订单项。 */
export interface RecentOrderDto {
  id: string;
  orderNo: string;
  customerName: string;
  totalAmount: number;
  createdAt: string;
}