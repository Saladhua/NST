using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Infrastructure.Persistence;

namespace OrderPlatform.Infrastructure.Repositories;

/// <summary>订单推送日志仓储实现。</summary>
public class OrderPushLogRepository : IOrderPushLogRepository
{
    private readonly OrderDbContext _dbContext;

    public OrderPushLogRepository(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>新增推送日志。</summary>
    public async Task AddAsync(OrderPushLog log, CancellationToken cancellationToken)
    {
        await _dbContext.OrderPushLogs.AddAsync(log, cancellationToken);
    }

    /// <summary>保存变更。</summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}