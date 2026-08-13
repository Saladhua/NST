using Microsoft.EntityFrameworkCore;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Infrastructure.Persistence;

namespace OrderPlatform.Infrastructure.Repositories;

public class CustomerPartRepository : ICustomerPartRepository
{
    private readonly OrderDbContext _dbContext;

    public CustomerPartRepository(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<CustomerPart>> ListByCustomerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        return _dbContext.CustomerParts
            .Where(x => x.CustomerId == customerId)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteByCustomerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var parts = await _dbContext.CustomerParts
            .Where(x => x.CustomerId == customerId)
            .ToListAsync(cancellationToken);
        _dbContext.CustomerParts.RemoveRange(parts);
    }

    public async Task AddRangeAsync(List<CustomerPart> parts, CancellationToken cancellationToken)
    {
        await _dbContext.CustomerParts.AddRangeAsync(parts, cancellationToken);
    }

    public Task<CustomerPart?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.CustomerParts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public void Update(CustomerPart part)
    {
        _dbContext.CustomerParts.Update(part);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
