using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using OrderPlatform.Application.Auth;
using OrderPlatform.Application.Auth.Dtos;
using OrderPlatform.Shared.Api;

namespace OrderPlatform.Api.Controllers;

/// <summary>认证接口：登录、刷新令牌、注册、修改密码。</summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>用户登录，返回访问令牌与刷新令牌。</summary>
    [HttpPost("login")]
    public async Task<ApiResponse<AuthResult>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        return ApiResponse<AuthResult>.Ok(result, "登录成功");
    }

    /// <summary>使用刷新令牌刷新登录状态。</summary>
    [HttpPost("refresh")]
    public async Task<ApiResponse<AuthResult>> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshAsync(request, cancellationToken);
        return ApiResponse<AuthResult>.Ok(result, "刷新成功");
    }

    /// <summary>注册普通用户。</summary>
    [HttpPost("register")]
    public async Task<ApiResponse<object>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        await _authService.RegisterAsync(request, cancellationToken);
        return ApiResponse<object>.Ok(null, "注册成功");
    }

    /// <summary>修改当前登录用户的密码。</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ApiResponse<object>> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        // 从 JWT 中解析当前用户 ID
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            throw new BusinessException("登录状态无效，请重新登录", 401);
        }

        await _authService.ChangePasswordAsync(userId, request, cancellationToken);
        return ApiResponse<object>.Ok(null, "密码修改成功");
    }
}