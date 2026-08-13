using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OrderPlatform.Application.Auth;
using OrderPlatform.Domain.Entities;

namespace OrderPlatform.Infrastructure.Security;

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

    public int AccessTokenExpireMinutes => _options.AccessTokenExpireMinutes;

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
