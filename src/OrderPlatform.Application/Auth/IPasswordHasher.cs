namespace OrderPlatform.Application.Auth;

/// <summary>密码哈希服务接口（BCrypt 实现）。</summary>
public interface IPasswordHasher
{
    /// <summary>对明文密码进行哈希。</summary>
    string Hash(string password);

    /// <summary>校验明文密码与哈希是否匹配。</summary>
    bool Verify(string password, string hash);
}