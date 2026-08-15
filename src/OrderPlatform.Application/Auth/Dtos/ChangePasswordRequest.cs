namespace OrderPlatform.Application.Auth.Dtos;

/// <summary>修改密码请求参数。</summary>
public class ChangePasswordRequest
{
    /// <summary>原密码。</summary>
    public string OldPassword { get; set; } = string.Empty;

    /// <summary>新密码（6-50 字符，不能与原密码相同）。</summary>
    public string NewPassword { get; set; } = string.Empty;
}