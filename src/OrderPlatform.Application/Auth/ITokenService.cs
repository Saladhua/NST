using System.Security.Claims;
using OrderPlatform.Domain.Entities;

namespace OrderPlatform.Application.Auth;

public interface ITokenService
{
    int AccessTokenExpireMinutes { get; }

    string GenerateAccessToken(User user);

    string GenerateRefreshToken(User user);

    ClaimsPrincipal? ValidateRefreshToken(string refreshToken);
}
