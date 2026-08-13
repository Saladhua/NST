using OrderPlatform.Domain.Entities;

namespace OrderPlatform.Application.Auth;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}
