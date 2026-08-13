using Microsoft.EntityFrameworkCore;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Infrastructure.Persistence;

namespace OrderPlatform.Infrastructure.Repositories;

public class DashboardRepository : IDashboardRepository
{
    private readonly OrderDbContext _dbContext;

    public DashboardRepository(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<OrderMain>> GetAllOrdersAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Orders.AsNoTracking().ToListAsync(cancellationToken);
    }

    public Task<List<Customer>> GetAllCustomersAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Customers.AsNoTracking().ToListAsync(cancellationToken);
    }
}
