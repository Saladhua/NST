using System.Security.Claims;
using OrderPlatform.Domain.Entities;

namespace OrderPlatform.Application.Auth;

/// <summary>JWT 令牌服务接口：签发访问令牌/刷新令牌，并校验刷新令牌。</summary>
public interface ITokenService
{
    /// <summary>访问令牌有效期（分钟）。</summary>
    int AccessTokenExpireMinutes { get; }

    /// <summary>签发访问令牌（含用户 ID、用户名、角色）。</summary>
    string GenerateAccessToken(User user);

    /// <summary>签发刷新令牌（仅含用户 ID）。</summary>
    string GenerateRefreshToken(User user);

    /// <summary>校验刷新令牌，合法则返回其声明主体，否则返回 null。</summary>
    ClaimsPrincipal? ValidateRefreshToken(string refreshToken);
}