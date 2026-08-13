using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Enums;

namespace OrderPlatform.Application.Users;

public class UserListDto
{
    public Guid Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public UserRole Role { get; set; }

    public UserStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class CreateUserRequest
{
    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public UserRole Role { get; set; }
}

public class UpdateUserRequest
{
    public string DisplayName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public UserRole Role { get; set; }

    public UserStatus Status { get; set; }
}

public class ResetPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}