using OrderPlatform.Domain.Enums;

namespace OrderPlatform.Domain.Entities;

/// <summary>系统用户实体。</summary>
public class User
{
    /// <summary>用户唯一标识。</summary>
    public Guid Id { get; set; }

    /// <summary>登录用户名（唯一）。</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>BCrypt 加密后的密码哈希。</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>显示名称。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>手机号。</summary>
    public string? Phone { get; set; }

    /// <summary>邮箱。</summary>
    public string? Email { get; set; }

    /// <summary>角色：Admin 管理员 / User 普通用户。</summary>
    public UserRole Role { get; set; }

    /// <summary>账号状态：Active 启用 / Disabled 禁用。</summary>
    public UserStatus Status { get; set; }

    /// <summary>创建时间。</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>更新时间。</summary>
    public DateTime? UpdatedAt { get; set; }
}