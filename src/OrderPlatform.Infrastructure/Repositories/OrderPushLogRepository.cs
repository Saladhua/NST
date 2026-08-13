using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Infrastructure.Persistence;

namespace OrderPlatform.Infrastructure.Repositories;

public class OrderPushLogRepository : IOrderPushLogRepository
{
    private readonly OrderDbContext _dbContext;

    public OrderPushLogRepository(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(OrderPushLog log, CancellationToken cancellationToken)
    {
        await _dbContext.OrderPushLogs.AddAsync(log, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
