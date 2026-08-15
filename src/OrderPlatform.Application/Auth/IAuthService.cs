using OrderPlatform.Application.Auth.Dtos;

namespace OrderPlatform.Application.Auth;

/// <summary>认证服务接口，提供登录、刷新令牌、注册、修改密码能力。</summary>
public interface IAuthService
{
    /// <summary>用户登录，校验用户名/密码/账号状态并返回令牌。</summary>
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>使用刷新令牌换取新的访问令牌与刷新令牌。</summary>
    Task<AuthResult> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken);

    /// <summary>注册新用户（普通用户角色）。</summary>
    Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    /// <summary>修改指定用户密码（需校验原密码）。</summary>
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken);
}