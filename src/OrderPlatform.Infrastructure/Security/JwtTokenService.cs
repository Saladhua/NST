using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OrderPlatform.Application.Auth;
using OrderPlatform.Domain.Entities;

namespace OrderPlatform.Infrastructure.Security;

/// <summary>JWT 令牌服务实现：签发访问令牌与刷新令牌、校验刷新令牌。</summary>
public class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly SymmetricSecurityKey _key;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(IOptions<JwtOptions> options, ILogger<JwtTokenService> logger)
    {
        _options = options.Value;
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        _logger = logger;
    }

    /// <summary>访问令牌有效期（分钟）。</summary>
    public int AccessTokenExpireMinutes => _options.AccessTokenExpireMinutes;

    /// <summary>签发访问令牌：含用户 ID、用户名、角色声明。</summary>
    public string GenerateAccessToken(User user)
    {
        var now = DateTime.Now;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Name, user.UserName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("token_type", "access")
        };

        return GenerateToken(claims, now, now.AddMinutes(_options.AccessTokenExpireMinutes));
    }

    /// <summary>签发刷新令牌：仅含用户 ID。</summary>
    public string GenerateRefreshToken(User user)
    {
        var now = DateTime.Now;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("token_type", "refresh")
        };

        return GenerateToken(claims, now, now.AddDays(_options.RefreshTokenExpireDays));
    }

    /// <summary>校验刷新令牌：合法且类型为 refresh 时返回声明主体，否则返回 null。</summary>
    public ClaimsPrincipal? ValidateRefreshToken(string refreshToken)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler
            {
                MapInboundClaims = false
            };
            var principal = handler.ValidateToken(
                refreshToken,
                _options.BuildTokenValidationParameters(),
                out _);

            return principal.FindFirstValue("token_type") == "refresh" ? principal : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "刷新令牌验证失败");
            return null;
        }
    }

    /// <summary>按 HS256 生成并序列化 JWT。</summary>
    private string GenerateToken(IEnumerable<Claim> claims, DateTime notBefore, DateTime expires)
    {
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: notBefore,
            expires: expires,
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}