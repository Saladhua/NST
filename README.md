# 耐世拓订单平台

订单文件自动解析与关联管理平台。上传 PDF 采购订单与 Excel 客户资料，自动识别客户、解析订单明细，并依据客户图号库自动关联 NEST 套图图号。

## 技术栈

| 端 | 技术 |
|----|------|
| 后端 | ASP.NET Core 8 Web API · EF Core · SQL Server 2022 · JWT · BCrypt |
| 前端 | React 19 · TypeScript · antd 5 · Vite 6 · Tailwind CSS |

## 项目结构

```
OrderPlatform.sln
└── src/
    ├── OrderPlatform.Api            Web API 入口（Controller、后台解析服务、统一返回过滤器）
    ├── OrderPlatform.Application    Service、DTO、FluentValidation 校验、AutoMapper、PDF/Excel 解析、图号匹配
    ├── OrderPlatform.Domain         实体、角色/状态枚举、仓储接口
    ├── OrderPlatform.Infrastructure EF Core DbContext、JWT 签发、BCrypt 哈希、仓储实现、种子数据
    ├── OrderPlatform.Shared         ApiResponse<T> 统一返回格式
    └── OrderPlatform.Web            React + antd 前端
```

## 功能模块

- **上传解析**：支持 PDF 订单、Excel 客户资料（含图号）；重复订单号自动跳过，同名文件可重复上传；异步后台解析，实时进度轮询
- **图号自动关联**：PDF 明细按「客户图号 / 客户新图号」精确匹配，未命中时按「外径×壁厚」规格前缀唯一匹配
- **订单管理**：列表筛选（关键词 / 客户 / 关联状态 / 推送状态）、订单详情、手动推送、删除订单
- **客户图号统计**：按客户展示订单明细中已匹配的去重图号数量
- **用户管理**：用户增删改、重置密码（仅管理员）
- **数据看板**：订单汇总、客户订单统计、近期订单
- **系统配置**：配置项管理（仅管理员）

## 删除与联动

- **上传记录**（上传订单文件）：仅作历史审计，不提供删除入口；若该批次生成的订单已被删除，文件名旁以红色 **「订单已删除」** 标记
- **订单删除**：仅管理员可删，**已推送的订单不可删除**；删除时同步清理订单明细与推送日志
- **图号数量**：以「订单明细中已匹配的去重图号数」为口径统计，删除订单后随之减少，删光即归零
- **重新上传**：删除订单后，同名文件可重新上传并正常生成订单；订单未删除时重传同名文件会提示「订单已存在，已跳过重复导入」，不产生重复订单

## 关联状态说明

订单及明细的关联状态分为三档：

| 状态 | 含义 |
|------|------|
| 已关联 Matched | 全部明细均匹配到图号 |
| 部分关联 Partial | 部分明细匹配、部分未匹配，需人工确认 |
| 未关联 Unmatched | 无任何明细匹配到图号 |

## 启动

```powershell
# 后端 API（端口 5080）
.\start-api.ps1

# 前端 Web（端口 5173）
.\start-web.ps1
```

- 前端：http://localhost:5173
- Swagger：http://localhost:5080/swagger
- 首次启动自动创建数据库 `OrderPlatform` 并写入种子管理员 `admin / 123456`

## 接口

| 模块 | 接口 | 说明 |
|------|------|------|
| 认证 | POST /api/auth/login | 登录，返回 AccessToken（30 分钟）+ RefreshToken（7 天） |
| 认证 | POST /api/auth/refresh | 刷新令牌 |
| 认证 | POST /api/auth/register | 注册（普通用户） |
| 认证 | POST /api/auth/change-password | 修改密码（需登录） |
| 上传 | POST /api/upload | 上传文件，创建解析批次（异步解析） |
| 上传 | GET /api/upload/batches | 分页查询历史上传记录 |
| 上传 | GET /api/upload/batch/{id} | 查询批次解析状态、进度及订单删除标记 |
| 上传 | GET /api/upload/batch/{id}/excel | 查看 Excel 批次解析明细 |
| 上传 | GET /api/upload/batch/{id}/orders | 查看 PDF 批次生成的订单 |
| 上传 | GET /api/upload/customers | 客户图号统计列表（客户 + 订单明细匹配去重图号数） |
| 订单 | GET /api/order/list | 订单分页列表（支持多维筛选） |
| 订单 | GET /api/order/detail/{id} | 订单详情（含明细） |
| 订单 | POST /api/order/push | 推送订单 |
| 订单 | DELETE /api/order/{id} | 删除订单（管理员，已推送不可删） |
| 用户 | GET /api/user/list | 用户列表（管理员） |
| 用户 | POST /api/user | 新增用户（管理员） |
| 用户 | PUT /api/user/{id} | 更新用户（管理员） |
| 用户 | PUT /api/user/{id}/reset-password | 重置密码（管理员） |
| 用户 | DELETE /api/user/{id} | 删除用户（管理员） |
| 配置 | GET /api/config | 配置列表（管理员） |
| 配置 | PUT /api/config/{id} | 更新配置（管理员） |
| 看板 | GET /api/dashboard/summary | 数据看板汇总 |

## 统一返回格式

```json
{ "code": 200, "success": true, "message": "成功", "data": {} }
```