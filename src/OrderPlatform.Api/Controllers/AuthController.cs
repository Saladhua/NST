using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using OrderPlatform.Application.Auth;
using OrderPlatform.Application.Auth.Dtos;
using OrderPlatform.Shared.Api;

namespace OrderPlatform.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ApiResponse<AuthResult>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        return ApiResponse<AuthResult>.Ok(result, "登录成功");
    }

    [HttpPost("refresh")]
    public async Task<ApiResponse<AuthResult>> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshAsync(request, cancellationToken);
        return ApiResponse<AuthResult>.Ok(result, "刷新成功");
    }

    [HttpPost("register")]
    public async Task<ApiResponse<object>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        await _authService.RegisterAsync(request, cancellationToken);
        return ApiResponse<object>.Ok(null, "注册成功");
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<ApiResponse<object>> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            throw new BusinessException("登录状态无效，请重新登录", 401);
        }

        await _authService.ChangePasswordAsync(userId, request, cancellationToken);
        return ApiResponse<object>.Ok(null, "密码修改成功");
    }
}
