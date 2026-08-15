namespace OrderPlatform.Application.Auth.Dtos;

/// <summary>刷新令牌请求参数。</summary>
public class RefreshRequest
{
    /// <summary>刷新令牌。</summary>
    public string RefreshToken { get; set; } = string.Empty;
}