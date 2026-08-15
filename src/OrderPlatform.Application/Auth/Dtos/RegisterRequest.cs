namespace OrderPlatform.Application.Auth.Dtos;

/// <summary>注册请求参数（注册用户默认为普通用户角色）。</summary>
public class RegisterRequest
{
    /// <summary>用户名（3-50 字符）。</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>密码（6-50 字符）。</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>显示名称。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>手机号（可选）。</summary>
    public string? Phone { get; set; }

    /// <summary>邮箱（可选）。</summary>
    public string? Email { get; set; }
}