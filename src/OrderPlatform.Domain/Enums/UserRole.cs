namespace OrderPlatform.Domain.Enums;

/// <summary>用户角色。</summary>
public enum UserRole
{
    /// <summary>管理员（可管理用户、配置、删除订单等）。</summary>
    Admin,

    /// <summary>普通用户。</summary>
    User
}