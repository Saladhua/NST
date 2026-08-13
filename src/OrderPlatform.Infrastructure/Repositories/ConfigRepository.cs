using Microsoft.EntityFrameworkCore;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Infrastructure.Persistence;

namespace OrderPlatform.Infrastructure.Repositories;

public class ConfigRepository : IConfigRepository
{
    private readonly OrderDbContext _dbContext;

    public ConfigRepository(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<SysConfig>> ListAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SysConfigs.OrderBy(x => x.ConfigKey).ToListAsync(cancellationToken);
    }

    public Task<SysConfig?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.SysConfigs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<SysConfig?> GetByKeyAsync(string key, CancellationToken cancellationToken)
    {
        return _dbContext.SysConfigs.FirstOrDefaultAsync(x => x.ConfigKey == key, cancellationToken);
    }

    public void Update(SysConfig config)
    {
        _dbContext.SysConfigs.Update(config);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
