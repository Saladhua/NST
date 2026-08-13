namespace OrderPlatform.Application.Auth.Dtos;

public class AuthResult
{
    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public UserInfoDto UserInfo { get; set; } = new();
}
