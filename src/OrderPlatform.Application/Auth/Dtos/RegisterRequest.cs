namespace OrderPlatform.Application.Auth.Dtos;

public class RegisterRequest
{
    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }
}
