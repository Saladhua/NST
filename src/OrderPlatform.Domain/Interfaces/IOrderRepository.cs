using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Enums;

namespace OrderPlatform.Domain.Interfaces;

public interface IOrderRepository
{
    Task<OrderMain?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<List<OrderMain>> QueryAsync(
        int page,
        int pageSize,
        string? keyword,
        Guid? customerId,
        PushStatus? pushStatus,
        MatchStatus? parseStatus,
        CancellationToken cancellationToken);

    Task<int> CountAsync(
        string? keyword,
        Guid? customerId,
        PushStatus? pushStatus,
        MatchStatus? parseStatus,
        CancellationToken cancellationToken);

    Task AddAsync(OrderMain order, CancellationToken cancellationToken);

    void Update(OrderMain order);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
