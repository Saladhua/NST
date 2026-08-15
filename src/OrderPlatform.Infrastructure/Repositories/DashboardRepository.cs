using Microsoft.EntityFrameworkCore;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Infrastructure.Persistence;

namespace OrderPlatform.Infrastructure.Repositories;

/// <summary>数据看板仓储实现。</summary>
public class DashboardRepository : IDashboardRepository
{
    private readonly OrderDbContext _dbContext;

    public DashboardRepository(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>查询全部订单（无跟踪）。</summary>
    public Task<List<OrderMain>> GetAllOrdersAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Orders.AsNoTracking().ToListAsync(cancellationToken);
    }

    /// <summary>查询全部未删除客户（无跟踪）。</summary>
    public Task<List<Customer>> GetAllCustomersAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Customers.AsNoTracking().Where(x => !x.IsDeleted).ToListAsync(cancellationToken);
    }
}