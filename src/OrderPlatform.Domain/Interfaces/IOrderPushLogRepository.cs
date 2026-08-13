using OrderPlatform.Domain.Entities;

namespace OrderPlatform.Domain.Interfaces;

public interface IOrderPushLogRepository
{
    Task AddAsync(OrderPushLog log, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
