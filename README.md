# 耐世拓订单平台 - 技术验证项目（登录功能）

本项目位于 `E:\开发试验工具\技术实验\耐世拓\`，用于验证订单平台登录认证技术链路。

## 技术栈

ASP.NET Core 8 Web API + EF Core + SQL Server 2022 + JWT + BCrypt

## 项目结构

```
OrderPlatform.sln
└── src/
    ├── OrderPlatform.Api            Web API 入口（Program、AuthController、统一返回过滤器）
    ├── OrderPlatform.Application    AuthService、DTO、FluentValidation 校验、AutoMapper
    ├── OrderPlatform.Domain         User 实体、角色/状态枚举、仓储接口
    ├── OrderPlatform.Infrastructure EF Core DbContext、JWT 签发、BCrypt 哈希、种子数据
    └── OrderPlatform.Shared         ApiResponse<T> 统一返回格式
```

## 启动

```powershell
dotnet run --project src\OrderPlatform.Api
```

- Swagger: http://localhost:5080/swagger
- 首次启动自动创建数据库 `OrderPlatform` 并写入种子管理员 `admin / 123456`

## 接口

| 接口 | 说明 |
|------|------|
| POST /api/auth/login | 登录，返回 AccessToken（30分钟）+ RefreshToken（7天） |
| POST /api/auth/refresh | 刷新令牌 |
| POST /api/auth/register | 注册（普通用户） |
| POST /api/auth/change-password | 修改密码（需登录） |

## 统一返回格式

```json
{ "code": 200, "success": true, "message": "成功", "data": {} }
```
