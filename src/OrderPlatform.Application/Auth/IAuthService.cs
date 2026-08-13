using OrderPlatform.Application.Auth.Dtos;

namespace OrderPlatform.Application.Auth;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<AuthResult> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken);

    Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken);
}
