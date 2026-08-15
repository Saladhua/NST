using Microsoft.EntityFrameworkCore;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Infrastructure.Persistence;

namespace OrderPlatform.Infrastructure.Repositories;

/// <summary>系统配置仓储实现。</summary>
public class ConfigRepository : IConfigRepository
{
    private readonly OrderDbContext _dbContext;

    public ConfigRepository(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>查询全部配置（按键排序）。</summary>
    public Task<List<SysConfig>> ListAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SysConfigs.OrderBy(x => x.ConfigKey).ToListAsync(cancellationToken);
    }

    /// <summary>按 ID 查询配置。</summary>
    public Task<SysConfig?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.SysConfigs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <summary>按键查询配置。</summary>
    public Task<SysConfig?> GetByKeyAsync(string key, CancellationToken cancellationToken)
    {
        return _dbContext.SysConfigs.FirstOrDefaultAsync(x => x.ConfigKey == key, cancellationToken);
    }

    /// <summary>更新配置。</summary>
    public void Update(SysConfig config)
    {
        _dbContext.SysConfigs.Update(config);
    }

    /// <summary>保存变更。</summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}