using OrderPlatform.Domain.Entities;

namespace OrderPlatform.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsByUserNameAsync(string userName, CancellationToken cancellationToken);

    Task AddAsync(User user, CancellationToken cancellationToken);

    Task<List<User>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<int> CountAsync(CancellationToken cancellationToken);

    void Update(User user);

    void Delete(User user);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
