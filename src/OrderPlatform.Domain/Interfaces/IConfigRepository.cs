using OrderPlatform.Domain.Entities;

namespace OrderPlatform.Domain.Interfaces;

public interface IConfigRepository
{
    Task<List<SysConfig>> ListAsync(CancellationToken cancellationToken);

    Task<SysConfig?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<SysConfig?> GetByKeyAsync(string key, CancellationToken cancellationToken);

    void Update(SysConfig config);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
