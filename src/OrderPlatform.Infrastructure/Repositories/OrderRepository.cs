using Microsoft.EntityFrameworkCore;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Enums;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Infrastructure.Persistence;

namespace OrderPlatform.Infrastructure.Repositories;

/// <summary>订单仓储实现：订单查询、筛选、新增、删除（联动明细与推送日志）。</summary>
public class OrderRepository : IOrderRepository
{
    private readonly OrderDbContext _dbContext;

    public OrderRepository(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>按 ID 查询订单（含明细）。</summary>
    public Task<OrderMain?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Orders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <summary>按订单号查询订单（含明细），用于重复导入去重。</summary>
    public Task<OrderMain?> GetByOrderNoAsync(string orderNo, CancellationToken cancellationToken)
    {
        return _dbContext.Orders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken);
    }

    /// <summary>按来源批次查询订单（含明细），按创建时间倒序。</summary>
    public Task<List<OrderMain>> ListBySourceFileIdAsync(Guid sourceFileId, CancellationToken cancellationToken)
    {
        return _dbContext.Orders
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.SourceFileId == sourceFileId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>批量统计各来源批次生成的订单数。</summary>
    public async Task<Dictionary<Guid, int>> CountBySourceFileIdsAsync(
        IEnumerable<Guid> sourceFileIds,
        CancellationToken cancellationToken)
    {
        var ids = sourceFileIds as ICollection<Guid> ?? sourceFileIds.ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var result = await _dbContext.Orders
            .AsNoTracking()
            .Where(x => x.SourceFileId.HasValue && ids.Contains(x.SourceFileId.Value))
            .GroupBy(x => x.SourceFileId!.Value)
            .Select(g => new { SourceFileId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SourceFileId, x => x.Count, cancellationToken);
        return result;
    }

    /// <summary>查询未完全关联的订单（用于 Excel 导入后补匹配）。</summary>
    public Task<List<OrderMain>> ListPendingMatchAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Orders
            .Include(x => x.Items)
            .Where(x => x.ParseStatus != MatchStatus.Matched)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>应用订单筛选条件（关键词/客户/推送状态/关联状态）。</summary>
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

    /// <summary>分页查询订单列表（按创建时间倒序）。</summary>
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

    /// <summary>统计筛选条件下的订单总数。</summary>
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

    /// <summary>新增订单。</summary>
    public async Task AddAsync(OrderMain order, CancellationToken cancellationToken)
    {
        await _dbContext.Orders.AddAsync(order, cancellationToken);
    }

    /// <summary>更新订单。</summary>
    public void Update(OrderMain order)
    {
        _dbContext.Orders.Update(order);
    }

    /// <summary>删除订单，并联动清理其明细与推送日志。</summary>
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
        var pushLogs = await _dbContext.OrderPushLogs
            .Where(x => x.OrderId == id)
            .ToListAsync(cancellationToken);
        _dbContext.OrderPushLogs.RemoveRange(pushLogs);
        _dbContext.Orders.Remove(order);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>按客户统计订单明细中已匹配（非空客户图号）的去重图号数量。</summary>
    public async Task<Dictionary<Guid, int>> CountMatchedPartNosByCustomersAsync(
        IEnumerable<Guid> customerIds,
        CancellationToken cancellationToken)
    {
        var ids = customerIds as ICollection<Guid> ?? customerIds.ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var rows = await _dbContext.Orders
            .AsNoTracking()
            .Where(o => ids.Contains(o.CustomerId))
            .SelectMany(o => o.Items, (o, i) => new { o.CustomerId, PartNo = i.CustomerPartNo })
            .ToListAsync(cancellationToken);

        return rows
            .Where(x => !string.IsNullOrWhiteSpace(x.PartNo))
            .GroupBy(x => x.CustomerId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.PartNo).Distinct().Count());
    }

    /// <summary>保存变更。</summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}