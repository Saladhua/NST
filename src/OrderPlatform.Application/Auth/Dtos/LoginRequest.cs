namespace OrderPlatform.Application.Auth.Dtos;

/// <summary>登录请求参数。</summary>
public class LoginRequest
{
    /// <summary>用户名。</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>密码。</summary>
    public string Password { get; set; } = string.Empty;
}