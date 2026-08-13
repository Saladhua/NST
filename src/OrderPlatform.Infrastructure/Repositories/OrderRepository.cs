using Microsoft.EntityFrameworkCore;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Enums;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Infrastructure.Persistence;

namespace OrderPlatform.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly OrderDbContext _dbContext;

    public OrderRepository(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<OrderMain?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Orders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<OrderMain?> GetByOrderNoAsync(string orderNo, CancellationToken cancellationToken)
    {
        return _dbContext.Orders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken);
    }

    public Task<List<OrderMain>> ListBySourceFileIdAsync(Guid sourceFileId, CancellationToken cancellationToken)
    {
        return _dbContext.Orders
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.SourceFileId == sourceFileId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<List<OrderMain>> ListPendingMatchAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Orders
            .Include(x => x.Items)
            .Where(x => x.ParseStatus != MatchStatus.Matched)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<OrderMain> ApplyFilter(
        IQueryable<OrderMain> query,
        string? keyword,
        Guid? customerId,
        PushStatus? pushStatus,
        MatchStatus? parseStatus)
    {
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.OrderNo.Contains(keyword));
        }

        if (customerId.HasValue)
        {
            query = query.Where(x => x.CustomerId == customerId.Value);
        }

        if (pushStatus.HasValue)
        {
            query = query.Where(x => x.PushStatus == pushStatus.Value);
        }

        if (parseStatus.HasValue)
        {
            query = query.Where(x => x.ParseStatus == parseStatus.Value);
        }

        return query;
    }

    public Task<List<OrderMain>> QueryAsync(
        int page,
        int pageSize,
        string? keyword,
        Guid? customerId,
        PushStatus? pushStatus,
        MatchStatus? parseStatus,
        CancellationToken cancellationToken)
    {
        var query = ApplyFilter(_dbContext.Orders.AsNoTracking(), keyword, customerId, pushStatus, parseStatus);

        return query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        string? keyword,
        Guid? customerId,
        PushStatus? pushStatus,
        MatchStatus? parseStatus,
        CancellationToken cancellationToken)
    {
        var query = ApplyFilter(_dbContext.Orders.AsNoTracking(), keyword, customerId, pushStatus, parseStatus);

        return query.CountAsync(cancellationToken);
    }

    public async Task AddAsync(OrderMain order, CancellationToken cancellationToken)
    {
        await _dbContext.Orders.AddAsync(order, cancellationToken);
    }

    public void Update(OrderMain order)
    {
        _dbContext.Orders.Update(order);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (order is null)
        {
            return false;
        }

        _dbContext.OrderItems.RemoveRange(order.Items);
        _dbContext.Orders.Remove(order);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
