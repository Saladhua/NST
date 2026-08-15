namespace OrderPlatform.Application.Auth.Dtos;

/// <summary>当前登录用户信息。</summary>
public class UserInfoDto
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

    /// <summary>角色名称（Admin/User）。</summary>
    public string Role { get; set; } = string.Empty;
}