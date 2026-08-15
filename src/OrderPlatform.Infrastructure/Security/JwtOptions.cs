using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OrderPlatform.Infrastructure.Security;

/// <summary>JWT 配置项（对应 appsettings.json 的 Jwt 节）。</summary>
public class JwtOptions
{
    /// <summary>配置节名称。</summary>
    public const string SectionName = "Jwt";

    /// <summary>签名密钥。</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>签发方。</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>受众。</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>访问令牌有效期（分钟，默认 30）。</summary>
    public int AccessTokenExpireMinutes { get; set; } = 30;

    /// <summary>刷新令牌有效期（天，默认 7）。</summary>
    public int RefreshTokenExpireDays { get; set; } = 7;

    /// <summary>构建令牌校验参数。</summary>
    public TokenValidationParameters BuildTokenValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Name,
            RoleClaimType = ClaimTypes.Role
        };
    }
}