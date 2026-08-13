namespace OrderPlatform.Application.Auth.Dtos;

public class UserInfoDto
{
    public Guid Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string Role { get; set; } = string.Empty;
}
