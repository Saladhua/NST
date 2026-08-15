using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Enums;

namespace OrderPlatform.Application.Users;

/// <summary>用户列表项。</summary>
public class UserListDto
{
    /// <summary>用户 ID。</summary>
    public Guid Id { get; set; }

    /// <summary>用户名。</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>显示名称。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>手机号。</summary>
    public string? Phone { get; set; }

    /// <summary>邮箱。</summary>
    public string? Email { get; set; }

    /// <summary>角色。</summary>
    public UserRole Role { get; set; }

    /// <summary>状态。</summary>
    public UserStatus Status { get; set; }

    /// <summary>创建时间。</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>新增用户请求。</summary>
public class CreateUserRequest
{
    /// <summary>用户名。</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>初始密码。</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>显示名称。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>手机号。</summary>
    public string? Phone { get; set; }

    /// <summary>邮箱。</summary>
    public string? Email { get; set; }

    /// <summary>角色。</summary>
    public UserRole Role { get; set; }
}

/// <summary>更新用户请求。</summary>
public class UpdateUserRequest
{
    /// <summary>显示名称。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>手机号。</summary>
    public string? Phone { get; set; }

    /// <summary>邮箱。</summary>
    public string? Email { get; set; }

    /// <summary>角色。</summary>
    public UserRole Role { get; set; }

    /// <summary>状态。</summary>
    public UserStatus Status { get; set; }
}

/// <summary>重置密码请求。</summary>
public class ResetPasswordRequest
{
    /// <summary>新密码。</summary>
    public string NewPassword { get; set; } = string.Empty;
}