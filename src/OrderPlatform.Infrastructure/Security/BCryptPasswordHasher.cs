using OrderPlatform.Application.Auth;

namespace OrderPlatform.Infrastructure.Security;

/// <summary>基于 BCrypt 的密码哈希实现（工作因子 12）。</summary>
public class BCryptPasswordHasher : IPasswordHasher
{
    /// <summary>对明文密码生成 BCrypt 哈希。</summary>
    public string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    /// <summary>校验明文密码与哈希是否匹配。</summary>
    public bool Verify(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}