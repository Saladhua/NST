namespace OrderPlatform.Application.Auth.Dtos;

/// <summary>登录/刷新成功后的返回结果。</summary>
public class AuthResult
{
    /// <summary>访问令牌（约 30 分钟有效）。</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>刷新令牌（约 7 天有效）。</summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>访问令牌过期时间。</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>当前登录用户信息。</summary>
    public UserInfoDto UserInfo { get; set; } = new();
}